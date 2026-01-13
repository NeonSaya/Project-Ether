using UnityEngine;

namespace OsuVR
{
    /// <summary>
    /// 表示一个转盘
    /// </summary>
    public class SpinnerObject : HitObject
    {
        /// <summary>
        /// 转盘结束时间
        /// </summary>
        private readonly double _endTime;

        /// <summary>
        /// 转盘结束时间
        /// </summary>
        public override double EndTime => _endTime;

        /// <summary>
        /// 难度计算中的堆叠位置（转盘总是在中心）
        /// </summary>
        public override Vector2 DifficultyStackedPosition => Position;

        /// <summary>
        /// 难度计算中的堆叠结束位置（转盘总是在中心）
        /// </summary>
        public override Vector2 DifficultyStackedEndPosition => Position;

        /// <summary>
        /// 游戏玩法中的堆叠位置（转盘总是在中心）
        /// </summary>
        public override Vector2 GameplayStackedPosition => Position;

        /// <summary>
        /// 游戏玩法中的堆叠结束位置（转盘总是在中心）
        /// </summary>
        public override Vector2 GameplayStackedEndPosition => Position;

        /// <summary>
        /// 屏幕空间中的游戏玩法堆叠位置（转盘总是在中心）
        /// </summary>
        public override Vector2 ScreenSpaceGameplayStackedPosition => ScreenSpaceGameplayPosition;

        /// <summary>
        /// 屏幕空间中的游戏玩法堆叠结束位置（转盘总是在中心）
        /// </summary>
        public override Vector2 ScreenSpaceGameplayStackedEndPosition => ScreenSpaceGameplayPosition;

        /// <summary>
        /// 构造函数
        /// </summary>
        public SpinnerObject(double startTime, double endTime, bool isNewCombo)
            : base(startTime, new Vector2(256f, 192f), HitObjectType.Spinner, isNewCombo, 0)
        {
            _endTime = endTime;
        }

        /// <summary>
        /// 获取转盘需要旋转的角度（基于时间进度）
        /// </summary>
        /// <param name="currentTime">当前时间</param>
        /// <returns>所需旋转角度（度）</returns>
        public float GetRequiredRotation(double currentTime)
        {
            if (currentTime < StartTime)
                return 0f;

            if (currentTime > EndTime)
                return 1080f; // 完成3圈

            double progress = (currentTime - StartTime) / Duration;
            return (float)(progress * 1080f); // 3圈 = 1080度
        }

        /// <summary>
        /// 检查是否完成转盘
        /// </summary>
        /// <param name="totalRotation">玩家累计旋转角度</param>
        /// <param name="currentTime">当前时间</param>
        /// <returns>是否完成</returns>
        public bool IsCompleted(float totalRotation, double currentTime)
        {
            if (currentTime > EndTime)
                return false; // 超时

            return totalRotation >= GetRequiredRotation(currentTime);
        }
    }
}