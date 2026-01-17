using UnityEngine;
using UnityEngine.XR;

namespace OsuVR
{
    /// <summary>
    /// VR 触觉反馈工具类 (静态工具)
    /// 作用：封装 Unity XR 底层 API，提供简单易用的震动接口
    /// </summary>
    public static class VrHaptics
    {
        /// <summary>
        /// 触发手柄震动
        /// </summary>
        /// <param name="hand">手柄侧 (Left/Right)</param>
        /// <param name="amplitude">震动强度 (0.0 ~ 1.0)</param>
        /// <param name="duration">持续时间 (秒)</param>
        public static void Trigger(LaserShooter.HandSide hand, float amplitude, float duration)
        {
            // 将我们定义的 HandSide 枚举映射到 Unity XR 的节点
            XRNode node = (hand == LaserShooter.HandSide.Left) ? XRNode.LeftHand : XRNode.RightHand;

            // 获取对应手柄设备
            InputDevice device = InputDevices.GetDeviceAtXRNode(node);

            if (device.isValid)
            {
                // 发送脉冲 (channel 0 是默认震动马达)
                device.SendHapticImpulse(0, amplitude, duration);
            }
        }
    }
}