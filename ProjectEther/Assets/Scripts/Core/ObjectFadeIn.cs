using System.Collections.Generic;
using UnityEngine;

namespace OsuVR
{
    public enum FadeMode
    {
        Standard, // 正常渐隐（在到达击打时间前消失）
        SliderBody, // 滑条本体渐隐（在击打时间之后才开始逐渐消失）
        HitCircleDelayed // HitCircle 贴图延迟隐藏（在到达击打时间后才开始消失）
    }

    public class ObjectFadeIn : MonoBehaviour
    {
        // 缓存 Shader 属性 ID
        private static readonly int PropBaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int PropColor = Shader.PropertyToID("_Color");
        private static readonly int PropTintColor = Shader.PropertyToID("_TintColor");
        private static readonly int PropEmissionColor = Shader.PropertyToID("_EmissionColor");

        private double hitTime;
        private double timePreempt;
        private double timeFadeIn;
        private RhythmGameManager gameManager;
        private MaterialPropertyBlock propBlock;
        private bool isInitialized = false;
        private bool hasFinishedFadeIn = false;

        private Renderer[] cachedRenderers;
        private Color[] cachedColors;
        private Color[] cachedEmissions;

        private bool isHidden = false;
        private double fadeOutStartTime;
        private double fadeOutDuration;
        private FadeMode fadeMode = FadeMode.Standard;
        private double sliderEndTime = 0;

        public void Initialize(double hitTimeMs, double timePreemptMs, RhythmGameManager manager, FadeMode mode = FadeMode.Standard, double endTimeMs = 0)
        {
            this.hitTime = hitTimeMs;
            this.timePreempt = timePreemptMs;
            this.gameManager = manager;
            this.fadeMode = mode;
            this.sliderEndTime = endTimeMs;
            this.isInitialized = true;
            this.hasFinishedFadeIn = false;

            if (this.gameManager != null && this.gameManager.GetModEffects() != null)
            {
                this.isHidden = this.gameManager.GetModEffects().IsHidden;
            }
            else
            {
                this.isHidden = false;
            }

            if (this.isHidden)
            {
                // HD 模式下：
                // 前 40% 时间淡入
                this.timeFadeIn = timePreemptMs * 0.4;
                
                if (this.fadeMode == FadeMode.Standard)
                {
                    // 标准模式：从 40% 到 70% 的时间淡出
                    this.fadeOutStartTime = hitTimeMs - timePreemptMs + this.timeFadeIn;
                    this.fadeOutDuration = timePreemptMs * 0.3;
                }
                else if (this.fadeMode == FadeMode.SliderBody)
                {
                    // 滑条本体模式：在 HitTime 开始淡出，直到 EndTime 结束
                    this.fadeOutStartTime = hitTimeMs;
                    // 确保有一定时长的淡出过程，防止极短滑条瞬间消失
                    this.fadeOutDuration = System.Math.Max(endTimeMs - hitTimeMs, timePreemptMs * 0.3);
                }
                else if (this.fadeMode == FadeMode.HitCircleDelayed)
                {
                    // HitCircle 延迟隐藏模式：到达 HitTime 后才开始消失
                    this.fadeOutStartTime = hitTimeMs;
                    this.fadeOutDuration = timePreemptMs * 0.3; // 消失速度与标准模式相同
                }
            }
            else
            {
                // 正常模式：
                // 前 2/3 时间淡入
                this.timeFadeIn = timePreemptMs * (2.0 / 3.0);
            }

            if (propBlock == null)
                propBlock = new MaterialPropertyBlock();

            var allRenderers = GetComponentsInChildren<Renderer>(true);
            var validRenderers = new List<Renderer>();
            
            foreach (var r in allRenderers)
            {
                // 排除 FollowBall，因为它在滑动时需要保持可见
                if (r.gameObject.name.Contains("FollowBall")) continue;
                
                // 排除已经有自己独立 ObjectFadeIn 的子物体（比如作为 SliderController 子物体的 headInstance）
                var childFadeIn = r.GetComponentInParent<ObjectFadeIn>();
                if (childFadeIn != null && childFadeIn != this) continue;

                validRenderers.Add(r);
            }

            cachedRenderers = validRenderers.ToArray();
            cachedColors = new Color[cachedRenderers.Length];
            cachedEmissions = new Color[cachedRenderers.Length];

            for (int i = 0; i < cachedRenderers.Length; i++)
            {
                cachedColors[i] = GetCurrentColor(cachedRenderers[i]);
                cachedEmissions[i] = GetEmissionColor(cachedRenderers[i]);
            }
            
            SetAlpha(0f);
        }

        private Color GetCurrentColor(Renderer renderer)
        {
            if (renderer == null) return Color.white;

            MaterialPropertyBlock tempBlock = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(tempBlock);

            if (tempBlock.isEmpty)
            {
                if (renderer.sharedMaterial != null)
                {
                    if (renderer.sharedMaterial.HasProperty("_BaseColor"))
                        return renderer.sharedMaterial.GetColor("_BaseColor");
                    if (renderer.sharedMaterial.HasProperty("_Color"))
                        return renderer.sharedMaterial.GetColor("_Color");
                }
                return Color.white;
            }

            Color color = Color.white;
            bool found = false;

            string[] colorProperties = { "_BaseColor", "_Color", "_TintColor" };
            foreach (string prop in colorProperties)
            {
                if (tempBlock.HasProperty(prop))
                {
                    color = tempBlock.GetColor(prop);
                    found = true;
                    break;
                }
            }

            if (!found && renderer.sharedMaterial != null)
            {
                foreach (string prop in colorProperties)
                {
                    if (renderer.sharedMaterial.HasProperty(prop))
                    {
                        color = renderer.sharedMaterial.GetColor(prop);
                        break;
                    }
                }
            }

            return color;
        }

