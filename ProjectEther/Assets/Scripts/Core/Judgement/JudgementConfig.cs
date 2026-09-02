using UnityEngine;

namespace OsuVR
{
    /// <summary>
    /// 判定配置（纯静态、无场景对象依赖，独立程序集以便单元测试）
    ///
    /// 判定窗口 OD 绑定公式：W(OD) = 350 - 12.5 × OD（OD Clamp 0~10）
    /// 基准 OD8 = 250ms（与历史固定窗口一致）
    /// 护栏：OD10 = 225ms（收紧但不过难）、OD0 = 350ms（放宽但不过易）
    /// 同时用作「悬停命中注册窗口」（晚击上限）与「计分宽度」（|timeDiff| → accuracy01 的分母）。
    /// 提前窗固定 -13ms（AutoPlay -16ms）不随 OD 变化；速度 Mod（DT/HT）不影响判定窗口。
    /// </summary>
    public static class JudgementConfig
    {
        /// <summary>历史基准窗口：OD8 = 250ms</summary>
        public const double WindowAtOD8 = 250.0;
        /// <summary>最窄窗口：OD10 = 225ms</summary>
        public const double WindowMin = 225.0;
        /// <summary>最宽窗口：OD0 = 350ms</summary>
        public const double WindowMax = 350.0;

        /// <summary>
        /// OD 绑定的判定窗口（毫秒）。OD 越高窗口越窄（越早 Miss）。
        /// </summary>
        public static double GetWindowMs(float od)
        {
            od = Mathf.Clamp(od, 0f, 10f);
            return 350.0 - 12.5 * od;
        }

        /// <summary>
        /// accuracy01 (1.0=完美重合, 0.0=判定边缘) → 分数档位。
        /// 以 OD8 基准窗口 250ms 为例：
        /// ≥0.8 (误差 ≤ 50ms) → 300
        /// ≥0.6 (误差 ≤ 100ms) → 100
        /// ≥0.01 (误差 ≤ 247.5ms) → 50
        /// 否则 → 0 (Miss)。实际毫秒档位随 OD 缩放（见 GetWindowMs）。
        /// </summary>
        public static int ScoreFromAccuracy(double accuracy01)
        {
            if (accuracy01 >= 0.8) return 300;
            if (accuracy01 >= 0.6) return 100;
            if (accuracy01 >= 0.01) return 50;
            return 0;
        }
    }
}
