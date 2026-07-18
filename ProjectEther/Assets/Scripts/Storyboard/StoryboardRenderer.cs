using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.Video;
using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using OsuVR.Storyboard.Data;
using OsuVR.Storyboard.Engine;

namespace OsuVR.Storyboard
{
    /// <summary>
    /// 纯 GPU 实例化 Storyboard 渲染器 (多线程版)
    ///
    /// 架构:
    ///   - Update(): 推进引擎 → 收集精灵到 NativeArray → Schedule BuildInstanceJob
    ///   - LateUpdate(): Complete Job → ComputeBuffer.SetData(NativeArray) → GPU 提交
    ///   - 零 GC 运行时: 所有 NativeArray 使用 Allocator.Persistent 预分配
    ///   - GPU 剔除法: 不可见精灵 Scale→zero, 由 Vertex Shader 瞬间剔除
    ///   - 双 Pass 共享缓冲区: Alpha/Blend/Pass 0 + Additive/Pass 1
    ///
    /// 视频渲染:
    ///   - VideoPlayer 直接输出到独立 RenderTexture (VideoRenderMode.RenderTexture)
    ///   - 视频 RT 由 HolographicScreenManager 的视频 Overlay 层独立显示
    ///   - 视频和 SB 完全解耦, 不经过 Graphics.DrawMesh
    /// </summary>
    public class StoryboardRenderer : MonoBehaviour
    {
        public static StoryboardRenderer Instance { get; private set; }

        // ---- osu! 标准画布 ----
        const int CanvasWidth = 640;
        const int CanvasHeight = 480;
        const int RT_Width = 1920;
        const int RT_Height = 1080;

        // ---- 隔离坐标 ----
        static readonly Vector3 IsolatedPosition = new Vector3(0, -1000f, 0);

        // ---- Layer ----
        const string LayerName = "Storyboard";
        const int FallbackLayer = 31;

        // ---- GPU 实例化参数 ----
        const int MaxInstances = 8192;       // 单一缓冲区容量 (Alpha + Additive 共享)
        const int InstanceDataStride = 96;   // sizeof(SpriteInstanceData)

        const float SBQuadZ = 0f;

        // =========================================================
        //  GPU 实例数据结构 (与 SBInstanced.shader 精确对齐)
        // =========================================================

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        struct SpriteInstanceData
        {
            public Matrix4x4 objectToWorld; // 64 bytes
            public Vector4 color;           // 16 bytes
            public Vector4 params0;         // 16 bytes (x=texIndex, y=blendMode, z=flipH, w=flipV)
        }   // Total: 96 bytes

        // SpriteInputData 已移至 SBFlatData.cs (OsuVR.Storyboard.Engine 命名空间)

        static readonly SpriteInstanceData ZeroInstance = new SpriteInstanceData
        {
            objectToWorld = new Matrix4x4(),
            color = Vector4.zero,
            params0 = Vector4.zero
        };

        // =========================================================
        //  Burst Job: 并行矩阵计算 + GPU 剔除
        // =========================================================

        [BurstCompile]
        struct BuildInstanceJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<SpriteInputData> Inputs;
            [ReadOnly] public NativeArray<float2> OriginOffsets;
            [WriteOnly] public NativeArray<SpriteInstanceData> Output;

            public int CanvasW, CanvasH;
            public float IsolatedY;
            public int InputCount;

            public void Execute(int i)
            {
                if (i >= InputCount)
                {
                    Output[i] = ZeroInstance;
                    return;
                }

                var input = Inputs[i];

                // GPU 剔除法: 不可见精灵 Scale→zero, Vertex Shader 瞬间剔除
                float alpha = math.min(input.Alpha, 1f);
                if (alpha <= 0.001f || input.TexIndex < 0)
                {
                    Output[i] = ZeroInstance;
                    return;
                }

                // 矩阵构建 (纯 Burst 数学)
                float x = input.X - CanvasW * 0.5f;
                float y = -(input.Y - CanvasH * 0.5f) + IsolatedY;

                float scaleX = input.TexWidth * input.ScaleX;
                float scaleY = input.TexHeight * input.ScaleY;

                int originIdx = input.OriginIndex;
                if ((uint)originIdx >= (uint)OriginOffsets.Length) originIdx = 1;
                float2 pivot = OriginOffsets[originIdx];

                // AdjustOrigin: flip XOR negative scale
                if ((input.FlipH != 0) ^ (input.ScaleX < 0)) pivot.x = -pivot.x;
                if ((input.FlipV != 0) ^ (input.ScaleY < 0)) pivot.y = -pivot.y;

                Matrix4x4 m = Matrix4x4.identity;

                if (input.Rotation == 0f)
                {
                    // 快速路径: 无旋转
                    m.m00 = scaleX;
                    m.m11 = scaleY;
                    m.m03 = x - pivot.x * scaleX;
                    m.m13 = y - pivot.y * scaleY;
                }
                else
                {
                    float cosR = math.cos(-input.Rotation);
                    float sinR = math.sin(-input.Rotation);
                    float px = pivot.x * scaleX;
                    float py = pivot.y * scaleY;
                    m.m00 = scaleX * cosR;
                    m.m01 = -scaleY * sinR;
                    m.m03 = x - (px * cosR - py * sinR);
                    m.m10 = scaleX * sinR;
                    m.m11 = scaleY * cosR;
                    m.m13 = y - (px * sinR + py * cosR);
                }

                m.m23 = SBQuadZ;

                Output[i] = new SpriteInstanceData
                {
                    objectToWorld = m,
                    color = new float4(input.R, input.G, input.B, alpha),
                    params0 = new float4(
                        input.TexIndex,
                        input.Additive != 0 ? 1f : 0f,
                        ((input.FlipH != 0) ^ (input.ScaleX < 0)) ? 1f : 0f,
                        ((input.FlipV != 0) ^ (input.ScaleY < 0)) ? 1f : 0f)
                };
            }
        }

