using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Video;
using OsuVR.Storyboard.Data;
using OsuVR.Storyboard.Engine;

namespace OsuVR.Storyboard
{
    /// <summary>
    /// 纯 GPU 实例化 Storyboard 渲染器
    ///
    /// 零运行时 GameObject (SB 精灵全部通过 DrawMeshInstancedIndirect 提交)
    /// 架构:
    ///   - Texture2DArray: 加载时打包所有 SB 纹理到一张纹理数组
    ///   - ComputeBuffer: 每帧填充 SpriteInstanceData[] → GPU
    ///   - 双 Pass: Alpha Blend (Pass 0) / Additive (Pass 1)
    ///   - 按 Layer 顺序遍历 (Background→Overlay)，保证深度正确
    ///   - 视频背景通过 Graphics.DrawMesh 渲染 (无 GameObject)
    /// </summary>
    public class StoryboardRenderer : MonoBehaviour
    {
        public static StoryboardRenderer Instance { get; private set; }

        // ---- osu! 标准画布 (4:3) ----
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
        const int MaxInstancesPerPass = 2048;
        const int InstanceDataStride = 96; // sizeof(SpriteInstanceData)

        // ---- 视频 Quad 深度 ----
        const float VideoQuadZ = 50f;
        const float SBQuadZ = 0f;

        // =========================================================
        //  GPU 实例数据结构 (与 SBInstanced.shader 精确对齐)
        //  全部使用 4 字节对齐类型，禁止 bool/int 混用
        // =========================================================

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        struct SpriteInstanceData
        {
            public Matrix4x4 objectToWorld; // 64 bytes (16 floats)
            public Vector4 color;           // 16 bytes (RGBA 0~1)
            public Vector4 params0;         // 16 bytes (x=texIndex, y=blendMode, z=flipH, w=flipV)
        }   // Total: 96 bytes, 16-byte aligned

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
        Dictionary<string, int> textureIndexMap; // ImagePath → Texture2DArray layer index
        Vector2Int[] textureDimensions;          // 每层纹理的像素尺寸 (width, height)
        int[] cachedTextureIndex;                // 每个元素缓存的纹理索引 (非动画)
        static Texture2D whitePixel;

        // ---- GPU 缓冲区 ----
        SpriteInstanceData[] alphaData;
        SpriteInstanceData[] additiveData;
        ComputeBuffer alphaBuffer;
        ComputeBuffer additiveBuffer;

        // ---- 共享资源 ----
        Material sbMaterial;
        Mesh quadMesh;
        MaterialPropertyBlock videoMPB;

        // ---- 状态 ----
        bool isRendering;


        // ---- Origin 偏移缓存 ----
        // 索引必须匹配 SBOrigin 枚举值:
        //   TopLeft=0, Centre=1, CentreLeft=2, TopRight=3, BottomCentre=4,
        //   TopCentre=5, Custom=6, CentreRight=7, BottomLeft=8, BottomRight=9
        // 偏移含义: 精灵中心到 origin 点的偏移 (归一化, [-0.5, 0.5])
        static readonly Vector2[] OriginOffsets = new Vector2[]
        {
            new Vector2(-0.5f,  0.5f),  // [0] TopLeft
            new Vector2( 0.0f,  0.0f),  // [1] Centre
            new Vector2(-0.5f,  0.0f),  // [2] CentreLeft
            new Vector2( 0.5f,  0.5f),  // [3] TopRight
            new Vector2( 0.0f, -0.5f),  // [4] BottomCentre
            new Vector2( 0.0f,  0.5f),  // [5] TopCentre
            new Vector2( 0.0f,  0.0f),  // [6] Custom → fallback to Centre
            new Vector2( 0.5f,  0.0f),  // [7] CentreRight
            new Vector2(-0.5f, -0.5f),  // [8] BottomLeft
            new Vector2( 0.5f, -0.5f),  // [9] BottomRight
        };

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

