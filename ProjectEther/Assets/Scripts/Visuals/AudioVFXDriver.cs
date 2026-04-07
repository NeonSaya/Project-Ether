using UnityEngine;
using UnityEngine.VFX;

namespace OsuVR
{
    /// <summary>
    /// 音频驱动视觉效果的通用组件
    /// 支持Transform缩放和VFX Graph属性驱动
    /// </summary>
    public class AudioVFXDriver : MonoBehaviour
    {
        // =========================================================
        // 枚举定义
        // =========================================================

        public enum FrequencyBand
        {
            Bass,
            Mid,
            Treble
        }

        public enum DriveTarget
        {
            TransformScale,
            VFXProperty
        }

        // =========================================================
        // 配置参数
        // =========================================================

        [Header("频段选择")]
        [Tooltip("选择要响应的频段")]
        public FrequencyBand frequencyBand = FrequencyBand.Bass;

        [Header("驱动目标")]
        [Tooltip("选择驱动方式")]
        public DriveTarget driveTarget = DriveTarget.TransformScale;

        [Header("Transform缩放设置")]
        [Tooltip("基础缩放大小")]
        public Vector3 baseScale = Vector3.one;

        [Tooltip("最大放大倍数")]
        [Range(1f, 10f)]
        public float maxScaleMultiplier = 3f;

        [Tooltip("缩放平滑速度")]
        [Range(0.01f, 1f)]
        public float scaleSmoothSpeed = 0.15f;

        [Header("VFX属性设置")]
        [Tooltip("VFX Graph中Exposed的变量名")]
        public string vfxPropertyName = "BassEnergy";

        [Tooltip("VFX属性最小值")]
        public float vfxMinValue = 0f;

        [Tooltip("VFX属性最大值")]
        public float vfxMaxValue = 1f;

        [Header("高级设置")]
        [Tooltip("响应曲线（用于调整音频值到视觉效果的映射）")]
        public AnimationCurve responseCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [Tooltip("是否在编辑器模式下预览")]
        public bool previewInEditor = false;

        // =========================================================
        // 内部状态
        // =========================================================

        private VisualEffect visualEffect;
        private Vector3 currentScale;
        private float currentEnergy;

        // =========================================================
        // 生命周期
        // =========================================================

        void Start()
        {
            if (driveTarget == DriveTarget.VFXProperty)
            {
                visualEffect = GetComponent<VisualEffect>();
                if (visualEffect == null)
                {
                    Debug.LogError($"[AudioVFXDriver] 未找到VisualEffect组件: {gameObject.name}");
                    enabled = false;
                    return;
                }
            }

            currentScale = baseScale;
            transform.localScale = baseScale;
        }

        void Update()
        {
            if (AudioVisualizationManager.Instance == null)
            {
                return;
            }

            float energy = GetEnergyValue();
            energy = responseCurve.Evaluate(energy);

            switch (driveTarget)
            {
                case DriveTarget.TransformScale:
                    ApplyTransformScale(energy);
                    break;

                case DriveTarget.VFXProperty:
                    ApplyVFXProperty(energy);
                    break;
            }
        }

        // =========================================================
        // 获取频段能量值
        // =========================================================

        private float GetEnergyValue()
        {
            return frequencyBand switch
            {
                FrequencyBand.Bass => AudioVisualizationManager.Instance.Bass,
                FrequencyBand.Mid => AudioVisualizationManager.Instance.Mid,
                FrequencyBand.Treble => AudioVisualizationManager.Instance.Treble,
                _ => 0f
            };
        }

        // =========================================================
        // Transform缩放驱动
        // =========================================================

        private void ApplyTransformScale(float energy)
        {
            float targetScale = 1f + (energy * (maxScaleMultiplier - 1f));
            Vector3 targetScaleVector = baseScale * targetScale;

            currentScale = Vector3.Lerp(currentScale, targetScaleVector, scaleSmoothSpeed);
            transform.localScale = currentScale;
        }

        // =========================================================
        // VFX属性驱动
        // =========================================================

        private void ApplyVFXProperty(float energy)
        {
            if (visualEffect == null) return;

            float mappedValue = Mathf.Lerp(vfxMinValue, vfxMaxValue, energy);
            visualEffect.SetFloat(vfxPropertyName, mappedValue);
        }

        // =========================================================
        // 编辑器可视化
        // =========================================================

#if UNITY_EDITOR
        void OnValidate()
        {
            if (!previewInEditor) return;

            if (driveTarget == DriveTarget.VFXProperty)
            {
                visualEffect = GetComponent<VisualEffect>();
            }
        }

        void OnDrawGizmosSelected()
        {
            if (!previewInEditor) return;

            float energy = GetEnergyValue();
            energy = responseCurve.Evaluate(energy);

            Gizmos.color = Color.HSVToRGB((float)frequencyBand / 3f, 1f, 1f);
            Gizmos.DrawWireSphere(transform.position, 0.5f + energy * 0.5f);
        }
#endif

        // =========================================================
        // 调试工具
        // =========================================================

        [ContextMenu("打印当前能量值")]
        public void LogCurrentEnergy()
        {
            if (AudioVisualizationManager.Instance != null)
            {
                float energy = GetEnergyValue();
                Debug.Log($"[AudioVFXDriver] {frequencyBand} Energy: {energy:F3}");
            }
            else
            {
                Debug.LogWarning("[AudioVFXDriver] AudioVisualizationManager未初始化");
            }
        }
    }
}
