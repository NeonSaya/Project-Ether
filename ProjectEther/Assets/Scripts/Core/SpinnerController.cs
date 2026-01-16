using UnityEngine;
using System.Collections;

namespace OsuVR
{
    /// <summary>
    /// 转盘控制器：控制转盘的旋转动画和判定逻辑
    /// </summary>
    public class SpinnerController : MonoBehaviour
    {
        [Header("转盘配置")]
        public SpinnerObject spinnerObject;

        [Header("转盘组件")]
        [Tooltip("转盘圆环渲染器")]
        public MeshRenderer spinnerRing;

        [Tooltip("转盘背景")]
        public MeshRenderer spinnerBackground;

        [Tooltip("旋转指示器")]
        public Transform rotationIndicator;

        [Header("转盘设置")]
        [Tooltip("需要旋转的总圈数")]
        public float requiredRotationCount = 3.0f;

        [Tooltip("旋转速度阈值（度/秒），低于此速度不计入有效旋转")]
        public float rotationSpeedThreshold = 90.0f;

        [Tooltip("最大转盘尺寸")]
        public float maxSpinnerSize = 2.0f;

        [Tooltip("最小转盘尺寸")]
        public float minSpinnerSize = 0.5f;

        [Header("状态")]
        public bool isActive = true;
        public bool isCompleted = false;
        public float currentRotation = 0.0f;
        public float totalRotation = 0.0f;
        public float currentRotationSpeed = 0.0f;


        [Header("视觉组件")]
        public Transform approachCircleObject; // 拖拽赋值


        // 私有变量
        private RhythmGameManager gameManager;
        private double currentMusicTimeMs = 0;
        private double timeToStart = 0;
        private double timeToEnd = 0;
        private MaterialPropertyBlock propertyBlock;
        private Color originalRingColor;
        private Color originalBackgroundColor;
        private Vector3 lastHandDirection = Vector3.zero;
        private bool isHandInSpinner = false;
        private Vector3 lastHandPosition;



        /// <summary>
        /// 初始化转盘 (修复了朝向问题和材质实例化导致的内存泄漏)
        /// </summary>
        public void Initialize(SpinnerObject spinnerObj, RhythmGameManager manager)
        {
            spinnerObject = spinnerObj;
            gameManager = manager;
            isActive = true;
            isCompleted = false;
            currentRotation = 0.0f;
            totalRotation = 0.0f;
            currentRotationSpeed = 0.0f;

            // 设置转盘位置
            Vector3 worldPosition = CoordinateMapper.MapToWorld(spinnerObj.Position);

            // 修复 1: 稍微往 Z 轴负方向移一点点 (Z-Offset)，防止和背景墙重叠导致闪烁
            transform.position = worldPosition - new Vector3(0, 0, 0.05f);

            // 修复 2: 保持平直，不要使用 LookAt(Vector3.zero)，否则转盘会歪向世界中心
            transform.localRotation = Quaternion.identity;

            // 初始化MaterialPropertyBlock
            if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();

            // 修复 3: 使用 sharedMaterial 获取原始颜色，避免实例化材质造成内存泄漏
            if (spinnerRing != null)
            {
                originalRingColor = spinnerRing.sharedMaterial.color;
            }

            if (spinnerBackground != null)
            {
                originalBackgroundColor = spinnerBackground.sharedMaterial.color;
            }

            Debug.Log($"转盘初始化: 开始时间={spinnerObj.StartTime}ms, 结束时间={spinnerObj.EndTime}ms, 持续时间={spinnerObj.Duration}ms");

            // [新增] 启动缩圈动画
            if (approachCircleObject != null)
            {
                var scaler = approachCircleObject.GetComponent<ApproachCircleScaler>();
                if (scaler == null) scaler = approachCircleObject.gameObject.AddComponent<ApproachCircleScaler>();

                // 转盘的缩圈通常指向 StartTime (开始旋转的时间)
                scaler.Initialize(spinnerObj.StartTime, manager.spawnOffsetMs);


                approachCircleObject.gameObject.SetActive(true);
            }
        }

        void Update()
        {
            if (!isActive) return;

            // 获取当前音乐时间
            if (gameManager != null)
            {
                currentMusicTimeMs = gameManager.GetCurrentMusicTimeMs();
            }

            // 计算时间
            timeToStart = spinnerObject.StartTime - currentMusicTimeMs;
            timeToEnd = spinnerObject.EndTime - currentMusicTimeMs;

            // 更新转盘状态
            UpdateSpinnerState();

            // 更新视觉效果
            UpdateVisuals();

            // 检查完成状态
            CheckCompletion();
        }

