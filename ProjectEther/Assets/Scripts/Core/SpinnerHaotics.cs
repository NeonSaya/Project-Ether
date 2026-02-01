using UnityEngine;

namespace OsuVR
{
    /// <summary>
    /// 独立的震动反馈组件 (占位符)
    /// 目前逻辑已清空，等待后续 VrHaptics 系统完善后再实现
    /// </summary>
    [RequireComponent(typeof(SpinnerController))]
    public class SpinnerHaptics : MonoBehaviour
    {
        private SpinnerController spinner;

        void Awake()
        {
            spinner = GetComponent<SpinnerController>();
        }

        void Update()
        {
            // TODO: 这里以后写震动逻辑
            // 暂时留空以修复编译错误
        }
    }
}