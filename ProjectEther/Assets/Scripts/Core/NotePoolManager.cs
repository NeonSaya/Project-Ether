using UnityEngine;
using UnityEngine.Pool;

namespace OsuVR
{
    /// <summary>
    /// 音符对象池管理器：统一管理所有音符类型的对象池
    /// 核心功能：
    /// - 零 GC 运行时：所有对象预分配，避免 Instantiate/Destroy
    /// - 双模式支持：预制体模式 / 纯代码生成模式
    /// - 统一材质管理：支持全局材质覆盖
    /// </summary>
    public class NotePoolManager : MonoBehaviour
    {
        public static NotePoolManager Instance;

        [Header("材质配置 (可选)")]
        [Tooltip("Note主体材质，不填则使用默认材质")]
        public Material bodyMaterial;

        [Tooltip("覆盖层材质")]
        public Material overlayMaterial;

        [Tooltip("缩圈材质")]
        public Material approachMaterial;

        [Tooltip("光晕材质")]
        public Material glowMaterial;

        [Header("滑条材质")]
        public Material sliderMaterial;

        [Header("预制体 (可选 - 留空则纯代码生成)")]
        [Tooltip("留空则使用工厂类纯代码生成")]
        public GameObject hitCirclePrefab;

        [Tooltip("留空则使用工厂类纯代码生成")]
        public GameObject sliderPrefab;

        [Tooltip("留空则使用工厂类纯代码生成")]
        public GameObject spinnerPrefab;

        [Tooltip("留空则使用工厂类纯代码生成")]
        public GameObject sliderTickPrefab;

        [Tooltip("留空则使用工厂类纯代码生成")]
        public GameObject followBallPrefab;

        [Header("生成模式")]
        [Tooltip("勾选后强制使用纯代码生成，忽略预制体")]
        public bool forceProceduralGeneration = true;

        public IObjectPool<GameObject> CirclePool { get; private set; }
        public IObjectPool<GameObject> SliderPool { get; private set; }
        public IObjectPool<GameObject> SpinnerPool { get; private set; }
        public IObjectPool<GameObject> TickPool { get; private set; }
        public IObjectPool<GameObject> FollowBallPool { get; private set; }
        public IObjectPool<GameObject> SliderHeadPool { get; private set; }

        private bool useProceduralGeneration;

        void Awake()
        {
            Instance = this;

            useProceduralGeneration = forceProceduralGeneration ||
                (hitCirclePrefab == null && sliderPrefab == null);

            if (useProceduralGeneration)
            {
                HitObjectFactory.Initialize(bodyMaterial, overlayMaterial, approachMaterial, glowMaterial);
                Debug.Log("[NotePoolManager] 使用纯代码生成模式");
            }
            else
            {
                Debug.Log("[NotePoolManager] 使用预制体模式");
            }

            InitializePools();
        }

        private void InitializePools()
        {
            CirclePool = new ObjectPool<GameObject>(
                createFunc: () => CreateHitCircle(),
                actionOnGet: (obj) =>
                {
                    if (obj != null) obj.SetActive(true);
                },
                actionOnRelease: (obj) =>
                {
                    if (obj != null)
                    {
                        obj.SetActive(false);
                        obj.transform.SetParent(transform);
                    }
                },
                actionOnDestroy: (obj) => Destroy(obj),
                collectionCheck: false,
                defaultCapacity: 500,
                maxSize: 2000
            );

            SliderPool = new ObjectPool<GameObject>(
                createFunc: () => CreateSlider(),
                actionOnGet: (obj) =>
                {
                    if (obj != null) obj.SetActive(true);
                },
                actionOnRelease: (obj) =>
                {
                    if (obj != null)
                    {
                        obj.SetActive(false);
                        obj.transform.SetParent(transform);
                    }
                },
                actionOnDestroy: (obj) => Destroy(obj),
                collectionCheck: false,
                defaultCapacity: 500,
                maxSize: 2000
            );

            SpinnerPool = new ObjectPool<GameObject>(
                createFunc: () => CreateSpinner(),
                actionOnGet: (obj) =>
                {
                    if (obj != null) obj.SetActive(true);
                },
                actionOnRelease: (obj) =>
                {
                    if (obj != null)
                    {
                        obj.SetActive(false);
                        obj.transform.SetParent(transform);
                    }
                },
                actionOnDestroy: (obj) => Destroy(obj),
                collectionCheck: false,
                defaultCapacity: 10,
                maxSize: 100
            );

            TickPool = new ObjectPool<GameObject>(
                createFunc: () => CreateSliderTick(),
                actionOnGet: (obj) =>
                {
                    if (obj != null) obj.SetActive(true);
                },
                actionOnRelease: (obj) =>
                {
                    if (obj != null)
                    {
                        obj.SetActive(false);
                        obj.transform.SetParent(transform);
                    }
                },
                actionOnDestroy: (obj) => Destroy(obj),
                collectionCheck: false,
                defaultCapacity: 10000,
                maxSize: 100000
            );

            FollowBallPool = new ObjectPool<GameObject>(
                createFunc: () => CreateFollowBall(),
                actionOnGet: (obj) =>
                {
                    if (obj != null) obj.SetActive(true);
                },
                actionOnRelease: (obj) =>
                {
                    if (obj != null)
                    {
                        obj.SetActive(false);
                        obj.transform.SetParent(transform);
                    }
                },
                actionOnDestroy: (obj) => Destroy(obj),
                collectionCheck: false,
                defaultCapacity: 100,
                maxSize: 500
            );

            SliderHeadPool = new ObjectPool<GameObject>(
                createFunc: () => CreateSliderHead(),
                actionOnGet: (obj) =>
                {
                    if (obj != null) obj.SetActive(true);
                },
                actionOnRelease: (obj) =>
                {
                    if (obj != null)
                    {
                        obj.SetActive(false);
                        obj.transform.SetParent(transform);
                    }
                },
                actionOnDestroy: (obj) => Destroy(obj),
                collectionCheck: false,
                defaultCapacity: 200,
                maxSize: 1000
            );
        }

