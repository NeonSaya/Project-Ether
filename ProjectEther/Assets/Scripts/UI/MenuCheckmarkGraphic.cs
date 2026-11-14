using UnityEngine;
using UnityEngine.UI;

namespace OsuVR
{
    /// <summary>继承 Image 以复用开关的原有填充动画，用几何绘制清晰勾选标记。</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class MenuCheckmarkGraphic : Image
    {
        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            Rect rect = GetPixelAdjustedRect();
            if (rect.width <= 0 || rect.height <= 0)
                return;
            Vector2 Point(float x, float y) =>
                new Vector2(rect.xMin + x * rect.width, rect.yMin + y * rect.height);
            Segment(helper, Point(0.16f, 0.49f), Point(0.40f, 0.25f), rect.width * 0.10f);
            Segment(helper, Point(0.40f, 0.25f), Point(0.86f, 0.78f), rect.width * 0.10f);
        }

        private void Segment(VertexHelper helper, Vector2 start, Vector2 end, float halfWidth)
        {
            Vector2 direction = (end - start).normalized;
            Vector2 normal = new Vector2(-direction.y, direction.x) * halfWidth;
            int first = helper.currentVertCount;
            helper.AddVert(start - normal, color, Vector2.zero);
            helper.AddVert(start + normal, color, Vector2.zero);
            helper.AddVert(end + normal, color, Vector2.zero);
            helper.AddVert(end - normal, color, Vector2.zero);
            helper.AddTriangle(first, first + 1, first + 2);
            helper.AddTriangle(first, first + 2, first + 3);
        }
    }
}
