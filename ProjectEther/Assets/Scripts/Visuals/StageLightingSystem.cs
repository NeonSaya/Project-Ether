using UnityEngine;

namespace OsuVR
{
    /// <summary>
    /// 舞台灯系统（与 EtherealEnvironment 解耦）。
    /// 只负责"舞台建筑 + 灯光"这一层：暗色平台、背墙、墙面灯条、四角立柱，
    /// 灯条合并成极少数网格批量绘制（参考 Beat Saber 的灯管批量填充）。
    /// 节拍/Kiai 由 EtherealEnvironment 以只读属性提供，本系统只读取，不反向影响环境。
    /// </summary>
    /// 说明：本系统复用 EtherealEnvironment 的节拍/阶段色（只读），不重复实现。
    [DefaultExecutionOrder(200)]
    public class StageLightingSystem : MonoBehaviour
    {
        public static StageLightingSystem Instance { get; private set; }

        /// <summary>全局舞台风格。BuiltinStage 为照 Beat Saber 做的极简舞台（平台 + 背墙 + 灯条），
        /// 其余三个为可插拔风格变体，复用同一套程序化几何与辉光材质。</summary>
        /// <summary>灯光模式。参考 Beat Saber 的 MenuLightsPreset / LightColorGroupEffect / OneTimeLightColorEffect 那一族：
        /// 常亮、呼吸、整场脉冲、左右交替、以及 Kiai 才爆发的两段式。</summary>
        public enum LightingPattern
        {
            Steady,
            Breathe,
            Pulse,
            Alternate,
            KiaiBurst,
            Reactive,   // 游戏内默认：AudioLink 频谱 + BPM 节拍 + Kiai 三层叠加
        }

        public enum StageStyle
        {
            BuiltinStage,
            NeonPillars,
            LaserFan,
            ClubGrid,
        }

        [Header("开关")]
        public bool enableStage = true;

        [Header("灯光模式")]
        public LightingPattern pattern = LightingPattern.Reactive;

        [Header("风格")]
        public StageStyle style = StageStyle.ClubGrid;

        [Header("舞台尺寸")]
        [Tooltip("远处灯环的分段数")]
        [Range(0, 32)] public int stripCount = 10;
        [Tooltip("左右暗色建筑体块的偏移距离")]
        public float platformRadius = 18f;
        [Tooltip("远处灯环半径（BS 里在 150m 外）")]
        public float wallRadius = 55f;
        [Tooltip("背后灯柱高度（BS 的 BoxLight 是 50m）")]
        public float wallHeight = 45f;
        public float lightWidth = 0.16f;

        [Header("节拍")]
        [Tooltip("没有外部推送时按这个 BPM 自己跑脉冲")]
        public float bpm = 128f;
        [Tooltip("脉冲衰减时间常数，越小越干脆")]
        public float pulseDecay = 0.22f;
        [Tooltip("勾上后只认 SetBeat() 推来的值（游戏侧驱动）")]
        public bool externalBeat = false;

        [Header("融合")]
        [Tooltip("颜色跟随 EtherealEnvironment 当前的背景色（只读它的公有字段，不改它）")]
        public bool followEnvironmentColors = true;
        [Tooltip("把背景色提亮成灯光色的倍率")]
        public float environmentColorBoost = 2.4f;

        [Header("模式")]
        [Tooltip("菜单/结算：柔和常亮，不做节拍闪")]
        public bool steadyOutsideGameplay = true;
        [Tooltip("常亮时的亮度")]
        [Range(0f, 2f)] public float steadyIntensity = 0.55f;

        [Header("游玩响应（AudioLink / 节拍）")]
        [Tooltip("游玩时按 AudioLink 频段驱动灯光")]
        public bool audioLinkReactive = true;
        [Range(0f, 3f)] public float spectrumResponse = 1.4f;
        [Tooltip("瞬态增益：声音突然变响时额外提亮，让画面更跟手")]
        [Range(0f, 4f)] public float spectrumTransient = 2.2f;
        [Tooltip("低/中/高频段索引（EtherealEnvironment 有 8 段）")]
        public int lowBand = 0;
        public int midBand = 3;
        public int highBand = 5;
        [Range(0f, 1f)] public float beatResponse = 0.35f;
        [Range(1f, 2f)] public float kiaiBoost = 1.5f;

