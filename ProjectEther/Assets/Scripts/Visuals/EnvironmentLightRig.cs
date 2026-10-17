using System.Collections.Generic;
using UnityEngine;

namespace OsuVR
{
    /// <summary>
    /// Beat Saber 风格的舞台结构层。几何与配比直接照 Beat Saber 的 Big Mirror 环境实测结构：
    /// - 背后两根极细的灯柱（BoxLight 0.10 × 50m）——竖向撑起"高"；
    /// - 远处一个直径几十米的灯环（BS 里在 150m 外）——撑起"远"；
    /// - 左右两侧暗色建筑体块——撑起"宽"；
    /// - 玩家脚下的假辉光椭圆（RectangleFakeGlow）。
    /// 所有灯管都是细长立方体而不是贴片，任何角度都不穿帮；灯与建筑各自合并成一张网格，
    /// 整层 4~5 个渲染器（参考 BS 的灯管批量填充思路）。
    /// 由 StageLightingSystem 创建、驱动与释放。
    /// </summary>
    public class EnvironmentLightRig
    {
        Material _dark;
        Material _light;
        Material _halo;
        Material _groupA;
        Material _groupB;

        Mesh _archMesh;
        Mesh _lightMesh;
        Mesh _haloMesh;
        Mesh _groupAMesh;
        Mesh _groupBMesh;

        Transform _root;

        public bool IsBuilt { get { return _root != null; } }

        public void Build(
            Transform parent,
            int ringSegments,
            float buildingOffset,
            float columnHeight,
            float ringRadius,
            float width,
            Color colorA,
            Color colorB
        )
        {
            if (parent == null || IsBuilt) return;

            GameObject rootGo = new GameObject("EnvironmentStage");
            rootGo.transform.SetParent(parent, false);
            _root = rootGo.transform;

            Color hotA = Color.Lerp(colorA, Color.white, 0.32f);
            Color hotB = Color.Lerp(colorB, Color.white, 0.32f);

            _dark = NeonStageKit.CreateAlphaMaterial(new Color(0.012f, 0.008f, 0.022f, 0.97f), null, "EnvStageDark");
            _light = NeonStageKit.CreateGlowMaterial(hotA, true, null, "EnvStageLight");
            _halo = NeonStageKit.CreateGlowMaterial(colorA, true, NeonStageKit.SoftRect, "EnvStageHalo");
            _groupA = NeonStageKit.CreateGlowMaterial(hotA, true, null, "EnvStageGroupA");
            _groupB = NeonStageKit.CreateGlowMaterial(hotB, true, null, "EnvStageGroupB");

            Mesh cube = NeonStageKit.CreateColumn(1f, 1f, 1f, 4);   // 单位立方体，靠矩阵缩放成各种灯管/体块

            // 1) 暗色建筑：左右两块大体量 + 脚下平台圆盘（合并成一张网格）
            MeshBuilder arch = new MeshBuilder();
            float bh = columnHeight * 0.55f;
            arch.Add(cube, Matrix4x4.TRS(new Vector3(-buildingOffset, bh * 0.5f, -buildingOffset * 0.7f), Quaternion.identity, new Vector3(buildingOffset * 0.9f, bh, buildingOffset * 1.8f)));
            arch.Add(cube, Matrix4x4.TRS(new Vector3(buildingOffset, bh * 0.5f, -buildingOffset * 0.7f), Quaternion.identity, new Vector3(buildingOffset * 0.9f, bh, buildingOffset * 1.8f)));
            // 照 BS 的 MoveBackWall：玩家背后一块 6m 宽 × 3m 高的暗墙
            arch.Add(cube, Matrix4x4.TRS(new Vector3(0f, 1.5f, -1.4f), Quaternion.identity, new Vector3(6f, 3f, 0.14f)));
            // 近处不放平台：地面交给 EtherealEnvironment 自己的镜面地板
            _archMesh = arch.Build("EnvStageArch");

            // 2) 结构灯：背后两根 50m 细灯柱 + 远处灯环 + 脚下假辉光椭圆
            MeshBuilder light = new MeshBuilder();
            Vector3 columnScale = new Vector3(width * 1.6f, columnHeight, width * 1.6f);
            light.Add(cube, Matrix4x4.TRS(new Vector3(-5.6f, columnHeight * 0.5f, 3.6f), Quaternion.identity, columnScale));
            light.Add(cube, Matrix4x4.TRS(new Vector3(5.6f, columnHeight * 0.5f, 3.6f), Quaternion.identity, columnScale));

            int segments = Mathf.Max(0, ringSegments);
            float segLength = Mathf.PI * 2f * ringRadius / Mathf.Max(1, segments) * 0.55f;
            for (int i = 0; i < segments; i++)
            {
                float a = Mathf.PI * 2f * i / Mathf.Max(1, segments);
                Vector3 pos = new Vector3(Mathf.Sin(a) * ringRadius, columnHeight * 0.20f, -ringRadius + Mathf.Cos(a) * ringRadius * 0.25f);
                Quaternion rot = Quaternion.Euler(0f, a * Mathf.Rad2Deg + 90f, 0f);
                light.Add(cube, Matrix4x4.TRS(pos, rot, new Vector3(segLength, width * 2.2f, width * 2.2f)));
            }
            _lightMesh = light.Build("EnvStageLight");

            // 3) 假辉光：灯柱背后的软边片 + 脚下椭圆
            MeshBuilder halo = new MeshBuilder();
            Mesh haloQuad = NeonStageKit.CreateQuad(width * 42f, columnHeight * 0.98f);
            halo.Add(haloQuad, Matrix4x4.TRS(new Vector3(-5.6f, columnHeight * 0.5f, 3.4f), Quaternion.identity, Vector3.one));
            halo.Add(haloQuad, Matrix4x4.TRS(new Vector3(5.6f, columnHeight * 0.5f, 3.4f), Quaternion.identity, Vector3.one));
            halo.Add(
                NeonStageKit.CreateDisc(2.4f, 48),
                Matrix4x4.TRS(new Vector3(0f, 0.015f, 1.2f), Quaternion.Euler(-90f, 0f, 0f), Vector3.one)
            );
            _haloMesh = halo.Build("EnvStageHalo");

            // 4) 两组灯：左右灯柱各一组，随拍交替
            MeshBuilder groupA = new MeshBuilder();
            MeshBuilder groupB = new MeshBuilder();
            groupA.Add(cube, Matrix4x4.TRS(new Vector3(-buildingOffset * 0.52f, columnHeight * 0.14f, buildingOffset * 0.3f), Quaternion.identity, new Vector3(width * 1.2f, columnHeight * 0.28f, width * 1.2f)));
            groupB.Add(cube, Matrix4x4.TRS(new Vector3(buildingOffset * 0.52f, columnHeight * 0.14f, buildingOffset * 0.3f), Quaternion.identity, new Vector3(width * 1.2f, columnHeight * 0.28f, width * 1.2f)));
            _groupAMesh = groupA.Build("EnvStageGroupA");
            _groupBMesh = groupB.Build("EnvStageGroupB");

            AddRenderer("StageArch", _archMesh, _dark);
            AddRenderer("StageLight", _lightMesh, _light);
            AddRenderer("StageHalo", _haloMesh, _halo);
            AddRenderer("StageGroupA", _groupAMesh, _groupA);
            AddRenderer("StageGroupB", _groupBMesh, _groupB);
        }

