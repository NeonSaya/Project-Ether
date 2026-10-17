using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR;

namespace OsuVR
{
    /// <summary>
    /// 可选的前奏跳过入口：右下侧低干扰提示，悬停恢复清晰，绝不自动跳过。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas), typeof(HUDFollower), typeof(GraphicRaycaster))]
    public sealed class IntroSkipOverlay : MonoBehaviour
    {
        [SerializeField]
        private RhythmGameManager gameManager;

        [SerializeField]
        private TMP_FontAsset font;

        private Canvas overlayCanvas;
        private CanvasGroup group;
        private RectTransform content;
        private RectTransform progress;
        private Button skipButton;
        private TextMeshProUGUI label;
        private Beatmap displayedBeatmap;
        private double lastMusicTimeMs;
        private double displayStartTimeMs;
        private float shownAt;
        private float alpha;
        private int hoverCount;
        private bool wasAvailable;
        private bool desktopMode;

        private void Awake()
        {
            overlayCanvas = GetComponent<Canvas>();
            overlayCanvas.worldCamera = Camera.main;
            CreateContent();
            HideImmediately();
        }

        private void OnEnable()
        {
            LocalizationManager.OnLanguageChanged += UpdateLabels;
            UpdateLabels();
        }

        private void OnDisable()
        {
            LocalizationManager.OnLanguageChanged -= UpdateLabels;
            HideImmediately();
        }

        private void Update()
        {
            bool desktop = !XRSettings.isDeviceActive;
            if (desktopMode != desktop)
            {
                desktopMode = desktop;
                UpdateLabels();
            }
            if (overlayCanvas.worldCamera == null)
                overlayCanvas.worldCamera = Camera.main;

            double targetTimeMs = 0;
            bool available =
                gameManager != null && gameManager.TryGetIntroSkipTarget(out targetTimeMs);
            double nowMs = gameManager != null ? gameManager.GetCurrentMusicTimeMs() : 0;
            var beatmap = gameManager != null ? gameManager.GetCurrentBeatmap() : null;
            if (
                available
                && (!wasAvailable || beatmap != displayedBeatmap || nowMs < lastMusicTimeMs - 1)
            )
            {
                displayedBeatmap = beatmap;
                displayStartTimeMs = nowMs;
                shownAt = Time.unscaledTime;
                hoverCount = 0;
            }
            wasAvailable = available;
            lastMusicTimeMs = nowMs;

            // 仅刚出现的半秒防误触；闲置时仍留淡提示，避免不可见的点击区域。
            bool armed = available && Time.unscaledTime - shownAt >= 0.5f;
            float targetAlpha = available
                ? (hoverCount > 0 || Time.unscaledTime - shownAt < 1.5f ? 1f : 0.28f)
                : 0f;
            alpha = Mathf.MoveTowards(alpha, targetAlpha, Time.unscaledDeltaTime / 0.25f);
            group.alpha = alpha;
            group.interactable = armed;
            group.blocksRaycasts = armed && alpha >= 0.1f;
            skipButton.interactable = armed;
            overlayCanvas.enabled = alpha > 0f;
            if (!available)
                hoverCount = 0;

            // 悬停只改亮度和颜色，命中区域不移动，边缘射线不会因动画反复进出。
            float remaining =
                available && targetTimeMs > displayStartTimeMs
                    ? Mathf.Clamp01(
                        (float)((targetTimeMs - nowMs) / (targetTimeMs - displayStartTimeMs))
                    )
                    : 0f;
            progress.sizeDelta = new Vector2(344f * remaining, 3f);

            if (
                armed
                && desktopMode
                && Mouse.current != null
                && Mouse.current.leftButton.wasPressedThisFrame
            )
                RequestSkip();
        }

        private void RequestSkip()
        {
            if (!group.interactable || gameManager == null || !gameManager.TrySkipIntro())
                return;
            group.interactable = false;
            group.blocksRaycasts = false;
            skipButton.interactable = false;
            hoverCount = 0;
        }

        private void HideImmediately()
        {
            wasAvailable = false;
            hoverCount = 0;
            alpha = 0f;
            if (overlayCanvas != null)
                overlayCanvas.enabled = false;
            if (group != null)
            {
                group.alpha = 0f;
                group.interactable = false;
                group.blocksRaycasts = false;
            }
        }

        private void UpdateLabels()
        {
            if (label == null)
                return;
            label.text = LocalizationManager.GetText("ui_skip_intro");
        }