        private Color GetEmissionColor(Renderer renderer)
        {
            if (renderer == null) return Color.black;

            MaterialPropertyBlock tempBlock = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(tempBlock);

            if (tempBlock.HasProperty("_EmissionColor"))
                return tempBlock.GetColor("_EmissionColor");

            if (renderer.sharedMaterial != null && renderer.sharedMaterial.HasProperty("_EmissionColor"))
                return renderer.sharedMaterial.GetColor("_EmissionColor");

            return Color.black;
        }

        void Update()
        {
            if (!isInitialized || gameManager == null)
                return;

            double currentTime = gameManager.GetCurrentMusicTimeMs();
            double fadeStartTime = hitTime - timePreempt;
            double fadeEndTime = fadeStartTime + timeFadeIn;

            if (!isHidden)
            {
                if (hasFinishedFadeIn) return;

                if (currentTime < fadeStartTime)
                {
                    SetAlpha(0f);
                }
                else if (currentTime >= fadeEndTime)
                {
                    SetAlpha(1f);
                    hasFinishedFadeIn = true;
                }
                else
                {
                    double fadeProgress = (currentTime - fadeStartTime) / timeFadeIn;
                    float alpha = Mathf.Clamp01((float)fadeProgress);
                    SetAlpha(alpha);
                }
            }
            else
            {
                // HD 模式逻辑
                if (currentTime < fadeStartTime)
                {
                    SetAlpha(0f);
                }
                else if (currentTime < fadeEndTime)
                {
                    // 淡入阶段
                    double fadeProgress = (currentTime - fadeStartTime) / timeFadeIn;
                    float alpha = Mathf.Clamp01((float)fadeProgress);
                    SetAlpha(alpha);
                }
                else if (currentTime < fadeOutStartTime + fadeOutDuration)
                {
                    // 淡出阶段
                    double fadeOutProgress = (currentTime - fadeOutStartTime) / fadeOutDuration;
                    float alpha = 1f - Mathf.Clamp01((float)fadeOutProgress);
                    SetAlpha(alpha);
                }
                else
                {
                    // 彻底隐藏
                    SetAlpha(0f);
                    // 保持更新，或者如果是最后阶段可以标记结束？
                    // 不标记结束，因为可能存在倒退时间（虽然不多见）
                }
            }
        }

        private void SetAlpha(float alpha)
        {
            if (cachedRenderers == null || cachedColors == null) return;

            for (int i = 0; i < cachedRenderers.Length; i++)
            {
                var renderer = cachedRenderers[i];
                if (renderer == null) continue;

                float currentAlpha = alpha;

                // 针对 Halo (光晕) 的特殊渐隐处理
                // 由于 Halo 的材质通常是 Additive 且带有 HDR 高亮 (例如 2.5x 亮度)
                // 这导致在本体 (1.0x 亮度) 已经基本看不见时，Halo 依然很亮
                if (isHidden && renderer.gameObject.name.Contains("Halo"))
                {
                    // 根据反馈："透明度小于67%就直接隐藏" -> 当 alpha < 0.67f 时直接为 0
                    // "加一点插值吧" -> 使用 InverseLerp 和 SmoothStep 让它在 0.67 到 1.0 之间更平滑地快速消失
                    if (currentAlpha < 0.67f)
                    {
                        currentAlpha = 0f;
                    }
                    else
                    {
                        // 映射到 0~1 范围
                        float t = Mathf.InverseLerp(0.67f, 1.0f, currentAlpha);
                        // 平滑插值
                        currentAlpha = t * t * (3f - 2f * t);
                    }
                }

                renderer.GetPropertyBlock(propBlock);

                Color colorWithAlpha = cachedColors[i];
                // 只修改 Alpha 通道，不要动 RGB，否则普通半透明材质在渐隐时会变黑
                colorWithAlpha.a = cachedColors[i].a * currentAlpha;

                propBlock.SetColor(PropBaseColor, colorWithAlpha);
                propBlock.SetColor(PropColor, colorWithAlpha);
                propBlock.SetColor(PropTintColor, colorWithAlpha);

                // 处理发光颜色 (Emission)
                Color emission = cachedEmissions[i];
                if (emission != Color.black)
                {
                    Color emissionWithAlpha = emission * currentAlpha; // 缩放 RGB 亮度
                    // 保持 Alpha
                    emissionWithAlpha.a = emission.a * currentAlpha;
                    propBlock.SetColor(PropEmissionColor, emissionWithAlpha);
                }

                renderer.SetPropertyBlock(propBlock);

                // 【核心修复】HD 模式下，如果透明度为 0，直接禁用 Renderer，防止某些叠加材质产生残留
                if (isHidden && currentAlpha <= 0f)
                {
                    renderer.enabled = false;
                }
                else
                {
                    if (!renderer.enabled) renderer.enabled = true;
                }
            }
        }

        public void ResetState()
        {
            isInitialized = false;
            hasFinishedFadeIn = false;
            
            // 恢复所有 Renderer 的启用状态
            if (cachedRenderers != null)
            {
                for (int i = 0; i < cachedRenderers.Length; i++)
                {
                    if (cachedRenderers[i] != null && !cachedRenderers[i].enabled)
                    {
                        cachedRenderers[i].enabled = true;
                    }
                }
            }

            SetAlpha(0f);
        }
    }
}
