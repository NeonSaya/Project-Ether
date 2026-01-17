using System.Collections.Generic;
using UnityEngine;

namespace OsuVR
{
    /// <summary>
    ///  滑条网格生成器
    /// </summary>
    public static class SliderMeshGenerator
    {
        // 圆角精细度：扇形的分段数。越高越圆，12-16 是性能与画质的最佳平衡点
        private const int ROUND_SEGMENTS = 16;

        // 极小值阈值：用于过滤重合点，防止除以零
        private const float MIN_POINT_DISTANCE = 0.001f;

        public static Mesh GenerateSmoothSlider(
            List<Vector3> rawPoints,
            float radius,
            float borderThickness,
            Color bodyColor,
            Color borderColor)
        {
            if (rawPoints == null || rawPoints.Count < 2) return null;

            // -------------------------------------------------------------
            // [关键修复] 1. 数据清洗 (Sanitization)
            // 过滤掉 osu! 数据中常见的重合点 (特别是 Linear 滑条起手)
            // -------------------------------------------------------------
            List<Vector3> pathPoints = new List<Vector3>();
            pathPoints.Add(rawPoints[0]);

            for (int i = 1; i < rawPoints.Count; i++)
            {
                // 只有当当前点和上一个点的距离 > 0.001 时才加入
                // 这能完美解决 osu! 线性滑条常见的“双起点”导致的渲染爆炸 Bug
                if (Vector3.Distance(rawPoints[i], pathPoints[pathPoints.Count - 1]) > 0.001f)
                {
                    pathPoints.Add(rawPoints[i]);
                }
            }

            if (pathPoints.Count < 2) return null;

            // -------------------------------------------------------------
            // 2. 预计算法线 (Precompute Normals)
            // -------------------------------------------------------------
            int count = pathPoints.Count;
            Vector3[] forwards = new Vector3[count];
            Vector3[] rights = new Vector3[count];
            Vector3 planeNormal = Vector3.back; // 假设滑条面向 -Z

            for (int i = 0; i < count; i++)
            {
                Vector3 dir;
                if (i < count - 1)
                    dir = (pathPoints[i + 1] - pathPoints[i]).normalized;
                else
                    dir = forwards[i - 1]; // 终点沿用上一个方向

                forwards[i] = dir;

                // 计算右向量
                Vector3 r = Vector3.Cross(dir, planeNormal).normalized;

                // [保护] 防止 dir 和 planeNormal 平行导致法线丢失
                if (r.sqrMagnitude < 0.001f) r = Vector3.right;

                rights[i] = r;
            }

            // -------------------------------------------------------------
            // 3. 构建网格
            // -------------------------------------------------------------
            Mesh mesh = new Mesh();
            mesh.name = "OsuSliderMesh_Fixed";

            List<Vector3> verts = new List<Vector3>();
            List<Color> cols = new List<Color>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> tris = new List<int>();

            for (int i = 0; i < count; i++)
            {
                Vector3 currentPos = pathPoints[i];
                Vector3 currentRight = rights[i];
                Vector3 currentForward = forwards[i];

                // A. 起点圆头
                if (i == 0)
                {
                    AddCap(verts, cols, uvs, tris, currentPos, -currentForward, currentRight, radius, borderThickness, bodyColor, borderColor);
                }

                // B. 拐角连接 (修复硬转角)
                if (i > 0 && i < count - 1)
                {
                    // 确保 prevRight 是有效的
                    Vector3 prevRight = rights[i - 1];
                    AddJoin(verts, cols, uvs, tris, currentPos, prevRight, currentRight, radius, borderThickness, bodyColor, borderColor);
                }

                // C. 直线段
                if (i < count - 1)
                {
                    Vector3 nextPos = pathPoints[i + 1];
                    AddSegment(verts, cols, uvs, tris, currentPos, nextPos, currentRight, rights[i], radius, borderThickness, bodyColor, borderColor);
                }

                // D. 终点圆头
                if (i == count - 1)
                {
                    AddCap(verts, cols, uvs, tris, currentPos, forwards[i - 1], rights[i - 1], radius, borderThickness, bodyColor, borderColor);
                }
            }

            mesh.SetVertices(verts);
            mesh.SetColors(cols);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();

            return mesh;
        }

