using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace OsuVR
{
    /// <summary>
    /// 主菜单控制器
    /// Beat Saber 风格 VR 世界空间 UI
    /// 按钮：Play / Settings / Credits / Quit
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [Header("UI 容器")]
        [Tooltip("主菜单面板，放置在玩家前方")]
        public Transform menuPanel;

        [Tooltip("按钮容器")]
        public Transform buttonContainer;

        [Header("按钮预制体")]
        [Tooltip("菜单按钮预制体")]
        public GameObject menuButtonPrefab;

        [Header("布局设置")]
        public float buttonSpacing = 0.15f;
        public float panelDistance = 3f;
        public float panelHeight = 1.5f;

        [Header("动画设置")]
        public float buttonHoverScale = 1.1f;
        public float buttonAnimSpeed = 8f;
        public float panelFadeInDuration = 0.5f;

        [Header("标题")]
        public TextMeshProUGUI titleText;
        public string gameTitle = "Project Ether";
        public string subtitle = "以太计划";

        [Header("版本信息")]
        public TextMeshProUGUI versionText;
        public string version = "Demo v0.1";

        [Header("音效")]
        public AudioClip hoverSound;
        public AudioClip clickSound;
        public AudioSource audioSource;

        private MenuButton[] menuButtons;
        private int currentIndex = 0;
        private bool isInitialized = false;

        private class MenuButton
        {
            public GameObject gameObject;
            public Transform transform;
            public Button button;
            public TextMeshProUGUI text;
            public Image background;
            public CanvasGroup canvasGroup;
            public Vector3 originalScale;
            public bool isHovered;
        }

        void Start()
        {
            StartCoroutine(InitializeMenu());
        }

        IEnumerator InitializeMenu()
        {
            yield return null;

            PositionPanel();
            CreateButtons();
            SetupTitle();

            isInitialized = true;

            yield return StartCoroutine(FadeInAnimation());
        }

        void PositionPanel()
        {
            if (menuPanel == null) return;

            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                Vector3 forward = mainCam.transform.forward;
                forward.y = 0;
                forward.Normalize();

                menuPanel.position = mainCam.transform.position + forward * panelDistance + Vector3.up * panelHeight;
                menuPanel.rotation = Quaternion.LookRotation(forward, Vector3.up);
            }
        }

        void CreateButtons()
        {
            if (buttonContainer == null || menuButtonPrefab == null)
            {
                CreateDefaultButtons();
                return;
            }

            var buttonData = new (string, string, System.Action)[]
            {
                ("Play", "开始游戏", OnPlayClicked),
                ("Settings", "设置", OnSettingsClicked),
                ("Credits", "制作名单", OnCreditsClicked),
                ("Quit", "退出", OnQuitClicked)
            };

            menuButtons = new MenuButton[buttonData.Length];

            for (int i = 0; i < buttonData.Length; i++)
            {
                var data = buttonData[i];
                GameObject btnObj = Instantiate(menuButtonPrefab, buttonContainer);
                btnObj.transform.localPosition = Vector3.down * i * buttonSpacing;
                btnObj.transform.localRotation = Quaternion.identity;
                btnObj.name = $"Btn_{data.Item1}";

                var menuBtn = new MenuButton
                {
                    gameObject = btnObj,
                    transform = btnObj.transform,
                    originalScale = btnObj.transform.localScale
                };

                menuBtn.button = btnObj.GetComponent<Button>();
                if (menuBtn.button == null)
                    menuBtn.button = btnObj.AddComponent<Button>();

                menuBtn.text = btnObj.GetComponentInChildren<TextMeshProUGUI>();
                if (menuBtn.text != null)
                    menuBtn.text.text = data.Item1;

                menuBtn.background = btnObj.GetComponent<Image>();
                menuBtn.canvasGroup = btnObj.GetComponent<CanvasGroup>();
                if (menuBtn.canvasGroup == null)
                    menuBtn.canvasGroup = btnObj.AddComponent<CanvasGroup>();

                int index = i;
                menuBtn.button.onClick.AddListener(() =>
                {
                    PlayClickSound();
                    data.Item3.Invoke();
                });

                AddHoverEvents(btnObj, menuBtn, index);

                menuButtons[i] = menuBtn;
            }
        }

        void CreateDefaultButtons()
        {
            menuButtons = new MenuButton[4];
            string[] names = { "Play", "Settings", "Credits", "Quit" };
            System.Action[] actions = { OnPlayClicked, OnSettingsClicked, OnCreditsClicked, OnQuitClicked };

            for (int i = 0; i < 4; i++)
            {
                GameObject btnObj = new GameObject($"Btn_{names[i]}");
                btnObj.transform.SetParent(buttonContainer != null ? buttonContainer : transform);
                btnObj.transform.localPosition = Vector3.down * i * buttonSpacing;
                btnObj.transform.localRotation = Quaternion.identity;
                btnObj.transform.localScale = new Vector3(1f, 0.15f, 0.02f);

                var menuBtn = new MenuButton
                {
                    gameObject = btnObj,
                    transform = btnObj.transform,
                    originalScale = btnObj.transform.localScale
                };

                var bg = btnObj.AddComponent<Image>();
                bg.color = new Color(0.1f, 0.1f, 0.15f, 0.9f);
                menuBtn.background = bg;

                var btn = btnObj.AddComponent<Button>();
                int index = i;
                btn.onClick.AddListener(() =>
                {
                    PlayClickSound();
                    actions[index].Invoke();
                });
                menuBtn.button = btn;

                var cg = btnObj.AddComponent<CanvasGroup>();
                menuBtn.canvasGroup = cg;

                var textObj = new GameObject("Text");
                textObj.transform.SetParent(btnObj.transform);
                textObj.transform.localPosition = Vector3.zero;
                textObj.transform.localScale = Vector3.one;

                var tmp = textObj.AddComponent<TextMeshProUGUI>();
                tmp.text = names[i];
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.fontSize = 0.08f;
                tmp.color = Color.white;
                menuBtn.text = tmp;

                menuButtons[i] = menuBtn;
            }
        }

        void AddHoverEvents(GameObject btnObj, MenuButton menuBtn, int index)
        {
            var trigger = btnObj.GetComponent<UnityEngine.EventSystems.EventTrigger>();
            if (trigger == null)
                trigger = btnObj.AddComponent<UnityEngine.EventSystems.EventTrigger>();

            var enterEntry = new UnityEngine.EventSystems.EventTrigger.Entry
            {
                eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter
            };
            enterEntry.callback.AddListener((_) =>
            {
                menuBtn.isHovered = true;
                currentIndex = index;
                PlayHoverSound();
            });
            trigger.triggers.Add(enterEntry);

            var exitEntry = new UnityEngine.EventSystems.EventTrigger.Entry
            {
                eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit
            };
            exitEntry.callback.AddListener((_) => menuBtn.isHovered = false);
            trigger.triggers.Add(exitEntry);
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

        void Update()
        {
            if (!isInitialized || menuButtons == null) return;

            UpdateButtonAnimations();
            UpdatePanelPosition();
        }

        void UpdateButtonAnimations()
        {
            foreach (var btn in menuButtons)
            {
                if (btn.transform == null) continue;

                Vector3 targetScale = btn.isHovered
                    ? btn.originalScale * buttonHoverScale
                    : btn.originalScale;

                btn.transform.localScale = Vector3.Lerp(
                    btn.transform.localScale,
                    targetScale,
                    Time.deltaTime * buttonAnimSpeed
                );

                if (btn.background != null)
                {
                    Color targetColor = btn.isHovered
                        ? new Color(0.2f, 0.4f, 0.8f, 0.95f)
                        : new Color(0.1f, 0.1f, 0.15f, 0.9f);

                    btn.background.color = Color.Lerp(
                        btn.background.color,
                        targetColor,
                        Time.deltaTime * buttonAnimSpeed
                    );
                }
            }
        }

        void UpdatePanelPosition()
        {
            if (menuPanel == null) return;

            Camera mainCam = Camera.main;
            if (mainCam == null) return;

            Vector3 targetPos = mainCam.transform.position + mainCam.transform.forward * panelDistance;
            targetPos.y = panelHeight;

            menuPanel.position = Vector3.Lerp(menuPanel.position, targetPos, Time.deltaTime * 2f);

            Vector3 lookDir = menuPanel.position - mainCam.transform.position;
            lookDir.y = 0;
            if (lookDir.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(lookDir, Vector3.up);
                menuPanel.rotation = Quaternion.Slerp(menuPanel.rotation, targetRot, Time.deltaTime * 2f);
            }
        }

        IEnumerator FadeInAnimation()
        {
            if (menuButtons == null) yield break;

            foreach (var btn in menuButtons)
            {
                if (btn.canvasGroup != null)
                    btn.canvasGroup.alpha = 0f;
            }

            float elapsed = 0f;
            while (elapsed < panelFadeInDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / panelFadeInDuration;

                for (int i = 0; i < menuButtons.Length; i++)
                {
                    if (menuButtons[i].canvasGroup != null)
                    {
                        float delay = i * 0.1f;
                        float buttonT = Mathf.Clamp01((t - delay) / 0.3f);
                        menuButtons[i].canvasGroup.alpha = buttonT;
                    }
                }

                yield return null;
            }

            foreach (var btn in menuButtons)
            {
                if (btn.canvasGroup != null)
                    btn.canvasGroup.alpha = 1f;
            }
        }

        void PlayHoverSound()
        {
            if (hoverSound != null && audioSource != null)
                audioSource.PlayOneShot(hoverSound, 0.5f);
        }

        void PlayClickSound()
        {
            if (clickSound != null && audioSource != null)
                audioSource.PlayOneShot(clickSound, 0.8f);
        }

        void OnPlayClicked()
        {
            if (SceneFlowManager.Instance != null)
            {
                SceneFlowManager.Instance.GoToSongSelect();
            }
            else
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene("SongSelectScene");
            }
        }

        void OnSettingsClicked()
        {
            Debug.Log("[MainMenu] Settings - 待实现");
        }

        void OnCreditsClicked()
        {
            Debug.Log("[MainMenu] Credits - 待实现");
        }

        void OnQuitClicked()
        {
            if (SceneFlowManager.Instance != null)
            {
                SceneFlowManager.Instance.QuitGame();
            }
            else
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }
        }
    }
}
