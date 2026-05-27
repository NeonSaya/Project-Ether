using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

namespace OsuVR
{
    /// <summary>
    /// X-PostProcessing 全局管理器。
    /// 自动为 Main Camera 添加 PostProcessLayer，创建全局 PostProcessVolume。
    /// 提供运行时启用/禁用效果的 API。
    /// </summary>
    public class PostProcessManager : MonoBehaviour
    {
        public static PostProcessManager Instance { get; private set; }

        [Header("默认 Profile")]
        [Tooltip("如果为空，会在运行时创建空 Profile")]
        public PostProcessProfile defaultProfile;

        private PostProcessLayer _layer;
        private PostProcessVolume _volume;
        private PostProcessProfile _runtimeProfile;
        private bool _cameraReady;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            Initialize();
        }

        private void Initialize()
        {
            // 1. 创建全局 PostProcessVolume（立即可用）
            _volume = GetComponent<PostProcessVolume>();
            if (_volume == null)
            {
                _volume = gameObject.AddComponent<PostProcessVolume>();
            }
            _volume.isGlobal = true;
            _volume.priority = 1f;

            // 2. 初始化 Profile
            if (defaultProfile != null)
            {
                _runtimeProfile = Instantiate(defaultProfile);
            }
            else
            {
                _runtimeProfile = ScriptableObject.CreateInstance<PostProcessProfile>();
            }
            _volume.profile = _runtimeProfile;

            // 3. 尝试立即设置 Camera，失败则启动协程等待
            if (!TrySetupCamera())
            {
                StartCoroutine(WaitForCamera());
            }

            Debug.Log("[PostProcessManager] 初始化完成");
        }

        private bool TrySetupCamera()
        {
            if (_cameraReady) return true;

            var mainCam = Camera.main;
            if (mainCam == null) return false;

            _layer = mainCam.GetComponent<PostProcessLayer>();
            if (_layer == null)
            {
                _layer = mainCam.gameObject.AddComponent<PostProcessLayer>();
                _layer.volumeTrigger = mainCam.transform;
                _layer.volumeLayer = ~0; // 检测所有 Layer
                _layer.antialiasingMode = PostProcessLayer.Antialiasing.FastApproximateAntialiasing;
                Debug.Log("[PostProcessManager] PostProcessLayer 已添加到 Main Camera");
            }
            _cameraReady = true;
            return true;
        }

        private IEnumerator WaitForCamera()
        {
            Debug.Log("[PostProcessManager] 等待 Main Camera 就绪...");
            float timeout = 10f;
            float elapsed = 0f;

            while (!_cameraReady && elapsed < timeout)
            {
                yield return null;
                elapsed += Time.unscaledDeltaTime;
                TrySetupCamera();
            }

            if (!_cameraReady)
            {
                Debug.LogError("[PostProcessManager] 超时：找不到 Main Camera，PostProcessLayer 未初始化");
            }
        }

        /// <summary>
        /// 启用或禁用指定类型的效果
        /// </summary>
        public void SetEffectEnabled<T>(bool enabled) where T : PostProcessEffectSettings
        {
            if (_runtimeProfile == null) return;

            var settings = _runtimeProfile.GetSetting<T>();
            if (settings != null)
            {
                settings.enabled.value = enabled;
            }
        }

        /// <summary>
        /// 添加效果到 Profile（如果不存在则创建）
        /// </summary>
        public T AddEffect<T>() where T : PostProcessEffectSettings
        {
            if (_runtimeProfile == null) return null;

            var settings = _runtimeProfile.GetSetting<T>();
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<T>();
                settings.enabled.Override(true);
                _runtimeProfile.AddSettings(settings);
            }
            return settings;
        }

        /// <summary>
        /// 从 Profile 移除效果
        /// </summary>
        public void RemoveEffect<T>() where T : PostProcessEffectSettings
        {
            if (_runtimeProfile == null) return;

            var settings = _runtimeProfile.GetSetting<T>();
            if (settings != null)
            {
                _runtimeProfile.RemoveSettings<T>();
            }
        }

        /// <summary>
        /// 获取当前 Profile（用于外部直接修改参数）
        /// </summary>
        public PostProcessProfile GetProfile()
        {
            return _runtimeProfile;
        }

        /// <summary>
        /// 获取 PostProcessLayer（用于修改 AA、雾效等全局设置）
        /// </summary>
        public PostProcessLayer GetLayer()
        {
            return _layer;
        }
    }
}
