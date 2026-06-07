using System.Collections.Generic;
using UnityEngine;
using OsuVR.Storyboard.Data;

namespace OsuVR.Storyboard.Engine
{
    /// <summary>
    /// 运行时 Sprite — 与 osu!lazer 一致的统一命令评估
    /// 直接命令和循环迭代命令按 StartTime 排序，每帧向后扫描找每个属性的最新命令
    /// 循环动态评估：不展开全部迭代，每帧计算所在迭代并映射 loopTime
    /// </summary>
    public class SBPlayingSprite
    {
        internal SBPlayingSprite Prev;
        internal SBPlayingSprite Next;

        public SBElement Element;
        public int CachedTexIndex = -1;
        public double StartTime;  // 元素首个命令的开始时间 (用于动画帧计算)
        public SBRenderState State;

        // 直接命令（已排序）
        private List<SBSpriteCommand> _directCmds;

        // 循环元数据（不展开，运行时评估）
        private struct LoopMeta
        {
            public double StartTime;
            public double LoopDuration;
            public int LoopCount;
            public List<SBSpriteCommand> InnerCmds; // 已排序，相对时间
        }
        private List<LoopMeta> _loops;

        public void Load(SBElement element, SBCommandGroup group)
        {
            Element = element;
            State = SBRenderState.Default;
            State.X = element.InitialPosition.x;
            State.Y = element.InitialPosition.y;

            _directCmds = new List<SBSpriteCommand>();
            _loops = null;

            for (int i = 0; i < group.Commands.Count; i++)
            {
                var cmd = group.Commands[i];
                if (cmd is SBLoopCommand loop)
                {
                    if (_loops == null) _loops = new List<LoopMeta>();
                    var innerCmds = new List<SBSpriteCommand>(loop.InnerGroup.Commands);
                    innerCmds.Sort((a, b) =>
                    {
                        int cmp = a.StartTime.CompareTo(b.StartTime);
                        return cmp != 0 ? cmp : a.EndTime.CompareTo(b.EndTime);
                    });
                    _loops.Add(new LoopMeta
                    {
                        StartTime = loop.StartTime,
                        LoopDuration = loop.LoopDuration,
                        LoopCount = loop.LoopCount,
                        InnerCmds = innerCmds
                    });
                }
                else
                {
                    _directCmds.Add(cmd);
                }
            }

            _directCmds.Sort((a, b) =>
            {
                int cmp = a.StartTime.CompareTo(b.StartTime);
                return cmp != 0 ? cmp : a.EndTime.CompareTo(b.EndTime);
            });

            // 设置初始值（与 osu!lazer 的 ApplyInitialValue 一致）
            ApplyInitialValues();

            double startTime = group.StartTime();
            StartTime = startTime;
            if (startTime < double.MaxValue)
                Update(startTime);
        }

        void ApplyInitialValues()
        {
            // 从直接命令中找每个属性的最早命令，应用其 StartValue
            var found = new HashSet<SBCommandTarget>();
            for (int i = 0; i < _directCmds.Count && found.Count < 10; i++)
            {
                var cmd = _directCmds[i];
                if (found.Contains(cmd.Target)) continue;
                found.Add(cmd.Target);

                switch (cmd)
                {
                    case SBFloatCommand fc:
                        ApplyFloat(fc.Target, fc.StartValue);
                        break;
                    case SBColorCommand cc:
                        State.R = cc.StartValue.r / 255f;
                        State.G = cc.StartValue.g / 255f;
                        State.B = cc.StartValue.b / 255f;
                        break;
                    case SBBoolCommand bc:
                        ApplyBool(bc.Target, bc.StartValue);
                        break;
                }
            }
        }

        public void Update(double time)
        {
            // 重置为默认值
            State.Alpha = 1f;
            State.ScaleX = 1f;
            State.ScaleY = 1f;
            State.Rotation = 0f;
            State.R = 1f;
            State.G = 1f;
            State.B = 1f;
            State.Additive = false;
            State.FlipH = false;
            State.FlipV = false;
            State.X = Element.InitialPosition.x;
            State.Y = Element.InitialPosition.y;

            // 收集所有活跃命令（直接 + 循环迭代），向后扫描找每个属性的最新命令
            // 用位掩码跟踪已找到的属性
            int mask = 0;
            int found = 0;

            // 1. 先评估循环（后评估 = 更高优先级，因为循环通常比直接命令晚）
            if (_loops != null)
            {
                for (int li = _loops.Count - 1; li >= 0 && found < 10; li--)
                {
                    EvalLoop(_loops[li], time, ref mask, ref found);
                }
            }

            // 2. 再评估直接命令（填充循环未覆盖的属性）
            for (int i = _directCmds.Count - 1; i >= 0 && found < 10; i--)
            {
                var cmd = _directCmds[i];
                if (cmd.StartTime > time) continue;

                int bit = 1 << (int)cmd.Target;
                if ((mask & bit) != 0) continue;

                mask |= bit;
                found++;
                ApplyCmd(cmd, time);
            }
        }

