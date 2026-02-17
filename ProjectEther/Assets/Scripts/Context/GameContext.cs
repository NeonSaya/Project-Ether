using UnityEngine;

namespace OsuVR
{
    /// <summary>
    /// 全局数据容器：用于跨场景传递数据
    /// 本质上是一个"中转站"，选歌场景选中的路径传递给游戏场景
    /// </summary>
    public class GameContext : MonoBehaviour
    {
        public static GameContext Instance { get; private set; }

        // =========================================================
        // 跨场景数据
        // =========================================================

        /// <summary>
        /// 当前选中的 .osu 文件绝对路径
        /// </summary>
        public string SelectedBeatmapPath { get; set; }

        /// <summary>
        /// 最近一次游戏的结算数据
        /// </summary>
        public ResultData LastResult { get; set; }

        /// <summary>
        /// 是否需要重试 (用于场景切换时判断)
        /// </summary>
        public bool ShouldRetry { get; set; } = false;

        // =========================================================
        // 场景配置
        // =========================================================

        /// <summary>
        /// 菜单场景名称
        /// </summary>
        public string MenuSceneName { get; set; } = "MenuScene";

        /// <summary>
        /// 游戏场景名称
        /// </summary>
        public string GameSceneName { get; set; } = "SampleScene";

        /// <summary>
        /// 结算场景名称
        /// </summary>
        public string ResultSceneName { get; set; } = "ResultScene";

        /// <summary>
        /// 当前游玩的谱面路径 (用于重试)
        /// </summary>
        public string CurrentBeatmapPath { get; set; }

        // =========================================================
        // 生命周期
        // =========================================================

        void Awake()
        {
            // 单例模式：保证全局只有一个实例
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);

                // 初始化 MusicManager（确保它存在）
                if (MusicManager.Instance == null)
                {
                    gameObject.AddComponent<MusicManager>();
                }
            }
            else
            {
                Destroy(gameObject);
            }
        }

        void OnEnable()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDisable()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        /// <summary>
        /// 场景加载完成回调
        /// </summary>
        private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            // 如果是重试，清除标记
            if (scene.name == GameSceneName && ShouldRetry)
            {
                ShouldRetry = false;
            }
        }

        // =========================================================
        // 公开接口
        // =========================================================

        /// <summary>
        /// 清除结算数据
        /// </summary>
        public void ClearResult()
        {
            LastResult = null;
        }
    }
}
