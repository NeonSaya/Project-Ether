using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using TMPro;

namespace OsuVR
{
    /// <summary>
    /// 射线控制器：VR下所有UI交互和音符判定
    /// 每只手只响应自己手柄的扳机，双手互不干扰
    ///
    /// 性能关键：
    /// 1. 缓存 FindObjectsOfType 结果，不每帧遍历整个场景
    /// 2. 缓存 Camera.main，不每帧查找
    /// 3. 预分配集合，避免每帧 GC
    /// </summary>
    public class RayController : MonoBehaviour
    {
        public enum ControlMode { WristGain, Direct1to1 }

        [Header("模式选择")]
        public ControlMode currentMode = ControlMode.WristGain;

        [Header("手柄设置")]
        public bool isRightHand = true;

        [Header("摇杆滚动")]
        public InputActionProperty rightStickAction;
        public float scrollSpeed = 500f;

        [Header("懒人模式参数")]
        public float gainFactor = 1.3f;
        public float maxInputAngle = 30f;
        public float maxOutputAngle = 60f;

        [Header("健身模式参数")]
        public Vector3 directOffset = new Vector3(15f, 0, 0);

        [Header("人体工学设置")]
        public float verticalOffset = 30f;

        [Header("检测增强")]
        public float rayRadius = 0.20f;

        [Header("通用配置")]
        public float rayLength = 100f;
        public LayerMask noteLayer;
        public Transform visualRay;

        private InputAction triggerAction;

        // --- 3D 物理检测 ---
        private GameObject lastHitObject;
        public Vector3 CurrentHitPoint { get; private set; }
        public bool IsHitting { get; private set; }

        // --- UI 检测 ---
        public bool IsHittingUI { get; private set; }
        private GameObject currentUIHoverObject;
        private GameObject previousUIHoverObject;

        // --- UI 交互状态 (ExecuteEvents 方式) ---
        private PointerEventData pointerData;
        private PointerEventData hoverPointerData; // 预分配，避免每帧 GC
        private GameObject pointerPressTarget;
        private bool isPointerDown;
        private Canvas pointerPressCanvas;
        private Camera pointerPressCamera;

        // --- Dropdown 模态拦截 ---
        private TMP_Dropdown activeDropdown;
        private GameObject dropdownListClone;

        // --- 缓存（避免每帧 FindObjectsOfType） ---
        private HashSet<GameObject> previousHitObjects = new HashSet<GameObject>();
        private Dictionary<GameObject, Vector3> currentHitMap = new Dictionary<GameObject, Vector3>();
        private HashSet<GameObject> currentHitObjects = new HashSet<GameObject>();
        private List<RaycastResult> raycastResults = new List<RaycastResult>();

        private EventSystem eventSystem;
        private Camera cachedMainCam;

        private List<Canvas> cachedCanvases = new List<Canvas>();
        private List<TMP_Dropdown> cachedDropdowns = new List<TMP_Dropdown>();
        private List<ScrollRect> cachedScrollRects = new List<ScrollRect>();
        private float cacheRefreshTimer;
        private const float CACHE_REFRESH_INTERVAL = 0.2f;

        void OnEnable()
        {
            if (triggerAction != null) triggerAction.Enable();
            EnableAction(rightStickAction);
            // 场景加载后立即刷新缓存，消除 1-2s UI 无响应延迟
            if (eventSystem == null) eventSystem = EventSystem.current;
            cachedMainCam = Camera.main;
            RefreshHeavyCaches();
        }

        void OnDisable()
        {
            if (triggerAction != null) triggerAction.Disable();
            DisableAction(rightStickAction);
            isPointerDown = false;
            pointerPressTarget = null;
            pointerData = null;
            pointerPressCanvas = null;
            pointerPressCamera = null;
        }