        // ====================================================================
        // 核心组件：绘制直线段
        // ====================================================================
        private static void AddSegment(
            List<Vector3> verts, List<Color> cols, List<Vector2> uvs, List<int> tris,
            Vector3 pStart, Vector3 pEnd,
            Vector3 rStart, Vector3 rEnd,
            float radius, float borderThickness,
            Color cBody, Color cBorder)
        {
            float dInner = radius;
            float dOuter = radius + borderThickness;

            // 我们生成 4 个条带 (LeftBorder, LeftBody, RightBody, RightBorder)
            // 为了简单，我们只生成 3 个 Quad (LeftBorder, Body, RightBorder)
            // 顶点顺序：从左外 -> 左内 -> 右内 -> 右外

            // Start 截面
            Vector3 s0 = pStart - rStart * dOuter;
            Vector3 s1 = pStart - rStart * dInner;
            Vector3 s2 = pStart + rStart * dInner;
            Vector3 s3 = pStart + rStart * dOuter;

            // End 截面
            Vector3 e0 = pEnd - rEnd * dOuter;
            Vector3 e1 = pEnd - rEnd * dInner;
            Vector3 e2 = pEnd + rEnd * dInner;
            Vector3 e3 = pEnd + rEnd * dOuter;

            int v = verts.Count;

            // 添加顶点
            verts.Add(s0); verts.Add(s1); verts.Add(s2); verts.Add(s3);
            verts.Add(e0); verts.Add(e1); verts.Add(e2); verts.Add(e3);

            // 添加颜色
            cols.Add(cBorder); cols.Add(cBody); cols.Add(cBody); cols.Add(cBorder);
            cols.Add(cBorder); cols.Add(cBody); cols.Add(cBody); cols.Add(cBorder);

            // 添加 UV (0=Left, 1=Right)
            uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(0.2f, 0)); uvs.Add(new Vector2(0.8f, 0)); uvs.Add(new Vector2(1, 0));
            uvs.Add(new Vector2(0, 1)); uvs.Add(new Vector2(0.2f, 1)); uvs.Add(new Vector2(0.8f, 1)); uvs.Add(new Vector2(1, 1));

            // 添加三角形 (3个 Quad = 6个 Tris)
            // Quad 1: Left Border (0-1-5-4)
            AddQuad(tris, v + 0, v + 1, v + 4, v + 5);
            // Quad 2: Main Body (1-2-6-5)
            AddQuad(tris, v + 1, v + 2, v + 5, v + 6);
            // Quad 3: Right Border (2-3-7-6)
            AddQuad(tris, v + 2, v + 3, v + 6, v + 7);
        }

        // ====================================================================
        // 核心组件：绘制圆头 (Cap)
        // ====================================================================
        private static void AddCap(
            List<Vector3> verts, List<Color> cols, List<Vector2> uvs, List<int> tris,
            Vector3 center, Vector3 forward, Vector3 right,
            float radius, float borderThickness,
            Color cBody, Color cBorder)
        {
            // 向 "forward" 方向画半圆，从 -Right 转到 +Right
            // 但因为是 Cap，通常指的是“末端封口”，所以其实是向后画
            // 这里的 forward 参数实际上是我们想要朝向的方向

            int centerIdx = verts.Count;
            verts.Add(center);
            cols.Add(cBody);
            uvs.Add(new Vector2(0.5f, 0f));

            int segments = ROUND_SEGMENTS;
            int firstEdgeIdx = centerIdx + 1;

            // 旋转轴：Z轴 (假设平面是 XY)
            // 起始向量：-Right
            Vector3 startVec = -right;

            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                // 绕 Z 轴旋转 180 度 (-Right -> Forward -> +Right)
                Quaternion rot = Quaternion.AngleAxis(180f * t, Vector3.back);
                // 注意：如果 Cap 是向后的，这里 rotation 轴可能要反过来，视 forward 方向而定
                // 这里我们做一个通用的 look rotation

                // 更通用的做法：在 -Right 和 +Right 之间插值，中间经过 forward
                // 简单的半圆插值：
                // 0.0 = -Right
                // 0.5 = Forward
                // 1.0 = +Right

                // 使用 Slerp 进行球面插值最稳
                Vector3 currentDir;
                if (t < 0.5f)
                    currentDir = Vector3.Slerp(-right, forward, t * 2f);
                else
                    currentDir = Vector3.Slerp(forward, right, (t - 0.5f) * 2f);

                currentDir.Normalize();

                // Inner Point (Body Edge)
                verts.Add(center + currentDir * radius);
                cols.Add(cBody);
                uvs.Add(new Vector2(0.5f, 1f));

                // Outer Point (Border Edge)
                verts.Add(center + currentDir * (radius + borderThickness));
                cols.Add(cBorder);
                uvs.Add(new Vector2(0.5f, 1f));
            }

