using OsuVR.Storyboard.Data;
using UnityEngine;

namespace OsuVR.Storyboard.Engine
{
    /// <summary>
    /// Storyboard 命令基类 (对应 osu!droid SpriteCommand)
    /// 一个从 StartTime 到 EndTime 的带缓动插值动画
    /// </summary>
    public abstract class SBSpriteCommand
    {
        public int Sequence;
        public double StartTime;
        public double EndTime;
        public SBEasing Easing;
        public SBCommandTarget Target;

        protected SBSpriteCommand(
            SBCommandTarget target,
            SBEasing easing,
            double startTime,
            double endTime
        )
        {
            Target = target;
            Easing = easing;
            StartTime = startTime;
            EndTime = System.Math.Max(startTime, endTime);
        }

        /// <summary>
        /// 创建时间偏移后的副本 (用于 Loop 展开)
        /// </summary>
        public abstract SBSpriteCommand CreateOffsetCommand(double offset);
    }

    /// <summary>
    /// 浮点命令: Alpha, X, Y, ScaleX, ScaleY, Rotation
    /// </summary>
    public class SBFloatCommand : SBSpriteCommand
    {
        public float StartValue;
        public float EndValue;

        public SBFloatCommand(
            SBCommandTarget target,
            SBEasing easing,
            double startTime,
            double endTime,
            float startValue,
            float endValue
        )
            : base(target, easing, startTime, endTime)
        {
            StartValue = startValue;
            EndValue = endValue;
        }

        public override SBSpriteCommand CreateOffsetCommand(double offset)
        {
            return new SBFloatCommand(
                Target,
                Easing,
                StartTime + offset,
                EndTime + offset,
                StartValue,
                EndValue
            )
            {
                Sequence = Sequence,
            };
        }
    }

    /// <summary>
    /// 颜色命令: Color (RGB, 0-255)
    /// </summary>
    public class SBColorCommand : SBSpriteCommand
    {
        public Color StartValue;
        public Color EndValue;

        public SBColorCommand(
            SBCommandTarget target,
            SBEasing easing,
            double startTime,
            double endTime,
            Color startValue,
            Color endValue
        )
            : base(target, easing, startTime, endTime)
        {
            StartValue = startValue;
            EndValue = endValue;
        }

        public override SBSpriteCommand CreateOffsetCommand(double offset)
        {
            return new SBColorCommand(
                Target,
                Easing,
                StartTime + offset,
                EndTime + offset,
                StartValue,
                EndValue
            )
            {
                Sequence = Sequence,
            };
        }
    }

    /// <summary>
    /// 布尔命令: BlendingMode(加法混合), FlipH, FlipV
    /// </summary>
    public class SBBoolCommand : SBSpriteCommand
    {
        public bool UseInitialValue;
        public bool Suppressed,
            ResetSuppressed;
        public bool StartValue;
        public bool EndValue;

        public SBBoolCommand(
            SBCommandTarget target,
            SBEasing easing,
            double startTime,
            double endTime,
            bool startValue,
            bool endValue
        )
            : base(target, easing, startTime, endTime)
        {
            UseInitialValue = startTime == endTime;
            StartValue = startValue;
            EndValue = endValue;
        }

        public override SBSpriteCommand CreateOffsetCommand(double offset)
        {
            return new SBBoolCommand(
                Target,
                Easing,
                StartTime + offset,
                EndTime + offset,
                StartValue,
                EndValue
            )
            {
                Sequence = Sequence,
                UseInitialValue = UseInitialValue,
                Suppressed = Suppressed,
                ResetSuppressed = ResetSuppressed,
            };
        }
    }

    /// <summary>
    /// Loop 容器命令 (对应 osu!droid CommandLoop)
    /// 保存内层 CommandGroup，由 SBTimelineFlattener 转换为扁平数据。
    /// </summary>
    public class SBLoopCommand : SBSpriteCommand
    {
        public SBCommandGroup InnerGroup;
        public int LoopCount;
        public double LoopDuration; // 内层命令的时间跨度

        public SBLoopCommand(double startTime, int loopCount, SBCommandGroup innerGroup)
            : base(SBCommandTarget.Alpha, SBEasing.Linear, startTime, 0)
        {
            // 解析器保存的是总迭代次数（文件中的重复次数）。重复次数为 0 时，
            // lazer 会把它归一化为播放一次。
            LoopCount = loopCount <= 0 ? 1 : loopCount;
            InnerGroup = innerGroup;
            double first = innerGroup.Commands.Count == 0 ? 0 : innerGroup.StartTime();
            LoopDuration =
                innerGroup.Commands.Count == 0
                    ? 0
                    : System.Math.Max(0, innerGroup.EndTime() - first);
            EndTime = StartTime + first + LoopCount * LoopDuration;
        }

        public override SBSpriteCommand CreateOffsetCommand(double offset)
        {
            return new SBLoopCommand(StartTime + offset, LoopCount, InnerGroup);
        }
    }
}
