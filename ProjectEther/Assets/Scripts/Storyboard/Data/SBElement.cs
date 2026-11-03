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
        public List<SBScaleVectorCommand> ScaleVectorCommands = new List<SBScaleVectorCommand>();
        public List<SBRotateCommand> RotateCommands = new List<SBRotateCommand>();
        public List<SBColorCommand> ColorCommands = new List<SBColorCommand>();
        public List<SBParameterCommand> ParameterCommands = new List<SBParameterCommand>();

        /// <summary>
        /// Loop 和 Trigger 容器
        /// </summary>
        public List<SBLoop> Loops = new List<SBLoop>();
        public List<SBTrigger> Triggers = new List<SBTrigger>();

        protected SBElement(SBLayer layer, SBOrigin origin, string imagePath, Vector2 position)
        {
            Layer = layer;
            Origin = origin;
            ImagePath = imagePath;
            InitialPosition = position;
        }
    }

    /// <summary>
    /// 静态 Sprite 元素
    /// </summary>
    public class SBStoryboardSprite : SBElement
    {
        public SBStoryboardSprite(
            SBLayer layer,
            SBOrigin origin,
            string imagePath,
            Vector2 position
        )
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

        public SBStoryboardAnimation(
            SBLayer layer,
            SBOrigin origin,
            string imagePath,
            Vector2 position,
            int frameCount,
            double frameDelay,
            SBAnimationLoopType loopType
        )
            : base(layer, origin, imagePath, position)
        {
            FrameCount = frameCount;
            FrameDelay = frameDelay;
            LoopType = loopType;
        }

        /// <summary>
        /// 构建第 frameIndex 帧的图片路径 (osu! 规范: 将文件名末尾数字替换)
        /// </summary>
        public string BuildFramePath(int frameIndex)
        {
            int dotIdx = ImagePath.LastIndexOf('.');
            if (dotIdx < 0)
                return ImagePath + frameIndex;
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
        public List<SBScaleVectorCommand> ScaleVectorCommands = new List<SBScaleVectorCommand>();
        public List<SBRotateCommand> RotateCommands = new List<SBRotateCommand>();
        public List<SBColorCommand> ColorCommands = new List<SBColorCommand>();
        public List<SBParameterCommand> ParameterCommands = new List<SBParameterCommand>();

        public SBLoop(double startTime, int loopCount)
        {
            StartTime = startTime;
            LoopCount = loopCount;
        }
    }

    /// <summary>
    /// Trigger 命令组 (hitSound, passing 等触发器，当前阶段仅存储，不执行)
    /// </summary>
    public class SBTrigger
    {
        public int GroupNumber;
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