        // =========================================================
        //  运行时对象
        // =========================================================

        Camera renderCamera;
        RenderTexture renderTexture;
        GameObject isolatedRoot;
        int storyboardLayer;

        // ---- 引擎 (DOD 扁平化管线) ----
        SBFlatTimelineData _flatTimeline;
        string currentBeatmapFolder;

        // ---- 引擎 (旧管线, 仅保留用于向后兼容) ----
        SBOsbPlayer osbPlayer;

        // ---- 视频 (VideoPlayer 直接解码到 RenderTexture) ----
        VideoPlayer videoPlayer;
        RenderTexture videoRT;
        bool hasVideo;
        int videoOffsetMs;

        // ---- 纹理数组 ----
        Texture2DArray textureArray;
        Dictionary<string, int> textureIndexMap;
        Vector2Int[] textureDimensions;
        int[] cachedTextureIndex;

        // ---- GPU 缓冲区 ----
        ComputeBuffer alphaBuffer;
        ComputeBuffer additiveBuffer;

        // ---- 共享资源 ----
        Material sbMaterialAlpha;    // Pass 0: Blend SrcAlpha OneMinusSrcAlpha (标准混合)
        Material sbMaterialAdditive; // Pass 1: Blend One One (加法混合)
        Mesh quadMesh;

        // ---- 状态 ----
        bool isRendering;

        // ---- Origin 偏移缓存 ----
        static readonly Vector2[] OriginOffsets = new Vector2[]
        {
            new Vector2(-0.5f,  0.5f),  // [0] TopLeft
            new Vector2( 0.0f,  0.0f),  // [1] Centre
            new Vector2(-0.5f,  0.0f),  // [2] CentreLeft
            new Vector2( 0.5f,  0.5f),  // [3] TopRight
            new Vector2( 0.0f, -0.5f),  // [4] BottomCentre
            new Vector2( 0.0f,  0.5f),  // [5] TopCentre
            new Vector2(-0.5f,  0.5f),  // [6] Custom → fallback to TopLeft
            new Vector2( 0.5f,  0.0f),  // [7] CentreRight
            new Vector2(-0.5f, -0.5f),  // [8] BottomLeft
            new Vector2( 0.5f, -0.5f),  // [9] BottomRight
        };

        // =========================================================
        //  Job System 状态
        // =========================================================

        NativeArray<SpriteInputData> _jobInputs;
        NativeArray<SpriteInstanceData> _jobOutput;
        NativeArray<float2> _jobOriginOffsets;
        int _jobActiveCount;
        bool _jobScheduled;
        JobHandle _jobHandle;

        // =========================================================
        //  Lifecycle
        // =========================================================

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void AutoInitialize()
        {
            if (Instance == null)
            {
                var go = new GameObject("[StoryboardRenderer]");
                go.AddComponent<StoryboardRenderer>();
            }
        }

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // GPU 缓冲区
            alphaBuffer = new ComputeBuffer(MaxInstances, InstanceDataStride);
            additiveBuffer = new ComputeBuffer(MaxInstances, InstanceDataStride);

            // Job System Persistent 预分配 (约束 2: 绝对禁止每帧 New/Dispose)
            InitializeJobSystem();

