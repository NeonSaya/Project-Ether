using System;
using System.IO;
using OsuVR.Storyboard;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OsuVR
{
    /// <summary>只重排已绑定的结算控件；动画、成绩与跳转继续由原控制器驱动。</summary>
    internal sealed class ResultScreenView
    {
        internal static readonly Color MissColor = new Color32(232, 112, 135, 255);

        internal static ResultScreenView Build(ResultScreenController owner)
        {
            var panel =
                owner.resultPanel != null ? owner.resultPanel.GetComponent<RectTransform>() : null;
            return panel != null ? new ResultScreenView(owner, panel) : null;
        }

        private ResultScreenView(ResultScreenController owner, RectTransform panel)
        {
            // 围绕原中心整体缩小 15%，控制器随后缓存此比例作为入场动画终点。
            panel.localScale *= 0.85f;
            var image = panel.GetComponent<Image>();
            if (image != null)
                image.color = Color.clear;
            var backgroundRect = MenuPageStyle.Rect(panel, "ResultBackground", 0, 0, 1200, 600);
            var background = backgroundRect.gameObject.AddComponent<SongSelectionBackground>();
            background.Build();
            background.ConfigureTint(
                new Color(0.07f, 0.09f, 0.15f),
                new Color(0.30f, 0.33f, 0.40f)
            );
            background.SetPath(FindBackground(GameContext.Instance?.CurrentBeatmapPath));

            // 歌名独占顶部约 75% 宽度，其余信息止于 x=688，保留右侧背景展示区。
            Place(owner.textTitle, 56, 22, 900, 54, 30);
            Place(owner.textArtist, 56, 76, 632, 32, 17, SongSelectionStyle.Muted);
            Place(owner.textDifficulty, 56, 107, 632, 32, 17, SongSelectionStyle.Accent);
            Place(owner.textMapper, 56, 139, 632, 28, 14, SongSelectionStyle.Muted);
            Caption(panel, "RankLabel", "ui_rank", 56, 184, 200, 28, 14);
            Place(
                owner.textRank,
                56,
                205,
                200,
                170,
                142,
                alignment: TextAlignmentOptions.Center,
                numbers: true
            );
            Line(panel, "RankDivider", 278, 184, 1, 185);
            Caption(panel, "ScoreLabel", "ui_score", 286, 184, 402, 28, 14);
            Place(owner.textScore, 280, 210, 408, 94, 70, numbers: true);
            Caption(panel, "AccuracyLabel", "ui_accuracy", 286, 307, 182, 28, 14);
            Place(owner.textAccuracy, 286, 333, 182, 48, 29, numbers: true);
            Line(panel, "MetricDivider", 479, 307, 1, 65);
            Caption(panel, "ComboLabel", "ui_max_combo", 500, 307, 188, 28, 14);
            Place(owner.textMaxCombo, 500, 333, 188, 48, 29, numbers: true);

            Line(panel, "StatisticsDivider", 56, 394, 632, 1);
            var values = new[]
            {
                owner.textHit300,
                owner.textHit100,
                owner.textHit50,
                owner.textMiss,
            };
            string[] keys = { "ui_hit300", "ui_hit100", "ui_hit50", "ui_miss" };
            Color[] colors =
            {
                SongSelectionStyle.Accent,
                new Color32(121, 222, 182, 255),
                new Color32(237, 194, 102, 255),
                MissColor,
            };
            for (int i = 0; i < values.Length; i++)
            {
                var label = Caption(
                    panel,
                    $"JudgementLabel_{i}",
                    keys[i],
                    56 + i * 158,
                    404,
                    158,
                    28,
                    14
                );
                label.color = colors[i];
                label.alignment = TextAlignmentOptions.Center;
                Place(
                    values[i],
                    56 + i * 158,
                    432,
                    158,
                    48,
                    28,
                    alignment: TextAlignmentOptions.Center,
                    numbers: true
                );
                if (i > 0)
                    Line(panel, $"JudgementDivider_{i}", 56 + i * 158, 414, 1, 60);
            }
            Button(owner.buttonRetry, "ui_retry", 56, SongSelectionStyle.Pink, 300);
            Button(owner.buttonBackToMenu, "ui_song_select", 372, SongSelectionStyle.Control, 316);
        }

        private static void Place(
            TextMeshProUGUI text,
            float x,
            float y,
            float width,
            float height,
            float size,
            Color? color = null,
            TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft,
            bool numbers = false
        )
        {
            if (text == null)
                return;
            Vector2 pivot = text.rectTransform.pivot;
            SongSelectionStyle.Place(text.rectTransform, x, y, width, height);
            text.rectTransform.pivot = pivot;
            text.rectTransform.anchoredPosition = new Vector2(
                x + width * pivot.x,
                -y - height * (1 - pivot.y)
            );
            text.font = MenuPageStyle.Font;
            text.fontStyle = FontStyles.Normal;
            text.margin = Vector4.zero;
            SongSelectionStyle.ConfigureText(text, size, alignment);
            text.color = color ?? SongSelectionStyle.Text;
            if (numbers)
            {
                text.font =
                    Resources.Load<TMP_FontAsset>("Fonts & Materials/Roboto-Bold SDF")
                    ?? TMP_Settings.defaultFontAsset;
                text.fontStyle = FontStyles.Normal;
                // 数值完整优先：极大成绩在本列内缩小，不能侵入右侧背景。
                text.enableAutoSizing = true;
                text.fontSizeMin = Mathf.Min(size, 20);
                text.fontSizeMax = size;
                text.overflowMode = TextOverflowModes.Ellipsis;
            }
        }

        private static TextMeshProUGUI Caption(
            Transform parent,
            string name,
            string key,
            float x,
            float y,
            float width,
            float height,
            float size
        )
        {
            return MenuPageStyle.Label(
                parent,
                name,
                LocalizationManager.GetText(key),
                key,
                x,
                y,
                width,
                height,
                size,
                SongSelectionStyle.Muted
            );
        }

        private static void Line(
            Transform parent,
            string name,
            float x,
            float y,
            float width,
            float height
        )
        {
            SongSelectionStyle.Fill(
                MenuPageStyle.Rect(parent, name, x, y, width, height),
                SongSelectionStyle.Line
            );
        }

        private static void Button(Button button, string key, float x, Color color, float width)
        {
            if (button == null)
                return;
            SongSelectionStyle.Place((RectTransform)button.transform, x, 509, width, 58);
            MenuPageStyle.ButtonStyle(button, color, 22);
            var label = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label == null)
                return;
            var localized =
                label.GetComponent<LocalizedText>()
                ?? label.gameObject.AddComponent<LocalizedText>();
            localized.SetKey(key);
        }

        private static string FindBackground(string beatmapPath)
        {
            if (string.IsNullOrEmpty(beatmapPath) || !File.Exists(beatmapPath))
                return null;
            try
            {
                bool events = false;
                foreach (string source in File.ReadLines(beatmapPath))
                {
                    string line = source.Trim();
                    if (line.StartsWith("["))
                    {
                        if (events)
                            break;
                        events = line.Equals("[Events]", StringComparison.OrdinalIgnoreCase);
                        continue;
                    }
                    if (!events || line.Length == 0 || line.StartsWith("//"))
                        continue;
                    // 与正式解析共用 CSV 和路径回退，兼容带逗号的文件名与 Android 大小写。
                    var parts = StoryboardParser.SplitCsv(line);
                    if (parts.Count >= 3 && (parts[0] == "0" || parts[0] == "Background"))
                        return StoryboardRenderer.ResolveStoryboardAssetPath(
                            Path.GetDirectoryName(Path.GetFullPath(beatmapPath)),
                            parts[2]
                        );
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            catch (ArgumentException) { }
            return null;
        }
    }
}
