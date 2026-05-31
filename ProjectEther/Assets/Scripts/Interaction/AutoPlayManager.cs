using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.Reflection;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.Interaction.Toolkit;

namespace OsuVR
{
    /// <summary>
    /// 自动演奏管理器：实现 AI 自动游玩功能，支持拟人化手部动作
    /// 核心特性：
    /// - 智能任务分配：根据音符位置和时间自动选择最优手
    /// - 避障系统：防止手臂穿过滑条中段导致断连
    /// - 拟人化运动：使用缓动函数模拟自然的手部移动
    /// - 零帧瞬移：紧急避险时直接瞬移，不产生碰撞
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class AutoPlayManager : MonoBehaviour
    {
        [Header("核心引用")]
        [Tooltip("节奏游戏管理器，用于获取音符列表和游戏时间")]
        public RhythmGameManager gameManager;

        [Tooltip("左手射线控制器")]
        public RayController leftRay;

        [Tooltip("右手射线控制器")]
        public RayController rightRay;

        private Camera _cachedMainCam;

        [Header("拟人化参数 (Lazy Relax Style)")]
        [Tooltip("模拟头部高度（米），用于计算肩膀位置")]
        public float simulatedHeadHeight = 0.0f;

        [Tooltip("手臂伸展长度（米），控制手能到达的最远距离")]
        public float armExtension = 0.25f;

        /// <summary>
        /// 自动手控制器：封装单只手的所有状态和行为
        /// </summary>
        private class AutoHand
        {
            public RayController controller;
            public Transform transform;
            public Transform originalParent;

            // 保存用户原始设置，暂停时恢复给玩家
            public RayController.ControlMode userMode;
            public float userVerticalOffset;
            public Vector3 userDirectOffset;

            public Queue<HitObject> taskQueue = new Queue<HitObject>();
            public HitObject currentTask;

            public Vector3 taskSourceAim;
            public double taskApproachDuration;

            public Vector3 lastValidAimPos;
            public double lastHitEndTime;
            public Vector3 restAnchor;

            public List<MonoBehaviour> disabledComponents = new List<MonoBehaviour>();

            public HashSet<HitObject> triggeredNotes = new HashSet<HitObject>();
        }

        private AutoHand leftHand;
        private AutoHand rightHand;

        private HitObject lastAssignedNote;
        private AutoHand lastAssignedHand;

        private FieldInfo _notesField;
        private List<HitObject> _allNotes;
        private int _noteStartIndex = 0;
        private HashSet<HitObject> _assignedNotes = new HashSet<HitObject>();

        private Dictionary<HitObject, GameObject> _activeObjectsRef;
        private GameObject _tempRoot;

        /// <summary>
        /// 初始化：获取反射字段引用，创建临时手柄容器，初始化双手控制器
        /// </summary>
        void Start()
        {
            if (gameManager == null) return;

            _cachedMainCam = Camera.main;

            _notesField = typeof(RhythmGameManager).GetField("hitObjects", BindingFlags.NonPublic | BindingFlags.Instance);
            var activeObjField = typeof(RhythmGameManager).GetField("activeNoteObjects", BindingFlags.NonPublic | BindingFlags.Instance);
            if (activeObjField != null) _activeObjectsRef = (Dictionary<HitObject, GameObject>)activeObjField.GetValue(gameManager);

            _tempRoot = new GameObject("[AutoPlay_Temp_Hands]");
            _tempRoot.transform.position = Vector3.zero;
            _tempRoot.transform.rotation = Quaternion.identity;

            leftHand = InitHand(leftRay, false);
            rightHand = InitHand(rightRay, true);
        }

        /// <summary>
        /// 重试时重置 AutoPlayManager 状态：清空任务队列、已触发判定、重置进度索引
        /// </summary>
        public void ResetForRetry()
        {
            if (leftHand != null)
            {
                leftHand.taskQueue.Clear();
                leftHand.currentTask = null;
                leftHand.triggeredNotes.Clear();
                leftHand.lastHitEndTime = 0;
                leftHand.lastValidAimPos = Vector3.zero;
                leftHand.restAnchor = Vector3.zero;
            }
            if (rightHand != null)
            {
                rightHand.taskQueue.Clear();
                rightHand.currentTask = null;
                rightHand.triggeredNotes.Clear();
                rightHand.lastHitEndTime = 0;
                rightHand.lastValidAimPos = Vector3.zero;
                rightHand.restAnchor = Vector3.zero;
            }
            _allNotes = null;
            _noteStartIndex = 0;
            _assignedNotes.Clear();
            lastAssignedNote = null;
            lastAssignedHand = null;

            // 重新获取 hitObjects（谱面可能已重新加载）
            if (gameManager != null && _notesField != null)
            {
                var currentNotes = (List<HitObject>)_notesField.GetValue(gameManager);
                if (currentNotes != null)
                {
                    _allNotes = currentNotes.OrderBy(n => n.StartTime).ToList();
                    _noteStartIndex = 0;
                    _assignedNotes.Clear();
                }
                var activeObjField = typeof(RhythmGameManager).GetField("activeNoteObjects", BindingFlags.NonPublic | BindingFlags.Instance);
                if (activeObjField != null) _activeObjectsRef = (Dictionary<HitObject, GameObject>)activeObjField.GetValue(gameManager);
            }

            // 确保 Auto 模式已接管（重试后立即接管）
            isPaused = false;
            TakeOverHandForRestart(leftHand);
            TakeOverHandForRestart(rightHand);

            Debug.Log("[AutoPlayManager] 重试状态已重置");
        }

        /// <summary>
        /// 初始化单只手的控制器：禁用 XR 追踪组件，切换到直接控制模式
        /// </summary>
        /// <param name="ray">射线控制器</param>
        /// <param name="isRight">是否为右手</param>
        /// <returns>初始化后的 AutoHand 对象</returns>
        private AutoHand InitHand(RayController ray, bool isRight)
        {
            if (ray == null) return null;

            Transform originalParent = ray.transform.parent;
            ray.transform.SetParent(_tempRoot.transform, true);
            ray.gameObject.SetActive(true);
            ray.isRightHand = isRight;

            var hand = new AutoHand { controller = ray, transform = ray.transform, originalParent = originalParent };
            // 保存用户原始设置
            hand.userMode = ray.currentMode;
            hand.userVerticalOffset = ray.verticalOffset;
            hand.userDirectOffset = ray.directOffset;

            foreach (var d in ray.GetComponents<TrackedPoseDriver>()) { d.enabled = false; hand.disabledComponents.Add(d); }
            foreach (var c in ray.GetComponents<ActionBasedController>()) { c.enabled = false; hand.disabledComponents.Add(c); }

            ray.currentMode = RayController.ControlMode.Direct1to1;
            ray.verticalOffset = 0f;
            ray.directOffset = Vector3.zero;

            return hand;
        }

        /// <summary>
        /// 禁用时恢复手柄原始状态：还原父子关系，重新启用 XR 追踪组件
        /// </summary>
        void OnDisable()
        {
            RestoreHand(leftHand);
            RestoreHand(rightHand);
            // 不销毁 _tempRoot，重试时可能需要重新使用
        }

        /// <summary>
        /// 恢复单只手的原始状态
        /// </summary>
        private void RestoreHand(AutoHand hand)
        {
            if (hand == null || hand.transform == null || hand.transform.Equals(null)) return;
            if (hand.originalParent != null && !hand.originalParent.Equals(null))
            {
                hand.transform.SetParent(hand.originalParent, true);
                hand.transform.localPosition = Vector3.zero;
                hand.transform.localRotation = Quaternion.identity;
            }
            foreach (var c in hand.disabledComponents) if (c != null) c.enabled = true;
            hand.disabledComponents.Clear();
        }

        /// <summary>
        /// 主循环：分配任务并更新双手运动
        /// 使用 Update 而非 LateUpdate，确保手位置在 RayController 射线检测之前更新
        /// </summary>
        private bool isPaused = false;
        private Vector3 leftPausePosition;
        private Quaternion leftPauseRotation;
        private Vector3 rightPausePosition;
        private Quaternion rightPauseRotation;

        /// <summary>
        /// 临时恢复手柄控制：暂停时让玩家能操作暂停菜单
        /// 恢复 XR 追踪组件，把手柄还给原始父级
        /// 保持射线偏转方向和正常游玩一致，方便玩家操作
        /// </summary>
        public void OnGamePaused()
        {
            if (isPaused) return;
            isPaused = true;

            // 保存暂停时 Auto 空间的手柄位置，恢复时用这些位置避免 miss
            if (leftHand != null && leftHand.transform != null)
            {
                leftPausePosition = leftHand.transform.position;
                leftPauseRotation = leftHand.transform.rotation;
            }
            if (rightHand != null && rightHand.transform != null)
            {
                rightPausePosition = rightHand.transform.position;
                rightPauseRotation = rightHand.transform.rotation;
            }

            RestoreHandForPlayerControl(leftHand);
            RestoreHandForPlayerControl(rightHand);
            Debug.Log("[AutoPlayManager] 暂停 → 手柄已还给玩家");
        }

        /// <summary>
        /// 重新接管手柄：恢复游戏时重新禁用 XR 追踪，切换到 Auto 模式
        /// 立即将手柄设回暂停时的位置，防止 3 秒倒计时期间 miss
        /// </summary>
        public void OnGameResumed()
        {
            if (!isPaused) return;
            isPaused = false;
            TakeOverHand(leftHand, leftPausePosition, leftPauseRotation);
            TakeOverHand(rightHand, rightPausePosition, rightPauseRotation);
            Debug.Log("[AutoPlayManager] 恢复 → 手柄已重新接管，位置已还原到暂停时");
        }

        /// <summary>
        /// 临时恢复：启用 XR 追踪组件，还原父子关系
        /// 保持射线的偏转方向和正常游玩一致（verticalOffset, directOffset）
        /// </summary>
        private void RestoreHandForPlayerControl(AutoHand hand)
        {
            if (hand == null || hand.transform == null || hand.transform.Equals(null)) return;

            // 还原父子关系
            if (hand.originalParent != null && !hand.originalParent.Equals(null))
            {
                hand.transform.SetParent(hand.originalParent, true);
                hand.transform.localPosition = Vector3.zero;
                hand.transform.localRotation = Quaternion.identity;
            }

            // 重新启用 XR 追踪组件
            foreach (var c in hand.disabledComponents)
            {
                if (c != null) c.enabled = true;
            }

            // 恢复用户原始的射线偏转设置
            if (hand.controller != null)
            {
                hand.controller.currentMode = hand.userMode;
                hand.controller.verticalOffset = hand.userVerticalOffset;
                hand.controller.directOffset = hand.userDirectOffset;
            }
        }

        /// <summary>
        /// 重新接管：禁用 XR 追踪组件，移到临时容器，切换到 Auto 模式
        /// 立即将手柄设回暂停时保存的位置，防止倒计时期间 miss
        /// </summary>
        private void TakeOverHand(AutoHand hand, Vector3 savedPosition, Quaternion savedRotation)
        {
            if (hand == null || hand.transform == null || hand.transform.Equals(null)) return;

            // 确保 _tempRoot 存在
            if (_tempRoot == null || _tempRoot.Equals(null))
            {
                _tempRoot = new GameObject("[AutoPlay_Temp_Hands]");
                _tempRoot.transform.position = Vector3.zero;
                _tempRoot.transform.rotation = Quaternion.identity;
            }

            // 先禁用 XR 追踪组件（必须在移到临时容器之前，防止 XR 覆盖位置）
            hand.disabledComponents.Clear();
            foreach (var d in hand.transform.GetComponents<TrackedPoseDriver>()) { d.enabled = false; hand.disabledComponents.Add(d); }
            foreach (var c in hand.transform.GetComponents<ActionBasedController>()) { c.enabled = false; hand.disabledComponents.Add(c); }

            // 移回临时容器
            if (_tempRoot != null)
            {
                hand.transform.SetParent(_tempRoot.transform, false);
            }

            // 立即将手柄设回暂停时的位置（防止 3 秒倒计时期间 miss）
            hand.transform.position = savedPosition;
            hand.transform.rotation = savedRotation;
            hand.lastValidAimPos = savedPosition;

            // 切换到 Auto 模式
            if (hand.controller != null)
            {
                hand.controller.currentMode = RayController.ControlMode.Direct1to1;
                hand.controller.verticalOffset = 0f;
                hand.controller.directOffset = Vector3.zero;
            }
        }

        /// <summary>
        /// 重试时接管手柄：不需要还原暂停位置，Auto 会从初始位置重新计算
        /// 确保 _tempRoot 存在（可能已被 OnDisable 销毁需要重建）
        /// </summary>
        private void TakeOverHandForRestart(AutoHand hand)
        {
            if (hand == null || hand.transform == null || hand.transform.Equals(null)) return;

            // 确保 _tempRoot 存在
            if (_tempRoot == null || _tempRoot.Equals(null))
            {
                _tempRoot = new GameObject("[AutoPlay_Temp_Hands]");
                _tempRoot.transform.position = Vector3.zero;
                _tempRoot.transform.rotation = Quaternion.identity;
            }

            // 先禁用 XR 追踪组件
            hand.disabledComponents.Clear();
            foreach (var d in hand.transform.GetComponents<TrackedPoseDriver>()) { d.enabled = false; hand.disabledComponents.Add(d); }
            foreach (var c in hand.transform.GetComponents<ActionBasedController>()) { c.enabled = false; hand.disabledComponents.Add(c); }

            // 移到临时容器
            if (_tempRoot != null)
            {
                hand.transform.SetParent(_tempRoot.transform, false);
            }

            // 重试不需要还原暂停位置，Auto 会从休息位开始
            // 切换到 Auto 模式
            if (hand.controller != null)
            {
                hand.controller.currentMode = RayController.ControlMode.Direct1to1;
                hand.controller.verticalOffset = 0f;
                hand.controller.directOffset = Vector3.zero;
            }
        }

        void Update()
        {
            if (_cachedMainCam == null) _cachedMainCam = Camera.main;
            if (_cachedMainCam != null && _cachedMainCam.transform.localPosition.y < 0.1f && simulatedHeadHeight > 0.01f)
                _cachedMainCam.transform.localPosition = new Vector3(0, simulatedHeadHeight, 0);

            if (gameManager == null) return;
            // 游戏进行中或缓冲期内都要更新手柄（缓冲期内提前移动到首个音符位置）
            if (!gameManager.isPlaying && !gameManager.isBufferPhase) return;

            // 检查手柄是否有效（重试时可能被 OnDisable 还原导致 transform 无效）
            if (leftHand != null && (leftHand.transform == null || leftHand.transform.Equals(null)))
                return;
            if (rightHand != null && (rightHand.transform == null || rightHand.transform.Equals(null)))
                return;

            double time = gameManager.currentMusicTimeMs;

            AssignTasks(time);
            if (_allNotes == null || _allNotes.Count == 0) return;

            UpdateHandMotion(leftHand, time);
            UpdateHandMotion(rightHand, time);
        }

        /// <summary>
        /// 获取肩膀位置：基于头部位置计算左右肩膀的空间坐标
        /// </summary>
        private Vector3 GetShoulderPos(AutoHand hand)
        {
            Vector3 headPos = _cachedMainCam ? _cachedMainCam.transform.position : new Vector3(0, simulatedHeadHeight, 0);
            float sign = hand.controller.isRightHand ? 1f : -1f;
            return headPos + new Vector3(sign * 0.2f, -0.25f, 0.0f);
        }

        /// <summary>
        /// 三次缓动函数：平滑的加速-减速曲线，模拟自然的手部运动
        /// </summary>
        private float EasingInOutCubic(float t)
        {
            if (t <= 0) return 0;
            if (t >= 1) return 1;
            return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
        }

        /// <summary>
        /// 智能警戒雷达：计算悬停姿态，检测并避开即将到来的音符
        /// 核心算法：
        /// - HitCircle：20cm 警戒圈，轻微避让
        /// - Slider：45cm 警戒圈，严格防守防止刮断
        /// - 提前 1.2 秒预警，确保有足够时间避险
        /// </summary>
        private Vector3 GetHoverPose(AutoHand hand, double time, bool isLongBreak, out bool isDodging)
        {
            isDodging = false;
            Vector3 baseWorld;

            if (isLongBreak)
            {
                float baseX = hand.controller.isRightHand ? 480f : 32f;
                baseWorld = CoordinateMapper.MapToWorld(new Vector2(baseX, 192f));
            }
            else
            {
                baseWorld = hand.restAnchor;
                if (baseWorld == Vector3.zero)
                    baseWorld = CoordinateMapper.MapToWorld(new Vector2(hand.controller.isRightHand ? 480f : 32f, 192f));
            }

            if (_allNotes != null)
            {
                for (int i = _noteStartIndex; i < _allNotes.Count; i++)
                {
                    var danger = _allNotes[i];

                    if (danger.StartTime > time + 1200) break;
                    if (GetTaskEndTime(danger) < time) continue;

                    if (hand.currentTask == danger || hand.taskQueue.Contains(danger)) continue;

                    // 分类处理 1：如果是普通圈 (HitCircle)，缩小警戒圈
                    if (danger is HitCircle)
                    {
                        Vector3 p1 = CoordinateMapper.MapToWorld(danger.Position);
                        // 普通圈警戒圈仅 20 厘米（微小避让，防止 Relax 提前吸附即可）
                        if (Vector3.Distance(baseWorld, p1) < 0.2f)
                        {
                            isDodging = true;
                            break;
                        }
                    }
                    // 分类处理 2：如果是滑条 (Slider)，保持严密防守
                    else if (danger is SliderObject s)
                    {
                        float minDist = float.MaxValue;

                        if (_activeObjectsRef != null && _activeObjectsRef.TryGetValue(danger, out GameObject obj) && obj != null)
                        {
                            var renderers = obj.GetComponentsInChildren<Renderer>(true);
                            foreach (var r in renderers)
                            {
                                Vector3 closestPoint = r.bounds.ClosestPoint(baseWorld);
                                float d = Vector3.Distance(baseWorld, closestPoint);
                                if (d < minDist) minDist = d;
                            }
                        }
                        else
                        {
                            minDist = Mathf.Min(minDist, Vector3.Distance(baseWorld, CoordinateMapper.MapToWorld(s.Position)));
                            minDist = Mathf.Min(minDist, Vector3.Distance(baseWorld, CoordinateMapper.MapToWorld(s.EndPosition)));
                        }

                        // 滑条本体警戒圈依然保持 45 厘米，绝对防止中段刮断连击
                        if (minDist < 0.45f)
                        {
                            isDodging = true;
                            break;
                        }
                    }
                }
            }

            if (isDodging)
            {
                float safeX = hand.controller.isRightHand ? 612f : -100f;
                return CoordinateMapper.MapToWorld(new Vector2(safeX, -50f));
            }

            float scale = isLongBreak ? 1f : 0.4f;
            float wanderX = Mathf.Sin((float)time * 0.0015f) * (30f * scale) + Mathf.Cos((float)time * 0.0009f) * (15f * scale);
            float wanderY = Mathf.Sin((float)time * 0.0019f) * (35f * scale) + Mathf.Cos((float)time * 0.0011f) * (20f * scale);

            Vector3 wanderWorld = CoordinateMapper.MapToWorld(new Vector2(256 + wanderX, 192 + wanderY)) - CoordinateMapper.MapToWorld(new Vector2(256, 192));

            return baseWorld + wanderWorld;
        }

        /// <summary>
        /// 运动学引擎：更新手部位置和旋转
        /// 核心特性：
        /// - 真·0帧瞬移：避险时直接瞬移到安全位置
        /// - 拟人化移动：使用 Lerp/Slerp 平滑过渡
        /// - 自动触发判定：到达精确时间点时触发音符判定
        /// </summary>
        private void UpdateHandMotion(AutoHand hand, double time)
        {
            if (hand == null || hand.transform == null || hand.transform.Equals(null)) return;
            CheckTask(hand, time);

            Vector3 shoulderPos = GetShoulderPos(hand);
            Vector3 aimTarget = hand.lastValidAimPos;
            bool isHitting = false;
            bool shouldSnap = false;

            if (hand.currentTask != null)
            {
                double timeUntilHit = hand.currentTask.StartTime - time;

                if (hand.currentTask is SpinnerObject sp)
                {
                    if (time < sp.StartTime)
                    {
                        float t = 1f - (float)(timeUntilHit / hand.taskApproachDuration);
                        t = EasingInOutCubic(Mathf.Clamp01(t));
                        Vector3 center = CoordinateMapper.MapToWorld(new Vector2(256, 192));
                        aimTarget = Vector3.Lerp(hand.taskSourceAim, center, t);
                    }
                    else
                    {
                        aimTarget = GetAimTarget(hand, time);
                        if (time <= sp.EndTime) isHitting = true;
                    }
                }
                else
                {
                    float approachDuration = (float)hand.taskApproachDuration;
                    bool isLongBreak = timeUntilHit > 800;
                    bool isDodging;
                    Vector3 targetHover = GetHoverPose(hand, time, isLongBreak, out isDodging);

                    // 距离目标还有 120ms 时，强行解除躲避状态，像闪电一样去接球
                    bool forceDodge = isDodging && timeUntilHit > 120f;

                    if (timeUntilHit > approachDuration || forceDodge)
                    {
                        if (forceDodge)
                        {
                            aimTarget = targetHover;
                            shouldSnap = true;
                        }
                        else
                        {
                            aimTarget = Vector3.Lerp(hand.lastValidAimPos, targetHover, Time.deltaTime * 15f);
                        }

                        hand.taskSourceAim = aimTarget;
                    }
                    else if (timeUntilHit > 0)
                    {
                        float effectiveDuration = forceDodge ? 120f : approachDuration;
                        if (effectiveDuration < 5f) effectiveDuration = 5f;

                        float t = 1f - (float)(timeUntilHit / effectiveDuration);
                        t = EasingInOutCubic(Mathf.Clamp01(t));

                        Vector3 targetCircle = CoordinateMapper.MapToWorld(hand.currentTask.Position);
                        aimTarget = Vector3.Lerp(hand.taskSourceAim, targetCircle, t);

                        if (timeUntilHit <= 30) isHitting = true;
                    }
                    else
                    {
                        aimTarget = GetAimTarget(hand, time);
                        if (hand.currentTask is HitCircle && Mathf.Abs((float)timeUntilHit) <= 30) isHitting = true;
                        if (hand.currentTask is SliderObject s && time >= s.StartTime && time <= s.EndTime) isHitting = true;
                    }
                }
            }
            else
            {
                bool isDodging;
                Vector3 targetHover = GetHoverPose(hand, time, true, out isDodging);

                if (isDodging)
                {
                    aimTarget = targetHover;
                    shouldSnap = true;
                }
                else
                {
                    aimTarget = Vector3.Lerp(hand.lastValidAimPos, targetHover, Time.deltaTime * 15f);
                }

                hand.taskSourceAim = aimTarget;
            }

            hand.lastValidAimPos = aimTarget;

            Vector3 dirToTarget = (aimTarget - shoulderPos).normalized;
            Vector3 targetPos = shoulderPos + dirToTarget * armExtension;
            Quaternion targetRot = Quaternion.LookRotation(dirToTarget);

            if (isHitting) targetPos += dirToTarget * 0.05f;

            // 瞬移解禁：如果拉响了避难警报，直接用 `=` 赋值覆盖
            // 这意味着手柄在当前帧会在屏幕中消失，并直接在绝对死角出现，绝不会发生沿途碰撞！
            if (shouldSnap)
            {
                hand.transform.position = targetPos;
                hand.transform.rotation = targetRot;
            }
            else
            {
                hand.transform.position = Vector3.Lerp(hand.transform.position, targetPos, Time.deltaTime * 120f);
                hand.transform.rotation = Quaternion.Slerp(hand.transform.rotation, targetRot, Time.deltaTime * 120f);
            }

            // AutoPlay 直接触发判定，确保音效播放
            // 只有手移动到位才会触发（位置判定仍然生效）
            TryTriggerHit(hand, time);
        }

        /// <summary>
        /// 直接触发判定：在精确时间点调用音符的 OnHit 方法
        /// 判定逻辑：
        /// - 0ms 判定：到达或超过判定时间就立即触发
        /// - 允许最多 16ms 提前（约一帧），确保 AutoPlay 总是精确判定
        /// - 使用 HashSet 防止重复触发同一音符
        /// </summary>
        private void TryTriggerHit(AutoHand hand, double time)
        {
            if (hand.currentTask == null) return;

            double timeUntilHit = hand.currentTask.StartTime - time;

            // 0ms 判定：到达或超过判定时间就触发（允许最多 16ms 提前，约一帧）
            // 这样确保 AutoPlay 总是精确判定，不会提前太多
            if (timeUntilHit <= 16 && timeUntilHit >= -100)
            {
                // 检查是否已经触发过
                if (hand.triggeredNotes == null) hand.triggeredNotes = new HashSet<HitObject>();
                if (hand.triggeredNotes.Contains(hand.currentTask)) return;

                // 获取活跃对象
                if (_activeObjectsRef == null || !_activeObjectsRef.TryGetValue(hand.currentTask, out GameObject obj) || obj == null) return;

                bool isRightHand = hand.controller.isRightHand;

                // HitCircle：直接调用 OnHit
                if (hand.currentTask is HitCircle)
                {
                    var noteCtrl = obj.GetComponent<NoteController>();
                    if (noteCtrl != null && !noteCtrl.hasBeenHit && noteCtrl.isActive)
                    {
                        hand.triggeredNotes.Add(hand.currentTask);
                        noteCtrl.OnHit(0, isRightHand); // 0 = 完美判定
                    }
                }
                // Slider：直接调用 TryHitHead
                else if (hand.currentTask is SliderObject)
                {
                    var sliderCtrl = obj.GetComponent<SliderController>();
                    if (sliderCtrl != null && !sliderCtrl.IsHeadHit)
                    {
                        hand.triggeredNotes.Add(hand.currentTask);
                        sliderCtrl.TryHitHead(isRightHand, Vector3.zero); // 参数：(isRightHand, hitPos)
                    }
                }
            }
        }

        /// <summary>
        /// 目标解算：根据当前任务类型计算手应该瞄准的位置
        /// - Spinner：绕中心旋转
        /// - Slider：追踪滑条球的位置
        /// - HitCircle：直接瞄准目标位置
        /// </summary>
        private Vector3 GetAimTarget(AutoHand hand, double time)
        {
            if (hand.currentTask is SpinnerObject)
            {
                Vector3 center = CoordinateMapper.MapToWorld(new Vector2(256, 192));
                float angle = (float)(time * 0.05f);
                if (hand.controller.isRightHand) angle += Mathf.PI;

                return center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * 0.2f;
            }
            else if (hand.currentTask is SliderObject s)
            {
                bool foundBall = false;
                Vector3 ballPos = Vector3.zero;

                if (_activeObjectsRef != null && _activeObjectsRef.TryGetValue(s, out GameObject obj) && obj != null)
                {
                    var renderers = obj.GetComponentsInChildren<Renderer>(true);
                    foreach (var r in renderers)
                    {
                        if (r.name.Contains("Ball") || r.name.Contains("Sphere") || r.gameObject.name.Contains("FollowBall"))
                        {
                            ballPos = r.transform.position;
                            foundBall = true;
                            break;
                        }
                    }
                }

                if (foundBall)
                {
                    hand.lastValidAimPos = ballPos;
                    return ballPos;
                }
                else
                {
                    if (time >= s.StartTime && hand.lastValidAimPos != Vector3.zero)
                    {
                        return hand.lastValidAimPos;
                    }
                    Vector3 startPos = CoordinateMapper.MapToWorld(s.Position);
                    hand.lastValidAimPos = startPos;
                    return startPos;
                }
            }

            Vector3 normalPos = CoordinateMapper.MapToWorld(hand.currentTask.Position);
            hand.lastValidAimPos = normalPos;
            return normalPos;
        }

        /// <summary>
        /// 任务检查：判断当前任务是否完成，完成后从队列取出下一个任务
        /// </summary>
        private void CheckTask(AutoHand hand, double time)
        {
            if (hand.currentTask != null)
            {
                bool done = false;
                if (hand.currentTask is HitCircle && time > hand.currentTask.StartTime + 20) done = true;
                else if (hand.currentTask is SliderObject s && time > s.EndTime + 25) done = true;
                else if (hand.currentTask is SpinnerObject sp && time > sp.EndTime) done = true;

                if (done)
                {
                    hand.lastHitEndTime = time;
                    hand.restAnchor = hand.lastValidAimPos;
                    hand.currentTask = null;
                }
            }

            if (hand.currentTask == null && hand.taskQueue.Count > 0)
            {
                hand.currentTask = hand.taskQueue.Dequeue();

                hand.taskSourceAim = hand.lastValidAimPos;

                if (hand.taskSourceAim == Vector3.zero)
                {
                    bool initDodging;
                    hand.taskSourceAim = GetHoverPose(hand, time, true, out initDodging);
                }

                double timeSinceLast = hand.currentTask.StartTime - hand.lastHitEndTime;
                hand.taskApproachDuration = Mathf.Clamp((float)timeSinceLast, 80f, 500f);
            }
        }

        /// <summary>
        /// 读谱分配：扫描未分配的音符，智能分配给左右手
        /// 核心优化：
        /// - O(1) 时间复杂度：使用 _noteStartIndex 记录扫描进度
        /// - 防重复分配：使用 HashSet 记录已分配的音符
        /// - Spinner 双手处理：转盘需要双手同时参与
        /// </summary>
        private void AssignTasks(double currentTime)
        {
            if (_notesField == null) return;

            var currentNotes = (List<HitObject>)_notesField.GetValue(gameManager);
            if (currentNotes == null) return;

            if (_allNotes == null || _allNotes.Count != currentNotes.Count)
            {
                _allNotes = currentNotes.OrderBy(n => n.StartTime).ToList();
                _noteStartIndex = 0;
                _assignedNotes.Clear();

                // 谱面重置时，清空已触发判定的音符记录
                if (leftHand != null && leftHand.triggeredNotes != null) leftHand.triggeredNotes.Clear();
                if (rightHand != null && rightHand.triggeredNotes != null) rightHand.triggeredNotes.Clear();
            }
            if (_allNotes.Count == 0) return;

            while (_noteStartIndex < _allNotes.Count && GetTaskEndTime(_allNotes[_noteStartIndex]) < currentTime - 1000)
            {
                _noteStartIndex++;
            }

            int assignedCount = 0;
            for (int i = _noteStartIndex; i < _allNotes.Count; i++)
            {
                var note = _allNotes[i];
                if (note.StartTime > currentTime + 2000) break;

                // 核心防打架：只要在这个表里的，绝对不再看第二眼
                if (note.StartTime >= currentTime - 20 && !_assignedNotes.Contains(note))
                {
                    if (note is SpinnerObject)
                    {
                        leftHand.taskQueue.Enqueue(note);
                        rightHand.taskQueue.Enqueue(note);
                        _assignedNotes.Add(note); // 记入生死簿
                        lastAssignedNote = note;
                    }
                    else
                    {
                        AutoHand targetHand = ChooseHandPro(note);
                        if (targetHand != null)
                        {
                            targetHand.taskQueue.Enqueue(note);
                            _assignedNotes.Add(note); // 记入生死簿
                            lastAssignedNote = note;
                            lastAssignedHand = targetHand;
                        }
                    }

                    assignedCount++;
                    if (assignedCount >= 10) break;
                }
            }
        }

        /// <summary>
        /// 获取音符的结束位置：Slider 返回终点，其他返回起点
        /// </summary>
        private Vector2 GetNoteEndPosition(HitObject note)
        {
            if (note is SliderObject s) return s.EndPosition;
            return note.Position;
        }

        /// <summary>
        /// 智能手选择算法：根据音符位置、时间和上一音符信息选择最优手
        /// 核心策略：
        /// - 空闲优先：选择当前空闲的手
        /// - 堆叠锁定：物理距离极近时强制使用同一只手
        /// - 同 Combo 锁定：同一 Combo 内近距离音符优先用同一手
        /// - 位置偏好：左侧音符倾向左手，右侧倾向右手
        /// </summary>
        private AutoHand ChooseHandPro(HitObject note)
        {
            bool leftFree = IsHandFreeAt(leftHand, note.StartTime);
            bool rightFree = IsHandFreeAt(rightHand, note.StartTime);

            if (leftFree && !rightFree) return leftHand;
            if (rightFree && !leftFree) return rightHand;
            if (!leftFree && !rightFree) return GetLastEndTime(leftHand) <= GetLastEndTime(rightHand) ? leftHand : rightHand;

            if (lastAssignedNote != null && lastAssignedHand != null)
            {
                // 测算距离时，用上一个物件的”结束坐标(EndPosition)”来测算
                float distance = Vector2.Distance(GetNoteEndPosition(lastAssignedNote), note.Position);
                double timeDelta = note.StartTime - GetTaskEndTime(lastAssignedNote);

                if (timeDelta > 800) return note.Position.x < 256 ? leftHand : rightHand;

                // 终极堆叠锁定：只要物理距离极近 (< 40)，说明是滑条头尾堆叠或原地连打
                // 无视一切 Combo 颜色，直接强制使用这只手，绝不打架！
                if (distance < 40f && IsHandFreeAt(lastAssignedHand, note.StartTime))
                {
                    return lastAssignedHand;
                }

                // 常规同 Combo 锁定
                if (lastAssignedNote.ComboIndex == note.ComboIndex)
                {
                    if (distance < 250 && IsHandFreeAt(lastAssignedHand, note.StartTime))
                        return lastAssignedHand;
                }
                else
                {
                    if (distance < 100 && IsHandFreeAt(lastAssignedHand, note.StartTime))
                        return lastAssignedHand;
                }

                return lastAssignedHand == leftHand ? rightHand : leftHand;
            }

            return note.Position.x < 256 ? leftHand : rightHand;
        }

        /// <summary>
        /// 检查手在指定时间是否空闲
        /// </summary>
        private bool IsHandFreeAt(AutoHand hand, double targetTime)
        {
            return GetLastEndTime(hand) <= targetTime;
        }

        /// <summary>
        /// 获取手最后任务的结束时间：遍历当前任务和队列找出最晚结束时间
        /// </summary>
        private double GetLastEndTime(AutoHand hand)
        {
            double lastTime = 0;
            if (hand.currentTask != null) lastTime = GetTaskEndTime(hand.currentTask);
            foreach (var t in hand.taskQueue)
            {
                double tEnd = GetTaskEndTime(t);
                if (tEnd > lastTime) lastTime = tEnd;
            }
            return lastTime;
        }

        /// <summary>
        /// 获取任务的结束时间：Slider 返回 EndTime，Spinner 返回 EndTime，其他返回 StartTime
        /// </summary>
        private double GetTaskEndTime(HitObject task)
        {
            if (task is SliderObject s) return s.EndTime;
            if (task is SpinnerObject sp) return sp.EndTime;
            return task.StartTime;
        }
    }
}