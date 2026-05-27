using System.Collections.Generic;
using UnityEngine;

namespace OsuVR.Storyboard.Data
{
    /// <summary>
    /// Storyboard 图层 (与 osu! 标准一致)
    /// 值的顺序决定渲染先后：Background 最先绘制 (最底层), Overlay 最后绘制 (最顶层)
    /// </summary>
    public enum SBLayer
    {
        Background = 0,
        Fail = 1,
        Pass = 2,
        Foreground = 3,
        Overlay = 4,
    }

    /// <summary>
    /// 锚点/原点 (与 osu! 标准一致)
    /// </summary>
    public enum SBOrigin
    {
        TopLeft = 0,
        Centre = 1,
        CentreLeft = 2,
        TopRight = 3,
        BottomCentre = 4,
        TopCentre = 5,
        Custom = 6,
        CentreRight = 7,
        BottomLeft = 8,
        BottomRight = 9,
    }

    /// <summary>
    /// 混合模式
    /// </summary>
    public enum SBBlendMode
    {
        Normal = 0,
        Additive = 1,
    }

    /// <summary>
    /// Storyboard 元素基类：代表一个可渲染的 SB 对象
    /// </summary>
    public abstract class SBElement
    {
        public SBLayer Layer;
        public SBOrigin Origin;
        public string ImagePath;
        public Vector2 InitialPosition;

        /// <summary>
        /// 所有命令按类型分组存储，每组按 StartTime 排序
        /// </summary>
        public List<SBFadeCommand> FadeCommands = new List<SBFadeCommand>();
        public List<SBMoveCommand> MoveCommands = new List<SBMoveCommand>();
        public List<SBMoveAxisCommand> MoveXCommands = new List<SBMoveAxisCommand>();
        public List<SBMoveAxisCommand> MoveYCommands = new List<SBMoveAxisCommand>();
        public List<SBScaleCommand> ScaleCommands = new List<SBScaleCommand>();
        public List<SBRotateCommand> RotateCommands = new List<SBRotateCommand>();
        public List<SBColorCommand> ColorCommands = new List<SBColorCommand>();
        public List<SBParameterCommand> ParameterCommands = new List<SBParameterCommand>();

        /// <summary>
        /// Loop 和 Trigger 容器
        /// </summary>
        public List<SBLoop> Loops = new List<SBLoop>();
        public List<SBTrigger> Triggers = new List<SBTrigger>();

        /// <summary>
        /// 当前时间的渲染状态快照 (由 Evaluate 计算)
        /// </summary>
        public float CurrentAlpha = 1f;
        public Vector2 CurrentPosition;
        public float CurrentScale = 1f;
        public float CurrentRotation;
        public Color CurrentColor = Color.white;
        public bool FlipH;
        public bool FlipV;
        public SBBlendMode BlendMode;

        protected SBElement(SBLayer layer, SBOrigin origin, string imagePath, Vector2 position)
        {
            Layer = layer;
            Origin = origin;
            ImagePath = imagePath;
            InitialPosition = position;
            CurrentPosition = position;
        }

        /// <summary>
        /// 评估当前时间点所有活跃命令，更新渲染状态
        /// </summary>
        public virtual void Evaluate(double currentTime)
        {
            // 按优先级逐属性评估 (osu! 规范: 后定义的命令在同一时间点优先)
            CurrentPosition = InitialPosition;
            CurrentScale = 1f;
            CurrentRotation = 0f;
            CurrentAlpha = 1f;
            CurrentColor = Color.white;
            FlipH = false;
            FlipV = false;
            BlendMode = SBBlendMode.Normal;

            // 评估所有命令类型 (从后往前遍历，最后定义的优先)
            EvaluateFade(currentTime);
            EvaluateMove(currentTime);
            EvaluateMoveX(currentTime);
            EvaluateMoveY(currentTime);
            EvaluateScale(currentTime);
            EvaluateRotate(currentTime);
            EvaluateColor(currentTime);
            EvaluateParameters(currentTime);

            // 评估 Loop 命令
            foreach (var loop in Loops)
                loop.Evaluate(currentTime, this);
        }

        void EvaluateFade(double time)
        {
            for (int i = FadeCommands.Count - 1; i >= 0; i--)
            {
                var cmd = FadeCommands[i];
                if (time >= cmd.StartTime)
                {
                    CurrentAlpha = cmd.Evaluate(time);
                    return;
                }
            }
        }