        /// <summary>
        /// 更新转盘状态
        /// </summary>
        private void UpdateSpinnerState()
        {
            // 如果还没到开始时间，不激活
            if (currentMusicTimeMs < spinnerObject.StartTime - spinnerObject.TimePreempt)
            {
                return;
            }

            // 如果已经过了结束时间，自动完成或失败
            if (currentMusicTimeMs > spinnerObject.EndTime)
            {
                if (!isCompleted)
                {
                    OnSpinnerFailed();
                }
                return;
            }

            // 处理手柄交互
            HandleInteraction();
        }


        /// <summary>
        /// 处理交互 (添加了编辑器宏，避免干扰VR输入)
        /// </summary>
        private void HandleInteraction()
        {
            // 仅在 Unity 编辑器中启用鼠标模拟，防止在 VR 真机上产生奇怪的输入冲突
#if UNITY_EDITOR
            if (Input.GetMouseButton(0))
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                RaycastHit hit;

                if (Physics.Raycast(ray, out hit))
                {
                    if (hit.collider.gameObject == gameObject)
                    {
                        Vector3 mouseDelta = Input.mousePosition - lastHandPosition;
                        float rotationAmount = mouseDelta.x * 0.5f; // 简化旋转计算

                        if (Mathf.Abs(rotationAmount) > rotationSpeedThreshold * Time.deltaTime)
                        {
                            AddRotation(rotationAmount);
                        }
                    }
                }
            }

            lastHandPosition = Input.mousePosition;
#endif
        }

        /// <summary>
        /// 添加旋转
        /// </summary>
        public void AddRotation(float degrees)
        {
            if (!isActive || isCompleted) return;

            // 计算旋转速度
            currentRotationSpeed = degrees / Time.deltaTime;

            // 添加旋转
            currentRotation += degrees;
            totalRotation += Mathf.Abs(degrees);

            // 旋转转盘
            if (rotationIndicator != null)
            {
                rotationIndicator.Rotate(Vector3.forward, degrees);
            }

            // 旋转整个转盘（可选）
            spinnerRing.transform.Rotate(Vector3.forward, degrees * 0.1f);
        }

        /// <summary>
        /// 更新视觉效果 (修复了直接修改 material.color 导致的性能问题)
        /// </summary>
        private void UpdateVisuals()
        {
            // 计算进度
            float progress = 0.0f;

            if (currentMusicTimeMs >= spinnerObject.StartTime && currentMusicTimeMs <= spinnerObject.EndTime)
            {
                // 转盘进行中
                progress = (float)((currentMusicTimeMs - spinnerObject.StartTime) / spinnerObject.Duration);

                // 计算旋转进度
                float rotationProgress = totalRotation / (requiredRotationCount * 360.0f);
                progress = Mathf.Max(progress, rotationProgress * 0.7f); // 旋转进度占70%权重
            }
            else if (currentMusicTimeMs < spinnerObject.StartTime)
            {
                // 转盘还未开始，显示进场效果
                progress = 1.0f - (float)(timeToStart / spinnerObject.TimePreempt);
                progress = Mathf.Clamp01(progress);
            }
            else
            {
                // 转盘已结束
                progress = 1.0f;
            }

            // 更新转盘大小（进场/退场动画）
            float size = Mathf.Lerp(minSpinnerSize, maxSpinnerSize, progress);
            transform.localScale = Vector3.one * size;

            // 修复 4: 使用 MaterialPropertyBlock 更新颜色，严禁在 Update 中直接访问 .material
            if (propertyBlock != null)
            {
                // 更新圆环颜色
                if (spinnerRing != null)
                {
                    spinnerRing.GetPropertyBlock(propertyBlock);

                    Color ringColor = originalRingColor;
                    if (currentRotationSpeed > rotationSpeedThreshold)
                    {
                        // 快速旋转时高亮
                        ringColor = Color.Lerp(originalRingColor, Color.yellow,
                            Mathf.Clamp01(currentRotationSpeed / (rotationSpeedThreshold * 3)));
                    }
                    ringColor.a = Mathf.Lerp(0.3f, 1.0f, progress);

                    propertyBlock.SetColor("_Color", ringColor);
                    // propertyBlock.SetFloat("_Progress", progress); // 如果你的Shader没有这个属性，请注释掉

                    spinnerRing.SetPropertyBlock(propertyBlock);
                }

                // 更新背景颜色
                if (spinnerBackground != null)
                {
                    spinnerBackground.GetPropertyBlock(propertyBlock);

                    Color bgColor = originalBackgroundColor;
                    bgColor.a = Mathf.Lerp(0.1f, 0.5f, progress);

                    propertyBlock.SetColor("_Color", bgColor);
                    spinnerBackground.SetPropertyBlock(propertyBlock);
                }
            }
        }

