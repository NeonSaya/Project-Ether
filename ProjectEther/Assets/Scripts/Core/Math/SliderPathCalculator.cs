using System;
using System.Collections.Generic;
using UnityEngine;

namespace OsuVR
{
    /// <summary>
    /// 滑条路径计算器：将各种曲线类型转换为点集
    /// 参考 osu-droid 的 PathApproximation.kt
    /// </summary>
    public static class SliderPathCalculator
    {
        // 常量定义
        public const int CATMULL_DETAIL = 50;
        public const float BEZIER_TOLERANCE = 0.25f;
        public const float CIRCULAR_ARC_TOLERANCE = 0.1f;
        private const float EPSILON = 0.0001f; // 用于浮点数比较的精度

        /// <summary>
        /// 创建贝塞尔曲线的分段线性逼近
        /// 通过自适应重复细分控制点，直到它们的逼近误差低于给定阈值
        /// </summary>
        /// <param name="controlPoints">曲线的控制点</param>
        /// <returns>表示结果分段线性逼近的点</returns>
        public static List<Vector2> ApproximatedBezier(List<Vector2> controlPoints)
        {
            List<Vector2> output = new List<Vector2>();
            int count = controlPoints.Count - 1;

            if (count < 0)
            {
                return output;
            }

            // "toFlatten" 包含所有尚未足够近似的曲线
            // 我们使用栈来模拟递归，而没有栈溢出的风险
            Stack<Vector2[]> toFlatten = new Stack<Vector2[]>();
            Stack<Vector2[]> freeBuffers = new Stack<Vector2[]>();

            toFlatten.Push(controlPoints.ToArray());
            Vector2[] subdivisionBuffer1 = new Vector2[count + 1];
            Vector2[] subdivisionBuffer2 = new Vector2[count * 2 + 1];

            while (toFlatten.Count > 0)
            {
                Vector2[] parent = toFlatten.Pop();

                if (BezierIsFlatEnough(parent))
                {
                    // 如果当前操作的控制点足够"平坦"，我们使用
                    // De Casteljau 算法的扩展来获得由我们的控制点表示的贝塞尔曲线的分段线性逼近
                    BezierApproximate(parent, output, subdivisionBuffer1, subdivisionBuffer2, count + 1);
                    freeBuffers.Push(parent);
                    continue;
                }

                // 如果我们还没有足够"平坦"（换句话说，详细）的逼近，我们继续细分当前操作的曲线
                Vector2[] rightChild = freeBuffers.Count > 0 ? freeBuffers.Pop() : new Vector2[count + 1];
                BezierSubdivide(parent, subdivisionBuffer2, rightChild, subdivisionBuffer1, count + 1);

                // 我们为其中一个子节点重用父节点的缓冲区，这样每次迭代可以节省一次分配
                for (int i = 0; i <= count; i++)
                {
                    parent[i] = subdivisionBuffer2[i];
                }

                toFlatten.Push(rightChild);
                toFlatten.Push(parent);
            }

            output.Add(controlPoints[count]);
            return output;
        }

        /// <summary>
        /// 创建 Catmull-Rom 样条的分段线性逼近
        /// </summary>
        /// <param name="controlPoints">控制点</param>
        /// <returns>表示结果分段线性逼近的点</returns>
        public static List<Vector2> ApproximatedCatmull(List<Vector2> controlPoints)
        {
            List<Vector2> result = new List<Vector2>();

            for (int i = 0; i < controlPoints.Count - 1; i++)
            {
                Vector2 v1 = i > 0 ? controlPoints[i - 1] : controlPoints[i];
                Vector2 v2 = controlPoints[i];
                Vector2 v3 = i < controlPoints.Count - 1 ? controlPoints[i + 1] : v2 + v2 - v1;
                Vector2 v4 = i < controlPoints.Count - 2 ? controlPoints[i + 2] : v3 + v3 - v2;

                for (int c = 0; c < CATMULL_DETAIL; c++)
                {
                    result.Add(CatmullFindPoint(v1, v2, v3, v4, c / (float)CATMULL_DETAIL));
                    result.Add(CatmullFindPoint(v1, v2, v3, v4, (c + 1) / (float)CATMULL_DETAIL));
                }
            }

            return result;
        }

