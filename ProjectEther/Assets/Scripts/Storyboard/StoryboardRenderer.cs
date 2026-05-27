using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Video;
using OsuVR.Storyboard.Data;
using OsuVR.Storyboard.Engine;

namespace OsuVR.Storyboard
{
    /// <summary>
    /// Storyboard 渲染隔离区 ("地下绿幕放映室")
    ///
    /// 在偏远坐标 (Y=-1000) 建立正交摄像机 + RenderTexture，与玩家 3D 舞台物理隔离。
    /// 支持三种复合模式:
    ///   1. 纯 Storyboard: 仅 SB 精灵渲染
    ///   2. 纯 Video: 仅视频背景
    ///   3. Video + Storyboard: 视频作为远景背景 + SB 精灵叠加其上
    ///
    /// 关键设计:
    ///   - 视频渲染为正交相机最远端 (z=50) 的巨型 Quad，作为实景背景
    ///   - SB 精灵在 z=0 层渲染，天然覆盖在视频之上
    ///   - 视频音频强制静音，防止与 AudioManager 重音
    ///   - VideoOffset 与 CurrentDSPTime 精准对齐
    ///   - 16:9 视频自动 letterbox 适配 4:3 画布
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

        // ---- 视频背景 Quad 深度 (正交相机最远端) ----
        const float VideoQuadZ = 50f;
        const float SBQuadZ = 0f;

        // ---- 运行时对象 ----
        Camera renderCamera;
        RenderTexture renderTexture;
        GameObject isolatedRoot;
        int storyboardLayer;

        // ---- 引擎 ----
        SBOsbPlayer osbPlayer;
        string currentBeatmapFolder;

        // ---- 视频 ----
        VideoPlayer videoPlayer;
        GameObject videoQuadGo;
        bool hasVideo;
        int videoOffsetMs;

        // ---- 纹理缓存 ----
        Dictionary<string, Texture2D> textureCache = new Dictionary<string, Texture2D>();
        Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();
        static Texture2D whitePixel;

        // ---- 对象池 ----
        Dictionary<int, PooledSprite> activePool = new Dictionary<int, PooledSprite>();
        Stack<PooledSprite> freePool = new Stack<PooledSprite>();
        MaterialPropertyBlock sharedMPB;

        bool isRendering;

        // =========================================================
        //  Pooled Sprite
        // =========================================================

        class PooledSprite
        {
            public GameObject Go;
            public SpriteRenderer Sr;
            public int CurrentId;
        }

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

