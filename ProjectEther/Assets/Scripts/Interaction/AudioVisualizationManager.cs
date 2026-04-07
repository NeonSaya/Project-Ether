using UnityEngine;
using Lasp;

namespace OsuVR
{
    /// <summary>
    /// 音频可视化核心管理器：支持Lasp系统捕获和AudioSource频谱分析
    /// 提供三频段能量数据供视觉系统使用
    /// </summary>
    public class AudioVisualizationManager : MonoBehaviour
    {
        public static AudioVisualizationManager Instance { get; private set; }

        // =========================================================
        // 配置参数
        // =========================================================

        [Header("平滑处理设置")]
        [Tooltip("平滑速度（值越小越平滑，建议0.05-0.2）")]
        [Range(0.01f, 1.0f)]
        public float smoothSpeed = 0.1f;

        [Header("增益调节")]
        [Tooltip("低频增益倍数")]
        [Range(0.1f, 5.0f)]
        public float bassGain = 1.5f;

        [Tooltip("中频增益倍数")]
        [Range(0.1f, 5.0f)]
        public float midGain = 1.2f;

        [Tooltip("高频增益倍数")]
        [Range(0.1f, 5.0f)]
        public float trebleGain = 1.0f;

        [Header("归一化设置")]
        [Tooltip("频谱数据归一化系数（用于将原始频谱值映射到0-1）")]
        [Range(1f, 100f)]
        public float normalizationFactor = 10f;

        [Header("音频源设置")]
        [Tooltip("目标音频源（留空则自动查找）")]
        public AudioSource targetAudioSource;

        [Header("Lasp系统捕获（可选）")]
        [Tooltip("Lasp频谱分析器组件（可选，用于系统级音频捕获）")]
        public SpectrumAnalyzer spectrumAnalyzer;

        [Tooltip("使用Lasp系统捕获（需要配置SpectrumAnalyzer）")]
        public bool useLaspCapture = false;

        // =========================================================
        // 公开属性：三频段能量值（0-1归一化）
        // =========================================================

        /// <summary>
        /// 低频能量（0-150Hz）：鼓点、贝斯
        /// </summary>
        public float Bass { get; private set; }

        /// <summary>
        /// 中频能量（200-500Hz）：人声、旋律
        /// </summary>
        public float Mid { get; private set; }

        /// <summary>
        /// 高频能量（500Hz-4kHz）：高音、镲片
        /// </summary>
        public float Treble { get; private set; }

        // =========================================================
        // 内部状态
        // =========================================================

        private float rawBass;
        private float rawMid;
        private float rawTreble;
        private float[] spectrumData;
        private const int spectrumSize = 512;

        // 全局Shader变量名
        private const string SHADER_BASS = "_Global_Audio_Bass";
        private const string SHADER_MID = "_Global_Audio_Mid";
        private const string SHADER_TREBLE = "_Global_Audio_Treble";

        // =========================================================
        // 生命周期
        // =========================================================

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

            spectrumData = new float[spectrumSize];
        }

        void Start()
        {
            // 尝试自动查找AudioSource
            if (targetAudioSource == null)
            {
                // 优先从MusicManager获取（跨场景音乐）
                if (MusicManager.Instance != null)
                {
                    targetAudioSource = MusicManager.Instance.GetAudioSource();
                    if (targetAudioSource != null)
                    {
                        Debug.Log($"[AudioVisualizationManager] 已连接到MusicManager的AudioSource: {targetAudioSource.gameObject.name}");
                    }
                }

                // 如果MusicManager没有，尝试查找场景中的AudioSource
                if (targetAudioSource == null)
                {
                    targetAudioSource = FindFirstObjectByType<AudioSource>();
                    if (targetAudioSource != null)
                    {
                        Debug.Log($"[AudioVisualizationManager] 已自动连接到AudioSource: {targetAudioSource.gameObject.name}");
                    }
                    else
                    {
                        Debug.LogWarning("[AudioVisualizationManager] 未找到AudioSource，等待RhythmGameManager注入...");
                    }
                }
            }

            ValidateLaspSetup();
            InitializeGlobalShaderVariables();
        }

        void Update()
        {
            if (useLaspCapture && spectrumAnalyzer != null)
            {
                AnalyzeWithLasp();
            }
            else
            {
                AnalyzeWithAudioSource();
            }

            ApplySmoothing();
            UpdateGlobalShaderVariables();
        }

        // =========================================================
        // 公开接口：设置目标音频源
        // =========================================================

        /// <summary>
        /// 设置目标音频源（由RhythmGameManager调用）
        /// </summary>
        /// <param name="source">要分析的AudioSource</param>
        public void SetTargetAudioSource(AudioSource source)
        {
            if (source == null)
            {
                Debug.LogWarning("[AudioVisualizationManager] SetTargetAudioSource: 传入的AudioSource为null");
                return;
            }

            targetAudioSource = source;
            Debug.Log($"[AudioVisualizationManager] 已绑定到AudioSource: {source.gameObject.name} (Clip: {source.clip?.name ?? "null"})");

            // 重置能量值，避免残留数据
            rawBass = 0f;
            rawMid = 0f;
            rawTreble = 0f;
            Bass = 0f;
            Mid = 0f;
            Treble = 0f;
        }

        // =========================================================
        // 验证Lasp配置
        // =========================================================

        private void ValidateLaspSetup()
        {
            if (useLaspCapture && spectrumAnalyzer == null)
            {
                Debug.LogWarning("[AudioVisualizationManager] useLaspCapture为true但未配置SpectrumAnalyzer，将使用AudioSource分析");
                useLaspCapture = false;
            }
        }

        // =========================================================
        // Lasp频谱分析
        // =========================================================