            // 生成三角形
            for (int i = 0; i < segments; i++)
            {
                int baseCurr = firstEdgeIdx + i * 2;
                int baseNext = firstEdgeIdx + (i + 1) * 2;

                // 1. Body Fan (Center -> Inner -> NextInner)
                tris.Add(centerIdx);
                tris.Add(baseCurr);
                tris.Add(baseNext);

                // 2. Border Quad (Inner -> Outer -> NextOuter -> NextInner)
                AddQuad(tris, baseCurr, baseCurr + 1, baseNext, baseNext + 1);
            }
        }

        // ====================================================================
        // 核心组件：绘制拐角 (Join)
        // ====================================================================
        private static void AddJoin(
            List<Vector3> verts, List<Color> cols, List<Vector2> uvs, List<int> tris,
            Vector3 center, Vector3 rPrev, Vector3 rCurr,
            float radius, float borderThickness,
            Color cBody, Color cBorder)
        {
            // 1. 计算两个法线向量之间的直接夹角
            // 这是判断是否需要拐角连接的最可靠依据
            float angleDiff = Vector3.Angle(rPrev, rCurr);

            // 如果夹角非常小 (比如小于 0.5 度)，则认为是直线，跳过连接
            // 这个阈值足够小，不会漏掉硬转角，又能过滤掉浮点误差
            if (angleDiff < 0.5f) return;

            // 2. 判断转向 (左转还是右转)
            // 使用叉积的 Z 分量 (假设在 XY 平面)
            float crossZ = rPrev.x * rCurr.y - rPrev.y * rCurr.x;
            bool isRightTurn = crossZ < 0;

            // [极端情况保护]：如果是 180 度大掉头，crossZ 会接近 0，导致转向判断失效。
            // 如果角度很大 (>175度) 且 crossZ 很小，我们强制给一个转向 (比如右转) 以确保填充
            if (angleDiff > 175f && Mathf.Abs(crossZ) < 1e-4f) isRightTurn = true;

            // 3. 确定需要填充的 "外侧" 扇形的起始和结束向量
            // 右转 -> 左侧 (-Right) 是外侧
            // 左转 -> 右侧 (+Right) 是外侧
            Vector3 vStart = isRightTurn ? -rPrev : rPrev;
            Vector3 vEnd = isRightTurn ? -rCurr : rCurr;

            // 4. 动态分段
            // 角度越大，分段越多，保证圆滑。每 10 度分一段。
            int segments = Mathf.CeilToInt(angleDiff / 10f);
            // 至少保证有 2 段，对于硬转角来说这很重要，否则看着太尖
            if (segments < 2) segments = 2;

            // --- 开始生成几何体 ---
            int centerIdx = verts.Count;
            verts.Add(center);
            cols.Add(cBody);
            uvs.Add(new Vector2(0.5f, 0f));

            int firstEdgeIdx = centerIdx + 1;

            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                // 使用 Slerp 在外侧向量之间进行球面插值，生成完美的圆弧
                Vector3 dir = Vector3.Slerp(vStart, vEnd, t).normalized;

                // Inner point
                verts.Add(center + dir * radius);
                cols.Add(cBody);
                uvs.Add(new Vector2(0.5f, 1f));

                // Outer point
                verts.Add(center + dir * (radius + borderThickness));
                cols.Add(cBorder);
                uvs.Add(new Vector2(0.5f, 1f));
            }

            // 生成三角形
            for (int i = 0; i < segments; i++)
            {
                int baseCurr = firstEdgeIdx + i * 2;
                int baseNext = firstEdgeIdx + (i + 1) * 2;

                // Body Fan (扇形)
                tris.Add(centerIdx);
                tris.Add(baseCurr);
                tris.Add(baseNext);

                // Border Quad (四边形)
                AddQuad(tris, baseCurr, baseCurr + 1, baseNext, baseNext + 1);
            }
        }

        // 辅助：添加四边形 (两个三角形)
        // v0-v1 是左边，v2-v3 是右边 (或者 上-下)
        // 顺序：v0 -> v2 -> v1 (Tri 1), v1 -> v2 -> v3 (Tri 2)
        private static void AddQuad(List<int> tris, int v0, int v1, int v2, int v3)
        {
            tris.Add(v0);
            tris.Add(v2);
            tris.Add(v1);

            tris.Add(v1);
            tris.Add(v2);
            tris.Add(v3);
        }
    }
}