            // 预分配实例数据数组
            alphaData = new SpriteInstanceData[MaxInstancesPerPass];
            additiveData = new SpriteInstanceData[MaxInstancesPerPass];

            // 创建 GPU 缓冲区
            alphaBuffer = new ComputeBuffer(MaxInstancesPerPass, InstanceDataStride);
            additiveBuffer = new ComputeBuffer(MaxInstancesPerPass, InstanceDataStride);

            EnsureLayerExists();
        }

        // =========================================================
        //  公开 API
        // =========================================================

        public void LoadStoryboard(SBStoryboard storyboard, string beatmapFolder)
        {
            UnloadStoryboard();

            if (storyboard == null || storyboard.TotalElementCount == 0)
            {
                Debug.Log("[SBRenderer] Storyboard 为空，跳过加载");
                return;
            }

            EnsureCameraSetup();
            currentBeatmapFolder = beatmapFolder;

            // 打包纹理到 Texture2DArray
            BuildTextureArray(storyboard, beatmapFolder);

            // 创建 SB 材质 (使用自定义 instanced shader)
            EnsureSBMaterial();

            // 启动引擎
            osbPlayer = new SBOsbPlayer();
            osbPlayer.LoadStoryboard(storyboard);

            // 缓存非动画精灵的纹理索引 (避免每帧 Dictionary 查找)
            CacheTextureIndices();

            isRendering = true;
            Debug.Log($"[SBRenderer] 已加载 Storyboard: {storyboard.TotalElementCount} 个元素, 纹理数组 {textureArray.depth} 层");
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
            SBStoryboard storyboard, string beatmapFolder)
        {
            LoadStoryboard(storyboard, beatmapFolder);
            LoadVideo(videoPath, videoOffset);
        }

