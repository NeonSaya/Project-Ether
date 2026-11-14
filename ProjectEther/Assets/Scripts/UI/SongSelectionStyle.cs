using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace OsuVR
{
    /// <summary>选歌与 Mod 页共用的扁平化配色及控件样式。</summary>
    internal static class SongSelectionStyle
    {
        internal static readonly Color Background = new Color32(12, 15, 27, 255);
        internal static readonly Color Panel = new Color32(18, 22, 37, 255);
        internal static readonly Color Control = new Color32(32, 38, 58, 255);
        internal static readonly Color Selected = new Color32(35, 64, 81, 255);
        internal static readonly Color Text = new Color32(235, 239, 249, 255);
        internal static readonly Color Muted = new Color32(160, 172, 196, 255);
        internal static readonly Color Accent = new Color32(105, 209, 233, 255);
        internal static readonly Color Pink = new Color32(205, 92, 147, 255);
        internal static readonly Color Line = new Color32(54, 64, 85, 255);

        /// <summary>按原始比例居中裁剪，填满预览框，不拉伸图片。</summary>
        internal static Rect CoverUvRect(Vector2 textureSize, Vector2 frameSize)
        {
            if (textureSize.x <= 0 || textureSize.y <= 0 || frameSize.x <= 0 || frameSize.y <= 0)
                return new Rect(0, 0, 1, 1);
            float sourceAspect = textureSize.x / textureSize.y;
            float targetAspect = frameSize.x / frameSize.y;
            if (sourceAspect > targetAspect)
            {
                float width = targetAspect / sourceAspect;
                return new Rect((1 - width) * 0.5f, 0, width, 1);
            }
            float height = sourceAspect / targetAspect;
            return new Rect(0, (1 - height) * 0.5f, 1, height);
        }

        internal static RectTransform Rect(
            Transform parent,
            string name,
            float x,
            float y,
            float width,
            float height
        )
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            Place(rect, x, y, width, height);
            return rect;
        }

        internal static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        internal static Image Fill(RectTransform rect, Color color, bool raycast = false)
        {
            var image = rect.GetComponent<Image>() ?? rect.gameObject.AddComponent<Image>();
            image.sprite = null;
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        internal static TextMeshProUGUI Label(
            Transform parent,
            string name,
            TMP_FontAsset font,
            float size = 16f,
            TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft
        )
        {
            var rect = Rect(parent, name, 0, 0, 100, Mathf.Ceil(size * 1.6f + 4));
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font != null ? font : TMP_Settings.defaultFontAsset;
            ConfigureText(text, size, alignment);
            text.text = "";
            return text;
        }

        internal static void ConfigureText(
            TextMeshProUGUI text,
            float size,
            TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft
        )
        {
            text.fontSize = size;
            text.color = Text;
            text.enableAutoSizing = false;
            text.enableWordWrapping = false;
            text.richText = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.alignment = alignment;
            text.raycastTarget = false;
        }

        internal static Button Button(
            Transform parent,
            string name,
            string label,
            TMP_FontAsset font,
            UnityAction action,
            float x,
            float y,
            float width,
            float height,
            float size = 15f
        )
        {
            var rect = Rect(parent, name, x, y, width, height);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = Fill(rect, Color.white, true);
            ConfigureButton(button, Control);
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var text = Label(rect, "Text", font, size, TextAlignmentOptions.Center);
            Place(text.rectTransform, 6, 0, width - 12, height);
            text.text = label;
            if (action != null)
                button.onClick.AddListener(action);
            return button;
        }

        internal static void ConfigureButton(Button button, Color color)
        {
            if (button.targetGraphic != null)
                button.targetGraphic.color = Color.white;
            var colors = button.colors;
            colors.normalColor = color;
            colors.highlightedColor = Color.Lerp(color, Accent, 0.18f);
            colors.pressedColor = Color.Lerp(color, Accent, 0.32f);
            colors.selectedColor = color;
            colors.disabledColor = new Color(0.14f, 0.16f, 0.21f, 0.65f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.transition = Selectable.Transition.ColorTint;
            if (button.targetGraphic != null)
                button.targetGraphic.CrossFadeColor(
                    button.IsInteractable() ? color : colors.disabledColor,
                    0,
                    true,
                    true
                );
        }

        internal static Scrollbar AddScrollbar(ScrollRect scroll, string name)
        {
            var track = Rect(scroll.transform, name, 0, 0, 18, 100);
            track.anchorMin = new Vector2(1, 0);
            track.anchorMax = new Vector2(1, 1);
            track.pivot = new Vector2(1, 0.5f);
            track.offsetMin = new Vector2(-18, 0);
            track.offsetMax = Vector2.zero;
            Fill(track, new Color32(24, 30, 47, 255), true);
            var area = Rect(track, "SlidingArea", 0, 0, 18, 100);
            area.anchorMin = Vector2.zero;
            area.anchorMax = Vector2.one;
            area.offsetMin = new Vector2(3, 3);
            area.offsetMax = new Vector2(-3, -3);
            var handle = Rect(area, "Handle", 0, 0, 12, 50);
            handle.anchorMin = Vector2.zero;
            handle.anchorMax = Vector2.one;
            handle.offsetMin = handle.offsetMax = Vector2.zero;
            var graphic = Fill(handle, Color.white, true);
            var scrollbar = track.gameObject.AddComponent<Scrollbar>();
            scrollbar.handleRect = handle;
            scrollbar.targetGraphic = graphic;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            var colors = scrollbar.colors;
            colors.normalColor = Muted;
            colors.highlightedColor = Accent;
            colors.pressedColor = Accent;
            colors.selectedColor = Muted;
            scrollbar.colors = colors;
            scrollbar.navigation = new Navigation { mode = Navigation.Mode.None };
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            scroll.viewport.offsetMin = Vector2.zero;
            scroll.viewport.offsetMax = new Vector2(-24, 0);
            return scrollbar;
        }
    }
}
