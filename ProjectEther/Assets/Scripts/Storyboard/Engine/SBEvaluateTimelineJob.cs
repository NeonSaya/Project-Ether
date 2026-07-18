using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using OsuVR.Storyboard.Data;

namespace OsuVR.Storyboard.Engine
{
    /// <summary>
    /// Burst-compiled 时间轴求值 Job
    /// 替代整个 SBOsbPlayer → SBPlayingLayer → SBPlayingSprite 主线程求值管线
    ///
    /// 每个 sprite 独立求值: alive 判断 → 直接命令位掩码扫描 → Loop 动态求值 → 动画帧解析
    /// 输出 SpriteInputData 供 BuildInstanceJob 消费
    /// </summary>
    [BurstCompile]
    public struct SBEvaluateTimelineJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<SBSpriteFlatData> Sprites;
        [ReadOnly] public NativeArray<SBCommandFlatData> Commands;
        [ReadOnly] public NativeArray<SBLoopFlatData> Loops;
        [ReadOnly] public NativeArray<int> FrameMap;
        [WriteOnly] public NativeArray<SpriteInputData> Output;

        public double CurrentTime;
        public int SpriteCount;

        public void Execute(int i)
        {
            if (i >= SpriteCount)
            {
                Output[i] = default;
                return;
            }

            var sprite = Sprites[i];

            // ---- 1. Alive 判断: 不可见直接输出零数据 (被 GPU 剔除) ----
            if (CurrentTime < sprite.StartTime || CurrentTime > sprite.EndTime)
            {
                Output[i] = default;
                return;
            }

            // ---- 2. 初始化为 sprite 的默认值 ----
            float alpha = sprite.InitAlpha;
            float x = sprite.InitX;
            float y = sprite.InitY;
            float scaleX = sprite.InitScaleX;
            float scaleY = sprite.InitScaleY;
            float rotation = sprite.InitRotation;
            float r = sprite.InitR;
            float g = sprite.InitG;
            float b = sprite.InitB;
            byte flipH = sprite.InitFlipH;
            byte flipV = sprite.InitFlipV;
            byte additive = sprite.InitAdditive;

            // ---- 3. 评估 Loop 命令 (先评估 = 更高优先级, 与 osu!/SBPlayingSprite 一致) ----
            int mask = 0;
            int found = 0;

            for (int li = sprite.LoopOffset + sprite.LoopCount - 1;
                 li >= sprite.LoopOffset && found < 10; li--)
            {
                EvalLoop(Loops[li], CurrentTime, ref mask, ref found,
                    ref alpha, ref x, ref y, ref scaleX, ref scaleY, ref rotation,
                    ref r, ref g, ref b, ref flipH, ref flipV, ref additive);
            }

            // ---- 4. 评估直接命令 (填充 Loop 未覆盖的属性) ----
            for (int ci = sprite.CmdOffset + sprite.CmdCount - 1;
                 ci >= sprite.CmdOffset && found < 10; ci--)
            {
                var cmd = Commands[ci];
                if (cmd.StartTime > CurrentTime) continue;

                int bit = 1 << cmd.Target;
                if ((mask & bit) != 0) continue;

                mask |= bit;
                found++;
                ApplyCommand(cmd, CurrentTime,
                    ref alpha, ref x, ref y, ref scaleX, ref scaleY, ref rotation,
                    ref r, ref g, ref b, ref flipH, ref flipV, ref additive);
            }

            // ---- 5. 动画帧纹理索引解析 ----
            int texIdx = sprite.TexIndex;
            if (sprite.AnimFrameCount > 0)
            {
                texIdx = ResolveAnimFrame(sprite, FrameMap, CurrentTime);
            }

            // ---- 6. 写入输出 ----
            Output[i] = new SpriteInputData
            {
                X = x,
                Y = y,
                ScaleX = scaleX,
                ScaleY = scaleY,
                Rotation = rotation,
                Alpha = alpha,
                R = r,
                G = g,
                B = b,
                FlipH = flipH,
                FlipV = flipV,
                Additive = additive,
                TexIndex = texIdx,
                OriginIndex = sprite.OriginIndex,
                TexWidth = sprite.TexWidth,
                TexHeight = sprite.TexHeight
            };
        }

        // =========================================================
        //  命令应用
        // =========================================================

