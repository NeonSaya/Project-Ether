using System;
using System.Collections;
using UnityEngine;
using TMPro;

namespace OsuVR
{
    public class ScoreManager : MonoBehaviour
    {
        [Header("UI References (Lazer Style HUD)")]
        public TextMeshPro textScore; // 右边
        public TextMeshPro textCombo; // 中间
        public TextMeshPro textAcc;   // 左边

        // --- Lazer 源码常量 ---
        private const double MAX_SCORE = 1000000;
        private const double COMBO_EXPONENT = 0.5; // 源码第28行

        // --- 运行时数据 ---
        private double _finalScore = 0;
        private int _currentCombo = 0;
        private int _maxComboReached = 0;

        // 统计数据
        private int _totalHitsPerformed = 0; // 当前打了几下
        private int _totalMapObjects = 0;    // 谱面总物件数 (分母)

        // --- 分数计算核心变量 (对应源码 ScoreProcessor 字段) ---

        // 1. 准确率相关
        private double _currentBaseScore = 0;      // 玩家当前的判定总分 (300+100...)
        private double _currentMaxBaseScore = 0;   // 当前进度的理论最高分 (300+300...)

        // 2. 连击相关
        private double _currentComboPortion = 0;   // 分子：玩家当前的连击加权分
        private double _maxComboPortionTotal = 0;  // 分母：整首歌全连的理论连击加权分 (预计算)

        // 3. 奖励分 (Bonus)
        private double _currentBonusScore = 0;     // 转盘等的额外分

        // 动画缩放记录
        private Vector3 _comboOriginalScale;

        void Start()
        {
            if (textCombo) _comboOriginalScale = textCombo.transform.localScale;
            ResetData();
        }

        private void ResetData()
        {
            _finalScore = 0;
            _currentCombo = 0;
            _maxComboReached = 0;
            _totalHitsPerformed = 0;

            _currentBaseScore = 0;
            _currentMaxBaseScore = 0;
            _currentComboPortion = 0;
            _currentBonusScore = 0;

            UpdateUI(1.0f);
        }

        // ================================================================
        // 步骤 1: 预计算 (Pre-calculation)
        // 对应 Lazer 源码中 Reset(true) 里的 MaximumResultCounts 计算逻辑
        // ================================================================
        /// <summary>
        /// 【必须调用】在解析完谱面后，告诉分数管理器这首歌有多少个物件
        /// </summary>
        public void Initialize(int totalHitObjects)
        {
            ResetData();
            _totalMapObjects = totalHitObjects;
            _maxComboPortionTotal = 0;

            // 模拟一次完美的 Full Combo (全是 300)
            // Lazer 逻辑：GetComboScoreChange = 300 * Math.Pow(combo, 0.5)
            for (int i = 1; i <= totalHitObjects; i++)
            {
                // 假设每个 Note 都能拿到 300 基础分
                _maxComboPortionTotal += 300 * Math.Pow(i, COMBO_EXPONENT);
            }

            Debug.Log($"[Score] Lazer Algo Initialized. Max Combo Weight: {_maxComboPortionTotal:F2}");
        }

        // ================================================================
        // 步骤 2: 命中处理 (Runtime Processing)
        // 对应 Lazer 源码 ApplyResultInternal
        // ================================================================

        /// <summary>
        /// 处理普通物件点击 (Circle, SliderHead, SliderTail)
        /// </summary>
        /// <param name="scoreValue">300, 100, 50, 0(Miss)</param>
        public void RegisterHit(int scoreValue)
        {
            _totalHitsPerformed++;

            // 1. 更新 Combo
            if (scoreValue > 0)
            {
                _currentCombo++;
                if (_currentCombo > _maxComboReached) _maxComboReached = _currentCombo;
            }
            else
            {
                _currentCombo = 0; // Miss 断连
            }

            // 2. 更新基础分 (用于算准确率 Accuracy)
            _currentBaseScore += scoreValue;
            _currentMaxBaseScore += 300; // 无论玩家打多少，理论这一下都是300

            // 3. 更新连击部分分 (Combo Portion)
            // 源码 GetComboScoreChange: result * Pow(combo, 0.5)
            if (scoreValue > 0)
            {
                _currentComboPortion += scoreValue * Math.Pow(_currentCombo, COMBO_EXPONENT);
            }

            // 4. 计算并更新 UI
            ComputeScore();
            if (scoreValue > 0) PunchCombo();
        }

        /// <summary>
        /// 处理额外奖励分 (比如 Spinner 的旋转、Slider 的 Tick)
        /// Lazer 中 Bonus 不影响准确率分母，直接加在 BonusPortion 上
        /// </summary>
        public void RegisterBonus(int bonusValue)
        {
            _currentBonusScore += bonusValue;
            ComputeScore();
        }

        // ================================================================
        // 步骤 3: 核心公式计算
        // 对应 Lazer 源码 ComputeTotalScore
        // ================================================================
        private void ComputeScore()
        {
            // A. 计算当前准确率 (Accuracy)
            // 源码: currentBaseScore / currentMaximumBaseScore
            double accuracy = 1.0;
            if (_currentMaxBaseScore > 0)
                accuracy = _currentBaseScore / _currentMaxBaseScore;

            // B. 计算 Combo 进度 (Combo Progress)
            // 源码: currentComboPortion / maximumComboPortion
            double comboProgress = 0;
            if (_maxComboPortionTotal > 0)
                comboProgress = _currentComboPortion / _maxComboPortionTotal;

            // C. 计算歌曲进度 (Accuracy Progress)
            // 源码: currentAccuracyJudgementCount / maximumAccuracyJudgementCount
            double accuracyProgress = 0;
            if (_totalMapObjects > 0)
                accuracyProgress = (double)_totalHitsPerformed / _totalMapObjects;

            // D. 套用 Lazer 终极公式 (第 225 行)
            // Total = (50万 * Acc * ComboProg) + (50万 * Acc^5 * AccProg) + Bonus
            double part1 = 500000 * accuracy * comboProgress;
            double part2 = 500000 * Math.Pow(accuracy, 5) * accuracyProgress;

            _finalScore = part1 + part2 + _currentBonusScore;

            UpdateUI((float)accuracy);
        }

        // ================================================================
        // UI 更新与动画
        // ================================================================
        private void UpdateUI(float accuracy)
        {
            // 1. 分数：标准 Lazer 格式 (000,000)
            if (textScore)
                textScore.text = _finalScore.ToString("N0");

            // 2. 准确率：标准格式 (99.85%)
            if (textAcc)
                textAcc.text = $"{accuracy * 100:F2}%";

            // 3. 连击：标准格式 (124x)
            if (textCombo)
                textCombo.text = $"{_currentCombo}x";
        }

        private void PunchCombo()
        {
            if (textCombo)
            {
                StopAllCoroutines();
                StartCoroutine(AnimateCombo());
            }
        }

        IEnumerator AnimateCombo()
        {
            // 经典的弹性动画
            textCombo.transform.localScale = _comboOriginalScale * 1.25f; // 瞬间放大

            float t = 0;
            while (t < 0.15f)
            {
                t += Time.deltaTime;
                // 快速回弹
                textCombo.transform.localScale = Vector3.Lerp(textCombo.transform.localScale, _comboOriginalScale, t * 15);
                yield return null;
            }
            textCombo.transform.localScale = _comboOriginalScale;
        }
    }
}