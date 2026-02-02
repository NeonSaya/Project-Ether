using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Pool;

namespace OsuVR
{
    [RequireComponent(typeof(Collider))]
    public class SpinnerController : MonoBehaviour
    {
        [Header("数据引用")]
        public SpinnerObject spinnerData;

        [Header("视觉组件 - 核心")]
        public Transform discRotating;
        public Transform approachCircle;
        public GameObject warningObject;
        public Image meterImage;
        public TextMeshProUGUI bonusText;
        public Transform trackerRing;
        private TrailRenderer ringTrail;

        [Header("判定参数")]
        [Tooltip("旋转灵敏度倍率 (值越大，画一圈算的角度越多)")]
        public float rotationMultiplier = 1.1f; // 稍微降低一点，因为现在画小圈更容易了

        [Tooltip("转盘整体大小倍率")]
        public float scaleSize = 0.5f;

        [Tooltip("转速要求倍率：每秒需要转多少圈 (默认 5 圈)")]
        public float rotationsPerSecond = 5f;

        [Header("手感参数")]
        [Tooltip("虚拟圆心跟随速度：越小越容忍大范围移动，越大要求画圈更圆 (建议 2-5)")]
        public float centerFollowSpeed = 3.0f;

        [Tooltip("最小画圈半径：如果画的圈太小(原地抖动)，不计入旋转")]
        public float minDrawingRadius = 0.05f;

        [Tooltip("视觉平滑系数")]
        public float visualSmoothing = 20f;

        // --- 状态变量 ---
        public bool IsActive { get; private set; } = true;
        public float CurrentRPM { get; private set; } = 0f;
        public float Progress { get; private set; } = 0f;

        public bool isHovered = false;
        private RhythmGameManager gameManager;
        private float totalRotationAngle = 0f;
        private float currentVisualRotation = 0f;
        private float targetVisualRotation = 0f;
        private float angleRequirement = 0f;

        // RPM 计算
        private float rotationDeltaAccumulator = 0f;
        // Bonus
        private int bonusCount = 0;
        private float bonusThreshold = 0f;

        private IObjectPool<GameObject> myPool;

        // ✅ [数据结构升级] 
        // 记录: <手柄, (上一帧角度, 上次时间, 虚拟圆心位置)>
        private Dictionary<RayController, HandState> handStates = new Dictionary<RayController, HandState>();

        // 定义一个结构体来存状态，比Tuple清晰
        private class HandState
        {
            public float lastAngle;
            public float lastTime;
            public Vector2 virtualCenter; // 这一只手的动态圆心
            public bool isInitialized;
            public Transform ringInstance;
            public TrailRenderer trail; // 是否刚进入
        }

        public void Initialize(SpinnerObject data, RhythmGameManager manager, IObjectPool<GameObject> pool, Vector3 fixPosition)
        {
            BoxCollider boxCol = GetComponent<BoxCollider>();
            this.spinnerData = data;
            this.gameManager = manager;
            this.myPool = pool;

            transform.position = fixPosition;
            transform.rotation = Quaternion.LookRotation(transform.position - Camera.main.transform.position);
            transform.localScale = Vector3.one * scaleSize;

            // 🔥 [难度提升] 计算目标角度
            float duration = (float)(data.EndTime - data.StartTime) / 1000f;

            // 公式：需要转的总圈数 = 时长 * 每秒目标圈数
            // 例如：时长5秒，要求每秒转4圈 -> 总共需要转 20 圈
            float requiredRotations = duration * rotationsPerSecond;

            // [可选] 极短转盘的低保：防止时长 0.1秒 这种算出来只需转 0.2 圈太容易
            // 这里设置至少要转 1 圈才能通关 (你可以根据需求改成 0.5f 或其他值)
            requiredRotations = Mathf.Max(requiredRotations, 1f);

            this.angleRequirement = requiredRotations * 360f;

            // 重置状态
            IsActive = true;
            totalRotationAngle = 0f;
            currentVisualRotation = 0f;
            targetVisualRotation = 0f;
            bonusCount = 0;
            bonusThreshold = angleRequirement + 360f;
            rotationDeltaAccumulator = 0f;

            handStates.Clear();

            if (boxCol == null)
            {
                // 销毁旧的 (如 SphereCollider)
                Collider oldCol = GetComponent<Collider>();
                if (oldCol != null) Destroy(oldCol);

                // 添加新的 BoxCollider
                boxCol = gameObject.AddComponent<BoxCollider>();
            }
            boxCol.size = new Vector3(20f, 20f, 0.01f);
            boxCol.isTrigger = true;

            // UI 重置
            if (meterImage) { meterImage.fillAmount = 0f; meterImage.color = Color.white; }
            if (warningObject) warningObject.SetActive(true);
            if (bonusText) { bonusText.gameObject.SetActive(false); bonusText.text = ""; }
            if (approachCircle) approachCircle.localScale = Vector3.one * 4f;
            if (trackerRing)
            {
                // 尝试获取组件 (如果还没获取过)
                if (ringTrail == null) ringTrail = trackerRing.GetComponent<TrailRenderer>();

                // 确保初始状态是关闭且干净的
                if (ringTrail != null)
                {
                    ringTrail.Clear(); // 清除旧轨迹
                    ringTrail.emitting = false; // 停止发射
                }
                trackerRing.gameObject.SetActive(false);
            }
        }