        private GameObject CreateHitCircle()
        {
            if (useProceduralGeneration)
            {
                return HitObjectFactory.CreateHitCircle();
            }
            else if (hitCirclePrefab != null)
            {
                return Instantiate(hitCirclePrefab, transform);
            }
            else
            {
                return HitObjectFactory.CreateHitCircle();
            }
        }

        private GameObject CreateSlider()
        {
            if (useProceduralGeneration || sliderPrefab == null)
            {
                GameObject slider = new GameObject("Slider_Procedural");

                slider.AddComponent<MeshFilter>();
                slider.AddComponent<MeshRenderer>();
                slider.AddComponent<MeshCollider>();
                slider.AddComponent<SliderController>();

                if (sliderMaterial != null)
                {
                    slider.GetComponent<MeshRenderer>().material = sliderMaterial;
                }

                slider.layer = 6;
                return slider;
            }
            else
            {
                return Instantiate(sliderPrefab, transform);
            }
        }

        private GameObject CreateSpinner()
        {
            // 转盘始终使用预制体
            if (spinnerPrefab != null)
            {
                return Instantiate(spinnerPrefab, transform);
            }
            else
            {
                Debug.LogWarning("[NotePoolManager] Spinner prefab is not assigned!");
                GameObject spinner = new GameObject("Spinner_Procedural");
                spinner.AddComponent<SphereCollider>();
                spinner.AddComponent<SpinnerController>();
                spinner.layer = 6;
                return spinner;
            }
        }

        private GameObject CreateSliderTick()
        {
            if (useProceduralGeneration || sliderTickPrefab == null)
            {
                return HitObjectFactory.CreateSliderTick();
            }
            else
            {
                return Instantiate(sliderTickPrefab, transform);
            }
        }

        private GameObject CreateFollowBall()
        {
            if (useProceduralGeneration || followBallPrefab == null)
            {
                return HitObjectFactory.CreateFollowBall();
            }
            else
            {
                return Instantiate(followBallPrefab, transform);
            }
        }

        private GameObject CreateSliderHead()
        {
            if (useProceduralGeneration)
            {
                return HitObjectFactory.CreateSliderHead();
            }
            else
            {
                return HitObjectFactory.CreateSliderHead();
            }
        }

        public GameObject GetSliderHead()
        {
            return SliderHeadPool.Get();
        }

        public void ReleaseSliderHead(GameObject head)
        {
            if (head != null)
            {
                SliderHeadPool.Release(head);
            }
        }

        public GameObject GetFollowBall()
        {
            return FollowBallPool.Get();
        }

        public void ReleaseFollowBall(GameObject ball)
        {
            if (ball != null)
            {
                FollowBallPool.Release(ball);
            }
        }

        public GameObject GetTick()
        {
            return TickPool.Get();
        }

        public void ReleaseTick(GameObject tick)
        {
            if (tick != null)
            {
                TickPool.Release(tick);
            }
        }

        void OnDestroy()
        {
            CirclePool?.Clear();
            SliderPool?.Clear();
            SpinnerPool?.Clear();
            TickPool?.Clear();
            FollowBallPool?.Clear();
            SliderHeadPool?.Clear();

            if (useProceduralGeneration)
            {
                HitObjectFactory.Cleanup();
            }
        }
    }
}
