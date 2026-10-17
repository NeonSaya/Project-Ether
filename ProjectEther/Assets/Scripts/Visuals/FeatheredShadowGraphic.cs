using UnityEngine;
using UnityEngine.UI;

namespace OsuVR
{
    /// <summary>
    /// 用同心椭圆的顶点透明度绘制柔和阴影，无额外纹理、材质实例或模糊通道。
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class FeatheredShadowGraphic : MaskableGraphic
    {
        private const int Segments = 48;
        private const int Rings = 12;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect rect = GetPixelAdjustedRect();
            if (rect.width <= 0f || rect.height <= 0f)
                return;

            Vector2 centre = rect.center;
            vh.AddVert(centre, color, Vector2.zero);
            for (int ring = 1; ring <= Rings; ring++)
            {
                float radius = (float)ring / Rings;
                Color ringColor = color;
                ringColor.a *= 1f - Mathf.SmoothStep(0f, 1f, radius);
                int start = 1 + (ring - 1) * Segments;
                for (int segment = 0; segment < Segments; segment++)
                {
                    float angle = segment * (2f * Mathf.PI / Segments);
                    Vector2 position =
                        centre
                        + new Vector2(
                            Mathf.Cos(angle) * rect.width * 0.5f * radius,
                            Mathf.Sin(angle) * rect.height * 0.5f * radius
                        );
                    vh.AddVert(position, ringColor, Vector2.zero);
                }

                for (int segment = 0; segment < Segments; segment++)
                {
                    int next = (segment + 1) % Segments;
                    if (ring == 1)
                    {
                        vh.AddTriangle(0, start + segment, start + next);
                        continue;
                    }

                    int previous = start - Segments;
                    vh.AddTriangle(previous + segment, start + segment, start + next);
                    vh.AddTriangle(previous + segment, start + next, previous + next);
                }
            }
        }
    }
}
