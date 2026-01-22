using System.Collections.Generic;
using UnityEngine;

namespace OsuVR
{
    public static class SliderMeshGenerator
    {
        private const int CIRCLE_RESOLUTION = 32;
        // 指定你的 Shader 名字
        private const string SHADER_NAME = "Osu/SliderVR_Flat_Stencil_VR_Fixed";

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

            // 3. 配置 Body 材质 (底层，先渲染)
            Material bodyMaterial = new Material(osuShader);
            bodyMaterial.SetColor("_Color", bodyColor);
            bodyMaterial.SetInt("_StencilID", stencilID);
            // 渲染队列设为 3000 (Transparent 默认值)，保证先画
            bodyMaterial.renderQueue = 2980;

            // 4. 配置 Border 材质 (顶层，后渲染)
            Material borderMaterial = new Material(osuShader);
            borderMaterial.SetColor("_Color", borderColor);
            borderMaterial.SetInt("_StencilID", stencilID);
            // 只有这样，Shader 里的 Stencil NotEqual 才能生效（Border 会避开 Body 的区域）
            borderMaterial.renderQueue = 2981;

            return (border, body, borderMaterial, bodyMaterial);
        }

        private static Mesh BuildSausageMesh(List<Vector3> path, float w, string name)
        {
            Mesh m = new Mesh { name = name };
            List<Vector3> v = new List<Vector3>();
            List<int> t = new List<int>();
            Vector3 up = Vector3.back; // 假设滑条是平铺在 XY 平面，背向 Z 轴

            for (int i = 0; i < path.Count; i++)
            {
                // 添加节点处的圆形盖帽
                AddCircle(v, t, path[i], w);

                // 添加两点之间的连接矩形
                if (i < path.Count - 1)
                {
                    Vector3 curr = path[i];
                    Vector3 next = path[i + 1];

                    // 计算侧向向量，构建带状网格
                    Vector3 dir = (next - curr).normalized;
                    Vector3 side = Vector3.Cross(dir, up).normalized;

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