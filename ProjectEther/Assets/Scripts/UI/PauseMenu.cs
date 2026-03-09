using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

namespace OsuVR
{
    /// <summary>
    /// 暂停菜单控制器 - BeatSaber风格
    /// 支持Back to Menu, Retry, Continue三个选项
    /// </summary>
    public class PauseMenu : MonoBehaviour
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
        public RhythmGameManager gameManager;

        private bool isPaused = false;
        private bool isCountingDown = false;
        private float countdownTimer = 0f;
        private const float COUNTDOWN_DURATION = 3f;

        void Start()
        {
            SetupButtons();
            HideCountdown();
            gameObject.SetActive(false); // 默认隐藏
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
            var trigger = button.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();

            var enterEntry = new UnityEngine.EventSystems.EventTrigger.Entry
            {
                eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter
            };
            enterEntry.callback.AddListener((_) => PlayHoverSound());
            trigger.triggers.Add(enterEntry);
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

        public void ShowPauseMenu()
        {
            if (gameManager == null || !gameManager.isPlaying)
                return;

            isPaused = true;
            isCountingDown = false;
            gameObject.SetActive(true);
            HideCountdown();

            // 暂停游戏
            gameManager.PauseGame();
        }

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
            SceneManager.LoadScene("MainMenuScene");
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
    }
}