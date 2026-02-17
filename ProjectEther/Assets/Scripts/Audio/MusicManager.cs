using UnityEngine;
using UnityEngine.SceneManagement;

namespace OsuVR
{
    /// <summary>
    /// 音乐管理器：处理跨场景音乐播放
    /// 在游戏结束时保持音乐继续播放，返回菜单时停止
    /// </summary>
    public class MusicManager : MonoBehaviour
    {
        public static MusicManager Instance { get; private set; }

        private AudioSource audioSource;
        private bool shouldPersist = false;

        // =========================================================
        // 生命周期
        // =========================================================

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

        void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        /// <summary>
        /// 场景加载回调：返回菜单时停止音乐并销毁
        /// </summary>
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // 如果返回菜单场景，停止音乐
            if (GameContext.Instance != null && scene.name == GameContext.Instance.MenuSceneName)
            {
                StopAndDestroy();
            }
        }

        // =========================================================
        // 公开接口
        // =========================================================

        /// <summary>
        /// 设置音频源引用（从 RhythmGameManager 传入）
        /// </summary>
        public void SetAudioSource(AudioSource source)
        {
            audioSource = source;
        }

        /// <summary>
        /// 标记音乐需要跨场景保持
        /// </summary>
        public void SetPersist(bool persist)
        {
            shouldPersist = persist;

            if (persist && audioSource != null)
            {
                // 将 AudioSource 的 GameObject 移动到持久化管理器下
                audioSource.transform.SetParent(null);
                DontDestroyOnLoad(audioSource.gameObject);
            }
        }

        /// <summary>
        /// 停止音乐并销毁
        /// </summary>
        public void StopAndDestroy()
        {
            if (audioSource != null)
            {
                audioSource.Stop();
                Destroy(audioSource.gameObject);
                audioSource = null;
            }

            shouldPersist = false;
        }

        /// <summary>
        /// 获取当前音频源
        /// </summary>
        public AudioSource GetAudioSource()
        {
            return audioSource;
        }

        /// <summary>
        /// 音乐是否正在播放
        /// </summary>
        public bool IsPlaying()
        {
            return audioSource != null && audioSource.isPlaying;
        }
    }
}
