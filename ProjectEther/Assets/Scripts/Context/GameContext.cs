using UnityEngine;

namespace OsuVR
{
    /// <summary>
    /// 全局单例：用于跨场景传递数据
    /// 它就像一个"接力棒"，把选歌场景选中的路径传给游戏场景
    /// </summary>
    public class GameContext : MonoBehaviour
    {
        public static GameContext Instance { get; private set; }

        // 核心数据：当前选中的 .osu 文件绝对路径
        public string SelectedBeatmapPath { get; set; }

        void Awake()
        {
            // 保证全场只有一个，且切场景不销毁
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}