        void EvaluateMove(double time)
        {
            for (int i = MoveCommands.Count - 1; i >= 0; i--)
            {
                var cmd = MoveCommands[i];
                if (time >= cmd.StartTime)
                {
                    CurrentPosition = cmd.Evaluate(time);
                    return;
                }
            }
        }

        void EvaluateMoveX(double time)
        {
            for (int i = MoveXCommands.Count - 1; i >= 0; i--)
            {
                var cmd = MoveXCommands[i];
                if (time >= cmd.StartTime)
                {
                    CurrentPosition = new Vector2(cmd.Evaluate(time), CurrentPosition.y);
                    return;
                }
            }
        }

        void EvaluateMoveY(double time)
        {
            for (int i = MoveYCommands.Count - 1; i >= 0; i--)
            {
                var cmd = MoveYCommands[i];
                if (time >= cmd.StartTime)
                {
                    CurrentPosition = new Vector2(CurrentPosition.x, cmd.Evaluate(time));
                    return;
                }
            }
        }

        void EvaluateScale(double time)
        {
            for (int i = ScaleCommands.Count - 1; i >= 0; i--)
            {
                var cmd = ScaleCommands[i];
                if (time >= cmd.StartTime)
                {
                    CurrentScale = cmd.Evaluate(time);
                    return;
                }
            }
        }

        void EvaluateRotate(double time)
        {
            for (int i = RotateCommands.Count - 1; i >= 0; i--)
            {
                var cmd = RotateCommands[i];
                if (time >= cmd.StartTime)
                {
                    CurrentRotation = cmd.Evaluate(time);
                    return;
                }
            }
        }

        void EvaluateColor(double time)
        {
            for (int i = ColorCommands.Count - 1; i >= 0; i--)
            {
                var cmd = ColorCommands[i];
                if (time >= cmd.StartTime)
                {
                    CurrentColor = cmd.Evaluate(time);
                    return;
                }
            }
        }

        void EvaluateParameters(double time)
        {
            for (int i = ParameterCommands.Count - 1; i >= 0; i--)
            {
                var cmd = ParameterCommands[i];
                if (time >= cmd.StartTime)
                {
                    switch (cmd.Parameter)
                    {
                        case "H": FlipH = true; break;
                        case "V": FlipV = true; break;
                        case "A": BlendMode = SBBlendMode.Additive; break;
                    }
                }
            }
        }
    }

    /// <summary>
    /// 静态 Sprite 元素
    /// </summary>
    public class SBStoryboardSprite : SBElement
    {
        public SBStoryboardSprite(SBLayer layer, SBOrigin origin, string imagePath, Vector2 position)
            : base(layer, origin, imagePath, position) { }
    }

    /// <summary>
    /// 动画 Sprite 元素 (帧序列)
    /// </summary>
    public class SBStoryboardAnimation : SBElement
    {
        public int FrameCount;
        public double FrameDelay;
        public SBAnimationLoopType LoopType;

        public SBStoryboardAnimation(SBLayer layer, SBOrigin origin, string imagePath,
            Vector2 position, int frameCount, double frameDelay, SBAnimationLoopType loopType)
            : base(layer, origin, imagePath, position)
        {
            FrameCount = frameCount;
            FrameDelay = frameDelay;
            LoopType = loopType;
        }

        /// <summary>
        /// 根据时间计算当前显示的帧索引
        /// </summary>
        public int GetCurrentFrame(double currentTime, double elementStartTime)
        {
            if (FrameCount <= 0 || FrameDelay <= 0) return 0;
            double elapsed = currentTime - elementStartTime;
            int frame = (int)(elapsed / FrameDelay);
            if (LoopType == SBAnimationLoopType.LoopForever)
                frame = frame % FrameCount;
            return Mathf.Clamp(frame, 0, FrameCount - 1);
        }

        /// <summary>
        /// 构建第 frameIndex 帧的图片路径 (osu! 规范: 将文件名末尾数字替换)
        /// </summary>
        public string BuildFramePath(int frameIndex)
        {
            int dotIdx = ImagePath.LastIndexOf('.');
            if (dotIdx < 0) return ImagePath + frameIndex;
            return ImagePath.Substring(0, dotIdx) + frameIndex + ImagePath.Substring(dotIdx);
        }
    }

