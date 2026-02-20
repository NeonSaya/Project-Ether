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

            // 2. 初始化 LineRenderer
            lineRenderer = GetComponent<LineRenderer>();
            lineRenderer.useWorldSpace = true;
            lineRenderer.startWidth = laserWidth;
            lineRenderer.endWidth = laserWidth;
            lineRenderer.positionCount = 2;

            // 3. 修复：让射线无视深度遮挡，永远显示在 UI 上方
            if (lineRenderer.sharedMaterial == null || rayMaterial == null)
            {
                // 使用 UI/Default 材质并强制关闭深度测试 (ZTest Always)
                Material alwaysOnTopMat = new Material(Shader.Find("UI/Default"));
                alwaysOnTopMat.SetInt("unity_GUIZTestMode", (int)UnityEngine.Rendering.CompareFunction.Always);
                lineRenderer.material = alwaysOnTopMat;
            }
            else
            {
                lineRenderer.material = rayMaterial;
            }

            // 强制提升渲染层级，盖过 Canvas (100)
            lineRenderer.sortingOrder = 32767;
        }

        void LateUpdate()
        {
            if (rayController == null || rayController.visualRay == null) return;

            Transform source = rayController.visualRay;
            Vector3 startPos = source.position;
            Vector3 endPos;

            if (rayController.IsHittingUI || rayController.IsHitting)
            {
                lineRenderer.startColor = hitColor;
                lineRenderer.endColor = hitColor;
                endPos = rayController.CurrentHitPoint;
            }
            else
            {
                lineRenderer.startColor = laserColor;
                lineRenderer.endColor = laserColor;
                endPos = startPos + source.forward * rayController.rayLength;
            }

            lineRenderer.SetPosition(0, startPos);
            lineRenderer.SetPosition(1, endPos);

            lineRenderer.startWidth = laserWidth;
            lineRenderer.endWidth = laserWidth;
        }
    }
}