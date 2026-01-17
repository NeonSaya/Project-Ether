using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI; // 必须引用 UI 命名空间
using TMPro;          // 引用 TextMeshPro (如果你用的是 TMP)

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

        [Tooltip("进度条/计量表 (需设置 Image Type 为 Filled)")]
        public Image meterImage;

        [Tooltip("奖励分数文本 (Bonus Text)")]
        public TextMeshProUGUI bonusText; // 如果没用 TMP，改成 Text

        [Tooltip("跟随射线的指环 (Tracker Ring)")]
        public Transform trackerRing;
        
        [Header("判定参数")]
        [Tooltip("旋转灵敏度倍率")]
        public float rotationMultiplier = 1.5f;

        // --- 状态变量 ---
        public bool IsActive { get; private set; } = true;
        public float CurrentRPM { get; private set; } = 0f;
        public float Progress { get; private set; } = 0f;

        private RhythmGameManager gameManager;
        private float totalRotationAngle = 0f;
        private float angleRequirement = 0f;
        private float currentVisualRotation = 0f;
        private Dictionary<LaserShooter.HandSide, float> lastHandAngles = new Dictionary<LaserShooter.HandSide, float>();
        private float rotationDeltaSinceLastFrame = 0f;

        // Bonus 相关
        private int bonusCount = 0;
        private float bonusRotationThreshold = 0f; // 下一次触发 Bonus 需要的角度

        public void Initialize(SpinnerObject data, RhythmGameManager manager)
        {
            spinnerData = data;
            gameManager = manager;
            IsActive = true;

            // 1. 难度计算 (假设 1ms 需要转 0.5 度左右，根据 OD 调整)
            // 这里为了演示，设定每秒需要转 360 度 (1圈)
            float durationSeconds = (float)(spinnerData.EndTime - spinnerData.StartTime) / 1000f;
            angleRequirement = 360f * 1.5f * durationSeconds; // 稍微简单点

            // 2. 初始化视觉状态
            if (discRotating) discRotating.localRotation = Quaternion.identity;
            if (approachCircle) approachCircle.gameObject.SetActive(true);
            
            // Warning 显示
            if (warningObject) warningObject.SetActive(true);

            // Meter 归零
            if (meterImage)
            {
                meterImage.type = Image.Type.Filled; // 确保是填充模式
                meterImage.fillAmount = 0f;          // 进度归零
            }

            // Bonus 隐藏
            if (bonusText)
            {
                bonusText.text = "";
                bonusText.gameObject.SetActive(false);
            }
            
            // Tracker 隐藏 (直到射线射中)
            if (trackerRing) trackerRing.gameObject.SetActive(false);

            lastHandAngles.Clear();
            bonusCount = 0;
            bonusRotationThreshold = angleRequirement + 180f; // 满条后，再转半圈开始给 Bonus
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

            // 2. 缩圈动画 (从大变小)
            if (approachCircle)
            {
                double duration = spinnerData.EndTime - spinnerData.StartTime;
                double timeLeft = spinnerData.EndTime - currentTime;
                float timeProgress = (float)(timeLeft / duration);
                approachCircle.localScale = Vector3.one * timeProgress * 4f; 
            }

            // 3. 计算 RPM 平滑
            float instantaneousRPM = (rotationDeltaSinceLastFrame / Time.deltaTime) / 6f; 
            CurrentRPM = Mathf.Lerp(CurrentRPM, instantaneousRPM, Time.deltaTime * 5f);
            rotationDeltaSinceLastFrame = 0f;
            
            // 4. 更新盘面旋转视觉
            if (discRotating)
            {
                discRotating.localEulerAngles = new Vector3(0, 0, -currentVisualRotation);
            }

            // 5. 更新 Meter (进度条)
            if (meterImage)
            {
                // totalRotationAngle 是当前转的角度，angleRequirement 是通关要求
                float progress = totalRotationAngle / angleRequirement;
                // 限制在 0~1 之间，让图片逐级填满
                meterImage.fillAmount = Mathf.Clamp01(progress);
            }

            // 6. Bonus 检测
            if (totalRotationAngle > angleRequirement)
            {
                // 如果转的圈数超过了要求，且达到了下一个阈值
                if (totalRotationAngle > bonusRotationThreshold)
                {
                    AddBonus();
                }
            }

            // 7. Tracker 自动隐藏逻辑 (如果这一帧没人摸)
            // 简单处理：如果没有调用 OnRayStay，tracker 应该隐藏。
            // 由于 OnRayStay 是被动调用，我们在 LateUpdate 里处理或者简单用计时器
            // 这里为了简化，假设一直显示，或者你可以加个变量判断 dirty
        }

        /// <summary>
        /// 核心交互：每帧调用
        /// </summary>
        public void OnRayStay(Vector3 hitPoint, LaserShooter.HandSide hand)
        {
            if (!IsActive) return;

            // --- 1. Tracker Ring 逻辑 (跟随射线) ---
            if (trackerRing)
            {
                trackerRing.gameObject.SetActive(true);
                // 把击中点转为本地坐标，让光环跟着跑
                Vector3 localPos = transform.InverseTransformPoint(hitPoint);
                // 稍微往 Z 轴负方向提一点，防止穿模
                trackerRing.localPosition = new Vector3(localPos.x, localPos.y, -0.02f);
            }

            // 只要开始有效转动了，就隐藏警告
            if (warningObject && warningObject.activeSelf)
            {
                // 可以加个判断：转速 > 0.1 才隐藏，防止误触
                warningObject.SetActive(false);
            }

            // --- 2. 旋转判定逻辑 ---
            Vector3 localHitPos = transform.InverseTransformPoint(hitPoint);
            float currentAngle = Mathf.Atan2(localHitPos.y, localHitPos.x) * Mathf.Rad2Deg;

            if (lastHandAngles.ContainsKey(hand))
            {
                float prevAngle = lastHandAngles[hand];
                float delta = Mathf.DeltaAngle(prevAngle, currentAngle);
                
                // 只要动了就算 (Lazy Spin)
                float validRotation = Mathf.Abs(delta) * rotationMultiplier;

                // 只有当旋转有效时，才算“开始旋转”
                if (validRotation > 0.1f)
                {
                    // 隐藏 Warning
                    if (warningObject && warningObject.activeSelf) 
                        warningObject.SetActive(false);
                }

                totalRotationAngle += validRotation;
                currentVisualRotation += validRotation;
                rotationDeltaSinceLastFrame += validRotation;
            }

            lastHandAngles[hand] = currentAngle;
        }

        private void AddBonus()
        {
            bonusCount++;
            bonusRotationThreshold += 180f; // 每多转 180 度(半圈)给一次奖励

            // 播放奖励音效 (需连接 AudioManager)
            // gameManager.PlaySound("SpinnerBonus");

            // 显示 Bonus UI
            if (bonusText)
            {
                bonusText.gameObject.SetActive(true);
                bonusText.text = (bonusCount * 1000).ToString(); // 1000, 2000, 3000...
                
                // 简单的跳动动画
                bonusText.transform.localScale = Vector3.one * 1.5f;
                // 你可以在 Update 里写个简单的 Lerp 回 1.0
            }
            
            // 加分
            // gameManager.AddScore(1000);
        }

        private void FinishSpinner()
        {
            IsActive = false;
            Progress = totalRotationAngle / angleRequirement;

            if (Progress >= 1.0f)
            {
                Debug.Log($"<color=cyan>Spinner Clear!</color> Bonus: {bonusCount}");
                gameManager.OnNoteHit(spinnerData, 0);
            }
            else
            {
                gameManager.OnNoteMiss(spinnerData);
            }

            Destroy(gameObject);
        }
    }
}