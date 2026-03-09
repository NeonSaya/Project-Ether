using UnityEngine;
using UnityEngine.SceneManagement;

namespace OsuVR
{
    /// <summary>
    /// 以太虚空环境生成器 (MMD 唯美舞台版)
    /// 100% 纯代码生成，零资产依赖
    /// 包含：呼吸星尘层、唯美光斑层、缓动晶体层
    /// </summary>
    public class EtherealEnvironment : MonoBehaviour
    {
        public static EtherealEnvironment Instance { get; private set; }

        public enum EnvironmentState
        {
            Idle,       // 梦幻紫粉 (前奏)
            Combo,      // 深空青蓝 (连击)
            Kiai        // 璀璨晨金 (高潮)
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void AutoInitialize()
        {
            if (Instance == null)
            {
                GameObject go = new GameObject("[EtherealEnvironment]");
                go.AddComponent<EtherealEnvironment>();
            }
        }

        [Header("🌌 舞台背景设置")]
        [Tooltip("极深紫罗兰，比纯黑更有氛围")]
        public Color voidBackgroundColor = new Color(0.04f, 0.02f, 0.06f);

        [Header("✨ 星尘层 (背景氛围)")]
        public float stardustRadius = 40f;
        public int stardustMaxParticles = 30000;
        public int stardustRate = 3000;
        public float stardustMinLifetime = 15f;
        public float stardustMaxLifetime = 25f;
        public float stardustSize = 0.05f;
        [Range(0.1f, 0.4f)] public float stardustAlpha = 0.25f;

        [Header("🫧 光斑层 (替代瞎眼流线)")]
        public float bokehRadius = 25f;
        public int bokehMaxParticles = 500;
        public int bokehRate = 20;
        public float bokehMinSize = 0.03f;
        public float bokehMaxSize = 0.08f;
        public float bokehFloatSpeed = 0.3f;

        [Header("💠 幻象晶体 (舞台点缀)")]
        public int crystalsMaxParticles = 60;
        public float crystalsRadius = 18f;
        public float crystalsSize = 0.2f;

        [Header("🎨 色彩映射 (Dreamy Gradients)")]
        private Gradient gradientIdle;
        private Gradient gradientCombo;
        private Gradient gradientKiai;

        private Texture2D glowTexture;
        private Material particleMaterial;
        private Material crystalMaterial;

        private ParticleSystem stardustPS;
        private ParticleSystem bokehPS;
        private ParticleSystem crystalsPS;

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

            InitializeGradients();
            SetupCamera();
            GenerateResources();

            CreateStardustLayer();
            CreateBokehLayer(); // 唯美光斑
            CreateCrystalsLayer();

            // 默认进入 Idle 状态
            SetEnvironmentState(EnvironmentState.Idle);
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode) => SetupCamera();

        void InitializeGradients()
        {
            // MMD 常用梦幻色调
            // Idle: 深蓝紫 -> 樱花粉
            gradientIdle = CreateGradient(new Color(0.3f, 0.1f, 0.8f), new Color(1.0f, 0.4f, 0.7f), stardustAlpha);
            // Combo: 青蓝 -> 薄荷绿
            gradientCombo = CreateGradient(new Color(0.1f, 0.6f, 1.0f), new Color(0.2f, 1.0f, 0.8f), stardustAlpha);
            // Kiai: 晨曦金 -> 纯白 (带有过曝倾向)
            gradientKiai = CreateGradient(new Color(1.0f, 0.8f, 0.2f), new Color(1.0f, 0.95f, 0.8f), stardustAlpha * 1.5f);
        }

        Gradient CreateGradient(Color start, Color end, float alpha)
        {
            Gradient g = new Gradient();
            g.SetKeys(
                new GradientColorKey[] { new GradientColorKey(start, 0f), new GradientColorKey(end, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(alpha, 0f), new GradientAlphaKey(alpha, 1f) }
            );
            return g;
        }

        void SetupCamera()
        {
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                mainCam.clearFlags = CameraClearFlags.SolidColor;
                mainCam.backgroundColor = voidBackgroundColor;
            }
        }

