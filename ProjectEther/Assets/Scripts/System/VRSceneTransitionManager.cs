using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace OsuVR
{
    /// <summary>
    /// VR 场景过渡管理器：黑屏渐变 + 异步加载
    /// 使用 World Space Canvas 跟随摄像机，兼容 VR
    /// </summary>
    public class VRSceneTransitionManager : MonoBehaviour
    {
        public static VRSceneTransitionManager Instance { get; private set; }

        [Header("过渡参数")]
        public float fadeDuration = 0.5f;
        public Color fadeColor = Color.black;

        private CanvasGroup canvasGroup;
        private Canvas overlayCanvas;
        private bool isTransitioning;

        // =========================================================
        //  自动创建
        // =========================================================

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoCreate()
        {
            if (Instance != null) return;
            var go = new GameObject("[VRSceneTransition]");
            Instance = go.AddComponent<VRSceneTransitionManager>();
        }

        // =========================================================
        //  生命周期
        // =========================================================

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            CreateOverlay();
        }

        // =========================================================
        //  幕布构建（World Space Canvas，兼容 VR）
        // =========================================================

        private void CreateOverlay()
        {
            // Canvas 挂在自身，跟随摄像机
            overlayCanvas = gameObject.AddComponent<Canvas>();
            overlayCanvas.renderMode = RenderMode.WorldSpace;
            overlayCanvas.sortingOrder = 9999;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.dynamicPixelsPerUnit = 10f;

            canvasGroup = gameObject.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            // 全屏黑色面板
            var panel = new GameObject("FadePanel");
            panel.transform.SetParent(transform, false);

            var rt = panel.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(2000f, 2000f);

            var img = panel.AddComponent<Image>();
            img.color = fadeColor;
            img.raycastTarget = false;

        }

        // =========================================================
        //  跟随摄像机（每帧将 Canvas 放在相机前方）
        // =========================================================

        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                // 场景切换中相机可能短暂为 null，尝试查找
                cam = FindFirstObjectByType<Camera>();
                if (cam == null) return;
            }

            // Canvas 放在相机前方 0.5m，尺寸自适应
            transform.position = cam.transform.position + cam.transform.forward * 0.5f;
            transform.rotation = cam.transform.rotation;

            float scale = 0.001f; // World Space Canvas 缩放
            transform.localScale = new Vector3(scale, scale, scale);
        }

        // =========================================================
        //  公开接口
        // =========================================================

        /// <summary>
        /// 带黑屏渐变的异步场景切换
        /// </summary>
        public void TransitionToScene(string sceneName)
        {
            if (isTransitioning)
            {
                Debug.LogWarning("[Transition] 已在过渡中，忽略");
                return;
            }
            StartCoroutine(TransitionCoroutine(sceneName));
        }

        /// <summary>
        /// 是否正在过渡
        /// </summary>
        public bool IsTransitioning => isTransitioning;

        // =========================================================
        //  核心协程
        // =========================================================

        private IEnumerator TransitionCoroutine(string sceneName)
        {
            isTransitioning = true;

            // --- 第一幕：渐入黑屏 ---
            yield return StartCoroutine(Fade(0f, 1f, fadeDuration));

            // --- 第二幕：启动异步加载 ---
            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
            if (asyncLoad == null)
            {
                Debug.LogError($"[Transition] 无法加载场景: {sceneName}");
                yield return StartCoroutine(Fade(1f, 0f, fadeDuration));
                isTransitioning = false;
                yield break;
            }

            // 防止加载完瞬间硬切
            asyncLoad.allowSceneActivation = false;

            // --- 第三幕：等待加载进度 ---
            while (asyncLoad.progress < 0.9f)
            {
                yield return null;
            }

            Debug.Log($"[Transition] 场景 {sceneName} 加载完成，准备激活");

            // --- 第四幕：激活新场景，立即开始渐出 ---
            asyncLoad.allowSceneActivation = true;

            // 等待新场景加载完成
            while (!asyncLoad.isDone)
                yield return null;

            // --- 第五幕：渐出黑屏 ---
            yield return StartCoroutine(Fade(1f, 0f, fadeDuration));

            isTransitioning = false;
            Debug.Log($"[Transition] 过渡完成: {sceneName}");
        }

        // =========================================================
        //  渐变控制
        // =========================================================

        private IEnumerator Fade(float from, float to, float duration)
        {
            canvasGroup.alpha = from;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                t = t * t * (3f - 2f * t); // SmoothStep
                canvasGroup.alpha = Mathf.Lerp(from, to, t);
                yield return null;
            }

            canvasGroup.alpha = to;
        }
    }
}
