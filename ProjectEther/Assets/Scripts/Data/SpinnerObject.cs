using UnityEngine;
using UnityEngine.Scripting;

namespace OsuVR
{
    /// <summary>
    /// 表示一个转盘
    /// </summary>
    [Preserve]
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
    }
}