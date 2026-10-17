using UnityEngine;
using UnityEngine.Rendering;

namespace OsuVR
{
    /// <summary>舞台配色预设：直接对应几种常见的演出风格。</summary>
    public enum StagePalettePreset
    {
        EtherNeon,  // 项目原生：青 + 品红 + 紫，贴合空灵背景
        BeatSaber,  // 经典红 + 蓝
        SunsetClub, // 落日俱乐部：橙 + 粉 + 青
        MonoIce,    // 单色冰蓝，最克制
    }

    /// <summary>
    /// 霓虹舞台工具：程序化几何 + URP Unlit 加色发光材质。
    /// 材质与网格均为运行期创建，释放由调用方负责（见 StageVariantBase）。
    /// 颜色用 HDR（分量可大于 1），配合项目既有的 Bloom（阈值 1）产生辉光。
    /// </summary>
    public static class NeonStageKit
    {
        const string UnlitShaderName = "Universal Render Pipeline/Unlit";

        static Texture2D _fadeUp;
        static Texture2D _radial;

        /// <summary>纵向渐变贴图：v=0 最亮，v=1 渐隐（光柱、激光、幕墙用）。</summary>
        public static Texture2D FadeUp
        {
            get
            {
                if (_fadeUp == null) _fadeUp = CreateFadeUpTexture();
                return _fadeUp;
            }
        }

        /// <summary>径向渐变贴图：中心亮、边缘透明（地面光环、光斑用）。</summary>
        public static Texture2D Radial
        {
            get
            {
                if (_radial == null) _radial = CreateRadialTexture();
                return _radial;
            }
        }

        /// <summary>取预设配色：0 主色、1 副色、2 点缀色，均为 HDR。</summary>
        public static Color[] GetPalette(StagePalettePreset preset)
        {
            switch (preset)
            {
                case StagePalettePreset.BeatSaber:
                    return new Color[]
                    {
                        new Color(1.35f, 0.10f, 0.16f),
                        new Color(0.12f, 0.42f, 1.40f),
                        new Color(1.15f, 1.15f, 1.25f),
                    };
                case StagePalettePreset.SunsetClub:
                    return new Color[]
                    {
                        new Color(1.35f, 0.55f, 0.12f),
                        new Color(1.30f, 0.22f, 0.62f),
                        new Color(0.14f, 1.05f, 1.15f),
                    };
                case StagePalettePreset.MonoIce:
                    return new Color[]
                    {
                        new Color(0.55f, 0.95f, 1.30f),
                        new Color(0.18f, 0.38f, 1.15f),
                        new Color(0.90f, 1.05f, 1.25f),
                    };
                default:
                    return new Color[]
                    {
                        new Color(0.10f, 0.95f, 1.20f),
                        new Color(1.20f, 0.22f, 0.88f),
                        new Color(0.55f, 0.30f, 1.25f),
                    };
            }
        }

        /// <summary>给颜色乘一个亮度系数（保持色相，用于节拍明暗）。</summary>
        public static Color Scale(Color color, float k)
        {
            return new Color(color.r * k, color.g * k, color.b * k, color.a);
        }

        // ---------------- 材质 ----------------

        /// <summary>
        /// 创建发光材质。additive=true 时用加色混合并关闭深度写入与背面剔除，
        /// 适合光柱/激光/幕墙这类纯发光体。
        /// </summary>
        public static Material CreateGlowMaterial(Color hdrColor, bool additive, Texture2D map, string name)
        {
            Shader shader = Shader.Find(UnlitShaderName);
            if (shader == null) shader = Shader.Find("Unlit/Texture");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            Material mat = new Material(shader);
            mat.name = name;
            mat.hideFlags = HideFlags.DontSave;
            mat.SetColor("_BaseColor", hdrColor);
            if (map != null) mat.SetTexture("_BaseMap", map);

            if (additive)
            {
                mat.SetOverrideTag("RenderType", "Transparent");
                if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
                if (mat.HasProperty("_SrcBlend")) mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                if (mat.HasProperty("_DstBlend")) mat.SetInt("_DstBlend", (int)BlendMode.One);
                if (mat.HasProperty("_ZWrite")) mat.SetInt("_ZWrite", 0);
                if (mat.HasProperty("_Cull")) mat.SetInt("_Cull", (int)CullMode.Off);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = (int)RenderQueue.Transparent + 20;
            }
            else
            {
                mat.renderQueue = (int)RenderQueue.Geometry;
            }

            return mat;
        }