    public enum SBAnimationLoopType
    {
        LoopForever = 0,
        LoopOnce = 1,
    }

    // =====================================================
    //  Loop 和 Trigger 容器
    // =====================================================

    /// <summary>
    /// Loop 命令组：从 startTime 开始重复执行 offsetTime~offsetTime+duration 的命令
    /// </summary>
    public class SBLoop
    {
        public double StartTime;
        public int LoopCount;
        public List<SBFadeCommand> FadeCommands = new List<SBFadeCommand>();
        public List<SBMoveCommand> MoveCommands = new List<SBMoveCommand>();
        public List<SBMoveAxisCommand> MoveXCommands = new List<SBMoveAxisCommand>();
        public List<SBMoveAxisCommand> MoveYCommands = new List<SBMoveAxisCommand>();
        public List<SBScaleCommand> ScaleCommands = new List<SBScaleCommand>();
        public List<SBRotateCommand> RotateCommands = new List<SBRotateCommand>();
        public List<SBColorCommand> ColorCommands = new List<SBColorCommand>();

        public SBLoop(double startTime, int loopCount)
        {
            StartTime = startTime;
            LoopCount = loopCount;
        }

        /// <summary>
        /// Loop 的总持续时间 (最晚命令的 EndTime 相对于 Loop StartTime 的偏移)
        /// </summary>
        public double Duration
        {
            get
            {
                double max = 0;
                void CheckMax<T>(List<T> cmds) where T : SBCommand
                {
                    foreach (var c in cmds)
                    {
                        double end = c.EndTime - StartTime;
                        if (end > max) max = end;
                    }
                }
                CheckMax(FadeCommands);
                CheckMax(MoveCommands);
                CheckMax(MoveXCommands);
                CheckMax(MoveYCommands);
                CheckMax(ScaleCommands);
                CheckMax(RotateCommands);
                CheckMax(ColorCommands);
                return max > 0 ? max : 1;
            }
        }

        public void Evaluate(double currentTime, SBElement element)
        {
            if (currentTime < StartTime) return;
            double elapsed = currentTime - StartTime;
            double loopDur = Duration;
            if (loopDur <= 0) return;

            int loopIndex = (int)(elapsed / loopDur);
            if (LoopCount > 0 && loopIndex >= LoopCount) return;

            double loopTime = StartTime + (elapsed % loopDur);

            // 在 loop 时间点评估 loop 内的命令
            for (int i = FadeCommands.Count - 1; i >= 0; i--)
            {
                var cmd = FadeCommands[i];
                if (loopTime >= cmd.StartTime && loopTime <= cmd.EndTime)
                {
                    element.CurrentAlpha = cmd.Evaluate(loopTime);
                    break;
                }
            }
            for (int i = MoveCommands.Count - 1; i >= 0; i--)
            {
                var cmd = MoveCommands[i];
                if (loopTime >= cmd.StartTime && loopTime <= cmd.EndTime)
                {
                    element.CurrentPosition = cmd.Evaluate(loopTime);
                    break;
                }
            }
            for (int i = ScaleCommands.Count - 1; i >= 0; i--)
            {
                var cmd = ScaleCommands[i];
                if (loopTime >= cmd.StartTime && loopTime <= cmd.EndTime)
                {
                    element.CurrentScale = cmd.Evaluate(loopTime);
                    break;
                }
            }
            for (int i = RotateCommands.Count - 1; i >= 0; i--)
            {
                var cmd = RotateCommands[i];
                if (loopTime >= cmd.StartTime && loopTime <= cmd.EndTime)
                {
                    element.CurrentRotation = cmd.Evaluate(loopTime);
                    break;
                }
            }
            for (int i = ColorCommands.Count - 1; i >= 0; i--)
            {
                var cmd = ColorCommands[i];
                if (loopTime >= cmd.StartTime && loopTime <= cmd.EndTime)
                {
                    element.CurrentColor = cmd.Evaluate(loopTime);
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Trigger 命令组 (hitSound, passing 等触发器，当前阶段仅存储，不执行)
    /// </summary>
    public class SBTrigger
    {
        public string TriggerName;
        public double StartTime;
        public double EndTime;
        public List<SBCommand> Commands = new List<SBCommand>();

        public SBTrigger(string name, double startTime, double endTime)
        {
            TriggerName = name;
            StartTime = startTime;
            EndTime = endTime;
        }
    }
}
