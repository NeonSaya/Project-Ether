using System.Collections.Generic;
using OsuVR.Storyboard.Data;
using UnityEngine;

namespace OsuVR.Storyboard.Engine
{
    /// <summary>把旧式命令映射到 lazer 中相互独立的 transform 属性。</summary>
    public static class SBCommandGroupBuilder
    {
        public static SBCommandGroup Build(SBElement element)
        {
            var group = new SBCommandGroup();
            AddCommands(
                group,
                element.FadeCommands,
                element.MoveCommands,
                element.MoveXCommands,
                element.MoveYCommands,
                element.ScaleCommands,
                element.ScaleVectorCommands,
                element.RotateCommands,
                element.ColorCommands,
                element.ParameterCommands
            );
            foreach (var loop in element.Loops)
                group.Commands.Add(
                    new SBLoopCommand(loop.StartTime, loop.LoopCount, BuildLoopInnerGroup(loop))
                );
            group.Sort();
            return group;
        }

        public static SBCommandGroup BuildLoopInnerGroup(SBLoop loop)
        {
            var group = new SBCommandGroup();
            AddCommands(
                group,
                loop.FadeCommands,
                loop.MoveCommands,
                loop.MoveXCommands,
                loop.MoveYCommands,
                loop.ScaleCommands,
                loop.ScaleVectorCommands,
                loop.RotateCommands,
                loop.ColorCommands,
                loop.ParameterCommands
            );
            group.Sort();
            return group;
        }

        public static SBCommandGroup BuildTriggerGroup(SBTrigger trigger)
        {
            var group = new SBCommandGroup(trigger.Commands.Count * 2);
            foreach (var command in trigger.Commands)
                AddCommand(group, command);
            group.Sort();
            return group;
        }

        static void AddCommands(
            SBCommandGroup group,
            List<SBFadeCommand> fade,
            List<SBMoveCommand> move,
            List<SBMoveAxisCommand> moveX,
            List<SBMoveAxisCommand> moveY,
            List<SBScaleCommand> scale,
            List<SBScaleVectorCommand> vectorScale,
            List<SBRotateCommand> rotate,
            List<Data.SBColorCommand> color,
            List<SBParameterCommand> parameters
        )
        {
            foreach (var c in fade)
                AddCommand(group, c);
            foreach (var c in move)
                AddCommand(group, c);
            foreach (var c in moveX)
                AddCommand(group, c);
            foreach (var c in moveY)
                AddCommand(group, c);
            foreach (var c in scale)
                AddCommand(group, c);
            foreach (var c in vectorScale)
                AddCommand(group, c);
            foreach (var c in rotate)
                AddCommand(group, c);
            foreach (var c in color)
                AddCommand(group, c);
            foreach (var c in parameters)
                AddCommand(group, c);
        }

        static void AddFloat(
            SBCommandGroup group,
            SBCommand c,
            SBCommandTarget target,
            float start,
            float end
        )
        {
            group.Commands.Add(
                new SBFloatCommand(target, c.Easing, c.StartTime, c.EndTime, start, end)
                {
                    Sequence = c.Sequence,
                }
            );
        }

        static void AddCommand(SBCommandGroup group, SBCommand command)
        {
            switch (command)
            {
                case SBFadeCommand c:
                    AddFloat(group, c, SBCommandTarget.Alpha, c.StartValue, c.EndValue);
                    break;
                case SBMoveCommand c:
                    AddFloat(group, c, SBCommandTarget.X, c.StartPos.x, c.EndPos.x);
                    AddFloat(group, c, SBCommandTarget.Y, c.StartPos.y, c.EndPos.y);
                    break;
                case SBMoveAxisCommand c:
                    AddFloat(
                        group,
                        c,
                        c.Type == SBCommandType.MX ? SBCommandTarget.X : SBCommandTarget.Y,
                        c.StartValue,
                        c.EndValue
                    );
                    break;
                case SBScaleCommand c:
                    AddFloat(group, c, SBCommandTarget.UniformScale, c.StartValue, c.EndValue);
                    break;
                case SBScaleVectorCommand c:
                    AddFloat(group, c, SBCommandTarget.VectorScaleX, c.StartValueX, c.EndValueX);
                    AddFloat(group, c, SBCommandTarget.VectorScaleY, c.StartValueY, c.EndValueY);
                    break;
                case SBRotateCommand c:
                    AddFloat(group, c, SBCommandTarget.Rotation, c.StartValue, c.EndValue);
                    break;
                case Data.SBColorCommand c:
                    group.Commands.Add(
                        new SBColorCommand(
                            SBCommandTarget.Color,
                            c.Easing,
                            c.StartTime,
                            c.EndTime,
                            c.StartColor,
                            c.EndColor
                        )
                        {
                            Sequence = c.Sequence,
                        }
                    );
                    break;
                case SBParameterCommand c:
                    SBCommandTarget target;
                    switch (c.Parameter)
                    {
                        case "A":
                            target = SBCommandTarget.BlendingMode;
                            break;
                        case "H":
                            target = SBCommandTarget.FlipH;
                            break;
                        case "V":
                            target = SBCommandTarget.FlipV;
                            break;
                        default:
                            return;
                    }
                    group.Commands.Add(
                        new SBBoolCommand(
                            target,
                            c.Easing,
                            c.StartTime,
                            c.EndTime,
                            true,
                            c.StartTime == c.EndTime
                        )
                        {
                            Sequence = c.Sequence,
                        }
                    );
                    break;
            }
        }
    }
}
