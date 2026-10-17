using System.Collections.Generic;
using UnityEngine;

namespace OsuVR
{
    /// <summary>
    /// 变体一：光柱环。一圈高低起伏的发光光柱围在玩家前方，交替色随节拍互换，
    /// 每根柱后叠一层更宽更暗的体积光，外圈再加一排矮柱与顶部光环做层次。
    /// </summary>
    [ExecuteAlways]
    public class NeonPillarStage : StageVariantBase
    {
        [Header("光柱环")]
        [Range(4, 40)] public int pillarCount = 12;
        public float pillarHeight = 3.4f;
        [Tooltip("高度起伏幅度：0 = 全部等高")]
        [Range(0f, 0.6f)] public float heightVariation = 0.32f;
        public float pillarBaseWidth = 0.16f;
        public float pillarTopWidth = 0.30f;
        [Range(-90f, 90f)] public float startYaw = -78f;
        [Range(-90f, 90f)] public float endYaw = 78f;
        public float ringRadius = 6.0f;
        [Tooltip("整圈环绕速度（度/秒）")]
        public float ringYawSpeed = 1.2f;

        [Header("体积光")]
        public bool showHalo = true;
        public float haloWidthScale = 1.7f;
        public float haloLevelScale = 0.22f;

        [Header("外圈")]
        public bool showOuterRing = true;
        [Range(2, 20)] public int outerRingCount = 10;
        public float outerRingRadius = 11.5f;
        public float outerRingHeight = 1.6f;

        [Header("顶部光环")]
        public bool showTopBand = true;
        public float topBandThickness = 0.06f;

        Material _matGlowA;
        Material _matGlowB;
        Material _matBeamA;
        Material _matBeamB;
        Material _matHaloA;
        Material _matHaloB;
        Material _matBaseRing;
        Material _matTopBand;
        Material _matPlatformEdge;
        Transform _ring;
        Transform _outerRing;

        protected override void Build()
        {
            _matBeamA = NewGlow(ColorA, true, NeonStageKit.FadeUp, "PillarBeamA");
            _matBeamB = NewGlow(ColorB, true, NeonStageKit.FadeUp, "PillarBeamB");
            _matHaloA = NewGlow(ColorA, true, NeonStageKit.FadeUp, "PillarHaloA");
            _matHaloB = NewGlow(ColorB, true, NeonStageKit.FadeUp, "PillarHaloB");
            _matGlowA = NewHaloMaterial(ColorA, "PillarGlowA");
            _matGlowB = NewHaloMaterial(ColorB, "PillarGlowB");
            _matBaseRing = NewGlow(ColorC, true, null, "PillarBaseRing");
            _matTopBand = NewGlow(ColorC, true, null, "PillarTopBand");

            BuildPlatform();

            _ring = AddEmpty("PillarRing", Vector3.zero, Quaternion.identity).transform;

            Mesh beamMesh = NewMesh(NeonStageKit.CreateColumn(pillarBaseWidth, pillarTopWidth, pillarHeight, 4));
            Mesh haloMesh = NewMesh(
                NeonStageKit.CreateColumn(pillarBaseWidth * haloWidthScale, pillarTopWidth * haloWidthScale, pillarHeight * 1.06f, 4)
            );

            for (int i = 0; i < pillarCount; i++)
            {
                float t = pillarCount > 1 ? (float)i / (pillarCount - 1) : 0.5f;
                float yaw = Mathf.Lerp(startYaw, endYaw, t);
                Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
                Vector3 position = rotation * new Vector3(0f, 0f, ringRadius);
                bool even = i % 2 == 0;

                // 高度起伏，避免看起来像一排等高的栅栏
                float wave = Mathf.Sin(t * Mathf.PI * 3.1f) * heightVariation;
                float scaleY = Mathf.Max(0.25f, 1f + wave);
                Transform pillar = AddMesh(
                    "Pillar_" + i,
                    beamMesh,
                    even ? _matBeamA : _matBeamB,
                    position,
                    Quaternion.identity,
                    _ring
                ).transform;
                pillar.localScale = new Vector3(1f, scaleY, 1f);

                AddHalo(
                    "PillarGlow_" + i,
                    Mathf.Max(0.8f, pillarTopWidth * 9f),
                    pillarHeight * 1.12f * scaleY,
                    even ? _matGlowA : _matGlowB,
                    position,
                    _ring
                );

                if (showHalo)
                {
                    Transform halo = AddMesh(
                        "PillarHalo_" + i,
                        haloMesh,
                        even ? _matHaloA : _matHaloB,
                        position,
                        Quaternion.identity,
                        _ring
                    ).transform;
                    halo.localScale = new Vector3(1f, scaleY, 1f);
                }
            }

            AddMesh(
                "BaseRing",
                NewMesh(NeonStageKit.CreateRing(ringRadius, 0.8f, 96)),
                _matBaseRing,
                new Vector3(0f, 0.02f, 0f),
                Quaternion.identity,
                _ring
            );

            if (showTopBand)
            {
                AddMesh(
                    "TopBand",
                    NewMesh(NeonStageKit.CreateTorus(ringRadius, topBandThickness, 64, 6)),
                    _matTopBand,
                    new Vector3(0f, pillarHeight * 0.94f, 0f),
                    Quaternion.identity,
                    _ring
                );
            }

            if (showOuterRing && outerRingCount > 0)
            {
                _outerRing = AddEmpty("OuterRing", Vector3.zero, Quaternion.identity).transform;
                Mesh outerMesh = NewMesh(NeonStageKit.CreateColumn(0.14f, 0.42f, outerRingHeight, 4));
                for (int i = 0; i < outerRingCount; i++)
                {
                    float yaw = 360f / outerRingCount * i + 18f;
                    Vector3 position = Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, 0f, outerRingRadius);
                    AddMesh(
                        "OuterPillar_" + i,
                        outerMesh,
                        i % 2 == 0 ? _matBeamB : _matBeamA,
                        position,
                        Quaternion.identity,
                        _outerRing
                    );
                }
            }
        }

        protected override void ApplyBeat(float beat, float low, float high, long beatIndex)
        {
            bool alt = beatIndex % 2 == 1;
            float hot = idleLevel + beat * beatGain;
            float cool = idleLevel + beat * beatGain * 0.25f;
            float levelA = alt ? cool : hot;
            float levelB = alt ? hot : cool;

            NeonStageKit.SetMaterialLevel(_matGlowA, GlowA, levelA * 0.30f);
            NeonStageKit.SetMaterialLevel(_matGlowB, GlowB, levelB * 0.30f);
            NeonStageKit.SetMaterialLevel(_matBeamA, GlowA, levelA);
            NeonStageKit.SetMaterialLevel(_matBeamB, GlowB, levelB);
            NeonStageKit.SetMaterialLevel(_matHaloA, GlowA, levelA * haloLevelScale);
            NeonStageKit.SetMaterialLevel(_matHaloB, GlowB, levelB * haloLevelScale);
            NeonStageKit.SetMaterialLevel(_matBaseRing, GlowC, idleLevel * 0.4f + low * accentGain * 0.3f);
            NeonStageKit.SetMaterialLevel(_matTopBand, GlowC, idleLevel * 0.3f + low * 0.25f);

            float now = Time.realtimeSinceStartup;
            if (_ring != null && ringYawSpeed != 0f)
            {
                _ring.localRotation = Quaternion.Euler(0f, now * ringYawSpeed, 0f);
            }
            if (_outerRing != null)
            {
                _outerRing.localRotation = Quaternion.Euler(0f, -now * ringYawSpeed * 0.6f, 0f);
            }
        }
    }
}
