using System.Collections.Generic;
using UnityEngine;

namespace OsuVR
{
    /// <summary>
    /// 舞台变体基类：统一负责材质/网格/子物体的创建与释放、参数变化后的重建，
    /// 以及每帧把节拍数据交给子类做表现。带 [ExecuteAlways]，编辑器里改参数能直接看到效果。
    /// 生成的子物体带 DontSave 标记，不会写进场景文件，风格全部由这里的参数描述。
    /// </summary>
    [ExecuteAlways]
    public abstract class StageVariantBase : MonoBehaviour
    {
        [Header("配色")]
        public StagePalettePreset palettePreset = StagePalettePreset.EtherNeon;
        [Tooltip("勾上后使用下面的自定义颜色，忽略预设")]
        public bool overrideColors = false;
        public Color colorA = new Color(0.10f, 0.95f, 1.20f);
        public Color colorB = new Color(1.20f, 0.22f, 0.88f);
        public Color colorC = new Color(0.55f, 0.30f, 1.25f);

        [Header("节拍响应")]
        [Range(0f, 1f)] public float idleLevel = 0.30f;
        [Range(0f, 3f)] public float beatGain = 1.20f;
        [Range(0f, 3f)] public float accentGain = 0.80f;

        [Header("发光")]
        [Tooltip("HDR 亮度倍率：越高越容易压过 Bloom 阈值，辉光越明显")]
        [Range(0.5f, 8f)] public float glowScale = 1.35f;

        [Header("天际辉光")]
        [Tooltip("远处一层很暗的弧形辉光，把舞台和空灵背景连起来（不抢戏）")]
        public bool showHorizonGlow = true;
        public float horizonGlowRadius = 15f;
        public float horizonGlowHeight = 6.5f;
        [Range(60f, 340f)] public float horizonGlowArc = 220f;

        [Header("地台")]
        [Tooltip("铺一块暗色圆台。注意 EtherealEnvironment 已有镜面地板，同时打开会叠成一块黑楔子")]
        public bool showPlatform = false;
        public float platformRadius = 14f;
        public float platformDepth = 0.55f;

        readonly List<Material> _materials = new List<Material>();
        readonly List<Mesh> _meshes = new List<Mesh>();
        readonly List<GameObject> _generated = new List<GameObject>();

        bool _dirty = true;
        bool _built;
        StageBeatDriver _driver;
        Material _matHorizonGlow;

        protected StageBeatDriver Driver
        {
            get
            {
                if (_driver == null) _driver = GetComponentInParent<StageBeatDriver>();
                return _driver;
            }
        }

        protected Color ColorA { get { return overrideColors ? colorA : NeonStageKit.GetPalette(palettePreset)[0]; } }
        protected Color ColorB { get { return overrideColors ? colorB : NeonStageKit.GetPalette(palettePreset)[1]; } }
        protected Color ColorC { get { return overrideColors ? colorC : NeonStageKit.GetPalette(palettePreset)[2]; } }

        /// <summary>带发光倍率的颜色，材质创建与每帧调光都用这一组。</summary>
        protected Color GlowA { get { return NeonStageKit.Scale(ColorA, glowScale); } }
        protected Color GlowB { get { return NeonStageKit.Scale(ColorB, glowScale); } }
        protected Color GlowC { get { return NeonStageKit.Scale(ColorC, glowScale); } }

        protected virtual void OnEnable()
        {
            _dirty = true;
        }

        protected virtual void OnDisable()
        {
            ClearStage();
        }

        void OnValidate()
        {
            _dirty = true;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
#endif
        }

        void Update()
        {
            if (_dirty)
            {
                Rebuild();
                return;
            }

            if (!_built) return;

            StageBeatDriver driver = Driver;
            float beat = driver != null ? driver.Beat : 0.5f;
            float low = driver != null ? driver.Low : 0.5f;
            float high = driver != null ? driver.High : 0.25f;
            long beatIndex = driver != null ? driver.BeatIndex : 0L;
            ApplyBeat(beat, low, high, beatIndex);

            if (_matHorizonGlow != null)
            {
                NeonStageKit.SetMaterialLevel(_matHorizonGlow, GlowC, 0.22f + low * 0.22f + beat * 0.08f);
            }
        }

        /// <summary>重建整个舞台。Inspector 改参数后会自动调用，也可以手动调。</summary>
        public void Rebuild()
        {
            _dirty = false;
            ClearStage();
            BuildHorizonGlow();
            Build();
            _built = true;
        }

