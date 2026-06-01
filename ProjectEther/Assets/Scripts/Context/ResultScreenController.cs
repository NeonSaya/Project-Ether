using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;

namespace OsuVR
{
    /// <summary>
    /// 结算界面控制器：管理游戏结束后的成绩显示
    /// 支持动画效果、按钮交互、场景跳转
    /// </summary>
    public class ResultScreenController : MonoBehaviour
    {
        public static ResultScreenController Instance { get; private set; }

        // =========================================================
        // UI 引用
        // =========================================================
        [Header("UI 引用")]
        public GameObject resultPanel;          // 结算面板根对象
        public CanvasGroup canvasGroup;         // 用于淡入淡出

        [Header("歌曲信息")]
        public TextMeshProUGUI textTitle;       // 曲名
        public TextMeshProUGUI textArtist;      // 艺术家
        public TextMeshProUGUI textDifficulty;  // 难度名称
        public TextMeshProUGUI textMapper;      // 谱师

        [Header("分数显示")]
        public TextMeshProUGUI textScore;       // 最终分数
        public TextMeshProUGUI textAccuracy;    // 准确率
        public TextMeshProUGUI textMaxCombo;    // 最大连击
        public TextMeshProUGUI textRank;        // 评级 (SS/S/A/B/C/D/F)

        [Header("判定统计")]
        public TextMeshProUGUI textHit300;      // 300 判定数
        public TextMeshProUGUI textHit100;      // 100 判定数
        public TextMeshProUGUI textHit50;       // 50 判定数
        public TextMeshProUGUI textMiss;        // Miss 判定数

        [Header("滑条统计")]
        public TextMeshProUGUI textSliderInfo;  // 滑条完成情况

        [Header("奖励")]
        public TextMeshProUGUI textSpinnerBonus; // 转盘奖励分

        [Header("Mod 显示")]
        public TextMeshProUGUI textMods;        // 使用的 Mod

        [Header("按钮")]
        public Button buttonRetry;              // 重试按钮
        public Button buttonBackToMenu;         // 返回菜单按钮
        public Button buttonWatchReplay;        // 观看回放按钮 (未实现)

        // =========================================================
        // 动画设置
        // =========================================================
        [Header("动画设置")]
        [Tooltip("分数滚动速度")]
        public float scoreScrollSpeed = 8000f;
        [Tooltip("评级出现延迟 (秒)")]
        public float rankAppearDelay = 1.5f;
        [Tooltip("淡入淡出时长 (秒)")]
        public float fadeDuration = 0.5f;
        [Tooltip("面板缩放时长 (秒)")]
        public float panelScaleDuration = 0.3f;
        [Tooltip("缩放动画曲线")]
        public AnimationCurve scaleCurve;

        [Header("评级动画")]
        [Tooltip("评级弹出放大倍数")]
        public float rankPunchScale = 1.3f;
        [Tooltip("评级动画时长 (秒)")]
        public float rankPunchDuration = 0.3f;
        [Tooltip("评级发光颜色")]
        public Color rankGlowColor = Color.yellow;

        [Header("全连特效")]
        public GameObject fullComboEffect;      // 全连特效对象
        public ParticleSystem fullComboParticles; // 全连粒子效果

        [Header("音效")]
        public AudioClip resultAppearSound;     // 结算出现音效
        public AudioClip rankAppearSound;       // 评级出现音效
        public AudioClip fullComboSound;        // 全连音效
        public AudioSource audioSource;         // 音源

        // =========================================================
        // 内部状态
        // =========================================================
        private ResultData currentResult;       // 当前结算数据
        private long displayScore = 0;          // 当前显示的分数 (用于动画)
        private double displayAccuracy = 0;     // 当前显示的准确率 (用于动画)
        private bool isAnimating = false;       // 是否正在播放动画

        private Vector3 originalPanelScale;     // 面板原始缩放

        // =========================================================
        // 生命周期
        // =========================================================

