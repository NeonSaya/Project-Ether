using UnityEngine;

namespace OsuVR
{
    public class GameInitializer : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void InitializeGame()
        {
            CreateGameContext();
            CreateUnicodeFontLoader();
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

        static void CreateUnicodeFontLoader()
        {
            if (UnicodeFontLoader.Instance == null)
            {
                var go = new GameObject("[UnicodeFontLoader]");
                go.AddComponent<UnicodeFontLoader>();
                Debug.Log("[GameInitializer] UnicodeFontLoader 已创建");
            }
        }
    }
}