        private void CreateContent()
        {
            var root = new GameObject(
                "IntroSkipContent",
                typeof(RectTransform),
                typeof(CanvasGroup)
            );
            content = root.GetComponent<RectTransform>();
            content.SetParent(transform, false);
            content.anchorMin = content.anchorMax = content.pivot = Vector2.one * 0.5f;
            content.sizeDelta = new Vector2(400f, 150f);
            content.anchoredPosition = new Vector2(640f, 0f);
            group = root.GetComponent<CanvasGroup>();

            var shadowObject = new GameObject(
                "SoftBacking",
                typeof(RectTransform),
                typeof(FeatheredShadowGraphic)
            );
            shadowObject.transform.SetParent(content, false);
            shadowObject.GetComponent<RectTransform>().sizeDelta = new Vector2(520f, 240f);
            var shadow = shadowObject.GetComponent<FeatheredShadowGraphic>();
            shadow.color = new Color(0f, 0f, 0f, 0.28f);
            shadow.raycastTarget = false;

            var buttonObject = new GameObject(
                "SkipIntroButton",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button)
            );
            buttonObject.transform.SetParent(content, false);
            var buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.sizeDelta = new Vector2(380f, 136f);
            var background = buttonObject.GetComponent<Image>();
            background.raycastTarget = true;
            // 命中范围略宽于可见卡片，容忍手柄射线抖动，不需要物理碰撞体。
            background.raycastPadding = new Vector4(-18f, -16f, -18f, -16f);
            skipButton = buttonObject.GetComponent<Button>();
            skipButton.targetGraphic = background;
            skipButton.navigation = new Navigation { mode = Navigation.Mode.None };
            var colors = skipButton.colors;
            colors.normalColor = new Color(0.055f, 0.07f, 0.09f, 0.9f);
            colors.highlightedColor = new Color(0.10f, 0.15f, 0.19f, 0.98f);
            colors.pressedColor = new Color(0.18f, 0.23f, 0.26f, 1f);
            colors.selectedColor = colors.normalColor;
            colors.fadeDuration = 0.12f;
            skipButton.colors = colors;
            skipButton.onClick.AddListener(RequestSkip);

            var events = buttonObject.AddComponent<EventTrigger>();
            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => hoverCount++);
            events.triggers.Add(enter);
            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(_ => hoverCount = Mathf.Max(0, hoverCount - 1));
            events.triggers.Add(exit);

            label = CreateText(buttonRect, 29f, new Vector2(300f, 54f), new Vector2(-22f, 14f));
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.fontStyle = FontStyles.Bold;
            var icon = CreateText(buttonRect, 30f, new Vector2(52f, 54f), new Vector2(152f, 14f));
            icon.richText = false;
            icon.fontStyle = FontStyles.Bold;
            icon.text = ">>";
            icon.color = new Color(1f, 0.8f, 0.5f, 1f);

            var accent = UILayoutHelper.CreatePanel(
                buttonRect,
                "Accent",
                new Color(1f, 0.78f, 0.44f, 0.85f),
                3f,
                86f
            );
            accent.anchoredPosition = new Vector2(-190f, 0f);
            var track = UILayoutHelper.CreatePanel(
                buttonRect,
                "TimeTrack",
                new Color(1f, 1f, 1f, 0.1f),
                344f,
                3f
            );
            track.anchoredPosition = new Vector2(0f, -46f);
            progress = UILayoutHelper.CreatePanel(
                buttonRect,
                "TimeRemaining",
                new Color(1f, 0.78f, 0.44f, 0.8f),
                344f,
                3f
            );
            progress.pivot = new Vector2(0f, 0.5f);
            progress.anchoredPosition = new Vector2(-172f, -46f);
            foreach (var child in GetComponentsInChildren<Transform>(true))
                child.gameObject.layer = gameObject.layer;
        }

        private TextMeshProUGUI CreateText(
            Transform parent,
            float size,
            Vector2 dimensions,
            Vector2 position
        )
        {
            var text = UILayoutHelper.CreateText(
                parent,
                string.Empty,
                size,
                width: dimensions.x,
                height: dimensions.y
            );
            text.font = font != null ? font : TMP_Settings.defaultFontAsset;
            text.enableWordWrapping = false;
            text.rectTransform.anchoredPosition = position;
            return text;
        }
    }
}
