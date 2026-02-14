using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace OsuVR
{
    public class ScoreManager : MonoBehaviour
    {
        [Header("UI Controller")]
        public ScoreBoardController boardController;

        // --- Lazer 源码常量 ---
        private const double MAX_SCORE = 1000000;
        private const double COMBO_EXPONENT = 0.5;

        // --- 运行时数据 ---
        private double _finalScore = 0;
        private int _currentCombo = 0;
        private int _maxComboReached = 0;

        // 统计数据
        private int _totalHitsPerformed = 0; // 当前判定次数 (分子部分)
        private int _totalMapJudgements = 0; // 全图判定总数 (分母部分，含Tick/Repeat)

        // --- 分数计算核心变量 ---
        private double _currentBaseScore = 0;      // 分子：当前得分 (300+10+30...)
        private double _currentMaxBaseScore = 0;   // 分母：当前进度的理论满分

        private double _currentComboPortion = 0;   // 分子：连击权重分
        private double _maxComboPortionTotal = 0;  // 分母：全图理论连击权重分

        private double _currentBonusScore = 0;     // 仅限 Spinner Bonus (不影响 Acc)

        void Start()
        {
            ResetData();
        }

        private void ResetData()
        {
            _finalScore = 0;
            _currentCombo = 0;
            _maxComboReached = 0;
            _totalHitsPerformed = 0;
            _totalMapJudgements = 0;

            _currentBaseScore = 0;
            _currentMaxBaseScore = 0;
            _currentComboPortion = 0;
            _currentBonusScore = 0;

            if (boardController) boardController.UpdateDashboard(0, 0, 1.0);
        }

        // ================================================================
        // 步骤 1: 预计算 (Pre-calculation)
        // 对应 Lazer 源码中 Reset(true) 里的 MaximumResultCounts 计算逻辑
        // ================================================================
        public void Initialize(List<HitObject> allHitObjects)
        {
            ResetData();

            // 模拟一次完美的 Full Combo (AutoPlay)
            // 以此计算出准确的分母：_maxComboPortionTotal 和 _totalMapJudgements

            int simCombo = 0;
            _maxComboPortionTotal = 0;
            _totalMapJudgements = 0; // 这个将作为 Accuracy Progress 的分母

            foreach (var obj in allHitObjects)
            {
                if (obj is HitCircle)
                {
                    // Circle: 1个判定, 300分
                    SimulateHit(ref simCombo, 300);
                }
                else if (obj is SpinnerObject)
                {
                    // Spinner: 结束时1个判定, 300分 (Bonus不计入分母)
                    SimulateHit(ref simCombo, 300);
                }
                else if (obj is SliderObject slider)
                {
                    // Slider: 包含 Head, Ticks, Repeats, Tail

                    // 1. Head (300分)
                    SimulateHit(ref simCombo, 300);

                    // 2. Nested Objects (Tick & Repeat)
                    if (slider.NestedHitObjects != null)
                    {
                        foreach (var nested in slider.NestedHitObjects)
                        {
                            if (nested.Type == SliderEventType.Tick)
                            {
                                SimulateHit(ref simCombo, 10); // Tick 10分
                            }
                            else if (nested.Type == SliderEventType.Repeat)
                            {
                                SimulateHit(ref simCombo, 30); // Repeat 30分
                            }
                        }
                    }

                    // 3. Tail (300分)
                    // 注意：你的 SliderController 逻辑里，Tail 是通过 OnNoteHit 触发的
                    // 所以它算作一次标准判定
                    SimulateHit(ref simCombo, 300);
                }
            }

            Debug.Log($"[Score] 初始化完成. 总判定数: {_totalMapJudgements}, 理论Combo权重: {_maxComboPortionTotal:F2}");
        }


        // 模拟击打辅助函数
        private void SimulateHit(ref int combo, int score)
        {
            combo++;
            _totalMapJudgements++;
            // Lazer 公式：分数 * Sqrt(Combo)
            _maxComboPortionTotal += score * Math.Pow(combo, COMBO_EXPONENT);
        }

        // ================================================================
        // 步骤 2: 命中处理 (Runtime Processing)
        // 对应 Lazer 源码 ApplyResultInternal
        // ================================================================

        // 专门处理 Miss 的方法，支持传入它本该有的满分
        public void RegisterMiss(int maxScoreValue)
        {
            _totalHitsPerformed++;
            _currentCombo = 0; // Miss 断连

            // 基础分+0，但分母精确加上它本该拿到的满分 (Tick是10，大圈是300)
            _currentBaseScore += 0;
            _currentMaxBaseScore += maxScoreValue;

            ComputeScore();
        }

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
        }

        /// <summary>
        /// 专门处理滑条的 Head, Tick, Repeat
        /// </summary>
        public void RegisterComboHit(int scoreValue)
        {
            _totalHitsPerformed++;

            // 1. 增加 Combo
            _currentCombo++;
            if (_currentCombo > _maxComboReached) _maxComboReached = _currentCombo;

            // 2. ✅ [关键修改] 计入 Acc 分子和分母
            // 以前是加到 Bonus，现在加到 BaseScore
            _currentBaseScore += scoreValue;

            // 分母加多少？对于 Tick 来说，满分就是 10 分；Repeat 满分就是 30
            // 所以分母加 scoreValue 即可 (假设玩家打中了就是满分)
            _currentMaxBaseScore += scoreValue;

            // 3. 增加 Combo 权重
            _currentComboPortion += scoreValue * Math.Pow(_currentCombo, COMBO_EXPONENT);

            ComputeScore();
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

        /// <summary>
        /// 计算公式
        /// </summary>
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
            if (_totalMapJudgements > 0)
                accuracyProgress = (double)_totalHitsPerformed / _totalMapJudgements;

            // D. 套用 Lazer 终极公式 (第 225 行)
            // Total = (50万 * Acc * ComboProg) + (50万 * Acc^5 * AccProg) + Bonus
            double part1 = 500000 * accuracy * comboProgress;
            double part2 = 500000 * Math.Pow(accuracy, 5) * accuracyProgress;

            _finalScore = part1 + part2 + _currentBonusScore;

            // 更新 UI
            if (boardController != null)
            {
                boardController.UpdateDashboard((long)_finalScore, _currentCombo, accuracy);
            }
        }
    }
}