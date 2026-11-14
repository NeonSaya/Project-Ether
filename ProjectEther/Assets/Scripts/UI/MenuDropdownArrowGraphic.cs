using UnityEngine;
using UnityEngine.UI;

namespace OsuVR
{
    /// <summary>纯几何下拉箭头，避免原子预制体的空 Sprite 显示成方块。</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class MenuDropdownArrowGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            raycastTarget = false;
            var rect = GetPixelAdjustedRect();
            float half = Mathf.Min(rect.width * 0.5f, 5);
            var center = rect.center;
            helper.AddVert(new Vector3(center.x - half, center.y + 2), color, Vector2.zero);
            helper.AddVert(new Vector3(center.x + half, center.y + 2), color, Vector2.zero);
            helper.AddVert(new Vector3(center.x, center.y - 3), color, Vector2.zero);
            helper.AddTriangle(0, 1, 2);
        }
    }
}
