using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace OsuVR
{
    /// <summary>仅绘制背景：等比纹理、横向渐变和四边羽化，不参与射线命中。</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class SongSelectionBackdropGraphic : MaskableGraphic
    {
        private Texture backgroundTexture;
        private Color leftColor = Color.white;
        private Color rightColor = Color.white;
        private float gradientStart;
        private float gradientEnd = 1f;
        private float feather = 20f;

        public override Texture mainTexture =>
            backgroundTexture != null ? backgroundTexture : Texture2D.whiteTexture;
        public Texture BackgroundTexture => backgroundTexture;

        public void SetTexture(Texture texture)
        {
            if (backgroundTexture == texture)
                return;
            backgroundTexture = texture;
            SetMaterialDirty();
            SetVerticesDirty();
        }

        public void Configure(
            Color left,
            Color right,
            float start = 0f,
            float end = 1f,
            float featherSize = 20f
        )
        {
            leftColor = left;
            rightColor = right;
            gradientStart = Mathf.Clamp01(start);
            gradientEnd = Mathf.Max(gradientStart + 0.001f, Mathf.Clamp01(end));
            feather = Mathf.Max(0, featherSize);
            raycastTarget = false;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            Rect rect = GetPixelAdjustedRect();
            if (rect.width <= 0 || rect.height <= 0)
                return;
            var xs = Stops(rect.width, gradientStart, gradientEnd);
            var ys = Stops(rect.height, 0, 1);
            Rect uv =
                backgroundTexture != null
                    ? SongSelectionStyle.CoverUvRect(
                        new Vector2(backgroundTexture.width, backgroundTexture.height),
                        rect.size
                    )
                    : new Rect(0, 0, 1, 1);
            for (int y = 0; y < ys.Count; y++)
            for (int x = 0; x < xs.Count; x++)
            {
                float nx = xs[x];
                float ny = ys[y];
                float transition = Mathf.SmoothStep(
                    0,
                    1,
                    Mathf.InverseLerp(gradientStart, gradientEnd, nx)
                );
                Color tint = Color.Lerp(leftColor, rightColor, transition) * color;
                tint.a *= EdgeAlpha(nx, rect.width) * EdgeAlpha(ny, rect.height);
                helper.AddVert(
                    new Vector3(rect.xMin + nx * rect.width, rect.yMin + ny * rect.height),
                    tint,
                    new Vector2(uv.x + nx * uv.width, uv.y + ny * uv.height)
                );
            }
            for (int y = 0; y < ys.Count - 1; y++)
            for (int x = 0; x < xs.Count - 1; x++)
            {
                int index = y * xs.Count + x;
                helper.AddTriangle(index, index + xs.Count, index + xs.Count + 1);
                helper.AddTriangle(index, index + xs.Count + 1, index + 1);
            }
        }

        private float EdgeAlpha(float position, float length)
        {
            if (feather <= 0)
                return 1;
            float distance = Mathf.Min(position, 1 - position) * length;
            return Mathf.SmoothStep(
                0,
                1,
                Mathf.Clamp01(distance / Mathf.Min(feather, length * 0.5f))
            );
        }

        private List<float> Stops(float length, float start, float end)
        {
            float edge = Mathf.Min(0.5f, feather / length);
            var values = new List<float>
            {
                0,
                edge / 3,
                edge * 2 / 3,
                edge,
                start,
                Mathf.Lerp(start, end, 0.25f),
                Mathf.Lerp(start, end, 0.5f),
                Mathf.Lerp(start, end, 0.75f),
                end,
                1 - edge,
                1 - edge * 2 / 3,
                1 - edge / 3,
                1,
            };
            values.Sort();
            for (int i = values.Count - 1; i > 0; i--)
                if (Mathf.Abs(values[i] - values[i - 1]) < 0.00001f)
                    values.RemoveAt(i);
            return values;
        }
    }
}
