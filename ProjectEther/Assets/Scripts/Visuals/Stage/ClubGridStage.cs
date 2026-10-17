using System.Collections.Generic;
using UnityEngine;

namespace OsuVR
{
    /// <summary>
    /// 变体三：俱乐部网格。双层地面网格 + 远处环形地平线光幕 + 多层悬浮光环 + 天光柱，
    /// 偏 VRChat 展厅观感，泛光最重的一版。
    /// </summary>
    [ExecuteAlways]
    public class ClubGridStage : StageVariantBase
    {
        [Header("地面网格")]
        public float gridSize = 14f;
        public float gridStep = 1f;
        public float gridLineWidth = 0.02f;
        public bool showOuterGrid = true;
        public float outerGridSize = 26f;
        public float outerGridLineWidth = 0.03f;
        public bool showCeilingGrid = true;
        public float ceilingHeight = 6f;

        [Header("地平线光幕")]
        [Tooltip("地平线光幕（近处那一层）。默认关闭")]
        public bool showHorizon = false;
        public float horizonRadius = 15f;
        public float horizonHeight = 8f;
        [Range(60f, 340f)] public float horizonArc = 240f;
        public bool showSecondHorizon = false;
        public float secondHorizonRadius = 21f;
        public float secondHorizonHeight = 12f;

        [Header("悬浮光环")]
        [Range(0, 8)] public int ringCount = 0;
        public float ringThickness = 0.06f;
        public float ringDepth = 8.5f;
        public float ringBaseHeight = 3.0f;
        public float ringHeightSpan = 4.6f;
        [Range(0.3f, 1.2f)] public float ringInnerScale = 0.42f;
        [Range(1f, 1.8f)] public float ringOuterScale = 1.25f;
        public float ringSpinSpeed = 10f;

        [Header("天光柱")]
        public bool showSkyBeam = true;
        public float skyBeamRadius = 1.9f;
        public float skyBeamHeight = 12f;
        public float skyBeamZ = 15f;
        public float skyBeamBaseRadius = 4.5f;

        [Header("整体布局")]
        [Tooltip("光环的推远倍数")]
        public float ringScale = 5f;
        [Tooltip("天光柱的推远倍数")]
        public float beamScale = 2f;
        [Tooltip("其余元素（网格/地平线）的距离倍数，默认 1 保持不变")]
        public float layoutScale = 1f;
        [Tooltip("整体不透明度（乘到所有灯光亮度上）")]
        [Range(0.05f, 1f)] public float opacity = 0.45f;

        [Header("微尘")]
        public bool showSparkles = true;
        public int sparkleCount = 900;
        public float sparkleBaseRate = 60f;

        Material _matGrid;
        Material _matOuterGrid;
        Material _matHorizon;
        Material _matHorizon2;
        Material _matRing;
        Material _matSky;
        Material _matSkyBase;
        Material _matSkyGlow;
        readonly List<Transform> _rings = new List<Transform>();
        readonly List<float> _ringBaseY = new List<float>();
        ParticleSystem _sparkles;
        ParticleSystem.EmissionModule _sparkleEmission;
        float _sparkleRateBase;