            sharedMPB = new MaterialPropertyBlock();
            EnsureLayerExists();
        }

        // =========================================================
        //  公开 API
        // =========================================================

        /// <summary>
        /// 加载 Storyboard 数据并启动引擎 + 渲染
        /// </summary>
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
            PreloadTextures(storyboard, beatmapFolder);

            osbPlayer = new SBOsbPlayer();
            osbPlayer.LoadStoryboard(storyboard);

            isRendering = true;
            Debug.Log($"[SBRenderer] 已加载 Storyboard: {storyboard.TotalElementCount} 个元素, RT={RT_Width}x{RT_Height}");
        }

        /// <summary>
        /// 加载视频并开始播放
        /// </summary>
        public void LoadVideo(string videoPath, int videoOffset)
        {
            if (string.IsNullOrEmpty(videoPath)) return;

            EnsureCameraSetup();

            hasVideo = true;
            videoOffsetMs = videoOffset;

            CreateVideoPlayer(videoPath);

            Debug.Log($"[SBRenderer] 已加载视频: {System.IO.Path.GetFileName(videoPath)}, offset={videoOffset}ms");
        }

        /// <summary>
        /// 同时加载视频 + Storyboard (复合模式)
        /// </summary>
        public void LoadVideoAndStoryboard(string videoPath, int videoOffset,
            SBStoryboard storyboard, string beatmapFolder)
        {
            // 先加载 SB，再加载视频 (视频在底层)
            LoadStoryboard(storyboard, beatmapFolder);
            LoadVideo(videoPath, videoOffset);
        }

        /// <summary>
        /// 停止渲染并释放所有资源
        /// </summary>
        public void UnloadStoryboard()
        {
            isRendering = false;

            osbPlayer?.Unload();
            osbPlayer = null;

            // 回收所有活跃对象
            foreach (var kvp in activePool)
                ReturnToPool(kvp.Value);
            activePool.Clear();

            // 释放纹理缓存
            foreach (var kvp in textureCache)
            {
                if (kvp.Value != null && kvp.Value != whitePixel) Destroy(kvp.Value);
            }
            textureCache.Clear();

            foreach (var kvp in spriteCache)
            {
                if (kvp.Value != null) Destroy(kvp.Value);
            }
            spriteCache.Clear();
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

            if (videoQuadGo != null)
            {
                Destroy(videoQuadGo);
                videoQuadGo = null;
            }
        }

        public void UnloadAll()
        {
            UnloadStoryboard();
            UnloadVideo();
        }

        public RenderTexture GetRenderTexture() => renderTexture;

        // =========================================================
        //  每帧更新
        // =========================================================

        void LateUpdate()
        {
            if (!isRendering) return;
            if (osbPlayer == null && !hasVideo) return;
            if (renderCamera == null) return;

            double musicTime = GetCurrentMusicTime();

            // 1. 同步视频时间轴
            if (hasVideo && videoPlayer != null && videoPlayer.isPrepared)
                SyncVideoTime(musicTime);

            // 2. 推进纯计算引擎
            if (osbPlayer != null)
            {
                osbPlayer.Update(musicTime);

                // 3. 对象池调度
                var currentFrame = CollectActiveSprites();

                // 归还消失的
                var toReturn = new List<int>();
                foreach (var kvp in activePool)
                {
                    if (!currentFrame.ContainsKey(kvp.Key))
                        toReturn.Add(kvp.Key);
                }
                for (int i = 0; i < toReturn.Count; i++)
                {
                    ReturnToPool(activePool[toReturn[i]]);
                    activePool.Remove(toReturn[i]);
                }

                // 处理活跃 sprite
                foreach (var kvp in currentFrame)
                {
                    int id = kvp.Key;
                    var playingSprite = kvp.Value;

                    if (!activePool.TryGetValue(id, out var pooled))
                    {
                        pooled = GetFromPool(id, playingSprite.Element);
                        activePool[id] = pooled;
                    }

                    UpdatePooledSprite(pooled, playingSprite.State, playingSprite.Element);
                }
            }

            // 4. 手动触发摄像机渲染
            renderCamera.Render();
        }

        Dictionary<int, SBPlayingSprite> CollectActiveSprites()
        {
            var frame = new Dictionary<int, SBPlayingSprite>();
            for (int layer = 0; layer < 5; layer++)
            {
                var sprite = osbPlayer.GetLayerActiveHead(layer);
                var tail = osbPlayer.GetLayerActiveTail(layer);
                while (sprite != tail)
                {
                    frame[sprite.GetHashCode()] = sprite;
                    sprite = sprite.Next;
                }
            }
            return frame;
        }

        // =========================================================
        //  视频播放
        // =========================================================

        void CreateVideoPlayer(string videoPath)
        {
            // 创建 VideoPlayer GameObject
            var vpGo = new GameObject("[SB_VideoPlayer]");
            vpGo.transform.SetParent(transform);

            videoPlayer = vpGo.AddComponent<VideoPlayer>();
            videoPlayer.source = VideoSource.Url;
            videoPlayer.url = videoPath;
            videoPlayer.playOnAwake = false;
            videoPlayer.isLooping = true;
            videoPlayer.skipOnDrop = true;

            // 关键: 强制静音，防止与 AudioManager 重音
            videoPlayer.audioOutputMode = VideoAudioOutputMode.None;

            // 渲染到 RenderTexture (我们自己的 RT)
            videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            videoPlayer.targetTexture = renderTexture;

            // 准备完成后自动播放
            videoPlayer.prepareCompleted += (vp) =>
            {
                vp.Play();
                Debug.Log("[SBRenderer] 视频准备完成，开始播放");
            };

            // 创建视频背景 Quad (位于正交相机最远端)
            CreateVideoBackgroundQuad();

            videoPlayer.Prepare();
        }

        /// <summary>
        /// 创建视频背景 Quad: 放置在正交相机最远端 (z=50)
        /// 自动计算 16:9 → 4:3 的 letterbox 缩放
        /// </summary>
        void CreateVideoBackgroundQuad()
        {
            videoQuadGo = new GameObject("[SB_VideoQuad]");
            videoQuadGo.layer = storyboardLayer;
            videoQuadGo.transform.SetParent(isolatedRoot.transform, false);
            videoQuadGo.transform.localPosition = new Vector3(0, 0, VideoQuadZ);

            // 使用 Quad Mesh
            var meshFilter = videoQuadGo.AddComponent<MeshFilter>();
            meshFilter.mesh = CreateFullScreenQuad();

            // 材质: URP Unlit, 渲染视频纹理
            var renderer = videoQuadGo.AddComponent<MeshRenderer>();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Unlit/Texture"));
            mat.SetFloat("_Surface", 0); // Opaque
            mat.SetFloat("_ZWrite", 1);
            mat.renderQueue = (int)RenderQueue.Geometry;
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            // 16:9 视频适配 4:3 画布的 letterbox 缩放
            // 视频纹理由 VideoPlayer 直接写入 RT，这里只是占位
            // 实际缩放在 UpdateVideoQuadScale 中动态计算
            UpdateVideoQuadScale();
        }

        /// <summary>
        /// 动态计算视频 Quad 缩放，确保 16:9 视频在 4:3 画布中不变形
        /// 策略: "Fit" 模式 — 等比缩放以完全显示，多余区域留黑
        /// </summary>
        void UpdateVideoQuadScale()
        {
            if (videoQuadGo == null || videoPlayer == null) return;

            float videoW = videoPlayer.width;
            float videoH = videoPlayer.height;
            if (videoW <= 0 || videoH <= 0) return;

            float canvasAspect = (float)CanvasWidth / CanvasHeight;  // 4:3 = 1.333
            float videoAspect = videoW / videoH;                      // 16:9 = 1.778

            float scaleX, scaleY;
            if (videoAspect > canvasAspect)
            {
                // 视频更宽: 宽度填满，高度留黑 (上下 letterbox)
                scaleX = CanvasWidth;
                scaleY = CanvasWidth / videoAspect;
            }
            else
            {
                // 视频更高: 高度填满，宽度留黑 (左右 pillarbox)
                scaleY = CanvasHeight;
                scaleX = CanvasHeight * videoAspect;
            }

            videoQuadGo.transform.localScale = new Vector3(scaleX, scaleY, 1f);
        }

        /// <summary>
        /// 同步视频时间轴: videoPlayer.time = musicTime + videoOffset
        /// 处理负偏移 (视频在音乐之前开始) 和大跳转
        /// </summary>
        void SyncVideoTime(double musicTimeMs)
        {
            double targetVideoTime = (musicTimeMs + videoOffsetMs) / 1000.0;

            if (targetVideoTime < 0)
            {
                // 视频尚未到起始点，暂停并等待
                if (videoPlayer.isPlaying) videoPlayer.Pause();
                return;
            }

            if (!videoPlayer.isPlaying)
                videoPlayer.Play();

            // 如果时间差超过 0.1 秒，执行跳转 (防止累积漂移)
            double drift = videoPlayer.time - targetVideoTime;
            if (drift > 0.1 || drift < -0.1)
            {
                videoPlayer.time = targetVideoTime;
            }
        }

        // =========================================================
        //  对象池操作
        // =========================================================

        PooledSprite GetFromPool(int id, SBElement element)
        {
            PooledSprite pooled;

            if (freePool.Count > 0)
            {
                pooled = freePool.Pop();
                pooled.Go.SetActive(true);
            }
            else
            {
                pooled = CreatePooledSprite();
            }

            pooled.CurrentId = id;

            Sprite sprite = GetSpriteForElement(element);
            if (sprite != null)
                pooled.Sr.sprite = sprite;

            return pooled;
        }

        void ReturnToPool(PooledSprite pooled)
        {
            if (pooled == null) return;
            pooled.Go.SetActive(false);
            pooled.Sr.sprite = null;
            pooled.CurrentId = 0;
            freePool.Push(pooled);
        }

        PooledSprite CreatePooledSprite()
        {
            var go = new GameObject("SB_Sprite");
            go.layer = storyboardLayer;
            go.transform.SetParent(isolatedRoot.transform, false);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sharedMaterial = CreateSBMaterial();
            sr.shadowCastingMode = ShadowCastingMode.Off;
            sr.receiveShadows = false;
            sr.sortingOrder = 0;

            return new PooledSprite { Go = go, Sr = sr };
        }

        // =========================================================
        //  视觉更新: SBRenderState → Transform + SpriteRenderer
        // =========================================================

        void UpdatePooledSprite(PooledSprite pooled, SBRenderState state, SBElement element)
        {
            var go = pooled.Go;
            var sr = pooled.Sr;

            // 可见性
            bool visible = state.Alpha > 0.001f;
            if (go.activeSelf != visible)
                go.SetActive(visible);
            if (!visible) return;

            // 位置 (osu! → Unity 坐标)
            float worldX = state.X - CanvasWidth * 0.5f;
            float worldY = CanvasHeight * 0.5f - state.Y;
            go.transform.localPosition = new Vector3(worldX, worldY, SBQuadZ);

            // 缩放
            go.transform.localScale = new Vector3(state.ScaleX, state.ScaleY, 1f);

            // 旋转 (osu! 弧度顺时针 → Unity 逆时针)
            go.transform.localRotation = Quaternion.Euler(0, 0, -state.Rotation * Mathf.Rad2Deg);

            // 颜色 + 透明度
            sharedMPB.Clear();
            sr.GetPropertyBlock(sharedMPB);
            Color c = new Color(state.R, state.G, state.B, state.Alpha);
            sharedMPB.SetColor("_Color", c);
            sr.SetPropertyBlock(sharedMPB);

            // 翻转
            sr.flipX = state.FlipH;
            sr.flipY = state.FlipV;

            // 混合模式
            var mat = sr.sharedMaterial;
            if (state.Additive)
            {
                mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)BlendMode.One);
            }
            else
            {
                mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            }
        }

        // =========================================================
        //  Sprite 纹理管理
        // =========================================================

        Sprite GetSpriteForElement(SBElement element)
        {
            string path = element.ImagePath;
            if (string.IsNullOrEmpty(path)) return null;

            if (spriteCache.TryGetValue(path, out var cached))
                return cached;

            Texture2D tex = null;
            if (textureCache.TryGetValue(path, out tex) && tex != null)
            {
                Vector2 pivot = GetPivotForOrigin(element.Origin);
                var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), pivot, 100f);
                spriteCache[path] = sprite;
                return sprite;
            }

            return null;
        }

        Vector2 GetPivotForOrigin(SBOrigin origin)
        {
            switch (origin)
            {
                case SBOrigin.TopLeft:      return new Vector2(0f, 1f);
                case SBOrigin.TopCentre:    return new Vector2(0.5f, 1f);
                case SBOrigin.TopRight:     return new Vector2(1f, 1f);
                case SBOrigin.CentreLeft:   return new Vector2(0f, 0.5f);
                case SBOrigin.Centre:       return new Vector2(0.5f, 0.5f);
                case SBOrigin.CentreRight:  return new Vector2(1f, 0.5f);
                case SBOrigin.BottomLeft:   return new Vector2(0f, 0f);
                case SBOrigin.BottomCentre: return new Vector2(0.5f, 0f);
                case SBOrigin.BottomRight:  return new Vector2(1f, 0f);
                default:                    return new Vector2(0.5f, 0.5f);
            }
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

            isolatedRoot = new GameObject("[SB_IsolatedRoot]");
            isolatedRoot.transform.SetParent(transform);
            isolatedRoot.transform.position = IsolatedPosition;

            var camGo = new GameObject("[SB_Camera]");
            camGo.transform.SetParent(isolatedRoot.transform);
            camGo.transform.localPosition = new Vector3(0, 0, -10);

            renderCamera = camGo.AddComponent<Camera>();
            renderCamera.orthographic = true;
            renderCamera.orthographicSize = CanvasHeight * 0.5f;
            renderCamera.aspect = (float)CanvasWidth / CanvasHeight;
            renderCamera.nearClipPlane = 0.1f;
            renderCamera.farClipPlane = 100f;
            renderCamera.cullingMask = 1 << storyboardLayer;
            renderCamera.clearFlags = CameraClearFlags.SolidColor;
            renderCamera.backgroundColor = Color.black;
            renderCamera.stereoTargetEye = StereoTargetEyeMask.None;

            renderTexture = new RenderTexture(RT_Width, RT_Height, 0, RenderTextureFormat.ARGB32);
            renderTexture.antiAliasing = 1;
            renderTexture.Create();
            renderCamera.targetTexture = renderTexture;
        }

        // =========================================================
        //  纹理管理
        // =========================================================

        void PreloadTextures(SBStoryboard storyboard, string beatmapFolder)
        {
            foreach (var element in storyboard.GetAllElementsInRenderOrder())
            {
                if (string.IsNullOrEmpty(element.ImagePath)) continue;
                if (textureCache.ContainsKey(element.ImagePath)) continue;

                string fullPath = System.IO.Path.Combine(beatmapFolder, element.ImagePath);
                textureCache[element.ImagePath] = LoadTexture(fullPath);
            }
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

        Material CreateSBMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");
            var mat = new Material(shader);
            mat.SetFloat("_Surface", 1);
            mat.SetFloat("_Blend", 0);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0);
            mat.renderQueue = (int)RenderQueue.Transparent;
            mat.enableInstancing = true;
            return mat;
        }

        static Mesh CreateFullScreenQuad()
        {
            var mesh = new Mesh();
            mesh.name = "SB_FullScreenQuad";
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

            if (renderTexture != null)
            {
                renderTexture.Release();
                Destroy(renderTexture);
            }

            if (whitePixel != null) { Destroy(whitePixel); whitePixel = null; }

            if (Instance == this) Instance = null;
        }
    }
}
