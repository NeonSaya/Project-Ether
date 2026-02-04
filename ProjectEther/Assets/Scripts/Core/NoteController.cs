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

        private bool hoveringHandIsRight = true;

        [Header("视觉组件")]
        public Transform approachCircleObject;

        [Header("视觉增强")]
        public float glowIntensity = 2.3f;

        // 内部变量
        private double currentMusicTimeMs = 0;
        private double timeToHit = 0;
        private RhythmGameManager gameManager;
        private MeshRenderer circleRenderer;
        private Color originalColor;
        private MaterialPropertyBlock _propBlock;
        private Renderer[] allRenderers;
        public Vector3? nextNotePosition;

        // ✅ [优化] 缓存 Scaler 组件，避免每次 Initialize 都 Get
        private ApproachCircleScaler cachedScaler;

        // ✅ [优化] 缓存相机，杜绝 Update 里使用 Camera.main
        private static Camera _cachedMainCamera;
        private float lastDebugTime = 0f;
        // 添加一个变量来防止第一帧暴毙
        private bool isFirstFrame = true;
        private MeshRenderer bodyRenderer;
        private MeshRenderer overlayRenderer;

        private Camera MainCamera
        {
            get
            {
                if (_cachedMainCamera == null) _cachedMainCamera = Camera.main;
                return _cachedMainCamera;
            }
        }

        // 存池接口
        private IObjectPool<GameObject> myPool;

        /// <summary>
        /// 当物体被激活（或从池中取出）时调用
        /// </summary>
        void OnEnable()
        {
            // 强制复活！防止从池里拿出来还是死的
            isActive = true;
            isHovered = false;
            // 如果是从池里取出的，hasBeenHit 状态可能脏了，需要重置
            // 但 Initialize 会再次重置它，这里兜个底
            hasBeenHit = false;
        }

        /// <summary>
        /// ✅ [核心修复] 确保组件已缓存
        /// 防止 Prefab 是隐藏状态导致 Awake 不执行，从而引发空引用
        /// </summary>
        private void EnsureComponentsCached()
        {
            if (_propBlock == null)
                _propBlock = new MaterialPropertyBlock();

            if (allRenderers == null || allRenderers.Length == 0)
                allRenderers = GetComponentsInChildren<Renderer>(true);
        }

        // ✅ 新增：对应 RayController 的离开调用
        public void OnRayExit()
        {
            isHovered = false;
        }

        /// <summary>
        /// 初始化音符
        /// </summary>
        public void Initialize(HitObject hitObj, Vector3 targetPos, float speed, float beatmapCS, Color comboColor, RhythmGameManager manager, IObjectPool<GameObject> pool, Vector3? nextPos = null)
        {
            EnsureComponentsCached();

            // 存下池引用
            this.myPool = pool;
            ResetState();
            isFirstFrame = true;

            hitObject = hitObj;
            targetPosition = targetPos;
            moveSpeed = speed;
            gameManager = manager;
            isActive = true;
            hasBeenHit = false;
            isHovered = false;

            // 存下原始颜色，供 Update 逻辑使用
            this.originalColor = comboColor;
            this.originalColor.a = 1.0f;
            ApplyColor(this.originalColor);
            if (_propBlock == null) _propBlock = new MaterialPropertyBlock();
            // 正确引用渲染器
            this.circleRenderer = GetComponentInChildren<MeshRenderer>();
            // 存下下一个音符位置
            this.nextNotePosition = nextPos;

            // -----------------------------------------------------------
            // 1. 获取 Body 和 Overlay 的 Renderer 引用 (如果是首次)
            // -----------------------------------------------------------
            if (bodyRenderer == null)
            {
                Transform bodyTr = transform.Find("Body");
                // 如果找不到名为 Body 的子物体，就尝试用根物体（兼容旧Prefab结构）
                if (bodyTr) bodyRenderer = bodyTr.GetComponent<MeshRenderer>();
                else bodyRenderer = GetComponent<MeshRenderer>();
            }

            if (overlayRenderer == null)
            {
                Transform overlayTr = transform.Find("Overlay");
                if (overlayTr) overlayRenderer = overlayTr.GetComponent<MeshRenderer>();
            }

            // -----------------------------------------------------------
            // 2. 颜色与 HDR 发光计算
            // -----------------------------------------------------------
            this.originalColor = comboColor;
            this.originalColor.a = 1.0f;

            // 计算 HDR 高亮颜色 (RGB变亮，Alpha不变)
            Color hdrColor = new Color(
                originalColor.r * glowIntensity,
                originalColor.g * glowIntensity,
                originalColor.b * glowIntensity,
                1.0f
            );

            // 准备属性块
            if (_propBlock == null) _propBlock = new MaterialPropertyBlock();

            // A. 给 Body 设置发光颜色
            if (bodyRenderer != null)
            {
                bodyRenderer.GetPropertyBlock(_propBlock);
                _propBlock.SetColor("_Color", hdrColor);          // Standard / Particles
                _propBlock.SetColor("_BaseColor", hdrColor);      // URP
                _propBlock.SetColor("_EmissionColor", hdrColor);  // Emission
                bodyRenderer.SetPropertyBlock(_propBlock);
            }

            // B. 给 Overlay 设置发光颜色 (通常是白色高亮)
            if (overlayRenderer != null)
            {
                overlayRenderer.GetPropertyBlock(_propBlock);
                // Overlay 通常保持白色，但也要乘亮度
                Color overlayHdr = new Color(glowIntensity, glowIntensity, glowIntensity, 1f);

                _propBlock.SetColor("_Color", overlayHdr);
                _propBlock.SetColor("_BaseColor", overlayHdr);
                overlayRenderer.SetPropertyBlock(_propBlock);
            }

            // 统一尺寸
            float finalSize = RhythmGameManager.CalculateVROsuSize(beatmapCS);
            transform.localScale = new Vector3(finalSize, finalSize, 0.02f);
            // 确保有碰撞体
            Collider col = GetComponent<Collider>();
            if (col == null)
            {
                // 如果没有碰撞体，自动加一个球形碰撞体
                SphereCollider sc = gameObject.AddComponent<SphereCollider>();
                // 半径设为 0.5 (直径 1.0)，配合 transform.localScale 刚好匹配圆圈大小
                sc.radius = 0.5f;
                // 确保它是 Trigger 或者是普通碰撞体都可以，SphereCast 都能检测到
                sc.isTrigger = true;
            }
            // Stacking 堆叠偏移
            Vector3 stackedPos = targetPos;
            stackedPos.z -= hitObj.StackOrder * 0.01f;

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

            //if (Camera.main) transform.LookAt(Camera.main.transform);
            if (MainCamera != null) transform.LookAt(MainCamera.transform);

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
        }

        /// <summary>
        /// 晚于Update执行：负责逻辑判定
        /// 确保LaserShooter已经完成了这一帧的射线检测
        /// </summary>
        void LateUpdate()
        {
            if (!isActive) return;

            CheckHitOrMiss();
        }

        /// <summary>
        /// 检查 Hit 或 Miss
        /// </summary>
        private void CheckHitOrMiss()
        {
            if (hasBeenHit) return;

            if (gameManager == null) return;

            double now = gameManager.GetCurrentMusicTimeMs();
            double diff = now - hitObject.StartTime;

            // ✅ 调试日志：如果刚生成 diff 就很大，说明 Manager 的时间同步有问题
            if (isFirstFrame)
            {
                isFirstFrame = false;
                // 如果第一帧就延迟超过 100ms，打印警告
                if (diff > 100)
                {
                    Debug.LogWarning($"[Timing Lag] 音符生成延迟！Diff: {diff:F2}ms (Window: {hitWindow})");
                }

                // ✅ 核心保护：如果是第一帧且判定为 Miss，强制不判 Miss，给它一次机会
                // 这防止了“刚生成就立刻销毁”的问题
                if (diff > hitWindow)
                {
                    return;
                }
            }

            // --- HIT 判定 ---
            // 条件1: diff >= -20 (缩圈几乎重合，只允许提前20ms)
            // 条件2: diff <= hitWindow (允许延迟 hitWindow 毫秒)
            // 条件3: isHovered (被射线指着)
            if (diff >= -20 && diff <= hitWindow)
            {
                if (isHovered) 
                {
                    Debug.Log($"[Check] Diff: {diff:F2} | Window: {hitWindow}");
                    OnHit(diff, hoveringHandIsRight);
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
        /// ✅ [核心修复] 安全染色方法
        /// </summary>
        private void ApplyColor(Color color)
        {
            // 双重保险：以防 Initialize 没调或者被直接调用
            EnsureComponentsCached();

            foreach (var r in allRenderers)
            {
                if (r == null) continue;

                r.GetPropertyBlock(_propBlock); // 现在 _propBlock 绝对不为空
                _propBlock.SetColor("_Color", color);
                _propBlock.SetColor("_BaseColor", color);
                _propBlock.SetColor("_TintColor", color);
                _propBlock.SetColor("_EmissionColor", color);
                r.SetPropertyBlock(_propBlock);
            }
        }



        /// <summary>
        /// 供 LaserShooter 调用的接口
        /// </summary>
        public void OnRayHover(bool isRightHand)
        {
            Debug.Log($"[Note] 收到 Hover 信号！来自: {(isRightHand ? "右手" : "左手")}");

            isHovered = true;
            hoveringHandIsRight = isRightHand;
            CheckHitOrMiss();
        }

        /// <summary>
        /// 击中逻辑
        /// </summary>
        public void OnHit(double accuracy, bool isRightHand)
        {
            if (hasBeenHit || !isActive) return;
             Debug.Log($" OnHit! Color: {originalColor}");
            hasBeenHit = true;
            isActive = false;

            if (HapticManager.Instance == null)
            {
                Debug.LogError("❌ 【严重错误】HapticManager.Instance 为空！");
            }
            else
            {
                // 如果不为空，尝试震动
                if (((int)hitObject.HitSound & 4) > 0)
                {
                    // 重击！双手震动
                    HapticManager.Instance.PlayHitHapticBoth((int)hitObject.HitSound);
                }
                else
                {
                    // 普通打击，单手震动
                    HapticManager.Instance.PlayHitHaptic(isRightHand, (int)hitObject.HitSound);
                }
            }
            // 播放特效
            if (CodeOnlyVFX.Instance != null)
                CodeOnlyVFX.Instance.PlayHit(
                    transform.position,
                    transform.rotation,
                    transform.localScale.x,
                    originalColor,
                    this.nextNotePosition
                );

            // 播放音效 
            if (AudioManager.Instance == null)
            {
                Debug.LogError("❌ 【严重错误】AudioManager.Instance 为空！场景里没有挂载 AudioManager，或者它被销毁了！");
            }
            else
            {
                // 如果不为空，尝试播放
                AudioManager.Instance.PlayHitSound(this.hitObject);
            }



            // 通知管理器
            if (gameManager != null)
            {
                gameManager.OnNoteHit(hitObject, accuracy);
            }

            // 播放消失动画（替代 LeanTween）
            if (approachCircle != null) StartCoroutine(HitEffectCoroutine());
            else ReturnToPool();
        }

        /// <summary>
        /// 错过逻辑
        /// </summary>
        private void OnMiss()
        {
            if (hasBeenHit || !isActive) return;

            hasBeenHit = true;
            isActive = false;

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

            transform.SetParent(null);

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