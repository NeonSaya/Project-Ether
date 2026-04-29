using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace OsuVR
{
    /// <summary>
    /// 以太虚空环境生成器 (VR音游版)
    /// 100% 纯代码生成，零资产依赖
    ///
    /// 层级：
    /// - A. 星云 (Nebula)      — 远景大色块，打破死黑背景
    /// - B. 星尘 (Stardust)    — 背景微粒，营造深空感
    /// - C. 流星 (FallingStar) — 菜单/选歌界面的飘落粒子（Beat Saber 风格）
    /// - D. 光斑 (Bokeh)       — 柔和飘浮光点
    /// - E. 晶体 (Crystal)     — 舞台点缀
    ///
    /// 场景感知：
    /// - MainMenu / SongSelect：流星雨增多，光斑加大，空灵梦幻
    /// - GameScene：流星减少，粒子让位给打歌，音频律动主导
    /// - ResultScene：恢复宁静，缓慢庆祝感
    /// </summary>
    public class EtherealEnvironment : MonoBehaviour
    {
        public static EtherealEnvironment Instance { get; private set; }

        public enum EnvironmentState
        {
            Idle,       // 梦幻深蓝紫 → 粉紫 → 樱花粉
            Combo,      // 青蓝 → 翠 → 薄荷绿
            Kiai        // 晨曦金 → 玫瑰橙 → 白金
        }

        public enum GamePhase
        {
            Menu,       // 主菜单 / 选歌
            Playing,    // 打歌中
            Result      // 结算
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

        [Header("舞台背景设置")]
        public Color menuBackgroundColor = new Color(0.08f, 0.04f, 0.14f);
        public Color gameBackgroundColor = new Color(0.03f, 0.01f, 0.05f);
        public Color resultBackgroundColor = new Color(0.06f, 0.04f, 0.10f);

        [Header("背景星云层")]
        public int nebulaCount = 10;
        public float nebulaMaxRadius = 40f;
        public float nebulaMinSize = 12f;
        public float nebulaMaxSize = 35f;
        [Range(0.01f, 0.3f)] public float nebulaAlpha = 0.18f;

        [Header("星尘层")]
        public float stardustRadius = 60f;
        public int stardustMaxParticles = 25000;
        public int stardustRate = 2500;
        public float stardustMinLifetime = 18f;
        public float stardustMaxLifetime = 30f;
        public float stardustSize = 0.06f;
        [Range(0.05f, 0.4f)] public float stardustAlpha = 0.25f;

        [Header("光斑层")]
        public float bokehRadius = 35f;
        public int bokehMaxParticles = 400;
        public int bokehRateMenu = 25;
        public int bokehRateGame = 12;
        public float bokehMinSize = 0.1f;
        public float bokehMaxSizeMenu = 0.45f;
        public float bokehMaxSizeGame = 0.3f;
        public float bokehFloatSpeed = 0.25f;

        [Header("流星层 (菜单/选歌装饰)")]
        public int fallingStarMaxParticles = 500;
        public float fallingStarRadius = 35f;
        public float fallingStarHeight = 25f;
        public float fallingStarFallSpeed = 1.0f;
        public float fallingStarMinSize = 0.04f;
        public float fallingStarMaxSize = 0.15f;
        [Range(0.1f, 1f)] public float fallingStarAlpha = 0.7f;

        [Header("晶体层")]
        public int crystalsMaxParticles = 40;
        public float crystalsRadius = 25f;
        public float crystalsSize = 0.15f;

        [Header("环状光带 (菜单环绕装饰)")]
        public int ringParticleCount = 200;
        public float ringRadius = 8f;
        public float ringHeight = 2f;
        public float ringRotSpeed = 0.3f;
        public float ringParticleAlpha = 0.5f;

        [Header("极光丝带层 (菜单装饰)")]
        public int auroraMaxParticles = 8;
        public int auroraEmissionRate = 3;
        public float auroraMinSizeX = 8f;
        public float auroraMaxSizeX = 20f;
        public float auroraMinSizeY = 0.3f;
        public float auroraMaxSizeY = 0.8f;
        [Range(0.01f, 0.15f)] public float auroraAlpha = 0.07f;

        [Header("脉冲波纹层 (节拍驱动)")]
        public int pulseRingMaxParticles = 20;
        public int pulseBurstCountMenu = 8;
        public int pulseBurstCountGame = 3;
        public int pulseBurstCountResult = 5;
        public float pulseRingAlpha = 0.35f;

        [Header("上升气泡层 (菜单装饰)")]
        public int bubbleMaxParticles = 100;
        public int bubbleEmissionRate = 10;
        public float bubbleMinSize = 0.2f;
        public float bubbleMaxSize = 0.8f;
        [Range(0.05f, 0.3f)] public float bubbleAlpha = 0.15f;
        public float bubbleRiseSpeed = 0.4f;

        [Header("音频响应")]
        [SerializeField] private float audioResponseSmooth = 8f;
        [SerializeField] private float bassBrightnessGain = 1.5f;
        [SerializeField] private float bassSizeKick = 2.5f;
        [SerializeField] private float trebleHueShift = 0.3f;

        [Header("节拍驱动")]
        [Tooltip("节拍时晶体/星尘的亮度脉冲峰值")]
        [SerializeField] private float beatBrightnessPeak = 1.5f;
        [Tooltip("节拍亮度脉冲衰减速度")]
        [SerializeField] private float beatBrightnessDecay = 8f;
        [Tooltip("节拍提前量（秒）")]
        [SerializeField] private float beatAnticipation = 0.02f;
        [Tooltip("Kiai 时节拍脉冲倍率")]
        [SerializeField] private float kiaiBeatMultiplier = 1.6f;

        // 色彩映射
        private Gradient gradientIdle;
        private Gradient gradientCombo;
        private Gradient gradientKiai;

        // 贴图和材质
        private Texture2D glowTexture;
        private Texture2D bokehTexture;
        private Texture2D nebulaTexture;
        private Material particleMaterial;
        private Material bokehMaterial;
        private Material crystalMaterial;
        private Material nebulaMaterial;
        private Material fallingStarMaterial;
        private Material ringMaterial;
        private Material auroraMaterial;
        private Material pulseMaterial;
        private Material bubbleMaterial;

        // 粒子系统
        private ParticleSystem stardustPS;
        private ParticleSystem bokehPS;
        private ParticleSystem crystalsPS;
        private ParticleSystem nebulaPS;
        private ParticleSystem fallingStarPS;
        private ParticleSystem ringPS;
        private ParticleSystem auroraPS;
        private ParticleSystem pulseRingPS;
        private ParticleSystem bubblePS;

        // 流星层 speedModifier 缓存
        private float currentFallingStarSpeedMod = 1f;

        // 基准值
        private int baseStardustMaxParticles;
        private int baseStardustRate;
        private int baseBokehMaxParticles;
        private int baseBokehRate;
        private int baseCrystalsMaxParticles;
        private int baseFallingStarMaxParticles;
        private int baseFallingStarRate;

        // 状态
        private float currentParticleDensity = 1.0f;
        private float currentHueOffset = 0f;
        private float currentBrightness = 1f;
        private EnvironmentState currentState = EnvironmentState.Idle;
        private GamePhase currentPhase = GamePhase.Menu;

        // 缓存
        private ParticleSystem.Particle[] crystalBuffer;
        private int crystalBufferCapacity = 0;
        private float currentBokehSpeedModifier = 1f;
        private Color currentBackgroundColor;

        // 音频响应 Gradient 缓存（避免每帧 GC）
        private Gradient cachedStardustGrad;
        private GradientColorKey[] cachedStardustColorKeys;
        private GradientAlphaKey[] cachedStardustAlphaKeys;

        // BPM 精准节拍驱动
        private double nextBeatTimeMs = -1;
        private double currentMsPerBeat = 500;   // 默认 120BPM
        private int currentTimingPointIndex = -1;
        private Beatmap cachedBeatmapRef;
        private RhythmGameManager rhythmGameManager;
        private float beatBrightnessPulse = 0f;  // 节拍亮度脉冲值
        private bool isKiaiActive = false;

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
            SceneManager.activeSceneChanged += OnActiveSceneChanged;

            InitializeGradients();
            SetupCamera();
            GenerateResources();

            CreateNebulaLayer();
            CreateStardustLayer();
            CreateFallingStarLayer();
            CreateBokehLayer();
            CreateCrystalsLayer();
            CreateRingLayer();
            CreateAuroraLayer();
            CreatePulseRingLayer();
            CreateBubbleLayer();

            SetEnvironmentState(EnvironmentState.Idle);
            ApplyPhase(GamePhase.Menu);

            // 延迟查找 RhythmGameManager（可能尚未初始化）
            Invoke(nameof(FindRhythmGameManager), 1f);
        }

        private void FindRhythmGameManager()
        {
            rhythmGameManager = FindFirstObjectByType<RhythmGameManager>();
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            SetupCamera();
            DetectPhase(scene);
        }

        void OnActiveSceneChanged(Scene oldScene, Scene newScene)
        {
            DetectPhase(newScene);
        }

        void DetectPhase(Scene scene)
        {
            string name = scene.name;
            if (name.Contains("Game") && !name.Contains("Test"))
                ApplyPhase(GamePhase.Playing);
            else if (name.Contains("Result"))
                ApplyPhase(GamePhase.Result);
            else
                ApplyPhase(GamePhase.Menu);
        }

        /// <summary>
        /// 根据 GamePhase 切换整体氛围：
        /// - Menu：流星活跃、光斑大而密、背景紫罗兰
        /// - Playing：流星减少、粒子让位、背景更深
        /// - Result：恢复柔和、轻微庆祝
        /// </summary>
        void ApplyPhase(GamePhase phase)
        {
            currentPhase = phase;

            // 背景色平滑切换
            switch (phase)
            {
                case GamePhase.Menu:
                    currentBackgroundColor = menuBackgroundColor;
                    break;
                case GamePhase.Playing:
                    currentBackgroundColor = gameBackgroundColor;
                    break;
                case GamePhase.Result:
                    currentBackgroundColor = resultBackgroundColor;
                    break;
            }

            SetupCamera();

            // 流星层：菜单时全量，游戏中大幅减少，结算中等
            if (fallingStarPS != null)
            {
                var main = fallingStarPS.main;
                var emission = fallingStarPS.emission;
                switch (phase)
                {
                    case GamePhase.Menu:
                        main.maxParticles = baseFallingStarMaxParticles;
                        emission.rateOverTime = new ParticleSystem.MinMaxCurve(baseFallingStarRate);
                        break;
                    case GamePhase.Playing:
                        main.maxParticles = Mathf.RoundToInt(baseFallingStarMaxParticles * 0.15f);
                        emission.rateOverTime = new ParticleSystem.MinMaxCurve(Mathf.RoundToInt(baseFallingStarRate * 0.1f));
                        break;
                    case GamePhase.Result:
                        main.maxParticles = Mathf.RoundToInt(baseFallingStarMaxParticles * 0.5f);
                        emission.rateOverTime = new ParticleSystem.MinMaxCurve(Mathf.RoundToInt(baseFallingStarRate * 0.4f));
                        break;
                }
            }

            // 光斑层：菜单更大更密
            if (bokehPS != null)
            {
                var main = bokehPS.main;
                var emission = bokehPS.emission;
                switch (phase)
                {
                    case GamePhase.Menu:
                        main.maxParticles = baseBokehMaxParticles;
                        main.startSize = new ParticleSystem.MinMaxCurve(bokehMinSize, bokehMaxSizeMenu);
                        emission.rateOverTime = new ParticleSystem.MinMaxCurve(bokehRateMenu);
                        break;
                    case GamePhase.Playing:
                        main.maxParticles = Mathf.RoundToInt(baseBokehMaxParticles * 0.6f);
                        main.startSize = new ParticleSystem.MinMaxCurve(bokehMinSize, bokehMaxSizeGame);
                        emission.rateOverTime = new ParticleSystem.MinMaxCurve(bokehRateGame);
                        break;
                    case GamePhase.Result:
                        main.maxParticles = Mathf.RoundToInt(baseBokehMaxParticles * 0.8f);
                        main.startSize = new ParticleSystem.MinMaxCurve(bokehMinSize, bokehMaxSizeMenu * 0.8f);
                        emission.rateOverTime = new ParticleSystem.MinMaxCurve(Mathf.RoundToInt(bokehRateMenu * 0.6f));
                        break;
                }
            }

            // 环状光带：菜单全量，游戏中极少量，结算中等
            if (ringPS != null)
            {
                var main = ringPS.main;
                var emission = ringPS.emission;
                switch (phase)
                {
                    case GamePhase.Menu:
                        main.maxParticles = ringParticleCount;
                        emission.rateOverTime = new ParticleSystem.MinMaxCurve(Mathf.RoundToInt(ringParticleCount / 8f));
                        break;
                    case GamePhase.Playing:
                        main.maxParticles = Mathf.RoundToInt(ringParticleCount * 0.1f);
                        emission.rateOverTime = new ParticleSystem.MinMaxCurve(Mathf.RoundToInt(ringParticleCount / 40f));
                        break;
                    case GamePhase.Result:
                        main.maxParticles = Mathf.RoundToInt(ringParticleCount * 0.5f);
                        emission.rateOverTime = new ParticleSystem.MinMaxCurve(Mathf.RoundToInt(ringParticleCount / 16f));
                        break;
                }
            }

            // 极光丝带：菜单全量，游戏中完全禁用，结算少量
            if (auroraPS != null)
            {
                var main = auroraPS.main;
                var emission = auroraPS.emission;
                switch (phase)
                {
                    case GamePhase.Menu:
                        main.maxParticles = auroraMaxParticles;
                        emission.rateOverTime = new ParticleSystem.MinMaxCurve(auroraEmissionRate);
                        break;
                    case GamePhase.Playing:
                        main.maxParticles = 0;
                        emission.rateOverTime = new ParticleSystem.MinMaxCurve(0);
                        break;
                    case GamePhase.Result:
                        main.maxParticles = Mathf.RoundToInt(auroraMaxParticles * 0.3f);
                        emission.rateOverTime = new ParticleSystem.MinMaxCurve(1);
                        break;
                }
            }

            // 脉冲波纹：跨 Phase 存在，数量不同（由 burst 驱动）
            if (pulseRingPS != null)
            {
                var main = pulseRingPS.main;
                switch (phase)
                {
                    case GamePhase.Menu:
                        main.maxParticles = pulseRingMaxParticles;
                        break;
                    case GamePhase.Playing:
                        main.maxParticles = pulseBurstCountGame;
                        break;
                    case GamePhase.Result:
                        main.maxParticles = Mathf.RoundToInt(pulseRingMaxParticles * 0.6f);
                        break;
                }
            }

            // 上升气泡：菜单全量，游戏中完全禁用，结算少量
            if (bubblePS != null)
            {
                var main = bubblePS.main;
                var emission = bubblePS.emission;
                switch (phase)
                {
                    case GamePhase.Menu:
                        main.maxParticles = bubbleMaxParticles;
                        emission.rateOverTime = new ParticleSystem.MinMaxCurve(bubbleEmissionRate);
                        break;
                    case GamePhase.Playing:
                        main.maxParticles = 0;
                        emission.rateOverTime = new ParticleSystem.MinMaxCurve(0);
                        break;
                    case GamePhase.Result:
                        main.maxParticles = Mathf.RoundToInt(bubbleMaxParticles * 0.4f);
                        emission.rateOverTime = new ParticleSystem.MinMaxCurve(4);
                        break;
                }
            }
        }

        void InitializeGradients()
        {
            // 初始化星尘音频响应 Gradient 缓存
            cachedStardustColorKeys = new GradientColorKey[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            };
            cachedStardustAlphaKeys = new GradientAlphaKey[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(stardustAlpha, 0.15f),
                new GradientAlphaKey(stardustAlpha * 0.8f, 0.85f),
                new GradientAlphaKey(0f, 1f)
            };
            cachedStardustGrad = new Gradient();
            cachedStardustGrad.SetKeys(cachedStardustColorKeys, cachedStardustAlphaKeys);

            gradientIdle = CreateGradient3(
                new Color(0.2f, 0.1f, 0.7f),
                new Color(0.6f, 0.2f, 0.8f),
                new Color(1.0f, 0.4f, 0.7f),
                stardustAlpha
            );

            gradientCombo = CreateGradient3(
                new Color(0.05f, 0.5f, 0.9f),
                new Color(0.1f, 0.8f, 0.7f),
                new Color(0.2f, 1.0f, 0.8f),
                stardustAlpha
            );

            gradientKiai = CreateGradient3(
                new Color(1.0f, 0.7f, 0.15f),
                new Color(1.0f, 0.5f, 0.3f),
                new Color(1.0f, 0.9f, 0.75f),
                stardustAlpha * 1.8f
            );
        }

        Gradient CreateGradient3(Color a, Color b, Color c, float alpha)
        {
            Gradient g = new Gradient();
            g.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(a, 0f),
                    new GradientColorKey(b, 0.5f),
                    new GradientColorKey(c, 1f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(alpha, 0f),
                    new GradientAlphaKey(alpha * 1.2f, 0.5f),
                    new GradientAlphaKey(alpha, 1f)
                }
            );
            return g;
        }

        void SetupCamera()
        {
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                mainCam.clearFlags = CameraClearFlags.SolidColor;
                mainCam.backgroundColor = currentBackgroundColor;
            }
        }

        void GenerateResources()
        {
            // 辉光贴图
            int glowSize = 128;
            glowTexture = new Texture2D(glowSize, glowSize, TextureFormat.RGBA32, false);
            float center = glowSize * 0.5f;
            for (int y = 0; y < glowSize; y++)
            {
                for (int x = 0; x < glowSize; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    float nd = Mathf.Clamp01(dist / center);
                    float core = Mathf.Pow(1f - nd, 6f);
                    float glow = Mathf.Pow(1f - nd, 2.2f) * 0.35f;
                    float alpha = Mathf.Clamp01(core + glow);
                    float coreRatio = core / (alpha + 0.001f);
                    glowTexture.SetPixel(x, y, new Color(
                        Mathf.Lerp(0.7f, 1f, coreRatio),
                        Mathf.Lerp(0.8f, 1f, coreRatio),
                        1f, alpha));
                }
            }
            glowTexture.Apply();

            // Bokeh 贴图
            int bokehSize = 128;
            bokehTexture = new Texture2D(bokehSize, bokehSize, TextureFormat.RGBA32, false);
            float bCenter = bokehSize * 0.5f;
            for (int y = 0; y < bokehSize; y++)
            {
                for (int x = 0; x < bokehSize; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(bCenter, bCenter));
                    float nd = Mathf.Clamp01(dist / bCenter);
                    float disk = Mathf.Pow(1f - nd, 1.5f);
                    float ring = Mathf.Exp(-Mathf.Pow((nd - 0.7f) * 5f, 2f)) * 0.2f;
                    bokehTexture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(disk + ring)));
                }
            }
            bokehTexture.Apply();

            // 星云贴图
            int nebulaSize = 128;
            nebulaTexture = new Texture2D(nebulaSize, nebulaSize, TextureFormat.RGBA32, false);
            float nCenter = nebulaSize * 0.5f;
            for (int y = 0; y < nebulaSize; y++)
            {
                for (int x = 0; x < nebulaSize; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(nCenter, nCenter));
                    float nd = Mathf.Clamp01(dist / nCenter);
                    nebulaTexture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Pow(1f - nd, 1.2f)));
                }
            }
            nebulaTexture.Apply();

            Shader additiveShader = Shader.Find("Mobile/Particles/Additive")
                ?? Shader.Find("Legacy Shaders/Particles/Additive")
                ?? Shader.Find("Particles/Standard Unlit");

            if (additiveShader == null)
            {
                Debug.LogError("[EtherealEnvironment] 未找到可用粒子 Shader！");
                return;
            }

            nebulaMaterial = new Material(additiveShader) { enableInstancing = true, renderQueue = 1999 };
            nebulaMaterial.SetTexture("_MainTex", nebulaTexture);

            particleMaterial = new Material(additiveShader) { enableInstancing = true, renderQueue = 2000 };
            particleMaterial.SetTexture("_MainTex", glowTexture);

            fallingStarMaterial = new Material(additiveShader) { enableInstancing = true, renderQueue = 2000 };
            fallingStarMaterial.SetTexture("_MainTex", glowTexture);

            bokehMaterial = new Material(additiveShader) { enableInstancing = true, renderQueue = 2001 };
            bokehMaterial.SetTexture("_MainTex", bokehTexture);

            crystalMaterial = new Material(additiveShader) { enableInstancing = true, renderQueue = 2002 };
            crystalMaterial.SetTexture("_MainTex", glowTexture);

            ringMaterial = new Material(additiveShader) { enableInstancing = true, renderQueue = 2003 };
            ringMaterial.SetTexture("_MainTex", glowTexture);

            auroraMaterial = new Material(additiveShader) { enableInstancing = true, renderQueue = 1998 };
            auroraMaterial.SetTexture("_MainTex", nebulaTexture);

            pulseMaterial = new Material(additiveShader) { enableInstancing = true, renderQueue = 2004 };
            pulseMaterial.SetTexture("_MainTex", glowTexture);

            bubbleMaterial = new Material(additiveShader) { enableInstancing = true, renderQueue = 2001 };
            bubbleMaterial.SetTexture("_MainTex", glowTexture);
        }

        // ---- 星云层 ----
        void CreateNebulaLayer()
        {
            GameObject go = new GameObject("LayerA_NebulaClouds");
            go.transform.SetParent(transform);

            nebulaPS = go.AddComponent<ParticleSystem>();
            var main = nebulaPS.main;
            main.maxParticles = nebulaCount;
            main.startLifetime = new ParticleSystem.MinMaxCurve(30f, 50f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.005f, 0.02f);
            main.startSize = new ParticleSystem.MinMaxCurve(nebulaMinSize, nebulaMaxSize);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.35f, 0.1f, 0.65f, nebulaAlpha),
                new Color(0.15f, 0.25f, 0.85f, nebulaAlpha)
            );
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var shape = nebulaPS.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = nebulaMaxRadius;
            shape.radiusThickness = 1f;

            var emission = nebulaPS.emission;
            emission.rateOverTime = 1;

            var noise = nebulaPS.noise;
            noise.enabled = true;
            noise.strength = 0.02f;
            noise.frequency = 0.05f;
            noise.scrollSpeed = 0.02f;
            noise.quality = ParticleSystemNoiseQuality.Low;

            var colorOL = nebulaPS.colorOverLifetime;
            colorOL.enabled = true;
            Gradient g = new Gradient();
            g.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(nebulaAlpha * 1.5f, 0.15f),
                    new GradientAlphaKey(nebulaAlpha * 1.2f, 0.85f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOL.color = g;

            var renderer = nebulaPS.GetComponent<ParticleSystemRenderer>();
            renderer.material = nebulaMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.enableGPUInstancing = true;
        }

        // ---- 星尘层 ----
        void CreateStardustLayer()
        {
            GameObject go = new GameObject("LayerB_DreamyStardust");
            go.transform.SetParent(transform);

            baseStardustMaxParticles = stardustMaxParticles;
            baseStardustRate = stardustRate;

            stardustPS = go.AddComponent<ParticleSystem>();
            var main = stardustPS.main;
            main.maxParticles = stardustMaxParticles;
            main.startLifetime = new ParticleSystem.MinMaxCurve(stardustMinLifetime, stardustMaxLifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.01f, 0.05f);
            main.startSize = stardustSize;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var shape = stardustPS.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = stardustRadius;

            var emission = stardustPS.emission;
            emission.rateOverTime = stardustRate;

            var noise = stardustPS.noise;
            noise.enabled = true;
            noise.strength = 0.04f;
            noise.frequency = 0.15f;
            noise.scrollSpeed = 0.08f;
            noise.quality = ParticleSystemNoiseQuality.Medium;

            var colorOL = stardustPS.colorOverLifetime;
            colorOL.enabled = true;
            Gradient stardustGrad = new Gradient();
            stardustGrad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(stardustAlpha, 0.15f),
                    new GradientAlphaKey(stardustAlpha * 0.8f, 0.85f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOL.color = stardustGrad;

            var renderer = stardustPS.GetComponent<ParticleSystemRenderer>();
            renderer.material = particleMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.enableGPUInstancing = true;
        }

        // ---- 流星层（Beat Saber 风格的飘落粒子）----
        void CreateFallingStarLayer()
        {
            GameObject go = new GameObject("LayerC_FallingStars");
            go.transform.SetParent(transform);

            baseFallingStarMaxParticles = fallingStarMaxParticles;
            baseFallingStarRate = Mathf.RoundToInt(fallingStarMaxParticles / 10f); // 约10s填满

            fallingStarPS = go.AddComponent<ParticleSystem>();
            var main = fallingStarPS.main;
            main.maxParticles = fallingStarMaxParticles;
            main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 10f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
            main.startSize = new ParticleSystem.MinMaxCurve(fallingStarMinSize, fallingStarMaxSize);
            // 冷暖双色星尘，高饱和高亮度确保在深紫背景上可见
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.5f, 0.7f, 1.0f, fallingStarAlpha),
                new Color(0.9f, 0.4f, 1.0f, fallingStarAlpha * 0.7f)
            );
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            // 盒形发射：从头顶向下散布
            var shape = fallingStarPS.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(fallingStarRadius * 2f, 1f, fallingStarRadius * 2f);
            shape.position = new Vector3(0f, fallingStarHeight, 0f);

            var emission = fallingStarPS.emission;
            emission.rateOverTime = baseFallingStarRate;

            // 向下飘落 + 微弱横向飘动
            var velocity = fallingStarPS.velocityOverLifetime;
            velocity.enabled = true;
            velocity.y = new ParticleSystem.MinMaxCurve(-fallingStarFallSpeed * 0.7f, -fallingStarFallSpeed);
            velocity.x = new ParticleSystem.MinMaxCurve(-0.1f, 0.1f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.1f, 0.1f);
            velocity.space = ParticleSystemSimulationSpace.World;

            // 轻微重力感：粒子越往下越快（使用 ForceOverLifetime 替代不存在的 gravityOverLifetime）
            var gravity = fallingStarPS.forceOverLifetime;
            gravity.enabled = true;
            gravity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            gravity.y = new ParticleSystem.MinMaxCurve(-0.15f, -0.05f);
            gravity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            // 轻柔漂移
            var noise = fallingStarPS.noise;
            noise.enabled = true;
            noise.strength = 0.06f;
            noise.frequency = 0.1f;
            noise.scrollSpeed = 0.05f;
            noise.quality = ParticleSystemNoiseQuality.Low;

            // 淡入淡出，峰值 alpha 足够高
            var colorOL = fallingStarPS.colorOverLifetime;
            colorOL.enabled = true;
            Gradient fallGrad = new Gradient();
            fallGrad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(fallingStarAlpha, 0.1f),
                    new GradientAlphaKey(fallingStarAlpha * 0.8f, 0.7f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOL.color = fallGrad;

            // 尾巴拉长感（size随lifetime略增再减）
            var sizeOL = fallingStarPS.sizeOverLifetime;
            sizeOL.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(new Keyframe(0f, 0.3f, 0f, 3f));
            sizeCurve.AddKey(new Keyframe(0.1f, 1f, 0f, 0f));
            sizeCurve.AddKey(new Keyframe(0.6f, 0.8f, 0f, 0f));
            sizeCurve.AddKey(new Keyframe(1f, 0.1f, -1f, 0f));
            sizeOL.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            var renderer = fallingStarPS.GetComponent<ParticleSystemRenderer>();
            renderer.material = fallingStarMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.enableGPUInstancing = true;
        }

        // ---- 光斑层 ----
        void CreateBokehLayer()
        {
            GameObject go = new GameObject("LayerD_FloatingBokeh");
            go.transform.SetParent(transform);

            baseBokehMaxParticles = bokehMaxParticles;
            baseBokehRate = bokehRateMenu;

            bokehPS = go.AddComponent<ParticleSystem>();
            var main = bokehPS.main;
            main.maxParticles = bokehMaxParticles;
            main.startLifetime = new ParticleSystem.MinMaxCurve(10f, 18f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0f);
            main.startSize = new ParticleSystem.MinMaxCurve(bokehMinSize, bokehMaxSizeMenu);
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var shape = bokehPS.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(bokehRadius * 2f, 35f, bokehRadius * 2f);

            var emission = bokehPS.emission;
            emission.rateOverTime = bokehRateMenu;

            var velocity = bokehPS.velocityOverLifetime;
            velocity.enabled = true;
            velocity.y = new ParticleSystem.MinMaxCurve(bokehFloatSpeed * 0.4f, bokehFloatSpeed);
            velocity.x = new ParticleSystem.MinMaxCurve(-0.12f, 0.12f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);
            velocity.space = ParticleSystemSimulationSpace.World;

            var noise = bokehPS.noise;
            noise.enabled = true;
            noise.strength = 0.08f;
            noise.frequency = 0.12f;
            noise.scrollSpeed = 0.05f;
            noise.quality = ParticleSystemNoiseQuality.Low;

            var colorOL = bokehPS.colorOverLifetime;
            colorOL.enabled = true;
            Gradient alphaGrad = new Gradient();
            alphaGrad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.5f, 0.2f),
                    new GradientAlphaKey(0.45f, 0.6f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOL.color = alphaGrad;

            var sizeOL = bokehPS.sizeOverLifetime;
            sizeOL.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(new Keyframe(0f, 0.3f, 0f, 2f));
            sizeCurve.AddKey(new Keyframe(0.2f, 1f, 0f, 0f));
            sizeCurve.AddKey(new Keyframe(0.7f, 0.9f, 0f, 0f));
            sizeCurve.AddKey(new Keyframe(1f, 0.1f, -1f, 0f));
            sizeOL.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            var renderer = bokehPS.GetComponent<ParticleSystemRenderer>();
            renderer.material = bokehMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.enableGPUInstancing = true;
        }

        // ---- 晶体层 ----
        void CreateCrystalsLayer()
        {
            GameObject go = new GameObject("LayerE_SlowCrystals");
            go.transform.SetParent(transform);

            baseCrystalsMaxParticles = crystalsMaxParticles;

            crystalsPS = go.AddComponent<ParticleSystem>();
            var main = crystalsPS.main;
            main.maxParticles = crystalsMaxParticles;
            main.startLifetime = 25f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.05f);
            main.startSize = new ParticleSystem.MinMaxCurve(crystalsSize * 0.8f, crystalsSize * 1.2f);
            main.startRotation3D = true;

            var shape = crystalsPS.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = crystalsRadius;

            var emission = crystalsPS.emission;
            emission.rateOverTime = 3;

            var rotOL = crystalsPS.rotationOverLifetime;
            rotOL.enabled = true;
            rotOL.x = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);
            rotOL.y = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);
            rotOL.z = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);

            var colorOL = crystalsPS.colorOverLifetime;
            colorOL.enabled = true;
            Gradient crystalGrad = new Gradient();
            crystalGrad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.35f, 0.15f),
                    new GradientAlphaKey(0.3f, 0.85f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOL.color = crystalGrad;

            var sizeOL = crystalsPS.sizeOverLifetime;
            sizeOL.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(new Keyframe(0f, 0.4f, 0f, 1.5f));
            sizeCurve.AddKey(new Keyframe(0.2f, 1f, 0f, 0f));
            sizeCurve.AddKey(new Keyframe(0.8f, 0.8f, 0f, 0f));
            sizeCurve.AddKey(new Keyframe(1f, 0.2f, -1f, 0f));
            sizeOL.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            var renderer = crystalsPS.GetComponent<ParticleSystemRenderer>();
            renderer.material = crystalMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.enableGPUInstancing = true;
        }

        // ---- 环状光带层（Beat Saber 风格的环绕粒子）----
        void CreateRingLayer()
        {
            GameObject go = new GameObject("LayerF_RingAura");
            go.transform.SetParent(transform);

            ringPS = go.AddComponent<ParticleSystem>();
            var main = ringPS.main;
            main.maxParticles = ringParticleCount;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 12f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.06f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
            // 蓝紫色高亮
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.4f, 0.6f, 1.0f, ringParticleAlpha),
                new Color(0.7f, 0.3f, 1.0f, ringParticleAlpha * 0.8f)
            );
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            // 环形发射：在玩家周围的水平圆环上
            var shape = ringPS.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = ringRadius;
            shape.radiusThickness = 0.3f;
            shape.arc = 360f;
            // 圆环平面水平放置
            shape.rotation = new Vector3(90f, 0f, 0f);
            shape.position = new Vector3(0f, ringHeight, 0f);

            var emission = ringPS.emission;
            emission.rateOverTime = Mathf.RoundToInt(ringParticleCount / 8f);

            // 缓慢旋转：让环上的粒子绕圆心缓慢漂流
            var velocity = ringPS.velocityOverLifetime;
            velocity.enabled = true;
            velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.y = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.orbitalX = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.orbitalY = new ParticleSystem.MinMaxCurve(ringRotSpeed * 0.9f, ringRotSpeed * 1.1f);
            velocity.orbitalZ = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.space = ParticleSystemSimulationSpace.Local;

            // 轻微上下浮动
            var noise = ringPS.noise;
            noise.enabled = true;
            noise.strength = 0.03f;
            noise.frequency = 0.1f;
            noise.scrollSpeed = 0.03f;
            noise.quality = ParticleSystemNoiseQuality.Low;

            // 淡入淡出
            var colorOL = ringPS.colorOverLifetime;
            colorOL.enabled = true;
            Gradient ringGrad = new Gradient();
            ringGrad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(ringParticleAlpha, 0.15f),
                    new GradientAlphaKey(ringParticleAlpha * 0.7f, 0.85f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOL.color = ringGrad;

            // 呼吸 size
            var sizeOL = ringPS.sizeOverLifetime;
            sizeOL.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(new Keyframe(0f, 0.3f, 0f, 2f));
            sizeCurve.AddKey(new Keyframe(0.15f, 1f, 0f, 0f));
            sizeCurve.AddKey(new Keyframe(0.7f, 0.8f, 0f, 0f));
            sizeCurve.AddKey(new Keyframe(1f, 0.2f, -1f, 0f));
            sizeOL.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            var renderer = ringPS.GetComponent<ParticleSystemRenderer>();
            renderer.material = ringMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.enableGPUInstancing = true;
        }

        // ---- 极光丝带层（水平飘动的梦幻光带）----
        void CreateAuroraLayer()
        {
            GameObject go = new GameObject("LayerG_AuroraRibbons");
            go.transform.SetParent(transform);

            auroraPS = go.AddComponent<ParticleSystem>();
            var main = auroraPS.main;
            main.maxParticles = auroraMaxParticles;
            main.startLifetime = new ParticleSystem.MinMaxCurve(20f, 40f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.01f, 0.03f);
            main.startSize3D = true;
            main.startSizeX = new ParticleSystem.MinMaxCurve(auroraMinSizeX, auroraMaxSizeX);
            main.startSizeY = new ParticleSystem.MinMaxCurve(auroraMinSizeY, auroraMaxSizeY);
            main.startSizeZ = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.3f, 0.5f, 1.0f, auroraAlpha),
                new Color(0.6f, 0.2f, 1.0f, auroraAlpha * 1.1f)
            );
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            // 水平层分布
            var shape = auroraPS.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(60f, 4f, 60f);
            shape.position = new Vector3(0f, 8f, 0f);

            var emission = auroraPS.emission;
            emission.rateOverTime = auroraEmissionRate;

            // 极慢水平漂移（全部 RandomBetweenTwoConstants）
            var velocity = auroraPS.velocityOverLifetime;
            velocity.enabled = true;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.3f, -0.1f);
            velocity.y = new ParticleSystem.MinMaxCurve(-0.02f, 0.02f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.1f, 0.1f);
            velocity.space = ParticleSystemSimulationSpace.World;

            // 低频大幅飘动
            var noise = auroraPS.noise;
            noise.enabled = true;
            noise.strength = 0.5f;
            noise.frequency = 0.03f;
            noise.scrollSpeed = 0.02f;
            noise.quality = ParticleSystemNoiseQuality.Low;

            // 缓慢渐变 size
            var sizeOL = auroraPS.sizeOverLifetime;
            sizeOL.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(new Keyframe(0f, 0.3f, 0f, 1f));
            sizeCurve.AddKey(new Keyframe(0.2f, 0.7f, 0f, 0f));
            sizeCurve.AddKey(new Keyframe(0.5f, 1f, 0f, 0f));
            sizeCurve.AddKey(new Keyframe(0.8f, 0.7f, 0f, 0f));
            sizeCurve.AddKey(new Keyframe(1f, 0.3f, -1f, 0f));
            sizeOL.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            // 淡入淡出
            var colorOL = auroraPS.colorOverLifetime;
            colorOL.enabled = true;
            Gradient auroraGrad = new Gradient();
            auroraGrad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(auroraAlpha * 1.5f, 0.15f),
                    new GradientAlphaKey(auroraAlpha * 1.2f, 0.85f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOL.color = auroraGrad;

            var auroraRenderer = auroraPS.GetComponent<ParticleSystemRenderer>();
            auroraRenderer.material = auroraMaterial;
            auroraRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            auroraRenderer.enableGPUInstancing = true;
        }

        // ---- 脉冲波纹层（节拍驱动环形波纹）----
        void CreatePulseRingLayer()
        {
            GameObject go = new GameObject("LayerH_PulseRings");
            go.transform.SetParent(transform);

            pulseRingPS = go.AddComponent<ParticleSystem>();
            var main = pulseRingPS.main;
            main.maxParticles = pulseRingMaxParticles;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 2.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0f);
            main.startSize = new ParticleSystem.MinMaxCurve(1f, 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.5f, 0.7f, 1.0f, pulseRingAlpha),
                new Color(0.7f, 0.4f, 1.0f, pulseRingAlpha * 0.8f)
            );
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            // 从中心点发射
            var shape = pulseRingPS.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 1f;
            shape.radiusThickness = 0f;
            shape.arc = 360f;
            shape.rotation = new Vector3(90f, 0f, 0f);

            // 不用 rateOverTime，由代码 burst 驱动
            var emission = pulseRingPS.emission;
            emission.rateOverTime = 0;

            // 快速扩散（全部 RandomBetweenTwoConstants）
            var velocity = pulseRingPS.velocityOverLifetime;
            velocity.enabled = true;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);
            velocity.y = new ParticleSystem.MinMaxCurve(-0.02f, 0.02f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);
            velocity.speedModifier = new ParticleSystem.MinMaxCurve(1f, 1f);
            velocity.space = ParticleSystemSimulationSpace.World;

            // 从小圈快速扩展到大圈
            var sizeOL = pulseRingPS.sizeOverLifetime;
            sizeOL.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(new Keyframe(0f, 0.5f, 4f, 4f));
            sizeCurve.AddKey(new Keyframe(0.3f, 2f, 1f, 1f));
            sizeCurve.AddKey(new Keyframe(1f, 4f, 0.5f, 0f));
            sizeOL.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            // alpha 快速衰减
            var colorOL = pulseRingPS.colorOverLifetime;
            colorOL.enabled = true;
            Gradient pulseGrad = new Gradient();
            pulseGrad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(pulseRingAlpha, 0f),
                    new GradientAlphaKey(pulseRingAlpha * 0.5f, 0.3f),
                    new GradientAlphaKey(pulseRingAlpha * 0.15f, 0.7f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOL.color = pulseGrad;

            var pulseRenderer = pulseRingPS.GetComponent<ParticleSystemRenderer>();
            pulseRenderer.material = pulseMaterial;
            pulseRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            pulseRenderer.enableGPUInstancing = true;
        }

        // ---- 上升气泡层（从下方缓缓上升的半透明光球）----
        void CreateBubbleLayer()
        {
            GameObject go = new GameObject("LayerI_RisingBubbles");
            go.transform.SetParent(transform);

            bubblePS = go.AddComponent<ParticleSystem>();
            var main = bubblePS.main;
            main.maxParticles = bubbleMaxParticles;
            main.startLifetime = new ParticleSystem.MinMaxCurve(8f, 15f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.06f);
            main.startSize = new ParticleSystem.MinMaxCurve(bubbleMinSize, bubbleMaxSize);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.4f, 0.6f, 1.0f, bubbleAlpha),
                new Color(0.6f, 0.3f, 1.0f, bubbleAlpha * 0.7f)
            );
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            // 从下方发射
            var shape = bubblePS.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(50f, 1f, 50f);
            shape.position = new Vector3(0f, -15f, 0f);

            var emission = bubblePS.emission;
            emission.rateOverTime = bubbleEmissionRate;

            // 缓慢上升 + 微弱漂移（全部 RandomBetweenTwoConstants）
            var velocity = bubblePS.velocityOverLifetime;
            velocity.enabled = true;
            velocity.y = new ParticleSystem.MinMaxCurve(bubbleRiseSpeed * 0.7f, bubbleRiseSpeed * 1.3f);
            velocity.x = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);
            velocity.space = ParticleSystemSimulationSpace.World;

            // 轻微摇摆
            var noise = bubblePS.noise;
            noise.enabled = true;
            noise.strength = 0.1f;
            noise.frequency = 0.08f;
            noise.scrollSpeed = 0.04f;
            noise.quality = ParticleSystemNoiseQuality.Low;

            // 先膨胀再缩小
            var sizeOL = bubblePS.sizeOverLifetime;
            sizeOL.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(new Keyframe(0f, 0.3f, 0f, 2f));
            sizeCurve.AddKey(new Keyframe(0.2f, 1f, 0f, 0f));
            sizeCurve.AddKey(new Keyframe(0.6f, 0.85f, 0f, 0f));
            sizeCurve.AddKey(new Keyframe(1f, 0.2f, -1f, 0f));
            sizeOL.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            // 淡入淡出
            var colorOL = bubblePS.colorOverLifetime;
            colorOL.enabled = true;
            Gradient bubbleGrad = new Gradient();
            bubbleGrad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(bubbleAlpha, 0.15f),
                    new GradientAlphaKey(bubbleAlpha * 0.8f, 0.8f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOL.color = bubbleGrad;

            var bubbleRenderer = bubblePS.GetComponent<ParticleSystemRenderer>();
            bubbleRenderer.material = bubbleMaterial;
            bubbleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            bubbleRenderer.enableGPUInstancing = true;
        }

        // =========================================================
        // 状态切换
        // =========================================================

        public void SetEnvironmentState(EnvironmentState state)
        {
            currentState = state;
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
                    targetSpeedMultiplier = 1.15f;
                    break;
                case EnvironmentState.Kiai:
                    targetGradient = gradientKiai;
                    targetSpeedMultiplier = 1.6f;
                    break;
            }

            var mainStardust = stardustPS.main;
            mainStardust.startColor = new ParticleSystem.MinMaxGradient(targetGradient);

            var mainBokeh = bokehPS.main;
            mainBokeh.startColor = new ParticleSystem.MinMaxGradient(targetGradient);
            var velocityBokeh = bokehPS.velocityOverLifetime;
            velocityBokeh.speedModifier = new ParticleSystem.MinMaxCurve(targetSpeedMultiplier);
            currentBokehSpeedModifier = targetSpeedMultiplier;

            var mainCrystals = crystalsPS.main;
            mainCrystals.startColor = new ParticleSystem.MinMaxGradient(targetGradient);

            // 流星层也变色
            if (fallingStarPS != null)
            {
                var mainFall = fallingStarPS.main;
                Color fallStart = targetGradient.Evaluate(0f);
                Color fallEnd = targetGradient.Evaluate(1f);
                fallStart.a = 0.5f;
                fallEnd.a = 0.35f;
                mainFall.startColor = new ParticleSystem.MinMaxGradient(fallStart, fallEnd);
            }

            // 星云层变色
            if (nebulaPS != null)
            {
                var mainNebula = nebulaPS.main;
                Color nebulaStart = targetGradient.Evaluate(0f);
                Color nebulaEnd = targetGradient.Evaluate(1f);
                nebulaStart.a = nebulaAlpha;
                nebulaEnd.a = nebulaAlpha;
                mainNebula.startColor = new ParticleSystem.MinMaxGradient(nebulaStart, nebulaEnd);
            }

            // 环状光带变色
            if (ringPS != null)
            {
                var mainRing = ringPS.main;
                Color ringStart = targetGradient.Evaluate(0.3f);
                Color ringEnd = targetGradient.Evaluate(0.7f);
                ringStart.a = ringParticleAlpha;
                ringEnd.a = ringParticleAlpha * 0.8f;
                mainRing.startColor = new ParticleSystem.MinMaxGradient(ringStart, ringEnd);
            }

            // 极光丝带变色
            if (auroraPS != null)
            {
                var mainAurora = auroraPS.main;
                Color auroraStart = targetGradient.Evaluate(0.2f);
                Color auroraEnd = targetGradient.Evaluate(0.8f);
                auroraStart.a = auroraAlpha;
                auroraEnd.a = auroraAlpha * 1.1f;
                mainAurora.startColor = new ParticleSystem.MinMaxGradient(auroraStart, auroraEnd);
            }

            // 脉冲波纹变色
            if (pulseRingPS != null)
            {
                var mainPulse = pulseRingPS.main;
                Color pulseStart = targetGradient.Evaluate(0.4f);
                Color pulseEnd = targetGradient.Evaluate(0.6f);
                pulseStart.a = pulseRingAlpha;
                pulseEnd.a = pulseRingAlpha * 0.8f;
                mainPulse.startColor = new ParticleSystem.MinMaxGradient(pulseStart, pulseEnd);
            }

            // 上升气泡变色
            if (bubblePS != null)
            {
                var mainBubble = bubblePS.main;
                Color bubbleStart = targetGradient.Evaluate(0.1f);
                Color bubbleEnd = targetGradient.Evaluate(0.9f);
                bubbleStart.a = bubbleAlpha;
                bubbleEnd.a = bubbleAlpha * 0.7f;
                mainBubble.startColor = new ParticleSystem.MinMaxGradient(bubbleStart, bubbleEnd);
            }
        }

        public void TriggerKickEvent()
        {
            if (crystalsPS == null) return;

            int count = crystalsPS.particleCount;
            if (count == 0) return;

            EnsureCrystalBuffer(count);
            count = crystalsPS.GetParticles(crystalBuffer);

            for (int i = 0; i < count; i++)
            {
                crystalBuffer[i].startSize = crystalsSize * bassSizeKick;
            }
            crystalsPS.SetParticles(crystalBuffer, count);
        }

        private void EnsureCrystalBuffer(int count)
        {
            if (crystalBuffer == null || crystalBufferCapacity < count)
            {
                crystalBufferCapacity = Mathf.Max(count + 8, 32);
                crystalBuffer = new ParticleSystem.Particle[crystalBufferCapacity];
            }
        }

        void Update()
        {
            UpdateKiaiState();
            UpdateBeatTiming();
            UpdateBeatPulseDecay();
            UpdateCrystalBreathing();
            UpdateAudioResponse();
            UpdateBackgroundColor();
        }

        // =========================================================
        // BPM 精准节拍驱动
        // =========================================================

        private void UpdateBeatTiming()
        {
            if (rhythmGameManager == null || !rhythmGameManager.isPlaying)
                return;

            double currentTimeMs = rhythmGameManager.currentMusicTimeMs;
            Beatmap beatmap = rhythmGameManager.GetCurrentBeatmap();

            if (beatmap == null || beatmap.ControlPoints == null || beatmap.ControlPoints.Timing.Count == 0)
                return;

            // 谱面引用变化时重置
            if (beatmap != cachedBeatmapRef)
            {
                cachedBeatmapRef = beatmap;
                nextBeatTimeMs = -1;
                currentTimingPointIndex = -1;
            }

            var timingPoints = beatmap.ControlPoints.Timing;
            int tpIndex = FindTimingPointIndex(timingPoints, currentTimeMs);

            // 红线变化 → 重新同步
            if (tpIndex != currentTimingPointIndex)
            {
                currentTimingPointIndex = tpIndex;
                var tp = timingPoints[tpIndex];
                currentMsPerBeat = tp.MsPerBeat;

                double tpStart = tp.Time;
                double beatsFromStart = (currentTimeMs - tpStart) / tp.MsPerBeat;
                int beatNumber = (int)Math.Ceiling(beatsFromStart);
                nextBeatTimeMs = tpStart + beatNumber * tp.MsPerBeat;
            }

            double anticipatedTime = currentTimeMs + beatAnticipation * 1000.0;
            if (nextBeatTimeMs >= 0 && anticipatedTime >= nextBeatTimeMs)
            {
                // 节拍触发
                float flashPower = beatBrightnessPeak * (isKiaiActive ? kiaiBeatMultiplier : 1f);
                beatBrightnessPulse = flashPower;

                // 晶体踢打
                TriggerKickEvent();

                // 脉冲波纹 burst
                if (pulseRingPS != null)
                {
                    // burst 数量在 Playing 时由 maxParticles 限制
                    int burstCount = isKiaiActive ? pulseBurstCountMenu : pulseBurstCountGame;
                    if (currentPhase == GamePhase.Menu) burstCount = pulseBurstCountMenu;
                    else if (currentPhase == GamePhase.Result) burstCount = pulseBurstCountResult;
                    pulseRingPS.Emit(burstCount);
                }

                nextBeatTimeMs += currentMsPerBeat;
                if (tpIndex + 1 < timingPoints.Count && nextBeatTimeMs >= timingPoints[tpIndex + 1].Time)
                {
                    currentTimingPointIndex = -1;
                }
            }
        }

        private static int FindTimingPointIndex(System.Collections.Generic.List<TimingPoint> timingPoints, double time)
        {
            int lo = 0, hi = timingPoints.Count - 1;
            int result = 0;
            while (lo <= hi)
            {
                int mid = (lo + hi) / 2;
                if (timingPoints[mid].Time <= time)
                {
                    result = mid;
                    lo = mid + 1;
                }
                else
                {
                    hi = mid - 1;
                }
            }
            return result;
        }

        private void UpdateBeatPulseDecay()
        {
            if (beatBrightnessPulse > 0.01f)
            {
                beatBrightnessPulse *= Mathf.Exp(-beatBrightnessDecay * Time.deltaTime);
            }
            else
            {
                beatBrightnessPulse = 0f;
            }
        }

        private void UpdateKiaiState()
        {
            isKiaiActive = false;

            if (rhythmGameManager == null || !rhythmGameManager.isPlaying)
                return;

            double currentTimeMs = rhythmGameManager.currentMusicTimeMs;
            Beatmap beatmap = rhythmGameManager.GetCurrentBeatmap();
            if (beatmap == null || beatmap.ControlPoints == null)
                return;

            var kiaiPeriods = beatmap.ControlPoints.KiaiPeriods;
            foreach (var kiaiPeriod in kiaiPeriods)
            {
                if (currentTimeMs >= kiaiPeriod.StartTime && currentTimeMs <= kiaiPeriod.EndTime)
                {
                    isKiaiActive = true;
                    break;
                }
            }
        }

        /// <summary>
        /// 背景色平滑过渡
        /// </summary>
        private void UpdateBackgroundColor()
        {
            Camera mainCam = Camera.main;
            if (mainCam == null) return;

            Color current = mainCam.backgroundColor;
            if (current != currentBackgroundColor)
            {
                mainCam.backgroundColor = Color.Lerp(current, currentBackgroundColor, Time.deltaTime * 3f);
            }
        }

        private void UpdateCrystalBreathing()
        {
            if (crystalsPS == null) return;

            int count = crystalsPS.particleCount;
            if (count == 0) return;

            EnsureCrystalBuffer(count);
            count = crystalsPS.GetParticles(crystalBuffer);
            bool modified = false;

            for (int i = 0; i < count; i++)
            {
                float size = crystalBuffer[i].startSize;
                if (size > crystalsSize * 1.05f)
                {
                    crystalBuffer[i].startSize = Mathf.Max(crystalsSize, size - Time.deltaTime * 0.4f);
                    modified = true;
                }
            }
            if (modified) crystalsPS.SetParticles(crystalBuffer, count);
        }

        private void UpdateAudioResponse()
        {
            var audioManager = AudioVisualizationManager.Instance;
            if (audioManager == null) return;

            float dt = Time.deltaTime;
            float lerpFactor = 1f - Mathf.Exp(-audioResponseSmooth * dt);

            float bass = audioManager.Bass;
            float treble = audioManager.Treble;
            float mid = audioManager.Mid;

            // Bass → 星尘亮度脉冲 + BPM 节拍脉冲叠加
            float targetBrightness = 0.3f + bass * bassBrightnessGain;
            if (isKiaiActive) targetBrightness *= 1.5f;

            // 叠加节拍脉冲
            targetBrightness += beatBrightnessPulse;

            currentBrightness = Mathf.Lerp(currentBrightness, targetBrightness, lerpFactor);

            var stardustColorOL = stardustPS.colorOverLifetime;
            float a = currentBrightness * stardustAlpha;
            cachedStardustAlphaKeys[0].alpha = 0f;
            cachedStardustAlphaKeys[1].alpha = a;
            cachedStardustAlphaKeys[2].alpha = a * 0.8f;
            cachedStardustAlphaKeys[3].alpha = 0f;
            cachedStardustGrad.SetKeys(cachedStardustColorKeys, cachedStardustAlphaKeys);
            stardustColorOL.color = cachedStardustGrad;

            // Bass → 光斑加速
            float targetSpeed = 1f + bass * 0.5f;
            if (isKiaiActive) targetSpeed += 0.5f;
            // 节拍时额外加速
            targetSpeed += beatBrightnessPulse * 0.3f;
            currentBokehSpeedModifier = Mathf.Lerp(currentBokehSpeedModifier, targetSpeed, lerpFactor);
            var bokehVel = bokehPS.velocityOverLifetime;
            bokehVel.speedModifier = new ParticleSystem.MinMaxCurve(currentBokehSpeedModifier);

            // Treble → 色相偏移
            currentHueOffset += treble * trebleHueShift * dt;
            if (currentHueOffset > 1f) currentHueOffset -= 1f;

            // Mid → 星尘 noise 增强
            var stardustNoise = stardustPS.noise;
            float currentNoise = stardustNoise.strength.constant;
            float targetNoiseStrength = 0.04f + mid * 0.15f;
            stardustNoise.strength = new ParticleSystem.MinMaxCurve(
                Mathf.Lerp(currentNoise, targetNoiseStrength, lerpFactor)
            );

            // Bass → 流星速度脉冲
            if (fallingStarPS != null)
            {
                float fallSpeedBoost = 1f + bass * 0.8f;
                currentFallingStarSpeedMod = Mathf.Lerp(currentFallingStarSpeedMod, fallSpeedBoost, lerpFactor);
                var fallVel = fallingStarPS.velocityOverLifetime;
                fallVel.speedModifier = new ParticleSystem.MinMaxCurve(currentFallingStarSpeedMod);
            }
        }

        public void SetParticleDensity(float density)
        {
            currentParticleDensity = Mathf.Clamp01(density);

            if (stardustPS != null)
            {
                var main = stardustPS.main;
                main.maxParticles = Mathf.RoundToInt(baseStardustMaxParticles * currentParticleDensity);
                var emission = stardustPS.emission;
                emission.rateOverTime = new ParticleSystem.MinMaxCurve(
                    Mathf.RoundToInt(baseStardustRate * currentParticleDensity)
                );
            }

            if (bokehPS != null)
            {
                var main = bokehPS.main;
                main.maxParticles = Mathf.RoundToInt(baseBokehMaxParticles * currentParticleDensity);
            }

            if (crystalsPS != null)
            {
                var main = crystalsPS.main;
                main.maxParticles = Mathf.RoundToInt(baseCrystalsMaxParticles * currentParticleDensity);
            }
        }

        public float GetParticleDensity() => currentParticleDensity;
    }
}