        void Start()
        {
            eventSystem = EventSystem.current;
            if (eventSystem == null)
                Debug.LogError("[RayController] 场景中没有 EventSystem！");

            var inputModule = eventSystem?.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            if (inputModule != null)
            {
                inputModule.enabled = false;
                Debug.Log("[RayController] 已禁用 InputSystemUIInputModule");
            }

            string handPath = isRightHand ? "<XRController>{RightHand}/triggerButton" : "<XRController>{LeftHand}/triggerButton";
            triggerAction = new InputAction("Trigger_" + (isRightHand ? "R" : "L"), binding: handPath);
            triggerAction.AddBinding(isRightHand ? "<XRController>{RightHand}/triggerPressed" : "<XRController>{LeftHand}/triggerPressed");
            triggerAction.Enable();

            Debug.Log($"[RayController] {handPath} 扳机已绑定, action valid={triggerAction.enabled}");

            // 监听场景加载事件，确保新场景的 UI 立即可检测
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;

            RefreshAllCaches();
        }

        void OnDestroy()
        {
            if (triggerAction != null) { triggerAction.Disable(); triggerAction.Dispose(); }
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        /// <summary>
        /// 场景加载完成时立即刷新所有缓存，确保新场景的 UI 在第一帧就可交互
        /// </summary>
        private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            RefreshAllCaches();
            Debug.Log($"[RayController] 场景 {scene.name} 加载完成，缓存已刷新");
        }

        void Update()
        {
            if (currentMode == ControlMode.WristGain) ApplyWristGainMapping();
            else ApplyDirectMapping();

            // Canvas 缓存刷新（高频，确保新出现的 UI 立即可检测）
            cacheRefreshTimer += Time.deltaTime;
            if (cacheRefreshTimer >= CACHE_REFRESH_INTERVAL)
            { cacheRefreshTimer = 0f; RefreshHeavyCaches(); }

            if (cachedMainCam == null)
            {
                cachedMainCam = Camera.main;
                // 如果 MainCamera 仍未就绪，尝试直接查找
                if (cachedMainCam == null)
                {
                    var camGo = GameObject.FindWithTag("MainCamera");
                    if (camGo != null) cachedMainCam = camGo.GetComponent<Camera>();
                }
            }

            PerformRaycastAll();
            UpdateDropdownState();
            PerformUIRaycast();
            HandleUIHover();
            UpdatePointerData();
            HandleUIClickAndDrag();
            HandleScrollInput();
        }

        // ============================================================
        //  缓存管理
        // ============================================================

        private void RefreshAllCaches()
        {
            RefreshHeavyCaches();
            cachedMainCam = Camera.main;
        }

        private void RefreshHeavyCaches()
        {
            cachedCanvases.Clear();
            cachedCanvases.AddRange(FindObjectsOfType<Canvas>());
            cachedDropdowns.Clear();
            cachedDropdowns.AddRange(FindObjectsOfType<TMP_Dropdown>());
            cachedScrollRects.Clear();
            cachedScrollRects.AddRange(FindObjectsOfType<ScrollRect>());
        }

        // ============================================================
        //  输入
        // ============================================================

        private static void EnableAction(InputActionProperty action)
        { if (action.action != null && action.action.bindings.Count > 0) action.action.Enable(); }

        private static void DisableAction(InputActionProperty action)
        { if (action.action != null && action.action.bindings.Count > 0) action.action.Disable(); }

        private bool WasClickedThisFrame()
        {
            if (triggerAction != null && triggerAction.WasPressedThisFrame()) return true;
            if (!isRightHand) return false;
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) return true;
            if (Input.GetMouseButtonDown(0)) return true;
            return false;
        }

        private bool WasReleasedThisFrame()
        {
            if (triggerAction != null && triggerAction.WasReleasedThisFrame()) return true;
            if (!isRightHand) return false;
            if (Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame) return true;
            if (Input.GetMouseButtonUp(0)) return true;
            return false;
        }

        private bool IsTriggerHeld()
        {
            if (triggerAction != null && triggerAction.ReadValue<float>() > 0.5f) return true;
            if (!isRightHand) return false;
            if (Mouse.current != null && Mouse.current.leftButton.isPressed) return true;
            if (Input.GetMouseButton(0)) return true;
            return false;
        }

        // ============================================================
        //  模式映射
        // ============================================================

        private void ApplyWristGainMapping()
        {
            Vector3 currentEuler = transform.localRotation.eulerAngles;
            float inputX = NormalizeAngle(currentEuler.x);
            float inputY = NormalizeAngle(currentEuler.y);
            if (visualRay != null)
            {
                visualRay.localRotation = Quaternion.Euler(CalculateNonLinear(inputX) + verticalOffset, CalculateNonLinear(inputY), 0);
                visualRay.localPosition = new Vector3(0, directOffset.y, directOffset.z);
            }
        }

