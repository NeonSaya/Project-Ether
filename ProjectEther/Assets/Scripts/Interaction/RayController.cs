using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

namespace OsuVR
{
    /// <summary>
    /// 射线控制器：支持 "手腕增益" (懒人模式) 和 "线性直连" (健身模式) 切换
    /// 已集成 OpenXR 通用输入
    /// 支持 3D 物理射线和 UI 射线检测
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
        public InputActionProperty clickAction;
        
        [Header("摇杆滚动")]
        [Tooltip("右手摇杆动作 (用于滚动列表)")]
        public InputActionProperty rightStickAction;
        public float scrollSpeed = 500f;

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

        [Header("UI 检测")]
        [Tooltip("场景中的 GraphicRaycaster 列表 (自动查找)")]
        public List<GraphicRaycaster> uiRaycasters = new List<GraphicRaycaster>();
        
        [Header("UI 备用检测 (无 GraphicRaycaster 时)")]
        [Tooltip("UI 按钮所在的 Layer")]
        public LayerMask uiLayer;

        [Header("通用配置")]
        public float rayLength = 100f;
        public LayerMask noteLayer;
        public Transform visualRay;

        private GameObject lastHitObject;
        private GameObject lastHitUIObject;
        public Vector3 CurrentHitPoint { get; private set; }
        public bool IsHitting { get; private set; }
        public bool IsHittingUI { get; private set; }

        private HashSet<GameObject> previousHitObjects = new HashSet<GameObject>();
        private EventSystem eventSystem;

        void OnEnable()
        {
            if (clickAction.action != null && clickAction.action.bindings.Count > 0) clickAction.action.Enable();
            if (rightStickAction.action != null && rightStickAction.action.bindings.Count > 0) rightStickAction.action.Enable();
        }

        void OnDisable()
        {
            if (clickAction.action != null && clickAction.action.bindings.Count > 0) clickAction.action.Disable();
            if (rightStickAction.action != null && rightStickAction.action.bindings.Count > 0) rightStickAction.action.Disable();
        }

        void Start()
        {
            eventSystem = EventSystem.current;
            
            if (eventSystem == null)
            {
                Debug.LogError("[RayController] 场景中没有 EventSystem！");
            }
            
            if (uiRaycasters.Count == 0)
            {
                uiRaycasters.AddRange(FindObjectsOfType<GraphicRaycaster>());
            }
            
            if (uiLayer.value == 0)
            {
                uiLayer = ~0;
            }
            
            Debug.Log($"[RayController] 找到 {uiRaycasters.Count} 个 GraphicRaycaster, UI Layer: {uiLayer.value}");
            foreach (var r in uiRaycasters)
            {
                if (r != null)
                {
                    Canvas c = r.GetComponent<Canvas>();
                    Debug.Log($"[RayController] - {r.name}, renderMode: {(c != null ? c.renderMode.ToString() : "null")}");
                }
            }
        }

        void Update()
        {
            if (currentMode == ControlMode.WristGain) ApplyWristGainMapping();
            else ApplyDirectMapping();

            PerformRaycastAll();
            PerformUIRaycast();

            // --- 终极点击修复 ---
            bool isClicked = false;

            // 1. VR扳机
            if (clickAction.action != null && clickAction.action.bindings.Count > 0 && clickAction.action.WasPressedThisFrame()) isClicked = true;

            // 2. 新版 Input System 鼠标左键
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) isClicked = true;

            // 3. 传统旧版输入兜底 (防止某些 Unity 版本的 Input System 冲突)
            if (Input.GetMouseButtonDown(0)) isClicked = true;

