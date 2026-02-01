using UnityEngine;

namespace OsuVR
{
    /// <summary>
    /// 射线视觉呈现器：复刻旧版 LaserShooter 的视觉配置体验
    /// 自动管理 LineRenderer，支持自定义颜色、宽度
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class RayVisualizer : MonoBehaviour
    {
        [Header("核心引用")]
        [Tooltip("如果不填，会自动在父物体上找 RayController")]
        public RayController rayController;

        [Header("视觉配置 (复刻旧版)")]
        public Color laserColor = Color.cyan;   // 常态颜色
        public Color hitColor = Color.yellow;   // 击中颜色
        public float laserWidth = 0.01f;        // 线条粗细

        [Header("高级特效")]
        public Material rayMaterial;            // 射线材质 (可选)

        private LineRenderer lineRenderer;

        void Start()
        {
            // 1. 自动获取 RayController
            if (rayController == null)
            {
                rayController = GetComponentInParent<RayController>();
            }

            // 2. 初始化 LineRenderer (就像以前一样)
            lineRenderer = GetComponent<LineRenderer>();
            lineRenderer.useWorldSpace = true;       // 必须用世界坐标
            lineRenderer.startWidth = laserWidth;
            lineRenderer.endWidth = laserWidth;
            lineRenderer.positionCount = 2;

            // 3. 自动设置材质 (防止变成紫色方块)
            if (lineRenderer.sharedMaterial == null)
            {
                // 如果你有指定的材质就用，没有就新建一个默认的
                if (rayMaterial != null)
                {
                    lineRenderer.material = rayMaterial;
                }
                else
                {
                    // 创建一个简单的 Shader 材质，防止变紫
                    Material defaultMat = new Material(Shader.Find("Legacy Shaders/Particles/Alpha Blended"));
                    lineRenderer.material = defaultMat;
                }
            }
        }

        void LateUpdate()
        {
            if (rayController == null || rayController.visualRay == null) return;

            // --- 核心：跟着 RayController 的逻辑轴动 ---
            // RayController 已经计算好了 Wrist-Gain 旋转，赋给了 visualRay
            // 我们只需要把线画在 visualRay 的位置和方向上
            Transform source = rayController.visualRay;

            Vector3 startPos = source.position;
            Vector3 endPos;

            // 1. 决定颜色和终点
            if (rayController.IsHitting)
            {
                // 打中：变黄，终点吸附在物体表面
                lineRenderer.startColor = hitColor;
                lineRenderer.endColor = hitColor;
                endPos = rayController.CurrentHitPoint;
            }
            else
            {
                // 没打中：变青，射向无限远
                lineRenderer.startColor = laserColor;
                lineRenderer.endColor = laserColor;
                endPos = startPos + source.forward * rayController.rayLength;
            }

            // 2. 更新线条位置
            lineRenderer.SetPosition(0, startPos);
            lineRenderer.SetPosition(1, endPos);

            // 实时更新宽度 (方便运行通过 Inspector 调节)
            lineRenderer.startWidth = laserWidth;
            lineRenderer.endWidth = laserWidth;
        }
    }
}