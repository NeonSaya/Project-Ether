using System;

namespace OsuVR
{
    /// <summary>
    /// 击打对象类型
    /// </summary>
    [Flags]
    public enum HitObjectType
    {
        /// <summary>
        /// 点击圆圈
        /// </summary>
        Circle = 1,

        /// <summary>
        /// 滑条
        /// </summary>
        Slider = 2,

        /// <summary>
        /// 转盘
        /// </summary>
        Spinner = 8,

        /// <summary>
        /// 新连击
        /// </summary>
        NewCombo = 4,

        /// <summary>
        /// 连击颜色偏移（高4位）
        /// </summary>
        ComboColorOffset = 112,// 二进制: 01110000

            /// <summary>
            /// Mania 长按 (兼容性保留)
            /// </summary>
        Hold = 128
    }

    /// <summary>
    /// 曲线类型（滑条路径类型）
    /// </summary>
    public enum CurveType
    {
        /// <summary>
        /// 线性滑条
        /// </summary>
        Linear = 'L',

        /// <summary>
        /// 完美曲线（圆形）
        /// </summary>
        Perfect = 'P',

        /// <summary>
        /// 贝塞尔曲线
        /// </summary>
        Bezier = 'B',

        /// <summary>
        /// 卡特姆曲线
        /// </summary>
        Catmull = 'C'
    }

    /// <summary>
    /// 游戏模式
    /// </summary>
    public enum GameMode
    {
        /// <summary>
        /// osu!droid 模式
        /// </summary>
        Droid,

        /// <summary>
        /// osu!standard 模式
        /// </summary>
        Standard
    }

    /// <summary>
    /// 样本库类型
    /// </summary>
    public enum SampleBank
    {
        None,
        Normal,
        Soft,
        Drum
    }



    /// <summary>
    /// 音效采样集类型 (决定是用柔和音还是鼓点音)
    /// </summary>
    public enum SampleSet
    {
        /// <summary>
        /// 自动/继承
        /// </summary>
        None = 0,

        /// <summary>
        /// 标准音 (Normal)
        /// </summary>
        Normal = 1,

        /// <summary>
        /// 柔和音 (Soft)
        /// </summary>
        Soft = 2,

        /// <summary>
        /// 鼓点音 (Drum)
        /// </summary>
        Drum = 3
    }

    /// <summary>
    /// 打击音效类型 (位掩码，支持叠加)
    /// </summary>
    [Flags]
    public enum HitSoundType
    {
        /// <summary>
        /// 无额外音效
        /// </summary>
        None = 0,

        /// <summary>
        /// 基础打击音
        /// </summary>
        Normal = 1,

        /// <summary>
        /// 哨音 (Whistle)
        /// </summary>
        Whistle = 2,

        /// <summary>
        /// 终结音 (Finish/镲片)
        /// </summary>
        Finish = 4,

        /// <summary>
        /// 掌声 (Clap)
        /// </summary>
        Clap = 8
    }
}