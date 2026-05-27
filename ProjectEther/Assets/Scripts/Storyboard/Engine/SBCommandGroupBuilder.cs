using UnityEngine;
using OsuVR.Storyboard.Data;

namespace OsuVR.Storyboard.Engine
{
    /// <summary>
    /// 转换层: SBElement (解析器输出) → SBCommandGroup (引擎输入)
    /// 处理 M→X+Y, S→ScaleX+ScaleY, P→Bool, Loop→SBLoopCommand
    /// </summary>
    public static class SBCommandGroupBuilder
    {
        /// <summary>
        /// 将 SBElement 的所有命令转换为一个扁平化、排序后的 SBCommandGroup
        /// </summary>
        public static SBCommandGroup Build(SBElement element)
        {
            // 估算容量: 每个命令最多展开为 2 个 (M, S)
            int estimate = element.FadeCommands.Count
                + element.MoveCommands.Count * 2
                + element.MoveXCommands.Count
                + element.MoveYCommands.Count
                + element.ScaleCommands.Count * 2
                + element.RotateCommands.Count
                + element.ColorCommands.Count
                + element.ParameterCommands.Count
                + element.Loops.Count;

            var group = new SBCommandGroup(estimate);

            // Fade → Alpha
            for (int i = 0; i < element.FadeCommands.Count; i++)
            {
                var cmd = element.FadeCommands[i];
                group.Commands.Add(new SBFloatCommand(
                    SBCommandTarget.Alpha, cmd.Easing, cmd.StartTime, cmd.EndTime,
                    cmd.StartValue, cmd.EndValue));
            }

            // Move → X + Y
            for (int i = 0; i < element.MoveCommands.Count; i++)
            {
                var cmd = element.MoveCommands[i];
                group.Commands.Add(new SBFloatCommand(
                    SBCommandTarget.X, cmd.Easing, cmd.StartTime, cmd.EndTime,
                    cmd.StartPos.x, cmd.EndPos.x));
                group.Commands.Add(new SBFloatCommand(
                    SBCommandTarget.Y, cmd.Easing, cmd.StartTime, cmd.EndTime,
                    cmd.StartPos.y, cmd.EndPos.y));
            }

            // MoveX → X
            for (int i = 0; i < element.MoveXCommands.Count; i++)
            {
                var cmd = element.MoveXCommands[i];
                group.Commands.Add(new SBFloatCommand(
                    SBCommandTarget.X, cmd.Easing, cmd.StartTime, cmd.EndTime,
                    cmd.StartValue, cmd.EndValue));
            }

            // MoveY → Y
            for (int i = 0; i < element.MoveYCommands.Count; i++)
            {
                var cmd = element.MoveYCommands[i];
                group.Commands.Add(new SBFloatCommand(
                    SBCommandTarget.Y, cmd.Easing, cmd.StartTime, cmd.EndTime,
                    cmd.StartValue, cmd.EndValue));
            }

            // Scale → ScaleX + ScaleY
            for (int i = 0; i < element.ScaleCommands.Count; i++)
            {
                var cmd = element.ScaleCommands[i];
                group.Commands.Add(new SBFloatCommand(
                    SBCommandTarget.ScaleX, cmd.Easing, cmd.StartTime, cmd.EndTime,
                    cmd.StartValue, cmd.EndValue));
                group.Commands.Add(new SBFloatCommand(
                    SBCommandTarget.ScaleY, cmd.Easing, cmd.StartTime, cmd.EndTime,
                    cmd.StartValue, cmd.EndValue));
            }

            // Rotate → Rotation
            for (int i = 0; i < element.RotateCommands.Count; i++)
            {
                var cmd = element.RotateCommands[i];
                group.Commands.Add(new SBFloatCommand(
                    SBCommandTarget.Rotation, cmd.Easing, cmd.StartTime, cmd.EndTime,
                    cmd.StartValue, cmd.EndValue));
            }

            // Color → Color
            for (int i = 0; i < element.ColorCommands.Count; i++)
            {
                var cmd = element.ColorCommands[i];
                group.Commands.Add(new SBColorCommand(
                    SBCommandTarget.Color, cmd.Easing, cmd.StartTime, cmd.EndTime,
                    cmd.StartColor, cmd.EndColor));
            }

            // Parameter → Bool (P,H → FlipH; P,V → FlipV; P,A → BlendingMode)
            for (int i = 0; i < element.ParameterCommands.Count; i++)
            {
                var cmd = element.ParameterCommands[i];
                SBCommandTarget target;
                switch (cmd.Parameter)
                {
                    case "H": target = SBCommandTarget.FlipH; break;
                    case "V": target = SBCommandTarget.FlipV; break;
                    case "A": target = SBCommandTarget.BlendingMode; break;
                    default: continue;
                }
                group.Commands.Add(new SBBoolCommand(
                    target, cmd.Easing, cmd.StartTime, cmd.EndTime, true, false));
            }

            // Loops → SBLoopCommand
            for (int i = 0; i < element.Loops.Count; i++)
            {
                var loop = element.Loops[i];
                var innerGroup = BuildLoopInnerGroup(loop);
                group.Commands.Add(new SBLoopCommand(loop.StartTime, loop.LoopCount, innerGroup));
            }

            // 展开 Loop + 排序
            group.ToFlatGroup();
            group.Sort();

            return group;
        }