        protected override void Build()
        {
            _matGrid = NewGlow(ColorC, true, null, "ClubGrid");
            _matOuterGrid = NewGlow(ColorB, true, null, "ClubGridOuter");
            _matHorizon = NewGlow(ColorA, true, NeonStageKit.FadeHorizon, "HorizonGlow");
            _matHorizon2 = NewGlow(ColorC, true, NeonStageKit.FadeHorizon, "HorizonGlowFar");
            _matRing = NewGlow(ColorB, true, NeonStageKit.FadeUp, "ClubRing");
            _matSky = NewGlow(ColorC, true, NeonStageKit.FadeUp, "SkyBeam");
            _matSkyBase = NewGlow(ColorC, true, NeonStageKit.Radial, "SkyBeamBase");
            _matSkyGlow = NewHaloMaterial(ColorC, "SkyBeamGlow");

            float s = Mathf.Max(1f, layoutScale);

            BuildPlatform();

            Mesh gridMesh = NewMesh(NeonStageKit.CreateFlatGrid(gridSize * s, gridSize * s, gridStep * s, gridLineWidth * s, 0.012f));
            AddMesh("FloorGrid", gridMesh, _matGrid, Vector3.zero, Quaternion.identity);

            if (showOuterGrid)
            {
                Mesh outerGrid = NewMesh(
                    NeonStageKit.CreateFlatGrid(outerGridSize * s, outerGridSize * s, gridStep * 2f * s, outerGridLineWidth * s, 0.01f)
                );
                AddMesh("FloorGridOuter", outerGrid, _matOuterGrid, Vector3.zero, Quaternion.identity);
            }

            if (showCeilingGrid)
            {
                AddMesh("CeilingGrid", gridMesh, _matGrid, new Vector3(0f, ceilingHeight * s, 0f), Quaternion.identity);
            }

            if (showHorizon)
            {
                AddMesh(
                    "HorizonGlow",
                    NewMesh(NeonStageKit.CreateArcWall(horizonRadius * s, horizonHeight * s, horizonArc, 64, 0f)),
                    _matHorizon,
                    Vector3.zero,
                    Quaternion.identity
                );
            }

            if (showSecondHorizon)
            {
                AddMesh(
                    "HorizonGlowFar",
                    NewMesh(NeonStageKit.CreateArcWall(secondHorizonRadius * s, secondHorizonHeight * s, horizonArc * 0.8f, 64, 0f)),
                    _matHorizon2,
                    Vector3.zero,
                    Quaternion.identity
                );
            }

            _rings.Clear();
            _ringBaseY.Clear();
            if (ringCount > 0)
            {
                for (int i = 0; i < ringCount; i++)
                {
                    float t = ringCount > 1 ? (float)i / (ringCount - 1) : 0.5f;
                    float scale = Mathf.Lerp(ringInnerScale, ringOuterScale, t);
                    float rs = Mathf.Max(1f, ringScale);
                    float radius = 5.6f * scale * s * rs;
                    Mesh ringMesh = NewMesh(NeonStageKit.CreateTorus(radius, ringThickness * s * rs, 56, 8));
                    float y = (ringBaseHeight + ringHeightSpan * t) * s * rs;
                    float z = (ringDepth + (t - 0.5f) * 3.5f) * s * rs;
                    Transform ring = AddMesh(
                        "ClubRing_" + i,
                        ringMesh,
                        _matRing,
                        new Vector3(0f, y, z),
                        Quaternion.Euler(0f, 0f, (i % 2 == 0 ? 1f : -1f) * 12f)
                    ).transform;
                    _rings.Add(ring);
                    _ringBaseY.Add(y);
                }
            }

            float bs = Mathf.Max(1f, beamScale);

            if (showSkyBeam)
            {
                AddMesh(
                    "SkyBeam",
                    NewMesh(NeonStageKit.CreateColumn(skyBeamRadius * s * bs, skyBeamRadius * 0.55f * s * bs, skyBeamHeight * s * bs, 16)),
                    _matSky,
                    new Vector3(0f, 0f, skyBeamZ * s * bs),
                    Quaternion.identity
                );

                AddHalo(
                    "SkyBeamGlow",
                    skyBeamRadius * 8f * s * bs,
                    skyBeamHeight * 1.05f * s * bs,
                    _matSkyGlow,
                    new Vector3(0f, skyBeamHeight * 0.5f * s * bs, skyBeamZ * s * bs)
                );

                AddMesh(
                    "SkyBeamBase",
                    NewMesh(NeonStageKit.CreateDisc(skyBeamBaseRadius * s * bs, 48)),
                    _matSkyBase,
                    new Vector3(0f, 0.02f * s, skyBeamZ * s * bs),
                    Quaternion.Euler(-90f, 0f, 0f)
                );
            }

            if (showSparkles)
            {
                GameObject go = AddEmpty("Sparkles", new Vector3(0f, 1.2f, 4f), Quaternion.identity);
                ParticleSystem ps = go.AddComponent<ParticleSystem>();

                var main = ps.main;
                main.loop = true;
                main.playOnAwake = true;
                main.maxParticles = Mathf.Max(64, sparkleCount);
                main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 12f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.3f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.1f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 1f, 0.8f));
                main.simulationSpace = ParticleSystemSimulationSpace.World;

