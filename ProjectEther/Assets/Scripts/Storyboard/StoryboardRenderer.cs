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

        // ---- 视频 Quad 深度 ----
        const float VideoQuadZ = 50f;
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

        // =========================================================
        //  Burst Job 数据结构
        // =========================================================

        struct SpriteInputData
        {
            public float X, Y;
            public float ScaleX, ScaleY;
            public float Rotation;
            public float Alpha;
            public float R, G, B;
            public byte FlipH, FlipV, Additive;
            public int TexIndex;
            public int OriginIndex;
            public int TexWidth, TexHeight;
        }

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
                float alpha = input.Alpha;
                if (alpha > 1f) alpha = math.fmod(alpha, 1f);
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

        // ---- 引擎 ----
        SBOsbPlayer osbPlayer;
        string currentBeatmapFolder;

        // ---- 视频 ----
        VideoPlayer videoPlayer;
        bool hasVideo;
        int videoOffsetMs;
        Material videoMaterial;
        Mesh videoQuadMesh;
        Matrix4x4 videoQuadMatrix;

        // ---- 纹理数组 ----
        Texture2DArray textureArray;
        Dictionary<string, int> textureIndexMap;
        Vector2Int[] textureDimensions;
        int[] cachedTextureIndex;
        static Texture2D whitePixel;

        // ---- GPU 缓冲区 ----
        ComputeBuffer alphaBuffer;
        ComputeBuffer additiveBuffer;

        // ---- 共享资源 ----
        Material sbMaterialAlpha;
        Material sbMaterialAdditive;
        Mesh quadMesh;
        MaterialPropertyBlock videoMPB;

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

            osbPlayer = new SBOsbPlayer();
            osbPlayer.LoadStoryboard(storyboard);
            SBDebugLog.Mem("引擎加载完成");

            CacheTextureIndices();

            isRendering = true;
            SBDebugLog.Log($"[SBRenderer] 加载完成: {storyboard.TotalElementCount} 元素, {textureArray.depth} 纹理层");
            SBDebugLog.End();
        }

        public void LoadVideo(string videoPath, int videoOffset)
        {
            if (string.IsNullOrEmpty(videoPath)) return;

            EnsureCameraSetup();

            hasVideo = true;
            videoOffsetMs = videoOffset;

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

            if (videoMaterial != null) { Destroy(videoMaterial); videoMaterial = null; }
            if (videoQuadMesh != null) { Destroy(videoQuadMesh); videoQuadMesh = null; }
        }

        public void UnloadAll()
        {
            UnloadStoryboard();
            UnloadVideo();
        }

        public RenderTexture GetRenderTexture() => renderTexture;

        // =========================================================
        //  Update: 引擎推进 + 精灵收集 + Schedule Job
        //  (约束 1: 拉开调度间距, Update 极早阶段 Schedule)
        // =========================================================

        void Update()
        {
            if (!isRendering) return;
            if (osbPlayer == null && !hasVideo) return;
            if (renderCamera == null) return;

            double musicTime = GetCurrentMusicTime();

            // 1. 视频时间同步
            if (hasVideo && videoPlayer != null && videoPlayer.isPrepared)
                SyncVideoTime(musicTime);

            // 2. 推进引擎 (主线程: 链表操作无法并行)
            if (osbPlayer != null)
                osbPlayer.Update(musicTime);

            // 3. 绘制视频背景 Quad
            if (hasVideo && videoMaterial != null && videoQuadMesh != null)
                DrawVideoQuad();

            // 4. 收集活跃精灵到 NativeArray + Schedule Job
            if (osbPlayer != null && sbMaterialAlpha != null && textureArray != null)
            {
                _jobActiveCount = CollectSpritesToNativeArray(musicTime);

                if (_jobActiveCount > 0)
                {
                    var job = new BuildInstanceJob
                    {
                        Inputs = _jobInputs,
                        OriginOffsets = _jobOriginOffsets,
                        Output = _jobOutput,
                        CanvasW = CanvasWidth,
                        CanvasH = CanvasHeight,
                        IsolatedY = IsolatedPosition.y,
                        InputCount = _jobActiveCount
                    };

                    _jobHandle = job.Schedule(_jobActiveCount, 256);
                    _jobScheduled = true;
                }
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
                        texIndex = ResolveTextureIndex(sprite.Element, musicTime);

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

            // Complete Job (约束 1: 在必须向 GPU 提交数据的前一刻)
            _jobHandle.Complete();
            _jobScheduled = false;

            if (_jobActiveCount <= 0) return;

            // 约束 3: GPU 剔除法 — 不需要原子计数器
            // 扫描 output 统计 Alpha/Additive 数量 (简单循环, ~0.001ms)
            int alphaCount = 0;
            int additiveCount = 0;
            for (int i = 0; i < _jobActiveCount; i++)
            {
                // params0.y = blendMode (0=Alpha, 1=Additive)
                // 只统计非零矩阵的精灵 (m00 != 0 表示可见)
                if (_jobOutput[i].objectToWorld.m00 != 0f || _jobOutput[i].objectToWorld.m11 != 0f)
                {
                    if (_jobOutput[i].params0.y > 0.5f)
                        additiveCount++;
                    else
                        alphaCount++;
                }
            }

            // 约束 2: NativeArray → GPU 零拷贝
            var bounds = new Bounds(IsolatedPosition, new Vector3(10000000f, 10000000f, 10000000f));

            // 双 Pass 共享同一缓冲区 (约束 3: 零 Scale 的实例被 Vertex Shader 剔除)
            if (alphaCount > 0)
            {
                alphaBuffer.SetData(_jobOutput, 0, 0, _jobActiveCount);
                sbMaterialAlpha.SetBuffer("_InstanceData", alphaBuffer);
                sbMaterialAlpha.SetTexture("_MainTexArray", textureArray);
                Graphics.DrawMeshInstancedProcedural(quadMesh, 0, sbMaterialAlpha, bounds, _jobActiveCount,
                    null, ShadowCastingMode.Off, false, storyboardLayer, null);
            }

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
        int ResolveTextureIndex(SBElement element, double musicTime)
        {
            string path = element.ImagePath;
            if (string.IsNullOrEmpty(path)) return -1;

            if (element is SBStoryboardAnimation anim)
            {
                int frame = anim.GetCurrentFrame(musicTime, 0);
                path = anim.BuildFramePath(frame);
            }

            if (textureIndexMap != null && textureIndexMap.TryGetValue(path, out int idx))
                return idx;

            if (textureIndexMap != null && textureIndexMap.TryGetValue(element.ImagePath, out int fallbackIdx))
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

            videoPlayer = vpGo.AddComponent<VideoPlayer>();
            videoPlayer.source = VideoSource.Url;
            videoPlayer.url = videoPath;
            videoPlayer.playOnAwake = false;
            videoPlayer.isLooping = true;
            videoPlayer.skipOnDrop = true;
            videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
            videoPlayer.renderMode = VideoRenderMode.APIOnly;

            videoPlayer.prepareCompleted += (vp) =>
            {
                vp.Play();
                UpdateVideoQuadScale();
                Debug.Log("[SBRenderer] 视频准备完成，开始播放");
            };

            CreateVideoQuad();
            videoPlayer.Prepare();
        }

        void CreateVideoQuad()
        {
            videoQuadMesh = CreateFullScreenQuad();

            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Unlit/Texture");
            videoMaterial = new Material(shader);
            videoMaterial.SetFloat("_Surface", 0);
            videoMaterial.SetFloat("_ZWrite", 1);
            videoMaterial.renderQueue = (int)RenderQueue.Geometry;

            videoMPB = new MaterialPropertyBlock();

            videoQuadMatrix = Matrix4x4.TRS(
                new Vector3(0, 0, VideoQuadZ),
                Quaternion.identity,
                new Vector3(CanvasWidth, CanvasHeight, 1f));
        }

        void UpdateVideoQuadScale()
        {
            if (videoPlayer == null) return;

            float videoW = videoPlayer.width;
            float videoH = videoPlayer.height;
            if (videoW <= 0 || videoH <= 0) return;

            float canvasAspect = (float)CanvasWidth / CanvasHeight;
            float videoAspect = videoW / videoH;

            float scaleX, scaleY;
            if (videoAspect > canvasAspect)
            {
                scaleX = CanvasWidth;
                scaleY = CanvasWidth / videoAspect;
            }
            else
            {
                scaleY = CanvasHeight;
                scaleX = CanvasHeight * videoAspect;
            }

            videoQuadMatrix = Matrix4x4.TRS(
                new Vector3(0, 0, VideoQuadZ),
                Quaternion.identity,
                new Vector3(scaleX, scaleY, 1f));
        }

        void DrawVideoQuad()
        {
            if (videoPlayer != null && videoPlayer.texture != null)
            {
                videoMaterial.mainTexture = videoPlayer.texture;
            }

            Graphics.DrawMesh(videoQuadMesh, videoQuadMatrix, videoMaterial,
                storyboardLayer, renderCamera, 0, videoMPB,
                ShadowCastingMode.Off, false, null, LightProbeUsage.Off, null);
        }

        void SyncVideoTime(double musicTimeMs)
        {
            double targetVideoTime = (musicTimeMs + videoOffsetMs) / 1000.0;

            if (targetVideoTime < 0)
            {
                if (videoPlayer.isPlaying) videoPlayer.Pause();
                return;
            }

            if (!videoPlayer.isPlaying)
                videoPlayer.Play();

            double drift = videoPlayer.time - targetVideoTime;
            if (drift > 0.1 || drift < -0.1)
            {
                videoPlayer.time = targetVideoTime;
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
                        if (pathSet.Add(framePath))
                            paths.Add(framePath);
                    }
                }
                else
                {
                    if (pathSet.Add(element.ImagePath))
                        paths.Add(element.ImagePath);
                }
            }

            if (paths.Count == 0)
            {
                Debug.LogWarning("[SBRenderer] 无纹理需要打包");
                return;
            }

            SBDebugLog.Log($"[BuildTextureArray] {paths.Count} 纹理待加载");

            var textures = new Texture2D[paths.Count];
            int maxWidth = 0, maxHeight = 0;

            for (int i = 0; i < paths.Count; i++)
            {
                textures[i] = LoadTexture(System.IO.Path.Combine(beatmapFolder, paths[i]));
                if (textures[i].width > maxWidth) maxWidth = textures[i].width;
                if (textures[i].height > maxHeight) maxHeight = textures[i].height;
            }

            SBDebugLog.Mem($"纹理加载完成: max={maxWidth}x{maxHeight}");

            maxWidth = Mathf.Min(maxWidth, 2048);
            maxHeight = Mathf.Min(maxHeight, 2048);

            textureArray = new Texture2DArray(maxWidth, maxHeight, paths.Count,
                TextureFormat.RGBA32, true, false);
            textureArray.filterMode = FilterMode.Bilinear;
            textureArray.wrapMode = TextureWrapMode.Clamp;
            SBDebugLog.Mem($"Texture2DArray 创建: {maxWidth}x{maxHeight}x{paths.Count}");

            var tempRT = RenderTexture.GetTemporary(maxWidth, maxHeight, 0, RenderTextureFormat.ARGB32);
            var prevRT = RenderTexture.active;

            for (int i = 0; i < textures.Length; i++)
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

            textureIndexMap = new Dictionary<string, int>(paths.Count);
            textureDimensions = new Vector2Int[paths.Count];
            for (int i = 0; i < paths.Count; i++)
            {
                textureIndexMap[paths[i]] = i;
                textureDimensions[i] = new Vector2Int(textures[i].width, textures[i].height);
            }

            for (int i = 0; i < textures.Length; i++)
            {
                if (textures[i] != null && textures[i] != whitePixel)
                    Destroy(textures[i]);
            }
            SBDebugLog.Mem("源纹理释放完成");

            Debug.Log($"[SBRenderer] 纹理数组打包完成: {paths.Count} 层, {maxWidth}x{maxHeight}");
            for (int i = 0; i < paths.Count; i++)
                Debug.Log($"  Tex[{i}] {paths[i]} → {textureDimensions[i].x}x{textureDimensions[i].y}");
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
                    return GetWhitePixel();

                byte[] data = System.IO.File.ReadAllBytes(path);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, true);
                if (tex.LoadImage(data))
                {
                    tex.filterMode = FilterMode.Bilinear;
                    return tex;
                }

                return GetWhitePixel();
            }
            catch
            {
                return GetWhitePixel();
            }
        }

        static Texture2D GetWhitePixel()
        {
            if (whitePixel == null)
            {
                whitePixel = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                var colors = new Color[16];
                for (int i = 0; i < 16; i++) colors[i] = Color.white;
                whitePixel.SetPixels(colors);
                whitePixel.Apply();
            }
            return whitePixel;
        }

        // =========================================================
        //  共享资源创建
        // =========================================================

        void EnsureSBMaterial()
        {
            if (sbMaterialAlpha != null) return;

            var shader = Shader.Find("OsuVR/SBInstanced");
            if (shader == null)
            {
                Debug.LogError("[SBRenderer] 找不到 Shader 'OsuVR/SBInstanced'!");
                return;
            }

            sbMaterialAlpha = new Material(shader);
            sbMaterialAlpha.enableInstancing = true;
            sbMaterialAlpha.SetShaderPassEnabled("SB_Additive", false);

            sbMaterialAdditive = new Material(shader);
            sbMaterialAdditive.enableInstancing = true;
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

        static Mesh CreateFullScreenQuad()
        {
            var mesh = new Mesh();
            mesh.name = "SB_VideoQuad";
            mesh.vertices = new Vector3[]
            {
                new Vector3(-0.5f, -0.5f, 0),
                new Vector3(-0.5f,  0.5f, 0),
                new Vector3( 0.5f,  0.5f, 0),
                new Vector3( 0.5f, -0.5f, 0),
            };
            mesh.uv = new Vector2[]
            {
                new Vector2(0, 0),
                new Vector2(0, 1),
                new Vector2(1, 1),
                new Vector2(1, 0),
            };
            mesh.triangles = new int[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateNormals();
            return mesh;
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
            renderCamera.backgroundColor = new Color(0.15f, 0.0f, 0.15f, 0.0f); // alpha=0: 叠加模式, 未绘制区域透明
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
            if (whitePixel != null) { Destroy(whitePixel); whitePixel = null; }

            if (Instance == this) Instance = null;
        }
    }
}
