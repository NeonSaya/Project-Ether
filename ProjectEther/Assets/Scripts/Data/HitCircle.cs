using UnityEngine;

namespace OsuVR
{
    /// <summary>
    /// 表示一个点击圆圈
    /// </summary>
    public class HitCircle : HitObject
    {
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="startTime">开始时间（毫秒）</param>
        /// <param name="position">位置（osu!像素坐标）</param>
        /// <param name="isNewCombo">是否开始新连击</param>
        /// <param name="comboOffset">连击偏移量</param>
        public HitCircle(double startTime, Vector2 position, bool isNewCombo, int comboOffset)
            : base(startTime, position, isNewCombo, comboOffset)
        {
        }
    }
}