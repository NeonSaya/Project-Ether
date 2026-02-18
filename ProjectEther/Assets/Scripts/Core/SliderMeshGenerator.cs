using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine;

namespace OsuVR
{
    public static class SliderMeshGenerator
    {
        private const int CIRCLE_RESOLUTION = 32;
        // 指定你的 Shader 名字
        private const string SHADER_NAME = "Osu/SliderVR_Flat_Stencil_VR_Fixed";

        /// <summary>
        /// 生成物理滑条网格和材质
        /// </summary>
        /// <param name="stencilID">模板缓冲区 ID (1-254)</param>
        /// <remarks>
        /// 机制3: 滑条自我交叉防重叠
        /// - Body 材质 (霸体写入): StencilComp = Always, StencilOp = Replace
        /// - Border 材质 (避让读取): StencilComp = NotEqual, StencilOp = Keep
        /// </remarks>
        public static (Mesh border, Mesh body, Material borderMaterial, Material bodyMaterial) GeneratePhysicalSlider(
            List<Vector3> worldPathPoints,
            float radius,
            float borderThickness,
            Color borderColor,
            Color bodyColor,
            int stencilID)
        {
            // 1. 生成网格
            // 边框网格半径 = 半径 + 厚度
            Mesh border = BuildSausageMesh(worldPathPoints, radius + borderThickness, "Slider_Border");
            // 本体网格半径 = 半径
            Mesh body = BuildSausageMesh(worldPathPoints, radius, "Slider_Body");

            // 2. 查找并创建材质
            Shader osuShader = Shader.Find(SHADER_NAME);
            if (osuShader == null)
            {
                Debug.LogWarning($"Shader '{SHADER_NAME}' not found! Fallback to Standard.");
                osuShader = Shader.Find("Standard");
            }

            // ---------------------------------------------------------
            // 机制3: 滑条自我交叉防重叠 (Stencil Buffer)
            // 目标：利用模板缓冲区实现滑条边框对本体的完美镂空
            // ---------------------------------------------------------

            // 3. 配置 Body 材质 (霸体写入)
            // StencilComp = Always: 总是通过模板测试
            // StencilOp = Replace: 把自己的 ID 刻入屏幕
            Material bodyMaterial = new Material(osuShader);
            bodyMaterial.SetColor("_Color", bodyColor);
            bodyMaterial.SetInt("_StencilID", stencilID);
            bodyMaterial.SetInt("_StencilComp", (int)CompareFunction.Always);
            bodyMaterial.SetInt("_StencilOp", (int)StencilOp.Replace);
            // renderQueue 由 SliderController 根据三步长画家算法设置

            // 4. 配置 Border 材质 (避让读取)
            // StencilComp = NotEqual: 只有当模板缓冲区的 ID 不等于自己的 ID 时才渲染
            // StencilOp = Keep: 遇到本体留下的 ID 则自动镂空
            Material borderMaterial = new Material(osuShader);
            borderMaterial.SetColor("_Color", borderColor);
            borderMaterial.SetInt("_StencilID", stencilID);
            borderMaterial.SetInt("_StencilComp", (int)CompareFunction.NotEqual);
            borderMaterial.SetInt("_StencilOp", (int)StencilOp.Keep);
            // renderQueue 由 SliderController 根据三步长画家算法设置

            return (border, body, borderMaterial, bodyMaterial);
        }

        private static Mesh BuildSausageMesh(List<Vector3> path, float w, string name)
        {
            Mesh m = new Mesh { name = name };
            m.indexFormat = IndexFormat.UInt32;
            List<Vector3> v = new List<Vector3>();
            List<int> t = new List<int>();
            Vector3 up = Vector3.back; // 假设滑条是平铺在 XY 平面，背向 Z 轴

            for (int i = 0; i < path.Count; i++)
            {
                Vector3 curr = path[i];
                // 添加节点处的圆形盖帽
                AddCircle(v, t, curr, w);

                // 添加两点之间的连接矩形
                if (i < path.Count - 1)
                {
                    Vector3 next = path[i + 1];
                    Vector3 diff = next - curr;

                    // 如果两点距离太近（重合），归一化会产生 NaN，导致整个 Mesh 消失
                    if (diff.sqrMagnitude < 0.000001f)
                    {
                        continue; // 跳过这段无效路径
                    }

                    // 计算侧向向量
                    Vector3 dir = diff.normalized; // 现在这里安全了
                    Vector3 side = Vector3.Cross(dir, up).normalized;

                    // 如果 dir 和 up 平行（极其罕见但存在），Cross 结果为 0，normalized 也会变成 0 或 NaN
                    if (side.sqrMagnitude < 0.001f)
                    {
                        // 兜底方案：使用默认右向量
                        side = Vector3.right;
                    }
                    int b = v.Count;
                    v.Add(curr - side * w);
                    v.Add(curr + side * w);
                    v.Add(next - side * w);
                    v.Add(next + side * w);

                    // 构建两个三角形组成矩形
                    t.Add(b); t.Add(b + 2); t.Add(b + 1);
                    t.Add(b + 1); t.Add(b + 2); t.Add(b + 3);
                }
            }
            m.SetVertices(v);
            m.SetTriangles(t, 0);
            m.RecalculateBounds();
            m.RecalculateNormals(); // 建议添加，虽然 Shader 是 Unlit 但计算一下没坏处
            return m;
        }

        private static void AddCircle(List<Vector3> v, List<int> t, Vector3 c, float r)
        {
            int centerIdx = v.Count;
            v.Add(c);
            int startEdge = v.Count;

            for (int i = 0; i <= CIRCLE_RESOLUTION; i++)
            {
                float a = (i / (float)CIRCLE_RESOLUTION) * Mathf.PI * 2f;
                // 这里生成的是 XY 平面的圆，根据你的 Up 向量逻辑是匹配的
                v.Add(c + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * r);
            }

            for (int i = 0; i < CIRCLE_RESOLUTION; i++)
            {
                t.Add(centerIdx);
                t.Add(startEdge + i + 1);
                t.Add(startEdge + i);
            }
        }
    }
}