        void Awake()
        {
            // 单例模式
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            AutoAttachLocalizedTexts();

            // 默认动画曲线
            if (scaleCurve == null || scaleCurve.length == 0)
            {
                scaleCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
            }

            originalPanelScale = resultPanel != null ? resultPanel.transform.localScale : Vector3.one;
        }

        void OnEnable()
        {
            LocalizationManager.OnLanguageChanged += OnLanguageChanged;
        }

        void OnDisable()
        {
            LocalizationManager.OnLanguageChanged -= OnLanguageChanged;
        }

        private void AutoAttachLocalizedTexts()
        {
            var allTexts = GetComponentsInChildren<TextMeshProUGUI>(true);
            var mapping = new Dictionary<string, string>
            {
                { "RESULT", "ui_result_title" },
                { "Score", "ui_score" },
                { "Accuracy", "ui_accuracy" },
                { "Max Combo", "ui_max_combo" },
                { "Rank", "ui_rank" },
                { "RETRY", "ui_retry" },
                { "Back to Menu", "ui_main_menu" },
                { "Mapper", "ui_mapper" }
            };

            foreach (var text in allTexts)
            {
                if (mapping.TryGetValue(text.text, out string key))
                {
                    if (text.GetComponent<LocalizedText>() == null)
                    {
                        var lt = text.gameObject.AddComponent<LocalizedText>();
                        lt.localizationKey = key;
                    }
                }
            }
        }

        private void OnLanguageChanged()
        {
            if (currentResult != null && !isAnimating)
            {
                SetSongInfo(currentResult);
                SetModDisplay(currentResult);
                UpdateStatisticsTexts(currentResult);
            }
        }

        private void UpdateStatisticsTexts(ResultData result)
        {
            if (textHit300 != null && textHit300.text.Contains(":"))
                textHit300.text = $"{LocalizationManager.GetText("ui_hit300")}: {result.hit300}";
            if (textHit100 != null && textHit100.text.Contains(":"))
                textHit100.text = $"{LocalizationManager.GetText("ui_hit100")}: {result.hit100}";
            if (textHit50 != null && textHit50.text.Contains(":"))
                textHit50.text = $"{LocalizationManager.GetText("ui_hit50")}: {result.hit50}";
            if (textMiss != null && textMiss.text.Contains(":"))
                textMiss.text = $"{LocalizationManager.GetText("ui_miss")}: {result.hitMiss}";

            if (textSliderInfo != null && result.totalSliders > 0)
            {
                string sliderTemplate = LocalizationManager.GetText("ui_sliders_info");
                textSliderInfo.text = string.Format(sliderTemplate, result.slidersPerfect, result.totalSliders);
            }

            if (textSpinnerBonus != null && result.spinnerBonus > 0)
            {
                string spinnerTemplate = LocalizationManager.GetText("ui_spinner_bonus_text");
                textSpinnerBonus.text = string.Format(spinnerTemplate, result.spinnerBonus);
            }
        }

        void Start()
        {
            if (buttonRetry != null)
                buttonRetry.onClick.AddListener(OnRetryClicked);
            if (buttonBackToMenu != null)
                buttonBackToMenu.onClick.AddListener(OnBackToMenuClicked);
            if (buttonWatchReplay != null)
                buttonWatchReplay.onClick.AddListener(OnWatchReplayClicked);

            if (resultPanel != null)
                resultPanel.SetActive(false);

            if (fullComboEffect != null)
                fullComboEffect.SetActive(false);

            if (GameContext.Instance != null && GameContext.Instance.LastResult != null)
            {
                ShowResult(GameContext.Instance.LastResult);
            }
            else
            {
                Debug.LogWarning("[ResultScreen] GameContext 或 LastResult 为空，无法显示结算");
            }
        }

        // =========================================================
        // 公开接口
        // =========================================================

        /// <summary>
        /// 显示结算界面 (带动画)
        /// </summary>
        /// <param name="result">结算数据</param>
        public void ShowResult(ResultData result)
        {
            currentResult = result;
            StartCoroutine(ShowResultCoroutine(result));
        }

        // =========================================================
        // 动画协程
        // =========================================================

