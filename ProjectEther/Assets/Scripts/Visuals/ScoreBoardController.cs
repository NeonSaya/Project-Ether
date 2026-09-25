using System;
using System.Collections;
using TMPro;
using UnityEngine;

namespace OsuVR
{
    /// <summary>
    /// 计分板控制器：管理游戏中的分数、连击、准确率显示
    /// </summary>
    public class ScoreBoardController : MonoBehaviour
    {
        // =========================================================
        // UI 引用
        // =========================================================
        [Header("UI 引用")]
        public TextMeshProUGUI textScore; // 分数文本
        public TextMeshProUGUI textCombo; // 连击文本
        public TextMeshProUGUI textAcc; // 准确率文本
        public GameObject comboGroup; // 连击组 (包含数字和标签，用于整体显隐)

        // =========================================================
        // 动画配置
        // =========================================================
        [Header("动画配置")]
        [Tooltip("分数滚动速度")]
        public float scoreScrollSpeed = 5.0f;

        [Tooltip("连击弹出放大倍数")]
        public float punchScale = 1.15f;

        [Tooltip("断连颜色")]
        public Color comboBreakColor = Color.red;

        // =========================================================
        // 内部状态
        // =========================================================
        private float displayScore = 0; // 当前显示的分数 (用于动画)
        private double displayAcc = 1.0; // 当前显示的准确率 (用于动画)

        private long targetScore = 0; // 目标分数
        private double targetAcc = 1.0; // 目标准确率
        private int currentCombo = 0; // 当前连击数

        private Vector3 comboOriginalScale; // 连击文本原始缩放
        private Color comboOriginalColor; // 连击文本原始颜色

        // 零 GC 文本缓存（动画期每帧刷新，避免 ToString 分配）
        private long _lastScoreShown = -1;
        private readonly char[] _scoreBuf = new char[12];
        private int _lastAccCenti = -1;
        private readonly char[] _accBuf = new char[8];

        // =========================================================
        // 生命周期
        // =========================================================

        void Start()
        {
            if (textCombo)
            {
                comboOriginalScale = Vector3.one;
                comboOriginalColor = textCombo.color;
            }

            // 初始状态隐藏 Combo
            if (comboGroup)
                comboGroup.SetActive(false);
            else if (textCombo)
                textCombo.gameObject.SetActive(false);
        }

        void Update()
        {
            // 1. 分数滚动动画 (Lerp 插值)
            if (Mathf.Abs(displayScore - targetScore) > 1f)
            {
                displayScore = Mathf.Lerp(
                    displayScore,
                    targetScore,
                    Time.deltaTime * scoreScrollSpeed
                );
                SetScoreText((long)displayScore);
            }
            else if ((long)displayScore != targetScore)
            {
                // 差值过小时直接对齐，防止小数抖动
                displayScore = targetScore;
                SetScoreText(targetScore);
            }

            // 2. 准确率滚动动画
            if (Mathf.Abs((float)(displayAcc - targetAcc)) > 0.0001f)
            {
                displayAcc = Mathf.Lerp((float)displayAcc, (float)targetAcc, Time.deltaTime * 3f);
                SetAccText(displayAcc);
            }
            else if (Math.Abs(displayAcc - targetAcc) > 0.000001)
            {
                // 强制对齐到目标值，防止浮点精度问题
                displayAcc = targetAcc;
                SetAccText(displayAcc);
            }
        }

        // =========================================================
        // 公开接口
        // =========================================================

        /// <summary>
        /// 更新计分板数据
        /// </summary>
        /// <param name="score">当前分数</param>
        /// <param name="combo">当前连击</param>
        /// <param name="acc">当前准确率</param>
        public void UpdateDashboard(long score, int combo, double acc)
        {
            targetScore = score;
            targetAcc = acc;

            // Combo 状态处理
            if (combo > currentCombo)
            {
                // Combo 增加：播放弹出动画
                currentCombo = combo;
                UpdateComboText();
                PunchCombo();

                // 确保显示
                if (comboGroup)
                    comboGroup.SetActive(true);
                else if (textCombo)
                    textCombo.gameObject.SetActive(true);
            }
            else if (combo == 0 && currentCombo > 0)
            {
                // Combo 断掉：播放断连动画
                currentCombo = 0;
                StartCoroutine(ComboBreakEffect());
            }
            else if (combo == 0)
            {
                // 一直是 0
                currentCombo = 0;
            }
        }