            if (isClicked)
            {
                if (IsHittingUI && lastHitUIObject != null)
                {
                    Debug.Log($"[RayController] 发送标准 UI 点击: {lastHitUIObject.name}");
                    
                    Camera cam = Camera.main;
                    Canvas parentCanvas = lastHitUIObject.GetComponentInParent<Canvas>();
                    if (parentCanvas != null && parentCanvas.worldCamera != null) cam = parentCanvas.worldCamera;
                    
                    var pointerData = new PointerEventData(eventSystem);
                    if (cam != null) pointerData.position = cam.WorldToScreenPoint(CurrentHitPoint);

                    ExecuteEvents.ExecuteHierarchy(lastHitUIObject, pointerData, ExecuteEvents.pointerDownHandler);
                    ExecuteEvents.ExecuteHierarchy(lastHitUIObject, pointerData, ExecuteEvents.pointerClickHandler);
                    ExecuteEvents.ExecuteHierarchy(lastHitUIObject, pointerData, ExecuteEvents.pointerUpHandler);
                    ExecuteEvents.ExecuteHierarchy(lastHitUIObject, pointerData, ExecuteEvents.submitHandler);
                }
                else if (IsHitting && lastHitObject != null)
                {
                    Button btn = lastHitObject.GetComponent<Button>();
                    if (btn == null) btn = lastHitObject.GetComponentInParent<Button>();

                    if (btn != null)
                    {
                        var pointerData = new PointerEventData(eventSystem);
                        ExecuteEvents.ExecuteHierarchy(btn.gameObject, pointerData, ExecuteEvents.pointerDownHandler);
                        ExecuteEvents.ExecuteHierarchy(btn.gameObject, pointerData, ExecuteEvents.pointerClickHandler);
                        ExecuteEvents.ExecuteHierarchy(btn.gameObject, pointerData, ExecuteEvents.pointerUpHandler);
                        Debug.Log($"[RayController] 成功点击 3D 按钮: {btn.gameObject.name}");
                    }
                }
            }

            HandleScrollInput();
        }

        void HandleScrollInput()
        {
            if (rightStickAction.action == null || rightStickAction.action.bindings.Count == 0) return;
            
            Vector2 stickValue = rightStickAction.action.ReadValue<Vector2>();
            
            if (Mathf.Abs(stickValue.y) > 0.3f)
            {
                ScrollRect[] scrollViews = FindObjectsOfType<ScrollRect>();
                foreach (var sv in scrollViews)
                {
                    if (sv.gameObject.activeInHierarchy && sv.vertical)
                    {
                        sv.verticalNormalizedPosition += stickValue.y * scrollSpeed * Time.deltaTime / sv.content.rect.height;
                        sv.verticalNormalizedPosition = Mathf.Clamp01(sv.verticalNormalizedPosition);
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

        private void PerformUIRaycast()
        {
            if (visualRay == null) return;

            IsHittingUI = false;
            lastHitUIObject = null;

            Vector3 origin = visualRay.position;
            Vector3 direction = visualRay.forward;
            float closestDistance = float.MaxValue;

            // 1. 终极物理检测：无视 Layer 限制，且强制检测 Trigger 触发器
            Ray ray = new Ray(origin, direction);
            RaycastHit[] hits = Physics.RaycastAll(ray, rayLength, Physics.AllLayers, QueryTriggerInteraction.Collide);

            foreach (var hit in hits)
            {
                Button btn = hit.collider.GetComponentInParent<Button>();
                if (btn == null) btn = hit.collider.GetComponentInChildren<Button>();

                if (btn != null)
                {
                    if (hit.distance < closestDistance)
                    {
                        closestDistance = hit.distance;
                        IsHittingUI = true;
                        lastHitUIObject = btn.gameObject;
                        CurrentHitPoint = hit.point;
                    }
                }
            }

            // 2. 如果物理没检测到，用 EventSystem (GraphicRaycaster) 兜底
            if (!IsHittingUI && uiRaycasters.Count > 0 && eventSystem != null)
            {
                foreach (var raycaster in uiRaycasters)
                {
                    if (raycaster == null) continue;

                    Canvas canvas = raycaster.GetComponent<Canvas>();
                    if (canvas == null || canvas.renderMode != RenderMode.WorldSpace) continue;

                    Plane canvasPlane = new Plane(canvas.transform.forward, canvas.transform.position);
                    float enter;

                    if (canvasPlane.Raycast(ray, out enter))
                    {
                        Vector3 hitPoint = ray.GetPoint(enter);

                        Camera eventCam = canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
                        PointerEventData ped = new PointerEventData(eventSystem);

                        if (eventCam != null)
                        {
                            ped.position = eventCam.WorldToScreenPoint(hitPoint);
                        }
                        else
                        {
                            ped.position = new Vector2(Screen.width / 2, Screen.height / 2);
                        }

                        List<RaycastResult> results = new List<RaycastResult>();
                        raycaster.Raycast(ped, results);

                        if (results.Count > 0)
                        {
                            float dist = Vector3.Distance(origin, hitPoint);
                            if (dist < closestDistance)
                            {
                                closestDistance = dist;
                                IsHittingUI = true;
                                lastHitUIObject = results[0].gameObject;
                                CurrentHitPoint = hitPoint;
                            }
                        }
                    }
                }
            }
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
                    else slider.OnRayExit(this.isRightHand); // ✅ 修复：必须告诉滑条，是哪只手离开了！
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