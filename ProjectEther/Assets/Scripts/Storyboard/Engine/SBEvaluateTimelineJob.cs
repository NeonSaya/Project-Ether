using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace OsuVR.Storyboard.Engine
{
    /// <summary>Evaluate the last started transform on each independent lazer property.</summary>
    [BurstCompile]
    public struct SBEvaluateTimelineJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<SBSpriteFlatData> Sprites;
        [ReadOnly] public NativeArray<SBCommandFlatData> Commands;
        [ReadOnly] public NativeArray<SBLoopFlatData> Loops;
        [ReadOnly] public NativeArray<int> FrameMap;
        [ReadOnly] public NativeArray<SBTriggeredCommand> TriggerCommands;
        [WriteOnly] public NativeArray<SpriteInputData> Output;
        public double CurrentTime;
        public int SpriteCount;
        public float XOffset; // always zero for both normal and widescreen storyboards

        struct Candidate
        {
            public int Index;
            public bool Triggered;
            public double Start, End, Offset, OrderStart, OrderEnd;
        }

        struct CandidateSet
        {
            Candidate c0, c1, c2, c3, c4, c5, c6, c7, c8, c9, c10, c11, c12;
            public int Length => 13;
            public Candidate this[int index]
            {
                get
                {
                    switch (index)
                    {
                        case 0: return c0;
                        case 1: return c1;
                        case 2: return c2;
                        case 3: return c3;
                        case 4: return c4;
                        case 5: return c5;
                        case 6: return c6;
                        case 7: return c7;
                        case 8: return c8;
                        case 9: return c9;
                        case 10: return c10;
                        case 11: return c11;
                        case 12: return c12;
                        default: return default;
                    }
                }
                set
                {
                    switch (index)
                    {
                        case 0: c0 = value; break;
                        case 1: c1 = value; break;
                        case 2: c2 = value; break;
                        case 3: c3 = value; break;
                        case 4: c4 = value; break;
                        case 5: c5 = value; break;
                        case 6: c6 = value; break;
                        case 7: c7 = value; break;
                        case 8: c8 = value; break;
                        case 9: c9 = value; break;
                        case 10: c10 = value; break;
                        case 11: c11 = value; break;
                        case 12: c12 = value; break;
                    }
                }
            }
        }

        public void Execute(int i)
        {
            if (i >= SpriteCount) { Output[i] = default; return; }
            var sprite = Sprites[i];
            if (CurrentTime < sprite.StartTime || CurrentTime >= sprite.EndTime)
            {
                Output[i] = default;
                return;
            }
            var state = new SpriteInputData
            {
                X = sprite.InitX, Y = sprite.InitY,
                ScaleX = sprite.InitScaleX, ScaleY = sprite.InitScaleY,
                VectorScaleX = sprite.InitVectorScaleX, VectorScaleY = sprite.InitVectorScaleY,
                Alpha = sprite.InitAlpha, Rotation = sprite.InitRotation,
                R = sprite.InitR, G = sprite.InitG, B = sprite.InitB,
                FlipH = sprite.InitFlipH, FlipV = sprite.InitFlipV, Additive = sprite.InitAdditive,
                TexIndex = sprite.TexIndex, OriginIndex = sprite.OriginIndex,
                TexWidth = sprite.TexWidth, TexHeight = sprite.TexHeight
            };
            float uniformScale = sprite.InitUniformScale;
            var selected = new CandidateSet();
            for (int t = 0; t < selected.Length; t++) selected[t] = new Candidate { Index = -1 };
            for (int c = sprite.CmdOffset; c < sprite.CmdOffset + sprite.CmdCount; c++)
                Select(ref selected, c, 0);
            for (int l = sprite.LoopOffset; l < sprite.LoopOffset + sprite.LoopCount; l++)
            {
                var loop = Loops[l];
                for (int c = loop.InnerCmdOffset; c < loop.InnerCmdOffset + loop.InnerCmdCount; c++)
                {
                    double first = loop.StartTime + Commands[c].StartTime;
                    if (CurrentTime < first) continue;
                    double iteration = loop.LoopDuration > 0
                        ? math.min(math.floor((CurrentTime - first) / loop.LoopDuration), math.max(1, loop.LoopCount) - 1)
                        : 0;
                    Select(ref selected, c, loop.StartTime + iteration * loop.LoopDuration, false, loop.StartTime);
                }
            }
            for (int c = sprite.TriggerHead; c >= 0 && c < TriggerCommands.Length; c = TriggerCommands[c].Next)
                Select(ref selected, c, 0, true);
            for (int t = 0; t < selected.Length; t++)
            {
                var candidate = selected[t];
                if (candidate.Index >= 0)
                    Apply(candidate.Triggered ? TriggerCommands[candidate.Index].Command : Commands[candidate.Index],
                        CurrentTime - candidate.Offset, ref state, ref uniformScale);
            }
            state.X += XOffset;
            state.ScaleX *= uniformScale * state.VectorScaleX;
            state.ScaleY *= uniformScale * state.VectorScaleY;
            if (sprite.AnimFrameCount > 0)
            {
                double elapsed = math.max(0, CurrentTime - sprite.StartTime);
                int frame = sprite.AnimFrameDelay > 0 ? (int)(elapsed / sprite.AnimFrameDelay) : 0;
                if (sprite.AnimLoopType == 0) frame %= sprite.AnimFrameCount;
                frame = math.clamp(frame, 0, sprite.AnimFrameCount - 1);
                int index = sprite.AnimFrameMapOffset + frame;
                state.TexIndex = (uint)index < (uint)FrameMap.Length ? FrameMap[index] : -1;
            }
            Output[i] = state;
        }

        void Select(ref CandidateSet selected, int index, double offset, bool triggered = false, double orderOffset = 0)
        {
            var command = triggered ? TriggerCommands[index].Command : Commands[index];
            if ((uint)command.Target >= (uint)selected.Length) return;
            double start = command.StartTime + offset;
            if (start > CurrentTime) return;
            double end = command.EndTime + offset;
            // P schedules two independent instantaneous changes in lazer. An older
            // window ending now can reset a newer overlapping parameter window.
            if (command.Target >= 7 && command.Target <= 9 && CurrentTime >= end) start = end;
            var previous = selected[command.Target];
            double orderStart = command.StartTime + orderOffset;
            double orderEnd = command.EndTime + orderOffset;
            if (previous.Index >= 0 && (start < previous.Start || (start == previous.Start &&
                (orderStart < previous.OrderStart || (orderStart == previous.OrderStart && orderEnd < previous.OrderEnd))))) return;
            if (previous.Index >= 0 && previous.Triggered && triggered && start == previous.Start && end == previous.End && index < previous.Index) return;
            selected[command.Target] = new Candidate { Index = index, Start = start, End = end, Offset = offset, Triggered = triggered, OrderStart = orderStart, OrderEnd = orderEnd };
        }

        static void Apply(SBCommandFlatData command, double time, ref SpriteInputData state, ref float uniformScale)
        {
            int target = command.Target;
            if (target >= 7 && target <= 9)
            {
                byte value = time >= command.EndTime ? command.BoolEnd : command.BoolStart;
                if (target == 7) state.Additive = value;
                if (target == 8) state.FlipH = value;
                if (target == 9) state.FlipV = value;
                return;
            }
            float progress = GetEasedProgress(command, time);
            if (target == 6)
            {
                // osu-framework interpolates colour commands in linear light, then
                // emits encoded colour to its UNORM sprite shader.
                progress = math.saturate(progress);
                state.R = InterpolateColour(command.ColorStartR, command.ColorEndR, progress);
                state.G = InterpolateColour(command.ColorStartG, command.ColorEndG, progress);
                state.B = InterpolateColour(command.ColorStartB, command.ColorEndB, progress);
                return;
            }
            float valueFloat = math.lerp(command.FloatStart, command.FloatEnd, progress);
            switch (target)
            {
                case 0: state.Alpha = valueFloat; break;
                case 1: state.X = valueFloat; break;
                case 2: state.Y = valueFloat; break;
                case 3: state.ScaleX = valueFloat; break;
                case 4: state.ScaleY = valueFloat; break;
                case 5: state.Rotation = valueFloat; break;
                case 10: uniformScale = valueFloat; break;
                case 11: state.VectorScaleX = valueFloat; break;
                case 12: state.VectorScaleY = valueFloat; break;
            }
        }

        static float ToLinear(float v) => v <= 0.04045f ? v / 12.92f : math.pow((v + 0.055f) / 1.055f, 2.4f);
        static float InterpolateColour(float a, float b, float t)
        {
            if (t <= 0) return a;
            if (t >= 1) return b;
            float v = math.lerp(ToLinear(a), ToLinear(b), t);
            return v <= 0.0031308f ? v * 12.92f : 1.055f * math.pow(v, 1f / 2.4f) - 0.055f;
        }

        static float GetEasedProgress(SBCommandFlatData command, double time)
        {
            if (time >= command.EndTime) return 1;
            if (time <= command.StartTime) return 0;
            return EaseFloat(command.Easing, (float)((time - command.StartTime) / (command.EndTime - command.StartTime)));
        }

        static float EaseFloat(int easing, float t)
        {
            switch (easing)
            {
                case 0:  return t;                                              // Linear
                case 1:                                                        // Out (OutQuad)
                case 4:  return t * (2f - t);                                   // OutQuad
                case 2:                                                        // In (InQuad)
                case 3:  return t * t;                                          // InQuad
                case 5:  return t < 0.5f ? t * t * 2f : (t - 1f) * (t - 1f) * -2f + 1f; // InOutQuad
                case 6:  return t * t * t;                                      // InCubic
                case 7:  { float n = t - 1f; return n * n * n + 1f; }          // OutCubic
                case 8:  return t < 0.5f ? t * t * t * 4f : (t - 1f) * (t - 1f) * (t - 1f) * 4f + 1f; // InOutCubic
                case 9:  return t * t * t * t;                                  // InQuart
                case 10: { float n = t - 1f; return 1f - n * n * n * n; }      // OutQuart
                case 11: return t < 0.5f ? t * t * t * t * 8f : (t - 1f) * (t - 1f) * (t - 1f) * (t - 1f) * -8f + 1f; // InOutQuart
                case 12: return t * t * t * t * t;                              // InQuint
                case 13: { float n = t - 1f; return n * n * n * n * n + 1f; }  // OutQuint
                case 14: return t < 0.5f ? t * t * t * t * t * 16f : (t - 1f) * (t - 1f) * (t - 1f) * (t - 1f) * (t - 1f) * 16f + 1f; // InOutQuint
                case 15: return 1f - math.cos(t * math.PI * 0.5f);             // InSine
                case 16: return math.sin(t * math.PI * 0.5f);                  // OutSine
                case 17: return 0.5f - 0.5f * math.cos(math.PI * t);           // InOutSine
                case 18: return math.pow(2f, 10f * (t - 1f)) + (t - 1f) / 1024f;                  // InExpo
                case 19: return -math.pow(2f, -10f * t) + 1f + t / 1024f;                  // OutExpo
                case 20: // InOutExpo
                    return t < 0.5f
                        ? 0.5f * (math.pow(2f, 20f * t - 10f) + (2f * t - 1f) / 1024f)
                        : 1f - 0.5f * (math.pow(2f, -20f * t + 10f) + (-2f * t + 1f) / 1024f);
                case 21: return 1f - math.sqrt(1f - t * t);                    // InCirc
                case 22: { float n = t - 1f; return math.sqrt(1f - n * n); }   // OutCirc
                case 23: // InOutCirc
                    {
                        float n = t * 2f;
                        if (n < 1f) return 0.5f - 0.5f * math.sqrt(1f - n * n);
                        n -= 2f;
                        return 0.5f * math.sqrt(1f - n * n) + 0.5f;
                    }
                case 24: // InElastic
                    return -math.pow(2f, -10f + 10f * t)
                           * math.sin((1f - 0.075f - t) * (2f * math.PI) / 0.3f) + (1f - t) / 2048f;
                case 25: // OutElastic
                    return math.pow(2f, -10f * t)
                           * math.sin((t - 0.075f) * (2f * math.PI) / 0.3f) + 1f - t / 2048f;
                case 26: // OutElasticHalf
                    return math.pow(2f, -10f * t)
                           * math.sin((0.5f * t - 0.075f) * (2f * math.PI) / 0.3f) + 1f
                           - t / 1024f * math.sin((0.5f - 0.075f) * (2f * math.PI) / 0.3f);
                case 27: // OutElasticQuarter
                    return math.pow(2f, -10f * t)
                           * math.sin((0.25f * t - 0.075f) * (2f * math.PI) / 0.3f) + 1f
                           - t / 1024f * math.sin((0.25f - 0.075f) * (2f * math.PI) / 0.3f);
                case 28: // lazer uses a 0.45 period for InOutElastic, with endpoint correction.
                    {
                        float n = t * 2f;
                        float frequency = 2f * math.PI / 0.45f;
                        float offset = math.sin((1f - 0.1125f) * frequency) / 1024f;
                        if (n < 1f)
                            return -0.5f * (math.pow(2f, -10f + 10f * n) * math.sin((1f - 0.1125f - n) * frequency) - offset * (1f - n));
                        n -= 1f;
                        return 0.5f * (math.pow(2f, -10f * n) * math.sin((n - 0.1125f) * frequency) - offset * n) + 1f;
                    }
                case 29: return t * t * ((1.70158f + 1f) * t - 1.70158f);      // InBack
                case 30: { float n = t - 1f; return n * n * ((1.70158f + 1f) * n + 1.70158f) + 1f; } // OutBack
                case 31: // InOutBack
                    {
                        float n = t * 2f;
                        if (n < 1f)
                            return 0.5f * n * n * ((1.70158f * 1.525f + 1f) * n - 1.70158f * 1.525f);
                        n -= 2f;
                        return 0.5f * (n * n * ((1.70158f * 1.525f + 1f) * n + 1.70158f * 1.525f) + 2f);
                    }
                case 32: return 1f - EaseBounce(1f - t);                       // InBounce
                case 33: return EaseBounce(t);                                  // OutBounce
                case 34: // InOutBounce
                    return t < 0.5f
                        ? 0.5f - 0.5f * EaseBounce(1f - t * 2f)
                        : EaseBounce((t - 0.5f) * 2f) * 0.5f + 0.5f;
                case 35: { float n = t - 1f; return n * math.pow(n, 10f) + 1f; } // OutPow10
                default: return t;
            }
        }

        static float EaseBounce(float n)
        {
            if (n < 1f / 2.75f)
                return 7.5625f * n * n;
            if (n < 2f * (1f / 2.75f))
            {
                n -= 1.5f * (1f / 2.75f);
                return 7.5625f * n * n + 0.75f;
            }
            if (n < 2.5f * (1f / 2.75f))
            {
                n -= 2.25f * (1f / 2.75f);
                return 7.5625f * n * n + 0.9375f;
            }
            n -= 2.625f * (1f / 2.75f);
            return 7.5625f * n * n + 0.984375f;
        }
    }
}