        // =========================================================
        // 辅助方法
        // =========================================================

        /// <summary>
        /// 零 GC 写入分数文本（等效 "D6" 格式，SetCharArray 直写缓冲）
        /// </summary>
        private void SetScoreText(long value)
        {
            if (textScore == null || value == _lastScoreShown)
                return;
            _lastScoreShown = value;
            if (value < 0)
                value = 0;

            int i = _scoreBuf.Length;
            long v = value;
            do
            {
                _scoreBuf[--i] = (char)('0' + (int)(v % 10));
                v /= 10;
            } while (v > 0);
            // 不足 6 位补前导零（D6 语义）
            while (_scoreBuf.Length - i < 6 && i > 0)
                _scoreBuf[--i] = '0';
            textScore.SetCharArray(_scoreBuf, i, _scoreBuf.Length - i);
        }

        /// <summary>
        /// 零 GC 写入准确率文本（等效 "F2" 百分比，SetCharArray 直写缓冲）
        /// </summary>
        private void SetAccText(double acc)
        {
            if (textAcc == null)
                return;
            double pct = acc * 100.0;
            if (pct < 0.0)
                pct = 0.0;
            if (pct > 100.0)
                pct = 100.0;
            // 与 "F2" 一致的四舍五入（正数 away-from-zero）
            int centi = (int)Math.Floor(pct * 100.0 + 0.5);
            if (centi == _lastAccCenti)
                return;
            _lastAccCenti = centi;

            int whole = centi / 100;
            int frac = centi % 100;
            int i = 0;
            if (whole >= 100)
            {
                _accBuf[i++] = (char)('0' + whole / 100);
                _accBuf[i++] = (char)('0' + (whole / 10) % 10);
                _accBuf[i++] = (char)('0' + whole % 10);
            }
            else if (whole >= 10)
            {
                _accBuf[i++] = (char)('0' + whole / 10);
                _accBuf[i++] = (char)('0' + whole % 10);
            }
            else
            {
                _accBuf[i++] = (char)('0' + whole);
            }
            _accBuf[i++] = '.';
            _accBuf[i++] = (char)('0' + frac / 10);
            _accBuf[i++] = (char)('0' + frac % 10);
            _accBuf[i++] = '%';
            textAcc.SetCharArray(_accBuf, 0, i);
        }

        /// <summary>
        /// 更新连击文本
        /// </summary>
        private void UpdateComboText()
        {
            if (textCombo)
                textCombo.text = $"{currentCombo}";
        }

        /// <summary>
        /// 播放连击弹出动画
        /// </summary>
        private void PunchCombo()
        {
            if (!textCombo)
                return;
            StopCoroutine("AnimatePunch");
            StartCoroutine("AnimatePunch");
        }

        /// <summary>
        /// 连击弹出动画协程
        /// </summary>
        private IEnumerator AnimatePunch()
        {
            float t = 0;
            while (t < 0.15f)
            {
                t += Time.deltaTime;
                float scale = Mathf.Lerp(punchScale, 1.0f, t * 10f);
                textCombo.transform.localScale = comboOriginalScale * scale;
                yield return null;
            }
            textCombo.transform.localScale = comboOriginalScale;
        }

        /// <summary>
        /// 断连动画：变红 -> 下落/淡出 -> 隐藏
        /// </summary>
        private IEnumerator ComboBreakEffect()
        {
            if (!textCombo)
                yield break;

            textCombo.color = comboBreakColor;

            Vector3 originalPos = textCombo.transform.localPosition;
            float t = 0;

            while (t < 0.3f)
            {
                t += Time.deltaTime;
                // 下落动画
                textCombo.transform.localPosition = originalPos - new Vector3(0, t * 0.5f, 0);
                // 淡出动画
                textCombo.alpha = 1f - (t / 0.3f);
                yield return null;
            }

            // 动画结束：隐藏并重置状态
            if (comboGroup)
                comboGroup.SetActive(false);
            else
                textCombo.gameObject.SetActive(false);

            textCombo.color = comboOriginalColor;
            textCombo.alpha = 1f;
            textCombo.transform.localPosition = originalPos;
            textCombo.transform.localScale = comboOriginalScale;
        }
    }
}
