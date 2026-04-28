using UnityEngine;
using UnityEngine.Rendering;

namespace OsuVR
{
    /// <summary>
    /// 纯代码驱动的空灵环境粒子系统
    /// 100% 免配置，挂载即用
    /// 
    /// 音频律动映射：
    /// - Bass (低音) → noise.strength (湍流强度) + 亮度脉冲 → 鼓点爆炸感
    /// - Treble (高频) → 色相偏移 → RGB LED 小灯效果
    /// 
    /// Kiai 段效果（从 TimingPoints 解析）：
    /// - 所有粒子固定速度向上飞行（星辰流动效果）
    /// - 亮度增强 × 1.5
    /// - 湍流增强 × 1.5
    /// - 发射率增强 × 1.5
    /// 
    /// 使用方法：
    /// 1. 在场景中创建空物体
    /// 2. 挂载此脚本（ParticleSystem 会自动添加）
    /// 3. 运行场景，粒子会自动响应音频和 Kiai 段
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public class CodeDrivenAmbientParticles : MonoBehaviour
    {
        // =========================================================
        // 配置参数（可在 Inspector 微调，但已有合理默认值）
        // =========================================================

        [Header("粒子基础参数")]
        [Tooltip("最大粒子数量")]
        [SerializeField] private int maxParticles = 10000;

        [Tooltip("发射速率")]
        [SerializeField] private int emissionRate = 100;

        [Tooltip("外层包裹半径")]
        [SerializeField] private float outerRadius = 30f;

        [Tooltip("内层排除半径（避免干扰读谱）")]
        [SerializeField] private float innerRadius = 3.0f;

        [Tooltip("内层区域最大粒子数（限制干扰）")]
        [SerializeField] private int innerMaxParticles = 2500;

        [Header("音频响应参数")]
        [Tooltip("Bass 对湍流的最大影响值")]
        [SerializeField] private float maxNoiseStrength = 1.5f;

        [Tooltip("基础湍流强度")]
        [SerializeField] private float baseNoiseStrength = 0.25f;

        [Tooltip("Bass 对亮度脉冲的最大乘数")]
        [SerializeField] private float maxBassBrightness = 2.5f;

        [Tooltip("Bass 脉冲速度")]
        [SerializeField] private float bassPulseSpeed = 2f;

        [Tooltip("Treble 对色相偏移速度")]
        [SerializeField] private float trebleHueSpeed = 0.5f;

        [Tooltip("响应平滑速度")]
        [SerializeField] private float responseSmoothSpeed = 16f;

        [Header("Kiai 段参数")]
        [Tooltip("Kiai 期间粒子上升速度")]
        [SerializeField] private float kiaiRiseSpeed = 3.5f;

        [Tooltip("Kiai 期间 Mid 对上升速度的加成")]
        [SerializeField] private float kiaiMidSpeedBonus = 1.5f;

        [Tooltip("Kiai 亮度增强倍数")]
        [SerializeField] private float kiaiBrightnessMultiplier = 1.5f;

        // =========================================================
        // 内部状态
        // =========================================================

        private ParticleSystem ps;
        private ParticleSystem.MainModule mainModule;
        private ParticleSystem.NoiseModule noiseModule;
        private ParticleSystem.EmissionModule emissionModule;
        private ParticleSystem.ColorOverLifetimeModule colorOverLifetimeModule;
        private ParticleSystem.TriggerModule triggerModule;
        private ParticleSystem.Particle[] particleBuffer;
        private int bufferCapacity = 0;
        private SphereCollider triggerCollider;

        private MaterialPropertyBlock mpb;
        private ParticleSystemRenderer psRenderer;
        private Texture2D glowTexture;

        private float currentNoiseStrength;
        private float currentBrightness = 1f;
        private float currentHueOffset = 0f;
        private float bassPhase = 0f;

        private bool isKiaiActive = false;
        private bool wasKiaiActive = false;
        private RhythmGameManager rhythmGameManager;

        private Gradient colorGradient;
        private Color baseColorLow;
        private Color baseColorHigh;

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
            InitializeColorGradient();
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
                Debug.LogWarning("[CodeDrivenAmbientParticles] RhythmGameManager 未找到，Kiai 检测将禁用");
            }
            else
            {
                Debug.Log("[CodeDrivenAmbientParticles] 已连接到 RhythmGameManager");
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
            UpdateAudioReactivity();
            UpdateParticleColors();
            LimitInnerParticles();
        }

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
            var velocity = ps.velocityOverLifetime;
            
