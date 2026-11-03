namespace OsuVR.Storyboard.Data
{
    /// <summary>
    /// osu! Storyboard 缓动类型 (对应 .osu/.osb 中的 Easing 参数)
    /// 值与 osu! 文件格式中的整数索引一一对应
    /// </summary>
    public enum SBEasing
    {
        Linear = 0,
        Out,
        In,
        InQuad,
        OutQuad,
        InOutQuad,
        InCubic,
        OutCubic,
        InOutCubic,
        InQuart,
        OutQuart,
        InOutQuart,
        InQuint,
        OutQuint,
        InOutQuint,
        InSine,
        OutSine,
        InOutSine,
        InExpo,
        OutExpo,
        InOutExpo,
        InCirc,
        OutCirc,
        InOutCirc,
        InElastic,
        OutElastic,
        OutElasticHalf,
        OutElasticQuarter,
        InOutElastic,
        InBack,
        OutBack,
        InOutBack,
        InBounce,
        OutBounce,
        InOutBounce,
        OutPow10,
    }
}
