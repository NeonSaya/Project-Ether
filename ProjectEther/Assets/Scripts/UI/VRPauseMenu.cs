using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using System.Collections;

namespace OsuVR
{
    /// <summary>
    /// VR暂停菜单控制器 - Prefab模式
    /// 支持BeatSaber风格的暂停菜单：Continue, Retry, Back to Menu
    /// 自动处理3秒倒计时继续游戏
    /// 使用对象池避免频繁Instantiate/Destroy
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    [RequireComponent(typeof(CanvasGroup))]
    public class VRPauseMenu : MonoBehaviour
    {
        [Header("按钮引用")]
        public Button continueButton;
        public Button retryButton;
        public Button backToMenuButton;

        [Header("倒计时显示")]
        public TextMeshProUGUI countdownText;
        public GameObject countdownPanel;

        [Header("音效")]
        public AudioSource audioSource;
        public AudioClip hoverSound;
        public AudioClip clickSound;

        [Header("引用")]
        [Tooltip("自动从场景中查找RhythmGameManager")]
        public RhythmGameManager gameManager;

        [Header("UI配置")]
        [Tooltip("暂停菜单固定位置")]
        public Vector3 fixedPosition = new Vector3(0f, 2.2f, 1.5f);
        
        [Tooltip("暂停菜单的旋转角度")]
        public Vector3 menuRotation = new Vector3(0f, 0f, 0f);

        private Canvas canvas;
        private CanvasGroup canvasGroup;
        private bool isPaused = false;
        private bool isCountingDown = false;
        private float countdownTimer = 0f;
        private const float COUNTDOWN_DURATION = 3f;

        void Awake()
        {
            canvas = GetComponent<Canvas>();
            canvasGroup = GetComponent<CanvasGroup>();

            // 确保Canvas设置正确
            if (canvas.renderMode != RenderMode.WorldSpace)
            {
                canvas.renderMode = RenderMode.WorldSpace;
            }
            canvas.sortingOrder = 100;

            // 确保UI在暂停时可交互
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.alpha = 1f;

            // 设置固定位置
            transform.position = fixedPosition;
            transform.rotation = Quaternion.Euler(menuRotation);

            SetupButtons();
            HideCountdown();
            gameObject.SetActive(false);
        }

        void Update()
        {
            if (isCountingDown)
            {
                UpdateCountdown();
            }
        }

        void SetupButtons()
        {
            if (continueButton != null)
            {
                continueButton.onClick.AddListener(OnContinueClicked);
                AddHoverEffect(continueButton);
            }

            if (retryButton != null)
            {
                retryButton.onClick.AddListener(OnRetryClicked);
                AddHoverEffect(retryButton);
            }

            if (backToMenuButton != null)
            {
                backToMenuButton.onClick.AddListener(OnBackToMenuClicked);
                AddHoverEffect(backToMenuButton);
            }
        }

        void AddHoverEffect(Button button)
        {
            var trigger = button.gameObject.AddComponent<EventTrigger>();

            var enterEntry = new EventTrigger.Entry
            {
                eventID = EventTriggerType.PointerEnter
            };
            enterEntry.callback.AddListener((_) => PlayHoverSound());
            trigger.triggers.Add(enterEntry);

            var exitEntry = new EventTrigger.Entry
            {
                eventID = EventTriggerType.PointerExit
            };
            exitEntry.callback.AddListener((_) => { /* 可以添加退出音效 */ });
            trigger.triggers.Add(exitEntry);
        }

        void PlayHoverSound()
        {
            if (audioSource != null && hoverSound != null)
                audioSource.PlayOneShot(hoverSound, 0.5f);
        }

        void PlayClickSound()
        {
            if (audioSource != null && clickSound != null)
                audioSource.PlayOneShot(clickSound, 0.8f);
        }

        /// <summary>
        /// 显示暂停菜单
        /// </summary>
        public void ShowPauseMenu()
        {
            // 自动查找GameManager（如果未设置）
            if (gameManager == null)
            {
                gameManager = FindObjectOfType<RhythmGameManager>();
            }

            isPaused = true;
            isCountingDown = false;
            
            // 设置固定位置
            transform.position = fixedPosition;
            transform.rotation = Quaternion.Euler(menuRotation);
            
            gameObject.SetActive(true);
            HideCountdown();

            // 暂停游戏
            if (gameManager != null)
            {
                gameManager.PauseGame();
            }

            // 确保UI可交互
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.alpha = 1f;
            
            Debug.Log($"[VRPauseMenu] 显示暂停菜单，位置: {transform.position}");
        }

        /// <summary>
        /// 隐藏暂停菜单
        /// </summary>
        public void HidePauseMenu()
        {
            isPaused = false;
            isCountingDown = false;
            gameObject.SetActive(false);
            HideCountdown();
        }

        void OnContinueClicked()
        {
            PlayClickSound();
            StartCountdown();
        }

        void OnRetryClicked()
        {
            PlayClickSound();
            HidePauseMenu();
            if (gameManager != null)
            {
                gameManager.RestartGame();
            }
        }

        void OnBackToMenuClicked()
        {
            PlayClickSound();
            HidePauseMenu();
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenuScene");
        }

        void StartCountdown()
        {
            isCountingDown = true;
            countdownTimer = COUNTDOWN_DURATION;
            ShowCountdown();
            UpdateCountdownDisplay();
        }

        void UpdateCountdown()
        {
            countdownTimer -= Time.deltaTime;
            UpdateCountdownDisplay();

            if (countdownTimer <= 0f)
            {
                FinishCountdown();
            }
        }

        void UpdateCountdownDisplay()
        {
            if (countdownText != null)
            {
                int seconds = Mathf.CeilToInt(countdownTimer);
                countdownText.text = seconds.ToString();
            }
        }

        void ShowCountdown()
        {
            if (countdownPanel != null)
            {
                countdownPanel.SetActive(true);
            }
            // 隐藏按钮
            if (continueButton != null) continueButton.gameObject.SetActive(false);
            if (retryButton != null) retryButton.gameObject.SetActive(false);
            if (backToMenuButton != null) backToMenuButton.gameObject.SetActive(false);
        }

        void HideCountdown()
        {
            if (countdownPanel != null)
            {
                countdownPanel.SetActive(false);
            }
            // 显示按钮
            if (continueButton != null) continueButton.gameObject.SetActive(true);
            if (retryButton != null) retryButton.gameObject.SetActive(true);
            if (backToMenuButton != null) backToMenuButton.gameObject.SetActive(true);
        }

        void FinishCountdown()
        {
            isCountingDown = false;
            HidePauseMenu();
            if (gameManager != null)
            {
                gameManager.ResumeGame();
            }
        }

        public bool IsPaused()
        {
            return isPaused || isCountingDown;
        }

        /// <summary>
        /// 重置菜单状态（用于对象池回收）
        /// </summary>
        public void ResetMenu()
        {
            HidePauseMenu();
            isPaused = false;
            isCountingDown = false;
            countdownTimer = 0f;
        }

        void OnDestroy()
        {
            // 清理TMP文本，避免字体资源引用错误
            var tmpComponents = GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var tmp in tmpComponents)
            {
                if (tmp != null)
                {
                    tmp.text = string.Empty;
                }
            }
        }
    }
}