        /// <summary>四路亮度分别对应：灯柱与车道线 / 假辉光 / 左组灯 / 右组灯。</summary>
        public void Apply(float light, float halo, float groupA, float groupB, Color colorA, Color colorB)
        {
            if (!IsBuilt) return;

            Color hotA = Color.Lerp(colorA, Color.white, 0.32f);
            Color hotB = Color.Lerp(colorB, Color.white, 0.32f);

            NeonStageKit.SetMaterialLevel(_light, hotA, Mathf.Max(0f, light));
            NeonStageKit.SetMaterialLevel(_halo, colorA, Mathf.Max(0f, halo));
            NeonStageKit.SetMaterialLevel(_groupA, hotA, Mathf.Max(0f, groupA));
            NeonStageKit.SetMaterialLevel(_groupB, hotB, Mathf.Max(0f, groupB));
        }

        void AddRenderer(string name, Mesh mesh, Material material)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(_root, false);

            MeshFilter filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        }

        public void Dispose()
        {
            DestroyMaterial(ref _dark);
            DestroyMaterial(ref _light);
            DestroyMaterial(ref _halo);
            DestroyMaterial(ref _groupA);
            DestroyMaterial(ref _groupB);
            DestroyMesh(ref _archMesh);
            DestroyMesh(ref _lightMesh);
            DestroyMesh(ref _haloMesh);
            DestroyMesh(ref _groupAMesh);
            DestroyMesh(ref _groupBMesh);

            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root.gameObject);
                _root = null;
            }
        }

        static void DestroyMaterial(ref Material material)
        {
            if (material != null) UnityEngine.Object.Destroy(material);
            material = null;
        }

        static void DestroyMesh(ref Mesh mesh)
        {
            if (mesh != null) UnityEngine.Object.Destroy(mesh);
            mesh = null;
        }

        class MeshBuilder
        {
            readonly List<Vector3> _vertices = new List<Vector3>();
            readonly List<Vector2> _uvs = new List<Vector2>();
            readonly List<int> _triangles = new List<int>();

            public void Add(Mesh source, Matrix4x4 matrix)
            {
                if (source == null) return;

                int offset = _vertices.Count;
                Vector3[] verts = source.vertices;
                Vector2[] uvs = source.uv;
                int[] tris = source.triangles;

                for (int i = 0; i < verts.Length; i++)
                {
                    _vertices.Add(matrix.MultiplyPoint3x4(verts[i]));
                    _uvs.Add(uvs != null && i < uvs.Length ? uvs[i] : Vector2.zero);
                }
                for (int i = 0; i < tris.Length; i++)
                {
                    _triangles.Add(tris[i] + offset);
                }
            }

            public Mesh Build(string name)
            {
                Mesh mesh = new Mesh();
                mesh.name = name;
                mesh.hideFlags = HideFlags.DontSave;
                mesh.SetVertices(_vertices);
                mesh.SetUVs(0, _uvs);
                mesh.SetTriangles(_triangles, 0);
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