        private void AnalyzeWithLasp()
        {
            if (spectrumAnalyzer == null)
            {
                rawBass = 0f;
                rawMid = 0f;
                rawTreble = 0f;
                return;
            }

            var spectrum = spectrumAnalyzer.spectrumSpan;
            if (spectrum.IsEmpty)
            {
                rawBass = 0f;
                rawMid = 0f;
                rawTreble = 0f;
                return;
            }

            int spectrumLength = spectrum.Length;
            int sampleRate = AudioSettings.outputSampleRate;

            int bassEnd = Mathf.FloorToInt(150f * spectrumLength / (sampleRate / 2f));
            int midStart = Mathf.FloorToInt(200f * spectrumLength / (sampleRate / 2f));
            int midEnd = Mathf.FloorToInt(500f * spectrumLength / (sampleRate / 2f));
            int trebleStart = Mathf.FloorToInt(500f * spectrumLength / (sampleRate / 2f));
            int trebleEnd = Mathf.FloorToInt(4000f * spectrumLength / (sampleRate / 2f));

            float bassSum = 0f;
            int bassCount = 0;
            for (int i = 0; i < bassEnd && i < spectrumLength; i++)
            {
                bassSum += spectrum[i];
                bassCount++;
            }
            rawBass = bassCount > 0 ? Mathf.Clamp01((bassSum / bassCount) * bassGain * normalizationFactor) : 0f;

            float midSum = 0f;
            int midCount = 0;
            for (int i = midStart; i < midEnd && i < spectrumLength; i++)
            {
                midSum += spectrum[i];
                midCount++;
            }
            rawMid = midCount > 0 ? Mathf.Clamp01((midSum / midCount) * midGain * normalizationFactor) : 0f;

            float trebleSum = 0f;
            int trebleCount = 0;
            for (int i = trebleStart; i < trebleEnd && i < spectrumLength; i++)
            {
                trebleSum += spectrum[i];
                trebleCount++;
            }
            rawTreble = trebleCount > 0 ? Mathf.Clamp01((trebleSum / trebleCount) * trebleGain * normalizationFactor) : 0f;
        }

        // =========================================================
        // AudioSource频谱分析
        // =========================================================

        private void AnalyzeWithAudioSource()
        {
            if (targetAudioSource == null || !targetAudioSource.isPlaying)
            {
                rawBass = 0f;
                rawMid = 0f;
                rawTreble = 0f;
                return;
            }

            targetAudioSource.GetSpectrumData(spectrumData, 0, FFTWindow.BlackmanHarris);

            int sampleRate = AudioSettings.outputSampleRate;

            int bassEnd = Mathf.FloorToInt(150f * spectrumSize / (sampleRate / 2f));
            int midStart = Mathf.FloorToInt(200f * spectrumSize / (sampleRate / 2f));
            int midEnd = Mathf.FloorToInt(500f * spectrumSize / (sampleRate / 2f));
            int trebleStart = Mathf.FloorToInt(500f * spectrumSize / (sampleRate / 2f));
            int trebleEnd = Mathf.FloorToInt(4000f * spectrumSize / (sampleRate / 2f));

            float bassSum = 0f;
            int bassCount = 0;
            for (int i = 0; i < bassEnd && i < spectrumSize; i++)
            {
                bassSum += spectrumData[i];
                bassCount++;
            }
            rawBass = bassCount > 0 ? Mathf.Clamp01((bassSum / bassCount) * bassGain * normalizationFactor) : 0f;

            float midSum = 0f;
            int midCount = 0;
            for (int i = midStart; i < midEnd && i < spectrumSize; i++)
            {
                midSum += spectrumData[i];
                midCount++;
            }
            rawMid = midCount > 0 ? Mathf.Clamp01((midSum / midCount) * midGain * normalizationFactor) : 0f;

            float trebleSum = 0f;
            int trebleCount = 0;
            for (int i = trebleStart; i < trebleEnd && i < spectrumSize; i++)
            {
                trebleSum += spectrumData[i];
                trebleCount++;
            }
            rawTreble = trebleCount > 0 ? Mathf.Clamp01((trebleSum / trebleCount) * trebleGain * normalizationFactor) : 0f;
        }

        // =========================================================
        // 平滑处理
        // =========================================================

        private void ApplySmoothing()
        {
            Bass = Mathf.Lerp(Bass, Mathf.Clamp01(rawBass), smoothSpeed);
            Mid = Mathf.Lerp(Mid, Mathf.Clamp01(rawMid), smoothSpeed);
            Treble = Mathf.Lerp(Treble, Mathf.Clamp01(rawTreble), smoothSpeed);
        }

        // =========================================================
        // 全局Shader变量更新
        // =========================================================

        private void InitializeGlobalShaderVariables()
        {
            Shader.SetGlobalFloat(SHADER_BASS, 0f);
            Shader.SetGlobalFloat(SHADER_MID, 0f);
            Shader.SetGlobalFloat(SHADER_TREBLE, 0f);
            Debug.Log("[AudioVisualizationManager] 全局Shader变量已初始化");
        }

        private void UpdateGlobalShaderVariables()
        {
            Shader.SetGlobalFloat(SHADER_BASS, Bass);
            Shader.SetGlobalFloat(SHADER_MID, Mid);
            Shader.SetGlobalFloat(SHADER_TREBLE, Treble);
        }

        // =========================================================
        // 调试工具
        // =========================================================

        [ContextMenu("打印当前频段值")]
        public void LogCurrentValues()
        {
            Debug.Log($"[AudioVisualizationManager] Bass: {Bass:F3}, Mid: {Mid:F3}, Treble: {Treble:F3}");
        }
    }
}
