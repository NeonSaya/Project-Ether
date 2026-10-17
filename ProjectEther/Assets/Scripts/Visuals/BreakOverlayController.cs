using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OsuVR
{
    /// <summary>
    /// 独立于计分板的休息提示；所有动画均由谱面时钟求值，暂停、回跳和重试不累积旧动画。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas), typeof(HUDFollower))]
    public sealed class BreakOverlayController : MonoBehaviour
    {
        // 与 lazer 的 BreakOverlay 一样，门槛、淡入淡出均使用谱面毫秒。
        // 主时钟已经包含播放倍率：真实淡入时长为 325 / rate ms，不能在 UI 再次缩放时间。
        private const double MinimumBreakDurationMs = 650;
        private const double FadeDurationMs = 325;
        private const float ProgressWidth = 640f;
        private const float ProgressHeight = 6f;

        [SerializeField]
        private RhythmGameManager gameManager;

        [SerializeField]
        private TMP_FontAsset font;

        private Canvas overlayCanvas;
        private CanvasGroup contentGroup;
        private RectTransform content;
        private RectTransform progressBar;
        private TextMeshProUGUI remainingText;
        private TextMeshProUGUI accuracyText;
        private TextMeshProUGUI leftArrow;
        private TextMeshProUGUI rightArrow;
        private HUDFollower follower;
        private bool hasAlignedHeight;
        private int lastRemainingSeconds = -1;
        private double lastAccuracy = double.NaN;
        private string lastRank;

        private void Awake()
        {
            overlayCanvas = GetComponent<Canvas>();
            overlayCanvas.worldCamera = Camera.main;
            follower = GetComponent<HUDFollower>();
            CreateContent();
            Hide();
        }

        private void OnDisable()
        {
            Hide();
        }

        private void LateUpdate()
        {
            // 等 XR 初始眼高就绪后对齐游玩区中心；随后的位置和朝向仍完全交给 HUDFollower。
            if (
                !hasAlignedHeight
                && gameManager != null
                && (gameManager.isPlaying || gameManager.isBufferPhase)
                && overlayCanvas.worldCamera != null
            )
            {
                follower.heightOffset =
                    CoordinateMapper.MapToWorld(new Vector2(256f, 192f)).y
                    - overlayCanvas.worldCamera.transform.position.y;
                hasAlignedHeight = true;
            }

            // 暂停保留画面并冻结动画；开局缓冲、停止与结算不是谱面休息。
            if (
                gameManager == null
                || gameManager.isGameEnded
                || (!gameManager.isPlaying && !gameManager.IsPaused)
            )
            {
                Hide();
                return;
            }

            double timeMs = gameManager.currentMusicTimeMs;
            var period = FindBreak(gameManager.GetCurrentBeatmap()?.Events?.Breaks, timeMs);
            if (period == null)
            {
                Hide();
                return;
            }

            UpdateDisplay(period, timeMs);
        }

        private static BreakPeriod FindBreak(IReadOnlyList<BreakPeriod> periods, double timeMs)
        {
            if (periods == null || double.IsNaN(timeMs) || double.IsInfinity(timeMs))
                return null;

            // 不依赖事件顺序或单向游标，回跳/重试时也能直接找到正确区间。
            for (int i = 0; i < periods.Count; i++)
            {
                var period = periods[i];
                if (
                    period == null
                    || double.IsInfinity(period.StartTime)
                    || double.IsInfinity(period.EndTime)
                    || !(period.EndTime - period.StartTime >= MinimumBreakDurationMs)
                )
                    continue;

                if (timeMs >= period.StartTime && timeMs < period.EndTime)
                    return period;
            }
            return null;
        }

        private void UpdateDisplay(BreakPeriod period, double timeMs)
        {
            double elapsedMs = timeMs - period.StartTime;
            double remainingMs = period.EndTime - timeMs;
            float enter = Mathf.Clamp01((float)(elapsedMs / FadeDurationMs));
            float exit = Mathf.Clamp01((float)(remainingMs / FadeDurationMs));
            float easedEnter = 1f - Mathf.Pow(1f - enter, 5f);
            float easedExit = Mathf.Pow(1f - exit, 3f);
            float slide = (1f - easedEnter) + easedExit;

            contentGroup.alpha = Mathf.SmoothStep(0f, 1f, Mathf.Min(enter, exit));
            content.anchoredPosition = new Vector2(0f, -10f * (1f - easedEnter) + 8f * easedExit);
            content.localScale = Vector3.one * (0.97f + 0.03f * easedEnter - 0.03f * easedExit);
            overlayCanvas.enabled = contentGroup.alpha > 0f;

            // 中心锚点与中心轴心保持不变，只改宽度，使两端向中间收拢。
            float remaining = Mathf.Clamp01(
                (float)(remainingMs / (period.EndTime - period.StartTime))
            );
            // 参考 lazer 的条容器展开，入场展开后开始倒退，退场时额外收拢。
            float width = ProgressWidth * remaining * easedEnter * (1f - easedExit);
            progressBar.sizeDelta = new Vector2(width, ProgressHeight);

            // 与 lazer 一样显示谱面秒并向上取整，DT/HT 不再重复除播放倍率。
            double seconds = remainingMs / 1000.0;
            int remainingSeconds = (int)Math.Min(int.MaxValue, Math.Ceiling(seconds));
            if (remainingSeconds != lastRemainingSeconds)
            {
                lastRemainingSeconds = remainingSeconds;
                remainingText.SetText("{0}", remainingSeconds);
            }

            float pulse = 1f - Mathf.Clamp01((float)(remainingSeconds - seconds) / 0.18f);
            remainingText.rectTransform.localScale = Vector3.one * (1f + 0.035f * pulse * pulse);
            remainingText.rectTransform.anchoredPosition = new Vector2(-40f * slide, 44f);
            accuracyText.rectTransform.anchoredPosition = new Vector2(40f * slide, -64f);

            // 两侧箭头低幅内收呼吸，不闪烁；同一谱面时间得到相同姿态，暂停时自然冻结。
            float attention =
                0.5f - 0.5f * Mathf.Cos((float)(elapsedMs % 1200.0) * (2f * Mathf.PI / 1200f));
            float arrowX = 360f + 12f * (1f - attention) + 24f * slide;
            leftArrow.rectTransform.anchoredPosition = new Vector2(-arrowX, -18f);
            rightArrow.rectTransform.anchoredPosition = new Vector2(arrowX, -18f);
            leftArrow.alpha = rightArrow.alpha = 0.45f + 0.3f * attention;

            UpdateAccuracy();
        }

        private void UpdateAccuracy()
        {
            var score = gameManager.scoreManager;
            if (score == null)
            {
                accuracyText.text = string.Empty;
                lastAccuracy = double.NaN;
                lastRank = null;
                return;
            }

            double accuracy = score.GetAccuracy();
            string rank = score.GetCurrentRank();
            if (accuracy == lastAccuracy && rank == lastRank)
                return;

            lastAccuracy = accuracy;
            lastRank = rank;
            string rankColor = ColorUtility.ToHtmlStringRGB(ResultData.GetRankColor(rank));
            // 仅成绩变化时格式化；沿用计分板的两位百分比与结算评级，不逐帧分配字符串。
            accuracyText.text = string.Format(
                CultureInfo.InvariantCulture,
                "{0:F2}%  ·  <color=#{1}>{2}</color>",
                accuracy * 100.0,
                rankColor,
                rank
            );
        }

        private void Hide()
        {
            if (overlayCanvas != null)
                overlayCanvas.enabled = false;
            if (contentGroup != null)
                contentGroup.alpha = 0f;
            lastRemainingSeconds = -1;
            lastAccuracy = double.NaN;
            lastRank = null;
        }

        private void CreateContent()
        {
            var contentObject = new GameObject(
                "BreakContent",
                typeof(RectTransform),
                typeof(CanvasGroup)
            );
            contentObject.layer = gameObject.layer;
            content = contentObject.GetComponent<RectTransform>();
            content.SetParent(transform, false);
            content.anchorMin = content.anchorMax = content.pivot = Vector2.one * 0.5f;
            content.sizeDelta = new Vector2(840f, 260f);
            contentGroup = contentObject.GetComponent<CanvasGroup>();
            contentGroup.interactable = false;
            contentGroup.blocksRaycasts = false;

            var shadowObject = new GameObject(
                "FeatheredShadow",
                typeof(RectTransform),
                typeof(FeatheredShadowGraphic)
            );
            shadowObject.layer = gameObject.layer;
            var shadowRect = shadowObject.GetComponent<RectTransform>();
            shadowRect.SetParent(content, false);
            shadowRect.sizeDelta = new Vector2(960f, 320f);
            var shadow = shadowObject.GetComponent<FeatheredShadowGraphic>();
            shadow.color = new Color(0.02f, 0.025f, 0.04f, 0.55f);
            shadow.raycastTarget = false;

            remainingText = CreateText(60f, 104f);
            remainingText.name = "RemainingTime";
            remainingText.fontStyle = FontStyles.Bold;
            remainingText.rectTransform.anchoredPosition = new Vector2(0f, 44f);

            leftArrow = CreateText(46f, 80f);
            leftArrow.name = "LeftAttentionArrow";
            leftArrow.fontStyle = FontStyles.Bold;
            leftArrow.text = ">";
            leftArrow.richText = false;
            leftArrow.rectTransform.sizeDelta = new Vector2(70f, 80f);
            rightArrow = CreateText(46f, 80f);
            rightArrow.name = "RightAttentionArrow";
            rightArrow.fontStyle = FontStyles.Bold;
            rightArrow.text = "<";
            rightArrow.richText = false;
            rightArrow.rectTransform.sizeDelta = new Vector2(70f, 80f);

            progressBar = UILayoutHelper.CreatePanel(
                content,
                "RemainingProgress",
                new Color(0.65f, 0.87f, 1f, 0.95f),
                ProgressWidth,
                ProgressHeight
            );
            progressBar.gameObject.layer = gameObject.layer;
            progressBar.anchorMin = progressBar.anchorMax = progressBar.pivot = Vector2.one * 0.5f;
            progressBar.anchoredPosition = new Vector2(0f, -18f);

            accuracyText = CreateText(26f, 50f);
            accuracyText.name = "AccuracyAndRank";
            accuracyText.color = new Color(0.85f, 0.89f, 0.95f, 0.9f);
            accuracyText.rectTransform.anchoredPosition = new Vector2(0f, -64f);
        }

        private TextMeshProUGUI CreateText(float size, float height)
        {
            // 显式行高容纳思源黑体，避免省略号模式把整行文字裁空。
            var text = UILayoutHelper.CreateText(
                content,
                string.Empty,
                size,
                Color.white,
                width: 800f,
                height: height
            );
            text.gameObject.layer = gameObject.layer;
            text.font = font != null ? font : TMP_Settings.defaultFontAsset;
            text.enableWordWrapping = false;
            text.richText = true;
            return text;
        }
    }
}
