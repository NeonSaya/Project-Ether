using UnityEngine;
using UnityEngine.Pool;

namespace OsuVR
{
    public class NotePoolManager : MonoBehaviour
    {
        public static NotePoolManager Instance;

        [Header("核心预制体")]
        public GameObject hitCirclePrefab;
        public GameObject sliderPrefab;
        public GameObject spinnerPrefab;

        [Header("可选预制体")]
        [Tooltip("如果没有分配，会自动生成一个白色小球")]
        public GameObject sliderTickPrefab;

        // 对象池接口
        public IObjectPool<GameObject> CirclePool { get; private set; }
        public IObjectPool<GameObject> SliderPool { get; private set; }
        public IObjectPool<GameObject> SpinnerPool { get; private set; }
        public IObjectPool<GameObject> TickPool { get; private set; }

        void Awake()
        {
            Instance = this;

            // 0. 兜底逻辑：如果没有 Tick 预制体，现场捏一个
            if (sliderTickPrefab == null)
            {
                sliderTickPrefab = CreateDefaultTickPrefab();
            }

            InitializePools();
        }

        private GameObject CreateDefaultTickPrefab()
        {
            // 创建一个简单的黄色小球作为 Tick
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "DefaultTick";
            go.transform.localScale = Vector3.one * 0.1f;

            // 移除碰撞体（Tick 纯视觉判定靠代码）
            DestroyImmediate(go.GetComponent<Collider>());

            // 给个简单的材质
            var renderer = go.GetComponent<Renderer>();
            renderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit")); // 或者 Standard
            renderer.material.color = Color.yellow;

            // 设为非活跃并作为 Prefab 模板
            go.SetActive(false);
            go.transform.SetParent(transform);
            return go;
        }

        private void InitializePools()
        {
            // 1. Circle 池：Aspire 连打极多，容量大
            CirclePool = new ObjectPool<GameObject>(
                createFunc: () => Instantiate(hitCirclePrefab, transform),
                actionOnGet: (obj) => {
                    obj.SetActive(true);
                    // 确保状态重置由 Controller 自身处理
                },
                actionOnRelease: (obj) => {
                    obj.SetActive(false);
                    obj.transform.SetParent(transform); // 回家
                },
                actionOnDestroy: (obj) => Destroy(obj),
                collectionCheck: false, // 🚨 极限性能模式：关闭重复检查
                defaultCapacity: 200,
                maxSize: 1000
            );

            // 2. Slider 池：滑条体Mesh重，尽量复用
            SliderPool = new ObjectPool<GameObject>(
                createFunc: () => Instantiate(sliderPrefab, transform),
                actionOnGet: (obj) => obj.SetActive(true),
                actionOnRelease: (obj) => {
                    obj.SetActive(false);
                    obj.transform.SetParent(transform);
                },
                actionOnDestroy: (obj) => Destroy(obj),
                collectionCheck: false,
                defaultCapacity: 50,
                maxSize: 200
            );

            // 3. Spinner 池
            SpinnerPool = new ObjectPool<GameObject>(
                createFunc: () => Instantiate(spinnerPrefab, transform),
                actionOnGet: (obj) => obj.SetActive(true),
                actionOnRelease: (obj) => {
                    obj.SetActive(false);
                    obj.transform.SetParent(transform);
                },
                actionOnDestroy: (obj) => Destroy(obj),
                collectionCheck: false,
                defaultCapacity: 5,
                maxSize: 20
            );

            // 4. Tick 池：应对每秒 30+ 个 Tick 的情况
            TickPool = new ObjectPool<GameObject>(
                createFunc: () => Instantiate(sliderTickPrefab, transform),
                actionOnGet: (obj) => obj.SetActive(true),
                actionOnRelease: (obj) => {
                    obj.SetActive(false);
                    // 注意：Tick 回收时通常不需要 SetParent，因为 Get 时会马上被 SetParent 到 Slider 下
                },
                actionOnDestroy: (obj) => Destroy(obj),
                collectionCheck: false,
                defaultCapacity: 200,
                maxSize: 2000 // Aspire 可能会有海量 Tick
            );
        }
    }
}