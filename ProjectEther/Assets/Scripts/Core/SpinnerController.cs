using System.Collections.Generic;
using TMPro;          // 引用 TextMeshPro
using UnityEngine;
using UnityEngine.UI; // 引用 UI
using UnityEngine.Pool;

namespace OsuVR
{
    [RequireComponent(typeof(Collider))]
    public class SpinnerController : MonoBehaviour
    {
        [Header("数据引用")]
        public SpinnerObject spinnerData;

        [Header("视觉组件 - 核心")]
        [Tooltip("旋转的主盘面 (Disc)")]
        public Transform discRotating;

        [Tooltip("缩圈 (Approach Circle)")]
        public Transform approachCircle;

        [Header("视觉组件 - UI")]
        [Tooltip("警告提示 (SPIN!)")]
        public GameObject warningObject;

        [Tooltip("进度条/计量表")]
        public Image meterImage;

        [Tooltip("奖励分数文本 (Bonus Text)")]
        public TextMeshProUGUI bonusText;

        [Tooltip("跟随射线的指环 (Tracker Ring)")]
        public Transform trackerRing;

        [Header("判定参数")]
        [Tooltip("旋转灵敏度倍率")]
        public float rotationMultiplier = 1.2f;

        [Tooltip("转盘整体大小倍率")]
        public float scaleSize = 0.5f;

        [Header("手感参数")]
        [Tooltip("中心死区半径 (米)：防止打中中心导致角度乱跳")]
        public float centerDeadZone = 0.05f;

        [Tooltip("视觉平滑系数：值越大越跟手，值越小越有惯性 (建议 15-20)")]
        public float visualSmoothing = 20f;

        // --- 状态变量 ---
        public bool IsActive { get; private set; } = true;
        public float CurrentRPM { get; private set; } = 0f;
        public float Progress { get; private set; } = 0f;

        public bool isHovered = false;
        private RhythmGameManager gameManager;
        private float totalRotationAngle = 0f;      // 累计旋转总角度 (判定用)
        private float currentVisualRotation = 0f;   // 当前视觉角度 (显示用)
        private float targetVisualRotation = 0f;    // 目标视觉角度 (插值用)
        private float angleRequirement = 0f;        // 通关所需角度

        private float rotationDeltaSinceLastFrame = 0f;
        // RPM 计算相关
        private float rotationDeltaAccumulator = 0f; 
        // Bonus 相关
        private int bonusCount = 0;
        private float bonusThreshold = 0f;

        // 对象池引用
        private IObjectPool<GameObject> myPool;

        // ✅ [只保留这一个字典] 记录每个手柄的状态：<手柄, (上一帧角度, 上次更新时间)>
        private Dictionary<RayController, (float angle, float time)> handStates = new Dictionary<RayController, (float, float)>();

        public void Initialize(SpinnerObject data, RhythmGameManager manager, IObjectPool<GameObject> pool)
        {
            this.spinnerData = data;
            this.gameManager = manager;
            this.myPool = pool;

            // 设置大小
            transform.localScale = Vector3.one * scaleSize;

            // 计算目标角度
            float duration = (float)(data.EndTime - data.StartTime) / 1000f;
            float requiredRotations = duration * 3f + 1f;
            this.angleRequirement = requiredRotations * 360f;

            // 重置状态
            IsActive = true;
            totalRotationAngle = 0f;
            currentVisualRotation = 0f;
            targetVisualRotation = 0f;
            bonusCount = 0;
            bonusThreshold = angleRequirement + 360f;
            rotationDeltaAccumulator = 0f;

            // 重置追踪状态
            if (handStates == null)
                handStates = new Dictionary<RayController, (float, float)>();
            else
                handStates.Clear();

            // 视觉重置
            if (meterImage) meterImage.fillAmount = 0f;
            if (warningObject) warningObject.SetActive(true);
            if (bonusText)
            {
                bonusText.text = "";
                bonusText.gameObject.SetActive(false);
                bonusText.transform.localScale = Vector3.one;
            }
            if (approachCircle) approachCircle.localScale = Vector3.one * 4f;
        }

