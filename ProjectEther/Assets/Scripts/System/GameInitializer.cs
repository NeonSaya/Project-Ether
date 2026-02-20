using UnityEngine;

namespace OsuVR
{
    /// <summary>
    /// 游戏启动初始化器
    /// 自动创建所有必要的管理器实例
    /// </summary>
    public class GameInitializer : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void InitializeGame()
        {
            CreateGameContext();
            CreateSceneFlowManager();
        }

        static void CreateGameContext()
        {
            if (GameContext.Instance == null)
            {
                var go = new GameObject("[GameContext]");
                go.AddComponent<GameContext>();
                Debug.Log("[GameInitializer] GameContext 已创建");
            }
        }

        static void CreateSceneFlowManager()
        {
            if (SceneFlowManager.Instance == null)
            {
                var go = new GameObject("[SceneFlowManager]");
                go.AddComponent<SceneFlowManager>();
                Debug.Log("[GameInitializer] SceneFlowManager 已创建");
            }
        }
    }
}