        private void ApplyDirectMapping()
        {
            if (visualRay != null)
            {
                visualRay.localRotation = Quaternion.Euler(directOffset.x + verticalOffset, 0, 0);
                visualRay.localPosition = new Vector3(0, directOffset.y, directOffset.z);
            }
        }

        private float CalculateNonLinear(float a) { return Mathf.Sign(a) * Mathf.Pow(Mathf.Clamp01(Mathf.Abs(a) / maxInputAngle), gainFactor) * maxOutputAngle; }
        private float NormalizeAngle(float a) { return a > 180f ? a - 360f : a; }

        // ============================================================
        //  3D 物理射线 (音符判定) — 预分配集合避免 GC
        // ============================================================

        private void PerformRaycastAll()
        {
            if (visualRay == null) return;
            Vector3 origin = visualRay.position, direction = visualRay.forward;
            RaycastHit[] hits = Physics.SphereCastAll(new Ray(origin, direction), rayRadius, rayLength, noteLayer, QueryTriggerInteraction.Collide);

            currentHitMap.Clear();
            currentHitObjects.Clear();
            float minDist = float.MaxValue; Vector3 closestPt = Vector3.zero; GameObject closestObj = null;

            foreach (var hit in hits)
            {
                GameObject obj = hit.collider.gameObject;
                currentHitObjects.Add(obj);
                if (!currentHitMap.ContainsKey(obj)) currentHitMap.Add(obj, hit.point);
                var spinner = obj.GetComponentInParent<SpinnerController>();
                if (spinner != null) spinner.UpdateRotation(hit.point, this);
                if (hit.distance < minDist) { minDist = hit.distance; closestPt = hit.point; closestObj = obj; }
            }

            IsHitting = closestObj != null;
            CurrentHitPoint = IsHitting ? closestPt : Vector3.zero;
            lastHitObject = closestObj;

            foreach (var old in previousHitObjects) { if (old != null && !currentHitObjects.Contains(old)) NotifyHoverState(old, false, Vector3.zero); }
            foreach (var kvp in currentHitMap) NotifyHoverState(kvp.Key, true, kvp.Value);

            // Swap 而不是 new，避免 GC
            previousHitObjects.Clear();
            foreach (var key in currentHitMap.Keys) previousHitObjects.Add(key);
        }

        // ============================================================
        //  Dropdown 模态拦截 — 使用缓存，不每帧 FindObjectsOfType
        // ============================================================

        private void UpdateDropdownState()
        {
            TMP_Dropdown prevDropdown = activeDropdown;
            activeDropdown = null;
            dropdownListClone = null;

            bool needsRefresh = false;

            for (int i = 0; i < cachedCanvases.Count; i++)
            {
                var canvas = cachedCanvases[i];
                // Unity fake null check：已销毁对象 != null 但比较运算符返回 true
                if (canvas == null || canvas.Equals(null))
                {
                    needsRefresh = true;
                    continue;
                }
                if (!canvas.gameObject.activeInHierarchy) continue;

                for (int j = 0; j < canvas.transform.childCount; j++)
                {
                    Transform child = canvas.transform.GetChild(j);
                    if (child.gameObject.activeInHierarchy && child.name == "Dropdown List")
                    {
                        dropdownListClone = child.gameObject;

                        for (int k = 0; k < cachedDropdowns.Count; k++)
                        {
                            var dd = cachedDropdowns[k];
                            if (dd == null || dd.Equals(null)) { needsRefresh = true; continue; }
                            if (dd.IsActive() && dd.IsInteractable())
                            {
                                Canvas ddCanvas = dd.GetComponentInParent<Canvas>();
                                if (ddCanvas == canvas)
                                {
                                    activeDropdown = dd;
                                    break;
                                }
                            }
                        }
                        break;
                    }
                }
                if (dropdownListClone != null) break;
            }

            if (needsRefresh) RefreshAllCaches();

            if (activeDropdown != null && prevDropdown == null)
            {
                RefreshAllCaches(); // Dropdown 打开需要刷新缓存（新增 Canvas/Raycaster）
                DisableBlocker();
            }

            if (prevDropdown != null && activeDropdown == null)
            {
                RefreshAllCaches();
            }
        }