        static SBCommandGroup BuildLoopInnerGroup(SBLoop loop)
        {
            int estimate = loop.FadeCommands.Count
                + loop.MoveCommands.Count * 2
                + loop.MoveXCommands.Count
                + loop.MoveYCommands.Count
                + loop.ScaleCommands.Count * 2
                + loop.RotateCommands.Count
                + loop.ColorCommands.Count;

            var group = new SBCommandGroup(estimate);

            for (int i = 0; i < loop.FadeCommands.Count; i++)
            {
                var cmd = loop.FadeCommands[i];
                group.Commands.Add(new SBFloatCommand(
                    SBCommandTarget.Alpha, cmd.Easing, cmd.StartTime, cmd.EndTime,
                    cmd.StartValue, cmd.EndValue));
            }

            for (int i = 0; i < loop.MoveCommands.Count; i++)
            {
                var cmd = loop.MoveCommands[i];
                group.Commands.Add(new SBFloatCommand(
                    SBCommandTarget.X, cmd.Easing, cmd.StartTime, cmd.EndTime,
                    cmd.StartPos.x, cmd.EndPos.x));
                group.Commands.Add(new SBFloatCommand(
                    SBCommandTarget.Y, cmd.Easing, cmd.StartTime, cmd.EndTime,
                    cmd.StartPos.y, cmd.EndPos.y));
            }

            for (int i = 0; i < loop.MoveXCommands.Count; i++)
            {
                var cmd = loop.MoveXCommands[i];
                group.Commands.Add(new SBFloatCommand(
                    SBCommandTarget.X, cmd.Easing, cmd.StartTime, cmd.EndTime,
                    cmd.StartValue, cmd.EndValue));
            }

            for (int i = 0; i < loop.MoveYCommands.Count; i++)
            {
                var cmd = loop.MoveYCommands[i];
                group.Commands.Add(new SBFloatCommand(
                    SBCommandTarget.Y, cmd.Easing, cmd.StartTime, cmd.EndTime,
                    cmd.StartValue, cmd.EndValue));
            }

            for (int i = 0; i < loop.ScaleCommands.Count; i++)
            {
                var cmd = loop.ScaleCommands[i];
                group.Commands.Add(new SBFloatCommand(
                    SBCommandTarget.ScaleX, cmd.Easing, cmd.StartTime, cmd.EndTime,
                    cmd.StartValue, cmd.EndValue));
                group.Commands.Add(new SBFloatCommand(
                    SBCommandTarget.ScaleY, cmd.Easing, cmd.StartTime, cmd.EndTime,
                    cmd.StartValue, cmd.EndValue));
            }

            for (int i = 0; i < loop.RotateCommands.Count; i++)
            {
                var cmd = loop.RotateCommands[i];
                group.Commands.Add(new SBFloatCommand(
                    SBCommandTarget.Rotation, cmd.Easing, cmd.StartTime, cmd.EndTime,
                    cmd.StartValue, cmd.EndValue));
            }

            for (int i = 0; i < loop.ColorCommands.Count; i++)
            {
                var cmd = loop.ColorCommands[i];
                group.Commands.Add(new SBColorCommand(
                    SBCommandTarget.Color, cmd.Easing, cmd.StartTime, cmd.EndTime,
                    cmd.StartColor, cmd.EndColor));
            }

            return group;
        }
    }
}
