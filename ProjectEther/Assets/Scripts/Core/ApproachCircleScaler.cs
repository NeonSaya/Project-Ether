using UnityEngine;

namespace OsuVR
{
    public class ApproachCircleScaler : MonoBehaviour
    {
        [Tooltip("需要缩放的目标物体（Quad 或 Sprite）")]
        public Transform targetTransform;

        private Renderer _renderer; // 改用通用的 Renderer，兼容 MeshRenderer 和 SpriteRenderer
        private double hitTime;
        private double timePreempt;
        private bool isRunning = false;

        public void Initialize(double hitTimeMs, double timePreemptMs)
        {
            this.hitTime = hitTimeMs;
            this.timePreempt = timePreemptMs;
            this.isRunning = true;

            // 1. 自动获取引用
            if (targetTransform == null) targetTransform = transform;

            // 尝试获取 Renderer (Quad 是 MeshRenderer, Sprite 是 SpriteRenderer)
            _renderer = targetTransform.GetComponent<Renderer>();

            // 2. 初始状态：4倍大小
            targetTransform.localScale = Vector3.one * 4f;

            // 3. 确保物体激活
            targetTransform.gameObject.SetActive(true);

            // 4. 先隐藏 Renderer (避免一开始闪一下)
            if (_renderer) _renderer.enabled = false;
        }

        void Update()
        {
            if (!isRunning) return;

            var manager = FindObjectOfType<RhythmGameManager>();
            if (manager == null) return;

            double currentTime = manager.GetCurrentMusicTimeMs();
            double timeRemaining = hitTime - currentTime;

            // 状态 1: 时间太早 (还没进 AR 范围) -> 隐藏
            if (timeRemaining > timePreempt)
            {
                if (_renderer) _renderer.enabled = false;
            }
            // 状态 2: 时间到了 (击中/Miss) -> 隐藏
            else if (timeRemaining <= 0)
            {
                targetTransform.localScale = Vector3.one;
                if (_renderer) _renderer.enabled = false;
                isRunning = false;
            }
            // 状态 3: 正在缩圈 -> 显示并缩放
            else
            {
                if (_renderer) _renderer.enabled = true;

                // 计算进度 (0 = 开始, 1 = 结束)
                float progress = 1f - (float)(timeRemaining / timePreempt);

                // 线性插值：从 4x 到 1x
                float scale = Mathf.Lerp(4f, 1f, progress);
                targetTransform.localScale = Vector3.one * scale;
            }
        }
    }
}