        private void DisableBlocker()
        {
            for (int i = 0; i < cachedCanvases.Count; i++)
            {
                var canvas = cachedCanvases[i];
                if (canvas == null || canvas.Equals(null)) continue;
                if (!canvas.gameObject.activeInHierarchy) continue;

                for (int j = 0; j < canvas.transform.childCount; j++)
                {
                    Transform child = canvas.transform.GetChild(j);
                    if (child.gameObject.activeInHierarchy && child.name == "Blocker")
                    {
                        child.gameObject.SetActive(false);
                        Debug.Log("[RayController] 已禁用 Dropdown Blocker");
                        return;
                    }
                }
            }
        }

        private bool IsInDropdownList(GameObject uiObj)
        {
            if (dropdownListClone == null) return false;
            Transform t = uiObj.transform;
            while (t != null)
            {
                if (t == dropdownListClone.transform) return true;
                t = t.parent;
            }
            return false;
        }

        private bool IsDropdownBody(GameObject uiObj)
        {
            if (activeDropdown == null) return false;
            var dd = uiObj.GetComponentInParent<TMP_Dropdown>();
            return dd == activeDropdown && !IsInDropdownList(uiObj);
        }

        // ============================================================
        //  UI 射线检测
        // ============================================================

        private void PerformUIRaycast()
        {
            if (visualRay == null) return;

            // 如果缓存为空，立即刷新（确保新场景/新 UI 立即可检测）
            if (cachedCanvases.Count == 0) RefreshHeavyCaches();

            IsHittingUI = false;
            previousUIHoverObject = currentUIHoverObject;
            currentUIHoverObject = null;

            Vector3 origin = visualRay.position, direction = visualRay.forward;
            float closestDistance = float.MaxValue;
            Vector3 closestUIHitPoint = Vector3.zero;
            GameObject closestUIObject = null;

            // 直接遍历所有 Canvas，从 Canvas 获取 GraphicRaycaster
            // 不依赖 GraphicRaycaster 缓存，确保新出现的 UI 立即可检测
            foreach (var canvas in cachedCanvases)
            {
                if (canvas == null || canvas.Equals(null)) continue;
                if (canvas.renderMode != RenderMode.WorldSpace) continue;
                if (!canvas.gameObject.activeInHierarchy) continue;

                GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
                if (raycaster == null) continue;

                Camera eventCam = canvas.worldCamera != null ? canvas.worldCamera : cachedMainCam;
                if (eventCam == null) continue;

                Plane canvasPlane = new Plane(canvas.transform.forward, canvas.transform.position);
                float enter;
                if (!canvasPlane.Raycast(new Ray(origin, direction), out enter)) continue;

                Vector3 hitPoint = origin + direction * enter;

                if (pointerData == null) pointerData = new PointerEventData(eventSystem);
                pointerData.position = eventCam.WorldToScreenPoint(hitPoint);

                raycastResults.Clear();
                raycaster.Raycast(pointerData, raycastResults);

                if (raycastResults.Count > 0)
                {
                    if (activeDropdown != null && dropdownListClone != null)
                    {
                        foreach (var result in raycastResults)
                        {
                            GameObject obj = result.gameObject;
                            if (obj.name == "Blocker") continue;

                            if (IsInDropdownList(obj))
                            {
                                if (enter < closestDistance)
                                {
                                    closestDistance = enter;
                                    closestUIObject = obj;
                                    closestUIHitPoint = hitPoint;
                                    IsHittingUI = true;
                                }
                                break;
                            }
                            if (IsDropdownBody(obj))
                            {
                                if (enter < closestDistance)
                                {
                                    closestDistance = enter;
                                    closestUIObject = obj;
                                    closestUIHitPoint = hitPoint;
                                    IsHittingUI = true;
                                }
                                break;
                            }
                        }
                    }
                    else
                    {
                        foreach (var result in raycastResults)
                        {
                            if (result.gameObject.name == "Blocker") continue;
                            if (enter < closestDistance)
                            {
                                closestDistance = enter;
                                closestUIObject = result.gameObject;
                                closestUIHitPoint = hitPoint;
                                IsHittingUI = true;
                            }
                            break;
                        }
                    }
                }
            }

            if (IsHittingUI)
            {
                currentUIHoverObject = closestUIObject;
                CurrentHitPoint = closestUIHitPoint;
            }
        }

