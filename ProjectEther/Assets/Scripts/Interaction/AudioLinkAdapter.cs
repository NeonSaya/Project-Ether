using UnityEngine;

namespace OsuVR
{
    /// <summary>
    /// AudioLink适配器：桥接AudioLink系统与Project Ether音频可视化架构
    /// 
    /// 功能职责：
    /// 1. 自动同步AudioLink的音频源与AudioVisualizationManager
    /// 2. 处理歌曲切换时的音频流更新
    /// 3. 提供AudioLink全局纹理访问接口
    /// 
    /// 全局纹理说明：
    /// - AudioLink生成的全局纹理名称：_AudioTexture
    /// - 在Shader Graph中可通过Property节点访问（Mode: Global）
    /// - 纹理格式：CustomRenderTexture，包含频谱、波形、自相关等数据
    /// 
    /// 使用方法：
    /// 1. 在场景中放置AudioLink预制体
    /// 2. 将此脚本挂载到AudioLink物体上
    /// 3. 系统会自动同步音频源
    /// </summary>
    public class AudioLinkAdapter : MonoBehaviour
    {
        [Header("AudioLink引用")]
        [Tooltip("AudioLink核心组件（自动查找）")]
        public MonoBehaviour audioLinkComponent;

        [Header("同步设置")]
        [Tooltip("是否自动同步音频源")]
        public bool autoSync = true;

        [Tooltip("同步间隔（秒）")]
        [Range(0.1f, 5.0f)]
        public float syncInterval = 1.0f;

        [Header("调试")]
        [Tooltip("是否显示调试日志")]
        public bool enableDebugLog = true;

        // 内部状态
        private AudioSource currentAudioSource;
        private float lastSyncTime;
        private System.Reflection.FieldInfo audioSourceField;
        private System.Reflection.MethodInfo updateAudioSourceMethod;

        // 全局纹理名称（供Shader使用）
        public const string AUDIO_LINK_TEXTURE_NAME = "_AudioTexture";

        // 单例模式（可选）
        public static AudioLinkAdapter Instance { get; private set; }

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        void Start()
        {
            FindAudioLinkComponent();
            CacheReflectionInfo();
            
            if (autoSync)
            {
                SyncAudioSource();
            }
        }

        void Update()
        {
            if (!autoSync)
                return;

            // 定期同步音频源
            if (Time.time - lastSyncTime >= syncInterval)
            {
                SyncAudioSource();
                lastSyncTime = Time.time;
            }

            // 检查音频源是否正在播放，如果是且当前源不匹配，立即同步
            CheckPlayingAudioSource();
        }

        /// <summary>
        /// 检查是否有新的音频源正在播放，及时同步
        /// </summary>
        private void CheckPlayingAudioSource()
        {
            AudioSource targetSource = GetTargetAudioSource();
            if (targetSource != null && targetSource.isPlaying && currentAudioSource != targetSource)
            {
                SyncAudioSource();
            }
        }

        // =========================================================
        // 初始化
        // =========================================================

        private void FindAudioLinkComponent()
        {
            // 获取AudioLink类型（只获取一次）
            var audioLinkType = System.Type.GetType("AudioLink.AudioLink, AudioLink");

            // 检查现有引用是否有效（必须是AudioLink类型）
            if (audioLinkComponent != null)
            {
                if (audioLinkType != null && audioLinkType.IsInstanceOfType(audioLinkComponent))
                {
                    // 引用正确，无需重新查找
                    if (enableDebugLog)
                    {
                        Debug.Log($"[AudioLinkAdapter] AudioLink组件已配置: {audioLinkComponent.gameObject.name}");
                    }
                    return;
                }
                else
                {
                    // 引用错误（可能是Inspector中配置失误），清除并重新查找
                    Debug.LogWarning($"[AudioLinkAdapter] audioLinkComponent引用错误（类型: {audioLinkComponent.GetType().Name}），重新查找AudioLink...");
                    audioLinkComponent = null;
                }
            }

            // 尝试查找AudioLink组件（通过反射，因为AudioLink可能不存在）
            if (audioLinkType != null)
            {
                audioLinkComponent = GetComponent(audioLinkType) as MonoBehaviour;
                if (audioLinkComponent == null)
                {
                    audioLinkComponent = FindObjectOfType(audioLinkType) as MonoBehaviour;
                }

                if (audioLinkComponent != null && enableDebugLog)
                {
                    Debug.Log($"[AudioLinkAdapter] 找到AudioLink组件: {audioLinkComponent.gameObject.name}");
                }
            }
            else
            {
                if (enableDebugLog)
                {
                    Debug.LogWarning("[AudioLinkAdapter] 未找到AudioLink组件。请确保已安装AudioLink包。");
                }
            }
        }

