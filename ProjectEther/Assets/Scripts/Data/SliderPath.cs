using System.Collections.Generic;
using UnityEngine;

namespace OsuVR
{
    /// <summary>
    /// [已废弃] 本类功能已由 SliderPathCalculator 完全替代
    /// 请使用 SliderPathCalculator.CalculatePoints() 和 SliderPathExtensions.GetSliderPath()
    /// SliderPathCalculator 提供更精确的曲线逼近（自适应细分贝塞尔、正确圆弧计算等）
    /// </summary>
    [System.Obsolete("Use SliderPathCalculator instead. See SliderPathExtensions.GetSliderPath() for the replacement API.")]
    public class SliderPath
    {
        public CurveType Type { get; set; }
        public List<Vector2> ControlPoints { get; set; }
        public double ExpectedDistance { get; set; }

        public SliderPath(CurveType type, List<Vector2> controlPoints, double expectedDistance)
        {
            Type = type;
            ControlPoints = controlPoints ?? new List<Vector2>();
            ExpectedDistance = expectedDistance;
            Debug.LogWarning("[Obsolete] SliderPath is deprecated. Use SliderPathCalculator.CalculatePoints() instead.");
        }
    }
}
