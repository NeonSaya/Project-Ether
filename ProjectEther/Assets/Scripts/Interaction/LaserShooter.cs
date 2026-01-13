using UnityEngine;

namespace OsuVR
{
    /// <summary>
    /// 激光射击器：发射射线检测音符，实现osu! Relax模式的悬停判定
    /// </summary>
    public class LaserShooter : MonoBehaviour
    {
        [Header("射线设置")]
        public Transform rayOrigin; // 射线发射点
        public float maxDistance = 10f; // 射线距离
        public LayerMask noteLayer; // 只检测音符层

        [Header("视觉效果")]
        public LineRenderer laserLine; // 激光线渲染器（可选）
        public Color laserColor = Color.cyan;
        public float laserWidth = 0.02f;

        [Header("输入设置")]
        public bool useMouseForDebug = false; // 在编辑器中用鼠标模拟VR控制器

        // 缓存上次击中的音符，避免每帧重复查找
        private NoteController lastHoveredNote = null;

        void Start()
        {
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
        /// 发射激光射线检测音符 (优化版：使用 SphereCast 粗光柱)
        /// </summary>
        private void CastLaserRay()
        {
           
            Vector3 direction = rayOrigin ? rayOrigin.forward : transform.forward;
            Vector3 origin = (rayOrigin ? rayOrigin.position : transform.position) + direction * 0.2f;
            RaycastHit hit;

            // --- 核心修改：使用 SphereCast 代替 Raycast ---
            // radius: 0.15f 表示激光有 15厘米 粗 (像根柱子一样捅过去)
            // 这样手抖也能打中，手感会好很多
            float laserRadius = 0.15f;

            bool hitSomething = Physics.SphereCast(
                origin,
                laserRadius,
                direction,
                out hit,
                maxDistance,
                noteLayer
            );

            if (hitSomething)
            {
                // 让控制台告诉我们，它到底打到了谁
                Debug.Log($"激光打到了: {hit.collider.name} (层级: {LayerMask.LayerToName(hit.collider.gameObject.layer)})");
            }

            // 后面的逻辑保持不变
            if (lastHoveredNote != null &&
                (lastHoveredNote.hasBeenHit ||
                 !lastHoveredNote.isActive ||
                 (hitSomething && hit.collider.gameObject != lastHoveredNote.gameObject)))
            {
                lastHoveredNote = null;
            }

            if (hitSomething)
            {
                NoteController note = hit.collider.GetComponent<NoteController>();

                if (note != null && note.isActive && !note.hasBeenHit)
                {
                    note.OnRayHover();
                    lastHoveredNote = note;

                    // 调试线看起来还是细的，但实际判定是粗的
                    Debug.DrawLine(origin, hit.point, Color.green);
                }
            }
            else
            {
                //Debug.DrawRay(origin, direction * maxDistance, Color.red);
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

            // 发射射线检测终点
            RaycastHit hit;
            Vector3 endPoint;

            if (Physics.Raycast(origin, direction, out hit, maxDistance, noteLayer))
            {
                endPoint = hit.point;

                // 根据击中音符的状态改变激光颜色
                NoteController note = hit.collider.GetComponent<NoteController>();
                if (note != null)
                {
                    if (note.isHovered)
                    {
                        laserLine.startColor = Color.yellow;
                        laserLine.endColor = Color.yellow;
                    }
                    else
                    {
                        laserLine.startColor = laserColor;
                        laserLine.endColor = laserColor;
                    }
                }
            }
            else
            {
                endPoint = origin + direction * maxDistance;
                laserLine.startColor = laserColor;
                laserLine.endColor = laserColor;
            }

            // 更新激光线位置
            laserLine.SetPosition(0, origin);
            laserLine.SetPosition(1, endPoint);

            // 添加一些视觉效果（可选）
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