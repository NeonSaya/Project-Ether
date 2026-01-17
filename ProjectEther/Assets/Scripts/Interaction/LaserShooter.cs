using UnityEngine;

namespace OsuVR
{
    /// <summary>
    /// 激光射击器：发射射线检测音符，实现osu! Relax模式的悬停判定
    /// </summary>
    public class LaserShooter : MonoBehaviour
    {
        public enum HandSide
        {
            Left,
            Right
        }
        [Header("手柄设置")]
 
        public HandSide handSide;

        [Header("射线设置")]
        public Transform rayOrigin; // 射线发射点
        public float maxDistance = 10f; // 射线距离
        public LayerMask noteLayer; // 只检测音符层

        [Header("视觉效果")]
        public LineRenderer laserLine;
        public Color laserColor = Color.cyan;
        public Color hitColor = Color.yellow;
        public float laserWidth = 0.02f;

        [Header("输入设置")]
        public bool useMouseForDebug = false; // 在编辑器中用鼠标模拟VR控制器

        // 缓存上次击中的音符，避免每帧重复查找
        private NoteController lastHoveredNote = null;

       
        void Start()
        {
            if (handSide == HandSide.Left)
            {
                laserColor=Color.cyan;
            }
            else
            {
                laserColor=Color.magenta;
            }
                // 初始化激光线
                InitializeLaserLine();
        }

        /// <summary>
        /// 初始化激光线
        /// </summary>
        private void InitializeLaserLine()
        {
            if (laserLine != null)
            {
                laserLine.startWidth = laserWidth;
                laserLine.endWidth = laserWidth;
                laserLine.startColor = laserColor;
                laserLine.endColor = laserColor;
                laserLine.positionCount = 2;
            }
        }

        void Update()
        {
            // 处理输入
            HandleInput();

            // 发射射线检测
            CastLaserRay();

            // 更新激光线视觉效果
            UpdateLaserVisual();
        }

