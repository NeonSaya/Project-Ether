using UnityEngine;
using UnityEngine.SceneManagement;

namespace OsuVR
{
    /// <summary>
    /// 以太虚空环境生成器
    /// 100% 纯代码生成，零资产依赖
    /// 包含：相机背景设置、星尘层、穿梭流线层
    /// 自动应用于所有场景，无需手动添加
    /// </summary>
    public class EtherealEnvironment : MonoBehaviour
    {
        public static EtherealEnvironment Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void AutoInitialize()
        {
            if (Instance == null)
            {
                GameObject go = new GameObject("[EtherealEnvironment]");
                go.AddComponent<EtherealEnvironment>();
            }
        }

        [Header("🌌 相机背景设置")]
        [Tooltip("极深宇宙蓝背景色")]
        public Color voidBackgroundColor = new Color(0.02f, 0.02f, 0.04f);

        [Header("✨ 星尘层配置")]
        public float stardustRadius = 40f;
        public int stardustRate = 200;
        public float stardustMinLifetime = 10f;
        public float stardustMaxLifetime = 20f;
        public float stardustMinSpeed = 0.1f;
        public float stardustMaxSpeed = 0.5f;
        public float stardustMinSize = 0.05f;
        public float stardustMaxSize = 0.2f;
        [Range(0.2f, 0.5f)] public float stardustAlphaMin = 0.2f;
        [Range(0.3f, 0.7f)] public float stardustAlphaMax = 0.5f;

        [Header("🚀 流线层配置")]
        public float streamlineDistance = 30f;
        public float streamlineRadius = 20f;
        public int streamlineRate = 50;
        public float streamlineMinLifetime = 2f;
        public float streamlineMaxLifetime = 4f;
        public float streamlineMinSpeed = 10f;
        public float streamlineMaxSpeed = 20f;
        public float streamlineSize = 0.05f;
        public float streamlineLengthScale = 10f;
        public float streamlineSpeedScale = 0.2f;

        [Header("🎨 材质设置")]
        [Tooltip("RenderQueue: 2000 = 天空盒层级，确保不遮挡音符 (3900)")]
        public int renderQueue = 2000;
        public int textureSize = 64;

        private Texture2D softDotTexture;
        private Material particleMaterial;
        private GameObject stardustObject;
        private GameObject streamlinesObject;
        private ParticleSystem stardustPS;
        private ParticleSystem streamlinesPS;

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            SceneManager.sceneLoaded += OnSceneLoaded;

            SetupCamera();
            GenerateResources();
            CreateStardustLayer();
            CreateStreamlinesLayer();
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            SetupCamera();
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;

            if (softDotTexture != null) Destroy(softDotTexture);
            if (particleMaterial != null) Destroy(particleMaterial);
            if (stardustObject != null) Destroy(stardustObject);
            if (streamlinesObject != null) Destroy(streamlinesObject);
        }

        void SetupCamera()
        {
            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                Debug.LogWarning("[EtherealEnvironment] 未找到主相机");
                return;
            }

