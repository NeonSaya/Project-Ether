using System.Collections.Generic;
using UnityEngine;

namespace OsuVR
{
    /// <summary>
    /// 变体二：背后旋转激光 + 门框。左右各一组激光在身后缓慢旋转，每拍步进一格，
    /// 谱面平面外套一圈细门框、地面两条光带收拢，背景靠天际辉光垫底。
    /// </summary>
    [ExecuteAlways]
    public class LaserFanStage : StageVariantBase
    {
        [Header("旋转激光")]
        [Range(2, 14)] public int beamCount = 3;
        public float beamLength = 16f;
        public float beamWidth = 0.12f;
        public float pivotX = 6.5f;
        public float pivotY = 1.6f;
        public float pivotZ = 11f;
        [Tooltip("连续旋转速度（度/秒）")]
        public float spinSpeed = 9f;
        [Tooltip("每拍步进（度）")]
        public float beatStepDegrees = 5f;
        public float stepSnapSpeed = 8f;

        [Header("门框")]
        public bool showGateFrame = true;
        public float gateWidth = 2.05f;
        public float gateHeight = 1.5f;
        public float gateZ = 2.0f;
        public float gateBarThickness = 0.07f;

        [Header("副框（纵深）")]
        public bool showSecondFrame = false;
        public float secondFrameZ = 7.5f;
        public float secondFrameScale = 2.8f;

        [Header("地面光带")]
        public bool showFloorStrips = true;
        public float stripLength = 6.5f;
        public float stripWidth = 0.10f;
        public float stripOffset = 1.7f;

        Material _matGlowA;
        Material _matGlowB;
        Material _matGateGlow;
        Material _matBeamA;
        Material _matBeamB;
        Material _matGate;
        Material _matGateFar;
        Material _matStrip;
        readonly List<Transform> _pivots = new List<Transform>();
        readonly List<float> _baseAngles = new List<float>();

        float _stepTarget;
        float _stepCurrent;
        long _lastBeatIndex = -1;
        float _lastTime;

        protected override void Build()
        {
            _matBeamA = NewGlow(ColorA, true, NeonStageKit.FadeUp, "LaserBeamA");
            _matBeamB = NewGlow(ColorB, true, NeonStageKit.FadeUp, "LaserBeamB");
            _matGlowA = NewHaloMaterial(ColorA, "LaserGlowA");
            _matGlowB = NewHaloMaterial(ColorB, "LaserGlowB");
            _matGateGlow = NewHaloMaterial(ColorC, "GateGlow");
            _matGate = NewGlow(ColorC, true, null, "GateFrame");
            _matGateFar = NewGlow(ColorB, true, null, "GateFrameFar");
            _matStrip = NewGlow(ColorC, true, NeonStageKit.FadeUp, "FloorStrip");

            _pivots.Clear();
            _baseAngles.Clear();
            _stepTarget = 0f;
            _stepCurrent = 0f;
            _lastBeatIndex = -1;
            _lastTime = Time.realtimeSinceStartup;

            Mesh beamMesh = NewMesh(NeonStageKit.CreateColumn(beamWidth, beamWidth * 0.7f, beamLength, 4));

            for (int side = 0; side < 2; side++)
            {
                float x = side == 0 ? -pivotX : pivotX;
                for (int i = 0; i < beamCount; i++)
                {
                    float baseAngle = (i / (float)Mathf.Max(1, beamCount)) * 180f + (side == 0 ? 0f : 90f);
                    GameObject pivot = AddEmpty(
                        "LaserPivot_" + side + "_" + i,
                        new Vector3(x, pivotY, pivotZ),
                        Quaternion.Euler(0f, 0f, baseAngle)
                    );
                    AddMesh(
                        "Beam_" + side + "_" + i,
                        beamMesh,
                        (side + i) % 2 == 0 ? _matBeamA : _matBeamB,
                        Vector3.zero,
                        Quaternion.identity,
                        pivot.transform
                    );
                    AddMesh(
                        "BeamGlow_" + side + "_" + i,
                        NewMesh(NeonStageKit.CreateQuad(beamWidth * 14f, beamLength)),
                        (side + i) % 2 == 0 ? _matGlowA : _matGlowB,
                        new Vector3(0f, beamLength * 0.5f, 0f),
                        Quaternion.identity,
                        pivot.transform
                    );
                    _pivots.Add(pivot.transform);
                    _baseAngles.Add(baseAngle);
                }
            }

            if (showGateFrame)
            {
                BuildFrame("Gate", 1f, gateZ, _matGate, gateWidth, gateHeight, gateBarThickness);
                AddHalo(
                    "GateGlow",
                    gateWidth * 2.6f,
                    gateHeight * 2.4f,
                    _matGateGlow,
                    new Vector3(0f, 1.6f, gateZ)
                );
            }

            if (showSecondFrame)
            {
                BuildFrame("GateFar", secondFrameScale, secondFrameZ, _matGateFar, gateWidth, gateHeight, gateBarThickness * 1.6f);
            }

            if (showFloorStrips)
            {
                Mesh stripMesh = NewMesh(NeonStageKit.CreateFloorStrip(stripWidth, stripLength, 0.015f));
                AddMesh("StripLeft", stripMesh, _matStrip, new Vector3(-stripOffset, 0f, 0.2f), Quaternion.identity);
                AddMesh("StripRight", stripMesh, _matStrip, new Vector3(stripOffset, 0f, 0.2f), Quaternion.identity);
            }
        }

