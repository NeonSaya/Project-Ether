using System.Collections.Generic;
using OsuVR.Storyboard.Data;
using OsuVR.Storyboard.Engine;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.Video;

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
    ///   - CommandBuffer 按原图层/声明顺序提交，独立原尺寸纹理与统一实例缓冲
    ///
    /// 视频渲染:
    ///   - VideoPlayer 直接输出到独立 RenderTexture (VideoRenderMode.RenderTexture)
    ///   - 视频 RT 由 HolographicScreenManager 的视频 Overlay 层独立显示
    ///   - 视频和 SB 完全解耦, 不经过 Graphics.DrawMesh
    /// </summary>
    [DefaultExecutionOrder(1000)]
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

        // ---- 图层 ----

        // ---- GPU 实例化参数 ----
        const int InitialInstanceCapacity = 8192;
        const int InstanceDataStride = 96; // sizeof(SpriteInstanceData) 的字节数

        const float SBQuadZ = 0f;

        // =========================================================
        //  GPU 实例数据结构 (与 SBInstanced.shader 精确对齐)
        // =========================================================

        [System.Runtime.InteropServices.StructLayout(
            System.Runtime.InteropServices.LayoutKind.Sequential
        )]
        struct SpriteInstanceData
        {
            public Matrix4x4 objectToWorld; // 64 字节
            public Vector4 color; // 16 字节
            public Vector4 params0; // 16 字节 (x=texIndex, y=blendMode, z=flipH, w=flipV)
        } // 合计: 96 字节

        // SpriteInputData 已移至 SBFlatData.cs (OsuVR.Storyboard.Engine 命名空间)

        // =========================================================
        //  Burst Job: 并行矩阵计算 + GPU 剔除
        // =========================================================

        [BurstCompile]
        struct BuildInstanceJob : IJobParallelFor
        {
            [ReadOnly]
            public NativeArray<SpriteInputData> Inputs;

            [ReadOnly]
            public NativeArray<float2> OriginOffsets;

            [ReadOnly]
            public NativeArray<int2> TextureDimensions;

            [WriteOnly]
            public NativeArray<SpriteInstanceData> Output;

            public int CanvasW,
                CanvasH;
            public float IsolatedY;
            public int InputCount;

            public void Execute(int i)
            {
                if (i >= InputCount)
                {
                    Output[i] = default;
                    return;
                }

                var input = Inputs[i];

                // GPU 剔除法: 不可见精灵 Scale→zero, Vertex Shader 瞬间剔除
                // 与 lazer/stable 的旧版行为一致: alpha 大于 1 时回绕取余,
                // SB 作者会利用这一点刻意做出闪烁效果。
                float alpha = input.Alpha;
                if (alpha > 1f)
                    alpha = math.fmod(alpha, 1f);
                if (alpha <= 0f || input.TexIndex < 0)
                {
                    Output[i] = default;
                    return;
                }

                // 矩阵构建 (纯 Burst 数学)
                float x = input.X - CanvasW * 0.5f;
                float y = -(input.Y - CanvasH * 0.5f) + IsolatedY;

                int texWidth = input.TexWidth;
                int texHeight = input.TexHeight;
                if ((uint)input.TexIndex < (uint)TextureDimensions.Length)
                {
                    texWidth = TextureDimensions[input.TexIndex].x;
                    texHeight = TextureDimensions[input.TexIndex].y;
                }
                float scaleX = texWidth * input.ScaleX * (input.FlipH != 0 ? -1f : 1f);
                float scaleY = texHeight * input.ScaleY * (input.FlipV != 0 ? -1f : 1f);

                int originIdx = input.OriginIndex;
                if ((uint)originIdx >= (uint)OriginOffsets.Length)
                    originIdx = 1;
                float2 pivot = OriginOffsets[originIdx];

                // AdjustOrigin: 翻转与负缩放取异或, 为真时反转对应的 pivot 分量
                if ((input.FlipH != 0) ^ (input.VectorScaleX < 0))
                    pivot.x = -pivot.x;
                if ((input.FlipV != 0) ^ (input.VectorScaleY < 0))
                    pivot.y = -pivot.y;

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
                    params0 = new float4(input.TexIndex, input.Additive != 0 ? 1f : 0f, 0f, 0f),
                };
            }
        }

        // =========================================================
        //  运行时对象
        // =========================================================

        Camera renderCamera;
        RenderTexture renderTexture;
        RenderTexture rasterSurface;
        Texture underlayTexture;
        bool underlayEncoded;
        GameObject isolatedRoot;

        // ---- 引擎 (DOD 扁平化管线) ----
        SBFlatTimelineData _flatTimeline;
        SBTriggerRuntime triggerRuntime;

        // ---- 视频 (VideoPlayer 直接解码到 RenderTexture) ----
        VideoPlayer videoPlayer;
        RenderTexture videoRT;
        bool hasVideo;
        int videoOffsetMs;

        // 普通/宽屏都以 (320,240) 为中心，禁止额外 X 平移。
        float _sbXOffset = 0f;

        // 音乐时间冻结检测（暂停时视频同步暂停，防止视频继续播放又被 drift seek 反复回跳）
        double _lastMusicTimeMs = -1;
        float _musicFrozenTimer = 0f;

        // ---- 原尺寸纹理 ----
        readonly List<Texture2D> storyboardTextures = new List<Texture2D>();
        Dictionary<string, int> textureIndexMap;
        Vector2Int[] textureDimensions;
        bool isWidescreen;

        // ---- GPU 缓冲区 ----
        ComputeBuffer instanceBuffer;

        // ---- 共享资源 ----
        Material storyboardMaterial;
        CommandBuffer drawCommands;
        Mesh quadMesh;

        // ---- 状态 ----
        bool isRendering;

        // ---- Origin 偏移缓存 ----
        static readonly Vector2[] OriginOffsets = new Vector2[]
        {
            new Vector2(-0.5f, 0.5f), // [0] TopLeft（左上）
            new Vector2(0.0f, 0.0f), // [1] Centre（中心）
            new Vector2(-0.5f, 0.0f), // [2] CentreLeft（中左）
            new Vector2(0.5f, 0.5f), // [3] TopRight（右上）
            new Vector2(0.0f, -0.5f), // [4] BottomCentre（下中）
            new Vector2(0.0f, 0.5f), // [5] TopCentre（上中）
            new Vector2(-0.5f, 0.5f), // [6] Custom（自定义）→ 回退到 TopLeft（左上）
            new Vector2(0.5f, 0.0f), // [7] CentreRight（中右）
            new Vector2(-0.5f, -0.5f), // [8] BottomLeft（左下）
            new Vector2(0.5f, -0.5f), // [9] BottomRight（右下）
        };

        // =========================================================
        //  Job System 状态
        // =========================================================

        NativeArray<SpriteInputData> _jobInputs;
        NativeArray<SpriteInstanceData> _jobOutput;
        NativeArray<float2> _jobOriginOffsets;
        NativeArray<int2> _jobTextureDimensions;
        int _jobActiveCount;
        bool _jobScheduled;
        JobHandle _jobHandle;
        int instanceCapacity;
        MaterialPropertyBlock drawProperties;
        static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        static readonly int InstanceOffsetId = Shader.PropertyToID("_InstanceOffset");

        // =========================================================
        //  生命周期
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
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (Application.isPlaying)
                DontDestroyOnLoad(gameObject);

            // GPU 缓冲区
            instanceCapacity = InitialInstanceCapacity;
            instanceBuffer = new ComputeBuffer(instanceCapacity, InstanceDataStride);
            drawProperties = new MaterialPropertyBlock();
            drawCommands = new CommandBuffer { name = "Storyboard ordered composition" };

            // Job System Persistent 预分配 (约束 2: 绝对禁止每帧 New/Dispose)
            InitializeJobSystem();
        }

        void InitializeJobSystem()
        {
            _jobInputs = new NativeArray<SpriteInputData>(instanceCapacity, Allocator.Persistent);
            _jobOutput = new NativeArray<SpriteInstanceData>(
                instanceCapacity,
                Allocator.Persistent
            );
            _jobTextureDimensions = new NativeArray<int2>(0, Allocator.Persistent);

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

            if (_jobInputs.IsCreated)
                _jobInputs.Dispose();
            if (_jobOutput.IsCreated)
                _jobOutput.Dispose();
            if (_jobOriginOffsets.IsCreated)
                _jobOriginOffsets.Dispose();
            if (_jobTextureDimensions.IsCreated)
                _jobTextureDimensions.Dispose();
        }

        // =========================================================
        //  公开 API
        // =========================================================

        public void LoadStoryboard(
            SBStoryboard storyboard,
            string beatmapFolder,
            bool widescreen = false,
            string backgroundPath = null
        )
        {
            UnloadAll();

            if (storyboard == null || storyboard.TotalElementCount == 0)
            {
                Debug.Log("[SBRenderer] Storyboard 为空，跳过加载");
                return;
            }

            SBDebugLog.Begin();
            try
            {
                SBDebugLog.Mem("LoadStoryboard 开始");
                SBDebugLog.Log($"元素数={storyboard.TotalElementCount}");

                // 宽屏 SB 不做坐标偏移：osu! 宽屏语义是「可见范围向两侧扩展到 ±107」，
                // 坐标原点仍在 640 游玩区左上角（320 仍为中心）。真实谱面验证：作者坐标按 640 空间书写，
                // 偏移 -107 会导致 SB 整体左移、与背景图错位（图层错位回归的根因）。
                _sbXOffset = 0f;
                isWidescreen = widescreen;

                EnsureCameraSetup();
                CacheRhythmGameManager();

                BuildTextures(storyboard, beatmapFolder);
                SBDebugLog.Mem("纹理加载完成");
                UpdateNativeTextureDimensions();

                EnsureSBMaterial();

                // DOD 管线: 扁平化所有 SB 数据到 NativeArray
                _flatTimeline = SBTimelineFlattener.Flatten(
                    storyboard,
                    textureIndexMap,
                    textureDimensions
                );
                triggerRuntime = new SBTriggerRuntime(storyboard);
                SBDebugLog.Mem($"DOD 扁平化完成: {_flatTimeline.SpriteCount} sprites");

                EnsureInstanceCapacity(_flatTimeline.SpriteCount);

                isRendering = true;

                // 当 Background 图层引用的正是该背景资源时, lazer 会替换预览背景图,
                // 即使该元素带有淡入命令、或出现时间晚得多也一样。
                string backgroundKey = NormalizeStoryboardPath(backgroundPath);
                if (backgroundKey.Length > 0)
                    foreach (var element in storyboard.Layers[(int)SBLayer.Background])
                        if (NormalizeStoryboardPath(element.ImagePath) == backgroundKey)
                        {
                            HolographicScreenManager.Instance?.HideBackgroundForSB();
                            break;
                        }

                SBDebugLog.Log(
                    $"[SBRenderer] 加载完成: {storyboard.TotalElementCount} 元素, {storyboardTextures.Count} 纹理"
                );
            }
            finally
            {
                SBDebugLog.End();
            }
        }

        public void LoadVideo(string videoPath, int videoOffset)
        {
            if (string.IsNullOrEmpty(videoPath))
                return;

            UnloadVideo();
            hasVideo = true;
            isRendering = true; // 视频时间同步需要 Update 循环
            videoOffsetMs = videoOffset;

            // 纯视频模式也必须缓存 RhythmGameManager:
            // 否则 GetCurrentMusicTime 回退到 Time.time (游戏启动时间而非音乐时间),
            // targetVideoTime 远超视频长度 → SyncVideoTime 永远 Pause → 视频黑屏
            CacheRhythmGameManager();

            CreateVideoPlayer(videoPath);

            Debug.Log(
                $"[SBRenderer] 已加载视频: {System.IO.Path.GetFileName(videoPath)}, offset={videoOffset}ms"
            );
        }

        public void LoadVideoAndStoryboard(
            string videoPath,
            int videoOffset,
            SBStoryboard storyboard,
            string beatmapFolder,
            bool widescreen = false,
            string backgroundPath = null
        )
        {
            LoadStoryboard(storyboard, beatmapFolder, widescreen, backgroundPath);
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
            if (_flatTimeline.Sprites.IsCreated)
                _flatTimeline.Dispose();
            _flatTimeline = default;
            underlayTexture = null;
            triggerRuntime = null;
            _jobActiveCount = 0;
            drawCommands?.Clear();
            ClearRenderTexture();

            foreach (var texture in storyboardTextures)
                if (texture != null)
                    ReleaseObject(texture);
            storyboardTextures.Clear();
            textureIndexMap?.Clear();

            if (storyboardMaterial != null)
            {
                ReleaseObject(storyboardMaterial);
                storyboardMaterial = null;
            }
        }

        public void UnloadVideo()
        {
            hasVideo = false;
            videoOffsetMs = 0;
            _lastMusicTimeMs = -1;
            _musicFrozenTimer = 0;

            if (videoPlayer != null)
            {
                videoPlayer.Stop();
                ReleaseObject(videoPlayer.gameObject);
                videoPlayer = null;
            }

            if (videoRT != null)
            {
                videoRT.Release();
                ReleaseObject(videoRT);
                videoRT = null;
            }
        }

        public void UnloadAll()
        {
            UnloadStoryboard();
            UnloadVideo();
        }

        bool PrepareTrigger()
        {
            if (!isRendering || triggerRuntime == null || !triggerRuntime.HasTriggers)
                return false;
            if (_jobScheduled)
            {
                _jobHandle.Complete();
                _jobScheduled = false;
            }
            return true;
        }

        public void NotifyTrigger(string name, double time)
        {
            if (PrepareTrigger())
                triggerRuntime.FireNamed(ref _flatTimeline, name, time);
        }

        public void NotifyHitSound(
            SampleSet normal,
            SampleSet addition,
            HitSoundType sounds,
            int customIndex
        )
        {
            if (PrepareTrigger())
                triggerRuntime.FireHitSound(
                    ref _flatTimeline,
                    GetCurrentMusicTime(),
                    normal,
                    addition,
                    sounds,
                    customIndex
                );
        }

        public void NotifyHitSamples(List<HitSampleInfo> samples)
        {
            if (PrepareTrigger())
                triggerRuntime.FireHitSamples(ref _flatTimeline, GetCurrentMusicTime(), samples);
        }

        public void SetUnderlay(Texture texture, bool encoded)
        {
            underlayTexture = texture;
            underlayEncoded = encoded;
        }

        public RenderTexture GetRenderTexture() => renderTexture;

        public RenderTexture GetVideoRenderTexture() => videoRT;

        // =========================================================
        //  Update: 引擎推进 + 精灵收集 + Schedule Job
        //  (约束 1: 拉开调度间距, Update 极早阶段 Schedule)
        // =========================================================

        void Update()
        {
            if (!isRendering)
                return;
            if (_flatTimeline.SpriteCount == 0 && !hasVideo)
                return;

            double musicTime = GetCurrentMusicTime();

            // 1. 视频: 时间同步 (VideoPlayer 直接解码到 videoRT)
            if (hasVideo && videoPlayer != null && videoPlayer.isPrepared)
                SyncVideoTime(musicTime);

            // 2. 以下需要 renderCamera (SB 渲染)
            if (renderCamera == null)
                return;

            ScheduleStoryboardFrame(musicTime);
        }

        void ScheduleStoryboardFrame(double musicTime)
        {
            if (
                _flatTimeline.SpriteCount > 0
                && storyboardMaterial != null
                && storyboardTextures.Count > 0
            )
            {
                if (_jobScheduled)
                {
                    _jobHandle.Complete();
                    _jobScheduled = false;
                }
                _jobActiveCount = _flatTimeline.SpriteCount;

                // Job 1: 时间轴求值 (Burst 并行, 零主线程求值)
                var evalJob = new SBEvaluateTimelineJob
                {
                    Sprites = _flatTimeline.Sprites,
                    Commands = _flatTimeline.Commands,
                    Loops = _flatTimeline.Loops,
                    FrameMap = _flatTimeline.FrameMap,
                    TriggerCommands = _flatTimeline.TriggerCommands,
                    Output = _jobInputs,
                    CurrentTime = musicTime,
                    SpriteCount = _jobActiveCount,
                    XOffset = _sbXOffset,
                };
                var evalHandle = evalJob.Schedule(_jobActiveCount, 64);

                // Job 2: 矩阵计算 (依赖 Job 1 完成)
                var buildJob = new BuildInstanceJob
                {
                    Inputs = _jobInputs,
                    OriginOffsets = _jobOriginOffsets,
                    TextureDimensions = _jobTextureDimensions,
                    Output = _jobOutput,
                    CanvasW = CanvasWidth,
                    CanvasH = CanvasHeight,
                    IsolatedY = 0f,
                    InputCount = _jobActiveCount,
                };
                _jobHandle = buildJob.Schedule(_jobActiveCount, 256, evalHandle);
                _jobScheduled = true;
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // 确定性帧捕获复用正式的求值器与 GPU 提交流程。
        public void RenderAtTime(double milliseconds)
        {
            if (!isRendering)
                return;
            ScheduleStoryboardFrame(milliseconds);
            if (_jobScheduled)
            {
                _jobHandle.Complete();
                _jobScheduled = false;
            }
            RenderInstances();
        }
#endif

        // =========================================================
        //  LateUpdate: Complete Job → SetData → GPU 提交
        //  (约束 1: LateUpdate 极末尾 Complete)
        // =========================================================

        void LateUpdate()
        {
            if (!isRendering)
                return;
            if (_jobScheduled)
            {
                _jobHandle.Complete();
                _jobScheduled = false;
            }
            RenderInstances();
        }

        void RenderInstances()
        {
            if (renderTexture == null || storyboardMaterial == null)
                return;
            drawCommands.Clear();
            drawCommands.SetRenderTarget(rasterSurface != null ? rasterSurface : renderTexture);
            drawCommands.SetViewport(new Rect(0, 0, renderTexture.width, renderTexture.height));
            drawCommands.ClearRenderTarget(false, true, Color.black);
            if (_jobActiveCount > 0)
                instanceBuffer.SetData(_jobOutput, 0, 0, _jobActiveCount);
            storyboardMaterial.SetBuffer("_InstanceData", instanceBuffer);
            // 在顶点变换前抵消隔离用的平移, 避免过大的世界坐标
            // 在半像素边界处被取整。
            var view = renderCamera.worldToCameraMatrix * Matrix4x4.Translate(IsolatedPosition);
            var vp = GL.GetGPUProjectionMatrix(renderCamera.projectionMatrix, false) * view;
            if (underlayTexture != null)
            {
                drawProperties.Clear();
                drawProperties.SetTexture(MainTexId, underlayTexture);
                drawProperties.SetFloat("_UnderlayEncoded", underlayEncoded ? 1 : 0);
                drawCommands.DrawMesh(
                    quadMesh,
                    Matrix4x4.identity,
                    storyboardMaterial,
                    0,
                    2,
                    drawProperties
                );
            }
            if (!isWidescreen)
            {
                float width = renderTexture.height * (4f / 3f);
                drawCommands.EnableScissorRect(
                    new Rect((renderTexture.width - width) * 0.5f, 0, width, renderTexture.height)
                );
            }
            int first = 0,
                texture = -1,
                blend = -1;
            for (int i = 0; i <= _jobActiveCount; i++)
            {
                int next = -1,
                    nextBlend = -1;
                if (i < _jobActiveCount && _jobOutput[i].color.w > 0)
                {
                    next = (int)_jobOutput[i].params0.x;
                    nextBlend = _jobOutput[i].params0.y > 0.5f ? 1 : 0;
                }
                if (next == texture && nextBlend == blend)
                    continue;
                if (texture >= 0 && texture < storyboardTextures.Count)
                {
                    drawProperties.Clear();
                    var source = storyboardTextures[texture];
                    drawProperties.SetTexture(MainTexId, source);
                    drawProperties.SetVector(
                        "_MainTex_TexelSize",
                        new Vector4(
                            1f / source.width,
                            1f / source.height,
                            source.width,
                            source.height
                        )
                    );
                    drawProperties.SetInt(InstanceOffsetId, first);
                    drawProperties.SetMatrix("_StoryboardVP", vp);
                    drawCommands.DrawMeshInstancedProcedural(
                        quadMesh,
                        0,
                        storyboardMaterial,
                        blend,
                        i - first,
                        drawProperties
                    );
                }
                first = i;
                texture = next;
                blend = nextBlend;
            }
            if (!isWidescreen)
                drawCommands.DisableScissorRect();
            // 先按显示方向光栅化, 再适配 Unity 的 RT 方向。
            // 翻转几何会改变半像素边界处的边缘像素归属。
            if (rasterSurface != null)
                drawCommands.Blit(
                    rasterSurface,
                    renderTexture,
                    new Vector2(1, -1),
                    new Vector2(0, 1)
                );
            // 显式的命令顺序绕过 URP 的透明排序与 LightMode 选择。
            var previousTarget = RenderTexture.active;
            bool previousSRGB = GL.sRGBWrite;
            try
            {
                GL.sRGBWrite = false;
                Graphics.ExecuteCommandBuffer(drawCommands);
            }
            finally
            {
                RenderTexture.active = previousTarget;
                GL.sRGBWrite = previousSRGB;
            }
        }

        void ClearRenderTexture()
        {
            if (renderTexture == null)
                return;
            var previous = RenderTexture.active;
            RenderTexture.active = renderTexture;
            GL.Clear(false, true, Color.clear);
            RenderTexture.active = previous;
        }

        void EnsureInstanceCapacity(int required)
        {
            if (required <= instanceCapacity)
                return;
            if (_jobScheduled)
            {
                _jobHandle.Complete();
                _jobScheduled = false;
            }

            instanceCapacity = Mathf.NextPowerOfTwo(required);
            if (_jobInputs.IsCreated)
                _jobInputs.Dispose();
            if (_jobOutput.IsCreated)
                _jobOutput.Dispose();
            _jobInputs = new NativeArray<SpriteInputData>(instanceCapacity, Allocator.Persistent);
            _jobOutput = new NativeArray<SpriteInstanceData>(
                instanceCapacity,
                Allocator.Persistent
            );
            if (instanceBuffer != null)
                instanceBuffer.Release();
            instanceBuffer = new ComputeBuffer(instanceCapacity, InstanceDataStride);
        }

        void UpdateNativeTextureDimensions()
        {
            if (_jobTextureDimensions.IsCreated)
                _jobTextureDimensions.Dispose();
            int count = textureDimensions == null ? 0 : textureDimensions.Length;
            _jobTextureDimensions = new NativeArray<int2>(count, Allocator.Persistent);
            for (int i = 0; i < count; i++)
                _jobTextureDimensions[i] = new int2(textureDimensions[i].x, textureDimensions[i].y);
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
            videoPlayer.isLooping = false; // osu! 视频只播放一次, 不循环 (末帧冻结)
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
                Debug.Log(
                    $"[SBRenderer] 视频准备完成: {vp.width}x{vp.height}, 时长={vp.length:F1}s"
                );
            };

            videoPlayer.Prepare();
            Debug.Log(
                $"[SBRenderer] VideoPlayer 已创建: {url}, RT={videoRT.width}x{videoRT.height}"
            );
        }

        void SyncVideoTime(double musicTimeMs)
        {
            // 音乐时间冻结检测（暂停）：冻结超过 0.15s 就同步暂停视频，
            // 否则视频继续播放、每 0.3s 被 drift seek 回跳，暂停期反复跳帧
            bool timeFrozen =
                _lastMusicTimeMs >= 0 && math.abs(musicTimeMs - _lastMusicTimeMs) < 0.01;
            _lastMusicTimeMs = musicTimeMs;
            _musicFrozenTimer = timeFrozen ? _musicFrozenTimer + Time.unscaledDeltaTime : 0f;
            if (_musicFrozenTimer > 0.15f)
            {
                if (videoPlayer.isPlaying)
                    videoPlayer.Pause();
                return;
            }

            // osu! 语义: 视频在 map 时间到达 offset 时开始播放 → videoTime = mapTime - offset
            double targetVideoTime = (musicTimeMs - videoOffsetMs) / 1000.0;

            if (targetVideoTime < 0)
            {
                if (videoPlayer.isPlaying)
                    videoPlayer.Pause();
                return;
            }

            // osu! 视频只播放一次: 超出视频时长后停在末帧 (不 seek, 避免 MediaFoundation 报错)
            double videoLength = videoPlayer.length;
            if (videoLength > 0 && targetVideoTime >= videoLength)
            {
                if (videoPlayer.isPlaying)
                    videoPlayer.Pause();
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

        void BuildTextures(SBStoryboard storyboard, string beatmapFolder)
        {
            textureIndexMap = new Dictionary<string, int>();
            textureDimensions = System.Array.Empty<Vector2Int>();
            storyboardTextures.Clear();

            var paths = new List<string>();
            var pathSet = new HashSet<string>();
            foreach (var element in storyboard.GetAllElementsInRenderOrder())
            {
                if (string.IsNullOrEmpty(element.ImagePath))
                    continue;
                int frameCount = element is SBStoryboardAnimation animation
                    ? animation.FrameCount
                    : 1;
                for (int frame = 0; frame < frameCount; frame++)
                {
                    string relativePath = element is SBStoryboardAnimation animated
                        ? animated.BuildFramePath(frame)
                        : element.ImagePath;
                    string key = NormalizeStoryboardPath(relativePath);
                    if (!string.IsNullOrEmpty(key) && pathSet.Add(key))
                        paths.Add(relativePath);
                }
            }

            var dimensions = new List<Vector2Int>(paths.Count);
            int skipped = 0;
            for (int i = 0; i < paths.Count; i++)
            {
                string fullPath = ResolveStoryboardAssetPath(beatmapFolder, paths[i]);
                Texture2D texture = fullPath == null ? null : LoadTexture(fullPath);
                if (texture == null)
                {
                    skipped++;
                    Debug.LogWarning($"[SBRenderer] SB 纹理缺失或无法解码: {paths[i]}");
                    continue;
                }

                int index = storyboardTextures.Count;
                storyboardTextures.Add(texture);
                textureIndexMap[NormalizeStoryboardPath(paths[i])] = index;
                dimensions.Add(new Vector2Int(texture.width, texture.height));
            }

            textureDimensions = dimensions.ToArray();
            Debug.Log(
                $"[SBRenderer] SB 纹理加载统计: {storyboardTextures.Count}/{paths.Count} 成功, {skipped} 跳过"
            );
        }

        static string NormalizeStoryboardPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;
            return path.Replace('\\', '/').Trim().Trim('"').ToLowerInvariant();
        }

        internal static string ResolveStoryboardAssetPath(string beatmapFolder, string relativePath)
        {
            if (string.IsNullOrEmpty(beatmapFolder) || string.IsNullOrEmpty(relativePath))
                return null;
            string normalized = relativePath.Replace('\\', '/').Trim().Trim('"');
            if (!System.IO.Path.HasExtension(normalized))
            {
                foreach (string extension in new[] { ".png", ".jpg", ".jpeg" })
                {
                    string candidate = ResolveStoryboardAssetPath(
                        beatmapFolder,
                        normalized + extension
                    );
                    if (candidate != null)
                        return candidate;
                }
                return null;
            }
            string exact = System.IO.Path.Combine(beatmapFolder, normalized);
            if (System.IO.File.Exists(exact))
                return exact;

            string current = beatmapFolder;
            string[] parts = normalized.Split('/');
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i];
                if (string.IsNullOrEmpty(part) || part == ".")
                    continue;
                bool isFile = i == parts.Length - 1;
                if (isFile)
                {
                    if (!System.IO.Directory.Exists(current))
                        return null;
                    foreach (string file in System.IO.Directory.GetFiles(current))
                        if (
                            string.Equals(
                                System.IO.Path.GetFileName(file),
                                part,
                                System.StringComparison.OrdinalIgnoreCase
                            )
                        )
                            return file;
                    return null;
                }

                string exactDirectory = System.IO.Path.Combine(current, part);
                if (System.IO.Directory.Exists(exactDirectory))
                {
                    current = exactDirectory;
                    continue;
                }
                if (!System.IO.Directory.Exists(current))
                    return null;
                string matched = null;
                foreach (string directory in System.IO.Directory.GetDirectories(current))
                    if (
                        string.Equals(
                            System.IO.Path.GetFileName(directory),
                            part,
                            System.StringComparison.OrdinalIgnoreCase
                        )
                    )
                    {
                        matched = directory;
                        break;
                    }
                if (matched == null)
                    return null;
                current = matched;
            }
            return null;
        }

        internal static Texture2D LoadTexture(string path)
        {
            Texture2D texture = null;
            try
            {
                if (!System.IO.File.Exists(path))
                    return null;
                // 保留源图的尺寸与像素。旧的数组打包会把每张图
                // 缩放到最大的 SB 纹理尺寸, 这会改变精灵的内容。
                var image = StbImageSharp.ImageResult.FromMemory(
                    System.IO.File.ReadAllBytes(path),
                    StbImageSharp.ColorComponents.RedGreenBlueAlpha
                );
                if (
                    image.Width > SystemInfo.maxTextureSize
                    || image.Height > SystemInfo.maxTextureSize
                )
                    throw new System.NotSupportedException(
                        "image exceeds device texture-size limit"
                    );
                // STB 返回的像素行自上而下排列, 而 Unity 的原始纹理由底部行开始存储。
                int stride = checked(image.Width * 4);
                var row = new byte[stride];
                for (int y = 0; y < image.Height / 2; y++)
                {
                    int opposite = image.Height - 1 - y;
                    System.Buffer.BlockCopy(image.Data, y * stride, row, 0, stride);
                    System.Buffer.BlockCopy(
                        image.Data,
                        opposite * stride,
                        image.Data,
                        y * stride,
                        stride
                    );
                    System.Buffer.BlockCopy(row, 0, image.Data, opposite * stride, stride);
                }
                texture = new Texture2D(
                    image.Width,
                    image.Height,
                    TextureFormat.RGBA32,
                    true,
                    true
                );
                texture.SetPixelData(image.Data, 0);
                texture.Apply(true, true);
                texture.filterMode = FilterMode.Trilinear;
                texture.anisoLevel = 1;
                texture.wrapMode = TextureWrapMode.Clamp;
                return texture;
            }
            catch (System.Exception e)
            {
                if (texture != null)
                    ReleaseObject(texture);
                Debug.LogWarning($"[SBRenderer] 纹理加载异常: {path}, {e.Message}");
                return null;
            }
        }

        // =========================================================
        //  共享资源创建
        // =========================================================

        void EnsureSBMaterial()
        {
            if (storyboardMaterial != null)
                return;
            var shader = Shader.Find("OsuVR/SBInstanced");
            if (shader == null || !shader.isSupported)
                throw new System.NotSupportedException(
                    "Storyboard shader is unavailable on this graphics backend"
                );
            storyboardMaterial = new Material(shader) { enableInstancing = true };
        }

        Mesh EnsureQuadMesh()
        {
            if (quadMesh != null)
                return quadMesh;

            quadMesh = new Mesh();
            quadMesh.name = "SB_InstancedQuad";

            quadMesh.vertices = new Vector3[]
            {
                new Vector3(-0.5f, -0.5f, 0),
                new Vector3(-0.5f, 0.5f, 0),
                new Vector3(0.5f, 0.5f, 0),
                new Vector3(0.5f, -0.5f, 0),
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

        void EnsureCameraSetup()
        {
            if (renderCamera != null)
            {
                renderCamera.aspect = RT_Width / (float)RT_Height;
                return;
            }

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
            renderCamera.aspect = RT_Width / (float)RT_Height;
            renderCamera.nearClipPlane = -100f;
            renderCamera.farClipPlane = 100f;
            renderCamera.cullingMask = 0;
            renderCamera.clearFlags = CameraClearFlags.SolidColor;
            renderCamera.backgroundColor = new Color(0f, 0f, 0f, 0f); // 透明黑: 预乘管线要求 rgb 也为 0
            renderCamera.stereoTargetEye = StereoTargetEyeMask.None;

            renderTexture = new RenderTexture(
                RT_Width,
                RT_Height,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Linear
            );
            renderTexture.name = "Storyboard encoded RGB";
            renderTexture.wrapMode = TextureWrapMode.Clamp;
            renderTexture.antiAliasing = 1;
            renderTexture.Create();
            if (SystemInfo.graphicsUVStartsAtTop)
            {
                rasterSurface = new RenderTexture(renderTexture.descriptor)
                {
                    name = "Storyboard display raster",
                    filterMode = FilterMode.Point,
                };
                rasterSurface.Create();
            }
            renderCamera.targetTexture = renderTexture;
            renderCamera.enabled = false; // 有序合成器使用同一套投影矩阵。
            ClearRenderTexture();

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
            if (cachedRGM != null)
                return cachedRGM.currentMusicTimeMs;
            return Time.time * 1000.0;
        }

        // =========================================================
        //  清理
        // =========================================================

        static void ReleaseObject(UnityEngine.Object value)
        {
            if (value == null)
                return;
            if (Application.isPlaying)
                Destroy(value);
            else
                DestroyImmediate(value);
        }

        void OnDestroy()
        {
            UnloadAll();

            DisposeJobSystem();

            instanceBuffer?.Release();
            drawCommands?.Release();

            if (renderTexture != null)
            {
                renderTexture.Release();
                ReleaseObject(renderTexture);
            }

            if (rasterSurface != null)
            {
                rasterSurface.Release();
                ReleaseObject(rasterSurface);
            }
            if (quadMesh != null)
                ReleaseObject(quadMesh);

            if (Instance == this)
                Instance = null;
        }
    }
}
