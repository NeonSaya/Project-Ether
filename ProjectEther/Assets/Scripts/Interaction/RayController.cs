using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

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
        public enum ControlMode
        {
            WristGain,
            Direct1to1,
        }

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
        private GameObject pointerClickTarget;
        private bool isPointerDown;
        private Canvas pointerPressCanvas;
        private Camera pointerPressCamera;

        // --- Dropdown 模态拦截 ---
        private TMP_Dropdown activeDropdown;
        private GameObject dropdownListClone;

        // --- 缓存（避免每帧 FindObjectsOfType） ---
        private HashSet<GameObject> previousHitObjects = new HashSet<GameObject>();
        private Dictionary<GameObject, Vector3> currentHitMap =
            new Dictionary<GameObject, Vector3>();
        private HashSet<GameObject> currentHitObjects = new HashSet<GameObject>();
        private List<RaycastResult> raycastResults = new List<RaycastResult>();

        private EventSystem eventSystem;
        private Camera cachedMainCam;
        private RhythmGameManager cachedGameManager;

        private List<ScrollRect> cachedScrollRects = new List<ScrollRect>();
        private float cacheRefreshTimer;
        private const float CACHE_REFRESH_INTERVAL = 0.2f;
        private int postLoadRefreshFrames; // 场景加载后密集刷新剩余帧数

        void OnEnable()
        {
            if (triggerAction != null)
                triggerAction.Enable();
            EnableAction(rightStickAction);
            // 场景加载后立即刷新缓存，消除 1-2s UI 无响应延迟
            if (eventSystem == null)
                eventSystem = EventSystem.current;
            cachedMainCam = FindAnyCamera();
            RefreshHeavyCaches();
        }

        void OnDisable()
        {
            if (triggerAction != null)
                triggerAction.Disable();
            DisableAction(rightStickAction);
            ReleasePointer(false);
            previousUIHoverObject = currentUIHoverObject;
            currentUIHoverObject = null;
            HandleUIHover();
            previousUIHoverObject = null;
            IsHittingUI = false;
            pointerData = null;
            hoverPointerData = null;
        }

        void Start()
        {
            eventSystem = EventSystem.current;
            if (eventSystem == null)
                Debug.LogError("[RayController] 场景中没有 EventSystem！");

            var inputModule =
                eventSystem?.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            if (inputModule != null)
            {
                inputModule.enabled = false;
                Debug.Log("[RayController] 已禁用 InputSystemUIInputModule");
            }

            string handPath = isRightHand
                ? "<XRController>{RightHand}/{TriggerButton}"
                : "<XRController>{LeftHand}/{TriggerButton}";
            triggerAction = new InputAction(
                "Trigger_" + (isRightHand ? "R" : "L"),
                InputActionType.Button,
                binding: handPath
            );
            triggerAction.AddBinding(
                isRightHand
                    ? "<XRController>{RightHand}/triggerButton"
                    : "<XRController>{LeftHand}/triggerButton"
            );
            triggerAction.AddBinding(
                isRightHand
                    ? "<XRController>{RightHand}/triggerPressed"
                    : "<XRController>{LeftHand}/triggerPressed"
            );
            triggerAction.Enable();

            Debug.Log(
                $"[RayController] {handPath} 扳机已绑定, action valid={triggerAction.enabled}"
            );

            // 从 SettingsManager 加载持久化的控制器偏移量
            LoadControllerOffset();

            // 监听场景加载事件，确保新场景的 UI 立即可检测
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;

            RefreshAllCaches();
        }

        void OnDestroy()
        {
            if (triggerAction != null)
            {
                triggerAction.Disable();
                triggerAction.Dispose();
            }
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        /// <summary>
        /// 从 SettingsManager 加载持久化的控制器偏移量，确保跨场景不丢失
        /// </summary>
        private void LoadControllerOffset()
        {
            if (SettingsManager.Instance != null)
            {
                var s = SettingsManager.Instance.Settings;
                float rot = s.controllerRotationOffset;
                float yOff = isRightHand ? s.rightControllerYOffset : s.leftControllerYOffset;
                float zOff = isRightHand ? s.rightControllerZOffset : s.leftControllerZOffset;
                directOffset = new Vector3(rot, yOff, zOff);
            }
            else
            {
                // SettingsManager 尚未初始化，从 PlayerPrefs 直接读取
                float rot = PlayerPrefs.GetFloat("Settings_CtrlRot", 0f);
                float yOff = isRightHand
                    ? PlayerPrefs.GetFloat("Settings_RightCtrlY", 0f)
                    : PlayerPrefs.GetFloat("Settings_LeftCtrlY", 0f);
                float zOff = isRightHand
                    ? PlayerPrefs.GetFloat("Settings_RightCtrlZ", 0f)
                    : PlayerPrefs.GetFloat("Settings_LeftCtrlZ", 0f);
                directOffset = new Vector3(rot, yOff, zOff);
            }
        }

        /// <summary>
        /// 场景加载完成时立即刷新所有缓存，确保新场景的 UI 在第一帧就可交互
        /// </summary>
        private void OnSceneLoaded(
            UnityEngine.SceneManagement.Scene scene,
            UnityEngine.SceneManagement.LoadSceneMode mode
        )
        {
            // 场景切换后 Camera.main 可能为 null (XR 追踪重建中)，立即积极查找
            cachedMainCam = FindAnyCamera();
            RefreshAllCaches();
            // 启动密集刷新: UI 在 Start() 中动态构建，OnSceneLoaded 时还不存在
            // 连续刷新 10 帧捕获动态构建的滚动区域
            postLoadRefreshFrames = 10;
            LoadControllerOffset();
            Debug.Log($"[RayController] 场景 {scene.name} 加载完成，缓存已刷新，偏移已重载");
        }

        /// <summary>
        /// 多策略查找场景中的相机，用于 Camera.main 为 null 时的兜底
        /// </summary>
        Camera FindAnyCamera()
        {
            var cam = Camera.main;
            if (cam != null)
                return cam;
            var camGo = GameObject.FindWithTag("MainCamera");
            if (camGo != null)
                return camGo.GetComponent<Camera>();
            return FindFirstObjectByType<Camera>();
        }

        void Update()
        {
            if (currentMode == ControlMode.WristGain)
                ApplyWristGainMapping();
            else
                ApplyDirectMapping();

            // 滚动区域缓存：场景加载后密集刷新，UI 射线直接使用活动注册表
            if (postLoadRefreshFrames > 0)
            {
                postLoadRefreshFrames--;
                RefreshHeavyCaches();
            }
            else
            {
                cacheRefreshTimer += Time.unscaledDeltaTime;
                if (cacheRefreshTimer >= CACHE_REFRESH_INTERVAL)
                {
                    cacheRefreshTimer = 0f;
                    RefreshHeavyCaches();
                }
            }

            if (cachedMainCam == null)
                cachedMainCam = FindAnyCamera();

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
            cachedGameManager = FindFirstObjectByType<RhythmGameManager>();
            cachedScrollRects.Clear();
            cachedScrollRects.AddRange(
                FindObjectsByType<ScrollRect>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            );
        }

        // ============================================================
        //  输入
        // ============================================================

        private static void EnableAction(InputActionProperty action)
        {
            if (action.action != null && action.action.bindings.Count > 0)
                action.action.Enable();
        }

        private static void DisableAction(InputActionProperty action)
        {
            if (action.action != null && action.action.bindings.Count > 0)
                action.action.Disable();
        }

        private bool WasClickedThisFrame()
        {
            if (triggerAction != null && triggerAction.WasPressedThisFrame())
                return true;
            if (!isRightHand)
                return false;
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                return true;
            if (Input.GetMouseButtonDown(0))
                return true;
            return false;
        }

        private bool WasReleasedThisFrame()
        {
            if (triggerAction != null && triggerAction.WasReleasedThisFrame())
                return true;
            if (!isRightHand)
                return false;
            if (Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame)
                return true;
            if (Input.GetMouseButtonUp(0))
                return true;
            return false;
        }

        private bool IsTriggerHeld()
        {
            if (triggerAction != null && triggerAction.IsPressed())
                return true;
            if (!isRightHand)
                return false;
            if (Mouse.current != null && Mouse.current.leftButton.isPressed)
                return true;
            if (Input.GetMouseButton(0))
                return true;
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
                visualRay.localRotation = Quaternion.Euler(
                    CalculateNonLinear(inputX) + verticalOffset,
                    CalculateNonLinear(inputY),
                    0
                );
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

        private float CalculateNonLinear(float a)
        {
            return Mathf.Sign(a)
                * Mathf.Pow(Mathf.Clamp01(Mathf.Abs(a) / maxInputAngle), gainFactor)
                * maxOutputAngle;
        }

        private float NormalizeAngle(float a)
        {
            return a > 180f ? a - 360f : a;
        }

        // ============================================================
        //  3D 物理射线 (音符判定) — 预分配集合避免 GC
        // ============================================================

        // SphereCastNonAlloc 复用缓冲区（一次性分配，每帧零 GC）
        private readonly RaycastHit[] _hitBuffer = new RaycastHit[256];

        private void PerformRaycastAll()
        {
            // 只屏蔽谱面交互，后续 UI 射线和暂停菜单输入继续执行。
            if (cachedGameManager != null && cachedGameManager.IsPaused)
            {
                foreach (var old in previousHitObjects)
                    if (old != null)
                        NotifyHoverState(old, false, Vector3.zero);
                previousHitObjects.Clear();
                currentHitObjects.Clear();
                currentHitMap.Clear();
                IsHitting = false;
                CurrentHitPoint = Vector3.zero;
                lastHitObject = null;
                return;
            }

            if (visualRay == null)
                return;
            Vector3 origin = visualRay.position,
                direction = visualRay.forward;
            int hitCount = Physics.SphereCastNonAlloc(
                new Ray(origin, direction),
                rayRadius,
                _hitBuffer,
                rayLength,
                noteLayer,
                QueryTriggerInteraction.Collide
            );

            currentHitMap.Clear();
            currentHitObjects.Clear();
            float minDist = float.MaxValue;
            Vector3 closestPt = Vector3.zero;
            GameObject closestObj = null;

            for (int _hi = 0; _hi < hitCount; _hi++)
            {
                var hit = _hitBuffer[_hi];
                GameObject obj = hit.collider.gameObject;
                currentHitObjects.Add(obj);
                if (!currentHitMap.ContainsKey(obj))
                    currentHitMap.Add(obj, hit.point);
                var spinner = obj.GetComponentInParent<SpinnerController>();
                if (spinner != null)
                    spinner.UpdateRotation(hit.point, this);
                if (hit.distance < minDist)
                {
                    minDist = hit.distance;
                    closestPt = hit.point;
                    closestObj = obj;
                }
            }

            IsHitting = closestObj != null;
            CurrentHitPoint = IsHitting ? closestPt : Vector3.zero;
            lastHitObject = closestObj;

            foreach (var old in previousHitObjects)
            {
                if (old != null && !currentHitObjects.Contains(old))
                    NotifyHoverState(old, false, Vector3.zero);
            }
            foreach (var kvp in currentHitMap)
                NotifyHoverState(kvp.Key, true, kvp.Value);

            // Swap 而不是 new，避免 GC
            previousHitObjects.Clear();
            foreach (var key in currentHitMap.Keys)
                previousHitObjects.Add(key);
        }

        // ============================================================
        //  Dropdown 模态拦截 — 使用活动注册表
        // ============================================================

        private void UpdateDropdownState()
        {
            activeDropdown = null;
            dropdownListClone = null;
            var raycasters = RaycasterManager.GetRaycasters();
            for (int i = 0; i < raycasters.Count; i++)
            {
                var raycaster = raycasters[i] as GraphicRaycaster;
                if (
                    raycaster == null
                    || !raycaster.isActiveAndEnabled
                    || raycaster.name != "Dropdown List"
                )
                    continue;

                // TMP 列表嵌在 Dropdown 下，自带已注册的 Canvas/Raycaster；两只手都能立即发现。
                var dropdown = raycaster.GetComponentInParent<TMP_Dropdown>();
                if (dropdown == null || !dropdown.IsActive() || !dropdown.IsExpanded)
                    continue;
                var group = raycaster.GetComponent<CanvasGroup>();
                if (group != null && !group.blocksRaycasts)
                    continue;

                activeDropdown = dropdown;
                dropdownListClone = raycaster.gameObject;
                break;
            }
        }

        private void CloseActiveDropdown()
        {
            // Hide 的淡出动画尚未结束时，列表也应立即停止拦截其他控件。
            if (dropdownListClone != null)
            {
                var group = dropdownListClone.GetComponent<CanvasGroup>();
                if (group != null)
                {
                    group.blocksRaycasts = false;
                    group.interactable = false;
                }
            }
            if (activeDropdown != null)
                activeDropdown.Hide();
            activeDropdown = null;
            dropdownListClone = null;
        }

        private bool IsInDropdownList(GameObject uiObj)
        {
            if (uiObj == null || dropdownListClone == null)
                return false;
            Transform t = uiObj.transform;
            while (t != null)
            {
                if (t == dropdownListClone.transform)
                    return true;
                t = t.parent;
            }
            return false;
        }

        private bool IsDropdownBody(GameObject uiObj)
        {
            if (activeDropdown == null)
                return false;
            var dd = uiObj.GetComponentInParent<TMP_Dropdown>();
            return dd == activeDropdown && !IsInDropdownList(uiObj);
        }

        // ============================================================
        //  UI 射线检测
        // ============================================================

        private void PerformUIRaycast()
        {
            IsHittingUI = false;
            previousUIHoverObject = currentUIHoverObject;
            currentUIHoverObject = null;
            if (pointerData == null)
                pointerData = new PointerEventData(eventSystem);
            pointerData.pointerCurrentRaycast = default;
            if (visualRay == null)
                return;

            var ray = new Ray(visualRay.position, visualRay.forward);
            float closestDistance = float.MaxValue;
            Canvas closestCanvas = null;
            RaycastResult closestResult = default;
            Vector3 closestHitPoint = Vector3.zero;

            // 使用 uGUI 的活动注册表，新创建/启用的 Canvas 无需等待 0.2 秒缓存轮询。
            var raycasters = RaycasterManager.GetRaycasters();
            for (int i = 0; i < raycasters.Count; i++)
            {
                var raycaster = raycasters[i] as GraphicRaycaster;
                if (raycaster == null || !raycaster.isActiveAndEnabled)
                    continue;
                var canvas = raycaster.GetComponent<Canvas>();
                if (
                    canvas == null
                    || !canvas.isActiveAndEnabled
                    || canvas.renderMode != RenderMode.WorldSpace
                )
                    continue;
                var eventCam = raycaster.eventCamera;
                if (eventCam == null)
                    continue;

                var plane = new Plane(canvas.transform.forward, canvas.transform.position);
                if (!plane.Raycast(ray, out float distance) || distance > rayLength)
                    continue;
                var hitPoint = ray.GetPoint(distance);
                var screenPoint = eventCam.WorldToScreenPoint(hitPoint);
                if (screenPoint.z <= 0)
                    continue;
                pointerData.position = screenPoint;
                raycastResults.Clear();
                raycaster.Raycast(pointerData, raycastResults);
                foreach (var result in raycastResults)
                {
                    var obj = result.gameObject;
                    if (obj.name == "Blocker")
                        continue;
                    if (activeDropdown != null && !IsInDropdownList(obj) && !IsDropdownBody(obj))
                        continue;

                    bool preferred = closestCanvas == null || distance < closestDistance;
                    if (
                        closestCanvas != null
                        && (
                            canvas.rootCanvas == closestCanvas.rootCanvas
                            || Mathf.Abs(distance - closestDistance) < 0.001f
                        )
                    )
                    {
                        int layer = SortingLayer.GetLayerValueFromID(result.sortingLayer);
                        int closestLayer = SortingLayer.GetLayerValueFromID(
                            closestResult.sortingLayer
                        );
                        if (layer != closestLayer)
                            preferred = layer > closestLayer;
                        else if (result.sortingOrder != closestResult.sortingOrder)
                            preferred = result.sortingOrder > closestResult.sortingOrder;
                    }
                    if (preferred)
                    {
                        closestDistance = distance;
                        closestCanvas = canvas;
                        closestResult = result;
                        closestResult.screenPosition = screenPoint;
                        closestHitPoint = hitPoint;
                    }
                    break;
                }
            }

            pointerData.pointerCurrentRaycast = closestResult;
            if (closestResult.gameObject != null)
            {
                IsHittingUI = true;
                currentUIHoverObject = closestResult.gameObject;
                CurrentHitPoint = closestHitPoint;
                pointerData.position = closestResult.screenPosition;
            }
        }

        // ============================================================
        //  UI Hover — 预分配 PointerEventData 避免 GC
        // ============================================================

        private void HandleUIHover()
        {
            if (hoverPointerData == null)
                hoverPointerData = new PointerEventData(eventSystem);

            if (currentUIHoverObject != null && currentUIHoverObject != previousUIHoverObject)
            {
                if (previousUIHoverObject != null)
                    ExecuteEvents.ExecuteHierarchy<IPointerExitHandler>(
                        previousUIHoverObject,
                        hoverPointerData,
                        ExecuteEvents.pointerExitHandler
                    );
                ExecuteEvents.ExecuteHierarchy<IPointerEnterHandler>(
                    currentUIHoverObject,
                    hoverPointerData,
                    ExecuteEvents.pointerEnterHandler
                );
            }
            if (currentUIHoverObject == null && previousUIHoverObject != null)
                ExecuteEvents.ExecuteHierarchy<IPointerExitHandler>(
                    previousUIHoverObject,
                    hoverPointerData,
                    ExecuteEvents.pointerExitHandler
                );
        }

        // ============================================================
        //  更新 PointerData
        // ============================================================

        private void UpdatePointerData()
        {
            if (pointerData == null)
                pointerData = new PointerEventData(eventSystem);

            if (
                isPointerDown
                && pointerPressCanvas != null
                && pointerPressCamera != null
                && visualRay != null
            )
            {
                Plane canvasPlane = new Plane(
                    pointerPressCanvas.transform.forward,
                    pointerPressCanvas.transform.position
                );
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
                    Camera eventCam =
                        canvas.worldCamera != null ? canvas.worldCamera : cachedMainCam;
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
            bool pressed = WasClickedThisFrame();
            bool released = WasReleasedThisFrame();
            bool held = IsTriggerHeld();

            // 丢失松开事件或控件被关闭时也必须结束交互，否则下一次按下会一直被拦截。
            if (
                isPointerDown
                && (
                    pointerData == null
                    || pointerData.rawPointerPress == null
                    || !pointerData.rawPointerPress.activeInHierarchy
                )
            )
                ReleasePointer(false);
            if (isPointerDown && (released || !held))
            {
                ReleasePointer(released);
                return;
            }

            if (pressed && !isPointerDown)
            {
                if (activeDropdown != null && !IsInDropdownList(currentUIHoverObject))
                {
                    CloseActiveDropdown();
                    return;
                }

                if (currentUIHoverObject != null)
                    PressPointer();
            }

            // 点击回调可能关闭菜单或禁用本组件。
            if (!isPointerDown || pointerData == null)
                return;

            if (pointerData.pointerDrag != null && held)
            {
                UpdatePointerData();
                if (
                    !pointerData.dragging
                    && (
                        !pointerData.useDragThreshold
                        || (pointerData.position - pointerData.pressPosition).sqrMagnitude
                            >= eventSystem.pixelDragThreshold * eventSystem.pixelDragThreshold
                    )
                )
                {
                    pointerData.dragging = true;
                    pointerData.eligibleForClick = false;
                    ExecuteEvents.Execute(
                        pointerData.pointerDrag,
                        pointerData,
                        ExecuteEvents.beginDragHandler
                    );
                }

                if (pointerData != null && pointerData.dragging)
                    ExecuteEvents.Execute(
                        pointerData.pointerDrag,
                        pointerData,
                        ExecuteEvents.dragHandler
                    );
            }

            // 一次输入更新里同时收到按下和松开时，也完成这次点击周期。
            if (isPointerDown && (released || !held))
                ReleasePointer(released);
        }

        private void PressPointer()
        {
            var data = pointerData;
            var hitObject = currentUIHoverObject;
            pointerClickTarget = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hitObject);
            var selectable =
                pointerClickTarget != null ? pointerClickTarget.GetComponent<Selectable>() : null;
            bool clickOnPress =
                selectable is Button || selectable is Toggle || selectable is TMP_Dropdown;

            pointerPressTarget =
                ExecuteEvents.GetEventHandler<IPointerDownHandler>(hitObject) ?? pointerClickTarget;
            pointerPressCanvas = hitObject.GetComponentInParent<Canvas>();
            pointerPressCamera =
                pointerPressCanvas != null && pointerPressCanvas.worldCamera != null
                    ? pointerPressCanvas.worldCamera
                    : cachedMainCam;
            data.pointerPressRaycast = new RaycastResult
            {
                module =
                    pointerPressCanvas != null
                        ? pointerPressCanvas.GetComponent<GraphicRaycaster>()
                        : null,
                gameObject = hitObject,
                screenPosition = data.position,
            };
            data.pointerPress = pointerPressTarget;
            data.rawPointerPress = hitObject;
            data.pressPosition = data.position;
            data.eligibleForClick = true;
            data.dragging = false;
            data.useDragThreshold = true;
            data.clickCount = 1;
            data.clickTime = Time.unscaledTime;
            isPointerDown = true;

            // 悬停用的 EventTrigger 也实现 IDragHandler；按钮/开关不能因此进入拖拽并丢掉点击。
            var dragObject = clickOnPress
                ? null
                : ExecuteEvents.GetEventHandler<IDragHandler>(hitObject);
            data.pointerDrag =
                dragObject != null
                && (pointerClickTarget == null || dragObject == pointerClickTarget)
                    ? dragObject
                    : null;
            if (data.pointerDrag != null)
                ExecuteEvents.Execute(
                    data.pointerDrag,
                    data,
                    ExecuteEvents.initializePotentialDrag
                );

            if (!isPointerDown || pointerData != data)
                return;
            ExecuteEvents.Execute(pointerPressTarget, data, ExecuteEvents.pointerDownHandler);
            if (!isPointerDown || pointerData != data)
                return;

            if (clickOnPress && pointerClickTarget != null)
            {
                // 先消费点击，避免回调切换 UI 后在松开时再次触发。
                data.eligibleForClick = false;
                bool selectingDropdownItem = selectable is Toggle && IsInDropdownList(hitObject);
                ExecuteEvents.Execute(pointerClickTarget, data, ExecuteEvents.pointerClickHandler);
                if (selectingDropdownItem)
                    CloseActiveDropdown();
            }
        }

        private void ReleasePointer(bool allowClick)
        {
            if (!isPointerDown)
                return;

            var data = pointerData;
            var pressTarget = pointerPressTarget;
            var clickTarget = pointerClickTarget;
            var dragTarget = data != null ? data.pointerDrag : null;
            bool wasDragging = data != null && data.dragging;
            bool shouldClick =
                allowClick
                && data != null
                && data.eligibleForClick
                && !wasDragging
                && clickTarget != null
                && ExecuteEvents.GetEventHandler<IPointerClickHandler>(currentUIHoverObject)
                    == clickTarget;

            // 回调可能禁用/销毁当前对象，先解除内部锁定，再发送收尾事件。
            isPointerDown = false;
            pointerPressTarget = null;
            pointerClickTarget = null;
            pointerPressCanvas = null;
            pointerPressCamera = null;
            if (data == null)
                return;

            ExecuteEvents.Execute(pressTarget, data, ExecuteEvents.pointerUpHandler);
            if (shouldClick && pointerData == data)
                ExecuteEvents.Execute(clickTarget, data, ExecuteEvents.pointerClickHandler);
            if (wasDragging)
                ExecuteEvents.Execute(dragTarget, data, ExecuteEvents.endDragHandler);

            data.pointerPress = null;
            data.rawPointerPress = null;
            data.pointerDrag = null;
            data.dragging = false;
            data.eligibleForClick = false;
        }

        // ============================================================
        //  3D 物体 hover 通知
        // ============================================================

        private void NotifyHoverState(GameObject obj, bool state, Vector3 hitPoint)
        {
            var note = obj.GetComponentInParent<NoteController>();
            if (note != null)
            {
                if (state)
                    note.OnRayHover(isRightHand);
                else
                    note.OnRayExit();
            }

            var sliderCtrl = obj.GetComponentInParent<SliderController>();
            if (sliderCtrl != null)
            {
                if (state)
                    sliderCtrl.OnRayStay(isRightHand, hitPoint);
                else
                    sliderCtrl.OnRayExit(isRightHand);
            }

            var spinner = obj.GetComponentInParent<SpinnerController>();
            if (spinner != null)
                spinner.isHovered = state;
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
            {
                Vector2 ms = Mouse.current.scroll.ReadValue();
                if (Mathf.Abs(ms.y) > 0.1f)
                    stickValue.y = ms.y > 0 ? 1f : -1f;
            }

            if (Mathf.Abs(stickValue.y) > 0.3f)
            {
                if (activeDropdown != null && dropdownListClone != null)
                {
                    var dropdownScroll = dropdownListClone.GetComponentInChildren<ScrollRect>();
                    if (
                        dropdownScroll != null
                        && dropdownScroll.gameObject.activeInHierarchy
                        && dropdownScroll.vertical
                    )
                    {
                        dropdownScroll.verticalNormalizedPosition +=
                            stickValue.y
                            * scrollSpeed
                            * Time.deltaTime
                            / dropdownScroll.content.rect.height;
                        dropdownScroll.verticalNormalizedPosition = Mathf.Clamp01(
                            dropdownScroll.verticalNormalizedPosition
                        );
                    }
                    return;
                }

                foreach (var sv in cachedScrollRects)
                {
                    if (
                        sv != null
                        && !sv.Equals(null)
                        && sv.gameObject.activeInHierarchy
                        && sv.vertical
                    )
                    {
                        sv.verticalNormalizedPosition +=
                            stickValue.y * scrollSpeed * Time.deltaTime / sv.content.rect.height;
                        sv.verticalNormalizedPosition = Mathf.Clamp01(
                            sv.verticalNormalizedPosition
                        );
                    }
                }
            }
        }

        public void SetMode(bool isLazyMode)
        {
            currentMode = isLazyMode ? ControlMode.WristGain : ControlMode.Direct1to1;
        }

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
