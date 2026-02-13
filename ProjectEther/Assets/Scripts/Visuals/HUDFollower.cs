using UnityEngine;

namespace OsuVR
{
    /// <summary>
    /// 像 Beat Saber 一样的 HUD 跟随：
    /// 1. 平滑跟随头显的 Y 轴旋转 (Yaw)
    /// 2. 高度 (Y) 相对固定，只在大幅度移动时缓慢调整
    /// 3. 始终保持固定距离
    /// </summary>
    public class HUDFollower : MonoBehaviour
    {
        [Header("跟随设置")]
        public float distance = 3.5f;       // 距离玩家多远
        public float heightOffset = 1.5f;   // 相对于地面的高度
        public float smoothTime = 0.3f;     // 平滑时间 (越大越慢)
        public float heightSmoothTime = 0.8f; // 高度跟随更慢，防止抬头低头时 UI 乱跑

        [Header("视角限制")]
        public bool lockPitch = true; // 锁定俯仰角 (UI 永远垂直于地面，不随你抬头而倾斜)

        [Header("倾斜设置")]
        [Tooltip("UI 向下倾斜的角度")]
        public float pitchOffset = -30.0f; // 默认倾斜 30 度

        private Transform headTransform;
        private Vector3 currentVelocity;
        private float heightVelocity;
        private float currentHeight;

        void Start()
        {
            if (Camera.main != null)
            {
                headTransform = Camera.main.transform;
                // 初始高度直接设为目标高度，避免开局飞过来
                currentHeight = headTransform.position.y + heightOffset;
                transform.position = headTransform.position + headTransform.forward * distance;
            }
        }

        void LateUpdate()
        {
            if (headTransform == null)
            {
                if (Camera.main != null) headTransform = Camera.main.transform;
                return;
            }

            // 1. 计算目标位置
            // 核心逻辑：只取头显的水平朝向 (ProjectOnPlane)
            Vector3 headForward = headTransform.forward;
            if (lockPitch)
            {
                headForward.y = 0; // 抹平 Y 轴分量
                headForward.Normalize();
            }

            // 目标位置：头显位置 + 前方 * 距离
            Vector3 targetPos = headTransform.position + (headForward * distance);

            // 2. 高度单独处理 (Beat Saber 风格：高度比较慵懒)
            // 目标高度是：头显高度 + 偏移 (或者固定高度)
            float targetHeight = headTransform.position.y + heightOffset;

            // 使用 SmoothDamp 平滑高度
            currentHeight = Mathf.SmoothDamp(currentHeight, targetHeight, ref heightVelocity, heightSmoothTime);
            targetPos.y = currentHeight;

            // 3. 位置平滑跟随
            transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref currentVelocity, smoothTime);

            // 4. 朝向：UI 始终看向头显，并叠加倾斜
            Vector3 lookDir = transform.position - headTransform.position;
            if (lockPitch) lookDir.y = 0;

            if (lookDir != Vector3.zero)
            {
                // 先计算基础的"正如朝向"
                Quaternion baseRotation = Quaternion.LookRotation(lookDir);

                // 根据 Unity 左手坐标系，正值通常会让顶部向"后"（即向玩家方向）倒
                Quaternion targetRot = baseRotation * Quaternion.Euler(pitchOffset, 0, 0);

                // 旋转也平滑一点
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 5f);
            }
        }
    }
}