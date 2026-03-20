using UnityEngine;

namespace OsuVR
{
    public class ObjectFadeIn : MonoBehaviour
    {
        private double hitTime;
        private double timePreempt;
        private double timeFadeIn;
        private RhythmGameManager gameManager;
        private Renderer[] renderers;
        private MaterialPropertyBlock propBlock;
        private bool isInitialized = false;
        private bool hasFinishedFadeIn = false;

        public void Initialize(double hitTimeMs, double timePreemptMs, RhythmGameManager manager)
        {
            this.hitTime = hitTimeMs;
            this.timePreempt = timePreemptMs;
            this.timeFadeIn = timePreemptMs * (2.0 / 3.0);
            this.gameManager = manager;
            this.isInitialized = true;
            this.hasFinishedFadeIn = false;

            renderers = GetComponentsInChildren<Renderer>(true);
            if (propBlock == null)
                propBlock = new MaterialPropertyBlock();

            SetAlpha(0f);
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
            if (renderers == null) return;

            foreach (var renderer in renderers)
            {
                if (renderer == null) continue;

                renderer.GetPropertyBlock(propBlock);

                Color currentColor = Color.white;
                bool hasColorProperty = false;

                if (renderer.sharedMaterial != null)
                {
                    if (renderer.sharedMaterial.HasProperty("_BaseColor"))
                    {
                        currentColor = renderer.sharedMaterial.GetColor("_BaseColor");
                        hasColorProperty = true;
                        currentColor.a = alpha;
                        propBlock.SetColor("_BaseColor", currentColor);
                    }
                    else if (renderer.sharedMaterial.HasProperty("_Color"))
                    {
                        currentColor = renderer.sharedMaterial.GetColor("_Color");
                        hasColorProperty = true;
                        currentColor.a = alpha;
                        propBlock.SetColor("_Color", currentColor);
                    }
                }

                if (!hasColorProperty)
                {
                    currentColor = new Color(1f, 1f, 1f, alpha);
                    propBlock.SetColor("_Color", currentColor);
                    propBlock.SetColor("_BaseColor", currentColor);
                }

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
