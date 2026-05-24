using System.Collections.Generic;
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

        // ============================================================
        //  Horizontal Layout
        // ============================================================

        /// <summary>
        /// 添加水平布局组件
        /// </summary>
        public static HorizontalLayoutGroup AddHorizontalLayout(Transform parent,
            float spacing = 0f, TextAnchor childAlignment = TextAnchor.MiddleCenter,
            float paddingLeft = 0f, float paddingRight = 0f,
            float paddingTop = 0f, float paddingBottom = 0f)
        {
            var hlg = parent.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = spacing;
            hlg.childAlignment = childAlignment;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.padding = new RectOffset(
                (int)paddingLeft, (int)paddingRight,
                (int)paddingTop, (int)paddingBottom);
            return hlg;
        }

        // ============================================================
        //  ScrollView
        // ============================================================

        /// <summary>
        /// 创建 ScrollView（ScrollRect + Viewport(Mask) + Content）
        /// </summary>
        /// <returns>(scrollRect, contentRectTransform)</returns>
        public static (ScrollRect scrollRect, RectTransform content) CreateScrollView(
            Transform parent, string name, float width, float height,
            Color? viewportColor = null)
        {
            // ScrollView root
            var scrollGo = new GameObject(name);
            scrollGo.transform.SetParent(parent, false);
            var scrollRt = scrollGo.AddComponent<RectTransform>();
            scrollRt.sizeDelta = new Vector2(width, height);

            var scrollRect = scrollGo.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 30f;

            // Viewport
            var viewportGo = new GameObject("Viewport");
            viewportGo.transform.SetParent(scrollGo.transform, false);
            var viewportRt = viewportGo.AddComponent<RectTransform>();
            SetAnchorStretch(viewportRt);

            var viewportImg = viewportGo.AddComponent<Image>();
            viewportImg.color = viewportColor ?? new Color(0, 0, 0, 0.01f);
            viewportImg.raycastTarget = true;

            var mask = viewportGo.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            // Content
            var contentGo = new GameObject("Content");
            contentGo.transform.SetParent(viewportGo.transform, false);
            var contentRt = contentGo.AddComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0, 1);
            contentRt.anchorMax = new Vector2(1, 1);
            contentRt.pivot = new Vector2(0.5f, 1);
            contentRt.sizeDelta = new Vector2(0, 0);
            contentRt.anchoredPosition = Vector2.zero;

            scrollRect.viewport = viewportRt;
            scrollRect.content = contentRt;

            return (scrollRect, contentRt);
        }

        // ============================================================
        //  Atomic Prefab Instantiation
        //
        //  原子预制体通过 HLG + LayoutElement 参与父级布局：
        //  - root: HLG(childControlWidth=true) + LayoutElement(flexibleWidth=1)
        //  - Label 子物体: LayoutElement(preferredWidth=160)
        //  - Slider 子物体: LayoutElement(preferredWidth=280, flexibleWidth=1)
        //  - Toggle 子物体: LayoutElement(preferredWidth=32)
        //  - Dropdown 子物体: LayoutElement(preferredWidth=180, flexibleWidth=1)
        //  - ValueText 子物体: LayoutElement(preferredWidth=70)
        // ============================================================

        private const float LabelPreferredWidth = 160f;
        private const float SliderPreferredWidth = 280f;
        private const float TogglePreferredWidth = 32f;
        private const float DropdownPreferredWidth = 180f;
        private const float ValuePreferredWidth = 70f;

        /// <summary>
        /// 为原子预制体 root 配置 HLG，使其作为行内元素参与父级 HLG 布局
        /// </summary>
        private static void SetupAtomicPrefabRoot(RectTransform root, float preferredHeight)
        {
            // 重置缩放（预制体自带 0.0025 缩放，与 Canvas 缩放叠加会不可见）
            root.localScale = Vector3.one;

            // 水平拉伸 + 垂直居中，固定高度（匹配 Prefab: anchor(0,0.5)→(1,0.5), sizeDelta(0,H)）
            root.anchorMin = new Vector2(0f, 0.5f);
            root.anchorMax = new Vector2(1f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(0f, preferredHeight);
            root.anchoredPosition = Vector2.zero;

            // 重置所有子物体的锚点和位置，让 HLG 接管布局
            for (int i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i) as RectTransform;
                if (child == null) continue;
                child.anchorMin = new Vector2(0.5f, 0.5f);
                child.anchorMax = new Vector2(0.5f, 0.5f);
                child.pivot = new Vector2(0.5f, 0.5f);
                child.anchoredPosition = Vector2.zero;
            }

            // 添加 HLG 让子物体水平排列（匹配 Prefab: childAlignment=LowerLeft, spacing=10）
            var hlg = root.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 10f;
            hlg.childAlignment = TextAnchor.LowerLeft;
            hlg.childControlWidth = true;
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;

            LayoutRebuilder.ForceRebuildLayoutImmediate(root);
        }

        /// <summary>
        /// 为子物体添加或设置 LayoutElement
        /// </summary>
        private static void SetChildLayoutElement(Transform child, float preferredWidth,
            float flexibleWidth = 0f)
        {
            if (child == null) return;
            var le = child.gameObject.GetComponent<LayoutElement>();
            if (le == null) le = child.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = preferredWidth;
            le.flexibleWidth = flexibleWidth;
        }

        /// <summary>
        /// 实例化原子 Slider 预制体，配置 Label、范围、值显示和回调
        /// </summary>
        /// <returns>返回 Slider 组件引用</returns>
        public static Slider InstantiateSliderPrefab(GameObject prefab, Transform parent,
            string label, string localizationKey, float minVal, float maxVal, float currentVal,
            string valueFormat, UnityEngine.Events.UnityAction<float> onValueChanged,
            float valueScale = 1f)
        {
            var instance = UnityEngine.Object.Instantiate(prefab, parent);
            var root = instance.GetComponent<RectTransform>();

            // 配置 HLG + 布局（Slider 行高 36）
            SetupAtomicPrefabRoot(root, 36f);

            // Label
            var labelTf = instance.transform.Find("Label");
            if (labelTf != null)
            {
                SetChildLayoutElement(labelTf, LabelPreferredWidth);
                var tmp = labelTf.GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                {
                    tmp.text = label;
                    if (!string.IsNullOrEmpty(localizationKey))
                    {
                        var lt = labelTf.gameObject.GetComponent<LocalizedText>();
                        if (lt == null) lt = labelTf.gameObject.AddComponent<LocalizedText>();
                        lt.localizationKey = localizationKey;
                    }
                }
            }

            // Slider
            var slider = instance.GetComponentInChildren<Slider>();
            if (slider != null)
            {
                SetChildLayoutElement(slider.transform, SliderPreferredWidth, 1f);
                slider.minValue = minVal;
                slider.maxValue = maxVal;
                slider.value = currentVal;
                if (onValueChanged != null)
                    slider.onValueChanged.AddListener(onValueChanged);
            }

            // ValueText
            var valueTextTf = instance.transform.Find("ValueText");
            TextMeshProUGUI valueTmp = null;
            if (valueTextTf != null)
            {
                SetChildLayoutElement(valueTextTf, ValuePreferredWidth);
                valueTmp = valueTextTf.GetComponent<TextMeshProUGUI>();
                if (valueTmp != null)
                    valueTmp.text = string.Format(valueFormat, currentVal * valueScale);
            }

            // Wire slider value change to update text
            if (slider != null && valueTmp != null)
            {
                var capturedFormat = valueFormat;
                var capturedScale = valueScale;
                var capturedTmp = valueTmp;
                slider.onValueChanged.AddListener(v =>
                {
                    capturedTmp.text = string.Format(capturedFormat, v * capturedScale);
                });
            }

            return slider;
        }

        /// <summary>
        /// 实例化原子 Toggle 预制体，配置 Label、初始状态和回调
        /// </summary>
        /// <returns>返回 Toggle 组件引用</returns>
        public static Toggle InstantiateTogglePrefab(GameObject prefab, Transform parent,
            string label, string localizationKey, bool isOn,
            UnityEngine.Events.UnityAction<bool> onValueChanged)
        {
            var instance = UnityEngine.Object.Instantiate(prefab, parent);
            var root = instance.GetComponent<RectTransform>();

            // 配置 HLG + LayoutElement（Toggle 行高 32）
            SetupAtomicPrefabRoot(root, 32f);

            // Label
            var labelTf = instance.transform.Find("Label");
            if (labelTf != null)
            {
                SetChildLayoutElement(labelTf, LabelPreferredWidth);
                var tmp = labelTf.GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                {
                    tmp.text = label;
                    if (!string.IsNullOrEmpty(localizationKey))
                    {
                        var lt = labelTf.gameObject.GetComponent<LocalizedText>();
                        if (lt == null) lt = labelTf.gameObject.AddComponent<LocalizedText>();
                        lt.localizationKey = localizationKey;
                    }
                }
            }

            // Toggle
            var toggle = instance.GetComponentInChildren<Toggle>();
            if (toggle != null)
            {
                SetChildLayoutElement(toggle.transform, TogglePreferredWidth);
                toggle.isOn = isOn;
                if (onValueChanged != null)
                    toggle.onValueChanged.AddListener(onValueChanged);
            }

            return toggle;
        }

        /// <summary>
        /// 实例化原子 Dropdown 预制体，配置 Label、选项和回调
        /// </summary>
        /// <returns>返回 TMP_Dropdown 组件引用</returns>
        public static TMP_Dropdown InstantiateDropdownPrefab(GameObject prefab, Transform parent,
            string label, string localizationKey, List<string> options, int currentIndex,
            UnityEngine.Events.UnityAction<int> onValueChanged)
        {
            var instance = UnityEngine.Object.Instantiate(prefab, parent);
            var root = instance.GetComponent<RectTransform>();

            // 配置 HLG + LayoutElement（Dropdown 行高 36）
            SetupAtomicPrefabRoot(root, 36f);

            // Label
            var labelTf = instance.transform.Find("Label");
            if (labelTf != null)
            {
                SetChildLayoutElement(labelTf, LabelPreferredWidth);
                var tmp = labelTf.GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                {
                    tmp.text = label;
                    if (!string.IsNullOrEmpty(localizationKey))
                    {
                        var lt = labelTf.gameObject.GetComponent<LocalizedText>();
                        if (lt == null) lt = labelTf.gameObject.AddComponent<LocalizedText>();
                        lt.localizationKey = localizationKey;
                    }
                }
            }

            // Dropdown
            var dropdown = instance.GetComponentInChildren<TMP_Dropdown>();
            if (dropdown != null)
            {
                SetChildLayoutElement(dropdown.transform, DropdownPreferredWidth, 1f);
                dropdown.ClearOptions();
                if (options != null && options.Count > 0)
                    dropdown.AddOptions(options);
                dropdown.value = Mathf.Clamp(currentIndex, 0, Mathf.Max(0, options != null ? options.Count - 1 : 0));
                dropdown.RefreshShownValue();
                if (onValueChanged != null)
                    dropdown.onValueChanged.AddListener(onValueChanged);
            }

            return dropdown;
        }

        /// <summary>
        /// 重新设置原子预制体子物体的值文本（用于 RefreshUI）
        /// </summary>
        public static void UpdateSliderValueText(Slider slider, string valueFormat, float valueScale = 1f)
        {
            if (slider == null) return;
            // 在预制体实例中查找 ValueText 兄弟节点
            Transform valueTextTf = null;
            var parent = slider.transform.parent;
            if (parent != null)
            {
                for (int i = 0; i < parent.childCount; i++)
                {
                    if (parent.GetChild(i).name == "ValueText")
                    {
                        valueTextTf = parent.GetChild(i);
                        break;
                    }
                }
            }
            if (valueTextTf != null)
            {
                var tmp = valueTextTf.GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                    tmp.text = string.Format(valueFormat, slider.value * valueScale);
            }
        }
    }
}