        void BuildFrame(string name, float scale, float z, Material material, float width, float height, float thickness)
        {
            float w = width * scale;
            float h = height * scale;
            Vector3 center = new Vector3(0f, 1.6f + height * (scale - 1f) * 0.5f, z);

            Mesh barV = NewMesh(NeonStageKit.CreateColumn(thickness, thickness, h + thickness * 2f, 4));
            Mesh barH = NewMesh(NeonStageKit.CreateColumn(thickness, thickness, w + thickness * 2f, 4));

            AddBar(name + "Top", barH, material, center + Vector3.up * (h * 0.5f), Vector3.right, w, thickness);
            AddBar(name + "Bottom", barH, material, center - Vector3.up * (h * 0.5f), Vector3.right, w, thickness);
            AddBar(name + "Left", barV, material, center - Vector3.right * (w * 0.5f), Vector3.up, h, thickness);
            AddBar(name + "Right", barV, material, center + Vector3.right * (w * 0.5f), Vector3.up, h, thickness);
        }

        protected override void ApplyBeat(float beat, float low, float high, long beatIndex)
        {
            float now = Time.realtimeSinceStartup;
            float deltaTime = Mathf.Clamp(now - _lastTime, 0f, 0.1f);
            _lastTime = now;

            if (beatIndex != _lastBeatIndex)
            {
                _lastBeatIndex = beatIndex;
                _stepTarget += beatStepDegrees;
            }

            float snap = 1f - Mathf.Exp(-stepSnapSpeed * Mathf.Max(0.0001f, deltaTime));
            _stepCurrent = Mathf.Lerp(_stepCurrent, _stepTarget, snap);

            float spin = now * spinSpeed + _stepCurrent;
            for (int i = 0; i < _pivots.Count; i++)
            {
                if (_pivots[i] == null) continue;
                _pivots[i].localRotation = Quaternion.Euler(0f, 0f, _baseAngles[i] + spin * (i % 2 == 0 ? 1f : -1f));
            }

            float beamLevel = idleLevel * 0.8f + beat * beatGain;
            NeonStageKit.SetMaterialLevel(_matGlowA, GlowA, beamLevel * 0.32f);
            NeonStageKit.SetMaterialLevel(_matGlowB, GlowB, beamLevel * 0.32f);
            NeonStageKit.SetMaterialLevel(_matGateGlow, GlowC, idleLevel * 0.5f + low * 0.35f);
            NeonStageKit.SetMaterialLevel(_matBeamA, GlowA, beamLevel);
            NeonStageKit.SetMaterialLevel(_matBeamB, GlowB, beamLevel);
            NeonStageKit.SetMaterialLevel(_matGate, GlowC, idleLevel * 0.5f + low * accentGain * 0.4f + beat * 0.25f);
            NeonStageKit.SetMaterialLevel(_matGateFar, GlowB, idleLevel * 0.3f + low * 0.2f);
            NeonStageKit.SetMaterialLevel(_matStrip, GlowC, idleLevel * 0.7f + beat * accentGain * 0.6f);
        }
    }
}
