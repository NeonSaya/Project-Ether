using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
namespace OsuVR
{
    /// <summary>
    /// 射线控制器：支持 "手腕增益" (懒人模式) 和 "线性直连" (健身模式) 切换
    /// 已集成 OpenXR 通用输入
    /// </summary>
    public class RayController : MonoBehaviour
    {
        public enum ControlMode
        {
            WristGain, // 懒人模式：非线性增益，动动手腕覆盖全屏
            Direct1to1 // 健身模式：1:1 物理映射，指哪打哪
        }

        [Header("模式选择")]
        [Tooltip("当前控制模式")]
        public ControlMode currentMode = ControlMode.WristGain;

        [Header("手柄设置")]
        public bool isRightHand = true;

        [Header("OpenXR 输入设置")]
        [Tooltip("点击动作 (请绑定 <XRController>/triggerPressed)")]
        // ✅ [新增] 通用输入动作，支持所有 VR 手柄
        public InputActionProperty clickAction;

        [Header("懒人模式参数 (Wrist Gain)")]
        [Tooltip("增益系数 (Gamma)：建议 1.3 ~ 1.5")]
        public float gainFactor = 1.3f;

        [Tooltip("最大输入角度 (度)")]
        public float maxInputAngle = 30f;

        [Tooltip("最大输出角度 (度)")]
        public float maxOutputAngle = 60f;

        [Header("健身模式参数 (Fitness)")]
        [Tooltip("人体工学偏移：修正手柄握持角度 (通常向下倾斜 15-30 度更舒服)")]
        public Vector3 directOffset = new Vector3(15f, 0, 0);

        [Header("人体工学设置")]
        [Tooltip("向下倾斜的角度 (度)，解决手柄自然握持指天的问题")]
        public float verticalOffset = 30f;

        [Header("检测增强")]
        [Tooltip("射线半径 (米)：把射线变粗，容错率更高 (建议 0.1 ~ 0.15)")]
        public float rayRadius = 0.13f;

        [Header("通用配置")]
        public float rayLength = 100f;
        public LayerMask noteLayer;
        public Transform visualRay; // 视觉射线对象

        // 内部状态
        private GameObject lastHitObject;
        public Vector3 CurrentHitPoint { get; private set; }
        public bool IsHitting { get; private set; }

        private HashSet<GameObject> previousHitObjects = new HashSet<GameObject>();

        // ✅ [新增] 必须启用/禁用 Action
        void OnEnable()
        {
            if (clickAction.action != null) clickAction.action.Enable();
        }

        void OnDisable()
        {
            if (clickAction.action != null) clickAction.action.Disable();
        }

        void Update()
        {
            // 1. 根据模式计算射线的局部旋转
            if (currentMode == ControlMode.WristGain)
            {
                ApplyWristGainMapping();
            }
            else
            {
                ApplyDirectMapping();
            }

            // 2. 执行射线检测
            PerformRaycastAll();

            // 3. ✅ [修改] 使用 OpenXR Action 检测点击
            // WasPressedThisFrame 只在按下瞬间触发一次
            if (clickAction.action != null && clickAction.action.WasPressedThisFrame())
            {
                if (IsHitting && lastHitObject != null)
                {
                    // 只处理 UI 按钮
                    Button btn = lastHitObject.GetComponentInParent<Button>();
                    if (btn == null) btn = lastHitObject.GetComponentInChildren<Button>();

                    if (btn != null)
                    {
                        btn.onClick.Invoke();
                    }
                }
            }
        }

        /// <summary>
        /// 模式 A: 手腕增益映射 (核心算法)
        /// </summary>
        private void ApplyWristGainMapping()
        {
            Vector3 currentEuler = transform.localRotation.eulerAngles;
            float inputX = NormalizeAngle(currentEuler.x);
            float inputY = NormalizeAngle(currentEuler.y);

            float outputX = CalculateNonLinear(inputX);
            float outputY = CalculateNonLinear(inputY);

            if (visualRay != null)
            {
                visualRay.localRotation = Quaternion.Euler(outputX + verticalOffset, outputY, 0);
            }
        }

        /// <summary>
        /// 模式 B: 线性直连映射 (健身模式)
        /// </summary>
        private void ApplyDirectMapping()
        {
            if (visualRay != null)
            {
                Vector3 finalOffset = directOffset;
                finalOffset.x += verticalOffset;
                visualRay.localRotation = Quaternion.Euler(finalOffset);
            }
        }