        [Header("osu 手感反馈（很小）")]
        [Tooltip("连击变化时给灯柱/车道线一点点亮，不进入读谱区")]
        [Range(0f, 1f)] public float hitFlashGain = 0.45f;
        public float hitFlashDecay = 7f;

        [Header("灯光事件（对应 Beat Saber 的 On/Off/Flash/Fade/Transition/Strobe）")]
        [Tooltip("颜色切换的平滑速度，越小越像 Fade 事件")]
        [Range(0.5f, 12f)] public float colorFadeSpeed = 3f;
        [Tooltip("Kiai 段的频闪频率（Hz），0 = 不闪")]
        [Range(0f, 12f)] public float strobeHz = 3.5f;
        [Tooltip("频闪深度 0~1")]
        [Range(0f, 1f)] public float strobeDepth = 0.22f;

        [Header("灯光")]
        [ColorUsage(false, true)] public Color colorA = new Color(0.18f, 0.85f, 1.15f);
        [ColorUsage(false, true)] public Color colorB = new Color(1.05f, 0.28f, 0.78f);
        [Range(0f, 2f)] public float intensity = 0.5f;

        EnvironmentLightRig _rig;
        GameObject _styleRoot;
        bool _built;
        float _externalBeat;
        bool _externalKiai;
        float _hitFlash;
        int _lastCombo = -1;
        long _lastScore = -1;
        float _spectrumLevel;
        float _lowLevel;
        float _prevSpectrum;
        float _specTransient;
        Color _fadeA = Color.white;
        Color _fadeB = Color.white;
        bool _fadeInit;

        /// <summary>由游戏侧推节拍（0~1），配合 externalBeat 使用。</summary>
        public void SetBeat(float value)
        {
            _externalBeat = Mathf.Clamp01(value);
        }