        void ApplyCommand(SBCommandFlatData cmd, double time,
            ref float alpha, ref float x, ref float y,
            ref float scaleX, ref float scaleY, ref float rotation,
            ref float r, ref float g, ref float b,
            ref byte flipH, ref byte flipV, ref byte additive)
        {
            int target = cmd.Target;

            // Bool 命令 (FlipH=8, FlipV=9, BlendingMode=7)
            if (target >= 7)
            {
                byte val = (time >= cmd.EndTime) ? cmd.BoolEnd : cmd.BoolStart;
                switch (target)
                {
                    case 7: additive = val; break; // BlendingMode
                    case 8: flipH = val; break;    // FlipH
                    case 9: flipV = val; break;    // FlipV
                }
                return;
            }

            // Color 命令 (Color=6)
            if (target == 6)
            {
                if (time >= cmd.EndTime)
                {
                    r = cmd.ColorEndR;
                    g = cmd.ColorEndG;
                    b = cmd.ColorEndB;
                }
                else
                {
                    float p = GetEasedProgress(cmd, time);
                    r = cmd.ColorStartR + (cmd.ColorEndR - cmd.ColorStartR) * p;
                    g = cmd.ColorStartG + (cmd.ColorEndG - cmd.ColorStartG) * p;
                    b = cmd.ColorStartB + (cmd.ColorEndB - cmd.ColorStartB) * p;
                }
                return;
            }

            // Float 命令 (Alpha=0, X=1, Y=2, ScaleX=3, ScaleY=4, Rotation=5)
            float fVal;
            if (time >= cmd.EndTime)
            {
                fVal = cmd.FloatEnd;
            }
            else
            {
                float p = GetEasedProgress(cmd, time);
                fVal = cmd.FloatStart + (cmd.FloatEnd - cmd.FloatStart) * p;
            }

            switch (target)
            {
                case 0: alpha = fVal; break;
                case 1: x = fVal; break;
                case 2: y = fVal; break;
                case 3: scaleX = fVal; break;
                case 4: scaleY = fVal; break;
                case 5: rotation = fVal; break;
            }
        }

        // =========================================================
        //  Loop 动态求值 (不预展开, 运行时计算迭代)
        // =========================================================

        void EvalLoop(SBLoopFlatData loop, double time, ref int mask, ref int found,
            ref float alpha, ref float x, ref float y,
            ref float scaleX, ref float scaleY, ref float rotation,
            ref float r, ref float g, ref float b,
            ref byte flipH, ref byte flipV, ref byte additive)
        {
            if (time < loop.StartTime) return;
            if (loop.LoopDuration <= 0) return;
            if (loop.InnerCmdCount == 0) return;

            double loopTime = time - loop.StartTime;

            // 1. Past loop end: hold last command's EndValue (storybrew: Commands[^1] at final iteration)
            if (loop.LoopCount > 0 && loopTime >= loop.LoopCount * loop.LoopDuration)
            {
                for (int ci = loop.InnerCmdOffset + loop.InnerCmdCount - 1;
                     ci >= loop.InnerCmdOffset && found < 10; ci--)
                {
                    var cmd = Commands[ci];
                    int bit = 1 << cmd.Target;
                    if ((mask & bit) != 0) continue;
                    mask |= bit;
                    found++;
                    ApplyEndValue(cmd,
                        ref alpha, ref x, ref y, ref scaleX, ref scaleY, ref rotation,
                        ref r, ref g, ref b, ref flipH, ref flipV, ref additive);
                }
                return;
            }

            // Normalize to current iteration's local time
            int loopNumber = (int)(loopTime / loop.LoopDuration);
            loopTime -= loopNumber * loop.LoopDuration;

            // 2. Between iterations (gap before first command): hold last command's EndValue
            //    from the PREVIOUS iteration (storybrew: Commands[^1].AsResult with previous offset)
            if (loopTime < Commands[loop.InnerCmdOffset].StartTime)
            {
                for (int ci = loop.InnerCmdOffset + loop.InnerCmdCount - 1;
                     ci >= loop.InnerCmdOffset && found < 10; ci--)
                {
                    var cmd = Commands[ci];
                    int bit = 1 << cmd.Target;
                    if ((mask & bit) != 0) continue;
                    mask |= bit;
                    found++;
                    ApplyEndValue(cmd,
                        ref alpha, ref x, ref y, ref scaleX, ref scaleY, ref rotation,
                        ref r, ref g, ref b, ref flipH, ref flipV, ref additive);
                }
                return;
            }

            // 3. Within iteration: evaluate commands at loopTime
            for (int ci = loop.InnerCmdOffset + loop.InnerCmdCount - 1;
                 ci >= loop.InnerCmdOffset && found < 10; ci--)
            {
                var cmd = Commands[ci];
                if (cmd.StartTime > loopTime) continue;

                int bit = 1 << cmd.Target;
                if ((mask & bit) != 0) continue;

                mask |= bit;
                found++;

                if (loopTime <= cmd.EndTime)
                {
                    ApplyCommand(cmd, loopTime,
                        ref alpha, ref x, ref y, ref scaleX, ref scaleY, ref rotation,
                        ref r, ref g, ref b, ref flipH, ref flipV, ref additive);
                }
                else
                {
                    ApplyEndValue(cmd,
                        ref alpha, ref x, ref y, ref scaleX, ref scaleY, ref rotation,
                        ref r, ref g, ref b, ref flipH, ref flipV, ref additive);
                }
            }
        }