        void GenerateResources()
        {
            // 生成更加柔和、带有高亮核心的辉光贴图 (Bloom 绝佳伴侣)
            int size = 128;
            glowTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float center = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    float normalizedDist = Mathf.Clamp01(dist / center);
                    // 使用平滑的指数衰减，让中心锐利，边缘极其平滑
                    float alpha = Mathf.Pow(1f - normalizedDist, 2.5f);
                    glowTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            glowTexture.Apply();

            Shader additiveShader = Shader.Find("Mobile/Particles/Additive") ?? Shader.Find("Particles/Standard Unlit");

            particleMaterial = new Material(additiveShader) { enableInstancing = true, renderQueue = 2000 };
            particleMaterial.SetTexture("_MainTex", glowTexture);

            crystalMaterial = new Material(additiveShader) { enableInstancing = true, renderQueue = 2001 };
        }

        void CreateStardustLayer()
        {
            GameObject go = new GameObject("LayerA_DreamyStardust");
            go.transform.SetParent(transform);

            stardustPS = go.AddComponent<ParticleSystem>();
            var main = stardustPS.main;
            main.maxParticles = stardustMaxParticles;
            main.startLifetime = new ParticleSystem.MinMaxCurve(stardustMinLifetime, stardustMaxLifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.08f); // 极慢的初速度
            main.startSize = stardustSize;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var shape = stardustPS.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = stardustRadius;

            var emission = stardustPS.emission;
            emission.rateOverTime = stardustRate;

            // 唯美缓动核心：微弱的力场噪点，像在水中漂浮
            var noise = stardustPS.noise;
            noise.enabled = true;
            noise.strength = 0.05f;
            noise.frequency = 0.2f;
            noise.scrollSpeed = 0.1f;

            var renderer = stardustPS.GetComponent<ParticleSystemRenderer>();
            renderer.material = particleMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.enableGPUInstancing = true;
        }

        void CreateBokehLayer()
        {
            // 这是替换流线层的新层：大体积的柔和光斑，缓慢向上浮动
            GameObject go = new GameObject("LayerB_FloatingBokeh");
            go.transform.SetParent(transform);

            bokehPS = go.AddComponent<ParticleSystem>();
            var main = bokehPS.main;
            main.maxParticles = bokehMaxParticles;
            main.startLifetime = new ParticleSystem.MinMaxCurve(8f, 15f);
            main.startSpeed = 0f; // 初始静止，靠 VelocityOverLifetime 驱动
            main.startSize = new ParticleSystem.MinMaxCurve(bokehMinSize, bokehMaxSize);
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var shape = bokehPS.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box; // 使用Box模拟圆柱体分布效果
            shape.scale = new Vector3(bokehRadius * 2f, 30f, bokehRadius * 2f); // XZ为半径，Y为高度

            var emission = bokehPS.emission;
            emission.rateOverTime = bokehRate;

            // 缓动核心：缓慢向上升起，伴随轻微的左右飘动 (类似落叶/飞花的反向)
            var velocity = bokehPS.velocityOverLifetime;
            velocity.enabled = true;
            velocity.y = new ParticleSystem.MinMaxCurve(bokehFloatSpeed * 0.5f, bokehFloatSpeed);
            velocity.x = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f); // 显式设置Z分量为0，确保所有分量使用相同模式
            velocity.space = ParticleSystemSimulationSpace.World;

            // 光斑的淡入淡出，极其重要，避免突然出现/消失
            var colorOL = bokehPS.colorOverLifetime;
            colorOL.enabled = true;
            Gradient alphaGrad = new Gradient();
            alphaGrad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.8f, 0.3f),
                    new GradientAlphaKey(0.8f, 0.7f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOL.color = alphaGrad;

            var renderer = bokehPS.GetComponent<ParticleSystemRenderer>();
            renderer.material = particleMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.enableGPUInstancing = true;
        }