            if (isKiaiActive)
            {
                float mid = 0f;
                var audioManager = AudioVisualizationManager.Instance;
                if (audioManager != null)
                {
                    mid = audioManager.Mid;
                }
                
                float currentSpeed = kiaiRiseSpeed + (mid * kiaiMidSpeedBonus);
                
                velocity.enabled = true;
                velocity.space = ParticleSystemSimulationSpace.World;
                velocity.x = new ParticleSystem.MinMaxCurve(0f);
                velocity.y = new ParticleSystem.MinMaxCurve(currentSpeed);
                velocity.z = new ParticleSystem.MinMaxCurve(0f);
                
                noiseModule.enabled = false;
            }
            else
            {
                velocity.space = ParticleSystemSimulationSpace.World;
                velocity.x = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);
                velocity.y = new ParticleSystem.MinMaxCurve(-0.1f, 0.3f);
                velocity.z = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);
                
                noiseModule.enabled = true;
            }
        }

        private void UpdateParticleColors()
        {
            if (ps == null) return;

            int particleCount = ps.particleCount;
            if (particleCount == 0) return;

            if (particleBuffer == null || particleBuffer.Length < particleCount)
            {
                bufferCapacity = Mathf.Max(particleCount, 256);
                particleBuffer = new ParticleSystem.Particle[bufferCapacity];
            }

            int count = ps.GetParticles(particleBuffer);

            for (int i = 0; i < count; i++)
            {
                uint seed = particleBuffer[i].randomSeed;
                // 使用确定性哈希代替 new System.Random，避免GC压力
                float hue = HashToFloat(seed);
                hue = Mathf.Repeat(hue + currentHueOffset, 1f);

                float saturation = 0.7f + HashToFloat(seed + 1u) * 0.3f;
                float value = 0.8f + HashToFloat(seed + 2u) * 0.2f;

                Color rgbColor = Color.HSVToRGB(hue, saturation, value);
                rgbColor.a = particleBuffer[i].startColor.a;

                particleBuffer[i].startColor = rgbColor;
            }

            ps.SetParticles(particleBuffer, count);
        }

        /// <summary>
        /// 快速确定性哈希：将 uint 种子映射到 [0,1) 浮点数
        /// 使用 xorshift 算法，零GC，零分配
        /// </summary>
        private static float HashToFloat(uint seed)
        {
            uint x = seed;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            return (float)(x & 0x7FFFFFFF) / (float)0x7FFFFFFF;
        }

        private void LimitInnerParticles()
        {
            if (ps == null) return;

            int particleCount = ps.particleCount;
            if (particleCount == 0) return;

            if (particleBuffer == null || particleBuffer.Length < particleCount)
            {
                bufferCapacity = Mathf.Max(particleCount, 256);
                particleBuffer = new ParticleSystem.Particle[bufferCapacity];
            }

            int count = ps.GetParticles(particleBuffer);

            int innerCount = 0;
            int killed = 0;

            for (int i = 0; i < count; i++)
            {
                float dist = particleBuffer[i].position.magnitude;
                if (dist < innerRadius)
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
            mainModule.startLifetime = new ParticleSystem.MinMaxCurve(12.5f, 25f);
            mainModule.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
            mainModule.startSize = new ParticleSystem.MinMaxCurve(0.75f, 0.85f);
            mainModule.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f);
            mainModule.simulationSpace = ParticleSystemSimulationSpace.World;
            mainModule.playOnAwake = true;
            mainModule.loop = true;
            mainModule.prewarm = true;

            Color etherealCyan = new Color(0.3f, 0.7f, 1.0f, 0.25f);
            Color etherealPurple = new Color(0.6f, 0.2f, 1.0f, 0.2f);
            mainModule.startColor = new ParticleSystem.MinMaxGradient(etherealCyan, etherealPurple);

            // Shape 模块：球形包裹玩家空间，排除中心区域
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = outerRadius;
            shape.radiusThickness = (outerRadius - innerRadius) / outerRadius;

            // Trigger 模块：限制进入内层区域的粒子数量
            triggerModule = ps.trigger;
            triggerModule.enabled = true;
            triggerModule.inside = ParticleSystemOverlapAction.Callback;
            triggerModule.radiusScale = 1f;

            // Emission 模块
            emissionModule = ps.emission;
            emissionModule.enabled = true;
            emissionModule.rateOverTime = emissionRate;

            // Noise 模块：萤火虫式漂浮
            noiseModule = ps.noise;
            noiseModule.enabled = true;
            noiseModule.quality = ParticleSystemNoiseQuality.High;
            noiseModule.strength = baseNoiseStrength;
            noiseModule.strengthMultiplier = 1f;
            noiseModule.frequency = 0.3f;
            noiseModule.scrollSpeed = 0.2f;
            noiseModule.damping = true;
            noiseModule.octaveCount = 3;
            noiseModule.octaveMultiplier = 0.5f;
            noiseModule.octaveScale = 2f;

            // Color Over Lifetime：淡入淡出
            colorOverLifetimeModule = ps.colorOverLifetime;
            colorOverLifetimeModule.enabled = true;
            Gradient fadeGradient = new Gradient();
            fadeGradient.SetKeys(
                new GradientColorKey[] 
                { 
                    new GradientColorKey(Color.white, 0f), 
                    new GradientColorKey(Color.white, 1f) 
                },
                new GradientAlphaKey[] 
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.35f, 0.1f),
                    new GradientAlphaKey(0.25f, 0.8f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOverLifetimeModule.color = fadeGradient;

            // Velocity Over Lifetime：缓慢漂移
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);
            velocity.y = new ParticleSystem.MinMaxCurve(-0.1f, 0.3f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);

            // Size Over Lifetime：轻微呼吸感
            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(0f, 0.5f);
            sizeCurve.AddKey(0.3f, 1f);
            sizeCurve.AddKey(0.7f, 0.9f);
            sizeCurve.AddKey(1f, 0.3f);
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

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

            GenerateGlowTexture();

            Shader particleShader = FindParticleShader();

            if (particleShader == null)
            {
                Debug.LogError("[CodeDrivenAmbientParticles] 未找到任何可用粒子 Shader！");
                return;
            }

            Debug.Log($"[CodeDrivenAmbientParticles] 使用 Shader: {particleShader.name}");

            Material particleMat = new Material(particleShader);
            particleMat.enableInstancing = true;

            if (glowTexture != null)
            {
                if (particleMat.HasProperty("_BaseMap"))
                    particleMat.SetTexture("_BaseMap", glowTexture);
                if (particleMat.HasProperty("_MainTex"))
                    particleMat.SetTexture("_MainTex", glowTexture);
            }

            Color tintColor = new Color(0.6f, 0.8f, 1f, 0.8f);
            if (particleMat.HasProperty("_BaseColor"))
                particleMat.SetColor("_BaseColor", tintColor);
            if (particleMat.HasProperty("_Color"))
                particleMat.SetColor("_Color", tintColor);
            if (particleMat.HasProperty("_TintColor"))
                particleMat.SetColor("_TintColor", tintColor);

            // URP Particles/Unlit 透明混合设置（参考 HitObjectFactory 的做法）
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
                    Debug.Log($"[CodeDrivenAmbientParticles] 使用 Shader: {candidates[i].name}");
                    return candidates[i];
                }
            }

            return null;
        }

        private void GenerateGlowTexture()
        {
            int size = 32;
            glowTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float center = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    float normalizedDist = Mathf.Clamp01(dist / center);
                    float alpha = Mathf.Pow(1f - normalizedDist, 8f);
                    glowTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            glowTexture.Apply();
        }

        private void InitializeColorGradient()
        {
            // 空灵色彩渐变：青色 → 紫色
            baseColorLow = new Color(0.3f, 0.7f, 1.0f, 0.3f);
            baseColorHigh = new Color(0.6f, 0.2f, 1.0f, 0.25f);

            colorGradient = new Gradient();
            colorGradient.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(baseColorLow, 0f),
                    new GradientColorKey(baseColorHigh, 1f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0.3f, 0f),
                    new GradientAlphaKey(0.25f, 1f)
                }
            );
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
            else
            {
                Debug.Log("[CodeDrivenAmbientParticles] 已连接到 AudioVisualizationManager");
            }
        }

        private void UpdateAudioReactivity()
        {
            var audioManager = AudioVisualizationManager.Instance;
            if (audioManager == null) return;

            float bass = audioManager.Bass;
            float treble = audioManager.Treble;

            float lerpFactor = 1f - Mathf.Exp(-responseSmoothSpeed * Time.deltaTime);

            // Bass → 湍流强度（爆炸感）- Kiai 期间禁用
            if (!isKiaiActive)
            {
                float targetNoiseStrength = baseNoiseStrength + bass * maxNoiseStrength;
                currentNoiseStrength = Mathf.Lerp(currentNoiseStrength, targetNoiseStrength, lerpFactor);
                noiseModule.strength = currentNoiseStrength;
            }

            // Bass → 亮度脉冲（规律变亮）
            bassPhase += Time.deltaTime * bassPulseSpeed * (1f + bass * 2f);
            float bassPulse = (Mathf.Sin(bassPhase * Mathf.PI * 2f) + 1f) * 0.5f;
            float targetBrightness = 0.3f + bassPulse * maxBassBrightness * bass;
            
            if (isKiaiActive)
            {
                targetBrightness *= kiaiBrightnessMultiplier;
            }
            
            currentBrightness = Mathf.Lerp(currentBrightness, targetBrightness, lerpFactor);
            UpdateBrightness(currentBrightness);

            // Treble → 色相偏移（通过修改粒子颜色实现）
            currentHueOffset += treble * trebleHueSpeed * Time.deltaTime;
            if (currentHueOffset > 1f) currentHueOffset -= 1f;

            // Bass → 发射率脉动
            float targetRate = emissionRate * (1f + bass * 2f);
            if (isKiaiActive)
            {
                targetRate *= 1.5f;
            }
            emissionModule.rateOverTime = Mathf.Lerp(emissionModule.rateOverTime.constant, targetRate, lerpFactor);
        }

        private void UpdateBrightness(float brightness)
        {
            Gradient fadeGradient = new Gradient();
            float alpha = Mathf.Clamp01(brightness);
            
            fadeGradient.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(alpha, 0.1f),
                    new GradientAlphaKey(alpha * 0.7f, 0.8f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOverLifetimeModule.color = fadeGradient;
        }

        // =========================================================
        // 公开接口：手动触发效果
        // =========================================================

        /// <summary>
        /// 手动触发一次粒子爆发
        /// </summary>
        /// <param name="count">爆发粒子数量</param>
        public void TriggerBurst(int count = 50)
        {
            if (ps == null) return;
            ps.Emit(count);
        }

        /// <summary>
        /// 设置粒子密度（0-1）
        /// </summary>
        public void SetDensity(float density)
        {
            density = Mathf.Clamp01(density);
            mainModule.maxParticles = Mathf.RoundToInt(maxParticles * density);
            emissionModule.rateOverTime = Mathf.RoundToInt(emissionRate * density);
        }

        /// <summary>
        /// 获取当前音频响应状态（调试用）
        /// </summary>
        public (float noiseStrength, float brightness, float hueOffset) GetCurrentState()
        {
            return (currentNoiseStrength, currentBrightness, currentHueOffset);
        }
    }
}
