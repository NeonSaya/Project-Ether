using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace OsuVR
{
    /// <summary>
    /// 纯代码驱动的空灵环境粒子系统
    /// 100% 免配置，挂载即用
    ///
    /// 节拍驱动（精准 BPM）：
    /// - 从 Beatmap.ControlPoints.Timing 读取红线，提取 MsPerBeat → BPM
    /// - 预计算下一拍时间点，在拍点精准触发闪烁/脉冲
    /// - 支持变速拍（红线切换时自动重新同步）
    ///
    /// 音频律动映射：
    /// - Bass (低音) → noise.strength + size脉冲 + 亮度脉冲 → 鼓点呼吸感
    /// - Mid  (中频) → 旋转速度 → 律动感
    /// - Treble (高频) → 色相偏移 → RGB LED 小灯效果
    ///
    /// Kiai 段效果（从 TimingPoints 解析）：
    /// - 粒子形成向上飞升的星流
    /// - 亮度增强 + 节拍同步脉冲闪烁
    /// - 湍流切换为螺旋上升
    /// - 发射率增强 + 周期性 burst
    /// - 颜色渐变为暖色系
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public class CodeDrivenAmbientParticles : MonoBehaviour
    {
        // =========================================================
        // 配置参数（可在 Inspector 微调，但已有合理默认值）
        // =========================================================

        [Header("粒子基础参数")]
        [Tooltip("最大粒子数量")]
        [SerializeField] private int maxParticles = 12000;

        [Tooltip("发射速率")]
        [SerializeField] private int emissionRate = 120;

        [Tooltip("外层包裹半径（越大粒子场越广）")]
        [SerializeField] private float outerRadius = 50f;

        [Tooltip("内层排除半径（避免干扰读谱）")]
        [SerializeField] private float innerRadius = 4.0f;

        [Tooltip("内层区域最大粒子数（限制干扰）")]
        [SerializeField] private int innerMaxParticles = 1500;

        [Tooltip("基础粒子大小")]
        [SerializeField] private float baseParticleSize = 0.4f;

        [Header("音频响应参数")]
        [Tooltip("Bass 对湍流的最大影响值")]
        [SerializeField] private float maxNoiseStrength = 1.2f;

        [Tooltip("基础湍流强度")]
        [SerializeField] private float baseNoiseStrength = 0.2f;

        [Tooltip("Bass 对亮度脉冲的最大乘数")]
        [SerializeField] private float maxBassBrightness = 2.0f;

        [Tooltip("Bass 对 Size 脉冲的最大乘数")]
        [SerializeField] private float maxBassSizePulse = 0.4f;

        [Tooltip("Bass 脉冲速度")]
        [SerializeField] private float bassPulseSpeed = 2f;

        [Tooltip("Treble 对色相偏移速度")]
        [SerializeField] private float trebleHueSpeed = 0.4f;

        [Tooltip("Mid 对旋转加成")]
        [SerializeField] private float maxMidRotationSpeed = 0.5f;

        [Tooltip("响应平滑速度")]
        [SerializeField] private float responseSmoothSpeed = 12f;

        [Header("节拍驱动参数")]
        [Tooltip("节拍闪烁亮度峰值")]
        [SerializeField] private float beatFlashPeak = 3.5f;

        [Tooltip("节拍 size 脉冲峰值")]
        [SerializeField] private float beatSizePeak = 1.8f;

        [Tooltip("节拍闪烁衰减速度")]
        [SerializeField] private float beatFlashDecay = 8f;

        [Tooltip("节拍 size 脉冲衰减速度")]
        [SerializeField] private float beatSizeDecay = 6f;

        [Tooltip("节拍提前量（秒），让视觉提前于音频一点到达")]
        [SerializeField] private float beatAnticipation = 0.02f;

        [Tooltip("无谱面时的默认 BPM")]
        [SerializeField] private float fallbackBPM = 120f;

        [Header("Kiai 段参数")]
        [Tooltip("Kiai 期间粒子上升速度")]
        [SerializeField] private float kiaiRiseSpeed = 3.0f;

        [Tooltip("Kiai 期间 Mid 对上升速度的加成")]
        [SerializeField] private float kiaiMidSpeedBonus = 1.2f;

        [Tooltip("Kiai 亮度增强倍数")]
        [SerializeField] private float kiaiBrightnessMultiplier = 1.5f;

        [Tooltip("Kiai 期间螺旋角速度")]
        [SerializeField] private float kiaiAngularVelocity = 0.8f;

        [Tooltip("Kiai burst 间隔（秒）")]
        [SerializeField] private float kiaiBurstInterval = 0.5f;

        [Tooltip("Kiai burst 粒子数")]
        [SerializeField] private int kiaiBurstCount = 30;

        [Tooltip("Kiai 节拍同步闪烁强度倍率")]
        [SerializeField] private float kiaiBeatFlashMultiplier = 1.8f;

        [Tooltip("Kiai 节拍 color burst 大小")]
        [SerializeField] private int kiaiBeatBurstCount = 50;

        // =========================================================
        // 内部状态
        // =========================================================

        private ParticleSystem ps;
        private ParticleSystem.MainModule mainModule;
        private ParticleSystem.NoiseModule noiseModule;
        private ParticleSystem.EmissionModule emissionModule;
        private ParticleSystem.ColorOverLifetimeModule colorOverLifetimeModule;
        private ParticleSystem.TriggerModule triggerModule;
        private ParticleSystem.SizeOverLifetimeModule sizeOverLifetimeModule;
        private ParticleSystem.VelocityOverLifetimeModule velocityModule;
        private ParticleSystem.RotationOverLifetimeModule rotationModule;
        private ParticleSystem.Particle[] particleBuffer;
        private int bufferCapacity = 0;
        private SphereCollider triggerCollider;

        private MaterialPropertyBlock mpb;
        private ParticleSystemRenderer psRenderer;
        private Texture2D glowTexture;

        private float currentNoiseStrength;
        private float currentBrightness = 1f;
        private float currentHueOffset = 0f;
        private float currentSizeMultiplier = 1f;
        private float currentRotationSpeed = 0f;
        private float bassPhase = 0f;
        private float kiaiBurstTimer = 0f;

        private bool isKiaiActive = false;
        private bool wasKiaiActive = false;
        private RhythmGameManager rhythmGameManager;

        // 缓存 Gradient 避免 GC
        private Gradient cachedFadeGradient;
        private GradientAlphaKey[] cachedAlphaKeys;
        private GradientColorKey[] cachedColorKeys;

        // 缓存当前 velocity y 值，用于 Lerp
        private float currentVelocityY = 0.1f;

        // --- BPM 精准节拍驱动 ---
        private double nextBeatTimeMs = -1;         // 下一拍的音乐时间（毫秒）
        private double currentMsPerBeat = 500;       // 当前每拍毫秒数（默认 120BPM）
        private int currentTimingPointIndex = -1;    // 当前红线索引，用于检测 BPM 变化
        private float beatFlashIntensity = 0f;       // 当前闪烁强度
        private float beatSizePulseIntensity = 0f;   // 当前 size 脉冲强度
        private bool isBeatFrame = false;            // 本帧是否检测到节拍
        private Beatmap cachedBeatmap;               // 缓存当前谱面引用

        // =========================================================
        // 生命周期
        // =========================================================

        void Awake()
        {
            ps = GetComponent<ParticleSystem>();
            if (ps == null)
            {
                Debug.LogError("[CodeDrivenAmbientParticles] ParticleSystem 组件缺失！");
                enabled = false;
                return;
            }

            InitializeParticleSystem();
            InitializeGradientCache();
            InitializeMaterialPropertyBlock();
        }

        void Start()
        {
            ValidateAudioManager();
            SetupTriggerCollider();
            FindRhythmGameManager();
        }

        private void FindRhythmGameManager()
        {
            rhythmGameManager = FindFirstObjectByType<RhythmGameManager>();
            if (rhythmGameManager == null)
            {
                Debug.LogWarning("[CodeDrivenAmbientParticles] RhythmGameManager 未找到，Kiai/节拍检测将禁用");
            }
        }

        private void SetupTriggerCollider()
        {
            triggerCollider = gameObject.AddComponent<SphereCollider>();
            triggerCollider.isTrigger = true;
            triggerCollider.radius = innerRadius;
            triggerCollider.center = Vector3.zero;

            triggerModule.SetCollider(0, triggerCollider);
        }

        void Update()
        {
            UpdateKiaiState();
            UpdateBeatTiming();
            UpdateAudioReactivity();
            UpdateBeatFlash();
            UpdateParticleColors();
            LimitInnerParticles();
        }

        // =========================================================
        // BPM 精准节拍驱动
        // =========================================================

        /// <summary>
        /// 核心逻辑：
        /// 1. 从 Beatmap.ControlPoints.Timing 获取当前红线
        /// 2. 用 MsPerBeat 计算拍间距
        /// 3. 预算下一拍时间点 nextBeatTimeMs
        /// 4. 当 currentMusicTimeMs >= nextBeatTimeMs 时触发节拍
        /// 5. 推进到下下一拍
        /// </summary>
        private void UpdateBeatTiming()
        {
            isBeatFrame = false;

            if (rhythmGameManager == null || !rhythmGameManager.isPlaying)
            {
                // 非游玩状态：用 AudioVisualizationManager 的 Bass 做粗略检测
                DetectFallbackBeat();
                return;
            }

            double currentTimeMs = rhythmGameManager.currentMusicTimeMs;
            Beatmap beatmap = rhythmGameManager.GetCurrentBeatmap();

            if (beatmap == null || beatmap.ControlPoints == null || beatmap.ControlPoints.Timing.Count == 0)
            {
                DetectFallbackBeat();
                return;
            }

            // 谱面引用变化时重置节拍状态
            if (beatmap != cachedBeatmap)
            {
                cachedBeatmap = beatmap;
                nextBeatTimeMs = -1;
                currentTimingPointIndex = -1;
            }

            var timingPoints = beatmap.ControlPoints.Timing;

            // 找到当前时间的红线（二分搜索）
            int tpIndex = FindTimingPointIndex(timingPoints, currentTimeMs);

            // 红线变化 → 重新同步节拍
            if (tpIndex != currentTimingPointIndex)
            {
                currentTimingPointIndex = tpIndex;
                var tp = timingPoints[tpIndex];
                currentMsPerBeat = tp.MsPerBeat;

                // 从当前红线起始时间开始，计算下一个拍点
                double tpStart = tp.Time;
                double msPerBeat = tp.MsPerBeat;
                double beatsFromStart = (currentTimeMs - tpStart) / msPerBeat;
                int beatNumber = (int)Math.Ceiling(beatsFromStart);
                nextBeatTimeMs = tpStart + beatNumber * msPerBeat;
            }

            // 检查是否到达拍点
            double anticipatedTime = currentTimeMs + beatAnticipation * 1000.0;
            if (nextBeatTimeMs >= 0 && anticipatedTime >= nextBeatTimeMs)
            {
                TriggerBeat();

                // 推进到下一拍
                // 检查是否接近下一个红线（避免跨红线时错过切换）
                nextBeatTimeMs += currentMsPerBeat;

                // 如果下一拍超出了当前红线范围且存在后续红线，让下一帧的红线检测来重新同步
                if (tpIndex + 1 < timingPoints.Count && nextBeatTimeMs >= timingPoints[tpIndex + 1].Time)
                {
                    // 标记需要重新同步
                    currentTimingPointIndex = -1;
                }
            }
        }

        /// <summary>
        /// 二分查找：找到 time 所属的 TimingPoint 索引（最后一个 Time <= time 的）
        /// </summary>
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

        /// <summary>
        /// 无谱面时用 Bass 上升沿做粗略节拍检测作为 fallback
        /// </summary>
        private float lastBassValue = 0f;
        private float beatCooldownTimer = 0f;

        private void DetectFallbackBeat()
        {
            var audioManager = AudioVisualizationManager.Instance;
            if (audioManager == null) return;

            beatCooldownTimer -= Time.deltaTime;
            if (beatCooldownTimer < 0f) beatCooldownTimer = 0f;

            // fallback 的最短拍间距 = 60/200BPM = 0.3s
            float minBeatInterval = 0.3f;

            float bass = audioManager.Bass;
            if (bass > 0.55f && lastBassValue <= 0.55f && beatCooldownTimer <= 0f)
            {
                TriggerBeat();
                beatCooldownTimer = minBeatInterval;
            }
            lastBassValue = bass;
        }

        /// <summary>
        /// 触发一次节拍效果
        /// </summary>
        private void TriggerBeat()
        {
            isBeatFrame = true;

            // 触发闪烁脉冲
            float flashPower = beatFlashPeak * (isKiaiActive ? kiaiBeatFlashMultiplier : 1f);
            beatFlashIntensity = flashPower;

            // 触发 size 脉冲
            float sizePower = beatSizePeak * (isKiaiActive ? kiaiBeatFlashMultiplier : 1f);
            beatSizePulseIntensity = sizePower;

            // Kiai 时节拍触发额外 burst
            if (isKiaiActive)
            {
                ps.Emit(kiaiBeatBurstCount);
            }
        }

        // =========================================================
        // Kiai 检测
        // =========================================================

        private void UpdateKiaiState()
        {
            wasKiaiActive = isKiaiActive;
            isKiaiActive = false;

            if (rhythmGameManager == null || !rhythmGameManager.isPlaying)
            {
                UpdateKiaiVelocity();
                return;
            }

            double currentTimeMs = rhythmGameManager.currentMusicTimeMs;
            Beatmap beatmap = rhythmGameManager.GetCurrentBeatmap();

            if (beatmap == null || beatmap.ControlPoints == null)
            {
                UpdateKiaiVelocity();
                return;
            }

            var kiaiPeriods = beatmap.ControlPoints.KiaiPeriods;
            foreach (var kiaiPeriod in kiaiPeriods)
            {
                if (currentTimeMs >= kiaiPeriod.StartTime && currentTimeMs <= kiaiPeriod.EndTime)
                {
                    isKiaiActive = true;
                    break;
                }
            }

            UpdateKiaiVelocity();
        }

        private void UpdateKiaiVelocity()
        {
            // 关键：x/y/z 全部使用相同的 MinMaxCurve 模式（RandomBetweenTwoConstants）
            // 绝对不能用 MinMaxCurve(float) 单值，否则 Unity 报 "curves must all be in the same mode"

            if (isKiaiActive)
            {
                float mid = 0f;
                var audioManager = AudioVisualizationManager.Instance;
                if (audioManager != null)
                {
                    mid = audioManager.Mid;
                }

                float currentSpeed = kiaiRiseSpeed + (mid * kiaiMidSpeedBonus);
                currentVelocityY = currentSpeed;

                velocityModule.enabled = true;
                velocityModule.space = ParticleSystemSimulationSpace.World;
                velocityModule.x = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);
                velocityModule.y = new ParticleSystem.MinMaxCurve(currentSpeed * 0.9f, currentSpeed * 1.1f);
                velocityModule.z = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);

                // Kiai: 螺旋上升效果
                rotationModule.enabled = true;
                rotationModule.z = new ParticleSystem.MinMaxCurve(kiaiAngularVelocity * (1f + mid));

                // 降低 noise 但不完全关闭，保留微弱摇曳
                noiseModule.enabled = true;
                noiseModule.strength = baseNoiseStrength * 0.3f;
                noiseModule.frequency = 0.5f;

                // Kiai burst
                kiaiBurstTimer += Time.deltaTime;
                if (kiaiBurstTimer >= kiaiBurstInterval)
                {
                    kiaiBurstTimer = 0f;
                    ps.Emit(kiaiBurstCount);
                }
            }
            else
            {
                currentVelocityY = 0.1f;

                velocityModule.space = ParticleSystemSimulationSpace.World;
                velocityModule.x = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);
                velocityModule.y = new ParticleSystem.MinMaxCurve(-0.05f, 0.2f);
                velocityModule.z = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);

                rotationModule.enabled = false;

                noiseModule.enabled = true;
                noiseModule.strength = currentNoiseStrength;
                noiseModule.frequency = 0.3f;

                kiaiBurstTimer = 0f;
            }
        }

        // =========================================================
        // 节拍闪烁衰减
        // =========================================================

        private void UpdateBeatFlash()
        {
            if (beatFlashIntensity > 0.01f)
            {
                beatFlashIntensity *= Mathf.Exp(-beatFlashDecay * Time.deltaTime);
            }
            else
            {
                beatFlashIntensity = 0f;
            }

            if (beatSizePulseIntensity > 0.01f)
            {
                beatSizePulseIntensity *= Mathf.Exp(-beatSizeDecay * Time.deltaTime);
            }
            else
            {
                beatSizePulseIntensity = 0f;
            }
        }

        private void UpdateParticleColors()
        {
            if (ps == null) return;

            int particleCount = ps.particleCount;
            if (particleCount == 0) return;

            EnsureParticleBuffer(particleCount);

            int count = ps.GetParticles(particleBuffer);

            bool shouldFlash = beatFlashIntensity > 0.1f;

            for (int i = 0; i < count; i++)
            {
                uint seed = particleBuffer[i].randomSeed;
                float hue = HashToFloat(seed);
                hue = Mathf.Repeat(hue + currentHueOffset, 1f);

                // 在 Kiai 时偏向暖色系
                if (isKiaiActive)
                {
                    float warmHue = Mathf.Lerp(hue, 0.08f + HashToFloat(seed + 3u) * 0.1f, 0.4f);
                    hue = warmHue;
                }

                float saturation = 0.6f + HashToFloat(seed + 1u) * 0.3f;
                float value = 0.85f + HashToFloat(seed + 2u) * 0.15f;

                Color rgbColor = Color.HSVToRGB(hue, saturation, value);
                rgbColor.a = particleBuffer[i].startColor.a;

                // 节拍闪烁：对 alpha 乘以闪烁系数
                if (shouldFlash)
                {
                    float flashFactor = 1f + beatFlashIntensity * 0.3f;
                    rgbColor.a = Mathf.Clamp01(rgbColor.a * flashFactor);
                    rgbColor.r = Mathf.Clamp01(rgbColor.r + beatFlashIntensity * 0.05f);
                    rgbColor.g = Mathf.Clamp01(rgbColor.g + beatFlashIntensity * 0.05f);
                    rgbColor.b = Mathf.Clamp01(rgbColor.b + beatFlashIntensity * 0.05f);
                }

                particleBuffer[i].startColor = rgbColor;
            }

            ps.SetParticles(particleBuffer, count);
        }

        /// <summary>
        /// 快速确定性哈希：将 uint 种子映射到 [0,1) 浮点数
        /// </summary>
        private static float HashToFloat(uint seed)
        {
            uint x = seed;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            return (float)(x & 0x7FFFFFFF) / (float)0x7FFFFFFF;
        }

        private void EnsureParticleBuffer(int particleCount)
        {
            if (particleBuffer == null || particleBuffer.Length < particleCount)
            {
                bufferCapacity = Mathf.Max(particleCount + 64, 256);
                particleBuffer = new ParticleSystem.Particle[bufferCapacity];
            }
        }

        private void LimitInnerParticles()
        {
            if (ps == null) return;

            int particleCount = ps.particleCount;
            if (particleCount == 0) return;

            EnsureParticleBuffer(particleCount);

            int count = ps.GetParticles(particleBuffer);

            int innerCount = 0;
            int killed = 0;

            float innerRadiusSq = innerRadius * innerRadius;

            for (int i = 0; i < count; i++)
            {
                float distSq = particleBuffer[i].position.sqrMagnitude;
                if (distSq < innerRadiusSq)
                {
                    innerCount++;
                    if (innerCount > innerMaxParticles)
                    {
                        particleBuffer[i].remainingLifetime = 0f;
                        killed++;
                    }
                }
            }

            if (killed > 0)
            {
                ps.SetParticles(particleBuffer, count);
            }
        }

        // =========================================================
        // ParticleSystem 全自动初始化
        // =========================================================

        private void InitializeParticleSystem()
        {
            // Main 模块
            mainModule = ps.main;
            mainModule.maxParticles = maxParticles;
            mainModule.startLifetime = new ParticleSystem.MinMaxCurve(14f, 28f);
            mainModule.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
            mainModule.startSize = new ParticleSystem.MinMaxCurve(
                baseParticleSize * 0.6f,
                baseParticleSize * 1.4f
            );
            mainModule.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f);
            mainModule.simulationSpace = ParticleSystemSimulationSpace.World;
            mainModule.playOnAwake = true;
            mainModule.loop = true;
            mainModule.prewarm = true;

            // 梦幻色：淡青蓝 → 淡紫
            Color etherealCyan = new Color(0.4f, 0.65f, 1.0f, 0.25f);
            Color etherealPurple = new Color(0.7f, 0.3f, 1.0f, 0.20f);
            mainModule.startColor = new ParticleSystem.MinMaxGradient(etherealCyan, etherealPurple);

            // Shape 模块
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = outerRadius;
            shape.radiusThickness = (outerRadius - innerRadius) / outerRadius;

            // Trigger 模块
            triggerModule = ps.trigger;
            triggerModule.enabled = true;
            triggerModule.inside = ParticleSystemOverlapAction.Callback;
            triggerModule.radiusScale = 1f;

            // Emission 模块
            emissionModule = ps.emission;
            emissionModule.enabled = true;
            emissionModule.rateOverTime = emissionRate;

            // Noise 模块
            noiseModule = ps.noise;
            noiseModule.enabled = true;
            noiseModule.quality = ParticleSystemNoiseQuality.High;
            noiseModule.strength = baseNoiseStrength;
            noiseModule.strengthMultiplier = 1f;
            noiseModule.frequency = 0.3f;
            noiseModule.scrollSpeed = 0.15f;
            noiseModule.damping = true;
            noiseModule.octaveCount = 3;
            noiseModule.octaveMultiplier = 0.5f;
            noiseModule.octaveScale = 2f;

            // Color Over Lifetime
            colorOverLifetimeModule = ps.colorOverLifetime;
            colorOverLifetimeModule.enabled = true;

            // Cache modules
            velocityModule = ps.velocityOverLifetime;
            rotationModule = ps.rotationOverLifetime;

            // Velocity Over Lifetime
            velocityModule.enabled = true;
            velocityModule.space = ParticleSystemSimulationSpace.World;
            velocityModule.x = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);
            velocityModule.y = new ParticleSystem.MinMaxCurve(-0.05f, 0.2f);
            velocityModule.z = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);

            // Size Over Lifetime
            sizeOverLifetimeModule = ps.sizeOverLifetime;
            sizeOverLifetimeModule.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(new Keyframe(0f, 0.2f, 0f, 2f));
            sizeCurve.AddKey(new Keyframe(0.15f, 1f, 0f, 0f));
            sizeCurve.AddKey(new Keyframe(0.5f, 0.9f, 0f, 0f));
            sizeCurve.AddKey(new Keyframe(0.85f, 0.7f, 0f, -1f));
            sizeCurve.AddKey(new Keyframe(1f, 0f, -1f, 0f));
            sizeOverLifetimeModule.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            // Renderer 设置
            psRenderer = GetComponent<ParticleSystemRenderer>();
            SetupRenderer();
        }

        private void SetupRenderer()
        {
            psRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            psRenderer.sortMode = ParticleSystemSortMode.Distance;
            psRenderer.enableGPUInstancing = true;
            psRenderer.allowOcclusionWhenDynamic = false;
            psRenderer.maxParticleSize = 0.5f;

            GenerateGlowTexture();

            Shader particleShader = FindParticleShader();

            if (particleShader == null)
            {
                Debug.LogError("[CodeDrivenAmbientParticles] 未找到任何可用粒子 Shader！");
                return;
            }

            Material particleMat = new Material(particleShader);
            particleMat.enableInstancing = true;

            if (glowTexture != null)
            {
                if (particleMat.HasProperty("_BaseMap"))
                    particleMat.SetTexture("_BaseMap", glowTexture);
                if (particleMat.HasProperty("_MainTex"))
                    particleMat.SetTexture("_MainTex", glowTexture);
            }

            Color tintColor = new Color(0.5f, 0.7f, 1f, 0.7f);
            if (particleMat.HasProperty("_BaseColor"))
                particleMat.SetColor("_BaseColor", tintColor);
            if (particleMat.HasProperty("_Color"))
                particleMat.SetColor("_Color", tintColor);
            if (particleMat.HasProperty("_TintColor"))
                particleMat.SetColor("_TintColor", tintColor);

            if (particleMat.HasProperty("_Surface"))
            {
                particleMat.SetInt("_Surface", 1);
                particleMat.SetInt("_Blend", 1);
            }
            if (particleMat.HasProperty("_SrcBlend"))
            {
                particleMat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                particleMat.SetInt("_DstBlend", (int)BlendMode.One);
            }
            if (particleMat.HasProperty("_ZWrite"))
                particleMat.SetInt("_ZWrite", 0);
            if (particleMat.HasProperty("_Cull"))
                particleMat.SetInt("_Cull", 0);

            particleMat.renderQueue = 3000;

            psRenderer.material = particleMat;
        }

        private Shader FindParticleShader()
        {
            Shader[] candidates = new Shader[]
            {
                Shader.Find("Universal Render Pipeline/Particles/Unlit"),
                Shader.Find("Universal Render Pipeline/Particles/Simple Lit"),
                Shader.Find("Universal Render Pipeline/Unlit"),
                Shader.Find("Mobile/Particles/Additive"),
                Shader.Find("Legacy Shaders/Particles/Additive"),
                Shader.Find("Particles/Standard Unlit"),
            };

            for (int i = 0; i < candidates.Length; i++)
            {
                if (candidates[i] != null)
                {
                    return candidates[i];
                }
            }

            return null;
        }

        /// <summary>
        /// 生成高质量辉光贴图：双层指数衰减（亮核 + 柔和辉光）
        /// </summary>
        private void GenerateGlowTexture()
        {
            int size = 128;
            glowTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float center = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    float normalizedDist = Mathf.Clamp01(dist / center);

                    float coreAlpha = Mathf.Pow(1f - normalizedDist, 6f);
                    float glowAlpha = Mathf.Pow(1f - normalizedDist, 2f) * 0.4f;
                    float alpha = Mathf.Clamp01(coreAlpha + glowAlpha);

                    float coreBlend = coreAlpha / (alpha + 0.001f);
                    float r = Mathf.Lerp(0.7f, 1f, coreBlend);
                    float g = Mathf.Lerp(0.8f, 1f, coreBlend);
                    float b = 1f;

                    glowTexture.SetPixel(x, y, new Color(r, g, b, alpha));
                }
            }

            glowTexture.Apply();
        }

        private void InitializeGradientCache()
        {
            cachedColorKeys = new GradientColorKey[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            };
            cachedAlphaKeys = new GradientAlphaKey[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.25f, 0.08f),
                new GradientAlphaKey(0.18f, 0.8f),
                new GradientAlphaKey(0f, 1f)
            };
            cachedFadeGradient = new Gradient();
            cachedFadeGradient.SetKeys(cachedColorKeys, cachedAlphaKeys);
        }

        private void InitializeMaterialPropertyBlock()
        {
            mpb = new MaterialPropertyBlock();
            psRenderer.GetPropertyBlock(mpb);
        }

        // =========================================================
        // 音频响应逻辑
        // =========================================================

        private void ValidateAudioManager()
        {
            if (AudioVisualizationManager.Instance == null)
            {
                Debug.LogWarning("[CodeDrivenAmbientParticles] AudioVisualizationManager 未找到，粒子将使用静态参数");
            }
        }

        private void UpdateAudioReactivity()
        {
            var audioManager = AudioVisualizationManager.Instance;
            float dt = Time.deltaTime;
            float lerpFactor = 1f - Mathf.Exp(-responseSmoothSpeed * dt);

            float bass = 0f, mid = 0f, treble = 0f;
            if (audioManager != null)
            {
                bass = audioManager.Bass;
                mid = audioManager.Mid;
                treble = audioManager.Treble;
            }

            // Bass → 湍流强度
            if (!isKiaiActive)
            {
                float targetNoiseStrength = baseNoiseStrength + bass * maxNoiseStrength;
                currentNoiseStrength = Mathf.Lerp(currentNoiseStrength, targetNoiseStrength, lerpFactor);
                noiseModule.strength = currentNoiseStrength;
            }

            // Bass → 亮度脉冲 + 节拍闪烁叠加
            bassPhase += dt * bassPulseSpeed * (1f + bass * 2f);
            float bassPulse = (Mathf.Sin(bassPhase * Mathf.PI * 2f) + 1f) * 0.5f;
            float targetBrightness = 0.2f + bassPulse * maxBassBrightness * bass;

            if (isKiaiActive)
            {
                targetBrightness *= kiaiBrightnessMultiplier;
            }

            // 叠加 BPM 节拍闪烁强度
            targetBrightness += beatFlashIntensity;

            currentBrightness = Mathf.Lerp(currentBrightness, targetBrightness, lerpFactor);
            UpdateBrightness(currentBrightness);

            // Bass → Size 脉冲 + 节拍 size 脉冲叠加
            float targetSizePulse = 1f + bass * maxBassSizePulse;
            if (isKiaiActive) targetSizePulse += bass * 0.2f;

            // 叠加 BPM 节拍 size 脉冲
            targetSizePulse += beatSizePulseIntensity * 0.3f;

            currentSizeMultiplier = Mathf.Lerp(currentSizeMultiplier, targetSizePulse, lerpFactor);

            var sizeCurve = sizeOverLifetimeModule.size;
            sizeOverLifetimeModule.size = new ParticleSystem.MinMaxCurve(currentSizeMultiplier, sizeCurve.curve);

            // Mid → 旋转速度
            if (!isKiaiActive)
            {
                currentRotationSpeed = Mathf.Lerp(currentRotationSpeed, mid * maxMidRotationSpeed, lerpFactor);
                if (currentRotationSpeed > 0.02f)
                {
                    rotationModule.enabled = true;
                    rotationModule.z = new ParticleSystem.MinMaxCurve(-currentRotationSpeed * 0.5f, currentRotationSpeed);
                }
                else
                {
                    rotationModule.enabled = false;
                }
            }

            // Treble → 色相偏移
            currentHueOffset += treble * trebleHueSpeed * dt;
            if (currentHueOffset > 1f) currentHueOffset -= 1f;

            // Bass → 发射率脉动 + 节拍发射
            float currentRate = emissionModule.rateOverTime.constant;
            float targetRate = emissionRate * (1f + bass * 1.5f);
            if (isKiaiActive)
            {
                targetRate *= 1.4f;
            }
            if (isBeatFrame)
            {
                targetRate *= 2.5f;
            }
            emissionModule.rateOverTime = new ParticleSystem.MinMaxCurve(Mathf.Lerp(currentRate, targetRate, lerpFactor));
        }

        private void UpdateBrightness(float brightness)
        {
            float alpha = Mathf.Clamp01(brightness);
            cachedAlphaKeys[0].alpha = 0f;
            cachedAlphaKeys[1].alpha = alpha;
            cachedAlphaKeys[2].alpha = alpha * 0.6f;
            cachedAlphaKeys[3].alpha = 0f;
            cachedFadeGradient.SetKeys(cachedColorKeys, cachedAlphaKeys);
            colorOverLifetimeModule.color = cachedFadeGradient;
        }
    }
}
