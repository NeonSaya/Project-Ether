namespace OsuVR.Storyboard.Engine
{
    /// <summary>
    /// Storyboard 元素单帧渲染状态 (struct, 零 GC)
    /// 纯计算输出，由渲染层读取并应用到 Unity 对象
    /// </summary>
    public struct SBRenderState
    {
        public float X, Y;
        public float ScaleX, ScaleY;
        public float Rotation;
        public float Alpha;
        public float R, G, B;
        public bool FlipH, FlipV;
        public bool Additive;

        public static SBRenderState Default => new SBRenderState
        {
            ScaleX = 1f,
            ScaleY = 1f,
            Alpha = 1f,
            R = 1f,
            G = 1f,
            B = 1f,
        };
    }
}