        /// <summary>修改发光材质的亮度（每帧节拍调用，不产生新材质）。</summary>
        public static void SetMaterialLevel(Material mat, Color hdrColor, float level)
        {
            if (mat == null) return;
            mat.SetColor("_BaseColor", Scale(hdrColor, level));
        }

        // ---------------- 贴图 ----------------

        static Texture2D CreateFadeUpTexture()
        {
            const int height = 128;
            Texture2D tex = new Texture2D(4, height, TextureFormat.RGBA32, false);
            tex.name = "NeonStage_FadeUp";
            tex.hideFlags = HideFlags.DontSave;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            for (int y = 0; y < height; y++)
            {
                float v = (float)y / (height - 1);
                // 根部保留一段全亮，之后按幂次衰减
                float a = Mathf.Clamp01(1f - v);
                a = Mathf.Pow(a, 1.6f);
                Color c = new Color(1f, 1f, 1f, a);
                for (int x = 0; x < 4; x++) tex.SetPixel(x, y, c);
            }

            tex.Apply();
            return tex;
        }

        static Texture2D _fadeBoth;

        /// <summary>水平两端渐隐贴图：中间亮、左右两端透明，用于弧形幕墙收边。</summary>
        public static Texture2D FadeBoth
        {
            get
            {
                if (_fadeBoth == null) _fadeBoth = CreateFadeBothTexture();
                return _fadeBoth;
            }
        }

