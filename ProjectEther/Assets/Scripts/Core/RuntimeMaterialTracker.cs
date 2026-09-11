using System.Collections.Generic;
using UnityEngine;

namespace OsuVR
{
    /// <summary>
    /// 运行时材质追踪器：挂在由代码动态生成视觉的 GameObject 上，
    /// 统一登记创建期产生的材质实例，对象销毁时一并 Destroy，防止显存泄漏。
    ///
    /// 背景：HitObjectFactory 多处按实例 new Material（Tick/FollowBall/Body/Solid/Approach），
    /// 材质是资源对象，不随 GameObject 销毁而释放；池上限下最坏可遗留数万孤儿材质。
    /// 已有追踪的路径（NoteController._clonedMaterials、SliderController.clonedMaterials）不受影响，
    /// 重复 Destroy 同一材质在 Unity 中是安全的（假 null 检查）。
    /// </summary>
    public class RuntimeMaterialTracker : MonoBehaviour
    {
        private readonly List<Material> _materials = new List<Material>();

        /// <summary>登记一个由工厂创建、归本对象所有的材质实例</summary>
        public void Track(Material mat)
        {
            if (mat != null) _materials.Add(mat);
        }

        /// <summary>获取或添加追踪器组件</summary>
        public static RuntimeMaterialTracker GetOrAdd(GameObject go)
        {
            var tracker = go.GetComponent<RuntimeMaterialTracker>();
            if (tracker == null) tracker = go.AddComponent<RuntimeMaterialTracker>();
            return tracker;
        }

        void OnDestroy()
        {
            for (int i = 0; i < _materials.Count; i++)
            {
                if (_materials[i] != null) Destroy(_materials[i]);
            }
            _materials.Clear();
        }
    }
}
