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
        public HitCircle(double startTime, Vector2 position, bool isNewCombo, int comboOffset)
            : base(startTime, position, HitObjectType.Circle, isNewCombo, comboOffset)
        {
        }

        /// <summary>
        /// 获取圆圈的大小（基于缩放）
        /// </summary>
        public float GetCircleSize()
        {
            return OBJECT_RADIUS * GameplayScale;
        }
    }
}