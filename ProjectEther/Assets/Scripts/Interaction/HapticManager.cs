using UnityEngine;
using UnityEngine.XR; // 引用底层 XR 库
using System.Collections.Generic;

namespace OsuVR
{
    /// <summary>
    /// 震动管理器 (终极免拖拽版)
    /// 直接通过 Unity 底层 InputDevices API 发送指令，无需引用 Controller GameObject
    /// </summary>
    public class HapticManager : MonoBehaviour
    {
        public static HapticManager Instance { get; private set; }

        [Header("配置引用")]
        [Tooltip("请将创建的 HapticProfile 配置文件拖入此处")]
        public HapticProfile profile;

        // 缓存设备列表，避免每帧 GC (垃圾回收)
        private List<InputDevice> devices = new List<InputDevice>();

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject); // ✅ 加上这句
            }
            else
            {
                Destroy(gameObject);
            }
        }

        // =========================================================
        // 核心底层方法
        // =========================================================

        /// <summary>
        /// 向指定节点 (左手/右手) 发送震动指令
        /// </summary>
        private void SendHaptic(XRNode node, float intensity, float duration)
        {
            // 1. 获取指定节点设备
            InputDevices.GetDevicesAtXRNode(node, devices);

            // 2. 遍历找到的设备 (通常只有一个)
            foreach (var device in devices)
            {
                // 检查设备是否有效且支持震动
                if (device.isValid)
                {
                    HapticCapabilities capabilities;
                    if (device.TryGetHapticCapabilities(out capabilities))
                    {
                        if (capabilities.supportsImpulse)
                        {
                            // 发送指令：通道0，强度，持续时间
                            device.SendHapticImpulse(0, intensity, duration);
                        }
                    }
                }
            }
        }

        // =========================================================
        // 单手震动接口 (供 NoteController 等调用)
        // =========================================================

        /// <summary>
        /// 根据 Osu 音效类型触发单手震动
        /// </summary>
        public void PlayHitHaptic(bool isRightHand, int hitSoundType)
        {
            // 1. 根据 HitSound 类型选择震动强度
            // 优先级：Finish > Clap > Whistle > Normal
            HapticProfile.HapticData data = profile.NormalHit;

            if ((hitSoundType & 4) > 0) data = profile.FinishHit;      // Finish (Bit 2)
            else if ((hitSoundType & 8) > 0) data = profile.ClapHit;   // Clap (Bit 3)
            else if ((hitSoundType & 2) > 0) data = profile.WhistleHit;// Whistle (Bit 1)

            // 2. 发送
            XRNode node = isRightHand ? XRNode.RightHand : XRNode.LeftHand;
            SendHaptic(node, data.intensity, data.duration);
        }

        /// <summary>
        /// 播放滑条 Tick 震动
        /// </summary>
        public void PlaySliderTick(bool isRightHand)
        {
            XRNode node = isRightHand ? XRNode.RightHand : XRNode.LeftHand;
            SendHaptic(node, profile.SliderTick.intensity, profile.SliderTick.duration);
        }

        /// <summary>
        /// 播放滑条折返震动
        /// </summary>
        public void PlaySliderReverse(bool isRightHand)
        {
            XRNode node = isRightHand ? XRNode.RightHand : XRNode.LeftHand;
            SendHaptic(node, profile.SliderReverse.intensity, profile.SliderReverse.duration);
        }

        /// <summary>
        /// 持续微震 (用于 Slider 滑动 和 Spinner)
        /// 建议在 Update 中每帧调用
        /// </summary>
        public void PlayContinuous(bool isRightHand, float intensity)
        {
            XRNode node = isRightHand ? XRNode.RightHand : XRNode.LeftHand;
            // 持续震动其实是每帧发送一个极短的脉冲 (0.1s 是为了覆盖帧间隔)
            SendHaptic(node, intensity, 0.1f);
        }

        // =========================================================
        // 双手震动接口 (爽快感增强)
        // =========================================================

        /// <summary>
        /// 双手同时震动 (自定义强度)
        /// </summary>
        public void PlayHapticBoth(float intensity, float duration)
        {
            SendHaptic(XRNode.LeftHand, intensity, duration);
            SendHaptic(XRNode.RightHand, intensity, duration);
        }

        /// <summary>
        /// 双手同时播放打击震动 (用于 Finish 大音符等)
        /// </summary>
        public void PlayHitHapticBoth(int hitSoundType)
        {
            HapticProfile.HapticData data = profile.NormalHit;

            if ((hitSoundType & 4) > 0) data = profile.FinishHit;
            else if ((hitSoundType & 8) > 0) data = profile.ClapHit;
            else if ((hitSoundType & 2) > 0) data = profile.WhistleHit;

            PlayHapticBoth(data.intensity, data.duration);
        }
    }
}