                var shape = ps.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = new Vector3(20f, 7f, 20f);

                _sparkleEmission = ps.emission;
                _sparkleEmission.enabled = true;
                _sparkleRateBase = sparkleBaseRate;
                _sparkleEmission.rateOverTime = sparkleBaseRate;

                ParticleSystemRenderer psr = go.GetComponent<ParticleSystemRenderer>();
                psr.sharedMaterial = NewGlow(ColorC, true, NeonStageKit.Radial, "SparkleMat");
                psr.renderMode = ParticleSystemRenderMode.Billboard;
                psr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                psr.receiveShadows = false;
                _sparkles = ps;
            }
        }

        protected override void ApplyBeat(float beat, float low, float high, long beatIndex)
        {
            float now = Time.realtimeSinceStartup;
            float a = Mathf.Clamp01(opacity);   // 整体透明度：越低越淡

            NeonStageKit.SetMaterialLevel(_matGrid, GlowC, (idleLevel * 0.35f + low * beatGain * 0.25f + beat * 0.15f) * a);
            NeonStageKit.SetMaterialLevel(_matOuterGrid, GlowB, (idleLevel * 0.22f + low * 0.2f) * a);
            NeonStageKit.SetMaterialLevel(_matHorizon, GlowA, (idleLevel * 0.7f + beat * beatGain * 0.5f + low * 0.2f) * a);
            NeonStageKit.SetMaterialLevel(_matHorizon2, GlowC, (idleLevel * 0.3f + beat * beatGain * 0.2f) * a);
            NeonStageKit.SetMaterialLevel(_matRing, GlowB, (idleLevel * 0.55f + beat * beatGain * 0.55f) * a);
            NeonStageKit.SetMaterialLevel(_matSkyGlow, GlowC, (idleLevel * 0.35f + low * 0.2f) * a);
            NeonStageKit.SetMaterialLevel(_matSky, GlowC, (idleLevel * 0.3f + low * 0.15f) * a);
            NeonStageKit.SetMaterialLevel(_matSkyBase, GlowC, (idleLevel * 0.35f + beat * accentGain * 0.3f) * a);

            // 微尘服从设置里的画质预设（EtherealEnvironment 的粒子密度）
            if (_sparkles != null)
            {
                float density = 1f;
                if (OsuVR.EtherealEnvironment.Instance != null)
                    density = Mathf.Clamp(OsuVR.EtherealEnvironment.Instance.GetParticleDensity(), 0f, 2f);

                _sparkleEmission.rateOverTime = _sparkleRateBase * density * (0.55f + low * 0.9f);
            }

            for (int i = 0; i < _rings.Count; i++)
            {
                if (_rings[i] == null) continue;
                float spin = now * ringSpinSpeed * (i % 2 == 0 ? 1f : -1f);
                float bob = Mathf.Sin(now * 0.35f + i * 1.7f) * 0.18f;
                Vector3 p = _rings[i].localPosition;
                _rings[i].localRotation = Quaternion.Euler(0f, 0f, spin + (i % 2 == 0 ? 12f : -12f));
                _rings[i].localPosition = new Vector3(p.x, _ringBaseY[i] + bob, p.z);
            }
        }
    }
}
