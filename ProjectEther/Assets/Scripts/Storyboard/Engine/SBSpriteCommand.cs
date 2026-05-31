using UnityEngine;
using OsuVR.Storyboard.Data;

namespace OsuVR.Storyboard.Engine
{
    /// <summary>
    /// Storyboard 命令基类 (对应 osu!droid SpriteCommand)
    /// 一个从 StartTime 到 EndTime 的带缓动插值动画
    /// </summary>
    public abstract class SBSpriteCommand
    {
        public double StartTime;
        public double EndTime;
        public SBEasing Easing;
        public SBCommandTarget Target;

        protected SBSpriteCommand(SBCommandTarget target, SBEasing easing, double startTime, double endTime)
        {
            Target = target;
            Easing = easing;
            StartTime = startTime;
            EndTime = endTime;
        }

        /// <summary>
        /// 计算经过缓动后的归一化进度 [0,1]
        /// </summary>
        public float GetProgress(double time)
        {
            if (StartTime == EndTime)
                return time < StartTime ? 0f : 1f;
            if (time < StartTime) return 0f;
            if (time > EndTime) return 1f;
            return EasingMath.Interpolate(Easing, (float)((time - StartTime) / (EndTime - StartTime)));
        }

        /// <summary>
        /// 创建时间偏移后的副本 (用于 Loop 展开)
        /// </summary>
        public abstract SBSpriteCommand CreateOffsetCommand(double offset);

        /// <summary>
        /// 创建 "hold" 命令：将 EndValue 保持在 [holdStart, holdEnd] 区间
        /// 用于 Loop 展开优化——超出展开限制的迭代用 hold 覆盖
        /// </summary>
        public abstract SBSpriteCommand CreateHoldCommand(double holdStart, double holdEnd);
    }

    /// <summary>
    /// 浮点命令: Alpha, X, Y, ScaleX, ScaleY, Rotation
    /// </summary>
    public class SBFloatCommand : SBSpriteCommand
    {
        public float StartValue;
        public float EndValue;

        public SBFloatCommand(SBCommandTarget target, SBEasing easing, double startTime, double endTime,
            float startValue, float endValue)
            : base(target, easing, startTime, endTime)
        {
            StartValue = startValue;
            EndValue = endValue;
        }

        public override SBSpriteCommand CreateOffsetCommand(double offset)
        {
            return new SBFloatCommand(Target, Easing, StartTime + offset, EndTime + offset, StartValue, EndValue);
        }

        public override SBSpriteCommand CreateHoldCommand(double holdStart, double holdEnd)
        {
            return new SBFloatCommand(Target, SBEasing.Linear, holdStart, holdEnd, EndValue, EndValue);
        }
    }

    /// <summary>
    /// 颜色命令: Color (RGB, 0-255)
    /// </summary>
    public class SBColorCommand : SBSpriteCommand
    {
        public Color32 StartValue;
        public Color32 EndValue;

        public SBColorCommand(SBCommandTarget target, SBEasing easing, double startTime, double endTime,
            Color32 startValue, Color32 endValue)
            : base(target, easing, startTime, endTime)
        {
            StartValue = startValue;
            EndValue = endValue;
        }

        public override SBSpriteCommand CreateOffsetCommand(double offset)
        {
            return new SBColorCommand(Target, Easing, StartTime + offset, EndTime + offset, StartValue, EndValue);
        }

        public override SBSpriteCommand CreateHoldCommand(double holdStart, double holdEnd)
        {
            return new SBColorCommand(Target, SBEasing.Linear, holdStart, holdEnd, EndValue, EndValue);
        }
    }

    /// <summary>
    /// 布尔命令: BlendingMode(加法混合), FlipH, FlipV
    /// </summary>
    public class SBBoolCommand : SBSpriteCommand
    {
        public bool StartValue;
        public bool EndValue;

        public SBBoolCommand(SBCommandTarget target, SBEasing easing, double startTime, double endTime,
            bool startValue, bool endValue)
            : base(target, easing, startTime, endTime)
        {
            StartValue = startValue;
            EndValue = endValue;
        }

        public override SBSpriteCommand CreateOffsetCommand(double offset)
        {
            return new SBBoolCommand(Target, Easing, StartTime + offset, EndTime + offset, StartValue, EndValue);
        }

        public override SBSpriteCommand CreateHoldCommand(double holdStart, double holdEnd)
        {
            return new SBBoolCommand(Target, SBEasing.Linear, holdStart, holdEnd, EndValue, EndValue);
        }
    }

    /// <summary>
    /// Loop 容器命令 (对应 osu!droid CommandLoop)
    /// 包含一个内层 CommandGroup，在 ToFlatGroup() 时展开为偏移后的普通命令
    /// </summary>
    public class SBLoopCommand : SBSpriteCommand
    {
        public SBCommandGroup InnerGroup;
        public int LoopCount;
        public double LoopDuration; // 内层命令的时间跨度

        public SBLoopCommand(double startTime, int loopCount, SBCommandGroup innerGroup)
            : base(SBCommandTarget.Alpha, SBEasing.Linear, startTime, 0)
        {
            LoopCount = loopCount;
            InnerGroup = innerGroup;
            // 计算内层命令的时间跨度
            LoopDuration = innerGroup.EndTime() - innerGroup.StartTime();
            if (LoopDuration <= 0) LoopDuration = 1;
            // 设置 EndTime 为循环结束时间（用于 Schedule）
            EndTime = StartTime + LoopCount * LoopDuration;
        }

        public override SBSpriteCommand CreateOffsetCommand(double offset)
        {
            return new SBLoopCommand(StartTime + offset, LoopCount, InnerGroup);
        }

        public override SBSpriteCommand CreateHoldCommand(double holdStart, double holdEnd)
        {
            return null; // Loop commands don't need hold
        }
    }
}
