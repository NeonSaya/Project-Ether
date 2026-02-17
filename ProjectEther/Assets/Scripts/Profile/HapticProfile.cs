using UnityEngine;

namespace OsuVR
{
    [CreateAssetMenu(fileName = "NewHapticProfile", menuName = "OsuVR/Haptic Profile")]
    public class HapticProfile : ScriptableObject
    {
        [Header("基础打击 (Note / Slider Head / Slider End)")]
        public HapticData NormalHit = new HapticData(0.6f, 0.08f);
        public HapticData WhistleHit = new HapticData(0.4f, 0.1f); // 哨音通常轻快
        public HapticData FinishHit = new HapticData(0.9f, 0.15f); // 终结音重击
        public HapticData ClapHit = new HapticData(0.7f, 0.1f);

        [Header("滑条过程 (Slider)")]
        [Tooltip("滑条持续滑动时的微震 (每一帧调用，只需填强度)")]
        [Range(0, 1)] public float SliderSlideIntensity = 0.05f;

        [Tooltip("滑条经过 Tick (小豆豆) 的震动")]
        public HapticData SliderTick = new HapticData(0.3f, 0.05f);

        [Tooltip("滑条折返 (Reverse Arrow) 的震动")]
        public HapticData SliderReverse = new HapticData(0.5f, 0.08f);

        [Header("转盘 (Spinner)")]
        [Tooltip("转盘刚开始转的最小震动")]
        public float SpinnerMinIntensity = 0.05f;

        [Tooltip("转盘转满速/进度满时的最大震动")]
        public float SpinnerMaxIntensity = 0.6f;

        [System.Serializable]
        public struct HapticData
        {
            [Range(0, 1)] public float intensity;
            public float duration;

            public HapticData(float i, float d)
            {
                intensity = i;
                duration = d;
            }
        }
    }
}