        // ============================================================
        //  UI Hover — 预分配 PointerEventData 避免 GC
        // ============================================================

        private void HandleUIHover()
        {
            if (hoverPointerData == null) hoverPointerData = new PointerEventData(eventSystem);

            if (currentUIHoverObject != null && currentUIHoverObject != previousUIHoverObject)
            {
                if (previousUIHoverObject != null)
                    ExecuteEvents.ExecuteHierarchy<IPointerExitHandler>(previousUIHoverObject, hoverPointerData, ExecuteEvents.pointerExitHandler);
                ExecuteEvents.ExecuteHierarchy<IPointerEnterHandler>(currentUIHoverObject, hoverPointerData, ExecuteEvents.pointerEnterHandler);
            }
            if (currentUIHoverObject == null && previousUIHoverObject != null)
                ExecuteEvents.ExecuteHierarchy<IPointerExitHandler>(previousUIHoverObject, hoverPointerData, ExecuteEvents.pointerExitHandler);
        }

        // ============================================================
        //  更新 PointerData
        // ============================================================

        private void UpdatePointerData()
        {
            if (pointerData == null)
                pointerData = new PointerEventData(eventSystem);

            if (isPointerDown && pointerPressCanvas != null && pointerPressCamera != null && visualRay != null)
            {
                Plane canvasPlane = new Plane(pointerPressCanvas.transform.forward, pointerPressCanvas.transform.position);
                float enter;
                if (canvasPlane.Raycast(new Ray(visualRay.position, visualRay.forward), out enter))
                {
                    Vector3 hitPoint = visualRay.position + visualRay.forward * enter;
                    pointerData.position = pointerPressCamera.WorldToScreenPoint(hitPoint);
                }
                return;
            }

            if (currentUIHoverObject != null)
            {
                Canvas canvas = currentUIHoverObject.GetComponentInParent<Canvas>();
                if (canvas != null && canvas.renderMode == RenderMode.WorldSpace)
                {
                    Camera eventCam = canvas.worldCamera != null ? canvas.worldCamera : cachedMainCam;
                    if (eventCam != null)
                    {
                        pointerData.position = eventCam.WorldToScreenPoint(CurrentHitPoint);
                    }
                }
            }
        }

        // ============================================================
        //  UI 点击与拖拽
        // ============================================================