        /// <summary>由游戏侧推 Kiai 状态。</summary>
        public void SetKiai(bool active)
        {
            _externalKiai = active;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void AutoInitialize()
        {
            if (Instance != null)
                return;

            GameObject go = new GameObject("[StageLighting]");
            go.AddComponent<StageLightingSystem>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            BuildRig();
        }

        void OnDestroy()
        {
            if (_rig != null)
            {
                _rig.Dispose();
                _rig = null;
            }

            if (_styleRoot != null)
            {
                Destroy(_styleRoot);
                _styleRoot = null;
            }

            _built = false;

            if (Instance == this)
                Instance = null;
        }

        /// <summary>运行时切换舞台风格（会重建几何）。</summary>
        public void SetStyle(StageStyle newStyle)
        {
            style = newStyle;
            Rebuild();
        }

        /// <summary>按当前参数重建舞台。</summary>
        public void Rebuild()
        {
            if (_rig != null)
            {
                _rig.Dispose();
                _rig = null;
            }
            if (_styleRoot != null)
            {
                Destroy(_styleRoot);
                _styleRoot = null;
            }
            _built = false;
            BuildRig();
        }

        void BuildRig()
        {
            if (!enableStage)
                return;

            if (_rig == null)
                _rig = new EnvironmentLightRig();

            if (style == StageStyle.BuiltinStage)
            {
                _rig.Build(transform, stripCount, platformRadius, wallHeight, wallRadius, lightWidth, colorA, colorB);
                _built = true;
                return;
            }

            // 可插拔风格：一个节拍驱动器 + 一个变体，几何与材质复用同一套工具
            GameObject go = new GameObject("Style_" + style);
            go.transform.SetParent(transform, false);
            _styleRoot = go;

            StageBeatDriver driver = go.AddComponent<StageBeatDriver>();
            driver.useEnvironmentBeat = true;
            driver.animateInEditor = false;

            StageVariantBase variant = null;
            switch (style)
            {
                case StageStyle.NeonPillars:
                    variant = go.AddComponent<NeonPillarStage>();
                    break;
                case StageStyle.LaserFan:
                    variant = go.AddComponent<LaserFanStage>();
                    break;
                case StageStyle.ClubGrid:
                    variant = go.AddComponent<ClubGridStage>();
                    break;
            }

            if (variant != null)
            {
                variant.glowScale = Mathf.Max(0.5f, intensity * 1.5f);
                variant.showPlatform = false;
                variant.showHorizonGlow = false;
            }

            _built = true;
        }

        void Update()
        {
            if (!_built)
            {
                if (enableStage)
                    BuildRig();
                return;
            }

            if (style != StageStyle.BuiltinStage)
                return;   // 可插拔风格自己读节拍，不需要这里再驱动

            float beat;
            bool kiai;

            EtherealEnvironment env = EtherealEnvironment.Instance;

            if (externalBeat)
            {
                beat = _externalBeat;
                kiai = _externalKiai;
            }
            else if (env != null)
            {
                // 复用环境系统已经在跑的节拍与 Kiai，两边永远同步
                beat = Mathf.Clamp01(env.BeatPulse);
                kiai = env.IsKiaiActive;
            }
            else
            {
                // 自己按 BPM 跑一个节拍包络：不依赖任何环境系统
                float secondsPerBeat = 60f / Mathf.Max(1f, bpm);
                float now = Time.realtimeSinceStartup;
                long index = (long)(now / secondsPerBeat);
                float sinceBeat = now - index * secondsPerBeat;
                beat = Mathf.Exp(-sinceBeat / Mathf.Max(0.01f, pulseDecay));
                kiai = false;
            }

            // ---- 分模式：菜单/结算柔和常亮，游戏内才做响应 ----
            bool inGameplay = env != null && env.CurrentPhase == EtherealEnvironment.GamePhase.Playing;
            float level = intensity;
            bool useKiai = false;

            if (steadyOutsideGameplay && !inGameplay)
            {
                level = steadyIntensity;
                beat = 0f;
                kiai = false;
                _hitFlash = 0f;
            }
            else
            {
                float spectrum = 0f;
                if (audioLinkReactive && env != null)
                {
                    float low = env.GetSpectrumBand(lowBand);
                    float mid = env.GetSpectrumBand(midBand);
                    float high = env.GetSpectrumBand(highBand);
                    _lowLevel = low;
                    spectrum = (low * 0.55f + mid * 0.30f + high * 0.15f) * spectrumResponse;

                    // 瞬态：谱值往上跳的瞬间额外加一点，画面更"跟手"
                    float rise = Mathf.Max(0f, spectrum - _prevSpectrum);
                    _specTransient = rise * spectrumTransient;
                    _prevSpectrum = spectrum;
                }

                _spectrumLevel = Mathf.Clamp(spectrum, 0f, 2f);
                level = intensity * (0.75f + spectrum) + beat * beatResponse;
                useKiai = kiai;
                if (kiai)
                    level *= kiaiBoost;

                // 连击变化 → 一次很小的闪，只作用于灯柱与车道线
                int combo = 0;
                long score = 0;
                OsuVR.ScoreManager scoreManager = UnityEngine.Object.FindObjectOfType<OsuVR.ScoreManager>();
                if (scoreManager != null)
                {
                    combo = scoreManager.GetCurrentCombo();
                    score = scoreManager.GetCurrentScore();
                }

                bool hit = _lastScore >= 0 && score > _lastScore;
                bool miss = _lastCombo > 0 && combo < _lastCombo && !hit;

                if (hit)
                    _hitFlash = 1f;          // 命中：一次小闪
                else if (miss)
                    _hitFlash = 0.55f;       // Miss：更淡

                _lastScore = score;
                _lastCombo = combo;
            }

            _hitFlash = Mathf.Max(0f, _hitFlash - Time.unscaledDeltaTime * Mathf.Max(0.5f, hitFlashDecay));

            // ---- 按模式算出四路亮度 ----
            float t = Time.realtimeSinceStartup;
            long beatIndex = (long)(t * Mathf.Max(1f, bpm) / 60f);
            bool oddBeat = beatIndex % 2 == 1;
            float baseLevel = Mathf.Max(0f, level);
            float breathe = 0.85f + Mathf.Sin(t * 0.55f) * 0.15f;

            float lightLevel;
            float haloLevel;
            float groupALevel;
            float groupBLevel;

            switch (pattern)
            {
                case LightingPattern.Steady:
                    lightLevel = baseLevel;
                    haloLevel = baseLevel * 0.12f;
                    groupALevel = baseLevel * 0.7f;
                    groupBLevel = baseLevel * 0.7f;
                    break;

                case LightingPattern.Breathe:
                    lightLevel = baseLevel * breathe;
                    haloLevel = baseLevel * 0.12f * breathe;
                    groupALevel = baseLevel * 0.7f * breathe;
                    groupBLevel = baseLevel * 0.7f * breathe;
                    break;

                case LightingPattern.Pulse:
                    lightLevel = baseLevel * (1f + beat * 0.5f);
                    haloLevel = baseLevel * (0.12f + beat * 0.06f);
                    groupALevel = baseLevel * (0.7f + beat * 0.6f);
                    groupBLevel = baseLevel * (0.7f + beat * 0.6f);
                    break;

                case LightingPattern.Alternate:
                    lightLevel = baseLevel * (1f + beat * 0.25f);
                    haloLevel = baseLevel * 0.12f;
                    groupALevel = baseLevel * (0.5f + (oddBeat ? beat * 0.9f : 0f));
                    groupBLevel = baseLevel * (0.5f + (oddBeat ? 0f : beat * 0.9f));
                    break;

                case LightingPattern.Reactive:
                {
                    // AudioLink 频谱打底（画面随低频起伏），BPM 只做轻微推波，Kiai 整体抬升
                    float spec = _spectrumLevel + _specTransient;   // 频谱 + 瞬态：AudioLink 是主驱动
                    float specSlow = spec * 0.75f + _lowLevel * 0.45f;
                    float kiaiLift = (kiai || useKiai) ? 1.5f : 1f;

                    // 三重叠加：AudioLink(主) + BPM(轻推) + 判定(在下方 judgeBoost 里加)
                    lightLevel = baseLevel * (0.46f + specSlow * 0.95f + beat * 0.05f) * kiaiLift;
                    haloLevel = baseLevel * (0.05f + specSlow * 0.16f) * kiaiLift;
                    groupALevel = baseLevel * (0.34f + specSlow * 1.0f + (oddBeat ? beat * 0.06f : 0f)) * kiaiLift;
                    groupBLevel = baseLevel * (0.34f + specSlow * 1.0f + (oddBeat ? 0f : beat * 0.06f)) * kiaiLift;
                    break;
                }

                default: // KiaiBurst
                    bool burst = kiai || useKiai;
                    lightLevel = baseLevel * (burst ? 1.5f : 0.85f);
                    haloLevel = baseLevel * (burst ? 0.2f : 0.1f);
                    groupALevel = baseLevel * (burst ? 1.4f + beat * 0.5f : 0.55f);
                    groupBLevel = baseLevel * (burst ? 1.4f + beat * 0.5f : 0.55f);
                    break;
            }

            // Strobe 事件：只在 Kiai 段生效，幅度很小
            if (kiai && strobeHz > 0.01f && strobeDepth > 0.001f)
            {
                float strobe = 1f + Mathf.Sin(t * strobeHz * Mathf.PI * 2f) * strobeDepth;
                lightLevel *= strobe;
                groupALevel *= strobe;
                groupBLevel *= strobe;
            }

            // 命中反馈只加在灯柱/车道线与两组灯上，幅度很小
            // BPM 闪 + 判定闪叠加（判定的权重更低，整体不刺眼）
            float judgeBoost = _hitFlash * hitFlashGain;
            lightLevel += judgeBoost * (0.10f + beat * 0.04f);
            groupALevel += judgeBoost * (0.07f + beat * 0.03f);
            groupBLevel += judgeBoost * (0.07f + beat * 0.03f);

            Color useA = colorA;
            Color useB = colorB;
            if (followEnvironmentColors)
            {
                if (env != null)
                {
                    // 直接取环境当前阶段的背景色做灯色，保证同一套色相
                    string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                    Color bg = env.menuBackgroundColor;
                    if (sceneName.Contains("Game"))
                        bg = env.gameBackgroundColor;
                    else if (sceneName.Contains("Result"))
                        bg = env.resultBackgroundColor;

                    useA = Color.Lerp(bg * environmentColorBoost, colorA, 0.45f);
                    useB = Color.Lerp(bg * environmentColorBoost, colorB, 0.65f);
                }
            }

            // Fade 事件：颜色平滑过渡（第一帧直接对齐，避免从白色渐变）
            if (!_fadeInit)
            {
                _fadeA = useA;
                _fadeB = useB;
                _fadeInit = true;
            }
            else
            {
                float k = 1f - Mathf.Exp(-colorFadeSpeed * Time.unscaledDeltaTime);
                _fadeA = Color.Lerp(_fadeA, useA, k);
                _fadeB = Color.Lerp(_fadeB, useB, k);
            }

            _rig.Apply(lightLevel, haloLevel, groupALevel, groupBLevel, _fadeA, _fadeB);
        }
    }
}