        /// <summary>
        /// 处理输入
        /// </summary>
        private void HandleInput()
        {
            // 调试功能：在编辑器中用鼠标模拟
            if (useMouseForDebug && Application.isEditor)
            {
                if (Input.GetMouseButton(0)) // 左键
                {
                    // 从摄像机发射射线
                    Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                    RaycastHit hit;

                    if (Physics.Raycast(ray, out hit, maxDistance, noteLayer))
                    {
                        NoteController note = hit.collider.GetComponent<NoteController>();
                        if (note != null && note.isActive)
                        {
                            note.OnRayHover();
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 发射激光射线检测音符 (支持 HitCircle 和 Slider)
        /// </summary>
        private void CastLaserRay()
        {
            Vector3 direction = rayOrigin ? rayOrigin.forward : transform.forward;
            Vector3 origin = (rayOrigin ? rayOrigin.position : transform.position) + direction * 0.2f;
            RaycastHit hit;

            // 使用 SphereCast 增加判定宽容度 (光柱直径 15cm)
            float laserRadius = 0.15f;

            bool hitSomething = Physics.SphereCast(
                origin,
                laserRadius,
                direction,
                out hit,
                maxDistance,
                noteLayer
            );

            // 清除上一帧的悬停状态（如果需要的话，目前NoteController逻辑里没有强制退出逻辑，视具体实现而定）
            if (lastHoveredNote != null &&
                (!lastHoveredNote.isActive ||
                 (hitSomething && hit.collider.gameObject != lastHoveredNote.gameObject)))
            {
                lastHoveredNote = null;
            }

            if (hitSomething)
            {
                // 1. 尝试获取 HitCircle (NoteController)
                NoteController note = hit.collider.GetComponent<NoteController>();
                if (note != null && note.isActive && !note.hasBeenHit)
                {
                    note.OnRayHover();
                    lastHoveredNote = note;
                }

                // 2. 尝试获取 Slider (SliderController)
                // 射线可能打到 SliderBall (有 Collider) 或者 SliderTrack (有 MeshCollider)
                SliderController slider = hit.collider.GetComponent<SliderController>();

                // 如果打到的是球 (FollowBall 是 Slider 的子物体，SliderController 在父物体上)
                if (slider == null && hit.collider.transform.parent != null)
                {
                    slider = hit.collider.transform.parent.GetComponent<SliderController>();
                }

                if (slider != null && slider.isActiveAndEnabled)
                {
                    // (A) 告诉滑条：我正在照着你 (用于 Tracking 和 Tick 判定)
                    slider.OnRayStay();

                    // (B) 尝试击打滑条头 (如果是刚开始)
                    slider.TryHitHead();
                }

                SpinnerController spinner = hit.collider.GetComponent<SpinnerController>();
                if (spinner == null && hit.collider.transform.parent != null)
                {
                    //传递打击点给 Spinner
                    spinner = hit.collider.transform.parent.GetComponent<SpinnerController>();
                }

                if (spinner != null && spinner.IsActive)
                {
                    // 传递击中点和当前手柄 ID (Left/Right)
                    // SpinnerController 会根据 handSide 区分两只手的角度增量
                    spinner.OnRayStay(hit.point, this.handSide);
                }

                // 调试绘制
                Debug.DrawLine(origin, hit.point, Color.green);
            }
        }

        /// <summary>
        /// 更新激光线视觉效果
        /// </summary>
        private void UpdateLaserVisual()
        {
            if (laserLine == null) return;

            Vector3 origin = rayOrigin ? rayOrigin.position : transform.position;
            Vector3 direction = rayOrigin ? rayOrigin.forward : transform.forward;

            RaycastHit hit;
            Vector3 endPoint;
            bool isHittingInteractive = false;

            // 为了视觉准确性，这里用 Raycast 而不是 SphereCast，或者你可以保持一致
            if (Physics.Raycast(origin, direction, out hit, maxDistance, noteLayer))
            {
                endPoint = hit.point;

                // 检查是否击中了交互物体 (Note 或 Slider)
                if (hit.collider.GetComponent<NoteController>() != null ||
                    hit.collider.GetComponent<SliderController>() != null ||
                    (hit.collider.transform.parent != null && hit.collider.transform.parent.GetComponent<SliderController>() != null))
                {
                    isHittingInteractive = true;
                }
            }
            else
            {
                endPoint = origin + direction * maxDistance;
            }

            // 更新激光线位置
            laserLine.SetPosition(0, origin);
            laserLine.SetPosition(1, endPoint);

            // 击中物体时变色
            Color targetColor = isHittingInteractive ? hitColor : laserColor;
            laserLine.startColor = targetColor;
            laserLine.endColor = targetColor;

            // 脉冲效果
            float pulse = Mathf.Sin(Time.time * 10f) * 0.1f + 0.9f;
            laserLine.startWidth = laserWidth * pulse;
            laserLine.endWidth = laserWidth * pulse * 0.5f;
        }

        /// <summary>
        /// 获取当前悬停的音符
        /// </summary>
        public NoteController GetCurrentHoveredNote()
        {
            return lastHoveredNote;
        }

        /// <summary>
        /// 获取激光的方向（用于调试或其他用途）
        /// </summary>
        public Vector3 GetLaserDirection()
        {
            return rayOrigin ? rayOrigin.forward : transform.forward;
        }

        /// <summary>
        /// 获取激光的起点（用于调试或其他用途）
        /// </summary>
        public Vector3 GetLaserOrigin()
        {
            return rayOrigin ? rayOrigin.position : transform.position;
        }

        /// <summary>
        /// 在编辑器中绘制Gizmos
        /// </summary>
        void OnDrawGizmos()
        {
            if (!Application.isPlaying) return;

            Vector3 origin = rayOrigin ? rayOrigin.position : transform.position;
            Vector3 direction = rayOrigin ? rayOrigin.forward : transform.forward;

            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(origin, origin + direction * maxDistance);

            // 绘制激光锥形范围（表示检测区域）
            Gizmos.color = new Color(0, 1, 1, 0.1f);
            float coneAngle = 5f; // 锥形角度
            float coneLength = maxDistance;

            Vector3 right = Quaternion.AngleAxis(coneAngle, transform.up) * direction;
            Vector3 left = Quaternion.AngleAxis(-coneAngle, transform.up) * direction;
            Vector3 up = Quaternion.AngleAxis(coneAngle, transform.right) * direction;
            Vector3 down = Quaternion.AngleAxis(-coneAngle, transform.right) * direction;

            Gizmos.DrawLine(origin, origin + right * coneLength);
            Gizmos.DrawLine(origin, origin + left * coneLength);
            Gizmos.DrawLine(origin, origin + up * coneLength);
            Gizmos.DrawLine(origin, origin + down * coneLength);
        }
    }
}