        private void HandleUIClickAndDrag()
        {
            if (activeDropdown != null && WasClickedThisFrame() && !isPointerDown)
            {
                if (currentUIHoverObject == null)
                {
                    Debug.Log("[RayController] Dropdown 空白处点击 → 关闭");
                    activeDropdown.Hide();
                    activeDropdown = null;
                    dropdownListClone = null;
                    return;
                }

                if (IsDropdownBody(currentUIHoverObject))
                {
                    var clickedDropdown = currentUIHoverObject.GetComponentInParent<TMP_Dropdown>();
                    if (clickedDropdown == activeDropdown)
                    {
                        Debug.Log("[RayController] 同一个 Dropdown 本体点击 → 关闭");
                        activeDropdown.Hide();
                        activeDropdown = null;
                        dropdownListClone = null;
                        return;
                    }
                    else
                    {
                        Debug.Log("[RayController] 不同 Dropdown 本体点击 → 关闭旧的，让新的打开");
                        activeDropdown.Hide();
                        activeDropdown = null;
                        dropdownListClone = null;
                    }
                }

                if (!IsInDropdownList(currentUIHoverObject))
                {
                    var anotherDropdown = currentUIHoverObject.GetComponentInParent<TMP_Dropdown>();
                    TMP_Dropdown oldDropdown = activeDropdown;
                    activeDropdown.Hide();
                    activeDropdown = null;
                    dropdownListClone = null;

                    if (anotherDropdown != null && anotherDropdown != oldDropdown)
                    {
                        Debug.Log("[RayController] 不同 Dropdown 点击 → 关闭旧的，让新的打开");
                    }
                    else
                    {
                        Debug.Log("[RayController] Dropdown 打开时点击其他 UI → 关闭并屏蔽");
                        return;
                    }
                }

                if (IsInDropdownList(currentUIHoverObject))
                {
                    var toggle = currentUIHoverObject.GetComponentInParent<Toggle>();
                    if (toggle != null)
                    {
                        Debug.Log($"[RayController] Dropdown 选项点击: {currentUIHoverObject.name}, Toggle: {toggle.gameObject.name}");
                        toggle.isOn = true;
                    }
                    else
                    {
                        Debug.Log("[RayController] Dropdown 列表空白处点击 → 关闭");
                        activeDropdown.Hide();
                        activeDropdown = null;
                        dropdownListClone = null;
                    }
                    return;
                }
            }

            if (WasClickedThisFrame() && currentUIHoverObject != null && !isPointerDown)
            {
                isPointerDown = true;

                Canvas pressCanvas = currentUIHoverObject.GetComponentInParent<Canvas>();
                GraphicRaycaster pressRaycaster = pressCanvas != null ? pressCanvas.GetComponent<GraphicRaycaster>() : null;
                Camera pressCam = pressCanvas != null && pressCanvas.worldCamera != null
                    ? pressCanvas.worldCamera : cachedMainCam;

                RaycastResult pressRaycast = new RaycastResult();
                pressRaycast.module = pressRaycaster;
                pressRaycast.gameObject = currentUIHoverObject;
                pressRaycast.screenPosition = pointerData.position;
                pointerData.pointerPressRaycast = pressRaycast;
                pointerData.pointerCurrentRaycast = pressRaycast;

                pointerData.pressPosition = pointerData.position;

                var pressObj = ExecuteEvents.ExecuteHierarchy<IPointerDownHandler>(
                    currentUIHoverObject, pointerData, ExecuteEvents.pointerDownHandler);
                pointerPressTarget = pressObj ?? currentUIHoverObject;
                pointerData.pointerPress = pointerPressTarget;

                var clickHandler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(currentUIHoverObject);
                var dragObj = ExecuteEvents.GetEventHandler<IDragHandler>(currentUIHoverObject);

                if (dragObj != null && (clickHandler == null || dragObj.gameObject == clickHandler.gameObject))
                {
                    pointerData.pointerDrag = dragObj;
                    pointerData.useDragThreshold = (dragObj.GetComponentInParent<Slider>() != null) ? false : true;
                }
                else
                {
                    pointerData.pointerDrag = null;
                    pointerData.useDragThreshold = true;
                }

                pointerData.dragging = false;
                pointerPressCanvas = pressCanvas;
                pointerPressCamera = pressCam;

                Debug.Log($"[RayController] PointerDown: press={pointerPressTarget.name}, click={clickHandler?.name ?? "null"}, drag={dragObj?.name ?? "null"}, pointerDrag={pointerData.pointerDrag?.name ?? "null"}");
            }

            if (isPointerDown && pointerData.pointerDrag != null && IsTriggerHeld())
            {
                UpdatePointerData();

                if (!pointerData.dragging)
                {
                    if (!pointerData.useDragThreshold ||
                        Vector2.Distance(pointerData.position, pointerData.pressPosition) > EventSystem.current.pixelDragThreshold)
                    {
                        pointerData.dragging = true;
                        ExecuteEvents.Execute<IBeginDragHandler>(
                            pointerData.pointerDrag, pointerData, ExecuteEvents.beginDragHandler);
                    }
                }

                if (pointerData.dragging)
                {
                    ExecuteEvents.Execute<IDragHandler>(
                        pointerData.pointerDrag, pointerData, ExecuteEvents.dragHandler);
                }
            }

            if (isPointerDown && WasReleasedThisFrame())
            {
                if (pointerPressTarget != null)
                {
                    ExecuteEvents.Execute<IPointerUpHandler>(
                        pointerPressTarget, pointerData, ExecuteEvents.pointerUpHandler);

                    if (!pointerData.dragging)
                    {
                        if (currentUIHoverObject != null)
                        {
                            var clickHandlerOnCurrent = ExecuteEvents.GetEventHandler<IPointerClickHandler>(currentUIHoverObject);
                            var clickHandlerOnPress = ExecuteEvents.GetEventHandler<IPointerClickHandler>(pointerPressTarget);
                            if (clickHandlerOnCurrent != null && clickHandlerOnCurrent == clickHandlerOnPress)
                            {
                                ExecuteEvents.Execute<IPointerClickHandler>(
                                    pointerPressTarget, pointerData, ExecuteEvents.pointerClickHandler);
                            }
                        }
                        else if (pointerPressTarget != null)
                        {
                            ExecuteEvents.Execute<IPointerClickHandler>(
                                pointerPressTarget, pointerData, ExecuteEvents.pointerClickHandler);
                        }
                    }

                    if (pointerData.dragging && pointerData.pointerDrag != null)
                    {
                        ExecuteEvents.Execute<IEndDragHandler>(
                            pointerData.pointerDrag, pointerData, ExecuteEvents.endDragHandler);
                    }

                    Debug.Log($"[RayController] PointerUp: {pointerPressTarget.name}, dragged={pointerData.dragging}");
                }

                isPointerDown = false;
                pointerPressTarget = null;
                pointerData.pointerPress = null;
                pointerData.pointerDrag = null;
                pointerData.dragging = false;
                pointerPressCanvas = null;
                pointerPressCamera = null;
            }
        }

