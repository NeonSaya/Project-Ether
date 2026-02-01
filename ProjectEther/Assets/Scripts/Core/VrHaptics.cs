// 临时的修复方案，防止报错
using UnityEngine;

namespace OsuVR
{
    public class VrHaptics : MonoBehaviour
    {
        public static void TriggerHaptic(RayController controller, float duration, float amplitude)
        {
            // 这里暂时留空，或者接入你的 VR SDK (如 XR Interaction Toolkit / SteamVR)
            // 以后我们再专门写震动
        }
    }
}