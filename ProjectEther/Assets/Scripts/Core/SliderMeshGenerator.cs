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
        // 核心组件：绘制拐角 (Join) - [已修复烤面筋/背面剔除问题]
        // ====================================================================
        private static void AddJoin(
            List<Vector3> verts, List<Color> cols, List<Vector2> uvs, List<int> tris,
            Vector3 center, Vector3 rPrev, Vector3 rCurr,
            float radius, float borderThickness,
            Color cBody, Color cBorder)
        {
            float angleDiff = Vector3.Angle(rPrev, rCurr);
            if (angleDiff < 0.5f) return;

            // 计算转向 (叉积 Z)
            float crossZ = rPrev.x * rCurr.y - rPrev.y * rCurr.x;
            bool isRightTurn = crossZ < 0; // 右转时 Z < 0
            
            // 180度掉头保护
            if (angleDiff > 175f && Mathf.Abs(crossZ) < 1e-4f) isRightTurn = true;

            // 确定外侧向量
            Vector3 vStart = isRightTurn ? -rPrev : rPrev;
            Vector3 vEnd   = isRightTurn ? -rCurr : rCurr;

            // 动态分段 (保证圆滑)
            int segments = Mathf.CeilToInt(angleDiff / 10f);
            if (segments < 2) segments = 2;

            int centerIdx = verts.Count;
            
            // 添加中心点 (UV X=0.5 表示在滑条中间)
            verts.Add(center);
            cols.Add(cBody);
            uvs.Add(new Vector2(0.5f, 0.5f)); 

            int firstEdgeIdx = centerIdx + 1;

            // 生成扇形顶点
            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                Vector3 dir = Vector3.Slerp(vStart, vEnd, t).normalized;

                // Inner (Body Edge)
                verts.Add(center + dir * radius);
                cols.Add(cBody);
                // UV X: 如果是外侧，根据左右转决定是 0 还是 1 (这里简单设为边缘)
                // UV Y: 保持 0.5
                float edgeUV = isRightTurn ? 0f : 1f; 
                uvs.Add(new Vector2(edgeUV, 0.5f)); // Body 边缘 (近似处理)

                // Outer (Border Edge)
                verts.Add(center + dir * (radius + borderThickness));
                cols.Add(cBorder);
                uvs.Add(new Vector2(edgeUV, 0.5f)); // Border 边缘
            }

            // 生成三角形 (关键修复：根据转向调整顶点顺序)
            for (int i = 0; i < segments; i++)
            {
                int baseCurr = firstEdgeIdx + i * 2;
                int baseNext = firstEdgeIdx + (i + 1) * 2;

                // --- 1. Body Fan (扇形) ---
                if (isRightTurn)
                {
                    // 右转：逆序连接防止剔除 (Center -> Next -> Curr)
                    tris.Add(centerIdx);
                    tris.Add(baseNext);
                    tris.Add(baseCurr);
                }
                else
                {
                    // 左转：顺序连接 (Center -> Curr -> Next)
                    tris.Add(centerIdx);
                    tris.Add(baseCurr);
                    tris.Add(baseNext);
                }

                // --- 2. Border Quad (四边形) ---
                // 同样根据转向调整顺序
                if (isRightTurn)
                {
                    // 右转 (反向)
                    AddQuad(tris, baseNext, baseNext + 1, baseCurr, baseCurr + 1);
                }
                else
                {
                    // 左转 (正向)
                    AddQuad(tris, baseCurr, baseCurr + 1, baseNext, baseNext + 1);
                }
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