        private void CacheReflectionInfo()
        {
            if (audioLinkComponent == null)
                return;

            var audioLinkType = audioLinkComponent.GetType();

            // 缓存audioSource字段
            audioSourceField = audioLinkType.GetField("audioSource",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            // 缓存更新方法（如果存在）
            updateAudioSourceMethod = audioLinkType.GetMethod("UpdateAudioSource",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        }

        // =========================================================
        // 音频源同步
        // =========================================================

        /// <summary>
        /// 同步AudioLink的音频源与AudioVisualizationManager
        /// </summary>
        public void SyncAudioSource()
        {
            if (audioLinkComponent == null)
            {
                FindAudioLinkComponent();
                if (audioLinkComponent == null)
                    return;
            }

            // 获取AudioVisualizationManager的目标音频源
            AudioSource targetSource = GetTargetAudioSource();
            if (targetSource == null)
            {
                if (enableDebugLog)
                {
                    Debug.LogWarning("[AudioLinkAdapter] 未找到目标音频源");
                }
                return;
            }

            // 检查是否需要更新
            if (currentAudioSource == targetSource)
                return;

            // 通过反射设置AudioLink的音频源
            if (audioSourceField != null)
            {
                try
                {
                    audioSourceField.SetValue(audioLinkComponent, targetSource);
                    currentAudioSource = targetSource;

                    if (enableDebugLog)
                    {
                        Debug.Log($"[AudioLinkAdapter] 已同步音频源: {targetSource.gameObject.name} (Clip: {targetSource.clip?.name ?? "null"})");
                    }

                    // 调用更新方法（如果存在）
                    if (updateAudioSourceMethod != null)
                    {
                        updateAudioSourceMethod.Invoke(audioLinkComponent, null);
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[AudioLinkAdapter] 设置音频源失败: {e.Message}");
                }
            }
        }

        /// <summary>
        /// 获取目标音频源（优先级：AudioVisualizationManager > MusicManager > 场景中的AudioSource）
        /// </summary>
        private AudioSource GetTargetAudioSource()
        {
            // 优先级1：从AudioVisualizationManager获取
            if (AudioVisualizationManager.Instance != null)
            {
                var source = AudioVisualizationManager.Instance.targetAudioSource;
                if (source != null)
                    return source;
            }

            // 优先级2：从MusicManager获取
            if (MusicManager.Instance != null)
            {
                var source = MusicManager.Instance.GetAudioSource();
                if (source != null)
                    return source;
            }

            // 优先级3：查找场景中的AudioSource
            var sceneSource = FindFirstObjectByType<AudioSource>();
            if (sceneSource != null)
            {
                if (enableDebugLog)
                {
                    Debug.Log($"[AudioLinkAdapter] 使用场景中的AudioSource: {sceneSource.gameObject.name}");
                }
                return sceneSource;
            }

            return null;
        }

        // =========================================================
        // 公开接口
        // =========================================================

        /// <summary>
        /// 手动设置AudioLink的音频源
        /// </summary>
        /// <param name="source">目标音频源</param>
        public void SetAudioSource(AudioSource source)
        {
            if (source == null)
            {
                Debug.LogWarning("[AudioLinkAdapter] SetAudioSource: 传入的AudioSource为null");
                return;
            }

            if (audioSourceField != null)
            {
                try
                {
                    audioSourceField.SetValue(audioLinkComponent, source);
                    currentAudioSource = source;

                    if (enableDebugLog)
                    {
                        Debug.Log($"[AudioLinkAdapter] 手动设置音频源: {source.gameObject.name}");
                    }

                    if (updateAudioSourceMethod != null)
                    {
                        updateAudioSourceMethod.Invoke(audioLinkComponent, null);
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[AudioLinkAdapter] 手动设置音频源失败: {e.Message}");
                }
            }
        }

        /// <summary>
        /// 获取AudioLink全局纹理名称
        /// </summary>
        /// <returns>纹理名称：_AudioTexture</returns>
        public static string GetAudioLinkTextureName()
        {
            return AUDIO_LINK_TEXTURE_NAME;
        }

        /// <summary>
        /// 检查AudioLink是否可用
        /// </summary>
        public bool IsAudioLinkAvailable()
        {
            return audioLinkComponent != null;
        }

        // =========================================================
        // 调试工具
        // =========================================================

        [ContextMenu("手动同步音频源")]
        public void ManualSync()
        {
            SyncAudioSource();
        }

        [ContextMenu("打印AudioLink状态")]
        public void LogStatus()
        {
            Debug.Log($"[AudioLinkAdapter] 状态报告:");
            Debug.Log($"  - AudioLink组件: {(audioLinkComponent != null ? "已找到" : "未找到")}");
            Debug.Log($"  - 当前音频源: {(currentAudioSource != null ? currentAudioSource.gameObject.name : "null")}");
            Debug.Log($"  - 自动同步: {autoSync}");
            Debug.Log($"  - 全局纹理名称: {AUDIO_LINK_TEXTURE_NAME}");
        }
    }
}