        /// <summary>
        /// 创建圆形弧曲线的分段线性逼近
        /// </summary>
        /// <param name="controlPoints">控制点（应为3个点）</param>
        /// <returns>表示结果分段线性逼近的点</returns>
        public static List<Vector2> ApproximatedCircularArc(List<Vector2> controlPoints)
        {
            if (controlPoints.Count != 3)
            {
                // 如果不是3个点，退回到贝塞尔曲线逼近
                return ApproximatedBezier(controlPoints);
            }

            Vector2 a = controlPoints[0];
            Vector2 b = controlPoints[1];
            Vector2 c = controlPoints[2];

            // 如果我们有一个退化的三角形，其中边长几乎为零，则放弃并回退到更数值稳定的方法
            float cross = (b.y - a.y) * (c.x - a.x) - (b.x - a.x) * (c.y - a.y);
            if (Mathf.Abs(cross) < EPSILON)
            {
                return ApproximatedBezier(controlPoints);
            }

            // 参见：https://en.wikipedia.org/wiki/Circumscribed_circle#Cartesian_coordinates_2
            float d = 2 * (a.x * (b.y - c.y) + b.x * (c.y - a.y) + c.x * (a.y - b.y));

            float aSq = a.x * a.x + a.y * a.y;
            float bSq = b.x * b.x + b.y * b.y;
            float cSq = c.x * c.x + c.y * c.y;

            Vector2 center = new Vector2(
                (aSq * (b.y - c.y) + bSq * (c.y - a.y) + cSq * (a.y - b.y)) / d,
                (aSq * (c.x - b.x) + bSq * (a.x - c.x) + cSq * (b.x - a.x)) / d
            );

            Vector2 dA = a - center;
            Vector2 dC = c - center;

            float radius = dA.magnitude;
            double thetaStart = Math.Atan2(dA.y, dA.x);
            double thetaEnd = Math.Atan2(dC.y, dC.x);

            while (thetaEnd < thetaStart)
            {
                thetaEnd += 2 * Math.PI;
            }

            double direction = 1.0;
            double thetaRange = thetaEnd - thetaStart;

            // 根据 B 在 AC 的哪一侧来决定绘制圆的方向
            Vector2 orthoAtoC = c - a;
            orthoAtoC = new Vector2(orthoAtoC.y, -orthoAtoC.x);

            if (Vector2.Dot(orthoAtoC, b - a) < 0)
            {
                direction = -direction;
                thetaRange = 2 * Math.PI - thetaRange;
            }

            // 我们通过要求离散曲率小于提供的容差来选择逼近的点数
            // 满足容差所需的确切角度是：2 * acos(1 - TOLERANCE / radius)
            // 特殊情况是针对半径小于容差的极短滑条。这是一个病态情况而非现实情况
            int amountPoints;
            if (2 * radius <= CIRCULAR_ARC_TOLERANCE)
            {
                amountPoints = 2;
            }
            else
            {
                amountPoints = Mathf.Max(2,
                    (int)Math.Ceiling(thetaRange / (2 * Math.Acos(1 - CIRCULAR_ARC_TOLERANCE / radius))));
            }

            List<Vector2> output = new List<Vector2>();

            for (int i = 0; i < amountPoints; i++)
            {
                double fraction = (double)i / (amountPoints - 1);
                double theta = thetaStart + direction * fraction * thetaRange;
                Vector2 o = new Vector2((float)Math.Cos(theta), (float)Math.Sin(theta)) * radius;

                output.Add(center + o);
            }

            return output;
        }

        /// <summary>
        /// 创建线性曲线的分段线性逼近（基本上就是返回输入）
        /// </summary>
        /// <param name="controlPoints">控制点</param>
        public static List<Vector2> ApproximatedLinear(List<Vector2> controlPoints)
        {
            // 线性曲线：直接返回输入点
            return new List<Vector2>(controlPoints);
        }

        /// <summary>
        /// 计算给定曲线类型和参数的点集
        /// </summary>
        /// <param name="curveType">曲线类型</param>
        /// <param name="controlPoints">控制点</param>
        /// <returns>逼近的点集</returns>
        public static List<Vector2> CalculatePoints(CurveType curveType, List<Vector2> controlPoints)
        {
            switch (curveType)
            {
                case CurveType.Linear:
                    return ApproximatedLinear(controlPoints);

                case CurveType.Bezier:
                    return ApproximatedBezier(controlPoints);

                case CurveType.Catmull:
                    return ApproximatedCatmull(controlPoints);

                case CurveType.Perfect:
                    return ApproximatedCircularArc(controlPoints);

                default:
                    Debug.LogWarning($"未知的曲线类型: {curveType}，使用线性逼近");
                    return ApproximatedLinear(controlPoints);
            }
        }