        void Update()
        {
            if (!IsActive || gameManager == null) return;

            double currentTime = gameManager.GetCurrentMusicTimeMs();

            if (currentTime > spinnerData.EndTime)
            {
                FinishSpinner();
                return;
            }

            if (approachCircle)
            {
                double duration = spinnerData.EndTime - spinnerData.StartTime;
                double timeLeft = spinnerData.EndTime - currentTime;
                approachCircle.localScale = Vector3.one * Mathf.Clamp01((float)(timeLeft / duration)) * 4f;
            }

            CleanUpInactiveHands();

            // 视觉平滑
            currentVisualRotation = Mathf.Lerp(currentVisualRotation, targetVisualRotation, Time.deltaTime * visualSmoothing);
            if (discRotating)
            {
                discRotating.localEulerAngles = new Vector3(0, 0, -currentVisualRotation);
            }

            // RPM 计算
            float instantaneousRPM = (rotationDeltaAccumulator / Time.deltaTime) / 6f;
            CurrentRPM = Mathf.Lerp(CurrentRPM, instantaneousRPM, Time.deltaTime * 5f);
            rotationDeltaAccumulator = 0f;

            if (meterImage && angleRequirement > 0)
            {
                Progress = totalRotationAngle / angleRequirement;
                meterImage.fillAmount = Mathf.Clamp01(Progress);
                if (Progress >= 1f) meterImage.color = Color.cyan;
            }

            // 反馈音效和震动
            if (IsActive && AudioManager.Instance != null && HapticManager.Instance != null)
            {
                // 只有转起来才震动 (RPM > 50)
                if (CurrentRPM > 50)
                {
                    // 强度随进度增加 (0.1 -> 1.0)
                    float intensity = Mathf.Clamp01(Progress);

                    // 1. 震动 (线性增强)
                    float hapticStr = Mathf.Lerp(
                        HapticManager.Instance.profile.SpinnerMinIntensity,
                        HapticManager.Instance.profile.SpinnerMaxIntensity,
                        intensity
                    );
                    HapticManager.Instance.PlayContinuous(true, hapticStr);

                    // 2. 音效 (Spinning Loop)
                    AudioManager.Instance.UpdateSpinnerLoop(true, intensity);
                }
                else
                {
                    AudioManager.Instance.UpdateSpinnerLoop(false, 0);
                }
            }

            // 奖励判定
            if (totalRotationAngle > bonusThreshold)
            {
                AddBonus();
            }

            if (bonusText && bonusText.gameObject.activeSelf)
            {
                bonusText.transform.localScale = Vector3.Lerp(bonusText.transform.localScale, Vector3.one, Time.deltaTime * 5f);
            }
        }

