using UnityEngine;
using System.Collections; // 必须引用，用于协程动画

namespace OsuVR
{
    /// <summary>
    /// 音符控制器：控制音符的缩圈动画和判定逻辑（去插件原生版）
    /// </summary>
    public class NoteController : MonoBehaviour
    {
        [Header("音符配置")]
        public HitObject hitObject;
        public Vector3 targetPosition;
        public float moveSpeed = 5.0f;

        [Header("缩圈动画")]
        public Transform approachCircle; // 缩圈圆环的Transform

        [Header("判定设置")]
        [Tooltip("判定窗口（毫秒）：在打击时间前后多少毫秒内算击中")]
        public float hitWindow = 250f;

        [Tooltip("最大缩圈倍数：圆环开始时是Note的几倍大")]
        public float maxApproachScale = 4f;

        [Header("状态")]
        public bool isActive = true;
        public bool hasBeenHit = false;
        public bool isHovered = false; // 当前帧是否被射线指着

        // 内部变量
        private double currentMusicTimeMs = 0;
        private double timeToHit = 0;
        private RhythmGameManager gameManager;
        private MeshRenderer circleRenderer;
        private Color originalColor;

        /// <summary>
        /// 初始化音符
        /// </summary>
        public void Initialize(HitObject hitObj, Vector3 targetPos, float speed, RhythmGameManager manager)
        {
            hitObject = hitObj;
            targetPosition = targetPos;
            moveSpeed = speed;
            gameManager = manager;
            isActive = true;
            hasBeenHit = false;
            isHovered = false;

            // 设置位置和朝向
            transform.position = targetPos;
            transform.LookAt(Vector3.zero); // 面向玩家

            // 初始化视觉组件
            if (approachCircle != null)
            {
                circleRenderer = approachCircle.GetComponent<MeshRenderer>();
                if (circleRenderer != null)
                {
                    originalColor = circleRenderer.material.color;
                }
                // 初始化缩圈大小
                approachCircle.localScale = Vector3.one * maxApproachScale;
            }
        }

        /// <summary>
        /// 每帧更新：负责视觉动画
        /// </summary>
        void Update()
        {
            if (!isActive) return;

            // 1. 获取精准时间
            if (gameManager != null)
            {
                currentMusicTimeMs = gameManager.GetCurrentMusicTimeMs();
            }

            // 2. 计算倒计时
            timeToHit = hitObject.StartTime - currentMusicTimeMs;

            // 3. 更新缩圈动画 (Progress: 1.0 -> 0.0)
            if (approachCircle != null)
            {
                float progress = (float)(timeToHit / hitObject.TimePreempt);
                progress = Mathf.Clamp01(progress);

                // 线性插值计算大小
                float currentScale = 1f + (maxApproachScale - 1f) * progress;
                approachCircle.localScale = Vector3.one * currentScale;
            }

            // 4. 更新颜色反馈 (被指着时变黄)
            if (circleRenderer != null)
            {
                if (isHovered)
                    circleRenderer.material.color = Color.yellow;
                else
                    circleRenderer.material.color = originalColor;
            }
        }

        /// <summary>
        /// 晚于Update执行：负责逻辑判定
        /// 确保LaserShooter已经完成了这一帧的射线检测
        /// </summary>
        void LateUpdate()
        {
            if (!isActive) return;

            CheckHitOrMiss();

            // ⚠️ 关键：每一帧结束时重置悬停状态
            // 这样下一帧 LaserShooter 必须再次射中它，isHovered 才会变回 true
            isHovered = false;
        }

        /// <summary>
        /// 检查 Hit 或 Miss
        /// </summary>
        private void CheckHitOrMiss()
        {
            if (hasBeenHit) return;

            double absDiff = System.Math.Abs(currentMusicTimeMs - hitObject.StartTime);

            // --- HIT 判定 ---
            // 条件：时间偏差在窗口内 AND 当前被射线指着
            if (absDiff <= hitWindow && isHovered)
            {
                OnHit(absDiff);
            }
            // --- MISS 判定 ---
            // 条件：当前时间已经超过了 (打击时间 + 宽容度) 且还没被打中
            else if (currentMusicTimeMs > hitObject.StartTime + hitWindow)
            {
                OnMiss();
            }
        }

        /// <summary>
        /// 供 LaserShooter 调用的接口
        /// </summary>
        public void OnRayHover()
        {
            isHovered = true;
        }

        /// <summary>
        /// 击中逻辑
        /// </summary>
        private void OnHit(double accuracy)
        {
            hasBeenHit = true;
            isActive = false;

            Debug.Log($"🔥 HIT! 时间: {hitObject.StartTime}ms, 偏差: {accuracy:F1}ms");

            // 通知管理器
            if (gameManager != null)
            {
                gameManager.OnNoteHit(hitObject, accuracy);
            }

            // 播放消失动画（替代 LeanTween）
            if (approachCircle != null)
            {
                StartCoroutine(HitEffectCoroutine());
            }
            else
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// 错过逻辑
        /// </summary>
        private void OnMiss()
        {
            hasBeenHit = true;
            isActive = false;

            Debug.Log($"💨 MISS! 时间: {hitObject.StartTime}ms");

            // 通知管理器
            if (gameManager != null)
            {
                gameManager.OnNoteMiss(hitObject);
            }

            // 播放Miss动画
            StartCoroutine(MissEffectCoroutine());
        }

        // --- 简单的原生动画协程 (替代插件) ---

        /// <summary>
        /// 击中效果：圆环瞬间变大并透明
        /// </summary>
        IEnumerator HitEffectCoroutine()
        {
            float timer = 0f;
            float duration = 0.2f; // 动画时长
            Vector3 startScale = approachCircle.localScale;
            Color startColor = circleRenderer != null ? circleRenderer.material.color : Color.white;
            Color endColor = startColor;
            endColor.a = 0f; // 变透明

            while (timer < duration)
            {
                timer += Time.deltaTime;
                float t = timer / duration;

                // 变大 1.5 倍
                approachCircle.localScale = Vector3.Lerp(startScale, startScale * 1.5f, t);

                // 变透明
                if (circleRenderer != null)
                {
                    circleRenderer.material.color = Color.Lerp(startColor, endColor, t);
                }

                yield return null;
            }

            Destroy(gameObject); // 动画播完，销毁物体
        }

        /// <summary>
        /// Miss效果：本体缩小消失
        /// </summary>
        IEnumerator MissEffectCoroutine()
        {
            float timer = 0f;
            float duration = 0.2f;
            Vector3 startScale = transform.localScale;

            while (timer < duration)
            {
                timer += Time.deltaTime;
                // 缩小到 0
                transform.localScale = Vector3.Lerp(startScale, Vector3.zero, timer / duration);
                yield return null;
            }

            Destroy(gameObject);
        }

        /// <summary>
        /// 调试辅助线
        /// </summary>
        void OnDrawGizmosSelected()
        {
            // 绘制判定球
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, 0.2f);
        }
    }
}