            EnsureLayerExists();
        }

        void InitializeJobSystem()
        {
            _jobInputs = new NativeArray<SpriteInputData>(MaxInstances, Allocator.Persistent);
            _jobOutput = new NativeArray<SpriteInstanceData>(MaxInstances, Allocator.Persistent);

            // Origin offsets: 静态只读, Persistent
            _jobOriginOffsets = new NativeArray<float2>(OriginOffsets.Length, Allocator.Persistent);
            for (int i = 0; i < OriginOffsets.Length; i++)
                _jobOriginOffsets[i] = new float2(OriginOffsets[i].x, OriginOffsets[i].y);

            _jobScheduled = false;
        }

        void DisposeJobSystem()
        {
            if (_jobScheduled)
            {
                _jobHandle.Complete();
                _jobScheduled = false;
            }

            if (_jobInputs.IsCreated) _jobInputs.Dispose();
            if (_jobOutput.IsCreated) _jobOutput.Dispose();
            if (_jobOriginOffsets.IsCreated) _jobOriginOffsets.Dispose();
        }

        // =========================================================
        //  公开 API
        // =========================================================

        public void LoadStoryboard(SBStoryboard storyboard, string beatmapFolder, bool widescreen = false)
        {
            UnloadStoryboard();

            if (storyboard == null || storyboard.TotalElementCount == 0)
            {
                Debug.Log("[SBRenderer] Storyboard 为空，跳过加载");
                return;
            }

            SBDebugLog.Begin();
            SBDebugLog.Mem("LoadStoryboard 开始");
            SBDebugLog.Log($"元素数={storyboard.TotalElementCount}");

            EnsureCameraSetup();
            CacheRhythmGameManager();
            currentBeatmapFolder = beatmapFolder;

            BuildTextureArray(storyboard, beatmapFolder);
            SBDebugLog.Mem("纹理打包完成");

            EnsureSBMaterial();

            // DOD 管线: 扁平化所有 SB 数据到 NativeArray
            _flatTimeline = SBTimelineFlattener.Flatten(storyboard, textureIndexMap, textureDimensions);
            SBDebugLog.Mem($"DOD 扁平化完成: {_flatTimeline.SpriteCount} sprites");

            if (_flatTimeline.SpriteCount > MaxInstances)
            {
                Debug.LogWarning($"[SBRenderer] Sprite 数量 {_flatTimeline.SpriteCount} 超过单缓冲区上限 {MaxInstances}, " +
                                 $"超出部分将被截断不渲染");
            }

            // 旧管线保留 (用于旧代码路径兼容, 不再每帧调用)
            osbPlayer = new SBOsbPlayer();
            osbPlayer.LoadStoryboard(storyboard);

            CacheTextureIndices();

            isRendering = true;

            // SB Background 层: 有无 Fade 命令的 sprite → 全不透明替代背景, 隐藏背景图
            //                  所有 sprite 都有 Fade → 有透明度, 保留背景图让 SB alpha 叠加
            // 前提: sprite 的纹理必须实际加载成功, 否则 sprite 不可见, 隐藏背景会导致黑屏
            if (storyboard.Layers[0] != null && storyboard.Layers[0].Count > 0)
            {
                bool hasOpaqueSprite = false;
                foreach (var elem in storyboard.Layers[0])
                {
                    if (elem.FadeCommands.Count == 0 && HasLoadedTexture(elem))
                    {
                        hasOpaqueSprite = true;
                        break;
                    }
                }
                if (hasOpaqueSprite)
                {
                    HolographicScreenManager.Instance?.HideBackgroundForSB();
                    SBDebugLog.Log("[SBRenderer] Background 层有全不透明 sprite, 已隐藏谱面背景图");
                }
            }

            SBDebugLog.Log($"[SBRenderer] 加载完成: {storyboard.TotalElementCount} 元素, {(textureArray != null ? textureArray.depth : 0)} 纹理层");
            SBDebugLog.End();
        }

        /// <summary>
        /// 判断元素的纹理是否实际加载进了纹理数组 (动画: 至少一帧存在; 静态: 路径在 map 中)
        /// </summary>
        bool HasLoadedTexture(SBElement element)
        {
            if (textureIndexMap == null || string.IsNullOrEmpty(element.ImagePath))
                return false;

            if (element is SBStoryboardAnimation anim)
            {
                for (int f = 0; f < anim.FrameCount; f++)
                {
                    string key = anim.BuildFramePath(f).Replace('\\', '/').ToLowerInvariant();
                    if (textureIndexMap.ContainsKey(key))
                        return true;
                }
                return false;
            }

            return textureIndexMap.ContainsKey(element.ImagePath.Replace('\\', '/').ToLowerInvariant());
        }

        public void LoadVideo(string videoPath, int videoOffset)
        {
            if (string.IsNullOrEmpty(videoPath)) return;

            hasVideo = true;
            isRendering = true;   // 视频时间同步需要 Update 循环
            videoOffsetMs = videoOffset;

            // 纯视频模式也必须缓存 RhythmGameManager:
            // 否则 GetCurrentMusicTime 回退到 Time.time (游戏启动时间而非音乐时间),
            // targetVideoTime 远超视频长度 → SyncVideoTime 永远 Pause → 视频黑屏
            CacheRhythmGameManager();

            CreateVideoPlayer(videoPath);

            Debug.Log($"[SBRenderer] 已加载视频: {System.IO.Path.GetFileName(videoPath)}, offset={videoOffset}ms");
        }

        public void LoadVideoAndStoryboard(string videoPath, int videoOffset,
            SBStoryboard storyboard, string beatmapFolder, bool widescreen = false)
        {
            LoadStoryboard(storyboard, beatmapFolder, widescreen);
            LoadVideo(videoPath, videoOffset);
        }

        public void UnloadStoryboard()
        {
            isRendering = false;

            // 确保 Job 完成
            if (_jobScheduled)
            {
                _jobHandle.Complete();
                _jobScheduled = false;
            }

            // 释放 DOD 扁平化数据
            if (_flatTimeline.Sprites.IsCreated) _flatTimeline.Dispose();

            osbPlayer?.Unload();
            osbPlayer = null;

            if (textureArray != null) { Destroy(textureArray); textureArray = null; }
            textureIndexMap?.Clear();

            if (sbMaterialAlpha != null) { Destroy(sbMaterialAlpha); sbMaterialAlpha = null; }
            if (sbMaterialAdditive != null) { Destroy(sbMaterialAdditive); sbMaterialAdditive = null; }
        }

        public void UnloadVideo()
        {
            hasVideo = false;
            videoOffsetMs = 0;

            if (videoPlayer != null)
            {
                videoPlayer.Stop();
                Destroy(videoPlayer.gameObject);
                videoPlayer = null;
            }

            if (videoRT != null)
            {
                videoRT.Release();
                Destroy(videoRT);
                videoRT = null;
            }
        }

        public void UnloadAll()
        {
            UnloadStoryboard();
            UnloadVideo();
        }

        public RenderTexture GetRenderTexture() => renderTexture;
        public RenderTexture GetVideoRenderTexture() => videoRT;

        // =========================================================
        //  Update: 引擎推进 + 精灵收集 + Schedule Job
        //  (约束 1: 拉开调度间距, Update 极早阶段 Schedule)
        // =========================================================

        void Update()
        {
            if (!isRendering) return;
            if (_flatTimeline.SpriteCount == 0 && osbPlayer == null && !hasVideo) return;

            double musicTime = GetCurrentMusicTime();

            // 1. 视频: 时间同步 (VideoPlayer 直接解码到 videoRT)
            if (hasVideo && videoPlayer != null && videoPlayer.isPrepared)
                SyncVideoTime(musicTime);

            // 2. 以下需要 renderCamera (SB 渲染)
            if (renderCamera == null) return;

            // 3. DOD 管线: 两个 Burst Job 链式调度 (零主线程求值)
            if (_flatTimeline.SpriteCount > 0 && sbMaterialAlpha != null && textureArray != null)
            {
                _jobActiveCount = math.min(_flatTimeline.SpriteCount, MaxInstances);

                // Job 1: 时间轴求值 (替代 SBOsbPlayer.Update + CollectSpritesToNativeArray)
                var evalJob = new SBEvaluateTimelineJob
                {
                    Sprites = _flatTimeline.Sprites,
                    Commands = _flatTimeline.Commands,
                    Loops = _flatTimeline.Loops,
                    FrameMap = _flatTimeline.FrameMap,
                    Output = _jobInputs,
                    CurrentTime = musicTime,
                    SpriteCount = _jobActiveCount
                };
                var evalHandle = evalJob.Schedule(_jobActiveCount, 64);

                // Job 2: 矩阵计算 (依赖 Job 1 完成)
                var buildJob = new BuildInstanceJob
                {
                    Inputs = _jobInputs,
                    OriginOffsets = _jobOriginOffsets,
                    Output = _jobOutput,
                    CanvasW = CanvasWidth,
                    CanvasH = CanvasHeight,
                    IsolatedY = IsolatedPosition.y,
                    InputCount = _jobActiveCount
                };
                _jobHandle = buildJob.Schedule(_jobActiveCount, 256, evalHandle);
                _jobScheduled = true;
            }
        }

        /// <summary>
        /// 收集活跃精灵到 NativeArray (主线程)
        /// </summary>
        int CollectSpritesToNativeArray(double musicTime)
        {
            int count = 0;

            for (int layer = 0; layer < 5; layer++)
            {
                var sprite = osbPlayer.GetLayerActiveHead(layer);
                var tail = osbPlayer.GetLayerActiveTail(layer);

                while (sprite != tail && count < MaxInstances)
                {
                    var state = sprite.State;

                    // 纹理索引: 优先缓存, 动画在主线程解析
                    int texIndex = sprite.CachedTexIndex;
                    if (texIndex < 0)
                        texIndex = ResolveTextureIndex(sprite.Element, musicTime, sprite.StartTime);

                    Vector2Int texSize = texIndex >= 0 ? textureDimensions[texIndex] : Vector2Int.zero;

                    int originIdx = sprite.Element != null ? (int)sprite.Element.Origin : 1;
                    if ((uint)originIdx >= (uint)OriginOffsets.Length) originIdx = 1;

                    _jobInputs[count] = new SpriteInputData
                    {
                        X = state.X,
                        Y = state.Y,
                        ScaleX = state.ScaleX,
                        ScaleY = state.ScaleY,
                        Rotation = state.Rotation,
                        Alpha = state.Alpha,
                        R = state.R,
                        G = state.G,
                        B = state.B,
                        FlipH = state.FlipH ? (byte)1 : (byte)0,
                        FlipV = state.FlipV ? (byte)1 : (byte)0,
                        Additive = state.Additive ? (byte)1 : (byte)0,
                        TexIndex = texIndex,
                        OriginIndex = originIdx,
                        TexWidth = texSize.x,
                        TexHeight = texSize.y
                    };

                    count++;
                    sprite = sprite.Next;
                }
            }

            return count;
        }

        // =========================================================
        //  LateUpdate: Complete Job → SetData → GPU 提交
        //  (约束 1: LateUpdate 极末尾 Complete)
        // =========================================================

        void LateUpdate()
        {
            if (!isRendering || !_jobScheduled) return;

            // Complete Job
            _jobHandle.Complete();
            _jobScheduled = false;

            if (_jobActiveCount <= 0) return;

            var bounds = new Bounds(IsolatedPosition, new Vector3(10000000f, 10000000f, 10000000f));

            // 统计可见的 Alpha/Additive 数量
            int alphaCount = 0, additiveCount = 0;
            for (int i = 0; i < _jobActiveCount; i++)
            {
                if (_jobOutput[i].objectToWorld.m00 != 0f || _jobOutput[i].objectToWorld.m11 != 0f)
                {
                    if (_jobOutput[i].params0.y > 0.5f)
                        additiveCount++;
                    else
                        alphaCount++;
                }
            }

            // Pass 0: Alpha Blend
            if (alphaCount > 0)
            {
                alphaBuffer.SetData(_jobOutput, 0, 0, _jobActiveCount);
                sbMaterialAlpha.SetBuffer("_InstanceData", alphaBuffer);
                sbMaterialAlpha.SetTexture("_MainTexArray", textureArray);
                Graphics.DrawMeshInstancedProcedural(quadMesh, 0, sbMaterialAlpha, bounds, _jobActiveCount,
                    null, ShadowCastingMode.Off, false, storyboardLayer, null);
            }

            // Pass 1: Additive
            if (additiveCount > 0)
            {
                additiveBuffer.SetData(_jobOutput, 0, 0, _jobActiveCount);
                sbMaterialAdditive.SetBuffer("_InstanceData", additiveBuffer);
                sbMaterialAdditive.SetTexture("_MainTexArray", textureArray);
                Graphics.DrawMeshInstancedProcedural(quadMesh, 0, sbMaterialAdditive, bounds, _jobActiveCount,
                    null, ShadowCastingMode.Off, false, storyboardLayer, null);
            }
        }

        /// <summary>
        /// 解析元素的纹理索引 (动画按当前帧解析)
        /// </summary>
        int ResolveTextureIndex(SBElement element, double musicTime, double elementStartTime)
        {
            string path = element.ImagePath;
            if (string.IsNullOrEmpty(path)) return -1;

            if (element is SBStoryboardAnimation anim)
            {
                int frame = anim.GetCurrentFrame(musicTime, elementStartTime);
                path = anim.BuildFramePath(frame);
            }

            // 统一路径格式: 反斜杠→正斜杠, 小写
            string normalized = path.Replace('\\', '/').ToLowerInvariant();

            if (textureIndexMap != null && textureIndexMap.TryGetValue(normalized, out int idx))
                return idx;

            // 回退: 尝试原始路径
            if (textureIndexMap != null && textureIndexMap.TryGetValue(path, out int fallbackIdx))
                return fallbackIdx;

            return -1;
        }

        // =========================================================
        //  视频渲染 (Graphics.DrawMesh, 无 GameObject)
        // =========================================================

        void CreateVideoPlayer(string videoPath)
        {
            var vpGo = new GameObject("[SB_VideoPlayer]");
            vpGo.transform.SetParent(transform);

            string normalizedPath = videoPath.Replace('\\', '/');
            // Windows MediaFoundation 对 file:// URL 的 %XX 编码不解码 (Unity 已知问题),
            // 会把编码后的字符串当文件路径直接打开 → "empty file"。
            // 正确做法: 直接传裸绝对路径 (不加 file:/// 前缀, 不做 URL 编码),
            // MediaFoundation/ExoPlayer 均将裸路径当本地文件打开, 空格/括号/日文均安全。
            string url = normalizedPath;

            // 创建 videoRT, 由 VideoPlayer 直接解码写入 (内部完成 YUV→RGB 转换, 跨平台稳定)
            videoRT = new RenderTexture(RT_Width, RT_Height, 0, RenderTextureFormat.ARGB32);
            videoRT.Create();

            videoPlayer = vpGo.AddComponent<VideoPlayer>();
            videoPlayer.source = VideoSource.Url;
            videoPlayer.url = url;
            videoPlayer.playOnAwake = false;
            videoPlayer.isLooping = false;  // osu! 视频只播放一次, 不循环 (末帧冻结)
            videoPlayer.skipOnDrop = true;
            videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
            videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            videoPlayer.targetTexture = videoRT;

            videoPlayer.errorReceived += (vp, msg) =>
            {
                Debug.LogError($"[SBRenderer] 视频错误: {msg}");
                // 解码失败: 标记无视频, 让 HolographicScreenManager 回退显示背景图
                hasVideo = false;
                HolographicScreenManager.Instance?.OnVideoLoadFailed();
            };

            videoPlayer.prepareCompleted += (vp) =>
            {
                // 不在此自动 Play: 由 SyncVideoTime 根据音乐时间接管启停, 避免视频早于音乐开播
                Debug.Log($"[SBRenderer] 视频准备完成: {vp.width}x{vp.height}, 时长={vp.length:F1}s");
            };

            videoPlayer.Prepare();
            Debug.Log($"[SBRenderer] VideoPlayer 已创建: {url}, RT={videoRT.width}x{videoRT.height}");
        }

        void SyncVideoTime(double musicTimeMs)
        {
            // osu! 语义: 视频在 map 时间到达 offset 时开始播放 → videoTime = mapTime - offset
            double targetVideoTime = (musicTimeMs - videoOffsetMs) / 1000.0;

            if (targetVideoTime < 0)
            {
                if (videoPlayer.isPlaying) videoPlayer.Pause();
                return;
            }

            // osu! 视频只播放一次: 超出视频时长后停在末帧 (不 seek, 避免 MediaFoundation 报错)
            double videoLength = videoPlayer.length;
            if (videoLength > 0 && targetVideoTime >= videoLength)
            {
                if (videoPlayer.isPlaying) videoPlayer.Pause();
                return;
            }

            if (!videoPlayer.isPlaying)
                videoPlayer.Play();

            // 阈值 0.3s: skipOnDrop 下 videoPlayer.time 有抖动, 过小阈值会导致频繁 seek 卡顿
            double drift = videoPlayer.time - targetVideoTime;
            if (drift > 0.3 || drift < -0.3)
            {
                videoPlayer.time = math.clamp(targetVideoTime, 0, math.max(0, videoLength - 0.05));
            }
        }

        // =========================================================
        //  纹理数组打包
        // =========================================================

        void BuildTextureArray(SBStoryboard storyboard, string beatmapFolder)
        {
            var paths = new List<string>();
            var pathSet = new HashSet<string>();

            foreach (var element in storyboard.GetAllElementsInRenderOrder())
            {
                if (string.IsNullOrEmpty(element.ImagePath)) continue;

                if (element is SBStoryboardAnimation anim)
                {
                    for (int f = 0; f < anim.FrameCount; f++)
                    {
                        string framePath = anim.BuildFramePath(f);
                        string key = framePath.Replace('\\', '/').ToLowerInvariant();
                        if (pathSet.Add(key))
                            paths.Add(framePath);
                    }
                }
                else
                {
                    string key = element.ImagePath.Replace('\\', '/').ToLowerInvariant();
                    if (pathSet.Add(key))
                        paths.Add(element.ImagePath);
                }
            }

            if (paths.Count == 0)
            {
                Debug.LogWarning("[SBRenderer] 无纹理需要打包");
                return;
            }

            SBDebugLog.Log($"[BuildTextureArray] {paths.Count} 纹理待加载");

            // 过滤掉不存在或解码失败的纹理路径, 统一加载
            var validPaths = new List<string>();
            var textures = new List<Texture2D>();
            int maxWidth = 0, maxHeight = 0;

            for (int i = 0; i < paths.Count; i++)
            {
                string fullPath = System.IO.Path.Combine(beatmapFolder, paths[i]);
                if (!System.IO.File.Exists(fullPath))
                {
                    Debug.LogWarning($"[SBRenderer] SB 纹理不存在, 跳过: {paths[i]}");
                    continue;
                }

                var tex = LoadTexture(fullPath);
                if (tex == null)
                    continue;  // 解码失败, 已在 LoadTexture 内打印警告

                validPaths.Add(paths[i]);
                textures.Add(tex);
                if (tex.width > maxWidth) maxWidth = tex.width;
                if (tex.height > maxHeight) maxHeight = tex.height;
            }

            if (textures.Count == 0)
            {
                Debug.LogWarning("[SBRenderer] 无有效 SB 纹理");
                return;
            }

            SBDebugLog.Mem($"纹理加载完成: {textures.Count} 张, max={maxWidth}x{maxHeight}");

            maxWidth = Mathf.Min(maxWidth, 2048);
            maxHeight = Mathf.Min(maxHeight, 2048);

            // 安全限制: 确保 Texture2DArray 总大小不超过 1.8GB (留余量)
            const long MAX_BYTES = (long)(1.8 * 1024 * 1024 * 1024);
            long bytesPerLayer = (long)maxWidth * maxHeight * 4; // RGBA32
            int maxLayers = textures.Count;

            if (bytesPerLayer * maxLayers > MAX_BYTES)
            {
                // 先降分辨率
                while (maxWidth > 256 && maxHeight > 256 && bytesPerLayer * maxLayers > MAX_BYTES)
                {
                    maxWidth /= 2;
                    maxHeight /= 2;
                    bytesPerLayer = (long)maxWidth * maxHeight * 4;
                }

                // 仍然超限则截断层数
                if (bytesPerLayer * maxLayers > MAX_BYTES)
                {
                    maxLayers = (int)(MAX_BYTES / bytesPerLayer);
                    Debug.LogWarning($"[SBRenderer] SB 纹理过多, 截断到 {maxLayers} 层 (原 {textures.Count})");
                }
            }

            int layerCount = Mathf.Min(textures.Count, maxLayers);
            paths = validPaths; // 更新 paths 为有效路径列表 (用于下方 textureIndexMap 构建)

            textureArray = new Texture2DArray(maxWidth, maxHeight, layerCount,
                TextureFormat.RGBA32, true, false);
            textureArray.filterMode = FilterMode.Bilinear;
            textureArray.wrapMode = TextureWrapMode.Clamp;
            SBDebugLog.Mem($"Texture2DArray 创建: {maxWidth}x{maxHeight}x{layerCount}");

            var tempRT = RenderTexture.GetTemporary(maxWidth, maxHeight, 0, RenderTextureFormat.ARGB32);
            var prevRT = RenderTexture.active;

            for (int i = 0; i < layerCount; i++)
            {
                var src = textures[i];
                Graphics.Blit(src, tempRT);
                RenderTexture.active = tempRT;

                var slice = new Texture2D(maxWidth, maxHeight, TextureFormat.RGBA32, false);
                slice.ReadPixels(new Rect(0, 0, maxWidth, maxHeight), 0, 0);
                slice.Apply();

                textureArray.SetPixels(slice.GetPixels(), i, 0);
                Destroy(slice);
            }

            var verifyPixels = textureArray.GetPixels(0, 0);
            bool allBlack = true;
            for (int p = 0; p < verifyPixels.Length; p++)
            {
                if (verifyPixels[p].r > 0.01f || verifyPixels[p].g > 0.01f || verifyPixels[p].b > 0.01f)
                { allBlack = false; break; }
            }
            Debug.Log($"[SBRenderer] 纹理数组验证: layer[0] {(allBlack ? "全黑!" : "有内容")} ({verifyPixels.Length} 像素)");

            textureArray.Apply(true, true);
            SBDebugLog.Mem("Texture2DArray.Apply 完成");

            RenderTexture.active = prevRT;
            RenderTexture.ReleaseTemporary(tempRT);

            textureIndexMap = new Dictionary<string, int>(layerCount);
            textureDimensions = new Vector2Int[layerCount];
            for (int i = 0; i < layerCount; i++)
            {
                // 统一路径: 反斜杠→正斜杠, 小写, 去引号
                string normalized = paths[i].Replace('\\', '/').ToLowerInvariant().Trim('"');
                textureIndexMap[normalized] = i;
                textureDimensions[i] = new Vector2Int(textures[i].width, textures[i].height);
            }

            for (int i = 0; i < textures.Count; i++)
            {
                Destroy(textures[i]);
            }
            SBDebugLog.Mem("源纹理释放完成");

            Debug.Log($"[SBRenderer] 纹理数组打包完成: {layerCount} 层, {maxWidth}x{maxHeight}");
        }

        void CacheTextureIndices()
        {
            if (osbPlayer == null || textureIndexMap == null) return;
            osbPlayer.ForEachSprite((layer, sprite) =>
            {
                var element = sprite.Element;
                if (element is SBStoryboardAnimation)
                {
                    sprite.CachedTexIndex = -1;
                }
                else if (!string.IsNullOrEmpty(element.ImagePath) &&
                         textureIndexMap.TryGetValue(element.ImagePath, out int idx))
                {
                    sprite.CachedTexIndex = idx;
                }
            });
        }

        Texture2D LoadTexture(string path)
        {
            try
            {
                if (!System.IO.File.Exists(path))
                    return null;  // 不存在 → null, 由调用方跳过该纹理 (sprite 渲染时不可见, 与 osu! 行为一致)

                byte[] data = System.IO.File.ReadAllBytes(path);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, true);
                if (tex.LoadImage(data))
                {
                    tex.filterMode = FilterMode.Bilinear;
                    return tex;
                }

                Debug.LogWarning($"[SBRenderer] 纹理解码失败: {path}");
                return null;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[SBRenderer] 纹理加载异常: {path}, {e.Message}");
                return null;
            }
        }

        // =========================================================
        //  共享资源创建
        // =========================================================

        void EnsureSBMaterial()
        {
            if (sbMaterialAlpha != null) return;

            var shader = Shader.Find("OsuVR/SBInstanced");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null)
            {
                Debug.LogError("[SBRenderer] 所有 SB Shader 均不可用!");
                return;
            }

            // Pass 0: 标准 Alpha 混合 (Blend SrcAlpha OneMinusSrcAlpha)
            sbMaterialAlpha = new Material(shader);
            sbMaterialAlpha.enableInstancing = true;
            sbMaterialAlpha.SetShaderPassEnabled("SB_Opaque", false);
            sbMaterialAlpha.SetShaderPassEnabled("SB_Additive", false);

            // Pass 1: 加法混合 (Blend One One)
            sbMaterialAdditive = new Material(shader);
            sbMaterialAdditive.enableInstancing = true;
            sbMaterialAdditive.SetShaderPassEnabled("SB_Opaque", false);
            sbMaterialAdditive.SetShaderPassEnabled("SB_AlphaBlend", false);

            Debug.Log($"[SBRenderer] 材质创建成功: shader={shader.name}");
        }

        Mesh EnsureQuadMesh()
        {
            if (quadMesh != null) return quadMesh;

            quadMesh = new Mesh();
            quadMesh.name = "SB_InstancedQuad";

            quadMesh.vertices = new Vector3[]
            {
                new Vector3(-0.5f, -0.5f, 0),
                new Vector3(-0.5f,  0.5f, 0),
                new Vector3( 0.5f,  0.5f, 0),
                new Vector3( 0.5f, -0.5f, 0),
            };
            quadMesh.uv = new Vector2[]
            {
                new Vector2(0, 0),
                new Vector2(0, 1),
                new Vector2(1, 1),
                new Vector2(1, 0),
            };
            quadMesh.triangles = new int[] { 0, 1, 2, 0, 2, 3 };
            quadMesh.RecalculateNormals();

            return quadMesh;
        }

        // =========================================================
        //  相机和渲染管线搭建
        // =========================================================

        void EnsureLayerExists()
        {
#if UNITY_EDITOR
            var tagManager = new UnityEditor.SerializedObject(
                UnityEditor.AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");

            for (int i = 0; i < layers.arraySize; i++)
            {
                var layerProp = layers.GetArrayElementAtIndex(i);
                if (layerProp.stringValue == LayerName)
                {
                    storyboardLayer = i;
                    return;
                }
            }

            for (int i = 8; i < layers.arraySize; i++)
            {
                var layerProp = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(layerProp.stringValue))
                {
                    layerProp.stringValue = LayerName;
                    tagManager.ApplyModifiedProperties();
                    storyboardLayer = i;
                    Debug.Log($"[SBRenderer] 已创建 {LayerName} (Layer {i})");
                    return;
                }
            }

            Debug.LogError($"[SBRenderer] 无法创建 {LayerName}：所有用户层已满！");
            storyboardLayer = FallbackLayer;
#else
            storyboardLayer = LayerMask.NameToLayer(LayerName);
            if (storyboardLayer < 0)
            {
                Debug.LogWarning($"[SBRenderer] {LayerName} 未配置，使用 Layer {FallbackLayer}");
                storyboardLayer = FallbackLayer;
            }
#endif
        }

        void EnsureCameraSetup()
        {
            if (renderCamera != null) return;

            EnsureQuadMesh();

            isolatedRoot = new GameObject("[SB_IsolatedRoot]");
            isolatedRoot.transform.SetParent(transform);
            isolatedRoot.transform.position = IsolatedPosition;

            var camGo = new GameObject("[SB_Camera]");
            camGo.transform.SetParent(isolatedRoot.transform);
            camGo.transform.localPosition = new Vector3(0, 0, -10);

            renderCamera = camGo.AddComponent<Camera>();
            renderCamera.orthographic = true;
            renderCamera.orthographicSize = CanvasHeight * 0.5f;
            renderCamera.nearClipPlane = -100f;
            renderCamera.farClipPlane = 100f;
            renderCamera.cullingMask = 1 << storyboardLayer;
            renderCamera.clearFlags = CameraClearFlags.SolidColor;
            renderCamera.backgroundColor = new Color(0f, 0f, 0f, 0f); // 透明黑: 预乘管线要求 rgb 也为 0
            renderCamera.stereoTargetEye = StereoTargetEyeMask.None;

            renderTexture = new RenderTexture(RT_Width, RT_Height, 0, RenderTextureFormat.ARGB32);
            renderTexture.antiAliasing = 1;
            renderTexture.Create();
            renderCamera.targetTexture = renderTexture;

            // 后处理: 确保模糊/bloom等效果烘焙进 SB RenderTexture
            var ppLayer = camGo.AddComponent<PostProcessLayer>();
            ppLayer.volumeTrigger = camGo.transform;
            ppLayer.volumeLayer = ~0; // 检测所有 Volume
            ppLayer.antialiasingMode = PostProcessLayer.Antialiasing.None;
        }

        // =========================================================
        //  音乐时间获取
        // =========================================================

        RhythmGameManager cachedRGM;

        void CacheRhythmGameManager()
        {
            if (cachedRGM == null)
                cachedRGM = FindFirstObjectByType<RhythmGameManager>();
        }

        double GetCurrentMusicTime()
        {
            // 惰性重查: 场景切换后缓存可能失效 (Unity 假 null), 确保视频/SB 时间基准正确
            if (cachedRGM == null)
                cachedRGM = FindFirstObjectByType<RhythmGameManager>();
            if (cachedRGM != null) return cachedRGM.currentMusicTimeMs;
            return Time.time * 1000.0;
        }

        // =========================================================
        //  Cleanup
        // =========================================================

        void OnDestroy()
        {
            UnloadAll();

            DisposeJobSystem();

            alphaBuffer?.Release();
            additiveBuffer?.Release();

            if (renderTexture != null)
            {
                renderTexture.Release();
                Destroy(renderTexture);
            }

            if (quadMesh != null) Destroy(quadMesh);

            if (Instance == this) Instance = null;
        }
    }
}
