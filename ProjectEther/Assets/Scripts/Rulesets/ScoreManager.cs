using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace OsuVR
{
    /// <summary>
    /// 分数管理器：基于 osu! Lazer 源码实现的分数计算系统
    /// 
    /// 分数公式：
    /// Total = (50万 * Acc * ComboProg) + (50万 * Acc^5 * AccProg) + Bonus
    /// 
    /// 其中：
    /// - Acc = currentBaseScore / currentMaxBaseScore (准确率)
    /// - ComboProg = currentComboPortion / maxComboPortion (连击进度)
    /// - AccProg = totalHitsPerformed / totalMapJudgements (判定进度)
    /// - Bonus = Spinner 奖励分 (不影响 Acc)
    /// </summary>
    public class ScoreManager : MonoBehaviour
    {
        [Header("UI Controller")]
        public ScoreBoardController boardController;

        // ================================================================
        // Lazer 源码常量
        // ================================================================
        private const double MAX_SCORE = 1000000;       // 理论满分
        private const double COMBO_EXPONENT = 0.5;      // 连击指数 (Sqrt)

        // ================================================================
        // 运行时数据
        // ================================================================
        private double _finalScore = 0;                 // 最终分数
        private int _currentCombo = 0;                  // 当前连击
        private int _maxComboReached = 0;               // 最大连击

        // 判定统计
        private int _totalHitsPerformed = 0;            // 当前判定次数 (分子部分)
        private int _totalMapJudgements = 0;            // 全图判定总数 (分母部分，含Tick/Repeat)

        // ================================================================
        // 分数计算核心变量
        // ================================================================
        private double _currentBaseScore = 0;           // 分子：当前得分 (300+10+30...)
        private double _currentMaxBaseScore = 0;        // 分母：当前进度的理论满分

        private double _currentComboPortion = 0;        // 分子：连击权重分
        private double _maxComboPortionTotal = 0;       // 分母：全图理论连击权重分

        private double _currentBonusScore = 0;          // 仅限 Spinner Bonus (不影响 Acc)

        // ================================================================
        // 判定统计 (用于结算界面)
        // ================================================================
        private int _hit300 = 0;                        // 300 判定数 (完美)
        private int _hit100 = 0;                        // 100 判定数 (良好)
        private int _hit50 = 0;                         // 50 判定数 (一般)
        private int _hitMiss = 0;                       // Miss 判定数

        // ================================================================
        // 滑条统计
        // ================================================================
        private int _totalSliders = 0;                  // 总滑条数
        private int _slidersPerfect = 0;                // 完美滑条数
        private int _slidersOk = 0;                     // 良好滑条数
        private int _slidersMiss = 0;                   // 失败滑条数

        // ================================================================
        // Tick 统计
        // ================================================================
        private int _totalTicks = 0;                    // 总 Tick 数
        private int _ticksHit = 0;                      // 命中的 Tick 数

        // ================================================================
        // Spinner 统计
        // ================================================================
        private int _spinnerBonus = 0;                  // 转盘奖励分

        // ================================================================
        // 谱面信息 (用于结算)
        // ================================================================
        private string _songTitle = "";
        private string _songArtist = "";
        private string _difficultyName = "";
        private string _mapperName = "";
        private int _totalNoteCount = 0;

        // ================================================================
        // Mod 信息
        // ================================================================
        private bool _isAutoPlay = false;
        private bool _isRelax = false;
        private bool _isHardRock = false;
        private bool _isDoubleTime = false;
        private bool _isHalfTime = false;
        private bool _isHidden = false;
        private bool _isFlashlight = false;
        private bool _isEasy = false;

        private ModEffectsApplier _modEffects;
        private float _scoreMultiplier = 1f;
        private float _scoreMultiplierForCalculation = 1f;

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

            _hit300 = 0;
            _hit100 = 0;
            _hit50 = 0;
            _hitMiss = 0;

            _totalSliders = 0;
            _slidersPerfect = 0;
            _slidersOk = 0;
            _slidersMiss = 0;

            _totalTicks = 0;
            _ticksHit = 0;

            _spinnerBonus = 0;
            _totalNoteCount = 0;

            if (boardController) boardController.UpdateDashboard(0, 0, 1.0);
        }

        // ================================================================
        // 步骤 1: 预计算 (Pre-calculation)
        // 对应 Lazer 源码中 Reset(true) 里的 MaximumResultCounts 计算逻辑
        // ================================================================
        public void Initialize(List<HitObject> allHitObjects)
        {
            float savedMultiplier = _scoreMultiplier;
            float savedMultiplierForCalculation = _scoreMultiplierForCalculation;
            var savedModEffects = _modEffects;
            bool savedAutoPlay = _isAutoPlay;
            bool savedHardRock = _isHardRock;
            bool savedDoubleTime = _isDoubleTime;
            bool savedHalfTime = _isHalfTime;
            bool savedHidden = _isHidden;
            bool savedFlashlight = _isFlashlight;
            bool savedEasy = _isEasy;

            ResetData();

            _scoreMultiplier = savedMultiplier;
            _scoreMultiplierForCalculation = savedMultiplierForCalculation;
            _modEffects = savedModEffects;
            _isAutoPlay = savedAutoPlay;
            _isHardRock = savedHardRock;
            _isDoubleTime = savedDoubleTime;
            _isHalfTime = savedHalfTime;
            _isHidden = savedHidden;
            _isFlashlight = savedFlashlight;
            _isEasy = savedEasy;

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
                                _totalTicks++;
                                SimulateHit(ref simCombo, 10);
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

        /// <summary>
        /// 设置谱面信息 (用于结算界面显示)
        /// </summary>
        public void SetBeatmapInfo(string title, string artist, string difficulty, string mapper)
        {
            _songTitle = title ?? "";
            _songArtist = artist ?? "";
            _difficultyName = difficulty ?? "";
            _mapperName = mapper ?? "";
        }

        /// <summary>
        /// 设置当前使用的 Mod (旧版兼容接口)
        /// </summary>
        public void SetMods(bool autoPlay, bool relax, bool hardRock, bool doubleTime, bool halfTime, bool hidden)
        {
            _isAutoPlay = autoPlay;
            _isRelax = relax;
            _isHardRock = hardRock;
            _isDoubleTime = doubleTime;
            _isHalfTime = halfTime;
            _isHidden = hidden;
        }

        public void SetModsFromSelection(ModSelection selection)
        {
            if (selection == null)
            {
                _isAutoPlay = false;
                _isHardRock = false;
                _isDoubleTime = false;
                _isHalfTime = false;
                _isHidden = false;
                _isFlashlight = false;
                _isEasy = false;
                _scoreMultiplier = 1f;
                _scoreMultiplierForCalculation = 1f;
                _modEffects = null;
                return;
            }

            _isAutoPlay = selection.HasMod(ModType.Auto);
            _isHardRock = selection.HasMod(ModType.HardRock);
            _isEasy = selection.HasMod(ModType.Easy);
            _isDoubleTime = selection.HasMod(ModType.DoubleTime);
            _isHalfTime = selection.HasMod(ModType.HalfTime);
            _isHidden = selection.HasMod(ModType.Hidden);
            _isFlashlight = selection.HasMod(ModType.Flashlight);

            _modEffects = new ModEffectsApplier(selection);
            _scoreMultiplier = _modEffects.ScoreMultiplier;
            _scoreMultiplierForCalculation = _modEffects.ScoreMultiplierForCalculation;
        }

        public ModEffectsApplier GetModEffects()
        {
            return _modEffects;
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
            _currentCombo = 0;
            _hitMiss++;

            _currentBaseScore += 0;
            _currentMaxBaseScore += maxScoreValue;

            ComputeScore();
        }

        public void RegisterHit(int scoreValue)
        {
            _totalHitsPerformed++;

            if (scoreValue >= 300)
            {
                _hit300++;
            }
            else if (scoreValue >= 100)
            {
                _hit100++;
            }
            else if (scoreValue >= 50)
            {
                _hit50++;
            }
            else
            {
                _hitMiss++;
            }

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

            if (scoreValue == 10)
            {
                _ticksHit++;
            }

            _currentCombo++;
            if (_currentCombo > _maxComboReached) _maxComboReached = _currentCombo;

            // 2. ✅ [关键修改] 计入 Acc 分子和分母
            // 以前是加到 Bonus，现在加到 BaseScore
            _currentBaseScore += scoreValue;
            _currentMaxBaseScore += scoreValue;

            _currentComboPortion += scoreValue * Math.Pow(_currentCombo, COMBO_EXPONENT);

            ComputeScore();
        }

        public void RegisterSliderResult(bool isPerfect, bool isOk)
        {
            if (isPerfect) _slidersPerfect++;
            else if (isOk) _slidersOk++;
            else _slidersMiss++;
        }

        public void RegisterBonus(int bonusValue)
        {
            _currentBonusScore += bonusValue;
            _spinnerBonus += bonusValue;
            ComputeScore();
        }

        /// <summary>
        /// 计算公式
        /// </summary>
        private void ComputeScore()
        {
            double accuracy = 1.0;
            if (_currentMaxBaseScore > 0)
                accuracy = _currentBaseScore / _currentMaxBaseScore;

            double comboProgress = 0;
            if (_maxComboPortionTotal > 0)
                comboProgress = _currentComboPortion / _maxComboPortionTotal;

            double accuracyProgress = 0;
            if (_totalMapJudgements > 0)
                accuracyProgress = (double)_totalHitsPerformed / _totalMapJudgements;

            double part1 = 500000 * accuracy * comboProgress;
            double part2 = 500000 * Math.Pow(accuracy, 5) * accuracyProgress;

            _finalScore = part1 + part2 + _currentBonusScore;

            if (_totalHitsPerformed >= _totalMapJudgements && _totalMapJudgements > 0)
            {
                _finalScore = Math.Round(_finalScore);
            }

            if (boardController != null)
            {
                long displayScore = (long)Math.Round(_finalScore * _scoreMultiplierForCalculation);
                boardController.UpdateDashboard(displayScore, _currentCombo, accuracy);
            }
        }

        /// <summary>
        /// 获取应用 Mod 倍率后的最终分数
        /// 使用 ScoreMultiplierForCalculation（排除 AT Mod）
        /// </summary>
        public long GetFinalScoreWithMultiplier()
        {
            return (long)Math.Round(_finalScore * _scoreMultiplierForCalculation);
        }

        /// <summary>
        /// 获取结算数据：生成完整的成绩信息用于结算界面显示
        /// </summary>
        public ResultData GetResultData()
        {
            double accuracy = 1.0;
            if (_currentMaxBaseScore > 0)
                accuracy = _currentBaseScore / _currentMaxBaseScore;

            bool isFullCombo = _hitMiss == 0;
            bool isPerfectPlay = isFullCombo && _hit300 == _totalNoteCount && _hit100 == 0 && _hit50 == 0;

            string rank = ResultData.CalculateRank(accuracy, isPerfectPlay, isFullCombo);

            string modString = BuildModString();

            long finalScore = GetFinalScoreWithMultiplier();

            return new ResultData
            {
                songTitle = _songTitle,
                songArtist = _songArtist,
                difficultyName = _difficultyName,
                mapperName = _mapperName,
                finalScore = finalScore,
                accuracy = accuracy,
                maxCombo = _maxComboReached,
                isFullCombo = isFullCombo,
                isPerfectPlay = isPerfectPlay,
                totalNotes = _totalNoteCount,
                hit300 = _hit300,
                hit100 = _hit100,
                hit50 = _hit50,
                hitMiss = _hitMiss,
                totalTicks = _totalTicks,
                ticksHit = _ticksHit,
                totalSliders = _totalSliders,
                slidersPerfect = _slidersPerfect,
                slidersOk = _slidersOk,
                slidersMiss = _slidersMiss,
                spinnerBonus = _spinnerBonus,
                totalJudgements = _totalMapJudgements,
                perfectJudgements = _hit300,
                rank = rank,
                playDate = DateTime.Now,
                modString = modString
            };
        }

        private string BuildModString()
        {
            var mods = new System.Text.StringBuilder();

            if (_isAutoPlay) mods.Append("AT ");
            if (_isEasy) mods.Append("EZ ");
            if (_isHardRock) mods.Append("HR ");
            if (_isDoubleTime) mods.Append("DT ");
            if (_isHalfTime) mods.Append("HT ");
            if (_isHidden) mods.Append("HD ");

            return mods.ToString().Trim();
        }

        /// <summary>
        /// 检查游戏是否完成 (所有判定已执行)
        /// </summary>
        public bool IsGameComplete()
        {
            return _totalHitsPerformed >= _totalMapJudgements && _totalMapJudgements > 0;
        }

        /// <summary>
        /// 获取当前准确率
        /// </summary>
        public double GetAccuracy()
        {
            if (_currentMaxBaseScore > 0)
                return _currentBaseScore / _currentMaxBaseScore;
            return 1.0;
        }

        /// <summary>
        /// 获取当前分数
        /// </summary>
        public long GetCurrentScore()
        {
            return (long)_finalScore;
        }

        /// <summary>
        /// 获取当前连击数
        /// </summary>
        public int GetCurrentCombo()
        {
            return _currentCombo;
        }

        /// <summary>
        /// 获取最大连击数
        /// </summary>
        public int GetMaxCombo()
        {
            return _maxComboReached;
        }
    }
}