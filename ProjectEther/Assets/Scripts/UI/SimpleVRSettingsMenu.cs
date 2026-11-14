using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace OsuVR
{
    /// <summary>设置页：保留四标签、即时应用、滚动和返回/重置的原有流程。</summary>
    public class SimpleVRSettingsMenu : MonoBehaviour
    {
        [Header("原子预制体（需在 Inspector 中配置）")]
        public GameObject atomicSliderPrefab;
        public GameObject atomicTogglePrefab;
        public GameObject atomicDropdownPrefab;

        [Header("音效资源（需在 Inspector 中配置）")]
        public AudioClip hoverSound;
        public AudioClip clickSound;

        private const float CanvasWidth = 660;
        private const float CanvasHeight = 495;
        private CanvasGroup canvasGroup;
        private AudioSource audioSource;
        private GameSettings tempSettings;
        private SettingsPageBase[] pages;
        private RectTransform[] pagePanels;
        private Image[] tabButtonImages;
        private Image[] tabIndicators;
        private int currentTabIndex;
        private ScrollRect settingsScrollRect;

        void Start()
        {
            BuildUI();
            LocalizationManager.ReloadAndNotify();
        }

        void OnEnable() => LocalizationManager.OnLanguageChanged += OnLanguageChanged;

        void OnDisable() => LocalizationManager.OnLanguageChanged -= OnLanguageChanged;

        private void OnLanguageChanged()
        {
            if (pages == null)
                return;
            foreach (var page in pages)
                if (page is GameplaySettingsPage gameplay)
                    gameplay.RefreshLanguageDropdown();
        }

        private void BuildUI()
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0;
            var canvas = UILayoutHelper.CreateCanvas("SettingsCanvas", CanvasWidth, CanvasHeight);
            canvas.sortingOrder = 100;
            canvas.transform.SetParent(transform, false);
            canvas.transform.localPosition = new Vector3(0, 0, 1.5f);
            canvas.transform.localScale = Vector3.one * 0.0025f;
            var canvasRect = (RectTransform)canvas.transform;
            canvasRect.anchorMin = canvasRect.anchorMax = canvasRect.pivot = Vector2.one * 0.5f;
            canvasRect.anchoredPosition = Vector2.zero;
            canvas.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 10;
            canvasGroup = canvas.gameObject.AddComponent<CanvasGroup>();

            var container = MenuPageStyle.Rect(
                canvas.transform,
                "SettingsContainer",
                0,
                0,
                660,
                495
            );
            MenuPageStyle.Stretch(container);
            SongSelectionStyle.Fill(container, Color.clear, true);
            MenuPageStyle.Backdrop(container);
            var tabs = MenuPageStyle.Rect(container, "TabBar", 28, 12, 604, 40);
            var tabsLayout = tabs.gameObject.AddComponent<HorizontalLayoutGroup>();
            tabsLayout.spacing = 9;
            tabsLayout.childControlWidth = tabsLayout.childControlHeight = true;
            tabsLayout.childForceExpandWidth = tabsLayout.childForceExpandHeight = true;
            string[] keys =
            {
                "ui_tab_audio",
                "ui_tab_graphics",
                "ui_tab_game",
                "ui_tab_controller",
            };
            string[] labels = { "Audio", "Graphics", "Game", "Controller" };
            tabButtonImages = new Image[keys.Length];
            tabIndicators = new Image[keys.Length];
            for (int i = 0; i < keys.Length; i++)
                CreateTabButton(tabs, i, keys[i], labels[i]);

            settingsScrollRect = MenuPageStyle.Scroll(
                container,
                "ContentArea",
                28,
                64,
                604,
                342,
                out var content
            );
            var contentLayout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            contentLayout.childControlWidth = contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;

            tempSettings =
                SettingsManager.Instance?.Settings?.Clone()
                ?? ScriptableObject.CreateInstance<GameSettings>();
            pages = new SettingsPageBase[]
            {
                new AudioSettingsPage(),
                new GraphicsSettingsPage(),
                new GameplaySettingsPage(),
                new ControllerSettingsPage(),
            };
            pagePanels = new RectTransform[pages.Length];
            for (int i = 0; i < pages.Length; i++)
            {
                pages[i]
                    .Initialize(
                        atomicSliderPrefab,
                        atomicTogglePrefab,
                        atomicDropdownPrefab,
                        audioSource,
                        hoverSound,
                        clickSound
                    );
                var panel = MenuPageStyle.Rect(content, $"Page_{i}", 0, 0, 0, 0);
                var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
                layout.padding = new RectOffset(12, 12, 10, 10);
                layout.spacing = 14;
                layout.childAlignment = TextAnchor.UpperCenter;
                layout.childControlWidth = true;
                layout.childControlHeight = false;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = false;
                panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter
                    .FitMode
                    .PreferredSize;
                pages[i].BuildContent(panel, tempSettings, 0);
                pagePanels[i] = panel;
                panel.gameObject.SetActive(i == 0);
            }

            var bottom = MenuPageStyle.Rect(container, "BottomButtons", 60, 425, 540, 50);
            var bottomLayout = bottom.gameObject.AddComponent<HorizontalLayoutGroup>();
            bottomLayout.spacing = 36;
            bottomLayout.childControlWidth = bottomLayout.childControlHeight = true;
            bottomLayout.childForceExpandWidth = bottomLayout.childForceExpandHeight = true;
            CreateBottomButton(
                bottom,
                "Back",
                "ui_back",
                () =>
                {
                    PlayClickSound();
                    VRSceneTransitionManager.Instance.TransitionToScene("MainMenuScene");
                }
            );
            CreateBottomButton(bottom, "Reset", "ui_reset", OnResetClicked);
            currentTabIndex = 0;
            UpdateTabVisuals();
            StartCoroutine(NotifyRayControllerNextFrame());
        }

        private void CreateTabButton(Transform parent, int index, string key, string text)
        {
            int tabIndex = index;
            var button = MenuPageStyle.Button(
                parent,
                $"Tab_{index}",
                text,
                key,
                () => SwitchTab(tabIndex)
            );
            UILayoutHelper.AddHoverSoundEffect(button.gameObject, audioSource, hoverSound, 0.5f);
            var indicator = MenuPageStyle.Rect(button.transform, "Indicator", 0, 0, 0, 2);
            indicator.anchorMin = Vector2.zero;
            indicator.anchorMax = new Vector2(1, 0);
            indicator.pivot = new Vector2(0.5f, 0);
            indicator.anchoredPosition = Vector2.zero;
            tabIndicators[index] = SongSelectionStyle.Fill(indicator, Color.clear);
            tabButtonImages[index] = button.GetComponent<Image>();
        }

        private void CreateBottomButton(
            Transform parent,
            string text,
            string key,
            UnityEngine.Events.UnityAction action
        )
        {
            var button = MenuPageStyle.Button(parent, text, text, key, action);
            UILayoutHelper.AddHoverSoundEffect(button.gameObject, audioSource, hoverSound, 0.5f);
        }

        private void SwitchTab(int index)
        {
            PlayClickSound();
            currentTabIndex = index;
            for (int i = 0; i < pagePanels.Length; i++)
                pagePanels[i].gameObject.SetActive(i == index);
            settingsScrollRect.verticalNormalizedPosition = 1;
            UpdateTabVisuals();
            LayoutRebuilder.ForceRebuildLayoutImmediate(pagePanels[index]);
            if (tempSettings != null)
            {
                tempSettings.CopyFrom(SettingsManager.Instance.Settings);
                pages[index].RefreshUI(tempSettings);
            }
        }

        private void UpdateTabVisuals()
        {
            for (int i = 0; i < tabButtonImages.Length; i++)
            {
                bool active = i == currentTabIndex;
                SongSelectionStyle.ConfigureButton(
                    tabButtonImages[i].GetComponent<Button>(),
                    active ? SongSelectionStyle.Selected : SongSelectionStyle.Control
                );
                tabIndicators[i].color = active ? SongSelectionStyle.Accent : Color.clear;
            }
        }

        private void OnResetClicked()
        {
            PlayClickSound();
            SettingsManager.Instance.ResetToDefaults();
            tempSettings.CopyFrom(SettingsManager.Instance.Settings);
            if (currentTabIndex < pages.Length)
                pages[currentTabIndex].RefreshUI(tempSettings);
        }

        public void Show()
        {
            gameObject.SetActive(true);
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1;
                canvasGroup.interactable = canvasGroup.blocksRaycasts = true;
            }
            RayController.NotifyUICanvasChanged();
            if (tempSettings != null && SettingsManager.Instance != null)
            {
                tempSettings.CopyFrom(SettingsManager.Instance.Settings);
                if (currentTabIndex < pages.Length)
                    pages[currentTabIndex].RefreshUI(tempSettings);
            }
        }

        public void Hide()
        {
            if (canvasGroup == null)
                return;
            canvasGroup.alpha = 0;
            canvasGroup.interactable = canvasGroup.blocksRaycasts = false;
        }

        private void PlayClickSound()
        {
            if (audioSource != null && clickSound != null)
                audioSource.PlayOneShot(clickSound, 0.8f);
        }

        private IEnumerator NotifyRayControllerNextFrame()
        {
            yield return null;
            RayController.NotifyUICanvasChanged();
        }
    }
}