        void Update()
        {
            if (!IsActive || gameManager == null) return;

            double currentTime = gameManager.GetCurrentMusicTimeMs();

            // 1. 检查结束
            if (currentTime > spinnerData.EndTime)
            {
                FinishSpinner();
                return;
            }

            // 2. 缩圈动画
            if (approachCircle)
            {
                double duration = spinnerData.EndTime - spinnerData.StartTime;
                double timeLeft = spinnerData.EndTime - currentTime;
                float timeProgress = (float)(timeLeft / duration);
                approachCircle.localScale = Vector3.one * Mathf.Clamp01(timeProgress) * 4f;
            }

            // 3. ✅ [已删除 ProcessInput] 改为被动接收 + 自动清理
            CleanUpInactiveHands();

            // 视觉平滑插值 (防止画面抖动)
            currentVisualRotation = Mathf.Lerp(currentVisualRotation, targetVisualRotation, Time.deltaTime * visualSmoothing);
            if (discRotating)
            {
                discRotating.localEulerAngles = new Vector3(0, 0, -currentVisualRotation);
            }

            // 4. 计算 RPM 平滑
            float instantaneousRPM = (rotationDeltaSinceLastFrame / Time.deltaTime) / 6f;
            CurrentRPM = Mathf.Lerp(CurrentRPM, instantaneousRPM, Time.deltaTime * 5f);
            rotationDeltaSinceLastFrame = 0f;

            // 5. 更新盘面旋转视觉
            if (discRotating)
            {
                discRotating.localEulerAngles = new Vector3(0, 0, -currentVisualRotation);
            }

            // 6. 更新 Meter
            if (meterImage && angleRequirement > 0)
            {
                float progress = totalRotationAngle / angleRequirement;
                meterImage.fillAmount = Mathf.Clamp01(progress);
                if (progress >= 1f) meterImage.color = Color.cyan;
                else meterImage.color = Color.white;
            }

            // 7. Bonus 检测
            if (totalRotationAngle > bonusThreshold)
            {
                AddBonus();
            }

            // 8. 动画
            if (bonusText && bonusText.gameObject.activeSelf)
            {
                bonusText.transform.localScale = Vector3.Lerp(bonusText.transform.localScale, Vector3.one, Time.deltaTime * 5f);
            }
        }

        // ✅ 核心旋转逻辑 (由 RayController 调用)
        public void UpdateRotation(Vector3 hitPoint, RayController source)
        {
            // 1. 本地坐标转换
            // 把世界坐标的击打点，转换为相对于转盘中心的本地向量
            Vector3 localPoint = transform.InverseTransformPoint(hitPoint);

            // ✅ [健壮性] 中心死区检测
            // 如果点击点离中心太近，Atan2 计算会不稳定，直接忽略
            if (new Vector2(localPoint.x, localPoint.y).magnitude < centerDeadZone)
            {
                return;
            }

            // 2. 更新指环位置 (视觉反馈)
            if (trackerRing)
            {
                if (!trackerRing.gameObject.activeSelf) trackerRing.gameObject.SetActive(true);
                trackerRing.position = hitPoint;
                // 稍微浮起来一点，防止穿模
                trackerRing.position -= transform.forward * 0.01f;
                trackerRing.rotation = transform.rotation;
            }

            // 3. 计算当前角度 (Atan2 返回 -180 到 180)
            float currentAngle = Mathf.Atan2(localPoint.y, localPoint.x) * Mathf.Rad2Deg;
            float currentTime = Time.time;

            // 4. 计算增量
            if (handStates.ContainsKey(source))
            {
                var (lastAngle, lastTime) = handStates[source];

                // ✅ [健壮性] 时间过滤：防止同一帧被多次调用
                if (currentTime > lastTime)
                {
                    float delta = currentAngle - lastAngle;

                    // 处理跨越 ±180 度的突变
                    // 比如从 179 度变成 -179 度，数学差值是 -358，实际只是转了 2 度
                    if (delta > 180f) delta -= 360f;
                    if (delta < -180f) delta += 360f;

                    // ✅ [健壮性] 物理限制过滤
                    // 人手不可能一帧转超过 120 度，如果发生，通常是追踪丢失或计算错误
                    if (Mathf.Abs(delta) < 120f && Mathf.Abs(delta) > 0.01f)
                    {
                        float validRotation = Mathf.Abs(delta) * rotationMultiplier;

                        // 累加数据
                        totalRotationAngle += validRotation;
                        targetVisualRotation += validRotation; // 目标值增加，Update里会插值追赶
                        rotationDeltaAccumulator += validRotation; // 喂给 RPM

                        // 隐藏警告
                        if (warningObject && warningObject.activeSelf) warningObject.SetActive(false);
                    }
                }
            }

            // 5. 更新状态
            handStates[source] = (currentAngle, currentTime);
        }

        // ✅ 自动清理离开的手
        private void CleanUpInactiveHands()
        {
            float currentTime = Time.time;
            List<RayController> toRemove = null;

            foreach (var kvp in handStates)
            {
                // 如果超过 0.1 秒没有更新，说明这只手移开了
                if (currentTime - kvp.Value.time > 0.1f)
                {
                    if (toRemove == null) toRemove = new List<RayController>();
                    toRemove.Add(kvp.Key);
                }
            }

            if (toRemove != null)
            {
                foreach (var hand in toRemove) handStates.Remove(hand);
            }

            // 如果没有手在操作，隐藏指环
            if (handStates.Count == 0 && trackerRing)
            {
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
        }

        private void FinishSpinner()
        {
            IsActive = false;
            Progress = totalRotationAngle / angleRequirement;

            if (Progress >= 1.0f) gameManager.OnNoteHit(spinnerData, 0);
            else if (Progress > 0.9f) gameManager.OnNoteHit(spinnerData, 1);
            else if (Progress > 0.75f) gameManager.OnNoteHit(spinnerData, 2);
            else gameManager.OnNoteMiss(spinnerData);

            if (myPool != null) myPool.Release(gameObject);
            else Destroy(gameObject);
        }
    }
}