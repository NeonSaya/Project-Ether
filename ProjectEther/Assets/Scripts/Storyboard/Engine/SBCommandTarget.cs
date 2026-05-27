namespace OsuVR.Storyboard.Engine
{
    /// <summary>
    /// 命令目标属性 (对应 osu!droid Target enum)
    /// </summary>
    public enum SBCommandTarget
    {
        Alpha = 0,
        X = 1,
        Y = 2,
        ScaleX = 3,
        ScaleY = 4,
        Rotation = 5,
        Color = 6,
        BlendingMode = 7,
        FlipH = 8,
        FlipV = 9,
    }

    /// <summary>
    /// 命令值类型
    /// </summary>
    public enum SBCommandValueType
    {
        Float,
        Color,
        Bool,
    }

    public static class SBCommandTargetExt
    {
        static readonly SBCommandValueType[] Types =
        {
            SBCommandValueType.Float,   // Alpha
            SBCommandValueType.Float,   // X
            SBCommandValueType.Float,   // Y
            SBCommandValueType.Float,   // ScaleX
            SBCommandValueType.Float,   // ScaleY
            SBCommandValueType.Float,   // Rotation
            SBCommandValueType.Color,   // Color
            SBCommandValueType.Bool,    // BlendingMode
            SBCommandValueType.Bool,    // FlipH
            SBCommandValueType.Bool,    // FlipV
        };

        public static SBCommandValueType GetValueType(this SBCommandTarget target)
        {
            return Types[(int)target];
        }

        public static int Count => Types.Length;
    }
}
