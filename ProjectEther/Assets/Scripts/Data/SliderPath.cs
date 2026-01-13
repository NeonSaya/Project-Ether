using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace OsuVR
{
    /// <summary>
    /// 滑条路径计算器
    /// </summary>
    public class SliderPath
    {
        /// <summary>
        /// 曲线类型
        /// </summary>
        public CurveType Type { get; set; }

        /// <summary>
        /// 控制点
        /// </summary>
        public List<Vector2> ControlPoints { get; set; }

        /// <summary>
        /// 期望距离（像素长度）
        /// </summary>
        public double ExpectedDistance { get; set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        public SliderPath(CurveType type, List<Vector2> controlPoints, double expectedDistance)
        {
            Type = type;
            ControlPoints = controlPoints ?? new List<Vector2>();
            ExpectedDistance = expectedDistance;

            // 确保控制点列表不为空
            if (ControlPoints.Count == 0)
            {
                ControlPoints.Add(Vector2.zero);
            }
        }

        /// <summary>
        /// 在指定进度处获取位置
        /// </summary>
        public Vector2 PositionAt(double progress)
        {
            progress = Mathf.Clamp01((float)progress);

            switch (Type)
            {
                case CurveType.Linear:
                    return CalculateLinearPosition(progress);

                case CurveType.Perfect:
                    return CalculatePerfectCurvePosition(progress);

                case CurveType.Bezier:
                    return CalculateBezierPosition(progress);

                case CurveType.Catmull:
                    return CalculateCatmullRomPosition(progress);

                default:
                    return CalculateLinearPosition(progress);
            }
        }

        /// <summary>
        /// 计算线性位置
        /// </summary>
        private Vector2 CalculateLinearPosition(double progress)
        {
            if (ControlPoints.Count < 2)
                return ControlPoints.FirstOrDefault();

            // 简单线性插值
            double totalLength = 0;
            List<double> segmentLengths = new List<double>();

            for (int i = 0; i < ControlPoints.Count - 1; i++)
            {
                float length = Vector2.Distance(ControlPoints[i], ControlPoints[i + 1]);
                segmentLengths.Add(length);
                totalLength += length;
            }

            if (totalLength == 0)
                return ControlPoints.FirstOrDefault();

            double targetLength = progress * totalLength;
            double accumulatedLength = 0;

            for (int i = 0; i < segmentLengths.Count; i++)
            {
                if (accumulatedLength + segmentLengths[i] >= targetLength)
                {
                    double segmentProgress = (targetLength - accumulatedLength) / segmentLengths[i];
                    return Vector2.Lerp(ControlPoints[i], ControlPoints[i + 1], (float)segmentProgress);
                }

                accumulatedLength += segmentLengths[i];
            }

            return ControlPoints.Last();
        }

        /// <summary>
        /// 计算完美曲线位置（圆形）
        /// </summary>
        private Vector2 CalculatePerfectCurvePosition(double progress)
        {
            // 简化实现：完美曲线需要三个控制点
            if (ControlPoints.Count != 3)
                return CalculateLinearPosition(progress);

            // 计算圆心和半径
            Vector2 p1 = ControlPoints[0];
            Vector2 p2 = ControlPoints[1];
            Vector2 p3 = ControlPoints[2];

            // 计算圆心（三点确定一个圆）
            // 这里简化处理，使用圆弧插值
            float angle = Mathf.Lerp(0, Mathf.PI * 2, (float)progress);
            Vector2 center = (p1 + p3) * 0.5f; // 简化：使用中点作为圆心
            float radius = Vector2.Distance(p1, center);

            return center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

        /// <summary>
        /// 计算贝塞尔曲线位置
        /// </summary>
        private Vector2 CalculateBezierPosition(double progress)
        {
            // 德卡斯特里奥算法
            List<Vector2> points = new List<Vector2>(ControlPoints);

            while (points.Count > 1)
            {
                List<Vector2> newPoints = new List<Vector2>();

                for (int i = 0; i < points.Count - 1; i++)
                {
                    Vector2 interpolated = Vector2.Lerp(points[i], points[i + 1], (float)progress);
                    newPoints.Add(interpolated);
                }

                points = newPoints;
            }

            return points.FirstOrDefault();
        }

        /// <summary>
        /// 计算卡特姆曲线位置
        /// </summary>
        private Vector2 CalculateCatmullRomPosition(double progress)
        {
            if (ControlPoints.Count < 4)
                return CalculateLinearPosition(progress);

            // 找到包含progress的段
            int segmentCount = ControlPoints.Count - 3;
            double segmentProgress = progress * segmentCount;
            int segmentIndex = Mathf.Clamp((int)segmentProgress, 0, segmentCount - 1);
            double t = segmentProgress - segmentIndex;

            // 获取四个控制点
            Vector2 p0 = ControlPoints[segmentIndex];
            Vector2 p1 = ControlPoints[segmentIndex + 1];
            Vector2 p2 = ControlPoints[segmentIndex + 2];
            Vector2 p3 = ControlPoints[segmentIndex + 3];

            // Catmull-Rom公式
            float t2 = (float)(t * t);
            float t3 = (float)(t2 * t);

            Vector2 position =
                0.5f * ((-p0 + 3f * p1 - 3f * p2 + p3) * t3 +
                       (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                       (-p0 + p2) * (float)t +
                       2f * p1);

            return position;
        }

        /// <summary>
        /// 计算路径总长度
        /// </summary>
        public double CalculateLength()
        {
            double length = 0;

            for (int i = 0; i < ControlPoints.Count - 1; i++)
            {
                length += Vector2.Distance(ControlPoints[i], ControlPoints[i + 1]);
            }

            return length;
        }
    }
}