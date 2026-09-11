using UnityEngine;

namespace OsuVR
{
    /// <summary>
    /// 物理工具：为移动碰撞体补上 kinematic Rigidbody。
    ///
    /// 背景：音符/滑条/转盘的碰撞体原本是「无 Rigidbody 的静态碰撞体」，
    /// 但它们每帧随 transform 移动 —— PhysX 会按「移动的静态几何」处理，
    /// 每帧在 broadphase 中移除并重新插入，造成隐性 CPU 开销。
    /// 挂上 kinematic Rigidbody 后按运动学刚体处理，broadphase 只更新不重建。
    /// kinematic 不受力、不改 transform 驱动方式；SphereCast 等物理查询行为不变。
    /// </summary>
    public static class PhysicsUtil
    {
        public static Rigidbody EnsureKinematicRigidbody(GameObject go)
        {
            var rb = go.GetComponent<Rigidbody>();
            if (rb == null) rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.None;
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
            return rb;
        }
    }
}
