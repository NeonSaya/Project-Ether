using UnityEngine;

namespace OsuVR
{
    /// <summary>
    /// 独立的震动反馈组件：监听 Spinner 速度并触发手柄震动
    /// 遵循 "组合优于继承" 原则，将反馈逻辑从核心判定中剥离
    /// </summary>
    [RequireComponent(typeof(SpinnerController))]
    public class SpinnerHaptics : MonoBehaviour
    {
        private SpinnerController spinner;

        [Header("震动配置")]
        [Tooltip("最小震动强度 (转得很慢时)")]
        public float minIntensity = 0.05f;

        [Tooltip("最大震动强度 (转得飞快时)")]
        public float maxIntensity = 0.5f;

        [Tooltip("达到最大震动所需的 RPM 阈值 (通常 300-400 RPM 就算很快了)")]
        public float maxRpmThreshold = 400f;

        void Awake()
        {
            spinner = GetComponent<SpinnerController>();
        }

        void Update()
        {
            // 如果 Spinner 已经结束或未激活，不震动
            if (spinner == null || !spinner.IsActive) return;

            float rpm = spinner.CurrentRPM;

            // 只有转动速度超过一定阈值才开始震动，避免静止时有杂讯
            if (rpm > 20f)
            {
                // 计算强度 (0~1 之间的插值)
                float t = Mathf.Clamp01(rpm / maxRpmThreshold);

                // 缓动曲线：可以使用 t*t 让高速时震动提升更明显，这里用线性 Lerp
                float intensity = Mathf.Lerp(minIntensity, maxIntensity, t);

                // 发送给左右手
                // 优化：这里简单地两手都震。如果想更精细，可以判断哪只手正在交互。
                // 但对于 Spinner 这种全屏特效感，双手共振反馈感更好。
                VrHaptics.Trigger(LaserShooter.HandSide.Left, intensity, Time.deltaTime);
                VrHaptics.Trigger(LaserShooter.HandSide.Right, intensity, Time.deltaTime);
            }
        }
    }
}