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
        /// <param name="volume">0.0 ~ 1.0 的音量倍率</param>
        public void PlayHitHaptic(bool isRightHand, int hitSoundType, float volume)
        {
            // 1. 获取基础配置
            HapticProfile.HapticData data = profile.NormalHit;
            if ((hitSoundType & 4) > 0) data = profile.FinishHit;
            else if ((hitSoundType & 8) > 0) data = profile.ClapHit;
            else if ((hitSoundType & 2) > 0) data = profile.WhistleHit;

            // 2. 根据音量计算最终强度 (音量越小震动越弱，但不仅是线性，可以用平方让小声音更柔和)
            float finalIntensity = data.intensity * Mathf.Clamp01(volume);

            // 设定一个最小震动阈值，避免有声音却没震动 (可选)
            if (volume > 0.05f && finalIntensity < 0.1f) finalIntensity = 0.1f;

            // 3. 发送
            XRNode node = isRightHand ? XRNode.RightHand : XRNode.LeftHand;
            SendHaptic(node, finalIntensity, data.duration);
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
        /// [修复] 双手同时播放打击震动，现在支持音量参数
        /// </summary>
        public void PlayHitHapticBoth(int hitSoundType, float volume)
        {
            HapticProfile.HapticData data = profile.NormalHit;

            if ((hitSoundType & 4) > 0) data = profile.FinishHit;
            else if ((hitSoundType & 8) > 0) data = profile.ClapHit;
            else if ((hitSoundType & 2) > 0) data = profile.WhistleHit;

            // [新增] 应用音量计算
            float finalIntensity = data.intensity * Mathf.Clamp01(volume);

            // 最小阈值防止无法感知
            if (volume > 0.05f && finalIntensity < 0.1f) finalIntensity = 0.1f;

            PlayHapticBoth(finalIntensity, data.duration);
        }
    }
}