        public void UnloadStoryboard()
        {
            isRendering = false;

            osbPlayer?.Unload();
            osbPlayer = null;

            // 释放纹理数组
            if (textureArray != null) { Destroy(textureArray); textureArray = null; }
            textureIndexMap?.Clear();

            // 释放 SB 材质
            if (sbMaterial != null) { Destroy(sbMaterial); sbMaterial = null; }
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
        //  每帧更新 — GPU 实例化渲染管线
        // =========================================================

        void LateUpdate()
        {
            if (!isRendering) return;
            if (osbPlayer == null && !hasVideo) return;
            if (renderCamera == null) return;

            double musicTime = GetCurrentMusicTime();

            // 1. 视频时间同步
            if (hasVideo && videoPlayer != null && videoPlayer.isPrepared)
                SyncVideoTime(musicTime);

            // 2. 推进引擎
            if (osbPlayer != null)
                osbPlayer.Update(musicTime);

            // 3. 绘制视频背景 Quad (z=50, 最远端)
            if (hasVideo && videoMaterial != null && videoQuadMesh != null)
                DrawVideoQuad();

            // 4. GPU 实例化绘制 SB 精灵 (z=0, 覆盖在视频上方)
            if (osbPlayer != null && sbMaterial != null && textureArray != null)
                DrawStoryboardInstances(musicTime);

            // 注意: 不要调用 Camera.Render()！
            // DrawMeshInstancedProcedural 的命令会在 Unity 自动渲染阶段被处理。
            // 红屏测试证明: 不调用 Camera.Render() 时，绘制命令能正常执行。
        }

        // =========================================================
        //  GPU 实例化核心: 收集 → 填充 → 提交
        // =========================================================

        void DrawStoryboardInstances(double musicTime)
        {
            int alphaCount = 0;
            int additiveCount = 0;

            // 按 Layer 顺序遍历 (0=Background → 4=Overlay)
            for (int layer = 0; layer < 5; layer++)
            {
                var sprite = osbPlayer.GetLayerActiveHead(layer);
                var tail = osbPlayer.GetLayerActiveTail(layer);

                while (sprite != tail)
                {
                    var state = sprite.State;

                    // 可见性裁剪
                    if (state.Alpha > 0.001f)
                    {
                        // 纹理索引: 优先使用缓存，动画才走 Dictionary
                        int texIndex = sprite.CachedTexIndex;
                        if (texIndex < 0)
                            texIndex = ResolveTextureIndex(sprite.Element, musicTime);

                        if (texIndex >= 0)
                        {
                            Vector2Int texSize = textureDimensions[texIndex];
                            Matrix4x4 matrix = BuildInstanceMatrix(state, sprite.Element, texSize);

                            var instance = new SpriteInstanceData
                            {
                                objectToWorld = matrix,
                                color = new Vector4(state.R, state.G, state.B, state.Alpha),
                                params0 = new Vector4(
                                    texIndex,
                                    state.Additive ? 1f : 0f,
                                    state.FlipH ? 1f : 0f,
                                    state.FlipV ? 1f : 0f),
                            };

                            if (state.Additive)
                            {
                                if (additiveCount < MaxInstancesPerPass)
                                    additiveData[additiveCount++] = instance;
                            }
                            else
                            {
                                if (alphaCount < MaxInstancesPerPass)
                                    alphaData[alphaCount++] = instance;
                            }
                        }
                    }

                    sprite = sprite.Next;
                }
            }

            // 提交 GPU 绘制 (bounds 极大, camera=null 自动匹配)
            var bounds = new Bounds(IsolatedPosition, new Vector3(10000000f, 10000000f, 10000000f));

            if (alphaCount > 0)
            {
                alphaBuffer.SetData(alphaData, 0, 0, alphaCount);
                sbMaterial.SetBuffer("_InstanceData", alphaBuffer);
                sbMaterial.SetTexture("_MainTexArray", textureArray);
                Graphics.DrawMeshInstancedProcedural(quadMesh, 0, sbMaterial, bounds, alphaCount,
                    null, ShadowCastingMode.Off, false, storyboardLayer, null);
            }

            if (additiveCount > 0)
            {
                additiveBuffer.SetData(additiveData, 0, 0, additiveCount);
                sbMaterial.SetBuffer("_InstanceData", additiveBuffer);
                sbMaterial.SetTexture("_MainTexArray", textureArray);
                Graphics.DrawMeshInstancedProcedural(quadMesh, 1, sbMaterial, bounds, additiveCount,
                    null, ShadowCastingMode.Off, false, storyboardLayer, null);
            }
        }


        /// <summary>
        /// 构建实例矩阵: osu! 坐标 → Unity 世界坐标
        /// 处理 Origin 偏移、缩放、旋转、翻转
        /// </summary>
        Matrix4x4 BuildInstanceMatrix(SBRenderState state, SBElement element, Vector2Int texSize)
        {
            float x = state.X - CanvasWidth * 0.5f;
            float y = -(state.Y - CanvasHeight * 0.5f) + IsolatedPosition.y;

            float scaleX = texSize.x * state.ScaleX;
            float scaleY = texSize.y * state.ScaleY;

            int originIdx = (int)element.Origin;
            if ((uint)originIdx >= (uint)OriginOffsets.Length) originIdx = 1;
            Vector2 pivot = OriginOffsets[originIdx];

            if (state.FlipH) pivot.x = -pivot.x;
            if (state.FlipV) pivot.y = -pivot.y;

            Matrix4x4 m = Matrix4x4.identity;

            // 快速路径: 无旋转时跳过 trig
            if (state.Rotation == 0f)
            {
                m.m00 = scaleX;
                m.m11 = scaleY;
                m.m03 = x - pivot.x * scaleX;
                m.m13 = y - pivot.y * scaleY;
            }
            else
            {
                float cosR = Mathf.Cos(-state.Rotation);
                float sinR = Mathf.Sin(-state.Rotation);
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
            return m;
        }

        /// <summary>
        /// 解析元素的纹理索引 (动画按当前帧解析)
        /// </summary>
        int ResolveTextureIndex(SBElement element, double musicTime)
        {
            string path = element.ImagePath;
            if (string.IsNullOrEmpty(path)) return -1;

            // 动画: 根据当前时间确定帧
            if (element is SBStoryboardAnimation anim)
            {
                int frame = anim.GetCurrentFrame(musicTime, 0); // startTime 已在引擎内部处理
                path = anim.BuildFramePath(frame);
            }

            if (textureIndexMap != null && textureIndexMap.TryGetValue(path, out int idx))
                return idx;

            // fallback: 尝试原始路径
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

            // APIOnly: 视频帧仅通过 videoPlayer.texture 提供给材质，
            // 不自动渲染到任何 RT。由 DrawVideoQuad 负责将视频 Quad 绘制到孤立摄像机的 RT 上。
            videoPlayer.renderMode = VideoRenderMode.APIOnly;

            videoPlayer.prepareCompleted += (vp) =>
            {
                vp.Play();
                UpdateVideoQuadScale();
                Debug.Log("[SBRenderer] 视频准备完成，开始播放");
            };

            // 创建视频 Quad (Graphics.DrawMesh 方式)
            CreateVideoQuad();

            videoPlayer.Prepare();
        }

        void CreateVideoQuad()
        {
            videoQuadMesh = CreateFullScreenQuad();

            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Unlit/Texture");
            videoMaterial = new Material(shader);
            videoMaterial.SetFloat("_Surface", 0); // Opaque
            videoMaterial.SetFloat("_ZWrite", 1);
            videoMaterial.renderQueue = (int)RenderQueue.Geometry;

            videoMPB = new MaterialPropertyBlock();

            // 初始矩阵 (位置在相机最远端)
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
            // 收集所有唯一的 ImagePath (含动画帧)
            var paths = new List<string>();
            var pathSet = new HashSet<string>();

            foreach (var element in storyboard.GetAllElementsInRenderOrder())
            {
                if (string.IsNullOrEmpty(element.ImagePath)) continue;

                if (element is SBStoryboardAnimation anim)
                {
                    // 为动画的每一帧生成路径
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

            // 加载所有纹理并找到最大尺寸
            var textures = new Texture2D[paths.Count];
            int maxWidth = 0, maxHeight = 0;

            for (int i = 0; i < paths.Count; i++)
            {
                textures[i] = LoadTexture(System.IO.Path.Combine(beatmapFolder, paths[i]));
                if (textures[i].width > maxWidth) maxWidth = textures[i].width;
                if (textures[i].height > maxHeight) maxHeight = textures[i].height;
            }

            // 限制最大尺寸 (节省显存)
            maxWidth = Mathf.Min(maxWidth, 2048);
            maxHeight = Mathf.Min(maxHeight, 2048);

            // 创建 Texture2DArray
            textureArray = new Texture2DArray(maxWidth, maxHeight, paths.Count,
                TextureFormat.RGBA32, true, false);
            textureArray.filterMode = FilterMode.Bilinear;
            textureArray.wrapMode = TextureWrapMode.Clamp;

            // 逐层复制纹理数据 (使用 SetPixels 而非 CopyTexture, 更可靠)
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

            // 验证: 在 Apply 之前读回像素 (Apply 会释放 CPU 副本导致不可读)
            var verifyPixels = textureArray.GetPixels(0, 0);
            bool allBlack = true;
            for (int p = 0; p < verifyPixels.Length; p++)
            {
                if (verifyPixels[p].r > 0.01f || verifyPixels[p].g > 0.01f || verifyPixels[p].b > 0.01f)
                { allBlack = false; break; }
            }
            Debug.Log($"[SBRenderer] 纹理数组验证: layer[0] {(allBlack ? "全黑!" : "有内容")} ({verifyPixels.Length} 像素)");

            textureArray.Apply(true, true); // 生成 mipmap, 释放 CPU 副本

            RenderTexture.active = prevRT;
            RenderTexture.ReleaseTemporary(tempRT);

            // 构建路径→索引映射 + 纹理像素尺寸缓存 (必须在销毁源纹理之前!)
            textureIndexMap = new Dictionary<string, int>(paths.Count);
            textureDimensions = new Vector2Int[paths.Count];
            for (int i = 0; i < paths.Count; i++)
            {
                textureIndexMap[paths[i]] = i;
                textureDimensions[i] = new Vector2Int(textures[i].width, textures[i].height);
            }

            // 释放源纹理
            for (int i = 0; i < textures.Length; i++)
            {
                if (textures[i] != null && textures[i] != whitePixel)
                    Destroy(textures[i]);
            }

            Debug.Log($"[SBRenderer] 纹理数组打包完成: {paths.Count} 层, {maxWidth}x{maxHeight}");
            for (int i = 0; i < paths.Count; i++)
                Debug.Log($"  Tex[{i}] {paths[i]} → {textureDimensions[i].x}x{textureDimensions[i].y}");
        }

        /// <summary>
        /// 预缓存所有非动画精灵的纹理索引，避免每帧 Dictionary 查找
        /// </summary>
        void CacheTextureIndices()
        {
            if (osbPlayer == null || textureIndexMap == null) return;
            osbPlayer.ForEachSprite((layer, sprite) =>
            {
                var element = sprite.Element;
                if (element is SBStoryboardAnimation)
                {
                    sprite.CachedTexIndex = -1; // 动画需要每帧解析
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
            if (sbMaterial != null) return;

            var shader = Shader.Find("OsuVR/SBInstanced");
            if (shader == null)
            {
                Debug.LogError("[SBRenderer] 找不到 Shader 'OsuVR/SBInstanced'!");
                return;
            }
            sbMaterial = new Material(shader);
            sbMaterial.enableInstancing = true;
            Debug.Log($"[SBRenderer] 材质创建成功: shader={shader.name}, isSupported={shader.isSupported}, enableInstancing={sbMaterial.enableInstancing}");
        }

        Mesh EnsureQuadMesh()
        {
            if (quadMesh != null) return quadMesh;

            quadMesh = new Mesh();
            quadMesh.name = "SB_InstancedQuad";

            // 居中 quad, [-0.5, 0.5] 范围
            // UV: 左下(0,0) → 右上(1,1), 与 osu! 坐标系一致
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

            // 确保 quad mesh 已创建
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
            // 不设置 aspect，让它自动使用 RT 的 16:9 比例，避免 4:3→16:9 拉伸变形
            renderCamera.nearClipPlane = -100f;
            renderCamera.farClipPlane = 100f;
            renderCamera.cullingMask = 1 << storyboardLayer;
            renderCamera.clearFlags = CameraClearFlags.SolidColor;
            renderCamera.backgroundColor = new Color(0.15f, 0.0f, 0.15f, 1.0f); // 深紫色背景 (调试用)
            renderCamera.stereoTargetEye = StereoTargetEyeMask.None;

            renderTexture = new RenderTexture(RT_Width, RT_Height, 0, RenderTextureFormat.ARGB32);
            renderTexture.antiAliasing = 1;
            renderTexture.Create();
            renderCamera.targetTexture = renderTexture;
        }

        // =========================================================
        //  音乐时间获取
        // =========================================================

        RhythmGameManager cachedRGM;

        double GetCurrentMusicTime()
        {
            if (cachedRGM == null) cachedRGM = FindObjectOfType<RhythmGameManager>();
            if (cachedRGM != null) return cachedRGM.currentMusicTimeMs;
            return Time.time * 1000.0;
        }

        // =========================================================
        //  Cleanup
        // =========================================================

        void OnDestroy()
        {
            UnloadAll();

            // 释放 GPU 缓冲区
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
