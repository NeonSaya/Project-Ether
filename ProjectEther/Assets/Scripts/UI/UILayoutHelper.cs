using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace OsuVR
{
    /// <summary>
    /// UI 布局工具类：纯代码驱动的 uGUI 创建引擎
    ///
    /// 设计目标：
    /// 1. 所有 UI 元素通过静态方法运行时创建，消除 Prefab 拖拽依赖
    /// 2. 文本创建时自动集成 LocalizedText 多语言支持
    /// 3. 自动挂载 CurvedUIEffect 实现 VR 弯曲效果
    /// 4. 提供原子级 UI 元素创建，由上层脚本组合布局
    /// </summary>
    public static class UILayoutHelper
    {
        // ============================================================
        //  默认样式常量
        // ============================================================

        public static readonly Color DefaultPanelColor = new Color(0.08f, 0.08f, 0.12f, 0.92f);
        public static readonly Color DefaultTextColor = new Color(0.9f, 0.92f, 0.95f, 1f);
        public static readonly Color AccentColor = new Color(0.3f, 0.6f, 1f, 1f);
        public static readonly Color ButtonNormalColor = new Color(0.15f, 0.18f, 0.28f, 1f);
        public static readonly Color ButtonHoverColor = new Color(0.22f, 0.3f, 0.5f, 1f);
        public static readonly Color ButtonPressedColor = new Color(0.12f, 0.2f, 0.4f, 1f);

        public const float DefaultCurveRadius = 3.5f;
        public const int DefaultFontSize = 36;

        // ============================================================
        //  Canvas
        // ============================================================

        /// <summary>
        /// 创建 WorldSpace Canvas（VR UI 根容器）
        /// </summary>
        /// <param name="name">Canvas 名称</param>
        /// <param name="width">Canvas 像素宽度</param>
        /// <param name="height">Canvas 像素高度</param>
        /// <param name="applyCurve">是否自动添加 CurvedUIEffect</param>
        public static Canvas CreateCanvas(string name, float width = 600f, float height = 450f,
            bool applyCurve = false)
        {
            var go = new GameObject(name);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(width, height);

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.referencePixelsPerUnit = 100f;
            scaler.dynamicPixelsPerUnit = 10f;

            go.AddComponent<GraphicRaycaster>();

            if (applyCurve)
                AddCurvedUIEffect(go, DefaultCurveRadius, 1f, 1);

            return canvas;
        }

        // ============================================================
        //  Panel
        // ============================================================

        /// <summary>
        /// 创建带背景色的面板
        /// </summary>
        public static RectTransform CreatePanel(Transform parent, string name,
            Color? color = null, float? width = null, float? height = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rt = go.AddComponent<RectTransform>();
            if (width.HasValue && height.HasValue)
                rt.sizeDelta = new Vector2(width.Value, height.Value);

            var img = go.AddComponent<Image>();
            img.color = color ?? DefaultPanelColor;
            img.raycastTarget = false;

            return rt;
        }

        // ============================================================
        //  Text
        // ============================================================

        /// <summary>
        /// 创建 TextMeshProUGUI 文本
        /// </summary>
        /// <param name="parent">父物体</param>
        /// <param name="text">初始文本（若提供 localizationKey 则会被覆盖）</param>
        /// <param name="fontSize">字体大小</param>
        /// <param name="color">字体颜色</param>
        /// <param name="alignment">对齐方式</param>
        /// <param name="localizationKey">本地化 Key（非空时自动挂载 LocalizedText）</param>
        /// <param name="width">宽度（可选）</param>
        /// <param name="height">高度（可选）</param>
        public static TextMeshProUGUI CreateText(Transform parent, string text,
            float fontSize = DefaultFontSize, Color? color = null,
            TextAlignmentOptions alignment = TextAlignmentOptions.Center,
            string localizationKey = null, float? width = null, float? height = null)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);

            var rt = go.AddComponent<RectTransform>();
            if (width.HasValue && height.HasValue)
                rt.sizeDelta = new Vector2(width.Value, height.Value);
            else
                rt.sizeDelta = new Vector2(400f, fontSize * 1.5f);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = fontSize;
            tmp.color = color ?? DefaultTextColor;
            tmp.alignment = alignment;
            tmp.text = text ?? "";
            tmp.enableAutoSizing = false;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            tmp.raycastTarget = false;

            // 多语言集成
            if (!string.IsNullOrEmpty(localizationKey))
            {
                var lt = go.AddComponent<LocalizedText>();
                lt.localizationKey = localizationKey;
                // LocalizedText.Start() 会自动调用 UpdateText() 设置正确文本和字体
            }

            return tmp;
        }

        // ============================================================
        //  Button
        // ============================================================

        /// <summary>
        /// 创建按钮（含背景 Image + 子物体 TextMeshProUGUI）
        /// </summary>
        /// <param name="parent">父物体</param>
        /// <param name="text">按钮文字（若提供 localizationKey 则会被覆盖）</param>
        /// <param name="onClick">点击回调</param>
        /// <param name="localizationKey">本地化 Key（非空时文本自动本地化）</param>
        /// <param name="width">按钮宽度</param>
        /// <param name="height">按钮高度</param>
        /// <param name="fontSize">文字大小</param>
        /// <param name="normalColor">Image 常态颜色</param>
        /// <param name="hoverColor">悬停颜色</param>
        /// <param name="addBoxCollider">是否添加 BoxCollider（VR 射线碰撞检测）</param>
        public static Button CreateButton(Transform parent, string text, System.Action onClick = null,
            string localizationKey = null, float width = 360f, float height = 70f,
            float fontSize = 32f, Color? normalColor = null, Color? hoverColor = null,
            bool addBoxCollider = false)
        {
            // 按钮根物体
            var go = new GameObject("Button");
            go.transform.SetParent(parent, false);

            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(width, height);

            var img = go.AddComponent<Image>();
            img.color = normalColor ?? ButtonNormalColor;

            var button = go.AddComponent<Button>();
            var colors = button.colors;
            colors.normalColor = new Color(1f, 1f, 1f, 1f);
            colors.highlightedColor = new Color(0.96f, 0.96f, 0.96f, 1f);
            colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
            colors.selectedColor = new Color(1f, 1f, 1f, 1f);
            button.colors = colors;
            button.targetGraphic = img;

            // BoxCollider（可选，VR 射线需要）
            if (addBoxCollider)
            {
                var col = go.AddComponent<BoxCollider>();
                col.isTrigger = true;
                col.size = new Vector3(0f, height, 10f);
            }

            // 按钮文字
            var tmp = CreateText(go.transform, text ?? "", fontSize, DefaultTextColor,
                TextAlignmentOptions.Center, localizationKey, width, height);

            // 点击回调
            if (onClick != null)
                button.onClick.AddListener(() => onClick());

            // 悬停颜色变化（通过 EventTrigger 实现精确控制）
            AddColorHoverEffect(go, img, normalColor ?? ButtonNormalColor, hoverColor ?? ButtonHoverColor);

            return button;
        }

        // ============================================================
        //  Vertical Layout
        // ============================================================

        /// <summary>
        /// 添加垂直布局组件
        /// </summary>
        public static VerticalLayoutGroup AddVerticalLayout(Transform parent, float spacing = 15f,
            float paddingLeft = 0f, float paddingRight = 0f,
            float paddingTop = 0f, float paddingBottom = 0f,
            TextAnchor childAlignment = TextAnchor.UpperCenter)
        {
            var vlg = parent.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = spacing;
            vlg.childAlignment = childAlignment;
            vlg.childControlWidth = false;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = false;
            vlg.childForceExpandHeight = false;
            vlg.padding = new RectOffset(
                (int)paddingLeft, (int)paddingRight,
                (int)paddingTop, (int)paddingBottom);
            return vlg;
        }

        /// <summary>
        /// 添加 Content Size Fitter（让容器自适应子物体大小）
        /// </summary>
        public static ContentSizeFitter AddContentSizeFitter(Transform parent,
            ContentSizeFitter.FitMode horizontal = ContentSizeFitter.FitMode.Unconstrained,
            ContentSizeFitter.FitMode vertical = ContentSizeFitter.FitMode.PreferredSize)
        {
            var fitter = parent.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = horizontal;
            fitter.verticalFit = vertical;
            return fitter;
        }

        /// <summary>
        /// 为 Layout 子元素设置 LayoutElement 约束
        /// </summary>
        public static LayoutElement SetLayoutElement(Transform target,
            float? minWidth = null, float? minHeight = null,
            float? preferredWidth = null, float? preferredHeight = null)
        {
            var le = target.gameObject.GetComponent<LayoutElement>();
            if (le == null) le = target.gameObject.AddComponent<LayoutElement>();

            if (minWidth.HasValue) le.minWidth = minWidth.Value;
            if (minHeight.HasValue) le.minHeight = minHeight.Value;
            if (preferredWidth.HasValue) le.preferredWidth = preferredWidth.Value;
            if (preferredHeight.HasValue) le.preferredHeight = preferredHeight.Value;

            return le;
        }

        // ============================================================
        //  效果组件
        // ============================================================

        /// <summary>
        /// 添加 CurvedUIEffect 弯曲效果
        /// </summary>
        public static CurvedUIEffect AddCurvedUIEffect(GameObject go, float radius = DefaultCurveRadius,
            float multiplier = 1f, int tessellation = 1)
        {
            var effect = go.AddComponent<CurvedUIEffect>();
            effect.curveRadius = radius;
            effect.curveMultiplier = multiplier;
            effect.tessellationSegments = tessellation;
            return effect;
        }

        /// <summary>
        /// 添加悬停变色效果（PointerEnter/PointerExit）
        /// </summary>
        public static void AddColorHoverEffect(GameObject go, Image targetImage,
            Color normalColor, Color hoverColor)
        {
            var trigger = go.AddComponent<EventTrigger>();

            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => { if (targetImage != null) targetImage.color = hoverColor; });
            trigger.triggers.Add(enter);

            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(_ => { if (targetImage != null) targetImage.color = normalColor; });
            trigger.triggers.Add(exit);
        }

        /// <summary>
        /// 添加悬停音效（PointerEnter 时播放）
        /// </summary>
        public static void AddHoverSoundEffect(GameObject go, AudioSource audioSource, AudioClip clip,
            float volume = 0.5f)
        {
            if (audioSource == null || clip == null) return;

            var trigger = go.AddComponent<EventTrigger>();
            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => audioSource.PlayOneShot(clip, volume));
            trigger.triggers.Add(enter);
        }

        // ============================================================
        //  坐标与锚点工具
        // ============================================================

        /// <summary>
        /// 设置 RectTransform 锚点为居中
        /// </summary>
        public static void SetAnchorCenter(RectTransform rt)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
        }

        /// <summary>
        /// 设置 RectTransform 锚点为拉伸（填充父物体）
        /// </summary>
        public static void SetAnchorStretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
        }

        /// <summary>
        /// 设置 anchoredPosition
        /// </summary>
        public static void SetPosition(RectTransform rt, float x, float y)
        {
            rt.anchoredPosition = new Vector2(x, y);
        }
    }
}
