using Unity.Mathematics;

namespace OsuVR.Storyboard.Engine
{
    /// <summary>
    /// 扁平化 Sprite 元数据 (Burst 兼容 blittable struct)
    /// 加载时从 SBElement + SBCommandGroup 一次性构建, 运行时只读
    /// </summary>
    public struct SBSpriteFlatData
    {
        // ---- 初始状态 (命令评估前的默认值) ----
        public float InitX, InitY;
        public float InitAlpha;
        public float InitScaleX, InitScaleY;
        public float InitRotation;
        public float InitR, InitG, InitB;
        public byte InitFlipH, InitFlipV, InitAdditive;

        // ---- 直接命令范围 (索引 into NativeArray<SBCommandFlatData>) ----
        public int CmdOffset;
        public int CmdCount;

        // ---- Loop 范围 (索引 into NativeArray<SBLoopFlatData>) ----
        public int LoopOffset;
        public int LoopCount;

        // ---- 时间范围 (alive 判断) ----
        public double StartTime;
        public double EndTime;

        // ---- 渲染属性 ----
        public int OriginIndex;
        public int TexIndex;       // 静态 sprite 固定纹理索引, 动画 sprite 为 AnimBaseTexIndex
        public int TexWidth, TexHeight;

        // ---- 动画属性 (仅动画 sprite 有效, 非动画时 AnimFrameCount=0) ----
        public int AnimFrameCount;
        public double AnimFrameDelay;
        public int AnimLoopType;   // 0=LoopForever, 1=LoopOnce
        public int AnimBaseTexIndex; // 动画第0帧的纹理索引
    }

    /// <summary>
    /// 扁平化命令数据 (Burst 兼容 blittable struct)
    /// 统一结构: Float/Color/Bool 命令共用同一 struct, 通过 Target 区分值类型
    /// </summary>
    public struct SBCommandFlatData
    {
        public double StartTime;
        public double EndTime;
        public int Easing;         // (int)SBEasing, 用于 Burst 内 switch 分发
        public int Target;         // (int)SBCommandTarget

        // ---- Float 值 (Alpha/X/Y/ScaleX/ScaleY/Rotation) ----
        public float FloatStart;
        public float FloatEnd;

        // ---- Color 值 (RGB 归一化到 0-1) ----
        public float ColorStartR, ColorStartG, ColorStartB;
        public float ColorEndR, ColorEndG, ColorEndB;

        // ---- Bool 值 (FlipH/FlipV/Additive) ----
        public byte BoolStart;
        public byte BoolEnd;
    }

    /// <summary>
    /// 扁平化 Loop 元数据 (Burst 兼容 blittable struct)
    /// Loop 的内层命令存储在 SBCommandFlatData 的连续区段中
    /// </summary>
    public struct SBLoopFlatData
    {
        public double StartTime;
        public double LoopDuration; // 单次迭代时长 (内层命令的最大 EndTime)
        public int LoopCount;       // 总迭代次数 (-1 = 无限)
        public int InnerCmdOffset;  // 内层命令在 SBCommandFlatData 中的起始索引
        public int InnerCmdCount;   // 内层命令数量
    }

    /// <summary>
    /// Burst Job 求值输出: 每个 sprite 的当前帧状态
    /// 由 SBEvaluateTimelineJob 写入, 由 BuildInstanceJob 消费
    /// </summary>
    public struct SpriteInputData
    {
        public float X, Y;
        public float ScaleX, ScaleY;
        public float Rotation;
        public float Alpha;
        public float R, G, B;
        public byte FlipH, FlipV, Additive;
        public int TexIndex;
        public int OriginIndex;
        public int TexWidth, TexHeight;
    }
}