        void CreateCrystalsLayer()
        {
            GameObject go = new GameObject("LayerC_SlowCrystals");
            go.transform.SetParent(transform);

            crystalsPS = go.AddComponent<ParticleSystem>();
            var main = crystalsPS.main;
            main.maxParticles = crystalsMaxParticles;
            main.startLifetime = 20f;
            main.startSpeed = 0.05f; // 几乎悬停
            main.startSize = crystalsSize;
            main.startRotation3D = true; // 允许三轴随机初始旋转

            var shape = crystalsPS.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = crystalsRadius;

            var emission = crystalsPS.emission;
            emission.rateOverTime = 5;

            // 极慢且优雅的自旋
            var rotOverLifetime = crystalsPS.rotationOverLifetime;
            rotOverLifetime.enabled = true;
            rotOverLifetime.x = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);
            rotOverLifetime.y = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);
            rotOverLifetime.z = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);

            var renderer = crystalsPS.GetComponent<ParticleSystemRenderer>();
            renderer.material = crystalMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Mesh;

            GameObject tempCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            renderer.mesh = tempCube.GetComponent<MeshFilter>().sharedMesh;
            Destroy(tempCube);

            renderer.enableGPUInstancing = true;
        }

        public void SetEnvironmentState(EnvironmentState state)
        {
            Gradient targetGradient = gradientIdle;
            float targetSpeedMultiplier = 1f;

            switch (state)
            {
                case EnvironmentState.Idle:
                    targetGradient = gradientIdle;
                    targetSpeedMultiplier = 1f;
                    break;
                case EnvironmentState.Combo:
                    targetGradient = gradientCombo;
                    targetSpeedMultiplier = 1.2f;
                    break;
                case EnvironmentState.Kiai:
                    targetGradient = gradientKiai;
                    targetSpeedMultiplier = 2.0f; // 高潮时粒子略微加速，但依然保持缓动优雅
                    break;
            }

            // 星尘变色
            var mainStardust = stardustPS.main;
            mainStardust.startColor = new ParticleSystem.MinMaxGradient(targetGradient);

            // 光斑变色与加速
            var mainBokeh = bokehPS.main;
            mainBokeh.startColor = new ParticleSystem.MinMaxGradient(targetGradient);
            var velocityBokeh = bokehPS.velocityOverLifetime;
            velocityBokeh.speedModifier = targetSpeedMultiplier;

            // 晶体变色
            var mainCrystals = crystalsPS.main;
            mainCrystals.startColor = new ParticleSystem.MinMaxGradient(targetGradient);
        }

        public void TriggerKickEvent()
        {
            if (crystalsPS == null) return;

            ParticleSystem.Particle[] particles = new ParticleSystem.Particle[crystalsPS.particleCount];
            int count = crystalsPS.GetParticles(particles);

            for (int i = 0; i < count; i++)
            {
                // 音频重音触发：晶体瞬间放大并缓慢缩回 (取代原本的狂暴自旋)
                // 这是一种更符合“唯美”调性的视觉呼吸感
                particles[i].startSize = crystalsSize * 2.5f;
            }
            crystalsPS.SetParticles(particles, count);
        }

        void Update()
        {
            // 唯美晶体的呼吸平滑恢复逻辑 (配合 TriggerKickEvent)
            if (crystalsPS == null) return;
            ParticleSystem.Particle[] particles = new ParticleSystem.Particle[crystalsPS.particleCount];
            int count = crystalsPS.GetParticles(particles);
            bool modified = false;

            for (int i = 0; i < count; i++)
            {
                if (particles[i].startSize > crystalsSize)
                {
                    // 平滑回弹 (Lerp 模拟)
                    particles[i].startSize = Mathf.Max(crystalsSize, particles[i].startSize - Time.deltaTime * 0.5f);
                    modified = true;
                }
            }
            if (modified) crystalsPS.SetParticles(particles, count);
        }
    }
}