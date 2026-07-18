using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace OsuVR
{
    /// <summary>
    /// 主菜单 — 代码驱动 UI 试点
    ///
    /// 所有 UI 元素在运行时通过 UILayoutHelper 动态生成，
    /// 无 Prefab 拖拽依赖，无 SerializeField 组件引用。
    /// 多语言通过 localizationKey 在创建时直接绑定。
    ///
    /// 布局参数严格复刻原 MainMenu.prefab：
    ///   MainMenu (Transform, 0,0,0)
    ///     └── MenuCanvas (Canvas WorldSpace, pos:0,0,1.5, scale:0.003)
    ///           anchoredPos: 0,2.5  sizeDelta: 600x450  sortingOrder:100
    ///           ├── Title (TMP, anchoredPos:0,120, size:500x70, fontSize:32)
    ///           ├── Buttons (VLG, spacing:12, alignment:4)
    ///           │   ├── Btn_Play     (size:0x50, fontSize:20)
    ///           │   ├── Btn_Settings (size:0x50, fontSize:20)
    ///           │   ├── Btn_Credits  (size:0x50, fontSize:20)
    ///           │   └── Btn_Quit     (size:0x50, fontSize:20)
    ///           └── Version (TMP, anchoredPos:0,-178, size:200x30, fontSize:14)
    /// </summary>
    public class SimpleMainMenu : MonoBehaviour
    {
        [Header("音效资源（需在 Inspector 中配置）")]
        public AudioClip hoverSound;
        public AudioClip clickSound;

        [Header("版本信息")]
        public string gameTitle = "Project Ether";
        public string subtitle = "\u4EE5\u592A\u8BA1\u5212"; // 以太计划
        private const string version = "v0.7.3";

        // ---- 布局常量（复刻原 Prefab） ----
        private const float CanvasLocalZ = 1.5f;
        private const float CanvasScale = 0.0025f;
        private const int SortingOrder = 100;
        private const float CanvasWidth = 600f;
        private const float CanvasHeight = 450f;
        private const float CanvasAnchoredY = 2.5f;

        private const float TitleAnchoredY = 120f;
        private const float TitleWidth = 500f;
        private const float TitleHeight = 70f;
        private const float TitleFontSize = 32f;

        private const float ButtonsAnchoredY = 80f;
        private const float ButtonsWidth = 220f;
        private const float ButtonHeight = 50f;
        private const float ButtonFontSize = 20f;
        private const float ButtonSpacing = 12f;

        private const float VersionAnchoredY = -178f;
        private const float VersionWidth = 200f;
        private const float VersionHeight = 30f;
        private const float VersionFontSize = 14f;

        private Canvas rootCanvas;
        private AudioSource audioSource;

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
            // LocalizedText 组件已自动处理文本刷新
        }

        // ============================================================
        //  UI 构建（1:1 复刻原 Prefab）
        // ============================================================

        private void BuildUI()
        {
            // ---- 音频源 ----
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;

            // ---- 根 Canvas ----
            // 复刻：Canvas WorldSpace, SortingOrder=100, 无 CurvedUIEffect
            rootCanvas = UILayoutHelper.CreateCanvas("MenuCanvas", CanvasWidth, CanvasHeight);
            rootCanvas.sortingOrder = SortingOrder;
            rootCanvas.transform.SetParent(transform, false);

            // 复刻：localPosition=(0,0,1.5), localScale=(0.0025,0.0025,0.0025)
            rootCanvas.transform.localPosition = new Vector3(0f, 0f, CanvasLocalZ);
            rootCanvas.transform.localScale = Vector3.one * CanvasScale;

            // 复刻：anchoredPosition=(0,2.5), sizeDelta=(600,450), pivot=center
            var canvasRt = rootCanvas.GetComponent<RectTransform>();
            canvasRt.anchorMin = new Vector2(0.5f, 0.5f);
            canvasRt.anchorMax = new Vector2(0.5f, 0.5f);
            canvasRt.pivot = new Vector2(0.5f, 0.5f);
            canvasRt.anchoredPosition = new Vector2(0f, CanvasAnchoredY);
            canvasRt.sizeDelta = new Vector2(CanvasWidth, CanvasHeight);

            // 复刻 CanvasScaler：ConstantPixelSize, ReferencePixelsPerUnit=100, DynamicPixelsPerUnit=10
            var scaler = rootCanvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.referencePixelsPerUnit = 100f;
            scaler.dynamicPixelsPerUnit = 10f;

            // ---- Title ----
            var titleTmp = UILayoutHelper.CreateText(rootCanvas.transform,
                $"{gameTitle}\n<size=60%>{subtitle}</size>",
                fontSize: TitleFontSize,
                color: Color.white,
                alignment: TextAlignmentOptions.Center);
            SetRectTransform(titleTmp.rectTransform,
                anchor: new Vector2(0.5f, 0.5f),
                anchoredPos: new Vector2(0f, TitleAnchoredY),
                sizeDelta: new Vector2(TitleWidth, TitleHeight));

            // ---- Buttons 容器（VerticalLayoutGroup） ----
            var buttonsGo = new GameObject("Buttons");
            buttonsGo.transform.SetParent(rootCanvas.transform, false);
            var buttonsRt = buttonsGo.AddComponent<RectTransform>();
            SetRectTransform(buttonsRt,
                anchor: new Vector2(0.5f, 0.5f),
                pivot: new Vector2(0.5f, 1f),
                anchoredPos: new Vector2(0f, ButtonsAnchoredY),
                sizeDelta: new Vector2(ButtonsWidth, 0f));

            var vlg = buttonsGo.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = ButtonSpacing;
            vlg.childAlignment = (TextAnchor)4; // UpperCenter
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            // 复刻 ContentSizeFitter：verticalFit = PreferredSize
            var fitter = buttonsGo.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // ---- 按钮 ----
            CreateMenuButton(buttonsGo.transform, "Play", "ui_play_button", OnPlayClicked,
                new Color(0.2f, 0.6f, 1f, 1f));
            CreateMenuButton(buttonsGo.transform, "Settings", "ui_settings", OnSettingsClicked,
                new Color(0.5f, 0.5f, 0.5f, 1f));
            CreateMenuButton(buttonsGo.transform, "Credits", "ui_credits", OnCreditsClicked,
                new Color(0.4f, 0.5f, 0.6f, 1f));
            CreateMenuButton(buttonsGo.transform, "Quit", "ui_quit", OnQuitClicked,
                new Color(0.8f, 0.3f, 0.3f, 1f));

            // ---- Version ----
            var versionTmp = UILayoutHelper.CreateText(rootCanvas.transform, version,
                fontSize: VersionFontSize,
                color: Color.white,
                alignment: TextAlignmentOptions.Center);
            SetRectTransform(versionTmp.rectTransform,
                anchor: new Vector2(0.5f, 0.5f),
                anchoredPos: new Vector2(0f, VersionAnchoredY),
                sizeDelta: new Vector2(VersionWidth, VersionHeight));

            // ---- 通知 RayController（延迟一帧，确保 Canvas 完全初始化） ----
            StartCoroutine(NotifyRayControllerNextFrame());

            Debug.Log("[SimpleMainMenu] 动态 UI 构建完成 (1:1 复刻 Prefab 布局)");
        }

        /// <summary>
        /// 创建菜单按钮（复刻原 Prefab：sizeDelta=0x50, fontSize=20, stretch text）
        /// </summary>
        private void CreateMenuButton(Transform parent, string defaultText, string locKey,
            System.Action onClick, Color imageColor)
        {
            // 按钮根物体
            var btnGo = new GameObject($"Btn_{defaultText}");
            btnGo.transform.SetParent(parent, false);

            var btnRt = btnGo.AddComponent<RectTransform>();
            btnRt.sizeDelta = new Vector2(0f, ButtonHeight);

            var btnImg = btnGo.AddComponent<Image>();
            btnImg.color = imageColor;

            var button = btnGo.AddComponent<Button>();
            var colors = button.colors;
            colors.normalColor = new Color(1f, 1f, 1f, 1f);
            colors.highlightedColor = new Color(0.96f, 0.96f, 0.96f, 1f);
            colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
            colors.selectedColor = new Color(1f, 1f, 1f, 1f);
            button.colors = colors;
            button.targetGraphic = btnImg;

            // BoxCollider（原 Prefab：IsTrigger=true, Size=(0,50,10)）
            var col = btnGo.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(0f, 50f, 10f);

            // 按钮文字（stretch 填充父物体）
            var textGo = new GameObject("Text");
            textGo.transform.SetParent(btnGo.transform, false);
            var textRt = textGo.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.sizeDelta = Vector2.zero;
            textRt.anchoredPosition = Vector2.zero;

            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = ButtonFontSize;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.text = defaultText;
            tmp.enableAutoSizing = false;
            tmp.fontSizeMin = 18f;
            tmp.fontSizeMax = 72f;
            tmp.raycastTarget = true;

            // 多语言
            if (!string.IsNullOrEmpty(locKey))
            {
                var lt = textGo.AddComponent<LocalizedText>();
                lt.localizationKey = locKey;
            }

            // 悬停变色
            UILayoutHelper.AddColorHoverEffect(btnGo, btnImg,
                imageColor, UILayoutHelper.ButtonHoverColor);

            // 悬停音效
            UILayoutHelper.AddHoverSoundEffect(btnGo, audioSource, hoverSound, 0.5f);

            // 点击事件（包装音效）
            button.onClick.AddListener(() =>
            {
                PlayClickSound();
                onClick?.Invoke();
            });
        }

        /// <summary>
        /// 设置 RectTransform 参数
        /// </summary>
        private static void SetRectTransform(RectTransform rt, Vector2? anchor = null,
            Vector2? pivot = null, Vector2? anchoredPos = null, Vector2? sizeDelta = null)
        {
            if (anchor.HasValue)
            {
                rt.anchorMin = anchor.Value;
                rt.anchorMax = anchor.Value;
            }
            if (pivot.HasValue) rt.pivot = pivot.Value;
            if (anchoredPos.HasValue) rt.anchoredPosition = anchoredPos.Value;
            if (sizeDelta.HasValue) rt.sizeDelta = sizeDelta.Value;
        }

        // ============================================================
        //  音效
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
            Debug.Log("[SimpleMainMenu] RayController 缓存已刷新");
        }

        // ============================================================
        //  场景跳转
        // ============================================================

        private void OnPlayClicked()
        {
            VRSceneTransitionManager.Instance.TransitionToScene("SongSelectScene");
        }

        private void OnSettingsClicked()
        {
            VRSceneTransitionManager.Instance.TransitionToScene("SettingsScene");
        }

        private void OnCreditsClicked()
        {
            Debug.Log("[MainMenu] Credits - 待实现");
        }

        private void OnQuitClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
