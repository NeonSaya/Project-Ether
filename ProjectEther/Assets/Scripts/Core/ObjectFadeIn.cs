using UnityEngine;

namespace OsuVR
{
    public class ObjectFadeIn : MonoBehaviour
    {
        private double hitTime;
        private double timePreempt;
        private double timeFadeIn;
        private RhythmGameManager gameManager;
        private MaterialPropertyBlock propBlock;
        private bool isInitialized = false;
        private bool hasFinishedFadeIn = false;

        private Renderer[] cachedRenderers;
        private Color[] cachedColors;

        public void Initialize(double hitTimeMs, double timePreemptMs, RhythmGameManager manager)
        {
            this.hitTime = hitTimeMs;
            this.timePreempt = timePreemptMs;
            this.timeFadeIn = timePreemptMs * (2.0 / 3.0);
            this.gameManager = manager;
            this.isInitialized = true;
            this.hasFinishedFadeIn = false;

            if (propBlock == null)
                propBlock = new MaterialPropertyBlock();

            cachedRenderers = GetComponentsInChildren<Renderer>(true);
            cachedColors = new Color[cachedRenderers.Length];

            for (int i = 0; i < cachedRenderers.Length; i++)
            {
                cachedColors[i] = GetCurrentColor(cachedRenderers[i]);
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

            string[] colorProperties = { "_BaseColor", "_Color" };
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

        void Update()
        {
            if (!isInitialized || hasFinishedFadeIn || gameManager == null)
                return;

            double currentTime = gameManager.GetCurrentMusicTimeMs();
            double fadeStartTime = hitTime - timePreempt;
            double fadeEndTime = fadeStartTime + timeFadeIn;

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

        private void SetAlpha(float alpha)
        {
            if (cachedRenderers == null || cachedColors == null) return;

            for (int i = 0; i < cachedRenderers.Length; i++)
            {
                var renderer = cachedRenderers[i];
                if (renderer == null) continue;

                renderer.GetPropertyBlock(propBlock);

                Color colorWithAlpha = cachedColors[i];
                colorWithAlpha.a = cachedColors[i].a * alpha;
                propBlock.SetColor("_BaseColor", colorWithAlpha);
                propBlock.SetColor("_Color", colorWithAlpha);

                renderer.SetPropertyBlock(propBlock);
            }
        }

        public void ResetState()
        {
            isInitialized = false;
            hasFinishedFadeIn = false;
            SetAlpha(0f);
        }
    }
}