        /// <summary>
        /// 创建半透明材质（普通 Alpha 混合，不是加色）。用于暗色地台、雾面遮挡这类"压光"元素。
        /// </summary>
        public static Material CreateAlphaMaterial(Color color, Texture2D map, string name)
        {
            Shader shader = Shader.Find(UnlitShaderName);
            if (shader == null) shader = Shader.Find("Unlit/Transparent");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            Material mat = new Material(shader);
            mat.name = name;
            mat.hideFlags = HideFlags.DontSave;
            mat.SetColor("_BaseColor", color);
            if (map != null) mat.SetTexture("_BaseMap", map);

            mat.SetOverrideTag("RenderType", "Transparent");
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_SrcBlend")) mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_ZWrite")) mat.SetInt("_ZWrite", 0);
            if (mat.HasProperty("_Cull")) mat.SetInt("_Cull", (int)CullMode.Off);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent - 10;
            return mat;
        }

        static Texture2D _softRect;

        /// <summary>软边矩形贴图：中间实、四周按 edge 比例柔和收边（对应 Beat Saber 的 RectangleFakeGlow）。</summary>
        public static Texture2D SoftRect
        {
            get
            {
                if (_softRect == null) _softRect = CreateSoftRectTexture(0.42f);
                return _softRect;
            }
        }

        static Texture2D CreateSoftRectTexture(float edge)
        {
            const int size = 128;
            edge = Mathf.Clamp(edge, 0.02f, 0.95f);
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.name = "NeonStage_SoftRect";
            tex.hideFlags = HideFlags.DontSave;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            for (int y = 0; y < size; y++)
            {
                float v = (float)y / (size - 1);
                float dv = Mathf.Min(v, 1f - v) / edge;
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / (size - 1);
                    float du = Mathf.Min(u, 1f - u) / edge;
                    float a = Mathf.Clamp01(Mathf.Min(du, dv));
                    a = a * a * (3f - 2f * a);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }

            tex.Apply();
            return tex;
        }

        /// <summary>XY 平面朝向 +Z 的四边形，UV 0..1，用于软边辉光片。</summary>
        public static Mesh CreateQuad(float width, float height)
        {
            Mesh mesh = NewMesh("NeonStage_Quad");
            float hw = width * 0.5f;
            float hh = height * 0.5f;
            mesh.vertices = new Vector3[]
            {
                new Vector3(-hw, -hh, 0f),
                new Vector3(hw, -hh, 0f),
                new Vector3(hw, hh, 0f),
                new Vector3(-hw, hh, 0f),
            };
            mesh.uv = new Vector2[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f),
            };
            mesh.triangles = new int[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateBounds();
            return mesh;
        }

        static Texture2D _fadeHorizon;

        /// <summary>地平线光幕贴图：底部最亮向上渐隐，同时左右两端收边，用于弧形幕墙。</summary>
        public static Texture2D FadeHorizon
        {
            get
            {
                if (_fadeHorizon == null) _fadeHorizon = CreateFadeHorizonTexture();
                return _fadeHorizon;
            }
        }

        static Texture2D CreateFadeHorizonTexture()
        {
            const int width = 256;
            const int height = 64;
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.name = "NeonStage_FadeHorizon";
            tex.hideFlags = HideFlags.DontSave;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            for (int y = 0; y < height; y++)
            {
                float v = (float)y / (height - 1);
                float vertical = Mathf.Pow(1f - v, 1.5f);
                for (int x = 0; x < width; x++)
                {
                    float u = (float)x / (width - 1);
                    float edge = Mathf.Pow(Mathf.Clamp01(Mathf.Min(u, 1f - u) * 2f), 0.7f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, vertical * edge));
                }
            }

            tex.Apply();
            return tex;
        }

        static Texture2D CreateFadeBothTexture()
        {
            const int width = 256;
            Texture2D tex = new Texture2D(width, 4, TextureFormat.RGBA32, false);
            tex.name = "NeonStage_FadeBoth";
            tex.hideFlags = HideFlags.DontSave;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            for (int x = 0; x < width; x++)
            {
                float u = (float)x / (width - 1);
                float edge = Mathf.Min(u, 1f - u) * 2f;
                float a = Mathf.Pow(Mathf.Clamp01(edge), 0.65f);
                Color c = new Color(1f, 1f, 1f, a);
                for (int y = 0; y < 4; y++) tex.SetPixel(x, y, c);
            }

            tex.Apply();
            return tex;
        }

        /// <summary>水平圆环（XZ 平面，法线朝上），用于地台边缘光带。</summary>
        public static Mesh CreateRing(float radius, float width, int segments)
        {
            segments = Mathf.Max(8, segments);
            Mesh mesh = NewMesh("NeonStage_Ring");

            float inner = Mathf.Max(0.001f, radius - width * 0.5f);
            float outer = radius + width * 0.5f;
            Vector3[] vertices = new Vector3[(segments + 1) * 2];
            Vector2[] uvs = new Vector2[(segments + 1) * 2];

            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                float angle = t * Mathf.PI * 2f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);
                vertices[i * 2] = new Vector3(cos * inner, 0f, sin * inner);
                vertices[i * 2 + 1] = new Vector3(cos * outer, 0f, sin * outer);
                uvs[i * 2] = new Vector2(t, 0f);
                uvs[i * 2 + 1] = new Vector2(t, 1f);
            }

            int[] triangles = new int[segments * 6];
            for (int i = 0; i < segments; i++)
            {
                int a = i * 2;
                int b = i * 2 + 1;
                int c = (i + 1) * 2;
                int d = (i + 1) * 2 + 1;
                triangles[i * 6] = a;
                triangles[i * 6 + 1] = c;
                triangles[i * 6 + 2] = b;
                triangles[i * 6 + 3] = b;
                triangles[i * 6 + 4] = c;
                triangles[i * 6 + 5] = d;
            }

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        static Texture2D CreateRadialTexture()
        {
            const int size = 128;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.name = "NeonStage_Radial";
            tex.hideFlags = HideFlags.DontSave;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - half) / half;
                    float dy = (y + 0.5f - half) / half;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - r);
                    a = Mathf.Pow(a, 2.0f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }

            tex.Apply();
            return tex;
        }

        // ---------------- 几何 ----------------

        static Mesh NewMesh(string name)
        {
            Mesh mesh = new Mesh();
            mesh.name = name;
            mesh.hideFlags = HideFlags.DontSave;
            return mesh;
        }

        /// <summary>
        /// 棱柱：底部宽度 baseWidth、顶部宽度 topWidth、高 height。UV.y 从 0（底）到 1（顶），
        /// 配合 FadeUp 贴图即可得到“根部亮、尖端渐隐”的光柱或激光。
        /// </summary>
        public static Mesh CreateColumn(float baseWidth, float topWidth, float height, int sides)
        {
            sides = Mathf.Max(3, sides);
            Mesh mesh = NewMesh("NeonStage_Column");

            Vector3[] vertices = new Vector3[sides * 2];
            Vector2[] uvs = new Vector2[sides * 2];

            for (int i = 0; i < sides; i++)
            {
                float t = (float)i / sides;
                float angle = t * Mathf.PI * 2f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                vertices[i * 2] = new Vector3(cos * baseWidth * 0.5f, 0f, sin * baseWidth * 0.5f);
                vertices[i * 2 + 1] = new Vector3(cos * topWidth * 0.5f, height, sin * topWidth * 0.5f);
                uvs[i * 2] = new Vector2(t, 0f);
                uvs[i * 2 + 1] = new Vector2(t, 1f);
            }

            // 首尾共享顶点（不复制接缝环），否则加色混合会在接缝处叠两遍 → 一条刺眼亮线
            int[] triangles = new int[sides * 6];
            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;
                int b0 = i * 2;
                int t0 = i * 2 + 1;
                int b1 = next * 2;
                int t1 = next * 2 + 1;

                triangles[i * 6] = b0;
                triangles[i * 6 + 1] = t0;
                triangles[i * 6 + 2] = b1;
                triangles[i * 6 + 3] = b1;
                triangles[i * 6 + 4] = t0;
                triangles[i * 6 + 5] = t1;
            }

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>水平四边形（XZ 平面，y 由参数给定），UV.y 沿长度方向，用于地面光带。</summary>
        public static Mesh CreateFloorStrip(float width, float length, float y)
        {
            Mesh mesh = NewMesh("NeonStage_FloorStrip");
            float hw = width * 0.5f;

            mesh.vertices = new Vector3[]
            {
                new Vector3(-hw, y, 0f),
                new Vector3(hw, y, 0f),
                new Vector3(hw, y, length),
                new Vector3(-hw, y, length),
            };
            mesh.uv = new Vector2[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f),
            };
            mesh.triangles = new int[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>圆盘（XY 平面，朝向 +Z），UV 以中心为 0.5，配合 Radial 贴图当光斑。</summary>
        public static Mesh CreateDisc(float radius, int segments)
        {
            segments = Mathf.Max(8, segments);
            Mesh mesh = NewMesh("NeonStage_Disc");

            Vector3[] vertices = new Vector3[segments + 1];
            Vector2[] uvs = new Vector2[segments + 1];
            vertices[0] = Vector3.zero;
            uvs[0] = new Vector2(0.5f, 0.5f);

            for (int i = 0; i < segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);
                vertices[i + 1] = new Vector3(cos * radius, sin * radius, 0f);
                uvs[i + 1] = new Vector2(0.5f + cos * 0.5f, 0.5f + sin * 0.5f);
            }

            int[] triangles = new int[segments * 3];
            for (int i = 0; i < segments; i++)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = (i + 1) % segments + 1;
                triangles[i * 3 + 2] = i + 1;
            }

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>圆环体（站在 XY 平面，圆环轴向为 Z，正对玩家），用于悬浮光环。</summary>
        public static Mesh CreateTorus(float majorRadius, float tubeRadius, int majorSegments, int minorSegments)
        {
            majorSegments = Mathf.Max(8, majorSegments);
            minorSegments = Mathf.Max(4, minorSegments);
            Mesh mesh = NewMesh("NeonStage_Torus");

            int count = (majorSegments + 1) * (minorSegments + 1);
            Vector3[] vertices = new Vector3[count];
            Vector2[] uvs = new Vector2[count];

            for (int i = 0; i <= majorSegments; i++)
            {
                float u = (float)i / majorSegments;
                float majorAngle = u * Mathf.PI * 2f;
                float cosMajor = Mathf.Cos(majorAngle);
                float sinMajor = Mathf.Sin(majorAngle);

                for (int j = 0; j <= minorSegments; j++)
                {
                    float v = (float)j / minorSegments;
                    float minorAngle = v * Mathf.PI * 2f;
                    float cosMinor = Mathf.Cos(minorAngle);
                    float sinMinor = Mathf.Sin(minorAngle);

                    float radius = majorRadius + tubeRadius * cosMinor;
                    int index = i * (minorSegments + 1) + j;
                    vertices[index] = new Vector3(cosMajor * radius, sinMajor * radius, tubeRadius * sinMinor);
                    uvs[index] = new Vector2(u, v);
                }
            }

            int[] triangles = new int[majorSegments * minorSegments * 6];
            int cursor = 0;
            for (int i = 0; i < majorSegments; i++)
            {
                for (int j = 0; j < minorSegments; j++)
                {
                    int a = i * (minorSegments + 1) + j;
                    int b = (i + 1) * (minorSegments + 1) + j;
                    int c = (i + 1) * (minorSegments + 1) + j + 1;
                    int d = i * (minorSegments + 1) + j + 1;

                    triangles[cursor++] = a;
                    triangles[cursor++] = b;
                    triangles[cursor++] = c;
                    triangles[cursor++] = a;
                    triangles[cursor++] = c;
                    triangles[cursor++] = d;
                }
            }

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>弧形幕墙（圆心在原点、朝 +Z 张开），UV.y 从底 0 到顶 1。</summary>
        public static Mesh CreateArcWall(float radius, float height, float arcDegrees, int segments, float y)
        {
            segments = Mathf.Max(2, segments);
            Mesh mesh = NewMesh("NeonStage_ArcWall");

            int ringCount = segments + 1;
            Vector3[] vertices = new Vector3[ringCount * 2];
            Vector2[] uvs = new Vector2[ringCount * 2];

            for (int i = 0; i < ringCount; i++)
            {
                float t = (float)i / segments;
                float angle = Mathf.Lerp(-arcDegrees * 0.5f, arcDegrees * 0.5f, t) * Mathf.Deg2Rad;
                float x = Mathf.Sin(angle) * radius;
                float z = Mathf.Cos(angle) * radius;

                vertices[i * 2] = new Vector3(x, y, z);
                vertices[i * 2 + 1] = new Vector3(x, y + height, z);
                uvs[i * 2] = new Vector2(t, 0f);
                uvs[i * 2 + 1] = new Vector2(t, 1f);
            }

            int[] triangles = new int[segments * 6];
            for (int i = 0; i < segments; i++)
            {
                int b0 = i * 2;
                int t0 = i * 2 + 1;
                int b1 = (i + 1) * 2;
                int t1 = (i + 1) * 2 + 1;

                triangles[i * 6] = b0;
                triangles[i * 6 + 1] = t0;
                triangles[i * 6 + 2] = b1;
                triangles[i * 6 + 3] = b1;
                triangles[i * 6 + 4] = t0;
                triangles[i * 6 + 5] = t1;
            }

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>地面网格（XZ 平面合并成一个网格，含横竖两组光线）。</summary>
        public static Mesh CreateFlatGrid(float sizeX, float sizeZ, float step, float lineWidth, float y)
        {
            step = Mathf.Max(0.05f, step);
            lineWidth = Mathf.Max(0.002f, lineWidth);

            int countX = Mathf.FloorToInt(sizeX / step) + 1;
            int countZ = Mathf.FloorToInt(sizeZ / step) + 1;
            int lineCount = countX + countZ;

            Vector3[] vertices = new Vector3[lineCount * 4];
            Vector2[] uvs = new Vector2[lineCount * 4];
            int[] triangles = new int[lineCount * 6];
            int cursor = 0;
            int v = 0;

            float halfX = sizeX * 0.5f;
            float halfZ = sizeZ * 0.5f;
            float hw = lineWidth * 0.5f;

            for (int i = 0; i < countX; i++)
            {
                float x = -halfX + i * step;
                float z0 = -halfZ;
                vertices[v] = new Vector3(x - hw, y, z0);
                vertices[v + 1] = new Vector3(x + hw, y, z0);
                vertices[v + 2] = new Vector3(x + hw, y, halfZ);
                vertices[v + 3] = new Vector3(x - hw, y, halfZ);
                uvs[v] = new Vector2(0f, 0f);
                uvs[v + 1] = new Vector2(1f, 0f);
                uvs[v + 2] = new Vector2(1f, 1f);
                uvs[v + 3] = new Vector2(0f, 1f);
                v += 4;

                triangles[cursor++] = v - 4;
                triangles[cursor++] = v - 2;
                triangles[cursor++] = v - 3;
                triangles[cursor++] = v - 4;
                triangles[cursor++] = v - 1;
                triangles[cursor++] = v - 2;
            }

            for (int i = 0; i < countZ; i++)
            {
                float z = -halfZ + i * step;
                vertices[v] = new Vector3(-halfX, y, z - hw);
                vertices[v + 1] = new Vector3(halfX, y, z - hw);
                vertices[v + 2] = new Vector3(halfX, y, z + hw);
                vertices[v + 3] = new Vector3(-halfX, y, z + hw);
                uvs[v] = new Vector2(0f, 0f);
                uvs[v + 1] = new Vector2(1f, 0f);
                uvs[v + 2] = new Vector2(1f, 1f);
                uvs[v + 3] = new Vector2(0f, 1f);
                v += 4;

                triangles[cursor++] = v - 4;
                triangles[cursor++] = v - 2;
                triangles[cursor++] = v - 3;
                triangles[cursor++] = v - 4;
                triangles[cursor++] = v - 1;
                triangles[cursor++] = v - 2;
            }

            Mesh mesh = NewMesh("NeonStage_Grid");
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
