using System;
using UnityEngine;

namespace OsuVR
{
    /// <summary>
    /// 结算数据结构：存储一局游戏的完整成绩信息
    /// 用于在游戏结束时传递给结算界面显示
    /// </summary>
    [Serializable]
    public class ResultData
    {
        // =========================================================
        // 谱面信息
        // =========================================================
        public string songTitle;        // 曲名
        public string songArtist;       // 艺术家
        public string difficultyName;   // 难度名称
        public string mapperName;       // 谱师

        // =========================================================
        // 核心成绩
        // =========================================================
        public long finalScore;         // 最终分数 (0-1,000,000)
        public double accuracy;         // 准确率 (0.0-1.0)
        public int maxCombo;            // 最大连击数
        public bool isFullCombo;        // 是否全连 (无 Miss)
        public bool isPerfectPlay;      // 是否完美游玩 (全 300)

        // =========================================================
        // 判定统计 (HitCircle + SliderHead + SliderTail + Spinner)
        // =========================================================
        public int totalNotes;          // 总音符数
        public int hit300;              // 300 判定数 (完美)
        public int hit100;              // 100 判定数 (良好)
        public int hit50;               // 50 判定数 (一般)
        public int hitMiss;             // Miss 判定数 (错过)

        // =========================================================
        // 滑条统计
        // =========================================================
        public int totalTicks;          // 总 Tick 数
        public int ticksHit;            // 命中的 Tick 数

        public int totalSliders;        // 总滑条数
        public int slidersPerfect;      // 完美滑条数 (所有 Tick 都拿到)
        public int slidersOk;           // 良好滑条数 (漏了部分 Tick)
        public int slidersMiss;         // 失败滑条数 (头或尾没打到)

        // =========================================================
        // 转盘统计
        // =========================================================
        public int spinnerBonus;        // 转盘奖励分

        // =========================================================
        // 其他数据
        // =========================================================
        public int totalJudgements;     // 总判定次数
        public int perfectJudgements;   // 完美判定次数

        public string rank;             // 评级 (SS/S/A/B/C/D/F)
        public float playTime;          // 游玩时长 (秒)

        public string modString;        // 使用的 Mod (如 "HD HR DT")

        public DateTime playDate;       // 游玩日期

        // =========================================================
        // 静态工具方法
        // =========================================================

        /// <summary>
        /// 根据准确率计算评级 (osu!lazer 原版逻辑)
        /// 
        /// 评级规则：
        /// - SS: 100% 准确率 (所有判定都是 300)
        /// - S:  ≥95% 准确率 + 全连
        /// - A:  ≥90% 准确率，或 ≥95% 但不是全连
        /// - B:  ≥80% 准确率
        /// - C:  ≥70% 准确率
        /// - D:  ≥60% 准确率
        /// - F:  &lt;60% 准确率
        /// </summary>
        /// <param name="accuracy">准确率 (0.0-1.0)</param>
        /// <param name="isPerfectPlay">是否完美游玩 (全 300，已废弃)</param>
        /// <param name="isFullCombo">是否全连</param>
        /// <returns>评级字符串</returns>
        public static string CalculateRank(double accuracy, bool isPerfectPlay, bool isFullCombo)
        {
            // 100% 准确率直接 SS
            if (accuracy >= 1.0) return "SS";
            
            // osu!lazer 原版评级逻辑
            if (accuracy >= 0.95) return isFullCombo ? "S" : "A";
            if (accuracy >= 0.90) return "A";
            if (accuracy >= 0.80) return "B";
            if (accuracy >= 0.70) return "C";
            if (accuracy >= 0.60) return "D";
            return "F";
        }

        /// <summary>
        /// 获取评级对应的颜色
        /// </summary>
        /// <param name="rank">评级字符串</param>
        /// <returns>UI 显示颜色</returns>
        public static Color GetRankColor(string rank)
        {
            switch (rank)
            {
                case "SS": return new Color(1f, 0.85f, 0f);     // 金色
                case "S": return new Color(1f, 0.9f, 0.3f);     // 亮金色
                case "A": return new Color(0.3f, 1f, 0.3f);     // 绿色
                case "B": return new Color(0.4f, 0.8f, 1f);     // 蓝色
                case "C": return new Color(1f, 0.6f, 0.2f);     // 橙色
                case "D": return new Color(1f, 0.3f, 0.3f);     // 红色
                default: return Color.gray;                      // 灰色
            }
        }
    }
}
