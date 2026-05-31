using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace OsuVR.Storyboard
{
    /// <summary>
    /// 全息幕布管理器：在玩家正前方动态生成带有边缘羽化的半透明屏幕。
    /// 纯代码驱动，不依赖 Prefab。遵循 EtherealEnvironment 的 Singleton 模式。
    /// 支持通过设置面板调整距离、透明度和开关。
    ///
    /// 双层架构:
    ///   - 底层: 静态背景图 (screenMaterial) — 始终存在, 作为兜底
    ///   - 顶层: SB/视频 Overlay (overlayMaterial) — 叠加在背景图之上
    ///   - SB 的 RenderTexture alpha=0 区域自动穿透, 显示底层背景图
    /// </summary>
    public class HolographicScreenManager : MonoBehaviour
    {
        public static HolographicScreenManager Instance { get; private set; }

        // --- 幕布默认参数 ---
        const float ScreenWidth = 12f;
        const float ScreenHeight = 8f;
        const float DefaultScreenZ = 12.5f;
        const float ScreenY = 2.5f;
        const float EdgeFadeWidth = 0.15f;

        // --- 弯曲参数 ---
        const float CurveRadius = 50f;
        const int CurveSegments = 32;

        // --- 底层: 静态背景图 ---
        GameObject screenObject;
        MeshRenderer screenRenderer;
        MeshFilter screenFilter;
        Material screenMaterial;
        Texture2D edgeFadeTexture;
        Texture2D backgroundTexture;

        // --- 顶层: SB/视频 Overlay ---
        GameObject overlayObject;
        MeshRenderer overlayRenderer;
        Material overlayMaterial;

        bool _hasContent;

        public bool IsActive => screenObject != null && screenObject.activeSelf;

        // =========================================================
        //  Lifecycle
        // =========================================================

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void AutoInitialize()
        {
            if (Instance == null)
            {
                var go = new GameObject("[HolographicScreenManager]");
                go.AddComponent<HolographicScreenManager>();
            }
        }

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // =========================================================
        //  公开 API
        // =========================================================

        /// <summary>
        /// 搭建幕布: 加载静态背景图到底层
        /// </summary>
        public void Setup(MediaAssetScanner.ScanResult scan, string beatmapFolder)
        {
            if (!IsStoryboardEnabled())
            {
                Hide();
                return;
            }

            EnsureScreenCreated();

            // 加载静态背景图到底层
            if (!string.IsNullOrEmpty(scan.BackgroundPath))
            {
                LoadBackgroundTexture(scan.BackgroundPath);
            }

            _hasContent = true;
            ApplySettings();
            ApplyVisibility();

            Debug.Log($"[HolographicScreen] 已搭建 (Video={scan.HasVideo}, SB={scan.HasStoryboard}, BG={!string.IsNullOrEmpty(scan.BackgroundPath)})");
        }

        public void Hide()
        {
            _hasContent = false;
            if (screenObject != null) screenObject.SetActive(false);
            if (overlayObject != null) overlayObject.SetActive(false);
        }

        /// <summary>
        /// 将 SB 的 RenderTexture 叠加到背景图之上 (不覆盖!)
        /// 背景图始终在底层作为兜底
        /// </summary>
        public void SetRenderTexture(RenderTexture rt)
        {
            EnsureScreenCreated();
            if (rt == null) return;

            // 确保 Overlay 层存在
            EnsureOverlayCreated();

            if (overlayMaterial != null)
            {
                overlayMaterial.mainTexture = rt;
                overlayObject.SetActive(true);

                // 同步 Overlay 的距离/透明度
                float z = GetScreenDistance();
                var pos = overlayObject.transform.localPosition;
                pos.z = z;
                overlayObject.transform.localPosition = pos;

                Color c = overlayMaterial.color;
                c.a = GetScreenAlpha();
                overlayMaterial.color = c;
            }

            _hasContent = true;
            ApplySettings();
            ApplyVisibility();
        }

        /// <summary>
        /// 隐藏 Overlay, 恢复只显示静态背景图
        /// </summary>
        public void RestoreBackgroundTexture()
        {
            if (overlayObject != null)
                overlayObject.SetActive(false);
        }

        public void SetVideoTexture(Texture videoTexture)
        {
            if (videoTexture != null)
            {
                EnsureOverlayCreated();
                if (overlayMaterial != null)
                {
                    overlayMaterial.mainTexture = videoTexture;
                    overlayObject.SetActive(true);
                }
            }
        }

        public void OnSettingsChanged()
        {
            if (!IsStoryboardEnabled())
            {
                Hide();
                return;
            }

            if (_hasContent)
            {
                ApplySettings();
                ApplyVisibility();
            }
        }

        // =========================================================
        //  设置读取
        // =========================================================

        bool IsStoryboardEnabled()
        {
            if (SettingsManager.Instance != null && SettingsManager.Instance.Settings != null)
                return SettingsManager.Instance.Settings.enableStoryboard;
            return true;
        }

        float GetScreenDistance()
        {
            if (SettingsManager.Instance != null && SettingsManager.Instance.Settings != null)
                return SettingsManager.Instance.Settings.storyboardScreenDistance;
            return DefaultScreenZ;
        }

        float GetScreenAlpha()
        {
            if (SettingsManager.Instance != null && SettingsManager.Instance.Settings != null)
                return SettingsManager.Instance.Settings.storyboardScreenAlpha;
            return 0.5f;
        }

        // =========================================================
        //  内部实现
        // =========================================================

        void ApplyVisibility()
        {
            if (screenObject == null) return;
            screenObject.SetActive(_hasContent);
        }

        void ApplySettings()
        {
            if (screenObject == null) return;

            float z = GetScreenDistance();
            var pos = screenObject.transform.localPosition;
            pos.z = z;
            screenObject.transform.localPosition = pos;

            float alpha = GetScreenAlpha();
            if (screenMaterial != null)
            {
                Color c = screenMaterial.color;
                c.a = alpha;
                screenMaterial.color = c;
            }

            // 同步 Overlay 设置
            if (overlayObject != null && overlayObject.activeSelf)
            {
                var opos = overlayObject.transform.localPosition;
                opos.z = z;
                overlayObject.transform.localPosition = opos;

                if (overlayMaterial != null)
                {
                    Color oc = overlayMaterial.color;
                    oc.a = alpha;
                    overlayMaterial.color = oc;
                }
            }
        }

        void EnsureScreenCreated()
        {
            if (screenObject != null) return;

            // 1. 创建底层 GameObject (静态背景图)
            screenObject = new GameObject("[HolographicScreen]");
            screenObject.transform.SetParent(transform);
            screenObject.transform.localPosition = new Vector3(0, ScreenY, GetScreenDistance());
            screenObject.transform.localRotation = Quaternion.identity;

            // 2. 程序化生成弯曲 Mesh
            screenFilter = screenObject.AddComponent<MeshFilter>();
            screenFilter.mesh = CreateCurvedMesh(ScreenWidth, ScreenHeight, CurveRadius, CurveSegments);

            // 3. 创建背景材质 (支持透明, 跟随不透明度设置)
            Shader shader = Shader.Find("OsuVR/HolographicScreen");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) { Debug.LogError("[HolographicScreen] 所有 Shader 均不可用!"); return; }
            screenMaterial = new Material(shader);

            // 确保材质支持透明 (自定义 shader 可能不需要, 但 fallback 必须设置)
            if (screenMaterial.HasProperty("_Surface")) screenMaterial.SetFloat("_Surface", 1); // Transparent
            if (screenMaterial.HasProperty("_Blend")) screenMaterial.SetFloat("_Blend", 0);     // Alpha
            if (screenMaterial.HasProperty("_SrcBlend")) screenMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            if (screenMaterial.HasProperty("_DstBlend")) screenMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            if (screenMaterial.HasProperty("_ZWrite")) screenMaterial.SetInt("_ZWrite", 0);

            float alpha = GetScreenAlpha();
            // 背景层固定亮度 0.5
            screenMaterial.color = new Color(0.5f, 0.5f, 0.5f, alpha);

            edgeFadeTexture = CreateEdgeFadeTexture(512, 512, EdgeFadeWidth);
            screenMaterial.mainTexture = edgeFadeTexture;

            // 4. 设置渲染器
            screenRenderer = screenObject.AddComponent<MeshRenderer>();
            screenRenderer.sharedMaterial = screenMaterial;
            screenRenderer.shadowCastingMode = ShadowCastingMode.Off;
            screenRenderer.receiveShadows = false;
        }

        /// <summary>
        /// 创建 Overlay 层 (SB/视频叠加层)
        /// 与底层使用相同 Mesh, 稍微前移避免 Z-Fighting
        /// </summary>
        void EnsureOverlayCreated()
        {
            if (overlayObject != null) return;

            EnsureScreenCreated(); // 确保底层已创建

            // 1. 创建 Overlay GameObject
            overlayObject = new GameObject("[HolographicScreen_Overlay]");
            overlayObject.transform.SetParent(transform);
            overlayObject.transform.localPosition = new Vector3(0, ScreenY, GetScreenDistance() - 0.01f); // 稍微前移
            overlayObject.transform.localRotation = Quaternion.identity;

            // 2. 共享底层 Mesh
            var overlayFilter = overlayObject.AddComponent<MeshFilter>();
            overlayFilter.mesh = screenFilter.mesh;

            // 3. Overlay 材质: SB RenderTexture × 边缘羽化纹理
            Shader overlayShader = Shader.Find("OsuVR/SBOverlay");
            if (overlayShader == null) overlayShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (overlayShader == null) overlayShader = Shader.Find("Standard");
            if (overlayShader == null) { Debug.LogError("[HolographicScreen] SBOverlay Shader 不可用!"); return; }
            overlayMaterial = new Material(overlayShader);
            overlayMaterial.renderQueue = (int)RenderQueue.Transparent + 1;

            // 传入边缘羽化纹理
            if (edgeFadeTexture != null)
                overlayMaterial.SetTexture("_EdgeFadeTex", edgeFadeTexture);

            Color c = Color.white;
            c.a = GetScreenAlpha();
            overlayMaterial.color = c;

            // 4. 设置渲染器
            overlayRenderer = overlayObject.AddComponent<MeshRenderer>();
            overlayRenderer.sharedMaterial = overlayMaterial;
            overlayRenderer.shadowCastingMode = ShadowCastingMode.Off;
            overlayRenderer.receiveShadows = false;

            // 默认隐藏 (等 SetRenderTexture 调用时才激活)
            overlayObject.SetActive(false);

            Debug.Log("[HolographicScreen] Overlay 层已创建");
        }

        // =========================================================
        //  Mesh / Texture 生成
        // =========================================================

        static Mesh CreateCurvedMesh(float width, float height, float radius, int segments)
        {
            var mesh = new Mesh();
            mesh.name = "HolographicScreen_Curved";

            float hw = width * 0.5f;
            float hh = height * 0.5f;

            int vertCountX = segments + 1;
            int vertCountY = 2;
            int vertCount = vertCountX * vertCountY;

            var vertices = new Vector3[vertCount];
            var uvs = new Vector2[vertCount];

            for (int yi = 0; yi < vertCountY; yi++)
            {
                float v = yi;
                float y = Mathf.Lerp(-hh, hh, v);

                for (int xi = 0; xi < vertCountX; xi++)
                {
                    float u = (float)xi / segments;
                    float x = Mathf.Lerp(-hw, hw, u);
                    float z = -(x * x) / (2f * radius);

                    int idx = yi * vertCountX + xi;
                    vertices[idx] = new Vector3(x, y, z);
                    uvs[idx] = new Vector2(u, v);
                }
            }

            int triCount = segments * 2 * 3;
            var triangles = new int[triCount];
            int ti = 0;

            for (int xi = 0; xi < segments; xi++)
            {
                int bl = 0 * vertCountX + xi;
                int tl = 1 * vertCountX + xi;
                int br = bl + 1;
                int tr = tl + 1;

                triangles[ti++] = bl;
                triangles[ti++] = tl;
                triangles[ti++] = tr;

                triangles[ti++] = bl;
                triangles[ti++] = tr;
                triangles[ti++] = br;
            }

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static Texture2D CreateEdgeFadeTexture(int w, int h, float fadeWidth)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var colors = new Color[w * h];

            for (int y = 0; y < h; y++)
            {
                float ny = (float)y / (h - 1);
                float dy = Mathf.Abs(ny - 0.5f) * 2f;
                float fy = Smoothstep01(1f, 1f - fadeWidth, dy);

                for (int x = 0; x < w; x++)
                {
                    float nx = (float)x / (w - 1);
                    float dx = Mathf.Abs(nx - 0.5f) * 2f;
                    float fx = Smoothstep01(1f, 1f - fadeWidth, dx);

                    colors[y * w + x] = new Color(1f, 1f, 1f, fx * fy);
                }
            }

            tex.SetPixels(colors);
            tex.Apply();
            return tex;
        }

        static float Smoothstep01(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        void LoadBackgroundTexture(string path)
        {
            try
            {
                byte[] data = File.ReadAllBytes(path);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (tex.LoadImage(data))
                {
                    if (backgroundTexture != null) Destroy(backgroundTexture);
                    backgroundTexture = tex;
                    screenMaterial.mainTexture = tex;
                }
                else
                {
                    Debug.LogWarning($"[HolographicScreen] 背景图解码失败: {path}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[HolographicScreen] 背景图加载异常: {e.Message}");
            }
        }

        void OnDestroy()
        {
            if (screenMaterial != null) Destroy(screenMaterial);
            if (overlayMaterial != null) Destroy(overlayMaterial);
            if (edgeFadeTexture != null) Destroy(edgeFadeTexture);
            if (backgroundTexture != null) Destroy(backgroundTexture);
            if (Instance == this) Instance = null;
        }
    }
}
