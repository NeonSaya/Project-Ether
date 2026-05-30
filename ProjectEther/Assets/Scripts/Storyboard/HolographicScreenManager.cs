using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace OsuVR.Storyboard
{
    /// <summary>
    /// 全息幕布管理器：在玩家正前方动态生成带有边缘羽化的半透明屏幕。
    /// 纯代码驱动，不依赖 Prefab。遵循 EtherealEnvironment 的 Singleton 模式。
    /// 支持通过设置面板调整距离、透明度和开关。
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
        const float CurveRadius = 50f;   // 曲率半径 (越大越平, 边缘凹进约 0.36m)
        const int CurveSegments = 32;    // 水平细分数 (越高越平滑)

        // --- 运行时对象 ---
        GameObject screenObject;
        MeshRenderer screenRenderer;
        MeshFilter screenFilter;
        Material screenMaterial;
        Texture2D edgeFadeTexture;
        Texture2D backgroundTexture;

        /// <summary>
        /// 幕布是否已完成 Setup 且有可展示的内容
        /// </summary>
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
        /// 根据嗅探结果搭建幕布。有背景图则加载，否则显示半透明占位。
        /// 自动读取设置中的开关、距离、透明度。
        /// </summary>
        public void Setup(MediaAssetScanner.ScanResult scan, string beatmapFolder)
        {
            // 检查全局开关
            if (!IsStoryboardEnabled())
            {
                Hide();
                return;
            }

            EnsureScreenCreated();

            // 加载静态背景图
            if (!string.IsNullOrEmpty(scan.BackgroundPath))
            {
                LoadBackgroundTexture(scan.BackgroundPath);
            }

            _hasContent = true;
            ApplySettings();
            ApplyVisibility();

            Debug.Log($"[HolographicScreen] 已搭建 (Video={scan.HasVideo}, SB={scan.HasStoryboard}, BG={!string.IsNullOrEmpty(scan.BackgroundPath)})");
        }

        /// <summary>
        /// 隐藏幕布（游戏结束、返回菜单时调用）
        /// </summary>
        public void Hide()
        {
            _hasContent = false;
            if (screenObject != null) screenObject.SetActive(false);
        }

        /// <summary>
        /// 将 StoryboardRenderer 的 RenderTexture 注入到幕布材质 (光纤对接)
        /// </summary>
        public void SetRenderTexture(RenderTexture rt)
        {
            EnsureScreenCreated();
            if (screenMaterial != null && rt != null)
            {
                screenMaterial.mainTexture = rt;
                _hasContent = true;
                ApplySettings();
                ApplyVisibility();
            }
        }

        /// <summary>
        /// 获取已加载的背景图纹理 (供 StoryboardRenderer 注册为 sprite)
        /// </summary>
        public Texture2D GetBackgroundTexture() => backgroundTexture;

        /// <summary>
        /// 恢复为静态背景图纹理
        /// </summary>
        public void RestoreBackgroundTexture()
        {
            if (screenMaterial != null)
            {
                screenMaterial.mainTexture = backgroundTexture != null
                    ? (Texture)backgroundTexture
                    : (Texture)edgeFadeTexture;
            }
        }

        /// <summary>
        /// 未来接入 VideoPlayer 时调用，将视频纹理设置到幕布
        /// </summary>
        public void SetVideoTexture(Texture videoTexture)
        {
            if (screenMaterial != null && videoTexture != null)
            {
                screenMaterial.mainTexture = videoTexture;
            }
        }

        /// <summary>
        /// 设置变更时调用（由 GameplaySettingsPage 触发）
        /// 实时应用距离、透明度、开关等变更
        /// </summary>
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

        /// <summary>
        /// 将设置面板的参数实时应用到幕布
        /// </summary>
        void ApplySettings()
        {
            if (screenObject == null) return;

            // 更新 Z 距离
            float z = GetScreenDistance();
            var pos = screenObject.transform.localPosition;
            pos.z = z;
            screenObject.transform.localPosition = pos;

            // 更新透明度
            float alpha = GetScreenAlpha();
            if (screenMaterial != null)
            {
                Color c = screenMaterial.color;
                c.a = alpha;
                screenMaterial.color = c;
            }
        }

        void EnsureScreenCreated()
        {
            if (screenObject != null) return;

            // 1. 创建 GameObject
            screenObject = new GameObject("[HolographicScreen]");
            screenObject.transform.SetParent(transform);
            screenObject.transform.localPosition = new Vector3(0, ScreenY, GetScreenDistance());
            screenObject.transform.localRotation = Quaternion.identity;

            // 2. 程序化生成弯曲 Mesh
            screenFilter = screenObject.AddComponent<MeshFilter>();
            screenFilter.mesh = CreateCurvedMesh(ScreenWidth, ScreenHeight, CurveRadius, CurveSegments);

            // 3. 创建材质 (优先自定义 shader，fallback 到 URP Unlit)
            Shader shader = Shader.Find("OsuVR/HolographicScreen");
            if (shader == null)
            {
                Debug.LogWarning("[HolographicScreen] 自定义 Shader 未找到，fallback 到 URP Unlit (无边缘羽化)");
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }
            screenMaterial = new Material(shader);

            // 4. 应用初始透明度
            float alpha = GetScreenAlpha();
            screenMaterial.color = new Color(1f, 1f, 1f, alpha);

            // 5. 生成边缘羽化纹理 (同时作为 fallback 和 shader 的主纹理)
            edgeFadeTexture = CreateEdgeFadeTexture(512, 512, EdgeFadeWidth);
            screenMaterial.mainTexture = edgeFadeTexture;

            // 6. 设置渲染器
            screenRenderer = screenObject.AddComponent<MeshRenderer>();
            screenRenderer.sharedMaterial = screenMaterial;
            screenRenderer.shadowCastingMode = ShadowCastingMode.Off;
            screenRenderer.receiveShadows = false;
        }

        /// <summary>
        /// 程序化生成圆柱弯曲 Mesh，面向玩家 (法线朝 -Z)
        /// 使用抛物线公式 z = -(x²) / (2R) 实现平滑弯曲
        /// </summary>
        static Mesh CreateCurvedMesh(float width, float height, float radius, int segments)
        {
            var mesh = new Mesh();
            mesh.name = "HolographicScreen_Curved";

            float hw = width * 0.5f;
            float hh = height * 0.5f;

            int vertCountX = segments + 1;
            int vertCountY = 2; // 上下两排即可
            int vertCount = vertCountX * vertCountY;

            var vertices = new Vector3[vertCount];
            var uvs = new Vector2[vertCount];

            for (int yi = 0; yi < vertCountY; yi++)
            {
                float v = yi; // 0 或 1
                float y = Mathf.Lerp(-hh, hh, v);

                for (int xi = 0; xi < vertCountX; xi++)
                {
                    float u = (float)xi / segments;
                    float x = Mathf.Lerp(-hw, hw, u);

                    // 抛物线弯曲：x 偏离中心越远，z 越往后凹
                    float z = -(x * x) / (2f * radius);

                    int idx = yi * vertCountX + xi;
                    vertices[idx] = new Vector3(x, y, z);
                    uvs[idx] = new Vector2(u, v);
                }
            }

            // 生成三角形索引
            int triCount = segments * 2 * 3;
            var triangles = new int[triCount];
            int ti = 0;

            for (int xi = 0; xi < segments; xi++)
            {
                int bl = 0 * vertCountX + xi;
                int tl = 1 * vertCountX + xi;
                int br = bl + 1;
                int tr = tl + 1;

                // 三角形 1
                triangles[ti++] = bl;
                triangles[ti++] = tl;
                triangles[ti++] = tr;

                // 三角形 2
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

        /// <summary>
        /// 生成边缘羽化纹理：中心完全不透明，四周 SmoothStep 渐变透明
        /// </summary>
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
                    // 释放旧的背景纹理
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
            if (edgeFadeTexture != null) Destroy(edgeFadeTexture);
            if (backgroundTexture != null) Destroy(backgroundTexture);
            if (Instance == this) Instance = null;
        }
    }
}
