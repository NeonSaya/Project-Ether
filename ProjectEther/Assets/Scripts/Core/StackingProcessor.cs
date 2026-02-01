using System.Collections.Generic;
using UnityEngine;

namespace OsuVR
{
    /// <summary>
    /// 堆叠处理器：专门负责计算 Note 的堆叠层级 (Stacking)
    /// 逻辑移植自 osu! 官方/osudroid 算法
    /// </summary>
    public static class StackingProcessor
    {
        // osu! 标准坐标系下的堆叠距离阈值 (3 osu!pixels)
        private const float STACK_DISTANCE_THRESHOLD = 3.0f;

        /// <summary>
        /// 对整个谱面应用堆叠处理
        /// </summary>
        public static void ApplyStacking(Beatmap beatmap)
        {
            if (beatmap == null || beatmap.HitObjects == null || beatmap.HitObjects.Count == 0)
                return;

            List<HitObject> hitObjects = beatmap.HitObjects;
            int count = hitObjects.Count;

            // 1. 计算堆叠时间窗口
            // StackLeniency (堆叠宽容度) 通常在谱面 General 段落定义，默认 0.7
            // 这里我们需要计算 AR (Approach Rate) 对应的 TimePreempt
            float ar = beatmap.Difficulty.ApproachRate;
            float timePreempt = CalculateTimePreempt(ar);
            float stackThreshold = timePreempt * beatmap.General.StackLeniency;

            // 2. 重置所有 StackOrder
            foreach (var obj in hitObjects)
            {
                obj.StackOrder = 0;
            }

            // 3. 执行堆叠算法 (反向遍历)
            // 逻辑：如果 Note[i] 在 Note[n] 之前，且坐标重叠、时间相近
            // 那么 Note[i] 应该叠在 Note[n] "上面" (Z轴更靠前)
            // 所以我们从后往前算，如果发现重叠，当前 Note 的层级 = 后一个 Note 层级 + 1

            // 扩展搜索范围，防止连续堆叠被打断
            int searchLimit = count - 1;

            for (int i = count - 1; i >= 0; i--)
            {
                var currentObj = hitObjects[i];

                // 转盘 (Spinner) 不参与堆叠
                if (currentObj is SpinnerObject) continue;

                for (int n = i + 1; n < count; n++)
                {
                    var nextObj = hitObjects[n];

                    // 如果转盘，跳过但不打断搜索链
                    if (nextObj is SpinnerObject) continue;

                    // A. 时间检查：如果两个 Note 间隔太久，说明这一组堆叠结束了
                    if (nextObj.StartTime - currentObj.StartTime > stackThreshold)
                    {
                        break;
                    }

                    // B. 空间检查：距离是否足够近 (osu!pixel 坐标系)
                    // 注意：这里只检查头部位置。如果以后要支持滑条尾部堆叠，逻辑会更复杂
                    if (Vector2.Distance(currentObj.Position, nextObj.Position) < STACK_DISTANCE_THRESHOLD)
                    {
                        // 命中堆叠！
                        // 当前(较早)物件的层级 = 下一个(较晚)物件的层级 + 1
                        currentObj.StackOrder = nextObj.StackOrder + 1;

                        // 找到了依赖对象后，就不用继续往后找了，因为 nextObj 已经包含了它后面的层级信息
                        break;
                    }
                }
            }

            Debug.Log($"[StackingProcessor] 堆叠处理完成，AR: {ar}, 阈值: {stackThreshold}ms");
        }

        /// <summary>
        /// 根据 AR 计算 Note 在屏幕上的停留时间 (TimePreempt)
        /// 公式源自 osu! wiki
        /// </summary>
        private static float CalculateTimePreempt(float ar)
        {
            if (ar < 5)
                return 1200f + 120f * (5f - ar);
            else if (ar > 5)
                return 1200f - 150f * (ar - 5f);
            else
                return 1200f;
        }
    }
}