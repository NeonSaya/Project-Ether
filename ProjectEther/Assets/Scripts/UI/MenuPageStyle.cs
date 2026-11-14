using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace OsuVR
{
    /// <summary>设置、结算与制作人员页共用的扁平控件；只负责呈现，不订阅业务回调。</summary>
    internal static class MenuPageStyle
    {
        private static TMP_FontAsset font;

        internal static TMP_FontAsset Font
        {
            get
            {
                if (font == null)
                    font = Resources.Load<TMP_FontAsset>(
                        "Fonts & Materials/SourceHanSansSC-Regular SDF"
                    );
                return font != null ? font : TMP_Settings.defaultFontAsset;
            }
        }

        internal static RectTransform Rect(
            Transform parent,
            string name,
            float x,
            float y,
            float width,
            float height
        ) => SongSelectionStyle.Rect(parent, name, x, y, width, height);

        internal static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        internal static void Backdrop(RectTransform parent)
        {
            var rect = Rect(parent, "MenuBackdrop", 0, 0, 1, 1);
            Stretch(rect);
            rect.SetAsFirstSibling();
            var graphic = rect.gameObject.AddComponent<SongSelectionBackdropGraphic>();
            graphic.Configure(new Color32(16, 20, 35, 255), new Color32(26, 33, 51, 255), 0, 1, 10);
        }

        internal static TextMeshProUGUI Label(
            Transform parent,
            string name,
            string text,
            string key,
            float x,
            float y,
            float width,
            float height,
            float size = 14,
            Color? color = null,
            TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft
        )
        {
            var label = SongSelectionStyle.Label(parent, name, Font, size, alignment);
            SongSelectionStyle.Place(label.rectTransform, x, y, width, height);
            label.text = text;
            label.color = color ?? SongSelectionStyle.Text;
            if (!string.IsNullOrEmpty(key))
                label.gameObject.AddComponent<LocalizedText>().localizationKey = key;
            return label;
        }

        internal static Button Button(
            Transform parent,
            string name,
            string text,
            string key,
            UnityAction onClick,
            float width = 240,
            float height = 50
        )
        {
            var button = SongSelectionStyle.Button(
                parent,
                name,
                text,
                Font,
                onClick,
                0,
                0,
                width,
                height,
                15
            );
            var label = button.GetComponentInChildren<TextMeshProUGUI>();
            Stretch(label.rectTransform);
            label.fontStyle = FontStyles.Bold;
            label.gameObject.AddComponent<LocalizedText>().localizationKey = key;
            return button;
        }

        internal static void ButtonStyle(Button button, Color color, float size = 15)
        {
            SongSelectionStyle.ConfigureButton(button, color);
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            foreach (var label in button.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                label.font = Font;
                SongSelectionStyle.ConfigureText(label, size, TextAlignmentOptions.Center);
                label.fontStyle = FontStyles.Bold;
                Stretch(label.rectTransform);
            }
        }

        internal static ScrollRect Scroll(
            Transform parent,
            string name,
            float x,
            float y,
            float width,
            float height,
            out RectTransform content
        )
        {
            var rect = Rect(parent, name, x, y, width, height);
            var scroll = rect.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.elasticity = 0.1f;
            scroll.scrollSensitivity = 50;
            var viewport = Rect(rect, "Viewport", 0, 0, width, height);
            Stretch(viewport);
            // 模板遮罩仍用不透明几何写入，只隐藏自身颜色。
            SongSelectionStyle.Fill(viewport, Color.white, true);
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            content = Rect(viewport, "ScrollContent", 0, 0, width, 0);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.sizeDelta = Vector2.zero;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;
            SongSelectionStyle.AddScrollbar(scroll, "Scrollbar");
            return scroll;
        }

        internal static void Divider(Transform parent)
        {
            var row = Rect(parent, "Divider", 0, 0, 100, 12);
            var layout = row.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = layout.preferredHeight = 12;
            var line = Rect(row, "Line", 0, 5, 100, 1);
            line.anchorMax = new Vector2(1, 1);
            line.sizeDelta = new Vector2(0, 1);
            SongSelectionStyle.Fill(line, SongSelectionStyle.Line);
        }

        private static void Width(Transform child, float width, float flexible = 0)
        {
            var layout =
                child.GetComponent<LayoutElement>()
                ?? child.gameObject.AddComponent<LayoutElement>();
            layout.minWidth = width;
            layout.preferredWidth = width;
            layout.flexibleWidth = flexible;
        }

        private static void Row(RectTransform row, float height, float labelWidth = 190)
        {
            row.sizeDelta = new Vector2(0, height);
            var group = row.GetComponent<HorizontalLayoutGroup>();
            group.childAlignment = TextAnchor.MiddleLeft;
            group.childForceExpandWidth = false;
            group.childForceExpandHeight = false;
            var label = row.Find("Label");
            if (label != null)
                Width(label, labelWidth);
            foreach (var text in row.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                text.font = Font;
                SongSelectionStyle.ConfigureText(text, 14);
            }
        }

        internal static void Slider(Slider slider, bool standalone = false)
        {
            if (!standalone)
            {
                var row = (RectTransform)slider.transform.parent;
                Row(row, 38);
                Width(slider.transform, 120, 1);
                var value = row.Find("ValueText");
                if (value != null)
                {
                    Width(value, 65);
                    value.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.MidlineRight;
                }
            }
            // 保留整条滑杆的热区，包含代码生成且原本没有根 Image 的音频偏移滑杆。
            var rootImage = slider.GetComponent<Image>() ?? slider.gameObject.AddComponent<Image>();
            rootImage.color = Color.clear;
            rootImage.raycastTarget = true;
            var background = (RectTransform)slider.transform.Find("Background");
            var fillArea = (RectTransform)slider.transform.Find("Fill Area");
            foreach (var track in new[] { background, fillArea })
            {
                track.anchorMin = new Vector2(0, 0.5f);
                track.anchorMax = new Vector2(1, 0.5f);
                track.anchoredPosition = Vector2.zero;
                track.sizeDelta = new Vector2(0, 8);
            }
            var backgroundImage = background.GetComponent<Image>();
            backgroundImage.color = SongSelectionStyle.Control;
            backgroundImage.raycastTarget = false;
            foreach (var tick in background.GetComponentsInChildren<Image>())
                if (tick != backgroundImage)
                {
                    tick.color = SongSelectionStyle.Line;
                    tick.raycastTarget = false;
                    tick.rectTransform.sizeDelta = new Vector2(1, 6);
                }
            var fill = slider.fillRect.GetComponent<Image>();
            fill.color = SongSelectionStyle.Accent;
            fill.raycastTarget = false;
            var handleArea = (RectTransform)slider.handleRect.parent;
            // Slider.UpdateVisuals 会把柄的纵向锚点重设为全拉伸，因此由父区域限定高度。
            handleArea.anchorMin = new Vector2(0, 0.5f);
            handleArea.anchorMax = new Vector2(1, 0.5f);
            handleArea.anchoredPosition = Vector2.zero;
            handleArea.sizeDelta = new Vector2(-12, 24);
            slider.handleRect.sizeDelta = new Vector2(12, 0);
            var handle = slider.handleRect.GetComponent<Image>();
            handle.color = SongSelectionStyle.Text;
            slider.targetGraphic = handle;
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            var colors = slider.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = SongSelectionStyle.Accent;
            colors.pressedColor = SongSelectionStyle.Accent;
            colors.selectedColor = Color.white;
            slider.colors = colors;
        }

        internal static void Toggle(Toggle toggle)
        {
            Row((RectTransform)toggle.transform.parent, 32);
            Width(toggle.transform.parent.Find("Label"), 165, 1);
            Width(toggle.transform, 36);
            var animation = toggle.GetComponent<ToggleCheckboxAnimation>();
            if (animation != null)
            {
                animation.offBgColor = SongSelectionStyle.Control;
                animation.onBgColor = SongSelectionStyle.Selected;
                animation.fillColor = SongSelectionStyle.Accent;
                var oldFill =
                    toggle.graphic
                    ?? toggle.transform.Find("Background/Fill").GetComponent<Image>();
                oldFill.name = "LegacyFill";
                var markRect = Rect(oldFill.transform.parent, "Fill", 0, 0, 20, 20);
                markRect.anchorMin = markRect.anchorMax = markRect.pivot = Vector2.one * 0.5f;
                markRect.anchoredPosition = Vector2.zero;
                var mark = markRect.gameObject.AddComponent<MenuCheckmarkGraphic>();
                mark.raycastTarget = false;
                oldFill.gameObject.SetActive(false);
                animation.SetFillGraphic(mark);
            }
            toggle.navigation = new Navigation { mode = Navigation.Mode.None };
        }

        internal static void Dropdown(TMP_Dropdown dropdown)
        {
            Row((RectTransform)dropdown.transform.parent, 38);
            Width(dropdown.transform, 150, 1);
            dropdown.GetComponent<Image>().color = SongSelectionStyle.Control;
            dropdown.navigation = new Navigation { mode = Navigation.Mode.None };
            dropdown.captionText.color = SongSelectionStyle.Text;
            var arrow = dropdown.transform.Find("Arrow");
            arrow.GetComponent<Image>().enabled = false;
            var mark = Rect(arrow, "Chevron", 0, 0, 10, 6);
            Stretch(mark);
            var chevron = mark.gameObject.AddComponent<MenuDropdownArrowGraphic>();
            chevron.color = SongSelectionStyle.Text;
            chevron.raycastTarget = false;
            var template = dropdown.template;
            template.sizeDelta = new Vector2(template.sizeDelta.x, 152);
            template.GetComponent<Image>().color = SongSelectionStyle.Control;
            var viewport = template.Find("Viewport");
            viewport.GetComponent<Image>().color = Color.white;
            viewport.GetComponent<Mask>().showMaskGraphic = false;
            var item = dropdown.itemText.GetComponentInParent<Toggle>(true);
            var itemRect = (RectTransform)item.transform;
            itemRect.sizeDelta = new Vector2(itemRect.sizeDelta.x, 36);
            item.GetComponent<LayoutElement>().preferredHeight = 36;
            var colors = item.colors;
            colors.normalColor = SongSelectionStyle.Control;
            colors.highlightedColor = SongSelectionStyle.Selected;
            colors.pressedColor = SongSelectionStyle.Selected;
            colors.selectedColor = SongSelectionStyle.Selected;
            item.colors = colors;
            item.GetComponent<Image>().color = Color.white;
            var oldCheck = item.graphic;
            var checkRect = Rect(oldCheck.transform, "Checkmark", 0, 0, 16, 16);
            Stretch(checkRect);
            var check = checkRect.gameObject.AddComponent<MenuCheckmarkGraphic>();
            check.raycastTarget = false;
            check.color = SongSelectionStyle.Accent;
            oldCheck.enabled = false;
            item.graphic = check;
            dropdown.itemText.font = Font;
            if (dropdown.itemText is TextMeshProUGUI itemText)
                SongSelectionStyle.ConfigureText(itemText, 14);
        }

        internal static void PairDropdowns(TMP_Dropdown left, TMP_Dropdown right)
        {
            var first = (RectTransform)left.transform.parent;
            var second = (RectTransform)right.transform.parent;
            var row = Rect(first.parent, "QualityFields", 0, 0, 100, 66);
            row.SetSiblingIndex(first.GetSiblingIndex());
            var group = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            group.spacing = 20;
            group.childControlWidth = group.childControlHeight = true;
            group.childForceExpandWidth = group.childForceExpandHeight = true;
            foreach (var field in new[] { first, second })
            {
                field.SetParent(row, false);
                field.GetComponent<HorizontalLayoutGroup>().enabled = false;
                Width(field, 0, 1);
                var label = (RectTransform)field.Find("Label");
                var control = (RectTransform)field.GetComponentInChildren<TMP_Dropdown>().transform;
                foreach (var child in new[] { label, control })
                {
                    child.anchorMin = new Vector2(0, 1);
                    child.anchorMax = new Vector2(1, 1);
                    child.pivot = new Vector2(0.5f, 1);
                    child.sizeDelta = new Vector2(0, 30);
                }
                label.anchoredPosition = Vector2.zero;
                control.anchoredPosition = new Vector2(0, -32);
            }
        }
    }
}