        /// <summary>
        /// 显示结算界面的完整动画流程
        /// </summary>
        private IEnumerator ShowResultCoroutine(ResultData result)
        {
            isAnimating = true;

            // 1. 先清空占位符，再显示面板
            ResetPlaceholders();

            // 2. 初始化面板状态
            if (resultPanel != null)
            {
                resultPanel.SetActive(true);
                resultPanel.transform.localScale = Vector3.zero;
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0;
            }

            PlaySound(resultAppearSound);

            // 3. 面板淡入 + 缩放动画
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeDuration;

                if (canvasGroup != null)
                    canvasGroup.alpha = t;

                if (resultPanel != null)
                    resultPanel.transform.localScale = Vector3.Lerp(Vector3.zero, originalPanelScale, scaleCurve.Evaluate(t));

                yield return null;
            }

            if (canvasGroup != null)
                canvasGroup.alpha = 1f;
            if (resultPanel != null)
                resultPanel.transform.localScale = originalPanelScale;

            // 4. 设置歌曲信息
            SetSongInfo(result);
            SetModDisplay(result);

            // 5. 分数滚动动画
            yield return StartCoroutine(AnimateScore(result));

            // 6. 判定统计动画
            yield return StartCoroutine(AnimateStatistics(result));

            // 7. 等待后显示评级
            yield return new WaitForSeconds(rankAppearDelay);

            yield return StartCoroutine(ShowRank(result));

            // 8. 全连特效
            if (result.isFullCombo && fullComboEffect != null)
            {
                fullComboEffect.SetActive(true);
                if (fullComboParticles != null)
                    fullComboParticles.Play();
                PlaySound(fullComboSound);
            }

            isAnimating = false;
        }

        /// <summary>
        /// 清空占位符：分数、准确率、连击、评级显示为空白
        /// 动画开始前先清空，然后数据逐渐填充
        /// </summary>
        private void ResetPlaceholders()
        {
            // 清空分数相关
            if (textScore != null)
                textScore.text = "";

            if (textAccuracy != null)
                textAccuracy.text = "";

            if (textMaxCombo != null)
            {
                textMaxCombo.text = "";
                textMaxCombo.color = Color.white;
            }

            // 清空评级
            if (textRank != null)
            {
                textRank.text = "";
                textRank.color = Color.gray;
                textRank.transform.localScale = Vector3.one;
            }

            // 清空判定统计
            if (textHit300 != null) textHit300.text = "";
            if (textHit100 != null) textHit100.text = "";
            if (textHit50 != null) textHit50.text = "";
            if (textMiss != null) textMiss.text = "";

            // 清空滑条和转盘信息
            if (textSliderInfo != null) textSliderInfo.text = "";
            if (textSpinnerBonus != null) textSpinnerBonus.text = "";
        }

        /// <summary>
        /// 隐藏结算界面的动画
        /// </summary>
        private IEnumerator HideResultCoroutine()
        {
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = 1f - (elapsed / fadeDuration);

                if (canvasGroup != null)
                    canvasGroup.alpha = t;
                if (resultPanel != null)
                    resultPanel.transform.localScale = originalPanelScale * t;

                yield return null;
            }