        /// <summary>
        /// 检查贝塞尔曲线是否足够平坦以进行逼近
        /// 确保二阶导数（使用有限元近似）在可容忍的范围内
        /// </summary>
        /// <param name="controlPoints">控制点</param>
        /// <returns>是否足够平坦</returns>
        private static bool BezierIsFlatEnough(Vector2[] controlPoints)
        {
            for (int i = 1; i < controlPoints.Length - 1; i++)
            {
                Vector2 prev = controlPoints[i - 1];
                Vector2 current = controlPoints[i];
                Vector2 next = controlPoints[i + 1];
                Vector2 finalVec = prev - current * 2 + next;

                // 检查向量的平方长度是否大于容差平方的4倍
                if (finalVec.sqrMagnitude > BEZIER_TOLERANCE * BEZIER_TOLERANCE * 4)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 逼近贝塞尔曲线
        /// 使用德卡斯特里奥算法获得最优的分段线性逼近
        /// </summary>
        /// <param name="controlPoints">描述要逼近的贝塞尔曲线的控制点</param>
        /// <param name="output">表示结果分段线性逼近的点</param>
        /// <param name="subdivisionBuffer1">包含当前细分状态的第一缓冲区</param>
        /// <param name="subdivisionBuffer2">包含当前细分状态的第二缓冲区</param>
        /// <param name="count">原始数组中的控制点数量</param>
        private static void BezierApproximate(
            Vector2[] controlPoints, List<Vector2> output,
            Vector2[] subdivisionBuffer1, Vector2[] subdivisionBuffer2,
            int count)
        {
            BezierSubdivide(controlPoints, subdivisionBuffer2, subdivisionBuffer1, subdivisionBuffer1, count);

            for (int i = 0; i < count - 1; i++)
            {
                subdivisionBuffer2[count + i] = subdivisionBuffer1[i + 1];
            }

            output.Add(controlPoints[0]);

            for (int i = 1; i < count - 1; i++)
            {
                int index = 2 * i;
                Vector2 p = (subdivisionBuffer2[index - 1] + subdivisionBuffer2[index] * 2 + subdivisionBuffer2[index + 1]) * 0.25f;
                output.Add(p);
            }
        }

        /// <summary>
        /// 将表示贝塞尔曲线的 n 个控制点细分为 2 组 n 个控制点，每组描述相当于原始曲线一半的贝塞尔曲线
        /// </summary>
        /// <param name="controlPoints">滑条的锚点</param>
        /// <param name="l">用于逼近的滑条部分</param>
        /// <param name="r">用于逼近的滑条部分</param>
        /// <param name="subdivisionBuffer">用于逼近的滑条部分</param>
        /// <param name="count">滑条中的锚点数量</param>
        private static void BezierSubdivide(
            Vector2[] controlPoints, Vector2[] l, Vector2[] r,
            Vector2[] subdivisionBuffer, int count)
        {
            // 将控制点复制到细分缓冲区
            for (int i = 0; i < count; i++)
            {
                subdivisionBuffer[i] = controlPoints[i];
            }

            // 进行细分
            for (int i = 0; i < count; i++)
            {
                l[i] = subdivisionBuffer[0];
                r[count - i - 1] = subdivisionBuffer[count - i - 1];

                for (int j = 0; j < count - i - 1; j++)
                {
                    subdivisionBuffer[j] = (subdivisionBuffer[j] + subdivisionBuffer[j + 1]) * 0.5f;
                }
            }
        }

        /// <summary>
        /// 在样条的参数位置找到一个点
        /// </summary>
        /// <param name="vec1">第一个点</param>
        /// <param name="vec2">第二个点</param>
        /// <param name="vec3">第三个点</param>
        /// <param name="vec4">第四个点</param>
        /// <param name="t">在样条上找到点的参数，范围 [0, 1]</param>
        private static Vector2 CatmullFindPoint(
            Vector2 vec1, Vector2 vec2,
            Vector2 vec3, Vector2 vec4, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;

            float x = 0.5f *
                (2 * vec2.x +
                (-vec1.x + vec3.x) * t +
                (2 * vec1.x - 5 * vec2.x + 4 * vec3.x - vec4.x) * t2 +
                (-vec1.x + 3 * vec2.x - 3 * vec3.x + vec4.x) * t3);

            float y = 0.5f *
                (2 * vec2.y +
                (-vec1.y + vec3.y) * t +
                (2 * vec1.y - 5 * vec2.y + 4 * vec3.y - vec4.y) * t2 +
                (-vec1.y + 3 * vec2.y - 3 * vec3.y + vec4.y) * t3);

            return new Vector2(x, y);
        }

        /// <summary>
        /// 计算路径的总长度
        /// </summary>
        /// <param name="points">路径点集</param>
        /// <returns>总长度</returns>
        public static float CalculatePathLength(List<Vector2> points)
        {
            if (points == null || points.Count < 2)
                return 0f;

            float length = 0f;
            for (int i = 0; i < points.Count - 1; i++)
            {
                length += Vector2.Distance(points[i], points[i + 1]);
            }
            return length;
        }

        /// <summary>
        /// 在路径上找到指定进度处的位置
        /// </summary>
        /// <param name="points">路径点集</param>
        /// <param name="progress">进度 (0-1)</param>
        /// <returns>位置</returns>
        public static Vector2 FindPointOnPath(List<Vector2> points, float progress)
        {
            if (points == null || points.Count == 0)
                return Vector2.zero;

            if (points.Count == 1 || progress <= 0)
                return points[0];

            if (progress >= 1)
                return points[points.Count - 1];

            // 计算总长度
            float totalLength = CalculatePathLength(points);
            float targetLength = totalLength * progress;

            // 找到目标点所在的线段
            float accumulatedLength = 0f;
            for (int i = 0; i < points.Count - 1; i++)
            {
                float segmentLength = Vector2.Distance(points[i], points[i + 1]);

                if (accumulatedLength + segmentLength >= targetLength)
                {
                    float segmentProgress = (targetLength - accumulatedLength) / segmentLength;
                    return Vector2.Lerp(points[i], points[i + 1], segmentProgress);
                }

                accumulatedLength += segmentLength;
            }

            return points[points.Count - 1];
        }

        /// <summary>
        /// 简化路径点（移除过于接近的点）
        /// </summary>
        /// <param name="points">原始点集</param>
        /// <param name="tolerance">容差（小于此距离的点将被移除）</param>
        /// <returns>简化后的点集</returns>
        public static List<Vector2> SimplifyPath(List<Vector2> points, float tolerance = 0.01f)
        {
            if (points == null || points.Count < 3)
                return new List<Vector2>(points);

            List<Vector2> simplified = new List<Vector2>();
            simplified.Add(points[0]);

            float sqrTolerance = tolerance * tolerance;

            for (int i = 1; i < points.Count - 1; i++)
            {
                // 如果点之间的距离足够大，则保留
                if ((points[i] - simplified[simplified.Count - 1]).sqrMagnitude > sqrTolerance)
                {
                    simplified.Add(points[i]);
                }
            }

            // 确保最后一个点被添加
            if (simplified[simplified.Count - 1] != points[points.Count - 1])
            {
                simplified.Add(points[points.Count - 1]);
            }

            return simplified;
        }
    }

    /// <summary>
    /// 扩展方法，便于在游戏中使用
    /// </summary>
    public static class SliderPathExtensions
    {
        /// <summary>
        /// 获取滑条路径上的点（基于像素长度）
        /// </summary>
        /// <param name="slider">滑条对象</param>
        /// <param name="pixelLength">像素长度（如果为0或负值，则使用滑条的PixelLength）</param>
        /// <returns>路径点集</returns>
        public static List<Vector2> GetSliderPath(this SliderObject slider, float pixelLength = 0f)
        {
            if (slider.ControlPoints == null || slider.ControlPoints.Count == 0)
                return new List<Vector2>();

            // 将控制点转换为相对于滑条起点的坐标
            List<Vector2> controlPoints = new List<Vector2>();
            foreach (Vector2 point in slider.ControlPoints)
            {
                controlPoints.Add(point);
            }

            // 计算路径点
            List<Vector2> pathPoints = SliderPathCalculator.CalculatePoints(slider.CurveType, controlPoints);

            // 如果有指定的像素长度，调整路径
            float targetLength = pixelLength > 0 ? pixelLength : (float)slider.PixelLength;
            if (targetLength > 0)
            {
                // 计算当前路径长度
                float currentLength = SliderPathCalculator.CalculatePathLength(pathPoints);

                if (currentLength > 0 && Mathf.Abs(currentLength - targetLength) > 0.01f)
                {
                    // 重新采样路径以匹配目标长度
                    pathPoints = ResamplePathToLength(pathPoints, targetLength);
                }
            }

            // 将点转换回世界坐标
            for (int i = 0; i < pathPoints.Count; i++)
            {
                pathPoints[i] = pathPoints[i] + slider.Position;
            }

            return pathPoints;
        }

        /// <summary>
        /// 将路径重新采样到指定长度
        /// </summary>
        /// <param name="originalPath">原始路径</param>
        /// <param name="targetLength">目标长度</param>
        /// <returns>重新采样后的路径</returns>
        private static List<Vector2> ResamplePathToLength(List<Vector2> originalPath, float targetLength)
        {
            float currentLength = SliderPathCalculator.CalculatePathLength(originalPath);

            // 如果长度很接近，不需要重新采样
            if (Mathf.Abs(currentLength - targetLength) < 0.01f)
                return new List<Vector2>(originalPath);

            List<Vector2> resampled = new List<Vector2>();

            // 根据目标长度计算需要的点数（每单位长度至少一个点）
            int numPoints = Mathf.Max(2, Mathf.CeilToInt(targetLength));

            for (int i = 0; i < numPoints; i++)
            {
                float progress = i / (float)(numPoints - 1);
                float fractionOnOriginalCurve = progress * (targetLength / currentLength);
                Vector2 point = SliderPathCalculator.FindPointOnPath(originalPath, fractionOnOriginalCurve);
                resampled.Add(point);
            }

            return resampled;
        }
    }
}