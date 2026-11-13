using System.Globalization;
using TMPro;
using UnityEngine;

namespace OsuVR
{
    /// <summary>可用现有 UI 射线点击的搜索键盘，不依赖平台软键盘。</summary>
    public sealed class SongSearchKeyboard : MonoBehaviour
    {
        private TMP_InputField input;
        private TextMeshProUGUI heading;
        private UnityEngine.UI.Button paste;
        private UnityEngine.UI.Button space;
        private UnityEngine.UI.Button backspace;
        private UnityEngine.UI.Button done;

        public void Build(Transform parent, TMP_InputField field, TMP_FontAsset font)
        {
            input = field;
            transform.SetParent(parent, false);
            var rect = (RectTransform)transform;
            SongSelectionStyle.Place(rect, 24, 326, 702, 278);
            SongSelectionStyle.Fill(rect, SongSelectionStyle.Panel, true);
            heading = SongSelectionStyle.Label(rect, "KeyboardHeading", font, 16);
            SongSelectionStyle.Place(heading.rectTransform, 14, 8, 670, 30);
            string[] rows = { "1234567890", "qwertyuiop", "asdfghjkl-", "zxcvbnm,./" };
            for (int row = 0; row < rows.Length; row++)
            for (int column = 0; column < rows[row].Length; column++)
            {
                string key = rows[row][column].ToString();
                SongSelectionStyle.Button(
                    rect,
                    "Key_" + key,
                    key,
                    font,
                    () => Insert(key),
                    14 + column * 68,
                    44 + row * 44,
                    62,
                    38,
                    17
                );
            }
            paste = SongSelectionStyle.Button(
                rect,
                "Paste",
                "",
                font,
                () => Insert(GUIUtility.systemCopyBuffer),
                14,
                224,
                160,
                40
            );
            space = SongSelectionStyle.Button(
                rect,
                "Space",
                "",
                font,
                () => Insert(" "),
                182,
                224,
                160,
                40
            );
            backspace = SongSelectionStyle.Button(
                rect,
                "Backspace",
                "",
                font,
                Backspace,
                350,
                224,
                160,
                40
            );
            done = SongSelectionStyle.Button(rect, "Done", "", font, Close, 518, 224, 170, 40);
            SongSelectionStyle.ConfigureButton(done, SongSelectionStyle.Selected);
            RefreshLanguage();
            gameObject.SetActive(false);
        }

        public void Show()
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        public void Close()
        {
            gameObject.SetActive(false);
            if (input != null)
                input.DeactivateInputField();
        }

        public void Insert(string value)
        {
            if (input == null || string.IsNullOrEmpty(value))
                return;
            string text = input.text;
            int start = Mathf.Clamp(
                Mathf.Min(input.selectionStringAnchorPosition, input.selectionStringFocusPosition),
                0,
                text.Length
            );
            int end = Mathf.Clamp(
                Mathf.Max(input.selectionStringAnchorPosition, input.selectionStringFocusPosition),
                start,
                text.Length
            );
            value = value.Replace('\r', ' ').Replace('\n', ' ');
            string next = text.Remove(start, end - start).Insert(start, value);
            if (next.Length > 128)
            {
                next = next.Substring(0, 128);
                if (char.IsHighSurrogate(next[next.Length - 1]))
                    next = next.Substring(0, next.Length - 1);
            }
            input.text = next;
            int position = Mathf.Min(start + value.Length, next.Length);
            input.selectionStringAnchorPosition = input.selectionStringFocusPosition = position;
            input.ForceLabelUpdate();
        }

        public void Backspace()
        {
            if (input == null)
                return;
            string text = input.text;
            int start = Mathf.Clamp(
                Mathf.Min(input.selectionStringAnchorPosition, input.selectionStringFocusPosition),
                0,
                text.Length
            );
            int end = Mathf.Clamp(
                Mathf.Max(input.selectionStringAnchorPosition, input.selectionStringFocusPosition),
                start,
                text.Length
            );
            if (start == end)
            {
                if (start == 0)
                    return;
                int[] elements = StringInfo.ParseCombiningCharacters(text.Substring(0, start));
                start = elements[elements.Length - 1];
            }
            input.text = text.Remove(start, end - start);
            input.selectionStringAnchorPosition = input.selectionStringFocusPosition = start;
            input.ForceLabelUpdate();
        }

        public void RefreshLanguage()
        {
            if (heading == null)
                return;
            heading.text = LocalizationManager.GetText("ui_library_search");
            paste.GetComponentInChildren<TextMeshProUGUI>().text = LocalizationManager.GetText(
                "ui_library_paste"
            );
            space.GetComponentInChildren<TextMeshProUGUI>().text = LocalizationManager.GetText(
                "ui_library_space"
            );
            backspace.GetComponentInChildren<TextMeshProUGUI>().text = LocalizationManager.GetText(
                "ui_library_backspace"
            );
            done.GetComponentInChildren<TextMeshProUGUI>().text = LocalizationManager.GetText(
                "ui_library_done"
            );
        }
    }
}
