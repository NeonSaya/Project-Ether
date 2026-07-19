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

        [Header("视觉增强")]
        public float glowIntensity = 2.5f; // 发光倍率

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

        // 缓存渲染器引用
        private Renderer bgDiscRenderer;
        private Renderer rotDiscRenderer;
        private Renderer approachRingRenderer;
        private MaterialPropertyBlock _propBlock;
        private static readonly int PropTintColor = Shader.PropertyToID("_TintColor");
        private static readonly int PropColor = Shader.PropertyToID("_Color");
        private static readonly int PropBaseColor = Shader.PropertyToID("_BaseColor");

        // RPM 计算
        private float rotationDeltaAccumulator = 0f;
        // Bonus
        private int bonusCount = 0;
        private float bonusThreshold = 0f;

        private IObjectPool<GameObject> myPool;

        // 指环对象池
        private Queue<Transform> ringPool = new Queue<Transform>();

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
        // 记录最大允许旋转角度（分数上限）
        private float maxPossibleRotation = 0f;
        // 下一次获得 Bonus 的阈值
        private float nextBonusThreshold = 0f;


        public void Initialize(SpinnerObject data, RhythmGameManager manager, IObjectPool<GameObject> pool, Vector3 fixPosition, float od)
        {
            BoxCollider boxCol = GetComponent<BoxCollider>();
            this.spinnerData = data;
            this.gameManager = manager;
            this.myPool = pool;

            transform.position = fixPosition;
            transform.rotation = Quaternion.LookRotation(transform.position - Camera.main.transform.position);
            transform.localScale = Vector3.one * scaleSize;




            // -------------------------------------------------------------
            // 🔥 [核心修改] Lazer 转盘算法
            // -------------------------------------------------------------

            // 1. 计算持续时间 (秒)
            float duration = (float)(data.EndTime - data.StartTime) / 1000f;

            // 2. 根据 OD 计算理论最大转速 (RPM)
            float maxRPM = CalculateMaxRPM(od);

            // 3. 计算分数上限 (Max Angle)
            // 公式：最大总角度 = 持续时间 * (RPM / 60) * 360
            this.maxPossibleRotation = duration * (maxRPM / 60f) * 360f;

            // 4. 计算 "通关" (Hit 300) 所需的角度
            // 在 osu! 中，通关通常比满分容易得多。
            // 一般通关只需要达到最大值的 60% ~ 75% 即可 (这里设为 70%)
            this.angleRequirement = this.maxPossibleRotation * 0.7f;

            // [VR 特供修正]
            // 考虑到 VR 里抡胳膊比鼠标画圈累，我们可以加一个 "VR 难度系数"
            // 如果觉得太累，把这个系数调低 (比如 0.8f)
            float vrDifficultyFactor = 1.2f;
            this.maxPossibleRotation *= vrDifficultyFactor;
            this.angleRequirement *= vrDifficultyFactor;

            // 保底机制：极短的转盘至少要转 1 圈
            this.angleRequirement = Mathf.Max(this.angleRequirement, 360f);

            // 5. 初始化 Bonus
            // 只有转过了 angleRequirement 才能开始拿 Bonus
            // Bonus 每转一圈 (360度) 给一次
            this.nextBonusThreshold = this.angleRequirement + 360f;

            // -------------------------------------------------------------

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

            // -------------------------------------------------------
            // 1. 查找视觉组件
            // -------------------------------------------------------

            // 查找背景盘 (Visual_Container/Disc_Background)
            if (bgDiscRenderer == null)
            {
                Transform t = transform.Find("Visual_Container/Disc_Background");
                if (t) bgDiscRenderer = t.GetComponent<Renderer>();
            }

            // 查找旋转盘 (Visual_Container/Disc_Rotating)
            if (rotDiscRenderer == null)
            {
                Transform t = transform.Find("Visual_Container/Disc_Rotating");
                if (t) rotDiscRenderer = t.GetComponent<Renderer>();
            }

            // 查找缩圈 (ApproachCircle)
            if (approachRingRenderer == null)
            {
                Transform t = transform.Find("ApproachCircle");
                if (t) approachRingRenderer = t.GetComponent<Renderer>();
            }

            // -------------------------------------------------------
            // 2. 计算 HDR 发光颜色
            // -------------------------------------------------------

            // 假设转盘是白色的或者跟随 Combo 颜色 (通常转盘是固定的颜色，或者白色)
            // 如果你有专门的颜色变量，替换这里的 Color.white
            Color baseColor = Color.white;

            // 计算高亮色 (RGB 乘倍率)
            Color hdrColor = new Color(
                baseColor.r * glowIntensity,
                baseColor.g * glowIntensity,
                baseColor.b * glowIntensity,
                1.0f
            );

            if (_propBlock == null) _propBlock = new MaterialPropertyBlock();

            // -------------------------------------------------------
            // 3. 应用颜色到盘子
            // -------------------------------------------------------

            // 设置背景盘
            if (bgDiscRenderer != null)
            {
                bgDiscRenderer.GetPropertyBlock(_propBlock);
                // 稍微暗一点，做成底座的感觉
                Color bgHdr = hdrColor * 0.5f;
                _propBlock.SetColor(PropTintColor, bgHdr);
                _propBlock.SetColor(PropColor, bgHdr);
                _propBlock.SetColor(PropBaseColor, bgHdr);
                bgDiscRenderer.SetPropertyBlock(_propBlock);
            }

            // 设置旋转盘 (最亮的核心)
            if (rotDiscRenderer != null)
            {
                rotDiscRenderer.GetPropertyBlock(_propBlock);
                _propBlock.SetColor(PropTintColor, hdrColor);
                _propBlock.SetColor(PropColor, hdrColor);
                _propBlock.SetColor(PropBaseColor, hdrColor);
                rotDiscRenderer.SetPropertyBlock(_propBlock);
            }

            // -------------------------------------------------------
            // 4. 设置缩圈 (如果有)
            // -------------------------------------------------------
            if (approachRingRenderer != null)
            {
                approachRingRenderer.GetPropertyBlock(_propBlock);
                // 缩圈通常用红色或者警告色，这里假设跟随 HDR 白
                // 你也可以换成 Color.red * glowIntensity
                _propBlock.SetColor(PropTintColor, hdrColor);
                _propBlock.SetColor(PropColor, hdrColor);
                approachRingRenderer.SetPropertyBlock(_propBlock);

                // 别忘了初始化你的缩圈逻辑
                var scaler = approachRingRenderer.GetComponent<ApproachCircleScaler>();
                if (scaler != null)
                {
                    scaler.Initialize(spinnerData.StartTime, spinnerData.EndTime - spinnerData.StartTime, gameManager);
                }
            }

            // -------------------------------------------------------
            // 5. 设置渲染队列，确保在 SB Overlay (3001) 之上
            // -------------------------------------------------------
            if (bgDiscRenderer != null) bgDiscRenderer.material.renderQueue = 3050;
            if (rotDiscRenderer != null) rotDiscRenderer.material.renderQueue = 3051;
            if (approachRingRenderer != null) approachRingRenderer.material.renderQueue = 3052;

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
                    float intensity = Mathf.Clamp01(Progress);

                    if (HapticManager.Instance != null)
                    {
                        float hapticStr = Mathf.Lerp(
                            HapticManager.Instance.profile.SpinnerMinIntensity,
                            HapticManager.Instance.profile.SpinnerMaxIntensity,
                            intensity
                        );
                        HapticManager.Instance.PlayContinuous(true, hapticStr);
                    }

                    AudioManager.Instance?.UpdateSpinnerLoop(true, intensity);
                }
                else
                {
                    AudioManager.Instance?.UpdateSpinnerLoop(false, 0);
                }
            }

            // 奖励判定
            if (totalRotationAngle >= nextBonusThreshold)
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
                // 克隆指环视觉
                Transform newRing = null;
                TrailRenderer newTrail = null;

                // 只有当模板存在时才克隆
                if (trackerRing)
                {
                    // 优先从池里拿一个，如果没有才实例化新对象
                    if (ringPool.Count > 0)
                    {
                        newRing = ringPool.Dequeue();
                        newRing.gameObject.SetActive(true);
                    }
                    else
                    {
                        newRing = Instantiate(trackerRing, transform);
                    }

                    newTrail = newRing.GetComponent<TrailRenderer>();
                    if (newTrail != null)
                    {
                        newTrail.Clear();       // 清除旧数据
                        newTrail.emitting = true; // 开始发射
                        newTrail.startWidth = 0.015f; // 起始宽度 (非常细，约1.5厘米)
                        newTrail.endWidth = 0f;       // 结束宽度 (尖尾)
                        newTrail.time = 0.2f;        // 持续时间 (0.2秒消失)
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
                // 超时判断
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
                    var state = handStates[hand];
                    if (state.ringInstance != null)
                    {
                        if (state.trail != null)
                        {
                            state.trail.emitting = false;
                            state.trail.Clear();
                        }

                        state.ringInstance.gameObject.SetActive(false);
                        ringPool.Enqueue(state.ringInstance);
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

        // 计算 RPM 的辅助函数
        float CalculateMaxRPM(float od)
        {
            if (od < 5) return 250f + (od * 26f); // (380-250)/5 = 26
            return 380f + ((od - 5f) * 10f);      // (430-380)/5 = 10
        }

        private void AddBonus()
        {
            // 1. 推进阈值，确保下一圈还能触发
            nextBonusThreshold += 360f;

            // 2. 震动反馈 (始终保留，保持手感)
            if (HapticManager.Instance != null)
            {
                HapticManager.Instance.PlayHapticBoth(0.4f, 0.05f);
            }

            // 3. 判断是否已达到分数上限
            bool isCapped = totalRotationAngle > maxPossibleRotation;

            // 4. 更新 UI 和 分数
            if (isCapped)
            {
                // --- 达到上限状态 ---
                if (bonusText)
                {
                    bonusText.gameObject.SetActive(true);
                    bonusText.text = LocalizationManager.GetText("ui_max");
                    bonusText.color = Color.yellow;
                    bonusText.transform.localScale = Vector3.one * 1.5f;
                }
            }
            else
            {
                // --- 正常加分状态 ---
                bonusCount++;
                if (bonusText)
                {
                    bonusText.gameObject.SetActive(true);
                    bonusText.text = $"{bonusCount * 50}";
                    bonusText.color = Color.white;
                    bonusText.transform.localScale = Vector3.one * 1.5f;
                }

                // 加分
                if (gameManager != null && gameManager.scoreManager != null)
                {
                    gameManager.scoreManager.RegisterBonus(50);
                }
            }
        }

        private void FinishSpinner()
        {
            IsActive = false;
            if (angleRequirement > 0)
                Progress = totalRotationAngle / angleRequirement;
            else
                Progress = 1.0f;

            if (AudioManager.Instance != null) AudioManager.Instance.UpdateSpinnerLoop(false, 0);
            if (HapticManager.Instance != null)
            {
                HapticManager.Instance.PlayContinuous(true, 0f);
                HapticManager.Instance.PlayContinuous(false, 0f);
            }
            // 音量 = TimingPoint音量 × 样本倍率
            float vol = (spinnerData.TimingPointVolume / 100f) * (spinnerData.SampleVolume / 100f);
            // 判定逻辑
            if (Progress >= 1.0f)
            {
                if (AudioManager.Instance != null)
                    AudioManager.Instance.PlayHitSound(spinnerData);

                if (HapticManager.Instance != null)
                    // 双手震动，因为转盘通常很激烈
                    HapticManager.Instance.PlayHitHapticBoth((int)spinnerData.HitSound, vol);

                if (CodeOnlyVFX.Instance != null)
                {
                    CodeOnlyVFX.Instance.PlaySpinnerClear(transform.position);
                }

                gameManager.OnNoteHit(spinnerData, 1.0f);
            }
            else if (Progress > 0.8f) gameManager.OnNoteHit(spinnerData, 0.7f);
            else if (Progress > 0.5f) gameManager.OnNoteHit(spinnerData, 0.3f);
            else gameManager.OnNoteMiss(spinnerData);

            if (myPool != null) myPool.Release(gameObject);
            else Destroy(gameObject);
        }
    }
}