        void EvalLoop(LoopMeta loop, double time, ref int mask, ref int found)
        {
            if (time < loop.StartTime) return;
            if (loop.LoopDuration <= 0) return;

            var cmds = loop.InnerCmds;
            int cmdCount = cmds.Count;
            if (cmdCount == 0) return;

            double loopTime = time - loop.StartTime;

            // Past loop end: hold last command's EndValue
            if (loop.LoopCount > 0 && loopTime >= loop.LoopCount * loop.LoopDuration)
            {
                for (int i = cmdCount - 1; i >= 0 && found < 10; i--)
                {
                    var cmd = cmds[i];
                    int bit = 1 << (int)cmd.Target;
                    if ((mask & bit) != 0) continue;
                    mask |= bit;
                    found++;
                    ApplyCmdEndValue(cmd);
                }
                return;
            }

            int loopNumber = (int)(loopTime / loop.LoopDuration);
            loopTime -= loopNumber * loop.LoopDuration;

            // Between iterations (gap before first command): hold last command's EndValue
            if (loopTime < cmds[0].StartTime)
            {
                for (int i = cmdCount - 1; i >= 0 && found < 10; i--)
                {
                    var cmd = cmds[i];
                    int bit = 1 << (int)cmd.Target;
                    if ((mask & bit) != 0) continue;
                    mask |= bit;
                    found++;
                    ApplyCmdEndValue(cmd);
                }
                return;
            }

            // Within iteration: evaluate commands at loopTime
            for (int i = cmdCount - 1; i >= 0 && found < 10; i--)
            {
                var cmd = cmds[i];
                if (cmd.StartTime > loopTime) continue;

                int bit = 1 << (int)cmd.Target;
                if ((mask & bit) != 0) continue;

                mask |= bit;
                found++;

                if (loopTime <= cmd.EndTime)
                    ApplyCmdAtLoopTime(cmd, loopTime);
                else
                    ApplyCmdEndValue(cmd);
            }
        }

        void ApplyCmd(SBSpriteCommand cmd, double time)
        {
            switch (cmd)
            {
                case SBFloatCommand fc:
                    ApplyFloat(fc.Target, time >= fc.EndTime ? fc.EndValue
                        : fc.StartValue + (fc.EndValue - fc.StartValue) * fc.GetProgress(time));
                    break;
                case SBColorCommand cc:
                    if (time >= cc.EndTime)
                    { State.R = cc.EndValue.r / 255f; State.G = cc.EndValue.g / 255f; State.B = cc.EndValue.b / 255f; }
                    else
                    {
                        float p = cc.GetProgress(time);
                        State.R = (cc.StartValue.r + (cc.EndValue.r - cc.StartValue.r) * p) / 255f;
                        State.G = (cc.StartValue.g + (cc.EndValue.g - cc.StartValue.g) * p) / 255f;
                        State.B = (cc.StartValue.b + (cc.EndValue.b - cc.StartValue.b) * p) / 255f;
                    }
                    break;
                case SBBoolCommand bc:
                    ApplyBool(bc.Target, time >= bc.EndTime ? bc.EndValue : bc.StartValue);
                    break;
            }
        }

        void ApplyCmdAtLoopTime(SBSpriteCommand cmd, double loopTime)
        {
            switch (cmd)
            {
                case SBFloatCommand fc:
                    ApplyFloat(fc.Target, loopTime >= fc.EndTime ? fc.EndValue
                        : fc.StartValue + (fc.EndValue - fc.StartValue) * cmd.GetProgress(loopTime));
                    break;
                case SBColorCommand cc:
                    if (loopTime >= cc.EndTime)
                    { State.R = cc.EndValue.r / 255f; State.G = cc.EndValue.g / 255f; State.B = cc.EndValue.b / 255f; }
                    else
                    {
                        float p = cmd.GetProgress(loopTime);
                        State.R = (cc.StartValue.r + (cc.EndValue.r - cc.StartValue.r) * p) / 255f;
                        State.G = (cc.StartValue.g + (cc.EndValue.g - cc.StartValue.g) * p) / 255f;
                        State.B = (cc.StartValue.b + (cc.EndValue.b - cc.StartValue.b) * p) / 255f;
                    }
                    break;
                case SBBoolCommand bc:
                    ApplyBool(bc.Target, loopTime >= bc.EndTime ? bc.EndValue : bc.StartValue);
                    break;
            }
        }

        void ApplyCmdEndValue(SBSpriteCommand cmd)
        {
            switch (cmd)
            {
                case SBFloatCommand fc:
                    ApplyFloat(fc.Target, fc.EndValue);
                    break;
                case SBColorCommand cc:
                    State.R = cc.EndValue.r / 255f;
                    State.G = cc.EndValue.g / 255f;
                    State.B = cc.EndValue.b / 255f;
                    break;
                case SBBoolCommand bc:
                    ApplyBool(bc.Target, bc.EndValue);
                    break;
            }
        }

        void ApplyFloat(SBCommandTarget target, float value)
        {
            switch (target)
            {
                case SBCommandTarget.Alpha: State.Alpha = value; break;
                case SBCommandTarget.X: State.X = value; break;
                case SBCommandTarget.Y: State.Y = value; break;
                case SBCommandTarget.ScaleX: State.ScaleX = value; break;
                case SBCommandTarget.ScaleY: State.ScaleY = value; break;
                case SBCommandTarget.Rotation: State.Rotation = value; break;
            }
        }

        void ApplyBool(SBCommandTarget target, bool value)
        {
            switch (target)
            {
                case SBCommandTarget.BlendingMode: State.Additive = value; break;
                case SBCommandTarget.FlipH: State.FlipH = value; break;
                case SBCommandTarget.FlipV: State.FlipV = value; break;
            }
        }
    }
}
