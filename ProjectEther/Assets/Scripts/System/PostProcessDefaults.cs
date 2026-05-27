using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

namespace OsuVR
{
    /// <summary>
    /// 为 X-PostProcessing 预配置默认效果参数。
    /// 挂载在 PostProcessManager 同一 GameObject 上，在 Start 中自动注入。
    /// </summary>
    public class PostProcessDefaults : MonoBehaviour
    {
        [Header("Bloom (URP Volume 已有，此处默认关闭)")]
        public bool enableBloom = false;

        [Header("Vignette (URP Volume 已有，此处默认关闭)")]
        public bool enableVignette = false;

        [Header("Chromatic Aberration")]
        public bool enableChromaticAberration = true;
        [Range(0f, 1f)] public float chromaticAberrationIntensity = 0.1f;

        [Header("Color Grading")]
        public bool enableColorGrading = true;
        [Range(-1f, 1f)] public float saturation = 0.1f;
        [Range(-1f, 1f)] public float contrast = 0.05f;

        [Header("Film Grain")]
        public bool enableFilmGrain = false;
        [Range(0f, 1f)] public float filmGrainIntensity = 0.2f;

        private void Start()
        {
            if (PostProcessManager.Instance == null)
            {
                Debug.LogWarning("[PostProcessDefaults] PostProcessManager 未就绪");
                return;
            }

            var profile = PostProcessManager.Instance.GetProfile();
            if (profile == null) return;

            if (enableChromaticAberration)
            {
                var ca = PostProcessManager.Instance.AddEffect<ChromaticAberration>();
                ca.intensity.Override(chromaticAberrationIntensity);
            }

            if (enableColorGrading)
            {
                var cg = PostProcessManager.Instance.AddEffect<ColorGrading>();
                cg.enabled.Override(true);
                cg.gradingMode.Override(GradingMode.LowDefinitionRange);
                cg.saturation.Override(saturation * 100f);
                cg.contrast.Override(contrast * 100f);
            }

            if (enableFilmGrain)
            {
                var grain = PostProcessManager.Instance.AddEffect<Grain>();
                grain.intensity.Override(filmGrainIntensity);
            }

            Debug.Log("[PostProcessDefaults] 默认效果已配置");
        }
    }
}
