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
        [Range(0.01f, 0.3f)] public float nebulaAlpha = 0.12f;

        [Header("星尘层")]
        public float stardustRadius = 60f;
        public int stardustMaxParticles = 25000;
        public int stardustRate = 2500;
        public float stardustMinLifetime = 18f;
        public float stardustMaxLifetime = 30f;
        public float stardustSize = 0.05f;
        [Range(0.05f, 0.4f)] public float stardustAlpha = 0.18f;

        [Header("光斑层")]
        public float bokehRadius = 35f;
        public int bokehMaxParticles = 400;
        public int bokehRateMenu = 25;
        public int bokehRateGame = 12;
        public float bokehMinSize = 0.08f;
        public float bokehMaxSizeMenu = 0.35f;
        public float bokehMaxSizeGame = 0.25f;
        public float bokehFloatSpeed = 0.25f;

        [Header("流星层 (菜单/选歌装饰)")]
        public int fallingStarMaxParticles = 500;
        public float fallingStarRadius = 35f;
        public float fallingStarHeight = 25f;
        public float fallingStarFallSpeed = 1.0f;
        public float fallingStarMinSize = 0.03f;
        public float fallingStarMaxSize = 0.12f;
        [Range(0.1f, 1f)] public float fallingStarAlpha = 0.5f;

        [Header("晶体层")]
        public int crystalsMaxParticles = 40;
        public float crystalsRadius = 25f;
        public float crystalsSize = 0.12f;

        [Header("环状光带 (菜单环绕装饰)")]
        public int ringParticleCount = 200;
        public float ringRadius = 8f;
        public float ringHeight = 2f;
        public float ringRotSpeed = 0.3f;
        public float ringParticleAlpha = 0.35f;

        [Header("极光丝带层 (菜单装饰)")]
        public int auroraMaxParticles = 8;
        public int auroraEmissionRate = 3;
        public float auroraMinSizeX = 7.5f;
        public float auroraMaxSizeX = 30f;
        public float auroraMinSizeY = 0.3f;
        public float auroraMaxSizeY = 0.8f;
        [Range(0.01f, 0.15f)] public float auroraAlpha = 0.07f;

        [Header("脉冲波纹层 (节拍驱动)")]
        public int pulseRingMaxParticles = 20;
        public int pulseBurstCountMenu = 8;
        public int pulseBurstCountGame = 3;
        public int pulseBurstCountResult = 5;
        public float pulseRingAlpha = 0.25f;

        [Header("上升气泡层 (菜单装饰)")]
        public int bubbleMaxParticles = 100;
        public int bubbleEmissionRate = 10;
        public float bubbleMinSize = 0.2f;
        public float bubbleMaxSize = 0.8f;
        [Range(0.05f, 0.3f)] public float bubbleAlpha = 0.15f;
        public float bubbleRiseSpeed = 0.4f;

        [Header("音频响应")]
        [SerializeField] private float audioResponseSmooth = 10f;
        [SerializeField] private float bassBrightnessGain = 1.2f;
        [SerializeField] private float bassSizeKick = 2.5f;
        [SerializeField] private float trebleHueShift = 0.3f;

        [Header("频谱精细响应 (AudioLink 8频段)")]
        [Tooltip("将128频谱压缩为8频段，用于粒子分组响应")]
        [SerializeField] private int spectrumBands = 8;
        [Tooltip("频谱平滑速度（越大响应越快，推荐30-50）")]
        [SerializeField] private float spectrumSmoothSpeed = 40f;
        [Tooltip("即时响应模式：快速响应但保留少量平滑")]
        [SerializeField] private bool instantResponseMode = false;
        [Tooltip("即时模式平滑因子（0-1，越小越平滑，推荐0.85）")]
        [SerializeField] private float instantSmoothFactor = 0.25f;

        [Header("镜面地板")]
        [Tooltip("地板大小(正方形边长)")]
        [SerializeField] private float mirrorFloorSize = 5f;
        [Tooltip("地板透明度")]
        [Range(0.05f, 0.5f)]
        [SerializeField] private float mirrorFloorAlpha = 0.15f;
        [Tooltip("地板反射强度")]
        [Range(0.1f, 1f)]
        [SerializeField] private float mirrorReflectivity = 0.3f;

        [Header("两侧频谱粒子 (地板两侧引导线)")]
        [Tooltip("每侧频谱条数量")]
        [SerializeField] private int spectrumBarCount = 128;
        [Tooltip("频谱条最大高度")]
        [SerializeField] private float spectrumBarHeightMax = 20.0f;
        [Tooltip("透明度")]
        [SerializeField] private float spectrumBarAlpha = 0.5f;
        [Tooltip("频谱条基础宽度")]
        [SerializeField] private float spectrumBarWidth = 0.08f;
        [Tooltip("频谱增益倍数（放大AudioLink数据）")]
        [SerializeField] private float spectrumGain = 2.0f;
        [Tooltip("频谱非线性放大指数（<1增强低值，>1增强高值）")]
        [SerializeField] private float spectrumPower = 0.7f;
        [Tooltip("频谱最小阈值（低于此值不显示）")]
        [SerializeField] private float spectrumMinThreshold = 0.005f;
        [Tooltip("低频额外增强倍数")]
        [SerializeField] private float spectrumBassBoost = 2.5f;
        [Tooltip("低频区域占比（前N%为低频）")]
        [SerializeField] private float spectrumBassRatio = 0.25f;
        [Tooltip("Kiai闪烁强度")]
        [SerializeField] private float spectrumKiaiFlashIntensity = 0.8f;
        [Tooltip("频谱分布偏移（正值让高频往左/前移，负值让低频往左/前移）")]
        [SerializeField] private float spectrumDistributionShift = 0.35f;

        [Header("节拍驱动")]
        [Tooltip("节拍时晶体/星尘的亮度脉冲峰值")]
        [SerializeField] private float beatBrightnessPeak = 1.5f;
        [Tooltip("节拍亮度脉冲衰减速度")]
        [SerializeField] private float beatBrightnessDecay = 8f;
        [Tooltip("节拍提前量（秒）")]
        [SerializeField] private float beatAnticipation = 0.015f;
        [Tooltip("Kiai 时节拍脉冲倍率")]
        [SerializeField] private float kiaiBeatMultiplier = 1.5f;

        // 色彩映射
        private Gradient gradientIdle;
        private Gradient gradientCombo;
        private Gradient gradientKiai;

        // 贴图和材质
        private Texture2D glowTexture;
        private Texture2D spectrumTexture; // 频谱专用高清贴图
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

        // 镜面地板
        private GameObject mirrorFloorObj;
        private Material mirrorFloorMaterial;

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

        // 两侧频谱粒子系统
        private ParticleSystem spectrumLeftPS;
        private ParticleSystem spectrumRightPS;
        private Material spectrumBarMaterial;

        // AudioLink 频谱数据
        private float[] spectrumBandValues;          // 8频段值
        private float[] spectrumBandSmoothed;        // 平滑后的值
        private float[] spectrumBarHeights;          // 64条高度 (用于两侧频谱)
        private float[] spectrumBarHeightsSmoothed;  // 平滑后的高度
        private bool audioLinkAvailable = false;

        // AudioLink 反射缓存（避免每帧查找）
        private System.Type audioLinkCachedType;
        private MonoBehaviour audioLinkCachedInstance;
        private System.Reflection.MethodInfo audioLinkGetDataMethod;
        private System.Reflection.MethodInfo audioLinkIsAvailableMethod;

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

            // 初始化频谱数组
            spectrumBandValues = new float[spectrumBands];
            spectrumBandSmoothed = new float[spectrumBands];
            spectrumBarHeights = new float[spectrumBarCount];
            spectrumBarHeightsSmoothed = new float[spectrumBarCount];

            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;

            InitializeGradients();
            SetupCamera();
            GenerateResources();

            CreateMirrorFloor();
            CreateNebulaLayer();
            CreateStardustLayer();
            CreateFallingStarLayer();
            CreateBokehLayer();
            CreateCrystalsLayer();
            CreateRingLayer();
            CreateAuroraLayer();
            CreatePulseRingLayer();
            CreateBubbleLayer();
            CreateSpectrumBarsLayer();

            SetEnvironmentState(EnvironmentState.Idle);
            ApplyPhase(GamePhase.Menu);

            // 延迟查找 RhythmGameManager（可能尚未初始化）
            Invoke(nameof(FindRhythmGameManager), 1f);
            // AudioLink检测在场景切换时自动执行，无需在Awake中调用
        }

        private void CheckAudioLinkAvailability()
        {
            // 只在GamePhase.Playing时需要AudioLink，其他场景不需要频谱
            if (currentPhase != GamePhase.Playing)
            {
                audioLinkAvailable = false;
                audioLinkCachedInstance = null;
                return;
            }

            // 检查AudioLink是否可用（注意：AudioLink类在AudioLink命名空间中）
            if (audioLinkCachedType == null)
            {
                audioLinkCachedType = System.Type.GetType("AudioLink.AudioLink, AudioLink");
            }

            if (audioLinkCachedType != null)
            {
                // 如果实例已缓存，使用缓存；否则查找新实例
                if (audioLinkCachedInstance == null)
                {
                    audioLinkCachedInstance = FindObjectOfType(audioLinkCachedType) as MonoBehaviour;
                }

                if (audioLinkCachedInstance != null)
                {
                    // 缓存反射方法（只缓存一次）
                    if (audioLinkGetDataMethod == null)
                    {
                        audioLinkGetDataMethod = audioLinkCachedType.GetMethod("GetDataAtPixel", new System.Type[] { typeof(Vector2) });
                    }
                    if (audioLinkIsAvailableMethod == null)
                    {
                        audioLinkIsAvailableMethod = audioLinkCachedType.GetMethod("AudioDataIsAvailable",
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    }

                    // 关键修复：启用 audioDataToggle 以允许 GPU Readback
                    // AudioLink 默认 audioDataToggle = false，导致 GetDataAtPixel() 返回空数据
                    var audioDataToggleField = audioLinkCachedType.GetField("audioDataToggle",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (audioDataToggleField != null)
                    {
                        bool currentValue = (bool)audioDataToggleField.GetValue(audioLinkCachedInstance);
                        if (!currentValue)
                        {
                            // 尝试调用 EnableReadback() 方法
                            var enableReadbackMethod = audioLinkCachedType.GetMethod("EnableReadback",
                                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                            if (enableReadbackMethod != null)
                            {
                                enableReadbackMethod.Invoke(audioLinkCachedInstance, null);
                                Debug.Log("[EtherealEnvironment] ✅ 已启用 AudioLink 数据回读 (EnableReadback)");
                            }
                            else
                            {
                                // 直接设置字段
                                audioDataToggleField.SetValue(audioLinkCachedInstance, true);
                                Debug.Log("[EtherealEnvironment] ✅ 已启用 AudioLink 数据回读 (直接设置 audioDataToggle)");
                            }
                        }
                    }

                    // 优化延迟：降低fade参数实现即时响应
                    // fadeLength 控制线性衰减拖尾，fadeExpFalloff 控制指数衰减
                    var fadeLengthField = audioLinkCachedType.GetField("fadeLength",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    var fadeExpFalloffField = audioLinkCachedType.GetField("fadeExpFalloff",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (fadeLengthField != null)
                    {
                        fadeLengthField.SetValue(audioLinkCachedInstance, 0.05f); // 从默认0.25降到0.05，大幅减少拖尾
                    }
                    if (fadeExpFalloffField != null)
                    {
                        fadeExpFalloffField.SetValue(audioLinkCachedInstance, 0.9f); // 更快的指数衰减
                    }
                    Debug.Log("[EtherealEnvironment] ✅ 已优化 AudioLink fade 参数，实现即时响应");

                    // 检查 AudioDataIsAvailable() 方法确认数据可用
                    if (audioLinkIsAvailableMethod != null)
                    {
                        bool dataAvailable = (bool)audioLinkIsAvailableMethod.Invoke(audioLinkCachedInstance, null);
                        if (dataAvailable)
                        {
                            audioLinkAvailable = true;
                            Debug.Log("[EtherealEnvironment] ✅ AudioLink 数据已可用，频谱功能启用");
                        }
                        else
                        {
                            Debug.LogWarning("[EtherealEnvironment] ⚠️ AudioLink 数据回读尚未就绪，可能在下一帧生效");
                            // 仍然标记为可用，下一帧应该能工作
                            audioLinkAvailable = true;
                        }
                    }
                    else
                    {
                        audioLinkAvailable = true;
                        Debug.Log("[EtherealEnvironment] ✅ AudioLink 已检测到，频谱数据可用");
                    }
                    return;
                }
                else
                {
                    // AudioLink包存在但场景中没有AudioLink实例
                    audioLinkCachedInstance = null;
                    Debug.LogWarning("[EtherealEnvironment] ⚠️ AudioLink包已安装但场景中无实例！");
                    Debug.LogWarning("[EtherealEnvironment] 请在GameScene中添加AudioLink预制体：Packages/com.llealloo.audiolink/Runtime/Prefabs/AudioLink.prefab");
                }
            }

            audioLinkAvailable = false;
            audioLinkCachedInstance = null;
            Debug.Log("[EtherealEnvironment] 使用 AudioVisualizationManager 三频段数据作为fallback");
        }

        private void FindRhythmGameManager()
        {
            rhythmGameManager = FindFirstObjectByType<RhythmGameManager>();
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            SetupCamera();
            DetectPhase(scene);
            // 每次加载场景时重新检测AudioLink（因为AudioLink预制体可能只在特定场景存在）
            CheckAudioLinkAvailability();
        }

        void OnActiveSceneChanged(Scene oldScene, Scene newScene)
        {
            DetectPhase(newScene);
            // 场景切换时重新检测AudioLink
            CheckAudioLinkAvailability();
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
            // 辉光贴图（提升分辨率）
            int glowSize = 256;
            glowTexture = new Texture2D(glowSize, glowSize, TextureFormat.RGBA32, false);
            glowTexture.filterMode = FilterMode.Bilinear; // 平滑过滤
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

            // 频谱专用条形贴图（带RGB发光渐变，增加立体感）
            // 使用非正方形贴图：宽度窄，高度大
            int spectrumWidth = 64;
            int spectrumHeight = 256;
            spectrumTexture = new Texture2D(spectrumWidth, spectrumHeight, TextureFormat.RGBA32, false);
            spectrumTexture.filterMode = FilterMode.Bilinear;
            float halfWidth = spectrumWidth * 0.5f;

            for (int y = 0; y < spectrumHeight; y++)
            {
                // 垂直方向：底部到顶部的亮度渐变
                float verticalPos = (float)y / spectrumHeight;
                float verticalBrightness = Mathf.Pow(1f - verticalPos, 2f);

                for (int x = 0; x < spectrumWidth; x++)
                {
                    // 水平方向：中心亮边缘暗，模拟圆柱体截面
                    float horizontalDist = Mathf.Abs(x - halfWidth) / halfWidth;
                    // 核心发光区（高亮度）
                    float coreGlow = Mathf.Pow(1f - horizontalDist, 6f);
                    // 外层辉光（RGB发散）
                    float outerGlow = Mathf.Pow(1f - horizontalDist, 2f) * 0.4f;
                    // 边缘RGB光晕（增强立体感）
                    float edgeGlow = Mathf.Exp(-Mathf.Pow((horizontalDist - 0.6f) * 3f, 2f)) * 0.3f;

                    float alpha = coreGlow + outerGlow + edgeGlow;
                    float coreRatio = coreGlow / (alpha + 0.001f);

                    // RGB发光：中心偏白，边缘偏暖色/冷色（立体感）
                    // 水平位置影响色温：中心白色，左边缘偏暖橙，右边缘偏冷青
                    float hueShift = (x - halfWidth) / halfWidth; // -1到1
                    float r = Mathf.Lerp(0.8f, 1f, coreRatio) + hueShift * 0.15f;  // 右侧偏青（r减少）
                    float g = Mathf.Lerp(0.9f, 1f, coreRatio) - Mathf.Abs(hueShift) * 0.1f;
                    float b = 1f - hueShift * 0.15f;  // 左侧偏橙（b减少）

                    spectrumTexture.SetPixel(x, y, new Color(
                        Mathf.Clamp01(r),
                        Mathf.Clamp01(g),
                        Mathf.Clamp01(b),
                        Mathf.Clamp01(alpha * (0.6f + verticalBrightness * 0.4f))));
                }
            }
            spectrumTexture.Apply();

            // Bokeh 贴图（提升分辨率）
            int bokehSize = 256;
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

            // 镜面地板材质
            Shader mirrorShader = Shader.Find("Standard");
            if (mirrorShader != null)
            {
                mirrorFloorMaterial = new Material(mirrorShader);
                mirrorFloorMaterial.SetFloat("_Mode", 3); // Transparent mode
                mirrorFloorMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mirrorFloorMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mirrorFloorMaterial.SetInt("_ZWrite", 0);
                mirrorFloorMaterial.DisableKeyword("_ALPHATEST_ON");
                mirrorFloorMaterial.EnableKeyword("_ALPHABLEND_ON");
                mirrorFloorMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mirrorFloorMaterial.renderQueue = 3000;

                // 镜面反射效果
                mirrorFloorMaterial.SetFloat("_Metallic", 0.9f);
                mirrorFloorMaterial.SetFloat("_Glossiness", 0.95f);
                mirrorFloorMaterial.SetColor("_Color", new Color(0.7f, 0.75f, 0.8f, mirrorFloorAlpha));
            }
        }

        // ---- 镜面地板 ----
        void CreateMirrorFloor()
        {
            mirrorFloorObj = new GameObject("MirrorFloor");
            mirrorFloorObj.transform.SetParent(transform);
            mirrorFloorObj.transform.localPosition = Vector3.zero;
            mirrorFloorObj.transform.localRotation = Quaternion.identity; // 旋转 0,0,0

            // 创建网格
            MeshFilter mf = mirrorFloorObj.AddComponent<MeshFilter>();
            Mesh mesh = new Mesh();

            // 5m正方形地板（水平放置在地面 Y=0）
            float halfSize = mirrorFloorSize * 0.5f;
            Vector3[] vertices = new Vector3[]
            {
                new Vector3(-halfSize, 0, -halfSize),
                new Vector3(halfSize, 0, -halfSize),
                new Vector3(halfSize, 0, halfSize),
                new Vector3(-halfSize, 0, halfSize)
            };
            int[] triangles = new int[] { 0, 2, 1, 0, 3, 2 };
            Vector2[] uv = new Vector2[]
            {
                new Vector2(0, 0),
                new Vector2(1, 0),
                new Vector2(1, 1),
                new Vector2(0, 1)
            };

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.uv = uv;
            mesh.RecalculateNormals();
            mf.mesh = mesh;

            // 添加渲染器
            MeshRenderer mr = mirrorFloorObj.AddComponent<MeshRenderer>();
            if (mirrorFloorMaterial != null)
            {
                mr.material = mirrorFloorMaterial;
            }
            else
            {
                // 备用材质
                mr.material = new Material(Shader.Find("Sprites/Default"));
                mr.material.color = new Color(0.6f, 0.7f, 0.8f, mirrorFloorAlpha);
            }

            // 确保地板在正确位置接收阴影
            mr.receiveShadows = true;
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
                    new GradientAlphaKey(0.35f, 0.2f),
                    new GradientAlphaKey(0.3f, 0.6f),
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
                    new GradientAlphaKey(0.25f, 0.15f),
                    new GradientAlphaKey(0.2f, 0.85f),
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

        // ---- 两侧频谱粒子层（地板左右两侧引导线，Y-Z平面）----
        void CreateSpectrumBarsLayer()
        {
            // 创建频谱材质
            Shader additiveShader = Shader.Find("Mobile/Particles/Additive")
                ?? Shader.Find("Legacy Shaders/Particles/Additive")
                ?? Shader.Find("Particles/Standard Unlit");

            if (additiveShader != null)
            {
                spectrumBarMaterial = new Material(additiveShader) { enableInstancing = true, renderQueue = 2005 };
                spectrumBarMaterial.SetTexture("_MainTex", spectrumTexture); // 使用高清频谱贴图
            }

            // 频谱粒子位置：往外移动，确保VR下不晃眼睛
            // 左侧：位置(-4.0, 0, 4.0)，Y轴旋转-15°（往内撇5°）
            GameObject leftGO = new GameObject("LayerK_SpectrumBars_Left");
            leftGO.transform.SetParent(transform);
            leftGO.transform.localPosition = new Vector3(-3.5f, 0f, 3.5f);
            leftGO.transform.localRotation = Quaternion.Euler(0f, -15f, 0f);
            leftGO.transform.localScale = new Vector3(1f, 1f, 6f);

            spectrumLeftPS = leftGO.AddComponent<ParticleSystem>();
            SetupSpectrumParticleSystem(spectrumLeftPS, true);

            // 右侧：位置(4.0, 0, 4.0)，Y轴旋转15°（往内撇5°）
            GameObject rightGO = new GameObject("LayerK_SpectrumBars_Right");
            rightGO.transform.SetParent(transform);
            rightGO.transform.localPosition = new Vector3(3.5f, 0f, 3.5f);
            rightGO.transform.localRotation = Quaternion.Euler(0f, 15f, 0f);
            rightGO.transform.localScale = new Vector3(1f, 1f, 6f);

            spectrumRightPS = rightGO.AddComponent<ParticleSystem>();
            SetupSpectrumParticleSystem(spectrumRightPS, false);
        }

        private void SetupSpectrumParticleSystem(ParticleSystem ps, bool isLeft)
        {
            var main = ps.main;
            main.maxParticles = spectrumBarCount;
            main.startLifetime = 100f; // 长生命周期
            main.startSpeed = 0f;
            // 固定粒子大小，不做3D拉伸
            main.startSize3D = false;
            main.startSize = spectrumBarWidth;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.playOnAwake = true;
            main.loop = true;
            main.startColor = new Color(1f, 1f, 1f, spectrumBarAlpha);

            // 使用Box形状，粒子沿Z轴分布
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            // 沿Z轴延伸（X=0, Y=高度变化, Z=延伸范围）
            shape.scale = new Vector3(0.01f, 0.01f, 0.01f);
            shape.position = Vector3.zero;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = spectrumBarCount;

            var noise = ps.noise;
            noise.enabled = false;

            var colorOL = ps.colorOverLifetime;
            colorOL.enabled = false;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            if (spectrumBarMaterial != null)
                renderer.material = spectrumBarMaterial;
            else
                renderer.material = particleMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.enableGPUInstancing = true;
        }

        // ---- 两侧频谱粒子更新 ----
        private void UpdateSpectrumBars()
        {
            if (spectrumLeftPS == null || spectrumRightPS == null) return;

            // 使用ParticleSystem.Particle数组更新位置、大小和颜色
            UpdateSpectrumParticles(spectrumLeftPS, true);
            UpdateSpectrumParticles(spectrumRightPS, false);
        }

        private ParticleSystem.Particle[] spectrumParticleBuffer;
        private int spectrumParticleBufferCapacity = 0;

        private void UpdateSpectrumParticles(ParticleSystem ps, bool isLeft)
        {
            int count = ps.particleCount;
            if (count == 0) return;

            EnsureSpectrumParticleBuffer(count);
            count = ps.GetParticles(spectrumParticleBuffer);

            bool modified = false;
            // 128粒子沿Z轴排列，间距略小于宽度确保无间隙
            float barSpacing = 0.06f; // Z轴间距（小于宽度0.08，确保重叠无锯齿）
            float startZ = 0f; // 从当前位置开始

            // Kiai节拍闪烁：复用beatBrightnessPulse
            float flashBoost = 1f;
            if (isKiaiActive && beatBrightnessPulse > 0.1f)
            {
                flashBoost = 1f + beatBrightnessPulse * spectrumKiaiFlashIntensity;
            }

            for (int bar = 0; bar < spectrumBarCount && bar < count; bar++)
            {
                // 每个粒子独立响应自己的频谱值
                float height = spectrumBarHeightsSmoothed[bar] * spectrumBarHeightMax;

                // Kiai增强高度
                float kiaiHeightBoost = isKiaiActive ? 1.3f : 1f;
                height *= kiaiHeightBoost;

                // 粒子位置：均匀间隔分布，Y位置控制频谱高度
                float z = startZ + bar * barSpacing;
                spectrumParticleBuffer[bar].position = new Vector3(0f, height * 0.5f, z);

                // 不修改大小，避免每帧修改导致模糊（大小在Setup时已固定）

                // 颜色：每个粒子独立颜色，基于位置渐变
                float huePosition = (float)bar / spectrumBarCount;
                Color color = SpectrumPositionToColor(huePosition, spectrumBarHeightsSmoothed[bar]);
                color.r = Mathf.Min(1f, color.r * 1.3f);
                color.g = Mathf.Min(1f, color.g * 1.3f);
                color.b = Mathf.Min(1f, color.b * 1.3f);
                if (flashBoost > 1f)
                {
                    color.r = Mathf.Min(1f, color.r * flashBoost);
                    color.g = Mathf.Min(1f, color.g * flashBoost);
                    color.b = Mathf.Min(1f, color.b * flashBoost);
                }
                color.a = spectrumBarAlpha * (0.7f + spectrumBarHeightsSmoothed[bar] * 0.3f) * flashBoost;
                spectrumParticleBuffer[bar].startColor = color;

                modified = true;
            }

            if (modified)
            {
                ps.SetParticles(spectrumParticleBuffer, count);
            }
        }

        private void EnsureSpectrumParticleBuffer(int count)
        {
            if (spectrumParticleBuffer == null || spectrumParticleBufferCapacity < count)
            {
                spectrumParticleBufferCapacity = Mathf.Max(count + 32, 256);
                spectrumParticleBuffer = new ParticleSystem.Particle[spectrumParticleBufferCapacity];
            }
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
            UpdateSpectrumFromAudioLink();  // 频谱数据更新
            UpdateSpectrumBars();           // 两侧频谱粒子更新
            UpdateAudioResponse();          // 已包含晶体频谱响应
            UpdateBackgroundColor();
            ReduceNearbyParticles();        // 降低近距离粒子密度
        }

        // =========================================================
        // 近距离粒子优化（0.5m内降低密度50%）
        // =========================================================

        private ParticleSystem.Particle[] nearbyParticleBuffer;
        private int nearbyParticleBufferCapacity = 0;

        /// <summary>
        /// 降低0.5m内粒子的大小和透明度，确保不影响读谱
        /// </summary>
        private void ReduceNearbyParticles()
        {
            Camera mainCam = Camera.main;
            if (mainCam == null) return;

            Vector3 playerPos = mainCam.transform.position;
            float nearbyRadius = 0.5f;
            float reduceFactor = 0.5f; // 降低50%

            // 处理星尘粒子
            ReduceParticlesInRange(stardustPS, playerPos, nearbyRadius, reduceFactor);

            // 处理光斑粒子
            ReduceParticlesInRange(bokehPS, playerPos, nearbyRadius, reduceFactor);

            // 处理流星粒子
            ReduceParticlesInRange(fallingStarPS, playerPos, nearbyRadius, reduceFactor);

            // 处理晶体粒子
            ReduceParticlesInRange(crystalsPS, playerPos, nearbyRadius, reduceFactor);
        }

        private void ReduceParticlesInRange(ParticleSystem ps, Vector3 center, float radius, float reduceFactor)
        {
            if (ps == null) return;

            int count = ps.particleCount;
            if (count == 0) return;

            EnsureNearbyParticleBuffer(count);
            count = ps.GetParticles(nearbyParticleBuffer);

            bool modified = false;
            float radiusSqr = radius * radius;

            for (int i = 0; i < count; i++)
            {
                Vector3 particlePos = ps.main.simulationSpace == ParticleSystemSimulationSpace.World
                    ? nearbyParticleBuffer[i].position
                    : ps.transform.TransformPoint(nearbyParticleBuffer[i].position);

                float distSqr = (particlePos - center).sqrMagnitude;

                if (distSqr < radiusSqr)
                {
                    // 在近距离范围内，降低大小和透明度
                    float dist = Mathf.Sqrt(distSqr);
                    float t = dist / radius; // 0到1，0表示在中心
                    float factor = Mathf.Lerp(reduceFactor, 1f, t); // 越近越小

                    nearbyParticleBuffer[i].startSize *= factor;
                    Color color = nearbyParticleBuffer[i].startColor;
                    color.a *= factor;
                    nearbyParticleBuffer[i].startColor = color;
                    modified = true;
                }
            }

            if (modified)
            {
                ps.SetParticles(nearbyParticleBuffer, count);
            }
        }

        private void EnsureNearbyParticleBuffer(int count)
        {
            if (nearbyParticleBuffer == null || nearbyParticleBufferCapacity < count)
            {
                nearbyParticleBufferCapacity = Mathf.Max(count + 32, 256);
                nearbyParticleBuffer = new ParticleSystem.Particle[nearbyParticleBufferCapacity];
            }
        }

        // =========================================================
        // AudioLink 频谱数据获取
        // =========================================================

        /// <summary>
        /// 对频谱数据应用增益和非线性放大，使视觉效果更明显
        /// </summary>
        private float ApplySpectrumGain(float rawValue)
        {
            // 低于阈值时返回0，避免噪音显示
            if (rawValue < spectrumMinThreshold)
                return 0f;

            // 应用增益
            float amplified = rawValue * spectrumGain;

            // 非线性放大（幂函数）- spectrumPower < 1 会增强低值部分
            amplified = Mathf.Pow(Mathf.Clamp01(amplified), 1f / spectrumPower);

            return Mathf.Clamp01(amplified);
        }

        /// <summary>
        /// 对频谱数据应用增益，并根据位置增强低频
        /// </summary>
        private float ApplySpectrumGainWithBassBoost(float rawValue, float position)
        {
            float baseValue = ApplySpectrumGain(rawValue);

            // 低频区域额外增强
            if (position < spectrumBassRatio)
            {
                float bassFactor = 1f + spectrumBassBoost * (1f - position / spectrumBassRatio);
                baseValue *= bassFactor;
            }

            return Mathf.Clamp01(baseValue);
        }

        /// <summary>
        /// 从AudioLink获取完整频谱数据，压缩为8频段
        /// 如果AudioLink不可用，则从AudioVisualizationManager获取三频段并扩展
        /// </summary>
        private void UpdateSpectrumFromAudioLink()
        {
            float dt = Time.deltaTime;
            // 即时响应模式：使用更高的平滑因子（接近1），几乎无延迟
            float smoothFactor = instantResponseMode ? instantSmoothFactor : (1f - Mathf.Exp(-spectrumSmoothSpeed * dt));

            if (audioLinkAvailable && audioLinkCachedInstance != null)
            {
                // 使用缓存的反射信息
                try
                {
                    // 先检查数据是否可用（AudioDataIsAvailable）
                    if (audioLinkIsAvailableMethod != null)
                    {
                        bool dataAvailable = (bool)audioLinkIsAvailableMethod.Invoke(audioLinkCachedInstance, null);
                        if (!dataAvailable)
                        {
                            // 数据不可用，可能刚启用 readback，等待一帧
                            // 不立即回退，保持 audioLinkAvailable 标志
                            return;
                        }
                    }

                    if (audioLinkGetDataMethod != null)
                    {
                        // ALPASS_DFT 位置 = uint2(0,4)，128x2 频谱数据
                        // 压缩为8频段：每频段取16个bin的平均值
                        for (int band = 0; band < spectrumBands; band++)
                        {
                            int startBin = band * 16;
                            float sum = 0f;
                            int validCount = 0;

                            for (int bin = startBin; bin < startBin + 16 && bin < 128; bin++)
                            {
                                // DFT数据在 y=4 和 y=5 两行
                                var result = audioLinkGetDataMethod.Invoke(audioLinkCachedInstance, new object[] { new Vector2(bin, 4) });
                                if (result is Vector4 v4)
                                {
                                    sum += v4.x;
                                    validCount++;
                                }
                            }

                            spectrumBandValues[band] = validCount > 0 ? sum / validCount : 0f;
                            // 应用增益和非线性放大
                            spectrumBandValues[band] = ApplySpectrumGain(spectrumBandValues[band]);
                            spectrumBandSmoothed[band] = Mathf.Lerp(spectrumBandSmoothed[band], spectrumBandValues[band], smoothFactor);
                        }

                        // 同时更新频谱高度（连续曲线映射，无区域断层）
                        for (int bar = 0; bar < spectrumBarCount; bar++)
                        {
                            float linearPosition = (float)bar / spectrumBarCount;

                            // 使用连续曲线函数映射粒子位置到bin位置
                            // 曲线设计：低频区域占5%粒子但映射到16bins，高频区域占50%粒子映射到约37bins
                            // 这样整体是连续曲线，没有边界跳跃
                            float binFloat;

                            // 分段连续曲线，每个段内部是平滑的，段与段之间值连续
                            // 关键：确保边界值相等（如 position=0.05 时，bin=15.0 和 bin=16.0 要连续）
                            if (linearPosition < 0.05f)
                            {
                                // 低频段：0-5%粒子 → bin 0-15，使用平滑曲线
                                float t = linearPosition / 0.05f;
                                // 使用二次曲线让过渡更自然
                                binFloat = 16f * Mathf.Pow(t, 0.8f); // t=1时bin=16，与下一段起始对齐
                            }
                            else if (linearPosition < 0.175f)
                            {
                                // 中低频段：5%-17.5%粒子 → bin 16-35
                                float t = (linearPosition - 0.05f) / 0.125f;
                                // 使用三次曲线平滑过渡
                                float curveT = t * t * (3f - 2f * t); // smoothstep
                                binFloat = 16f + 20f * curveT; // 结束时bin=36
                            }
                            else if (linearPosition < 0.425f)
                            {
                                // 中频段：17.5%-42.5%粒子 → bin 36-60
                                float t = (linearPosition - 0.175f) / 0.25f;
                                float curveT = t * t * (3f - 2f * t);
                                binFloat = 36f + 25f * curveT; // 结束时bin=61
                            }
                            else if (linearPosition < 0.75f)
                            {
                                // 中高频段：42.5%-75%粒子 → bin 61-90
                                float t = (linearPosition - 0.425f) / 0.325f;
                                float curveT = t * t * (3f - 2f * t);
                                binFloat = 61f + 30f * curveT; // 结束时bin=91
                            }
                            else
                            {
                                // 高频段：75%-100%粒子 → bin 91-127
                                float t = (linearPosition - 0.75f) / 0.25f;
                                float curveT = t * t * (3f - 2f * t);
                                binFloat = 91f + 37f * curveT; // 结束时bin=128（取127）
                            }

                            binFloat = Mathf.Clamp(binFloat, 0f, 127f);

                            // bassPosition 用于低频增强，线性变化
                            float bassPosition = linearPosition; // 0-1线性，后面会用于判断是否在低频区

                            // 多点采样插值：采样3个相邻bin做平滑曲线插值
                            int binCenter = Mathf.Clamp((int)binFloat, 0, 127);
                            int binLeft = Mathf.Max(binCenter - 1, 0);
                            int binRight = Mathf.Min(binCenter + 1, 127);
                            float interpT = binFloat - binCenter;

                            // 获取三个bin的数据
                            var resultLeft = audioLinkGetDataMethod.Invoke(audioLinkCachedInstance, new object[] { new Vector2(binLeft, 4) });
                            var resultCenter = audioLinkGetDataMethod.Invoke(audioLinkCachedInstance, new object[] { new Vector2(binCenter, 4) });
                            var resultRight = audioLinkGetDataMethod.Invoke(audioLinkCachedInstance, new object[] { new Vector2(binRight, 4) });

                            float valueLeft = 0f, valueCenter = 0f, valueRight = 0f;
                            if (resultLeft is Vector4 v4L) valueLeft = v4L.x;
                            if (resultCenter is Vector4 v4C) valueCenter = v4C.x;
                            if (resultRight is Vector4 v4R) valueRight = v4R.x;

                            // 使用二次插值（Catmull-Rom简化版）获得更平滑曲线
                            float rawValue;
                            if (interpT <= 0.5f)
                            {
                                // 左半部分：从center向left插值，但考虑right的影响
                                float t = interpT + 0.5f; // 0-1
                                rawValue = Mathf.Lerp(valueCenter, valueLeft, t);
                                // 加入平滑曲线修正
                                float interpSmooth = 0.5f * (1f - Mathf.Cos(t * Mathf.PI));
                                rawValue = Mathf.Lerp(Mathf.Lerp(valueCenter, valueLeft, t), rawValue, interpSmooth);
                            }
                            else
                            {
                                // 右半部分：从center向right插值
                                float t = interpT - 0.5f; // 0-0.5
                                rawValue = Mathf.Lerp(valueCenter, valueRight, t * 2f);
                                float interpSmooth = 0.5f * (1f - Mathf.Cos(t * 2f * Mathf.PI));
                                rawValue = Mathf.Lerp(Mathf.Lerp(valueCenter, valueRight, t * 2f), rawValue, interpSmooth);
                            }

                            spectrumBarHeights[bar] = ApplySpectrumGainWithBassBoost(rawValue, bassPosition);
                            spectrumBarHeightsSmoothed[bar] = Mathf.Lerp(spectrumBarHeightsSmoothed[bar], spectrumBarHeights[bar], smoothFactor);
                        }
                    }
                }
                catch (System.Exception e)
                {
                    // 反射失败，回退到三频段
                    audioLinkAvailable = false;
                    audioLinkCachedInstance = null;
                    Debug.LogWarning($"[EtherealEnvironment] AudioLink反射失败: {e.Message}");
                }
            }

            // 如果AudioLink不可用，使用AudioVisualizationManager的三频段
            if (!audioLinkAvailable)
            {
                var audioManager = AudioVisualizationManager.Instance;
                if (audioManager != null)
                {
                    // 获取三频段数据，应用增益处理并增强低频
                    float bass = ApplySpectrumGain(audioManager.Bass) * (1f + spectrumBassBoost);
                    float mid = ApplySpectrumGain(audioManager.Mid);
                    float treble = ApplySpectrumGain(audioManager.Treble);

                    // 5区域分配：低频5%，中低频12.5%，中频25%，中高频32.5%，高频25%
                    for (int bar = 0; bar < spectrumBarCount; bar++)
                    {
                        float position = (float)bar / spectrumBarCount;
                        float baseHeight;

                        if (position < 0.05f)
                        {
                            // 低频 5%：纯Bass
                            baseHeight = bass;
                        }
                        else if (position < 0.175f)
                        {
                            // 中低频 12.5%：Bass → Mid 过渡
                            float t = (position - 0.05f) / 0.125f;
                            baseHeight = Mathf.Lerp(bass * 0.95f, bass * 0.7f + mid * 0.3f, t);
                        }
                        else if (position < 0.425f)
                        {
                            // 中频 25%：Mid主导
                            float t = (position - 0.175f) / 0.25f;
                            float midStart = bass * 0.7f + mid * 0.3f;
                            float midEnd = mid * 0.9f + treble * 0.1f;
                            baseHeight = Mathf.Lerp(midStart, midEnd, t);
                        }
                        else if (position < 0.75f)
                        {
                            // 中高频 32.5%：Mid → Treble 过渡
                            float t = (position - 0.425f) / 0.325f;
                            float midHighStart = mid * 0.9f + treble * 0.1f;
                            float midHighEnd = mid * 0.4f + treble * 0.6f;
                            baseHeight = Mathf.Lerp(midHighStart, midHighEnd, t);
                        }
                        else
                        {
                            // 高频 25%：Treble主导
                            float t = (position - 0.75f) / 0.25f;
                            float highStart = mid * 0.4f + treble * 0.6f;
                            float highEnd = treble;
                            baseHeight = Mathf.Lerp(highStart, highEnd, t);
                        }

                        spectrumBarHeightsSmoothed[bar] = Mathf.Clamp01(baseHeight);
                    }
                }
            }
        }
        
        // =========================================================
        // AudioLink HSV to RGB 颜色映射 (简化版)
        // =========================================================

        /// <summary>
        /// 将频段索引映射为颜色 (Bass暖色→Treble亮色)
        /// </summary>
        private Color SpectrumBandToColor(int band, float intensity)
        {
            float hue = band / (float)spectrumBands;
            hue = Mathf.Lerp(0.0f, 0.75f, hue);

            float saturation = 0.8f + intensity * 0.2f;
            float value = 0.6f + intensity * 0.4f;

            float c = value * saturation;
            float x = c * (1 - Mathf.Abs((hue * 6) % 2 - 1));
            float m = value - c;

            float r = 0, g = 0, b = 0;
            int hIndex = Mathf.FloorToInt(hue * 6) % 6;

            switch (hIndex)
            {
                case 0: r = c; g = x; b = 0; break;
                case 1: r = x; g = c; b = 0; break;
                case 2: r = 0; g = c; b = x; break;
                case 3: r = 0; g = x; b = c; break;
                case 4: r = x; g = 0; b = c; break;
                case 5: r = c; g = 0; b = x; break;
            }

            return new Color(r + m, g + m, b + m, spectrumBarAlpha * intensity);
        }

        /// <summary>
        /// 根据粒子位置渐变颜色（低频暖色→高频冷色），带RGB发光和立体感
        /// </summary>
        private Color SpectrumPositionToColor(float position, float intensity)
        {
            // position: 0-1，0为低频(近端)，1为高频(远端)
            // 颜色范围：红橙(0) → 黄绿(0.25) → 青蓝(0.5) → 蓝紫(0.75) → 白(1)
            float hue = Mathf.Lerp(0.0f, 0.85f, position);

            // 发光效果：高强度时颜色更亮、更白（立体感）
            float glowFactor = Mathf.Pow(intensity, 0.5f); // 发光强度
            float saturation = 0.85f - position * 0.15f - glowFactor * 0.3f; // 高强度时降低饱和度（偏白发光）
            saturation = Mathf.Max(0.3f, saturation); // 最低饱和度
            float value = 0.7f + intensity * 0.4f + glowFactor * 0.2f; // 高强度时更亮

            float c = value * saturation;
            float x = c * (1 - Mathf.Abs((hue * 6) % 2 - 1));
            float m = value - c;

            float r = 0, g = 0, b = 0;
            int hIndex = Mathf.FloorToInt(hue * 6) % 6;

            switch (hIndex)
            {
                case 0: r = c; g = x; b = 0; break;
                case 1: r = x; g = c; b = 0; break;
                case 2: r = 0; g = c; b = x; break;
                case 3: r = 0; g = x; b = c; break;
                case 4: r = x; g = 0; b = c; break;
                case 5: r = c; g = 0; b = x; break;
            }

            // 添加RGB发光层：高强度时整体亮度提升
            float glowAdd = glowFactor * 0.3f;
            r = Mathf.Clamp01(r + m + glowAdd);
            g = Mathf.Clamp01(g + m + glowAdd);
            b = Mathf.Clamp01(b + m + glowAdd);

            return new Color(r, g, b, spectrumBarAlpha);
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

        private void UpdateAudioResponse()
        {
            var audioManager = AudioVisualizationManager.Instance;
            if (audioManager == null) return;

            float dt = Time.deltaTime;
            float lerpFactor = 1f - Mathf.Exp(-audioResponseSmooth * dt);

            // 使用8频段数据（如果可用）或三频段数据
            float bass = spectrumBandSmoothed[0] + spectrumBandSmoothed[1]; // Band 0-1: Bass
            float lowMid = spectrumBandSmoothed[2] + spectrumBandSmoothed[3]; // Band 2-3: LowMid
            float highMid = spectrumBandSmoothed[4] + spectrumBandSmoothed[5]; // Band 4-5: HighMid
            float treble = spectrumBandSmoothed[6] + spectrumBandSmoothed[7]; // Band 6-7: Treble

            // 如果AudioLink不可用，使用AudioVisualizationManager的数据
            if (!audioLinkAvailable)
            {
                bass = audioManager.Bass;
                lowMid = audioManager.Mid * 0.8f;
                highMid = audioManager.Mid * 0.6f;
                treble = audioManager.Treble;
            }

            // Kiai增强系数
            float kiaiMult = isKiaiActive ? 1.5f : 1f;

            // Bass → 星尘亮度脉冲 + BPM 节拍脉冲叠加
            float targetBrightness = 0.3f + bass * bassBrightnessGain * kiaiMult;
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

            // Bass + LowMid → 光斑加速
            float targetSpeed = 1f + (bass + lowMid * 0.3f) * 0.5f * kiaiMult;
            targetSpeed += beatBrightnessPulse * 0.3f;
            currentBokehSpeedModifier = Mathf.Lerp(currentBokehSpeedModifier, targetSpeed, lerpFactor);
            var bokehVel = bokehPS.velocityOverLifetime;
            bokehVel.speedModifier = new ParticleSystem.MinMaxCurve(currentBokehSpeedModifier);

            // Treble → 色相偏移（更精细：使用各频段相位不同）
            // Bass频段偏移慢，Treble频段偏移快
            currentHueOffset += (treble * 0.4f + highMid * 0.2f) * trebleHueShift * dt * kiaiMult;
            if (currentHueOffset > 1f) currentHueOffset -= 1f;

            // Mid → 星尘 noise 增强
            var stardustNoise = stardustPS.noise;
            float currentNoise = stardustNoise.strength.constant;
            float targetNoiseStrength = 0.04f + (lowMid + highMid) * 0.08f * kiaiMult;
            stardustNoise.strength = new ParticleSystem.MinMaxCurve(
                Mathf.Lerp(currentNoise, targetNoiseStrength, lerpFactor)
            );

            // Bass → 流星速度脉冲
            if (fallingStarPS != null)
            {
                float fallSpeedBoost = 1f + bass * 0.8f * kiaiMult;
                currentFallingStarSpeedMod = Mathf.Lerp(currentFallingStarSpeedMod, fallSpeedBoost, lerpFactor);
                var fallVel = fallingStarPS.velocityOverLifetime;
                fallVel.speedModifier = new ParticleSystem.MinMaxCurve(currentFallingStarSpeedMod);
            }

            // 晶体层：根据频段分组闪烁（使用8频段数据）
            UpdateCrystalSpectrumResponse();
        }

        // 晶体频谱响应：不同频段的晶体在不同节拍闪烁
        private void UpdateCrystalSpectrumResponse()
        {
            if (crystalsPS == null) return;

            int count = crystalsPS.particleCount;
            if (count == 0) return;

            EnsureCrystalBuffer(count);
            count = crystalsPS.GetParticles(crystalBuffer);
            bool modified = false;

            // Kiai增强
            float kiaiMult = isKiaiActive ? 1.3f : 1f;

            for (int i = 0; i < count; i++)
            {
                // 根据粒子随机种子分配频段
                uint seed = crystalBuffer[i].randomSeed;
                int band = (int)(seed % spectrumBands);

                float bandIntensity = spectrumBandSmoothed[band] * kiaiMult;

                // 节拍脉冲叠加
                if (beatBrightnessPulse > 0.1f)
                {
                    bandIntensity += beatBrightnessPulse * 0.5f;
                }

                // 频段对应的颜色
                Color bandColor = SpectrumBandToColor(band, bandIntensity);
                bandColor.a = 0.35f + bandIntensity * 0.3f;

                crystalBuffer[i].startColor = bandColor;

                // Bass频段的晶体在节拍时放大
                if (band < 2 && beatBrightnessPulse > 0.5f)
                {
                    crystalBuffer[i].startSize = crystalsSize * bassSizeKick;
                    modified = true;
                }

                // 大小自然衰减
                float size = crystalBuffer[i].startSize;
                if (size > crystalsSize * 1.05f)
                {
                    crystalBuffer[i].startSize = Mathf.Max(crystalsSize, size - Time.deltaTime * 0.4f);
                    modified = true;
                }
            }

            if (modified) crystalsPS.SetParticles(crystalBuffer, count);
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
