using UnityEngine;
using OsuVR; // 引用你的命名空间

public class LaserShooter : MonoBehaviour
{
    public Transform rayOrigin; // 射线发射点
    public float maxDistance = 100f; // 射线距离
    public LayerMask noteLayer; // 只检测音符层，优化性能

    void Update()
    {
        // 1. 如果没有指定发射点，就用自己的位置
        Vector3 origin = rayOrigin ? rayOrigin.position : transform.position;
        Vector3 direction = rayOrigin ? rayOrigin.forward : transform.forward;

        // 2. 发射物理射线 (Raycast)
        RaycastHit hit;
        // out hit 表示如果打中了，把信息存在 hit 变量里
        if (Physics.Raycast(origin, direction, out hit, maxDistance))
        {
            // 3. 检查打中的东西是不是音符
            // 尝试获取由于碰撞体上的 NoteController 组件
            NoteController note = hit.collider.GetComponent<NoteController>();

            if (note != null && note.isActive)
            {
                // 4. 触发击中逻辑
                // 这里加个判定：只有当音符飞得比较近了才算（可选，防止在 100米外就打掉了）
                // 目前先直接调用 OnHit
                note.OnHit();
            }
        }
    }
}