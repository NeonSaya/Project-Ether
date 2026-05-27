using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace OsuVR
{
    /// <summary>
    /// 设置菜单 — 精确复刻 VRSettingsMenu.prefab
    ///
    /// 层级结构（1:1 复刻 Prefab）：
    ///   SettingsCanvas (Canvas WorldSpace, sortingOrder=100, localScale=0.0025)
    ///     anchor(0,0)->(1,1), sizeDelta(0,0)
    ///     └─ SettingsContainer (Image: 0.05,0.05,0.08,0.85)
    ///        anchor(0,0)->(1,1), sizeDelta(0,0)
    ///        ├─ TabBar (anchor 0,1->1,1, sizeDelta 0x50, pivot top)
    ///        │  HLG: padding(25,25,8,8), spacing=8, childControlWidth=1
    ///        ├─ ContentArea (anchor 0,0->1,1, pos(0,10), sizeDelta(-50,-130))
    ///        │  ├─ AudioPanel (active=1)
    ///        │  ├─ GraphicsPanel (active=0)
    ///        │  ├─ GamePanel (active=0)
    ///        │  └─ ControllerOffsetPanel (active=0)
    ///        └─ BottomButtons (anchor 0.5,0->0.5,0, pos(0,20), sizeDelta 400x50)
    /// </summary>
    public class SimpleVRSettingsMenu : MonoBehaviour
    {
        [Header("原子预制体（需在 Inspector 中配置）")]
        public GameObject atomicSliderPrefab;
        public GameObject atomicTogglePrefab;
        public GameObject atomicDropdownPrefab;

        [Header("音效资源（需在 Inspector 中配置）")]
        public AudioClip hoverSound;
        public AudioClip clickSound;

        // ---- Prefab 精确常量 ----
        private const float CanvasLocalZ = 1.5f;
        private const float CanvasScale = 0.0025f;
        private const int SortingOrder = 100;
        private const float CanvasWidth = 660f;
        private const float CanvasHeight = 495f;
        private const float TabBarHeight = 55f;

        // ---- Prefab 精确颜色 ----
        private static readonly Color ContainerBgColor = new Color(0.05f, 0.05f, 0.08f, 0.85f);
        private static readonly Color PanelBgColor = new Color(0.08f, 0.08f, 0.12f, 0.6f);
        private static readonly Color TabNormalColor = new Color(0.12f, 0.12f, 0.18f, 0.7f);
        private static readonly Color TabHighlightedColor = new Color(0.2f, 0.25f, 0.35f, 0.85f);
        private static readonly Color TabPressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
        private static readonly Color TabSelectedColor = new Color(0.15f, 0.3f, 0.5f, 0.9f);
        private static readonly Color TabActiveColor = new Color(0.2f, 0.35f, 0.55f, 1f);
        private static readonly Color TabInactiveColor = new Color(0.12f, 0.12f, 0.18f, 0.7f);
        private static readonly Color IndicatorActiveColor = new Color(0.25f, 0.55f, 0.85f, 1f);
        private static readonly Color IndicatorInactiveColor = new Color(0.25f, 0.55f, 0.85f, 0f);
        private static readonly Color ButtonNormalColor = new Color(0.12f, 0.15f, 0.22f, 0.6f);
        private static readonly Color ButtonHighlightedColor = new Color(0.2f, 0.3f, 0.45f, 0.8f);
        private static readonly Color ButtonPressedColor = new Color(0.08f, 0.1f, 0.15f, 0.7f);
        private static readonly Color ButtonSelectedColor = new Color(0.18f, 0.25f, 0.38f, 0.75f);

        // ---- 运行时状态 ----
        private CanvasGroup canvasGroup;
        private AudioSource audioSource;
        private GameSettings tempSettings;

        private SettingsPageBase[] pages;
        private RectTransform[] pagePanels;
        private Image[] tabButtonImages;
        private Image[] tabIndicators;
        private int currentTabIndex;

        // ============================================================
        //  生命周期
        // ============================================================

        void Start()
        {
            BuildUI();
            LocalizationManager.ReloadAndNotify();
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
            if (pages != null)
            {
                foreach (var page in pages)
                {
                    if (page is GameplaySettingsPage gp)
                        gp.RefreshLanguageDropdown();
                }
            }
        }

        // ============================================================
        //  UI 构建（精确复刻 VRSettingsMenu.prefab）
        // ============================================================

        private void BuildUI()
        {
            // ---- 音频源 ----
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;

            // ---- 根 Canvas ----
            // WorldSpace Canvas，固定尺寸（与 SimpleMainMenu 一致的模式）
            var rootCanvas = UILayoutHelper.CreateCanvas("SettingsCanvas", CanvasWidth, CanvasHeight);
            rootCanvas.sortingOrder = SortingOrder;
            rootCanvas.transform.SetParent(transform, false);
            rootCanvas.transform.localPosition = new Vector3(0f, 0f, CanvasLocalZ);
            rootCanvas.transform.localScale = Vector3.one * CanvasScale;

            var canvasRt = rootCanvas.GetComponent<RectTransform>();
            canvasRt.anchorMin = new Vector2(0.5f, 0.5f);
            canvasRt.anchorMax = new Vector2(0.5f, 0.5f);
            canvasRt.pivot = new Vector2(0.5f, 0.5f);
            canvasRt.anchoredPosition = Vector2.zero;
            canvasRt.sizeDelta = new Vector2(CanvasWidth, CanvasHeight);

            var scaler = rootCanvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.dynamicPixelsPerUnit = 10f;

            // Prefab: CanvasGroup on root
            canvasGroup = rootCanvas.gameObject.AddComponent<CanvasGroup>();

            // ---- SettingsContainer ----
            // Prefab: Image(0.05,0.05,0.08,0.85), full screen stretch
            var containerGo = new GameObject("SettingsContainer");
            containerGo.transform.SetParent(rootCanvas.transform, false);
            var containerRt = containerGo.AddComponent<RectTransform>();
            containerRt.anchorMin = Vector2.zero;
            containerRt.anchorMax = Vector2.one;
            containerRt.sizeDelta = Vector2.zero;
            containerRt.anchoredPosition = Vector2.zero;

            var containerImg = containerGo.AddComponent<Image>();
            containerImg.color = ContainerBgColor;
            containerImg.raycastTarget = true;

            // ---- TabBar ----
            // Prefab: anchor(0,1)->(1,1), sizeDelta(0,50), pivot(0.5,1)
            // HLG: padding(25,25,8,8), spacing=8, childAlignment=LowerCenter, childControlWidth=1
            var tabBarGo = new GameObject("TabBar");
            tabBarGo.transform.SetParent(containerGo.transform, false);
            var tabBarRt = tabBarGo.AddComponent<RectTransform>();
            tabBarRt.anchorMin = new Vector2(0, 1);
            tabBarRt.anchorMax = new Vector2(1, 1);
            tabBarRt.pivot = new Vector2(0.5f, 1);
            tabBarRt.sizeDelta = new Vector2(0, TabBarHeight);
            tabBarRt.anchoredPosition = Vector2.zero;

            var tabHlg = tabBarGo.AddComponent<HorizontalLayoutGroup>();
            tabHlg.padding = new RectOffset(28, 28, 9, 9);
            tabHlg.spacing = 9f;
            tabHlg.childAlignment = TextAnchor.LowerCenter;
            tabHlg.childControlWidth = true;
            tabHlg.childControlHeight = true;
            tabHlg.childForceExpandWidth = true;
            tabHlg.childForceExpandHeight = true;

            // Tab order: Audio, Graphics, Game, Controller (matches prefab)
            string[] tabKeys = { "ui_tab_audio", "ui_tab_graphics", "ui_tab_game", "ui_tab_controller" };
            string[] tabDefaults = { "Audio", "Graphics", "Game", "Controller" };
            int tabCount = tabKeys.Length;

            tabButtonImages = new Image[tabCount];
            tabIndicators = new Image[tabCount];

            for (int i = 0; i < tabCount; i++)
                CreateTabButton(tabBarGo.transform, i, tabKeys[i], tabDefaults[i]);

            // ---- ContentArea ----
            // Prefab: anchor(0,0)->(1,1), anchoredPos(0,10), sizeDelta(-50,-130)
            var contentAreaGo = new GameObject("ContentArea");
            contentAreaGo.transform.SetParent(containerGo.transform, false);
            var contentAreaRt = contentAreaGo.AddComponent<RectTransform>();
            contentAreaRt.anchorMin = Vector2.zero;
            contentAreaRt.anchorMax = Vector2.one;
            contentAreaRt.sizeDelta = new Vector2(-55, -143);
            contentAreaRt.anchoredPosition = new Vector2(0, 11);

            // 初始化 tempSettings
            tempSettings = SettingsManager.Instance.Settings.Clone();

            // Create pages
            pages = new SettingsPageBase[]
            {
                new AudioSettingsPage(),
                new GraphicsSettingsPage(),
                new GameplaySettingsPage(),
                new ControllerSettingsPage()
            };

            pagePanels = new RectTransform[pages.Length];

            for (int i = 0; i < pages.Length; i++)
            {
                pages[i].Initialize(atomicSliderPrefab, atomicTogglePrefab, atomicDropdownPrefab,
                    audioSource, hoverSound, clickSound);

                // Create panel (matches prefab: full screen stretch + VLG + Image)
                var panelGo = new GameObject($"Page_{i}");
                panelGo.transform.SetParent(contentAreaRt, false);
                var panelRt = panelGo.AddComponent<RectTransform>();
                panelRt.anchorMin = Vector2.zero;
                panelRt.anchorMax = Vector2.one;
                panelRt.sizeDelta = Vector2.zero;
                panelRt.anchoredPosition = Vector2.zero;

                var panelImg = panelGo.AddComponent<Image>();
                panelImg.color = PanelBgColor;
                panelImg.raycastTarget = false;

                // Prefab: VLG padding(15,15,10,10), spacing=6, childAlignment=UpperCenter, childControlWidth=1
                var vlg = panelGo.AddComponent<VerticalLayoutGroup>();
                vlg.padding = new RectOffset(16, 16, 11, 11);
                vlg.spacing = 7f;
                vlg.childAlignment = TextAnchor.UpperCenter;
                vlg.childControlWidth = true;
                vlg.childControlHeight = false;
                vlg.childForceExpandWidth = true;
                vlg.childForceExpandHeight = true;

                // Build page content
                pages[i].BuildContent(panelRt, tempSettings, 0f);

                // 强制重建布局（确保 VLG 正确计算子物体位置）
                LayoutRebuilder.ForceRebuildLayoutImmediate(panelRt);

                pagePanels[i] = panelRt;

                // AudioPanel starts active, others inactive (matches prefab)
                panelGo.SetActive(i == 0);
            }

            // ---- BottomButtons ----
            // Prefab: anchor(0.5,0)->(0.5,0), anchoredPos(0,20), sizeDelta(400,50)
            // HLG: spacing=40, childAlignment=MiddleCenter, childControlWidth=1
            var bottomGo = new GameObject("BottomButtons");
            bottomGo.transform.SetParent(containerGo.transform, false);
            var bottomRt = bottomGo.AddComponent<RectTransform>();
            bottomRt.anchorMin = new Vector2(0.5f, 0);
            bottomRt.anchorMax = new Vector2(0.5f, 0);
            bottomRt.pivot = new Vector2(0.5f, 0);
            bottomRt.sizeDelta = new Vector2(440, 55);
            bottomRt.anchoredPosition = new Vector2(0, 20);

            var bottomHlg = bottomGo.AddComponent<HorizontalLayoutGroup>();
            bottomHlg.spacing = 44f;
            bottomHlg.childAlignment = TextAnchor.MiddleCenter;
            bottomHlg.childControlWidth = true;
            bottomHlg.childControlHeight = true;
            bottomHlg.childForceExpandWidth = true;
            bottomHlg.childForceExpandHeight = true;

            // Back button (matches prefab colors and style)
            CreateBottomButton(bottomGo.transform, "Back", "ui_back",
                () => { PlayClickSound(); SceneManager.LoadScene("MainMenuScene"); });

            // Reset button
            CreateBottomButton(bottomGo.transform, "Reset", "ui_reset", OnResetClicked);

            // ---- 初始化 ----
            currentTabIndex = 0;
            UpdateTabVisuals();

            StartCoroutine(NotifyRayControllerNextFrame());
        }

        // ============================================================
        //  Tab 按钮（精确复刻 Prefab）
        // ============================================================

        private void CreateTabButton(Transform parent, int index, string locKey, string defaultText)
        {
            var go = new GameObject($"Tab_{index}");
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();

            // Prefab: Image color (0.12,0.12,0.18,0.7)
            var img = go.AddComponent<Image>();
            img.color = TabNormalColor;

            // Prefab: Button colors
            var button = go.AddComponent<Button>();
            var colors = button.colors;
            colors.normalColor = TabNormalColor;
            colors.highlightedColor = TabHighlightedColor;
            colors.pressedColor = TabPressedColor;
            colors.selectedColor = TabSelectedColor;
            button.colors = colors;
            button.targetGraphic = img;

            int capturedIndex = index;
            button.onClick.AddListener(() => SwitchTab(capturedIndex));

            // Indicator (bottom line)
            var indicatorGo = new GameObject("Indicator");
            indicatorGo.transform.SetParent(go.transform, false);
            var indicatorRt = indicatorGo.AddComponent<RectTransform>();
            indicatorRt.anchorMin = new Vector2(0, 0);
            indicatorRt.anchorMax = new Vector2(1, 0);
            indicatorRt.pivot = new Vector2(0.5f, 0);
            indicatorRt.sizeDelta = new Vector2(0, 3f);
            indicatorRt.anchoredPosition = Vector2.zero;
            var indicatorImg = indicatorGo.AddComponent<Image>();
            indicatorImg.color = IndicatorInactiveColor;

            // Text (fontSize=15, color 0.9,0.9,0.95,1, centered)
            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            var textRt = textGo.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.sizeDelta = Vector2.zero;
            textRt.anchoredPosition = Vector2.zero;

            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = defaultText;
            tmp.fontSize = 15f;
            tmp.color = new Color(0.9f, 0.9f, 0.95f, 1f);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableAutoSizing = false;
            tmp.raycastTarget = false;

            var lt = textGo.AddComponent<LocalizedText>();
            lt.localizationKey = locKey;

            // Hover effect
            UILayoutHelper.AddColorHoverEffect(go, img, TabNormalColor, TabHighlightedColor);
            UILayoutHelper.AddHoverSoundEffect(go, audioSource, hoverSound, 0.5f);

            tabButtonImages[index] = img;
            tabIndicators[index] = indicatorImg;
        }

        // ============================================================
        //  底部按钮（精确复刻 Prefab）
        // ============================================================

        private void CreateBottomButton(Transform parent, string text, string locKey,
            UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(text);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();

            // Prefab: Image color (0.12,0.15,0.22,0.6)
            var img = go.AddComponent<Image>();
            img.color = ButtonNormalColor;

            var button = go.AddComponent<Button>();
            var colors = button.colors;
            colors.normalColor = ButtonNormalColor;
            colors.highlightedColor = ButtonHighlightedColor;
            colors.pressedColor = ButtonPressedColor;
            colors.selectedColor = ButtonSelectedColor;
            button.colors = colors;
            button.targetGraphic = img;
            button.onClick.AddListener(onClick);

            // Text (fontSize=15, color 0.9,0.9,0.95,1, centered)
            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            var textRt = textGo.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.sizeDelta = Vector2.zero;
            textRt.anchoredPosition = Vector2.zero;

            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 15f;
            tmp.color = new Color(0.9f, 0.9f, 0.95f, 1f);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableAutoSizing = false;
            tmp.raycastTarget = false;

            var lt = textGo.AddComponent<LocalizedText>();
            lt.localizationKey = locKey;

            UILayoutHelper.AddHoverSoundEffect(go, audioSource, hoverSound, 0.5f);
        }

        // ============================================================
        //  Tab 切换
        // ============================================================

        private void SwitchTab(int index)
        {
            PlayClickSound();
            currentTabIndex = index;

            for (int i = 0; i < pagePanels.Length; i++)
                pagePanels[i].gameObject.SetActive(i == index);

            UpdateTabVisuals();

            // 强制重建当前面板布局（非活动面板的布局未被计算）
            if (index < pagePanels.Length)
                LayoutRebuilder.ForceRebuildLayoutImmediate(pagePanels[index]);

            // 刷新当前页面 UI
            if (tempSettings != null && pages != null && index < pages.Length)
            {
                tempSettings.CopyFrom(SettingsManager.Instance.Settings);
                pages[index].RefreshUI(tempSettings);
            }
        }

        private void UpdateTabVisuals()
        {
            for (int i = 0; i < tabButtonImages.Length; i++)
            {
                bool isActive = (i == currentTabIndex);
                tabButtonImages[i].color = isActive ? TabActiveColor : TabInactiveColor;
                tabIndicators[i].color = isActive ? IndicatorActiveColor : IndicatorInactiveColor;
            }
        }

        // ============================================================
        //  按钮回调
        // ============================================================

        private void OnResetClicked()
        {
            PlayClickSound();
            SettingsManager.Instance.ResetToDefaults();
            tempSettings.CopyFrom(SettingsManager.Instance.Settings);

            if (currentTabIndex < pages.Length)
                pages[currentTabIndex].RefreshUI(tempSettings);
        }

        // ============================================================
        //  Show / Hide
        // ============================================================

        public void Show()
        {
            gameObject.SetActive(true);

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
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
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
        }

        // ============================================================
        //  工具方法
        // ============================================================

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
