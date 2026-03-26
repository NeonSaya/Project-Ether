using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using System.Collections;

namespace OsuVR
{
    [RequireComponent(typeof(Canvas))]
    [RequireComponent(typeof(CanvasGroup))]
    public class VRPauseMenu : MonoBehaviour
    {
        [Header("按钮引用")]
        public Button continueButton;
        public Button retryButton;
        public Button backToMenuButton;

        [Header("按钮文本引用")]
        public TextMeshProUGUI continueButtonText;
        public TextMeshProUGUI retryButtonText;
        public TextMeshProUGUI backToMenuButtonText;

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

            AutoAttachLocalizedTexts();

            if (canvas.renderMode != RenderMode.WorldSpace)
            {
                canvas.renderMode = RenderMode.WorldSpace;
            }
            canvas.sortingOrder = 100;

            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.alpha = 1f;

            transform.position = fixedPosition;
            transform.rotation = Quaternion.Euler(menuRotation);

            SetupButtons();
            HideCountdown();
            gameObject.SetActive(false);
        }

        void Start()
        {
            LocalizationManager.ReloadAndNotify();
        }

        void OnEnable()
        {
            LocalizationManager.OnLanguageChanged += OnLanguageChanged;
            UpdateButtonTexts();
        }

        void OnDisable()
        {
            LocalizationManager.OnLanguageChanged -= OnLanguageChanged;
        }

        private void AutoAttachLocalizedTexts()
        {
            var allTexts = GetComponentsInChildren<TextMeshProUGUI>(true);
            var mapping = new System.Collections.Generic.Dictionary<string, string>
            {
                { "PAUSED", "ui_pause" },
                { "Continue", "ui_resume" },
                { "Retry", "ui_retry" },
                { "Back to Menu", "ui_main_menu" }
            };

            foreach (var text in allTexts)
            {
                if (mapping.TryGetValue(text.text, out string key))
                {
                    if (text.GetComponent<LocalizedText>() == null)
                    {
                        var lt = text.gameObject.AddComponent<LocalizedText>();
                        lt.localizationKey = key;
                    }
                }
            }
        }

        private void OnLanguageChanged()
        {
            UpdateButtonTexts();
        }

        private void UpdateButtonTexts()
        {
            if (continueButtonText != null)
                continueButtonText.text = LocalizationManager.GetText("ui_resume");
            if (retryButtonText != null)
                retryButtonText.text = LocalizationManager.GetText("ui_retry");
            if (backToMenuButtonText != null)
                backToMenuButtonText.text = LocalizationManager.GetText("ui_main_menu");
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
            exitEntry.callback.AddListener((_) => { });
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

        public void ShowPauseMenu()
        {
            if (gameManager == null)
            {
                gameManager = FindObjectOfType<RhythmGameManager>();
            }

            isPaused = true;
            isCountingDown = false;
            
            transform.position = fixedPosition;
            transform.rotation = Quaternion.Euler(menuRotation);
            
            gameObject.SetActive(true);
            HideCountdown();

            if (gameManager != null)
            {
                gameManager.PauseGame();
            }

            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.alpha = 1f;
            
            UpdateButtonTexts();
            
            Debug.Log($"[VRPauseMenu] 显示暂停菜单，位置: {transform.position}");
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

        public void ResetMenu()
        {
            HidePauseMenu();
            isPaused = false;
            isCountingDown = false;
            countdownTimer = 0f;
        }

        void OnDestroy()
        {
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