        void ClearStage()
        {
            for (int i = 0; i < _generated.Count; i++) DestroySafe(_generated[i]);
            _generated.Clear();
            for (int i = 0; i < _materials.Count; i++) DestroySafe(_materials[i]);
            _materials.Clear();
            for (int i = 0; i < _meshes.Count; i++) DestroySafe(_meshes[i]);
            _meshes.Clear();
            _built = false;
        }

        static void DestroySafe(Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Object.Destroy(obj);
            else Object.DestroyImmediate(obj);
        }

        /// <summary>生成本变体的几何表现。</summary>
        protected abstract void Build();

        /// <summary>每帧表现：beat/low/high 为 0..1，beatIndex 用于步进跳变与奇偶交替。</summary>
        protected abstract void ApplyBeat(float beat, float low, float high, long beatIndex);

        // ---------------- 构建辅助 ----------------

        protected Material NewGlow(Color color, bool additive = true, Texture2D map = null, string name = null)
        {
            Material mat = NeonStageKit.CreateGlowMaterial(NeonStageKit.Scale(color, glowScale), additive, map, name ?? "NeonStage_Mat");
            _materials.Add(mat);
            return mat;
        }

        /// <summary>软边辉光片材质（Beat Saber 的 RectangleFakeGlow 思路：光晕靠几何，不靠把画面点亮）。</summary>
        protected Material NewHaloMaterial(Color color, string name)
        {
            return NewGlow(color, true, NeonStageKit.SoftRect, name);
        }

        /// <summary>放一张朝向玩家的软边辉光片。</summary>
        protected GameObject AddHalo(string name, float width, float height, Material material, Vector3 position, Transform parent = null)
        {
            Quaternion rotation = Quaternion.LookRotation(-position.normalized, Vector3.up);
            return AddMesh(name, NewMesh(NeonStageKit.CreateQuad(width, height)), material, position, rotation, parent);
        }

        protected Material NewAlpha(Color color, Texture2D map, string name)
        {
            Material mat = NeonStageKit.CreateAlphaMaterial(color, map, name);
            _materials.Add(mat);
            return mat;
        }

        /// <summary>铺一块暗色圆台 + 边缘光环，给舞台一个"站得住"的底面。</summary>
        protected void BuildHorizonGlow()
        {
            if (!showHorizonGlow) return;
            _matHorizonGlow = NewGlow(ColorC, true, NeonStageKit.FadeHorizon, "StageHorizonGlow");
            AddMesh(
                "HorizonGlow",
                NewMesh(NeonStageKit.CreateArcWall(horizonGlowRadius, horizonGlowHeight, horizonGlowArc, 64, 0f)),
                _matHorizonGlow,
                Vector3.zero,
                Quaternion.identity
            );
        }

        protected void BuildPlatform()
        {
            if (!showPlatform) return;

            Material disc = NewAlpha(new Color(0.012f, 0.008f, 0.022f, 0.96f), NeonStageKit.Radial, "StagePlatform");
            AddMesh("Platform", NewMesh(NeonStageKit.CreateDisc(platformRadius, 64)), disc, new Vector3(0f, 0.004f, 0f), Quaternion.Euler(-90f, 0f, 0f));

            Material edge = NewGlow(ColorC, true, null, "PlatformEdge");
            AddMesh(
                "PlatformEdge",
                NewMesh(NeonStageKit.CreateRing(platformRadius, 0.10f, 96)),
                edge,
                new Vector3(0f, 0.012f, 0f),
                Quaternion.identity
            );
        }

        protected Mesh NewMesh(Mesh mesh)
        {
            if (mesh != null) _meshes.Add(mesh);
            return mesh;
        }

        protected GameObject AddEmpty(string name, Vector3 localPosition, Quaternion localRotation, Transform parent = null)
        {
            GameObject go = new GameObject(name);
            go.hideFlags = Application.isPlaying ? HideFlags.None : HideFlags.DontSave;
            go.transform.SetParent(parent != null ? parent : transform, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation;
            _generated.Add(go);
            return go;
        }

        protected GameObject AddMesh(string name, Mesh mesh, Material material, Vector3 localPosition, Quaternion localRotation, Transform parent = null)
        {
            GameObject go = AddEmpty(name, localPosition, localRotation, parent);

            MeshFilter filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

            return go;
        }

        /// <summary>沿指定轴向摆一根方形光条（轴为长度方向，起点在 center - axis*length/2）。</summary>
        protected GameObject AddBar(string name, Mesh barMesh, Material material, Vector3 center, Vector3 axis, float length, float thickness, Transform parent = null)
        {
            Vector3 direction = axis.normalized;
            Quaternion rotation = Quaternion.FromToRotation(Vector3.up, direction);
            Vector3 origin = center - direction * (length * 0.5f);
            return AddMesh(name, barMesh, material, origin, rotation, parent);
        }
    }
}