        // --- 算法与工具函数 ---
        private float CalculateNonLinear(float inputAngle)
        {
            float sign = Mathf.Sign(inputAngle);
            float absAngle = Mathf.Abs(inputAngle);
            float normalizedInput = Mathf.Clamp01(absAngle / maxInputAngle);
            float curvedInput = Mathf.Pow(normalizedInput, gainFactor);
            return sign * curvedInput * maxOutputAngle;
        }

        private float NormalizeAngle(float angle)
        {
            if (angle > 180f) angle -= 360f;
            return angle;
        }

        // --- 射线检测逻辑 ---
        private void PerformRaycastAll()
        {
            if (visualRay == null) return;

            Vector3 origin = visualRay.position;
            Vector3 direction = visualRay.forward;
            Ray ray = new Ray(origin, direction);

            // 调试线
            Color debugColor = (currentMode == ControlMode.WristGain) ? Color.cyan : Color.red;
            Debug.DrawRay(origin, direction * 100, Color.red);

            RaycastHit[] hits = Physics.SphereCastAll(ray, rayRadius, rayLength, noteLayer, QueryTriggerInteraction.Collide);

            Dictionary<GameObject, Vector3> currentHitMap = new Dictionary<GameObject, Vector3>();

            HashSet<GameObject> currentHitObjects = new HashSet<GameObject>();

            // 用于计算最近点击点
            float minDistance = float.MaxValue;
            Vector3 closestPoint = Vector3.zero;
            bool hasValidHit = false;
            GameObject closestObj = null; // ✅ 临时变量

            foreach (var hit in hits)
            {
                GameObject obj = hit.collider.gameObject;
                currentHitObjects.Add(obj);

                if (!currentHitMap.ContainsKey(obj))
                {
                    currentHitMap.Add(obj, hit.point);
                }

                // ✅ [修改] 传递 'this' 给 Spinner，支持多点触控
                var spinner = obj.GetComponentInParent<SpinnerController>();
                if (spinner != null)
                {
                    spinner.UpdateRotation(hit.point, this);
                }

                if (hit.distance < minDistance)
                {
                    minDistance = hit.distance;
                    closestPoint = hit.point;
                    hasValidHit = true;
                    closestObj = obj; // 记录最近的
                }
            }

            IsHitting = hasValidHit;
            CurrentHitPoint = hasValidHit ? closestPoint : Vector3.zero;
            lastHitObject = closestObj; // ✅ 更新成员变量供点击使用

            // 4. 处理离开 (Exit)
            foreach (var oldObj in previousHitObjects)
            {
                if (oldObj != null && !currentHitObjects.Contains(oldObj))
                {
                    NotifyHoverState(oldObj, false, Vector3.zero); // 离开时位置传 zero 即可
                }
            }

            // 5. 处理进入/保持 (Enter/Stay)
            foreach (var kvp in currentHitMap)
            {
                    NotifyHoverState(kvp.Key, true, kvp.Value); // kvp.Value 就是 hit.point
            }

            previousHitObjects = new HashSet<GameObject>(currentHitMap.Keys);
            
        }

        private void NotifyHoverState(GameObject obj, bool state, Vector3 hitPoint)
        {
           
                // 1. 通知 Note (传入 isRightHand)
                var note = obj.GetComponentInParent<NoteController>();
                if (note != null)
                {
                    if (state) note.OnRayHover(this.isRightHand);
                    else note.OnRayExit(); // ✅ 新增：需要去 NoteController 里加这个方法
                }

                // 2. 通知 Slider
                var slider = obj.GetComponentInParent<SliderController>();
                if (slider != null)
                {
                    if (state) slider.OnRayStay(this.isRightHand, hitPoint);
                    else slider.OnRayExit();
                }

                // 3. 通知 Spinner (传入 this，你原代码里好像已经处理了 UpdateRotation)
                var spinner = obj.GetComponentInParent<SpinnerController>();
                if (spinner != null)
                {
                    spinner.isHovered = true; // Spinner 暂时保持原样，或者你也给它加个 OnRayHover
                }
            }
       

        public void SetMode(bool isLazyMode)
        {
            currentMode = isLazyMode ? ControlMode.WristGain : ControlMode.Direct1to1;
            Debug.Log($"切换模式: {currentMode}");
        }
    }
}