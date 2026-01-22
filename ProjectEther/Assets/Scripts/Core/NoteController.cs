using System.Collections;
using UnityEngine;
using UnityEngine.Pool;

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

        [Header("视觉组件")]
        public Transform approachCircleObject;

        // 内部变量
        private double currentMusicTimeMs = 0;
        private double timeToHit = 0;
        private RhythmGameManager gameManager;
        private MeshRenderer circleRenderer;
        private Color originalColor;
        private MaterialPropertyBlock _propBlock;

        // 存池接口
        private IObjectPool<GameObject> myPool;

        /// <summary>
        /// 初始化音符
        /// </summary>
        public void Initialize(HitObject hitObj, Vector3 targetPos, float speed, float beatmapCS, Color comboColor, RhythmGameManager manager, IObjectPool<GameObject> pool)
        {
            // 存下池引用
            this.myPool = pool;
            ResetState();

            hitObject = hitObj;
            targetPosition = targetPos;
            moveSpeed = speed;
            gameManager = manager;
            isActive = true;
            hasBeenHit = false;
            isHovered = false;

            // 存下原始颜色，供 Update 逻辑使用
            this.originalColor = comboColor;
            if (_propBlock == null) _propBlock = new MaterialPropertyBlock();
            // 正确引用渲染器
            this.circleRenderer = GetComponentInChildren<MeshRenderer>();

            // 统一尺寸
            float finalSize = RhythmGameManager.CalculateVROsuSize(beatmapCS);
            transform.localScale = new Vector3(finalSize, finalSize, 0.02f);

            // Stacking 堆叠偏移
            Vector3 stackedPos = targetPos;
            stackedPos.z -= hitObj.StackOrder * 0.005f;

            // 直接设置位置，删掉后面那行重复的 transform.position = targetPos
            transform.position = stackedPos;

            // 应用 Combo 颜色
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                r.GetPropertyBlock(_propBlock);
                _propBlock.SetColor("_Color", comboColor);
                _propBlock.SetColor("_BaseColor", comboColor); // 兼容 URP
                r.SetPropertyBlock(_propBlock);
            }

            // [核心修复] 强制修复 AR (TimePreempt)
            // 如果 hitObject 数据里没算 AR (是0)，或者非常小，强制使用 Manager 的全局 AR
            if (hitObject.TimePreempt < 100)
            {
                // 如果 Manager 也没算，就默认 AR5 (1200ms)
                double defaultAR = (manager != null && manager.spawnOffsetMs > 100) ? manager.spawnOffsetMs : 1200;
                hitObject.TimePreempt = defaultAR;
            }

            transform.position = targetPos;

            if (Camera.main) transform.LookAt(Camera.main.transform);

            // 初始化视觉 (缩圈)
            if (approachCircleObject != null)
            {
                var scaler = approachCircleObject.GetComponent<ApproachCircleScaler>();
                if (scaler == null) scaler = approachCircleObject.gameObject.AddComponent<ApproachCircleScaler>();

                // 确保传入正确的 TimePreempt
                scaler.Initialize(hitObject.StartTime, hitObject.TimePreempt);
                approachCircleObject.gameObject.SetActive(true);
            }

            // 手动调用一次 Update 确保初始大小正确
            Update();
        }

        /// <summary>
        /// 重置状态 (防止复用时出现“半透明”或“已击打”的僵尸音符)
        /// </summary>
        private void ResetState()
        {
            isActive = true;
            hasBeenHit = false;
            isHovered = false;

            // 恢复可见性 (防止上一条命是 Miss 导致缩小成 0 了)
            transform.localScale = Vector3.one;

            // 恢复缩圈大小
            if (approachCircle != null)
            {
                approachCircle.localScale = Vector3.one * maxApproachScale;
                approachCircle.gameObject.SetActive(true);
            }

            // 恢复颜色 (尤其是 Alpha 值)
            if (circleRenderer != null)
            {
                circleRenderer.enabled = true;
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
                // [修复] 获取 AR 的双重保险
                // 优先用 hitObject 自带的，没有就用 Manager 的全局 AR，还没有就默认 1200
                double preempt = hitObject.TimePreempt > 0.1 ? hitObject.TimePreempt : (gameManager ? gameManager.spawnOffsetMs : 1200);

                // 计算进度 (1.0 -> 0.0)
                float progress = (float)(timeToHit / preempt);
                progress = Mathf.Clamp01(progress);

                // [手感优化] 视觉平滑处理：
                // 使用 Lerp 线性缩放是标准的 osu! 行为
                float currentScale = 1f + (maxApproachScale - 1f) * progress;

                // [视觉修复] 强制压扁 Z 轴
                // 只要你的 ApproachCircle 是平面贴图，Z=1 还是 Z=0.01 视觉上都是扁的
                // 如果你的预制体是 3D 的 (如 Cylinder)，把 z 设为 0.01f 可以强行压扁
                approachCircle.localScale = new Vector3(currentScale, currentScale, 1f);

                // [可选] 确保它朝向摄像机 (如果是 VR，这一步很重要，让圆圈始终正面朝你)
                approachCircle.LookAt(Camera.main.transform);
            }

            // 4. 更新颜色反馈 (被指着时变黄)
            if (circleRenderer != null)
            {
                circleRenderer.GetPropertyBlock(_propBlock);
                Color targetColor = isHovered ? Color.yellow : originalColor;
                _propBlock.SetColor("_Color", targetColor);
                _propBlock.SetColor("_BaseColor", targetColor);
                circleRenderer.SetPropertyBlock(_propBlock);
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

            // 计算时间偏差：当前时间 - 打击时间
            // 负数 = 提前 (Early), 正数 = 延迟 (Late)
            double diff = currentMusicTimeMs - hitObject.StartTime;

            // --- HIT 判定 ---
            // 条件1: diff >= -20 (缩圈几乎重合，只允许提前20ms)
            // 条件2: diff <= hitWindow (允许延迟 hitWindow 毫秒)
            // 条件3: isHovered (被射线指着)
            if (diff >= -20 && diff <= hitWindow)
            {
                if (isHovered)
                {
                    OnHit(diff);
                }
            }
            // --- 保护逻辑 ---
            // 如果 diff < -20 (打太早了)，直接 return，什么都不做
            // 这样玩家手放在那里不动，也不会触发 Miss，直到缩圈到位自动触发 Hit
            else if (diff < -20)
            {
                return;
            }
            // --- MISS 判定 ---
            // 条件：当前时间已经超过了 (打击时间 + 宽容度) 且还没被打中
            else if (diff > hitWindow)
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
            float duration = 0.2f;
            Vector3 startScale = approachCircle.localScale;
            Color startColor = originalColor; // 使用存下的颜色
            Color endColor = startColor;
            endColor.a = 0f;

            while (timer < duration)
            {
                timer += Time.deltaTime;
                float t = timer / duration;
                approachCircle.localScale = Vector3.Lerp(startScale, startScale * 1.5f, t);

                // 更新透明度
                if (circleRenderer != null)
                {
                    circleRenderer.GetPropertyBlock(_propBlock);
                    Color c = Color.Lerp(startColor, endColor, t);
                    _propBlock.SetColor("_Color", c);
                    _propBlock.SetColor("_BaseColor", c);
                    circleRenderer.SetPropertyBlock(_propBlock);
                }
                yield return null;
            }

            ReturnToPool(); // 替换 Destroy(gameObject)
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
                transform.localScale = Vector3.Lerp(startScale, Vector3.zero, timer / duration);
                yield return null;
            }

            ReturnToPool(); // ✅ 替换 Destroy(gameObject)
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

        /// <summary>
        /// Destory
        /// </summary>
        private void ReturnToPool()
        {
            StopAllCoroutines();

            if (myPool != null)
            {
                myPool.Release(gameObject);
            }
            else
            {
                Destroy(gameObject); // 兜底：如果池子没了直接销毁
            }
        }
    }
}