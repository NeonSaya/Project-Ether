using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.Reflection;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.Interaction.Toolkit;

namespace OsuVR
{
    public class AutoPlayManager : MonoBehaviour
    {
        [Header("核心引用")]
        public RhythmGameManager gameManager;
        public RayController leftRay;
        public RayController rightRay;

        [Header("拟人化参数 (Lazy Relax Style)")]
        public float simulatedHeadHeight = 0.0f;
        public float armExtension = 0.25f;

        private class AutoHand
        {
            public RayController controller;
            public Transform transform;
            public Transform originalParent;

            public Queue<HitObject> taskQueue = new Queue<HitObject>();
            public HitObject currentTask;

            public Vector3 taskSourceAim;
            public double taskApproachDuration;

            public Vector3 lastValidAimPos;
            public double lastHitEndTime;
            public Vector3 restAnchor;

            public List<MonoBehaviour> disabledComponents = new List<MonoBehaviour>();
        }

        private AutoHand leftHand;
        private AutoHand rightHand;

        private HitObject lastAssignedNote;
        private AutoHand lastAssignedHand;

        private FieldInfo _notesField;
        private List<HitObject> _allNotes;
        private int _noteStartIndex = 0; // ✅ 终极性能优化：记录当前进度，告别全局扫描导致的时间越长越卡！
        private HashSet<HitObject> _assignedNotes = new HashSet<HitObject>();

        private Dictionary<HitObject, GameObject> _activeObjectsRef;
        private GameObject _tempRoot;

        void Start()
        {
            if (gameManager == null) return;

            _notesField = typeof(RhythmGameManager).GetField("hitObjects", BindingFlags.NonPublic | BindingFlags.Instance);
            var activeObjField = typeof(RhythmGameManager).GetField("activeNoteObjects", BindingFlags.NonPublic | BindingFlags.Instance);
            if (activeObjField != null) _activeObjectsRef = (Dictionary<HitObject, GameObject>)activeObjField.GetValue(gameManager);

            _tempRoot = new GameObject("[AutoPlay_Temp_Hands]");
            _tempRoot.transform.position = Vector3.zero;
            _tempRoot.transform.rotation = Quaternion.identity;

            leftHand = InitHand(leftRay, false);
            rightHand = InitHand(rightRay, true);
        }

        private AutoHand InitHand(RayController ray, bool isRight)
        {
            if (ray == null) return null;

            Transform originalParent = ray.transform.parent;
            ray.transform.SetParent(_tempRoot.transform, true);
            ray.gameObject.SetActive(true);
            ray.isRightHand = isRight;

            var hand = new AutoHand { controller = ray, transform = ray.transform, originalParent = originalParent };

            foreach (var d in ray.GetComponents<TrackedPoseDriver>()) { d.enabled = false; hand.disabledComponents.Add(d); }
            foreach (var c in ray.GetComponents<ActionBasedController>()) { c.enabled = false; hand.disabledComponents.Add(c); }

            ray.currentMode = RayController.ControlMode.Direct1to1;
            ray.verticalOffset = 0f;
            ray.directOffset = Vector3.zero;

            return hand;
        }

        void OnDisable()
        {
            RestoreHand(leftHand);
            RestoreHand(rightHand);
            if (_tempRoot != null) Destroy(_tempRoot);
        }

        private void RestoreHand(AutoHand hand)
        {
            if (hand == null || hand.transform == null) return;
            if (hand.originalParent != null)
            {
                hand.transform.SetParent(hand.originalParent, true);
                hand.transform.localPosition = Vector3.zero;
                hand.transform.localRotation = Quaternion.identity;
            }
            foreach (var c in hand.disabledComponents) if (c != null) c.enabled = true;
            hand.disabledComponents.Clear();
        }

        void LateUpdate()
        {
            if (Camera.main != null && Camera.main.transform.localPosition.y < 0.1f && simulatedHeadHeight > 0.01f)
                Camera.main.transform.localPosition = new Vector3(0, simulatedHeadHeight, 0);

            if (gameManager == null || !gameManager.isPlaying) return;

            double time = gameManager.currentMusicTimeMs;

            AssignTasks(time);
            if (_allNotes == null || _allNotes.Count == 0) return;

            UpdateHandMotion(leftHand, time);
            UpdateHandMotion(rightHand, time);
        }

