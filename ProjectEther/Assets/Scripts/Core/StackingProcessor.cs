using System.Collections.Generic;
using UnityEngine;

namespace OsuVR
{
    /// <summary>
    /// 堆叠处理器：基于 osu! stable 算法
    /// </summary>
    public static class StackingProcessor
    {
        // osu! 标准：如果两个 Note 距离小于 3 像素，视为重叠
        private const float STACK_DISTANCE_THRESHOLD = 3.0f;

        /// <summary>
        /// 对整个谱面应用堆叠处理 (修改 HitObject 的原始坐标)
        /// </summary>
        public static void ApplyStacking(Beatmap beatmap)
        {
            if (beatmap == null || beatmap.HitObjects == null || beatmap.HitObjects.Count == 0)
                return;

            List<HitObject> hitObjects = beatmap.HitObjects;
            int count = hitObjects.Count;

            // ---------------------------------------------------------
            // 1. 计算堆叠参数
            // ---------------------------------------------------------
            float ar = beatmap.Difficulty.ApproachRate;
            float cs = beatmap.Difficulty.CircleSize;

            // 这里必须用 double 接收，因为 Manager 返回的是 double
            double timePreempt = RhythmGameManager.CalculateTimePreempt(ar);

            // StackLeniency 参与计算，结果也应该是 double
            double stackThreshold = timePreempt * beatmap.General.StackLeniency;

            // ---------------------------------------------------------
            // 2. 重置所有 StackHeight
            // ---------------------------------------------------------
            foreach (var obj in hitObjects)
            {
                // StackHeight (或者叫 StackOrder) 归零
                // 注意：你的 HitObject 类里叫 StackOrder，原版叫 StackHeight，这里统一用你的变量名
                obj.StackOrder = 0;
            }

            // ---------------------------------------------------------
            // 3. 核心算法：反向遍历计算层级
            // ---------------------------------------------------------
            // 逻辑：从最后一个 Note 往前看。
            // 如果 Note i 和 Note i+1 重叠，那么 Note i 必须"浮"在 Note i+1 上面。
            // 所以 Note i 的层级 = Note i+1 的层级 + 1。

            // 为了防止无限搜索，设置一个搜索范围限制（虽然原版是全搜，但通常不需要太远）
            int searchLimit = count;

            for (int i = count - 1; i >= 0; i--)
            {
                var currentObj = hitObjects[i];

                // 转盘不参与堆叠，StackOrder 保持 0
                if (currentObj is SpinnerObject) continue;

                // 往后找，看有没有谁压着我
                for (int n = i + 1; n < count; n++)
                {
                    var nextObj = hitObjects[n];

                    // 如果找到转盘，跳过它，继续往后找（转盘不会打断堆叠链）
                    if (nextObj is SpinnerObject) continue;

                    // A. 时间检查：如果两个 Note 间隔太久，说明这一组堆叠结束了
                    if (nextObj.StartTime - currentObj.StartTime > stackThreshold)
                    {
                        // 链条断裂，停止搜索
                        break;
                    }

                    // B. 空间检查：距离是否足够近 (3 osu!pixels)
                    if (Vector2.Distance(currentObj.Position, nextObj.Position) < STACK_DISTANCE_THRESHOLD)
                    {
                        // 命中！我被后面的 Note 压住了
                        // 我的层级 = 压着我的那个 Note 的层级 + 1
                        currentObj.StackOrder = nextObj.StackOrder + 1;

                        // 找到了直接依赖对象后，停止搜索
                        break;
                    }
                }
            }

            // ---------------------------------------------------------
            // 4. 应用坐标偏移 (这是之前缺失的关键步骤！)
            // ---------------------------------------------------------

            // 计算偏移比例 (Based on CS)
            // 公式来源：osu! source code
            // CS 5 = 1.0 (无缩放)
            // CS 2 = 缩放变大 -> 偏移变大
            // CS 7 = 缩放变小 -> 偏移变小
            float scale = (1.0f - 0.7f * (cs - 5f) / 5f);

            // osu! 标准偏移量是 -6.4 像素 (向左上角偏移)
            // 这个值是经过 Scale 调整的
            float offsetBase = -6.4f * scale;
            Vector2 stackOffsetVector = new Vector2(offsetBase, offsetBase);

            foreach (var obj in hitObjects)
            {
                if (obj.StackOrder > 0)
                {
                    // 直接修改原始数据中的 Position
                    // 比如 StackOrder 是 2，就向左上角偏移 2 * 6.4 像素
                    // 这样当 CoordinateMapper 把 (x,y) 转成世界坐标时，就已经带上偏移了

                    Vector2 finalOffset = stackOffsetVector * obj.StackOrder;
                    obj.Position += finalOffset;

                    // 如果是滑条，不仅头要动，整个路径都要动吗？
                    // 在 osu! 数据结构里，滑条的位置就是它的头的位置。
                    // 改变 Position 属性会自动作为所有控制点的参考原点（如果是相对坐标）
                    // 但如果是绝对坐标路径，需要额外处理。
                    // 假设你的 SliderObject 在 GenerateSliderPath 时是基于 Position 计算的，
                    // 那么这里修改 Position 就足够了。
                }
            }

            Debug.Log($"[Stacking] 处理完成. CS:{cs} AR:{ar} Offset:{offsetBase:F2}px. MaxStack:{GetMaxStack(hitObjects)}");
        }

        // 辅助调试：查看最大堆叠层数
        private static int GetMaxStack(List<HitObject> list)
        {
            int max = 0;
            foreach (var o in list) if (o.StackOrder > max) max = o.StackOrder;
            return max;
        }
    }
}