using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OsuVR
{
    /// <summary>
    /// 场景流程管理器
    /// 管理游戏整体流程：主菜单 → 选歌 → 游戏 → 结算
    /// </summary>
    public class SceneFlowManager : MonoBehaviour
    {
        public static SceneFlowManager Instance { get; private set; }

        [Header("场景配置")]
        public string mainMenuScene = "MainMenuScene";
        public string songSelectScene = "SongSelectScene";
        public string gameScene = "GameScene";
        public string resultScene = "ResultScene";

        [Header("过渡效果")]
        public float fadeDuration = 0.5f;
        public AnimationCurve fadeCurve;

        public enum GameState
        {
            MainMenu,
            SongSelect,
            Playing,
            Result
        }

        public GameState CurrentState { get; private set; } = GameState.MainMenu;
        public bool IsTransitioning { get; private set; } = false;

        public event Action<GameState, GameState> OnStateChanged;

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

            if (fadeCurve == null || fadeCurve.length == 0)
            {
                fadeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
            }
        }

        void Start()
        {
            EnsureGameContext();
        }

        void EnsureGameContext()
        {
            if (GameContext.Instance == null)
            {
                new GameObject("GameContext").AddComponent<GameContext>();
            }
        }

        public void GoToMainMenu()
        {
            TransitionToScene(mainMenuScene, GameState.MainMenu);
        }

        public void GoToSongSelect()
        {
            TransitionToScene(songSelectScene, GameState.SongSelect);
        }

        public void GoToGame(string beatmapPath)
        {
            if (GameContext.Instance != null)
            {
                GameContext.Instance.SelectedBeatmapPath = beatmapPath;
                GameContext.Instance.CurrentBeatmapPath = beatmapPath;
            }
            TransitionToScene(gameScene, GameState.Playing);
        }

        public void GoToResult()
        {
            TransitionToScene(resultScene, GameState.Result);
        }

        public void RetryGame()
        {
            if (GameContext.Instance != null)
            {
                GameContext.Instance.ShouldRetry = true;
                GameContext.Instance.SelectedBeatmapPath = GameContext.Instance.CurrentBeatmapPath;
            }
            TransitionToScene(gameScene, GameState.Playing);
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void TransitionToScene(string sceneName, GameState newState)
        {
            if (IsTransitioning) return;
            StartCoroutine(TransitionCoroutine(sceneName, newState));
        }

        IEnumerator TransitionCoroutine(string sceneName, GameState newState)
        {
            IsTransitioning = true;

            var oldState = CurrentState;

            yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);

            CurrentState = newState;
            OnStateChanged?.Invoke(oldState, newState);

            if (GameContext.Instance != null)
            {
                if (newState == GameState.MainMenu)
                {
                    GameContext.Instance.ClearResult();
                }
            }

            IsTransitioning = false;
        }

        public string GetCurrentSceneName()
        {
            return SceneManager.GetActiveScene().name;
        }
    }
}
