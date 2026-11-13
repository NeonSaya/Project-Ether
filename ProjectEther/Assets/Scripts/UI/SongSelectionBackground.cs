using UnityEngine;

namespace OsuVR
{
    /// <summary>全画布背景与双层淡入淡出；快速选择时只排队最后一个目标。</summary>
    public sealed class SongSelectionBackground : MonoBehaviour
    {
        private const float FadeSeconds = 0.28f;
        private SongSelectionBackdropGraphic current;
        private SongSelectionBackdropGraphic incoming;
        private Texture2D currentTexture;
        private Texture2D incomingTexture;
        private string currentPath;
        private string incomingPath;
        private string queuedPath;
        private bool hasQueuedPath;
        private bool fading;
        private float elapsed;
        public bool IsTransitioning => fading;
        public string DisplayedPath => currentPath;

        public void Build()
        {
            var root = (RectTransform)transform;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;
            transform.SetAsFirstSibling();
            var group = gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            AddFull(
                root,
                "BackdropBase",
                SongSelectionStyle.Background,
                SongSelectionStyle.Background
            );
            var images = SongSelectionStyle.Rect(root, "BackgroundImages", 0, 0, 1200, 800);
            images.anchorMin = Vector2.zero;
            images.anchorMax = Vector2.one;
            images.offsetMin = images.offsetMax = Vector2.zero;
            current = AddFull(images, "BackgroundCurrent", Color.white, Color.white);
            incoming = AddFull(images, "BackgroundIncoming", Color.white, Color.white);
            // 直接在图片上统一压低亮度，避免多层羽化遮罩叠出亮边和分块感。
            Color libraryTint = new Color(0.09f, 0.11f, 0.17f, 1);
            Color detailTint = new Color(0.28f, 0.31f, 0.38f, 1);
            current.Configure(libraryTint, detailTint, 0.27f, 0.60f, 12);
            incoming.Configure(libraryTint, detailTint, 0.27f, 0.60f, 12);
            current.enabled = incoming.enabled = false;
        }

        private static SongSelectionBackdropGraphic AddFull(
            Transform parent,
            string name,
            Color left,
            Color right
        )
        {
            var rect = SongSelectionStyle.Rect(parent, name, 0, 0, 1200, 800);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var graphic = rect.gameObject.AddComponent<SongSelectionBackdropGraphic>();
            graphic.Configure(left, right, 0, 1, 12);
            return graphic;
        }

        public void SetPath(string path)
        {
            if (fading)
            {
                hasQueuedPath = path != incomingPath;
                queuedPath = path;
                return;
            }
            if (path == currentPath && (currentTexture != null || path == null))
                return;
            BeginTransition(path);
        }

        private void BeginTransition(string path)
        {
            incomingPath = path;
            Texture2D texture = SongMetaLoader.LoadBackground(path);
            incomingTexture = texture;
            incoming.SetTexture(texture);
            incoming.enabled = texture != null;
            incoming.transform.SetAsLastSibling();
            incoming.canvasRenderer.SetAlpha(0);
            current.canvasRenderer.SetAlpha(1);
            elapsed = 0;
            fading = currentTexture != null || texture != null;
            if (!fading)
                currentPath = path;
        }

        private void Update()
        {
            if (!fading)
                return;
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / FadeSeconds);
            float alpha = Mathf.SmoothStep(0, 1, progress);
            if (incomingTexture != null)
            {
                // 旧层维持覆盖，新层逐渐盖上，避免双透明叠加造成中途变黑。
                incoming.canvasRenderer.SetAlpha(alpha);
            }
            else
            {
                current.canvasRenderer.SetAlpha(1 - alpha);
            }
            if (progress < 1)
                return;
            Release(current, currentTexture);
            var old = current;
            current = incoming;
            incoming = old;
            currentTexture = incomingTexture;
            incomingTexture = null;
            current.canvasRenderer.SetAlpha(1);
            currentPath = incomingPath;
            fading = false;
            if (hasQueuedPath)
            {
                string next = queuedPath;
                hasQueuedPath = false;
                SetPath(next);
            }
        }

        public void RefreshUVs()
        {
            current?.SetVerticesDirty();
            incoming?.SetVerticesDirty();
        }

        private static void Release(SongSelectionBackdropGraphic graphic, Texture texture)
        {
            if (graphic == null)
                return;
            graphic.SetTexture(null);
            graphic.enabled = false;
            if (texture != null)
                Destroy(texture);
        }

        private void OnDestroy()
        {
            // 纹理由控制器持有，不依赖子 Graphic 的销毁先后顺序。
            if (currentTexture != null)
                Destroy(currentTexture);
            if (incomingTexture != null && incomingTexture != currentTexture)
                Destroy(incomingTexture);
        }
    }
}