        /// <summary>
        /// 检查完成状态
        /// </summary>
        private void CheckCompletion()
        {
            if (isCompleted) return;

            // 检查是否达到要求的旋转圈数
            float completedRotations = totalRotation / 360.0f;

            if (completedRotations >= requiredRotationCount)
            {
                OnSpinnerCompleted();
            }
        }

        /// <summary>
        /// 转盘完成
        /// </summary>
        private void OnSpinnerCompleted()
        {
            isCompleted = true;
            isActive = false;

            Debug.Log($"🎯 转盘完成! 总旋转: {totalRotation:F0}度, 圈数: {totalRotation / 360.0f:F1}");

            // 修复 5: 通知游戏管理器，否则无法触发判定和加分
            if (gameManager != null)
            {
                gameManager.OnSpinnerCompleted(spinnerObject);
            }

            // 播放完成效果
            StartCoroutine(CompletionEffect());
        }

        /// <summary>
        /// 转盘失败
        /// </summary>
        private void OnSpinnerFailed()
        {
            isActive = false;

            Debug.Log($"💥 转盘失败! 完成度: {totalRotation / (requiredRotationCount * 360.0f):P0}");

            // 播放失败效果
            StartCoroutine(FailureEffect());
        }

        /// <summary>
        /// 完成效果协程
        /// </summary>
        private IEnumerator CompletionEffect()
        {
            float duration = 0.5f;
            float timer = 0f;
            Vector3 originalScale = transform.localScale;
            Color originalColor = spinnerRing.material.color;

            while (timer < duration)
            {
                timer += Time.deltaTime;
                float t = timer / duration;

                // 放大并变透明
                transform.localScale = Vector3.Lerp(originalScale, originalScale * 1.5f, t);

                if (spinnerRing != null)
                {
                    Color color = Color.Lerp(originalColor, Color.clear, t);
                    spinnerRing.material.color = color;
                }

                yield return null;
            }

            Destroy(gameObject);
        }

        /// <summary>
        /// 失败效果协程
        /// </summary>
        private IEnumerator FailureEffect()
        {
            float duration = 0.5f;
            float timer = 0f;
            Vector3 originalScale = transform.localScale;

            while (timer < duration)
            {
                timer += Time.deltaTime;
                float t = timer / duration;

                // 缩小并变红
                transform.localScale = Vector3.Lerp(originalScale, Vector3.zero, t);

                if (spinnerRing != null)
                {
                    Color color = Color.Lerp(originalRingColor, Color.red, t);
                    color.a = Mathf.Lerp(1.0f, 0.0f, t);
                    spinnerRing.material.color = color;
                }

                yield return null;
            }

            Destroy(gameObject);
        }

        /// <summary>
        /// 当手柄进入转盘区域
        /// </summary>
        public void OnHandEnter(Vector3 handPosition)
        {
            isHandInSpinner = true;
            lastHandPosition = handPosition;
        }

        /// <summary>
        /// 当手柄在转盘区域内移动
        /// </summary>
        public void OnHandMove(Vector3 handPosition)
        {
            if (!isHandInSpinner) return;

            // 计算旋转角度
            Vector3 direction = handPosition - transform.position;
            direction.y = 0; // 只在水平面旋转

            if (lastHandDirection != Vector3.zero)
            {
                float angle = Vector3.SignedAngle(lastHandDirection, direction, Vector3.up);
                AddRotation(angle);
            }

            lastHandDirection = direction.normalized;
        }

        /// <summary>
        /// 当手柄离开转盘区域
        /// </summary>
        public void OnHandExit()
        {
            isHandInSpinner = false;
            lastHandDirection = Vector3.zero;
            currentRotationSpeed = 0.0f;
        }

        /// <summary>
        /// 获取转盘完成百分比
        /// </summary>
        public float GetCompletionPercentage()
        {
            return Mathf.Clamp01(totalRotation / (requiredRotationCount * 360.0f));
        }

        void OnDestroy()
        {
            // 清理资源
            if (spinnerRing != null)
            {
                Destroy(spinnerRing.material);
            }

            if (spinnerBackground != null)
            {
                Destroy(spinnerBackground.material);
            }
        }
    }
}