        // =================================================================================
        // 🔥 核心旋转逻辑 (任意位置画圈版)
        // =================================================================================
        public void UpdateRotation(Vector3 hitPoint, RayController source)
        {
            // 1. 转为本地坐标 (Z轴由HitPoint决定，这里只取XY平面)
            Vector3 localPoint3D = transform.InverseTransformPoint(hitPoint);
            Vector2 currentPos = new Vector2(localPoint3D.x, localPoint3D.y);
            float currentTime = Time.time;

            // 2. 获取或初始化状态 (如果是新进来的手)
            if (!handStates.ContainsKey(source))
            {
                // ✅ [新增] 克隆指环视觉
                Transform newRing = null;
                TrailRenderer newTrail = null;

                // 只有当模板存在时才克隆
                if (trackerRing)
                {
                    // 克隆一个新的指环
                    newRing = Instantiate(trackerRing, transform);
                    newRing.gameObject.SetActive(true); // 确保克隆体是显示的

                    newTrail = newRing.GetComponent<TrailRenderer>();
                    if (newTrail != null)
                    {
                        newTrail.Clear();       // 清除旧数据
                        newTrail.emitting = true; // 开始发射

                        // ✅ [关键] 代码强制把拖尾变细
                        newTrail.startWidth = 0.015f; // 起始宽度 (非常细，约1.5厘米)
                        newTrail.endWidth = 0f;       // 结束宽度 (尖尾)
                        newTrail.time = 0.25f;        // 持续时间 (0.25秒消失)
                    }
                }

                // 初始化并存入状态
                handStates[source] = new HandState
                {
                    lastTime = currentTime,
                    lastAngle = 0f, // 初始角度暂定0，反正第一帧不计算Delta
                    virtualCenter = currentPos, // 刚进来时，圆心就是当前点
                    isInitialized = false,
                    ringInstance = newRing, // 绑定视觉
                    trail = newTrail
                };
            }

            // 获取当前手的状态
            HandState state = handStates[source];

            // 3. 🔥 [动态圆心逻辑]
            // 圆心会缓慢跟随手柄当前位置。
            // 如果你画圈，currentPos 始终围着 virtualCenter 转。
            // 如果你平移，virtualCenter 会跟过去。
            state.virtualCenter = Vector2.Lerp(state.virtualCenter, currentPos, Time.deltaTime * centerFollowSpeed);

            // 4. 计算相对于 "虚拟圆心" 的角度
            // 这样无论你在转盘的哪个角落画圈，只要你在绕着你的虚拟圆心转，就算数
            Vector2 direction = currentPos - state.virtualCenter;

            // [防抖] 如果画的半径太小 (比如只是手抖)，不计算角度，防止数值乱跳
            if (direction.magnitude < minDrawingRadius)
            {
                return;
            }

            // 计算角度 (Atan2 返回 -180 到 180)
            float currentAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            // 5. 更新指环 (视觉反馈在击打点)
            if (state.ringInstance != null)
            {
                state.ringInstance.position = hitPoint;
                state.ringInstance.position -= transform.forward * 0.01f; // 防穿模，稍微浮起来
                state.ringInstance.rotation = transform.rotation;
            }

            // 6. 计算增量
            if (state.isInitialized && currentTime > state.lastTime)
            {
                float delta = currentAngle - state.lastAngle;

                // 处理 ±180 度跨越突变
                if (delta > 180f) delta -= 360f;
                if (delta < -180f) delta += 360f;

                // [限制] 物理过滤：防止追踪丢帧导致的瞬间180度跳变
                if (Mathf.Abs(delta) < 120f && Mathf.Abs(delta) > 0.01f)
                {
                    // 速度越快(delta越大)，加分越多
                    float validRotation = Mathf.Abs(delta) * rotationMultiplier;

                    totalRotationAngle += validRotation;
                    targetVisualRotation += validRotation;
                    rotationDeltaAccumulator += validRotation; // 喂给 RPM 计速器

                    // 如果有警告提示，转动起来后隐藏它
                    if (warningObject && warningObject.activeSelf) warningObject.SetActive(false);
                }
            }
            else
            {
                state.isInitialized = true; // 第一帧只记录，不计算
            }

            // 7. 更新状态供下一帧使用
            state.lastAngle = currentAngle;
            state.lastTime = currentTime;
        }

        private void CleanUpInactiveHands()
        {
            float currentTime = Time.time;
            List<RayController> toRemove = null;

            foreach (var kvp in handStates)
            {
                if (currentTime - kvp.Value.lastTime > 0.1f)
                {
                    if (toRemove == null) toRemove = new List<RayController>();
                    toRemove.Add(kvp.Key);
                }
            }

            if (toRemove != null)
            {
                foreach (var hand in toRemove)
                {
                    // ✅ [新增] 销毁这只手对应的指环物体
                    var state = handStates[hand];
                    if (state.ringInstance != null)
                    {
                        Destroy(state.ringInstance.gameObject);
                    }
                    handStates.Remove(hand);
                }
            }

            if (handStates.Count == 0 && trackerRing && trackerRing.gameObject.activeSelf)
            {

                if (ringTrail != null)
                {
                    ringTrail.emitting = false;
                    ringTrail.Clear();
                }
                trackerRing.gameObject.SetActive(false);
            }
        }

        private void AddBonus()
        {
            bonusCount++;
            bonusThreshold += 360f;
            if (bonusText)
            {
                bonusText.gameObject.SetActive(true);
                bonusText.text = (bonusCount * 1000).ToString();
                bonusText.transform.localScale = Vector3.one * 1.5f;
            }

            if (HapticManager.Instance != null)
            {
                HapticManager.Instance.PlayHapticBoth(0.4f, 0.05f);
            }
        }

        private void FinishSpinner()
        {
            IsActive = false;
            Progress = totalRotationAngle / angleRequirement;

            // 判定逻辑保持不变
            if (Progress >= 1.0f)
            {
                if (AudioManager.Instance != null)
                    AudioManager.Instance.PlayHitSound(spinnerData);

                if (HapticManager.Instance != null)
                    // 双手震动，因为转盘通常很激烈
                    HapticManager.Instance.PlayHitHapticBoth((int)spinnerData.HitSound);
                gameManager.OnNoteHit(spinnerData, 0);
            }
            else if (Progress > 0.9f) gameManager.OnNoteHit(spinnerData, 1);
            else if (Progress > 0.5f) gameManager.OnNoteHit(spinnerData, 2);
            else gameManager.OnNoteMiss(spinnerData);

            if (myPool != null) myPool.Release(gameObject);
            else Destroy(gameObject);
        }
    }
}