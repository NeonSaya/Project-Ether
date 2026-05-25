using UnityEngine;

namespace OsuVR
{
    /// <summary>
    /// 场景流程管理器
    /// 管理游戏整体流程：主菜单 → 选歌 → 游戏 → 结算
    /// </summary>
    public class SceneFlowManager : MonoBehaviour
    {
        public static SceneFlowManager Instance { get; private set; }

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
            }
        }
    }
}
