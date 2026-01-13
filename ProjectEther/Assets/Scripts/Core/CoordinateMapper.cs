using UnityEngine;

namespace OsuVR
{
    /// <summary>
    /// 坐标映射工具类：将osu的2D坐标映射到VR空间中的3D位置
    /// </summary>
    public static class CoordinateMapper
    {
        // Osu游戏区域的原始尺寸
        private const float OSURegionWidth = 512f;
        private const float OSURegionHeight = 384f;

        // 目标3D平面的尺寸（VR中适合挥手范围）
        private const float TargetWidth = 1.5f;    // 1.5米宽
        private const float TargetHeight = 1.1f;   // 1.1米高

        // 目标平面在玩家前方的位置（Z轴距离）
        private const float TargetDistance = 2.0f;

        // Osu坐标中心点对应的世界坐标（玩家眼睛高度前方）
        private static readonly Vector3 TargetCenter = new Vector3(0f, 1.3f, TargetDistance);

        // 计算缩放比例
        private static readonly float ScaleX = TargetWidth / OSURegionWidth;
        private static readonly float ScaleY = TargetHeight / OSURegionHeight;

        /// <summary>
        /// 将osu的2D坐标映射到VR空间中的3D位置
        /// </summary>
        /// <param name="osuPos">osu坐标 (0-512, 0-384)</param>
        /// <returns>Unity世界空间中的3D坐标</returns>
        public static Vector3 MapToWorld(Vector2 osuPos)
        {
            // 步骤1：将osu坐标归一化到[-0.5, 0.5]范围（相对于中心）
            float normalizedX = (osuPos.x / OSURegionWidth) - 0.5f;
            float normalizedY = (osuPos.y / OSURegionHeight) - 0.5f;

            // 步骤2：应用缩放到目标尺寸
            // 注意：Unity中Y轴是向上的，所以保持Y坐标不变
            // 但是osu的Y轴是向下的，我们需要反转Y轴（这样向上移动在Unity中也是向上）
            float worldX = normalizedX * TargetWidth;
            float worldY = -normalizedY * TargetHeight; // 反转Y轴

            // 步骤3：将坐标平移到目标中心点
            Vector3 worldPosition = TargetCenter + new Vector3(worldX, worldY, 0f);

            // 如果觉得还要抬头，就减小这个值；如果觉得还要低头，就增大这个值
            worldPosition.y += 0.5f;

            return worldPosition;
        }

        /// <summary>
        /// 批量映射多个osu坐标到世界坐标
        /// </summary>
        /// <param name="osuPositions">osu坐标数组</param>
        /// <returns>世界坐标数组</returns>
        public static Vector3[] MapMultipleToWorld(Vector2[] osuPositions)
        {
            Vector3[] worldPositions = new Vector3[osuPositions.Length];

            for (int i = 0; i < osuPositions.Length; i++)
            {
                worldPositions[i] = MapToWorld(osuPositions[i]);
            }

            return worldPositions;
        }

        /// <summary>
        /// 获取目标平面的边界框（用于调试或碰撞检测）
        /// </summary>
        /// <returns>平面中心、宽度、高度</returns>
        public static (Vector3 center, float width, float height) GetTargetPlaneInfo()
        {
            return (TargetCenter, TargetWidth, TargetHeight);
        }

        /// <summary>
        /// 计算从当前位置到目标平面的方向向量（用于音符移动）
        /// </summary>
        /// <param name="currentPosition">当前位置</param>
        /// <returns>朝向目标平面的标准化方向</returns>
        public static Vector3 GetDirectionToPlane(Vector3 currentPosition)
        {
            // 方向是从当前位置指向目标平面
            Vector3 direction = TargetCenter - currentPosition;

            // 保持相同的X和Y，但只考虑Z轴方向（让音符正对着玩家飞来）
            // 也可以直接标准化整个向量
            direction.Normalize();

            return direction;
        }
    }
}