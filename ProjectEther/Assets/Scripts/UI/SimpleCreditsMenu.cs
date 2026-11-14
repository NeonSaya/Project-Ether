using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OsuVR
{
    /// <summary>制作人员与完整开源许可的单列滚动页；正文集中在 C# 层。</summary>
    public class SimpleCreditsMenu : MonoBehaviour
    {
        [Header("音效资源（需在 Inspector 中配置）")]
        public AudioClip hoverSound;
        public AudioClip clickSound;

        // ---- 布局常量（与 SimpleVRSettingsMenu 一致） ----
        private const float CanvasLocalZ = 1.5f;
        private const float CanvasScale = 0.0025f;
        private const int SortingOrder = 100;
        private const float CanvasWidth = 660f;
        private const float CanvasHeight = 495f;

        // ---- 颜色（沿用设置菜单配色） ----
        private static readonly Color SectionTitleColor = SongSelectionStyle.Accent;
        private static readonly Color GroupTitleColor = SongSelectionStyle.Muted;
        private static readonly Color EntryColor = SongSelectionStyle.Text;
        private static readonly Color MutedColor = SongSelectionStyle.Muted;

        // ---- 开源许可全文（所有文本集中在 C# 层，改这里即可） ----
        private const string MitLicenseText =
            "Permission is hereby granted, free of charge, to any person obtaining a copy "
            + "of this software and associated documentation files (the \"Software\"), to deal "
            + "in the Software without restriction, including without limitation the rights "
            + "to use, copy, modify, merge, publish, distribute, sublicense, and/or sell "
            + "copies of the Software, and to permit persons to whom the Software is "
            + "furnished to do so, subject to the following conditions:\n\n"
            + "The above copyright notice and this permission notice shall be included in all "
            + "copies or substantial portions of the Software.\n\n"
            + "THE SOFTWARE IS PROVIDED \"AS IS\", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR "
            + "IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, "
            + "FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE "
            + "AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER "
            + "LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, "
            + "OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE "
            + "SOFTWARE.";

        private const string UnlicenseText =
            "This is free and unencumbered software released into the public domain.\n\n"
            + "Anyone is free to copy, modify, publish, use, compile, sell, or distribute this "
            + "software, either in source code form or as a compiled binary, for any purpose, "
            + "commercial or non-commercial, and by any means.\n\n"
            + "In jurisdictions that recognize copyright laws, the author or authors of this "
            + "software dedicate any and all copyright interest in the software to the public "
            + "domain. We make this dedication for the benefit of the public at large and to "
            + "the detriment of our heirs and successors. We intend this dedication to be an "
            + "overt act of relinquishment in perpetuity of all present and future rights to "
            + "this software under copyright law.\n\n"
            + "THE SOFTWARE IS PROVIDED \"AS IS\", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR "
            + "IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, "
            + "FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE "
            + "AUTHORS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN "
            + "ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION "
            + "WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.\n\n"
            + "For more information, please refer to <https://unlicense.org>";

        private AudioSource audioSource;

        // ============================================================
        //  生命周期
        // ============================================================

        void Start()
        {
            BuildUI();
            LocalizationManager.ReloadAndNotify();
        }

        // ============================================================
        //  UI 构建
        // ============================================================

        private void BuildUI()
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0;
            var canvas = UILayoutHelper.CreateCanvas("CreditsCanvas", CanvasWidth, CanvasHeight);
            canvas.sortingOrder = SortingOrder;
            canvas.transform.SetParent(transform, false);
            canvas.transform.localPosition = new Vector3(0, 0, CanvasLocalZ);
            canvas.transform.localScale = Vector3.one * CanvasScale;
            canvas.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 10;

            var container = MenuPageStyle.Rect(
                canvas.transform,
                "CreditsContainer",
                0,
                0,
                660,
                495
            );
            MenuPageStyle.Stretch(container);
            SongSelectionStyle.Fill(container, Color.clear, true);
            MenuPageStyle.Backdrop(container);
            var header = MenuPageStyle.Rect(container, "TitleBar", 28, 8, 604, 44);
            MenuPageStyle.Label(header, "Title", "Credits", "ui_credits", 0, 0, 300, 44, 22);
            MenuPageStyle.Label(
                header,
                "Version",
                $"Project Ether v{Application.version}",
                null,
                310,
                8,
                294,
                30,
                12,
                MutedColor,
                TextAlignmentOptions.MidlineRight
            );
            var line = MenuPageStyle.Rect(container, "HeaderLine", 28, 54, 604, 1);
            SongSelectionStyle.Fill(line, SongSelectionStyle.Line);

            MenuPageStyle.Scroll(container, "ContentArea", 28, 68, 604, 338, out var content);
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(8, 10, 4, 12);
            layout.spacing = 6;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            BuildSections(content);

            var bottom = MenuPageStyle.Rect(container, "BottomButtons", 28, 425, 604, 50);
            var bottomLayout = bottom.gameObject.AddComponent<HorizontalLayoutGroup>();
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
            StartCoroutine(NotifyRayControllerNextFrame());
        }

        // ============================================================
        //  正文内容（节 → 分组 → 条目），许可全文保持原样
        // ============================================================

        private void BuildSections(RectTransform parent)
        {
            // ============ 第 1 节：开发者 ============
            AddSectionTitle(parent, "Development", "ui_credits_section_dev");
            AddEntry(parent, "NyaonSaya><", 21f, EntryColor, FontStyles.Bold);
            AddEntry(parent, "Project Ether · Design & Code", 13f, MutedColor);
            AddSpacing(parent, 8f);

            AddGroupTitle(parent, "A Word from the Developer", "ui_credits_section_word");
            var wordTmp = CreateText(
                parent,
                "",
                13f,
                EntryColor,
                TextAlignmentOptions.Left,
                FontStyles.Italic,
                "ui_credits_word_text"
            );
            wordTmp.raycastTarget = false;
            MenuPageStyle.Divider(parent);

            // ============ 第 2 节：开源鸣谢 ============
            AddSectionTitle(parent, "Open Source Acknowledgments", "ui_credits_section_opensource");

            AddGroupTitle(parent, "Gameplay & Beatmap Format", null);
            AddEntry(
                parent,
                "osu! (peppy) — the soul of this project's gameplay, and its open beatmap format (.osu)"
            );
            AddEntry(
                parent,
                "osu!lazer [MIT] — reference implementation for storyboard command evaluation"
            );
            AddEntry(
                parent,
                "storybrew (Damnae) [MIT] — storyboard timeline & rendering reference"
            );
            AddEntry(parent, "osu-droid [Apache-2.0] — reference for the beatmap parser");
            AddEntry(parent, "OsuParsers (mrflashstudio) [MIT] — .osu/.osb decoding reference");
            AddSpacing(parent, 8f);

            AddGroupTitle(parent, "Audio", null);
            AddEntry(
                parent,
                "Lasp (Keijiro Takahashi) [Unlicense] — low-latency audio FFT capture"
            );
            AddEntry(parent, "AudioLink (llealloo) [MIT] — audio-reactive visual data");
            AddSpacing(parent, 8f);

            AddGroupTitle(parent, "Graphics", null);
            AddEntry(
                parent,
                "X-PostProcessing-Library (QianMo) [MIT] — URP post-processing filters"
            );
            AddEntry(parent, "Effekseer [MIT] — open-source particle effect toolchain");
            AddSpacing(parent, 8f);

            AddGroupTitle(parent, "Engine & Tools", null);
            AddEntry(
                parent,
                "Unity [Unity Companion License] — URP / XR Interaction Toolkit / VFX Graph / TextMeshPro / Input System"
            );
            AddEntry(
                parent,
                "Graphy (Tayx94 / Martín Pane) [MIT] - FPS, Memory & Audio stats and graphs\n"
                    + "https://github.com/Tayx94/graphy"
            );
            AddEntry(parent, "Liberation Sans font [SIL OFL 1.1]");
            AddEntry(
                parent,
                "Source Han Sans (Adobe & Google) [SIL OFL 1.1] — CJK UI font (Simplified Chinese / Japanese)"
            );
            AddSpacing(parent, 12f);

            // ============ 第 3 节：开源许可 ============
            AddSectionTitle(parent, "Open Source Licenses", "ui_credits_section_licenses");

            AddEntry(
                parent,
                "MIT License — applies to: osu!lazer (c) ppy Pty Ltd & contributors, storybrew (c) Damnae, OsuParsers (c) mrflashstudio, AudioLink (c) llealloo, X-PostProcessing-Library (c) contributors, Effekseer (c) contributors, Graphy",
                12f,
                MutedColor
            );
            AddEntry(parent, "Copyright (c) 2018 Martín Pane", 12f, MutedColor);
            AddParagraph(parent, MitLicenseText, 11f);
            AddSpacing(parent, 10f);

            AddEntry(
                parent,
                "GNU General Public License v3.0 — applies to: Project Ether (c) 2026 NyaonSaya><. See https://www.gnu.org/licenses/gpl-3.0.html",
                12f,
                MutedColor
            );
            AddSpacing(parent, 10f);

            AddEntry(
                parent,
                "The Unlicense — applies to: Lasp (c) Keijiro Takahashi",
                12f,
                MutedColor
            );
            AddParagraph(parent, UnlicenseText, 11f);
            AddSpacing(parent, 10f);

            AddEntry(
                parent,
                "Apache License 2.0 — applies to: osu-droid. Licensed under the Apache License, Version 2.0. See https://www.apache.org/licenses/LICENSE-2.0",
                12f,
                MutedColor
            );
            AddSpacing(parent, 10f);

            AddEntry(
                parent,
                "Liberation Sans and Source Han Sans fonts are licensed under the SIL Open Font License 1.1 (https://openfontlicense.org).",
                12f,
                MutedColor
            );
            AddEntry(
                parent,
                "X-PostProcessing-Library shader sources are derived from Unity's Post Processing Stack and remain under the Unity Companion License.",
                12f,
                MutedColor
            );
            AddEntry(
                parent,
                "Unity Editor & Engine components are subject to the Unity Companion License. See https://unity.com/legal",
                12f,
                MutedColor
            );
            AddSpacing(parent, 12f);

            // ============ 第 4 节：版权与商标 ============
            AddSectionTitle(parent, "Copyright & Trademarks", "ui_credits_section_disclaimer");
            AddEntry(
                parent,
                "Project Ether is a free, fan-made, non-commercial VR beatmap player. It is not affiliated with osu! or ppy Pty Ltd."
            );
            AddEntry(parent, "\"osu!\" is a trademark of ppy Pty Ltd.");
            AddEntry(
                parent,
                "All beatmaps, music, videos and storyboards belong to their original creators and artists."
            );
            AddEntry(
                parent,
                "Project Ether (c) 2026 NyaonSaya>< — open source under the GNU General Public License v3.0."
            );
        }

        // ============================================================
        //  内容构建 Helper
        // ============================================================

        /// <summary>节标题（三语本地化，蓝色加粗）</summary>
        private void AddSectionTitle(RectTransform parent, string defaultText, string locKey)
        {
            var tmp = CreateText(
                parent,
                defaultText,
                16f,
                SectionTitleColor,
                TextAlignmentOptions.Left,
                FontStyles.Bold,
                locKey
            );
            tmp.rectTransform.sizeDelta = new Vector2(0, 30f);
        }

        /// <summary>分组小标题（可选本地化）</summary>
        private void AddGroupTitle(RectTransform parent, string defaultText, string locKey)
        {
            var tmp = CreateText(
                parent,
                defaultText,
                13f,
                GroupTitleColor,
                TextAlignmentOptions.Left,
                FontStyles.Bold,
                locKey
            );
            tmp.rectTransform.sizeDelta = new Vector2(0, 25f);
        }

        /// <summary>普通条目（英文正文）</summary>
        private void AddEntry(
            RectTransform parent,
            string text,
            float fontSize = 13f,
            Color? color = null,
            FontStyles style = FontStyles.Normal
        )
        {
            var tmp = CreateText(
                parent,
                text,
                fontSize,
                color ?? EntryColor,
                TextAlignmentOptions.Left,
                style,
                null
            );
        }

        /// <summary>长段落（自动换行，如许可全文）</summary>
        private void AddParagraph(RectTransform parent, string text, float fontSize)
        {
            var tmp = CreateText(
                parent,
                text,
                fontSize,
                MutedColor,
                TextAlignmentOptions.Left,
                FontStyles.Normal,
                null
            );
        }

        /// <summary>占位间距</summary>
        private static void AddSpacing(RectTransform parent, float height)
        {
            var go = new GameObject("Spacing");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = height;
            le.preferredHeight = height;
            le.flexibleHeight = 0f;
        }

        /// <summary>创建 TMP 文本（自动换行，高度由布局撑开）</summary>
        private static TextMeshProUGUI CreateText(
            Transform parent,
            string text,
            float fontSize,
            Color color,
            TextAlignmentOptions alignment,
            FontStyles style,
            string locKey
        )
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.font = MenuPageStyle.Font;
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.richText = false;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.enableAutoSizing = false;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.raycastTarget = false;

            if (!string.IsNullOrEmpty(locKey))
            {
                var lt = go.AddComponent<LocalizedText>();
                lt.localizationKey = locKey;
            }
            return tmp;
        }

        // ============================================================
        //  底部按钮（复刻 SimpleVRSettingsMenu）
        // ============================================================

        private void CreateBottomButton(
            Transform parent,
            string text,
            string locKey,
            UnityEngine.Events.UnityAction onClick
        )
        {
            var go = new GameObject(text);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();

            var img = go.AddComponent<Image>();
            img.color = new Color(0.12f, 0.15f, 0.22f, 0.6f);

            var button = go.AddComponent<Button>();
            var colors = button.colors;
            colors.normalColor = new Color(0.12f, 0.15f, 0.22f, 0.6f);
            colors.highlightedColor = new Color(0.2f, 0.3f, 0.45f, 0.8f);
            colors.pressedColor = new Color(0.08f, 0.1f, 0.15f, 0.7f);
            colors.selectedColor = new Color(0.18f, 0.25f, 0.38f, 0.75f);
            button.colors = colors;
            button.targetGraphic = img;
            button.onClick.AddListener(onClick);

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

            MenuPageStyle.ButtonStyle(button, SongSelectionStyle.Control);
            UILayoutHelper.AddHoverSoundEffect(go, audioSource, hoverSound, 0.5f);
        }

        // ============================================================
        //  音效 & RayController 通知
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
            Debug.Log("[SimpleCreditsMenu] RayController 缓存已刷新");
        }
    }
}