            if (resultPanel != null)
                resultPanel.SetActive(false);
        }

        /// <summary>
        /// 分数滚动动画
        /// </summary>
        private IEnumerator AnimateScore(ResultData result)
        {
            displayScore = 0;
            displayAccuracy = 0;

            float scoreDuration = 1.5f;
            float elapsed = 0f;

            while (elapsed < scoreDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / scoreDuration);
                // 缓出动画：开始快，结束慢
                float easedT = 1f - Mathf.Pow(1f - t, 3);

                displayScore = (long)(result.finalScore * easedT);
                displayAccuracy = result.accuracy * easedT;

                if (textScore != null)
                    textScore.text = displayScore.ToString("D7");
                if (textAccuracy != null)
                    textAccuracy.text = $"{displayAccuracy * 100:F2}%";
                if (textMaxCombo != null)
                    textMaxCombo.text = $"{result.maxCombo}x";

                yield return null;
            }

            // 确保最终值精确
            displayScore = result.finalScore;
            displayAccuracy = result.accuracy;

            if (textScore != null)
                textScore.text = result.finalScore.ToString("D7");
            if (textAccuracy != null)
                textAccuracy.text = $"{result.accuracy * 100:F2}%";
            if (textMaxCombo != null)
            {
                textMaxCombo.text = $"{result.maxCombo}x";
                // 全连时高亮显示
                if (result.isFullCombo)
                {
                    textMaxCombo.color = Color.yellow;
                }
            }
        }

        /// <summary>
        /// 判定统计动画 (依次显示)
        /// </summary>
        private IEnumerator AnimateStatistics(ResultData result)
        {
            yield return StartCoroutine(AnimateNumber(textHit300, result.hit300, LocalizationManager.GetText("ui_hit300")));
            yield return new WaitForSeconds(0.1f);
            yield return StartCoroutine(AnimateNumber(textHit100, result.hit100, LocalizationManager.GetText("ui_hit100")));
            yield return new WaitForSeconds(0.1f);
            yield return StartCoroutine(AnimateNumber(textHit50, result.hit50, LocalizationManager.GetText("ui_hit50")));
            yield return new WaitForSeconds(0.1f);
            yield return StartCoroutine(AnimateNumber(textMiss, result.hitMiss, LocalizationManager.GetText("ui_miss"), Color.red));

            // 滑条信息
            if (textSliderInfo != null && result.totalSliders > 0)
            {
                string sliderTemplate = LocalizationManager.GetText("ui_sliders_info");
                textSliderInfo.text = string.Format(sliderTemplate, result.slidersPerfect, result.totalSliders);
            }

            // 转盘奖励
            if (textSpinnerBonus != null && result.spinnerBonus > 0)
            {
                string spinnerTemplate = LocalizationManager.GetText("ui_spinner_bonus_text");
                textSpinnerBonus.text = string.Format(spinnerTemplate, result.spinnerBonus);
            }
        }

        /// <summary>
        /// 单个数字滚动动画
        /// </summary>
        private IEnumerator AnimateNumber(TextMeshProUGUI text, int target, string label, Color? color = null)
        {
            if (text == null) yield break;

            int current = 0;
            float duration = 0.3f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                current = (int)(target * (elapsed / duration));
                text.text = $"{label}: {current}";
                yield return null;
            }

            text.text = $"{label}: {target}";
            if (color.HasValue)
                text.color = color.Value;
        }

        /// <summary>
        /// 评级弹出动画
        /// </summary>
        private IEnumerator ShowRank(ResultData result)
        {
            if (textRank == null) yield break;

            textRank.text = result.rank;
            textRank.color = ResultData.GetRankColor(result.rank);
            textRank.transform.localScale = Vector3.zero;

            PlaySound(rankAppearSound);

            float elapsed = 0f;
            while (elapsed < rankPunchDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / rankPunchDuration;
                float scale = Mathf.Lerp(0, rankPunchScale, t);

                // 后半段回弹
                if (t > 0.5f)
                {
                    float bounceT = (t - 0.5f) * 2f;
                    scale = Mathf.Lerp(rankPunchScale, 1f, bounceT);
                }

                textRank.transform.localScale = Vector3.one * scale;
                yield return null;
            }

            textRank.transform.localScale = Vector3.one;
        }

        // =========================================================
        // 辅助方法
        // =========================================================

        /// <summary>
        /// 设置歌曲信息
        /// </summary>
        private void SetSongInfo(ResultData result)
        {
            bool useOriginalLanguage = false;
            if (SettingsManager.Instance != null && SettingsManager.Instance.Settings != null)
            {
                useOriginalLanguage = SettingsManager.Instance.Settings.displayOriginalLanguage;
            }

            if (textTitle != null)
            {
                textTitle.text = result.GetDisplayTitle(useOriginalLanguage) ?? LocalizationManager.GetText("ui_unknown_title");
                textTitle.enableWordWrapping = true;
                textTitle.overflowMode = TextOverflowModes.Ellipsis;
                textTitle.raycastTarget = false;
                // 拉宽文本框，尽可能显示完整歌名
                var titleRt = textTitle.rectTransform;
                titleRt.sizeDelta = new Vector2(800f, titleRt.sizeDelta.y);
            }
            if (textArtist != null)
            {
                textArtist.text = result.GetDisplayArtist(useOriginalLanguage) ?? LocalizationManager.GetText("ui_unknown_artist");
                textArtist.enableWordWrapping = true;
                textArtist.overflowMode = TextOverflowModes.Ellipsis;
                textArtist.raycastTarget = false;
                var artistRt = textArtist.rectTransform;
                artistRt.sizeDelta = new Vector2(800f, artistRt.sizeDelta.y);
            }
            if (textDifficulty != null)
            {
                string diffName = string.IsNullOrEmpty(result.difficultyName) ? LocalizationManager.GetText("ui_normal") : result.difficultyName;
                textDifficulty.text = $"[{diffName}]";
            }
            if (textMapper != null)
            {
                string mapperName = string.IsNullOrEmpty(result.mapperName) ? LocalizationManager.GetText("ui_unknown_mapper") : result.mapperName;
                string mappedByTemplate = LocalizationManager.GetText("ui_mapped_by");
                textMapper.text = string.Format(mappedByTemplate, mapperName);
            }
        }

        /// <summary>
        /// 设置 Mod 显示
        /// </summary>
        private void SetModDisplay(ResultData result)
        {
            if (textMods != null)
            {
                if (string.IsNullOrEmpty(result.modString))
                {
                    textMods.gameObject.SetActive(false);
                }
                else
                {
                    textMods.gameObject.SetActive(true);
                    string modsTemplate = LocalizationManager.GetText("ui_mods_display");
                    textMods.text = string.Format(modsTemplate, result.modString);
                }
            }
        }

        /// <summary>
        /// 播放音效
        /// </summary>
        private void PlaySound(AudioClip clip)
        {
            if (clip != null && audioSource != null)
            {
                audioSource.PlayOneShot(clip);
            }
        }

        // =========================================================
        // 按钮事件
        // =========================================================

        /// <summary>
        /// 重试按钮点击：停止音乐，重新游玩同一张谱面
        /// </summary>
        private void OnRetryClicked()
        {
            if (isAnimating) return;

            // 停止跨场景音乐
            if (MusicManager.Instance != null)
            {
                MusicManager.Instance.StopAndDestroy();
            }

            if (GameContext.Instance != null)
            {
                // 恢复谱面路径，标记为重试
                GameContext.Instance.SelectedBeatmapPath = GameContext.Instance.CurrentBeatmapPath;
                GameContext.Instance.ShouldRetry = true;

                // 跳转到游戏场景
                VRSceneTransitionManager.Instance.TransitionToScene(GameContext.Instance.GameSceneName);
            }
        }

        /// <summary>
        /// 返回菜单按钮点击：停止音乐，清除数据，返回选歌界面
        /// </summary>
        private void OnBackToMenuClicked()
        {
            if (isAnimating) return;

            // 停止跨场景音乐
            if (MusicManager.Instance != null)
            {
                MusicManager.Instance.StopAndDestroy();
            }

            if (GameContext.Instance != null)
            {
                // 清除结算数据
                GameContext.Instance.ClearResult();
                GameContext.Instance.ShouldRetry = false;

                // 跳转到菜单场景
                VRSceneTransitionManager.Instance.TransitionToScene(GameContext.Instance.SongSelectSceneName);
            }
            else
            {
                VRSceneTransitionManager.Instance.TransitionToScene("SongSelectScene");
            }
        }

        /// <summary>
        /// 观看回放按钮点击 (未实现)
        /// </summary>
        private void OnWatchReplayClicked()
        {
            Debug.Log("[Result] Replay feature not implemented yet");
        }
    }
}