            mainCam.clearFlags = CameraClearFlags.SolidColor;
            mainCam.backgroundColor = voidBackgroundColor;
        }

        Texture2D GenerateSoftDotTexture(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            float center = size * 0.5f;
            float maxDist = center;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float normalizedDist = dist / maxDist;

                    float alpha = 1f - (normalizedDist * normalizedDist);
                    alpha = Mathf.Clamp01(alpha);

                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply();
            return tex;
        }

        void GenerateResources()
        {
            softDotTexture = GenerateSoftDotTexture(textureSize);

            Shader shader = Shader.Find("Mobile/Particles/Additive");
            if (shader == null) shader = Shader.Find("Legacy Shaders/Particles/Additive");
            if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");

            if (shader == null)
            {
                Debug.LogError("[EtherealEnvironment] 无法找到合适的粒子 Shader");
                return;
            }

            particleMaterial = new Material(shader);
            particleMaterial.enableInstancing = true;
            particleMaterial.renderQueue = renderQueue;

            if (particleMaterial.HasProperty("_MainTex"))
                particleMaterial.SetTexture("_MainTex", softDotTexture);
            if (particleMaterial.HasProperty("_BaseMap"))
                particleMaterial.SetTexture("_BaseMap", softDotTexture);

            if (particleMaterial.HasProperty("_Color"))
                particleMaterial.SetColor("_Color", Color.white);
            if (particleMaterial.HasProperty("_BaseColor"))
                particleMaterial.SetColor("_BaseColor", Color.white);

            particleMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            particleMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            particleMaterial.SetInt("_ZWrite", 0);
            particleMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        }

        void CreateStardustLayer()
        {
            stardustObject = new GameObject("VFX_Stardust");
            stardustObject.transform.SetParent(transform);
            stardustObject.transform.localPosition = Vector3.zero;

            stardustPS = stardustObject.AddComponent<ParticleSystem>();
            var main = stardustPS.main;

            main.duration = 10f;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(stardustMinLifetime, stardustMaxLifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(stardustMinSpeed, stardustMaxSpeed);
            main.startSize = new ParticleSystem.MinMaxCurve(stardustMinSize, stardustMaxSize);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0f, 1f, 1f, stardustAlphaMin),
                new Color(1f, 0f, 1f, stardustAlphaMax)
            );
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.playOnAwake = true;

            var shape = stardustPS.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = stardustRadius;
            shape.radiusThickness = 1f;

            var emission = stardustPS.emission;
            emission.enabled = true;
            emission.rateOverTime = stardustRate;

            var noise = stardustPS.noise;
            noise.enabled = true;
            noise.strength = 0.1f;
            noise.frequency = 0.5f;
            noise.scrollSpeed = 0.2f;
            noise.quality = ParticleSystemNoiseQuality.Medium;

            var renderer = stardustPS.GetComponent<ParticleSystemRenderer>();
            renderer.enabled = true;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.material = particleMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enableGPUInstancing = true;
        }

        void CreateStreamlinesLayer()
        {
            streamlinesObject = new GameObject("VFX_Streamlines");
            streamlinesObject.transform.SetParent(transform);
            streamlinesObject.transform.localPosition = Vector3.forward * streamlineDistance;
            streamlinesObject.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            streamlinesPS = streamlinesObject.AddComponent<ParticleSystem>();
            var main = streamlinesPS.main;

            main.duration = 10f;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(streamlineMinLifetime, streamlineMaxLifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(streamlineMinSpeed, streamlineMaxSpeed);
            main.startSize = streamlineSize;
            main.startColor = new Color(0.5f, 0.8f, 1f, 0.8f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.playOnAwake = true;

            var shape = streamlinesPS.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = streamlineRadius;
            shape.radiusThickness = 1f;
            shape.arc = 360f;

            var emission = streamlinesPS.emission;
            emission.enabled = true;
            emission.rateOverTime = streamlineRate;

            var colorOverLifetime = streamlinesPS.colorOverLifetime;
            colorOverLifetime.enabled = true;

            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(new Color(0.5f, 0.8f, 1f), 0.0f),
                    new GradientColorKey(new Color(0.8f, 0.5f, 1f), 0.5f),
                    new GradientColorKey(new Color(0.5f, 0.8f, 1f), 1.0f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0.0f, 0.0f),
                    new GradientAlphaKey(0.9f, 0.2f),
                    new GradientAlphaKey(0.9f, 0.8f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            colorOverLifetime.color = gradient;

            var renderer = streamlinesPS.GetComponent<ParticleSystemRenderer>();
            renderer.enabled = true;
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = streamlineLengthScale;
            renderer.velocityScale = streamlineSpeedScale;
            renderer.material = particleMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enableGPUInstancing = true;
        }

        public void SetBackgroundColor(Color color)
        {
            voidBackgroundColor = color;
            if (Camera.main != null)
            {
                Camera.main.backgroundColor = color;
            }
        }

        public void SetStardustIntensity(float intensity)
        {
            if (stardustPS != null)
            {
                var emission = stardustPS.emission;
                emission.rateOverTime = Mathf.RoundToInt(stardustRate * intensity);
            }
        }

        public void SetStreamlineSpeed(float speedMultiplier)
        {
            if (streamlinesPS != null)
            {
                var main = streamlinesPS.main;
                main.startSpeed = new ParticleSystem.MinMaxCurve(
                    streamlineMinSpeed * speedMultiplier,
                    streamlineMaxSpeed * speedMultiplier
                );
            }
        }
    }
}
