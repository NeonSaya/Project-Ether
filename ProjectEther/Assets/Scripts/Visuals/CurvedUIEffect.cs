using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace OsuVR
{
    /// <summary>
    /// 简单的曲面 UI 效果
    /// 挂载在 Image 或 TextMeshProUGUI 上，会让它们呈现圆柱形弯曲
    /// </summary>
    [ExecuteInEditMode]
    public class CurvedUIEffect : BaseMeshEffect
    {
        [Header("弯曲设置")]
        [Tooltip("曲率半径 (米)，数值越小越弯，通常设为和摄像机距离一致")]
        public float curveRadius = 3.5f;

        [Tooltip("弯曲方向：正数向后弯(包围玩家)，负数向前弯")]
        public float curveMultiplier = 1.0f;

        [Header("质量设置")]
        [Tooltip("图片细分段数。文字不需要改这个(设为1)，但背景大图建议设为 20-30 以获得平滑曲线")]
        [Range(1, 50)]
        public int tessellationSegments = 1;

        // 缓存列表，避免 GC
        private List<UIVertex> stream = new List<UIVertex>();

        protected override void Awake()
        {
            base.Awake();
            // 如果是 Image，自动增加细分段数，否则弯不过来
            if (GetComponent<Image>() != null && tessellationSegments == 1)
            {
                tessellationSegments = 20;
            }
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0) return;

            // 安全检查：防止半径为 0 导致除零错误
            if (Mathf.Abs(curveRadius) < 0.1f)
            {
                // 如果半径太小，就不弯曲（或者强制设为一个极小值）
                return;
            }

            // 1. 如果需要细分 (针对大图片背景)
            if (tessellationSegments > 1)
            {
                SubdivideMesh(vh);
            }

            // 2. 获取所有顶点
            vh.GetUIVertexStream(stream);
            int count = stream.Count;

            float centerOffsetX = transform.localPosition.x;

            for (int i = 0; i < count; i++)
            {
                UIVertex v = stream[i];

                float globalX = v.position.x + centerOffsetX;

                // --- 核心弯曲公式 ---
                // 这里如果 curveRadius 是 0，会由上面的 if 拦截，保证安全
                float zOffset = -(globalX * globalX) / (2.0f * curveRadius) * curveMultiplier;

                v.position.z += zOffset;

                stream[i] = v;
            }

            // 3. 应用回 VertexHelper
            vh.Clear();
            vh.AddUIVertexTriangleStream(stream);
            stream.Clear();
        }

        /// <summary>
        /// 将简单的 Quad (4顶点) 切割成多个片段，以便能够弯曲
        /// </summary>
        private void SubdivideMesh(VertexHelper vh)
        {
            // 只有当是标准的 Quad (4顶点, 2三角形) 时才处理
            // TextMeshPro 的字本身就有很多顶点，不需要细分
            if (vh.currentVertCount != 4) return;

            UIVertex v0 = new UIVertex();
            UIVertex v1 = new UIVertex();
            UIVertex v2 = new UIVertex();
            UIVertex v3 = new UIVertex();

            vh.PopulateUIVertex(ref v0, 0); // 左下
            vh.PopulateUIVertex(ref v1, 1); // 左上
            vh.PopulateUIVertex(ref v2, 2); // 右上
            vh.PopulateUIVertex(ref v3, 3); // 右下

            vh.Clear();

            float step = 1.0f / tessellationSegments;

            for (int i = 0; i < tessellationSegments; i++)
            {
                float t1 = i * step;
                float t2 = (i + 1) * step;

                // 插值生成新的四个顶点
                UIVertex newV0 = LerpUIVertex(v0, v3, t1);
                UIVertex newV1 = LerpUIVertex(v1, v2, t1);
                UIVertex newV2 = LerpUIVertex(v1, v2, t2);
                UIVertex newV3 = LerpUIVertex(v0, v3, t2);

                vh.AddUIVertexQuad(new UIVertex[] { newV0, newV1, newV2, newV3 });
            }
        }

        private UIVertex LerpUIVertex(UIVertex a, UIVertex b, float t)
        {
            UIVertex v = new UIVertex();
            v.position = Vector3.Lerp(a.position, b.position, t);
            v.normal = Vector3.Lerp(a.normal, b.normal, t);
            v.tangent = Vector4.Lerp(a.tangent, b.tangent, t);
            v.uv0 = Vector2.Lerp(a.uv0, b.uv0, t);
            v.color = Color32.Lerp(a.color, b.color, t);
            return v;
        }
    }
}