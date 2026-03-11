using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace OsuVR
{
    /// <summary>
    /// 简化版主菜单控制器
    /// Beat Saber 风格，简洁实用
    /// </summary>
    public class SimpleMainMenu : MonoBehaviour
    {
        [Header("按钮引用")]
        public Button playButton;
        public Button settingsButton;
        public Button creditsButton;
        public Button quitButton;

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