        private Vector3 GetShoulderPos(AutoHand hand)
        {
            Vector3 headPos = Camera.main ? Camera.main.transform.position : new Vector3(0, simulatedHeadHeight, 0);
            float sign = hand.controller.isRightHand ? 1f : -1f;
            return headPos + new Vector3(sign * 0.2f, -0.25f, 0.0f);
        }

        private float EasingInOutCubic(float t)
        {
            if (t <= 0) return 0;
            if (t >= 1) return 1;
            return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
        }

        // ==========================================
        // 🛡️ 智能警戒雷达 (终极防撞：读取真实物理本体模型 + 提早1.2秒预警)
        // ==========================================
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

                    // ✅ 分类处理 1：如果是普通圈 (HitCircle)，缩小警戒圈，别一惊一乍！
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
                    // ✅ 分类处理 2：如果是滑条 (Slider)，保持严密防守！
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

        // ==========================================
        // 🔥 1. 运动学引擎 (真·0帧瞬移避险)
        // ==========================================
        private void UpdateHandMotion(AutoHand hand, double time)
        {
            if (hand == null) return;
            CheckTask(hand, time);

            Vector3 shoulderPos = GetShoulderPos(hand);
            Vector3 aimTarget = hand.lastValidAimPos;
            bool isHitting = false;
            bool shouldSnap = false; // ✅ 新增：控制物理引擎是否执行 0 帧瞬移！

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
                            shouldSnap = true; // ✅ 指示物理引擎瞬移！
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
                    shouldSnap = true; // ✅ 指示物理引擎瞬移！
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

            // ✅ 真·瞬移解禁：如果拉响了避难警报，直接用 `=` 赋值覆盖！
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
        }

        // ==========================================
        // 🎯 2. 目标解算与队列流转
        // ==========================================
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

        // ==========================================
        // 🧠 3. 读谱分配 (O(1) 无内存泄漏版本)
        // ==========================================
        private void AssignTasks(double currentTime)
        {
            if (_notesField == null) return;

            var currentNotes = (List<HitObject>)_notesField.GetValue(gameManager);
            if (currentNotes == null) return;

            if (_allNotes == null || _allNotes.Count != currentNotes.Count)
            {
                _allNotes = currentNotes.OrderBy(n => n.StartTime).ToList();
                _noteStartIndex = 0;
                _assignedNotes.Clear(); // ✅ 铺面重置时，清空生死簿
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

                // ✅ 核心防打架：只要在这个表里的，绝对不再看第二眼！
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

        private Vector2 GetNoteEndPosition(HitObject note)
        {
            if (note is SliderObject s) return s.EndPosition;
            return note.Position;
        }

        private AutoHand ChooseHandPro(HitObject note)
        {
            bool leftFree = IsHandFreeAt(leftHand, note.StartTime);
            bool rightFree = IsHandFreeAt(rightHand, note.StartTime);

            if (leftFree && !rightFree) return leftHand;
            if (rightFree && !leftFree) return rightHand;
            if (!leftFree && !rightFree) return GetLastEndTime(leftHand) <= GetLastEndTime(rightHand) ? leftHand : rightHand;

            if (lastAssignedNote != null && lastAssignedHand != null)
            {
                // ✅ 核心修复：测算距离时，用上一个物件的“结束坐标(EndPosition)”来测算！
                float distance = Vector2.Distance(GetNoteEndPosition(lastAssignedNote), note.Position);
                double timeDelta = note.StartTime - GetTaskEndTime(lastAssignedNote);

                if (timeDelta > 800) return note.Position.x < 256 ? leftHand : rightHand;

                // ✅ 终极堆叠锁定：只要物理距离极近 (< 40)，说明是滑条头尾堆叠或原地连打
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

        private bool IsHandFreeAt(AutoHand hand, double targetTime)
        {
            return GetLastEndTime(hand) <= targetTime;
        }

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

        private double GetTaskEndTime(HitObject task)
        {
            if (task is SliderObject s) return s.EndTime;
            if (task is SpinnerObject sp) return sp.EndTime;
            return task.StartTime;
        }
    }
}