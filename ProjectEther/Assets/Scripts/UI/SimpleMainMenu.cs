using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace OsuVR
{
    public class SimpleMainMenu : MonoBehaviour
    {
        [Header("按钮引用")]
        public Button playButton;
        public Button settingsButton;
        public Button creditsButton;
        public Button quitButton;

        [Header("按钮文本引用")]
        public TextMeshProUGUI playButtonText;
        public TextMeshProUGUI settingsButtonText;
        public TextMeshProUGUI creditsButtonText;
        public TextMeshProUGUI quitButtonText;

        [Header("标题")]
        public TextMeshProUGUI titleText;
        public TextMeshProUGUI versionText;

        [Header("音效")]
        public AudioSource audioSource;
        public AudioClip hoverSound;
        public AudioClip clickSound;

        [Header("版本信息")]
        public string gameTitle = "Project Ether";
        public string subtitle = "以太计划";
        public string version = "Demo v0.1";

        void Start()
        {
            SetupButtons();
            SetupTitle();
            UpdateButtonTexts();
        }

        void OnEnable()
        {
            LocalizationManager.OnLanguageChanged += OnLanguageChanged;
        }

        void OnDisable()
        {
            LocalizationManager.OnLanguageChanged -= OnLanguageChanged;
        }

        private void OnLanguageChanged()
        {
            UpdateButtonTexts();
        }

        private void UpdateButtonTexts()
        {
            if (playButtonText != null)
                playButtonText.text = LocalizationManager.GetText("ui_play_button");
            if (settingsButtonText != null)
                settingsButtonText.text = LocalizationManager.GetText("ui_settings");
            if (creditsButtonText != null)
                creditsButtonText.text = LocalizationManager.GetText("ui_credits");
            if (quitButtonText != null)
                quitButtonText.text = LocalizationManager.GetText("ui_quit");
        }

        void SetupButtons()
        {
            if (playButton != null)
            {
                playButton.onClick.AddListener(OnPlayClicked);
                AddHoverEffect(playButton);
            }

            if (settingsButton != null)
            {
                settingsButton.onClick.AddListener(OnSettingsClicked);
                AddHoverEffect(settingsButton);
            }

            if (creditsButton != null)
            {
                creditsButton.onClick.AddListener(OnCreditsClicked);
                AddHoverEffect(creditsButton);
            }

            if (quitButton != null)
            {
                quitButton.onClick.AddListener(OnQuitClicked);
                AddHoverEffect(quitButton);
            }
        }

        void SetupTitle()
        {
            if (titleText != null)
            {
                titleText.text = $"{gameTitle}\n<size=60%>{subtitle}</size>";
            }

            if (versionText != null)
            {
                versionText.text = version;
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

        void OnPlayClicked()
        {
            PlayClickSound();
            SceneManager.LoadScene("SongSelectScene");
        }

        void OnSettingsClicked()
        {
            PlayClickSound();
            SceneManager.LoadScene("SettingsScene");
        }

        void OnCreditsClicked()
        {
            PlayClickSound();
            Debug.Log("[MainMenu] Credits - 待实现");
        }

        void OnQuitClicked()
        {
            PlayClickSound();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