        void ApplyEndValue(SBCommandFlatData cmd,
            ref float alpha, ref float x, ref float y,
            ref float scaleX, ref float scaleY, ref float rotation,
            ref float r, ref float g, ref float b,
            ref byte flipH, ref byte flipV, ref byte additive)
        {
            int target = cmd.Target;
            if (target >= 7)
            {
                switch (target)
                {
                    case 7: additive = cmd.BoolEnd; break;
                    case 8: flipH = cmd.BoolEnd; break;
                    case 9: flipV = cmd.BoolEnd; break;
                }
            }
            else if (target == 6)
            {
                r = cmd.ColorEndR;
                g = cmd.ColorEndG;
                b = cmd.ColorEndB;
            }
            else
            {
                switch (target)
                {
                    case 0: alpha = cmd.FloatEnd; break;
                    case 1: x = cmd.FloatEnd; break;
                    case 2: y = cmd.FloatEnd; break;
                    case 3: scaleX = cmd.FloatEnd; break;
                    case 4: scaleY = cmd.FloatEnd; break;
                    case 5: rotation = cmd.FloatEnd; break;
                }
            }
        }

        // =========================================================
        //  动画帧解析
        // =========================================================

        static int ResolveAnimFrame(SBSpriteFlatData sprite, NativeArray<int> frameMap, double currentTime)
        {
            if (sprite.AnimFrameCount <= 0 || sprite.AnimFrameDelay <= 0)
                return -1;

            double elapsed = currentTime - sprite.StartTime;
            int frame = (int)(elapsed / sprite.AnimFrameDelay);

            if (sprite.AnimLoopType == 0) // LoopForever
            {
                frame %= sprite.AnimFrameCount;
                if (frame < 0) frame += sprite.AnimFrameCount;
            }
            frame = math.clamp(frame, 0, sprite.AnimFrameCount - 1);

            // 经帧映射取纹理切片: 缺失帧 (-1) → 该帧不绘制 (与 storybrew/osu! 一致)
            int mapIdx = sprite.AnimFrameMapOffset + frame;
            if ((uint)mapIdx >= (uint)frameMap.Length)
                return -1;
            return frameMap[mapIdx];
        }

        // =========================================================
        //  缓动函数 (Burst 兼容, 完整移植 EasingMath.Interpolate)
        // =========================================================

        static float GetEasedProgress(SBCommandFlatData cmd, double time)
        {
            double duration = cmd.EndTime - cmd.StartTime;
            if (duration <= 0) return time < cmd.StartTime ? 0f : 1f;
            float t = (float)((time - cmd.StartTime) / duration);
            return EaseFloat(cmd.Easing, math.clamp(t, 0f, 1f));
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
                case 18: return math.pow(2f, 10f * (t - 1f));                  // InExpo
                case 19: return -math.pow(2f, -10f * t) + 1f;                  // OutExpo
                case 20: // InOutExpo
                    return t < 0.5f
                        ? 0.5f * math.pow(2f, 20f * t - 10f)
                        : 1f - 0.5f * math.pow(2f, -20f * t + 10f);
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
                           * math.sin((1f - 0.075f - t) * (2f * math.PI) / 0.3f);
                case 25: // OutElastic
                    return math.pow(2f, -10f * t)
                           * math.sin((t - 0.075f) * (2f * math.PI) / 0.3f) + 1f;
                case 26: // OutElasticHalf
                    return math.pow(2f, -10f * t)
                           * math.sin((0.5f * t - 0.075f) * (2f * math.PI) / 0.3f) + 1f;
                case 27: // OutElasticQuarter
                    return math.pow(2f, -10f * t)
                           * math.sin((0.25f * t - 0.075f) * (2f * math.PI) / 0.3f) + 1f;
                case 28: // InOutElastic (与 storybrew 一致: ToInOut(ElasticIn), 原始常量)
                    {
                        float n = t * 2f;
                        if (n < 1f)
                            return -0.5f * math.pow(2f, -10f + 10f * n)
                                   * math.sin((1f - 0.075f - n) * (2f * math.PI) / 0.3f);
                        n -= 1f;
                        return 0.5f * math.pow(2f, -10f * n)
                               * math.sin((n - 0.075f) * (2f * math.PI) / 0.3f) + 1f;
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