        // ============================================================
        //  3D 物体 hover 通知
        // ============================================================

        private void NotifyHoverState(GameObject obj, bool state, Vector3 hitPoint)
        {
            var note = obj.GetComponentInParent<NoteController>();
            if (note != null) { if (state) note.OnRayHover(isRightHand); else note.OnRayExit(); }

            var sliderCtrl = obj.GetComponentInParent<SliderController>();
            if (sliderCtrl != null) { if (state) sliderCtrl.OnRayStay(isRightHand, hitPoint); else sliderCtrl.OnRayExit(isRightHand); }

            var spinner = obj.GetComponentInParent<SpinnerController>();
            if (spinner != null) spinner.isHovered = true;
        }

        // ============================================================
        //  滚动输入 — 使用缓存而非 FindObjectsOfType
        // ============================================================

        private void HandleScrollInput()
        {
            Vector2 stickValue = Vector2.zero;
            if (rightStickAction.action != null && rightStickAction.action.bindings.Count > 0)
                stickValue = rightStickAction.action.ReadValue<Vector2>();

            if (isRightHand && Mouse.current != null)
            { Vector2 ms = Mouse.current.scroll.ReadValue(); if (Mathf.Abs(ms.y) > 0.1f) stickValue.y = ms.y > 0 ? 1f : -1f; }

            if (Mathf.Abs(stickValue.y) > 0.3f)
            {
                if (activeDropdown != null && dropdownListClone != null)
                {
                    var dropdownScroll = dropdownListClone.GetComponentInChildren<ScrollRect>();
                    if (dropdownScroll != null && dropdownScroll.gameObject.activeInHierarchy && dropdownScroll.vertical)
                    {
                        dropdownScroll.verticalNormalizedPosition += stickValue.y * scrollSpeed * Time.deltaTime / dropdownScroll.content.rect.height;
                        dropdownScroll.verticalNormalizedPosition = Mathf.Clamp01(dropdownScroll.verticalNormalizedPosition);
                    }
                    return;
                }

                foreach (var sv in cachedScrollRects)
                { if (sv != null && !sv.Equals(null) && sv.gameObject.activeInHierarchy && sv.vertical) { sv.verticalNormalizedPosition += stickValue.y * scrollSpeed * Time.deltaTime / sv.content.rect.height; sv.verticalNormalizedPosition = Mathf.Clamp01(sv.verticalNormalizedPosition); } }
            }
        }

        public void SetMode(bool isLazyMode) { currentMode = isLazyMode ? ControlMode.WristGain : ControlMode.Direct1to1; }

        /// <summary>
        /// 通知所有 RayController 刷新缓存（新 UI Canvas 出现时调用）
        /// VRPauseMenu.Show / VRSettingsMenu.Show 应调用此方法
        /// </summary>
        public static void NotifyUICanvasChanged()
        {
            foreach (var rc in FindObjectsOfType<RayController>())
            {
                rc.RefreshAllCaches();
            }
        }
    }
}