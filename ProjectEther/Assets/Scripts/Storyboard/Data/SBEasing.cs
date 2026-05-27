using System;
using UnityEngine;

namespace OsuVR.Storyboard.Data
{
    /// <summary>
    /// osu! Storyboard 缓动类型 (对应 .osu/.osb 中的 Easing 参数)
    /// 值与 osu! 文件格式中的整数索引一一对应
    /// </summary>
    public enum SBEasing
    {
        Linear = 0,
        Out,
        In,
        InQuad,
        OutQuad,
        InOutQuad,
        InCubic,
        OutCubic,
        InOutCubic,
        InQuart,
        OutQuart,
        InOutQuart,
        InQuint,
        OutQuint,
        InOutQuint,
        InSine,
        OutSine,
        InOutSine,
        InExpo,
        OutExpo,
        InOutExpo,
        InCirc,
        OutCirc,
        InOutCirc,
        InElastic,
        OutElastic,
        OutElasticHalf,
        OutElasticQuarter,
        InOutElastic,
        InBack,
        OutBack,
        InOutBack,
        InBounce,
        OutBounce,
        InOutBounce,
        OutPow10,
    }

    /// <summary>
    /// 缓动插值数学，完整移植自 osu!droid Easing.kt
    /// 所有函数接收 [0,1] 的 t 值，返回 [0,1] 的插值结果
    /// </summary>
    public static class EasingMath
    {
        public static float Interpolate(SBEasing easing, float t)
        {
            float n = Mathf.Clamp01(t);

            switch (easing)
            {
                case SBEasing.Linear:
                    return n;

                case SBEasing.In:
                case SBEasing.InQuad:
                    return n * n;

                case SBEasing.Out:
                case SBEasing.OutQuad:
                    return n * (2f - n);

                case SBEasing.InOutQuad:
                    return n < 0.5f ? n * n * 2f : (n -= 1f) * n * -2f + 1f;

                case SBEasing.InCubic:
                    return n * n * n;

                case SBEasing.OutCubic:
                    return (n -= 1f) * n * n + 1f;

                case SBEasing.InOutCubic:
                    return n < 0.5f ? n * n * n * 4f : (n -= 1f) * n * n * 4f + 1f;

                case SBEasing.InQuart:
                    return n * n * n * n;

                case SBEasing.OutQuart:
                    return 1f - (n -= 1f) * n * n * n;

                case SBEasing.InOutQuart:
                    return n < 0.5f ? n * n * n * n * 8f : (n -= 1f) * n * n * n * -8f + 1f;

                case SBEasing.InQuint:
                    return n * n * n * n * n;

                case SBEasing.OutQuint:
                    return (n -= 1f) * n * n * n * n + 1f;

                case SBEasing.InOutQuint:
                    return n < 0.5f ? n * n * n * n * n * 16f : (n -= 1f) * n * n * n * n * 16f + 1f;

                case SBEasing.InSine:
                    return 1f - Mathf.Cos(n * Mathf.PI * 0.5f);

                case SBEasing.OutSine:
                    return Mathf.Sin(n * Mathf.PI * 0.5f);

                case SBEasing.InOutSine:
                    return 0.5f - 0.5f * Mathf.Cos(Mathf.PI * n);

                case SBEasing.InExpo:
                    return Mathf.Pow(2f, 10f * (n - 1f));

                case SBEasing.OutExpo:
                    return -Mathf.Pow(2f, -10f * n) + 1f;

                case SBEasing.InOutExpo:
                    return n < 0.5f
                        ? 0.5f * Mathf.Pow(2f, 20f * n - 10f)
                        : 1f - 0.5f * Mathf.Pow(2f, -20f * n + 10f);

                case SBEasing.InCirc:
                    return 1f - Mathf.Sqrt(1f - n * n);

                case SBEasing.OutCirc:
                    return Mathf.Sqrt(1f - (n -= 1f) * n);

                case SBEasing.InOutCirc:
                    if ((n *= 2f) < 1f)
                        return 0.5f - 0.5f * Mathf.Sqrt(1f - n * n);
                    n -= 2f;
                    return 0.5f * Mathf.Sqrt(1f - n * n) + 0.5f;

                case SBEasing.InElastic:
                    return -Mathf.Pow(2f, -10f + 10f * n)
                           * Mathf.Sin((1f - 0.075f - n) * (2f * Mathf.PI) / 0.3f);

                case SBEasing.OutElastic:
                    return Mathf.Pow(2f, -10f * n)
                           * Mathf.Sin((n - 0.075f) * (2f * Mathf.PI) / 0.3f) + 1f;

                case SBEasing.OutElasticHalf:
                    return Mathf.Pow(2f, -10f * n)
                           * Mathf.Sin((0.5f * n - 0.075f) * (2f * Mathf.PI) / 0.3f) + 1f;

                case SBEasing.OutElasticQuarter:
                    return Mathf.Pow(2f, -10f * n)
                           * Mathf.Sin((0.25f * n - 0.075f) * (2f * Mathf.PI) / 0.3f) + 1f;

                case SBEasing.InOutElastic:
                    if ((n *= 2f) < 1f)
                        return -0.5f * Mathf.Pow(2f, -10f + 10f * n)
                               * Mathf.Sin((1f - 0.075f * 1.5f - n) * (2f * Mathf.PI) / (0.3f * 1.5f));
                    return 0.5f * Mathf.Pow(2f, -10f * (n -= 1f))
                           * Mathf.Sin((n - 0.075f * 1.5f) * (2f * Mathf.PI) / (0.3f * 1.5f)) + 1f;

                case SBEasing.InBack:
                    return n * n * ((1.70158f + 1f) * n - 1.70158f);

                case SBEasing.OutBack:
                    return (n -= 1f) * n * ((1.70158f + 1f) * n + 1.70158f) + 1f;

                case SBEasing.InOutBack:
                    if ((n *= 2f) < 1f)
                        return 0.5f * n * n * ((1.70158f * 1.525f + 1f) * n - 1.70158f * 1.525f);
                    n -= 2f;
                    return 0.5f * (n * n * ((1.70158f * 1.525f + 1f) * n + 1.70158f * 1.525f) + 2f);

                case SBEasing.InBounce:
                    return 1f - InterpolateBounce(1f - n);

                case SBEasing.OutBounce:
                    return InterpolateBounce(n);

                case SBEasing.InOutBounce:
                    return n < 0.5f
                        ? 0.5f - 0.5f * InterpolateBounce(1f - n * 2f)
                        : InterpolateBounce((n - 0.5f) * 2f) * 0.5f + 0.5f;

                case SBEasing.OutPow10:
                    return (n -= 1f) * Mathf.Pow(n, 10f) + 1f;

                default:
                    return n;
            }
        }

        static float InterpolateBounce(float n)
        {
            if (n < 1f / 2.75f)
                return 7.5625f * n * n;
            if (n < 2f * (1f / 2.75f))
                return 7.5625f * (n -= 1.5f * (1f / 2.75f)) * n + 0.75f;
            if (n < 2.5f * (1f / 2.75f))
                return 7.5625f * (n -= 2.25f * (1f / 2.75f)) * n + 0.9375f;
            return 7.5625f * (n -= 2.625f * (1f / 2.75f)) * n + 0.984375f;
        }

        /// <summary>
        /// 从 osu! 文件格式的整数索引解析缓动类型
        /// </summary>
        public static SBEasing FromInt(int value)
        {
            if (value >= 0 && value <= (int)SBEasing.OutPow10)
                return (SBEasing)value;
            return SBEasing.Linear;
        }
    }
}
