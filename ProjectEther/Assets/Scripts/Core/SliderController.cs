using System.Collections.Generic;
using UnityEngine;

namespace OsuVR
{
    /// <summary>
    /// 滑条控制器（优化版）：支持折返、高性能路径查找、无GC材质修改
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    
    public class SliderController : MonoBehaviour
    {
        [Header("滑条数据")]
        public SliderObject sliderData;

        [Header("视觉设置")] // 修改原来的 header
        [Tooltip("滑条本体宽度")]
        public float sliderWidth = 0.05f;

        [Tooltip("滑条边框宽度 (必须比本体宽)")] // [新增]
        public float borderWidth = 0.06f;

        [Tooltip("滑条高度（轻微凸起效果）")]
        public float sliderHeight = 0.01f;

        [Tooltip("滑条本体材质")]
        public Material bodyMaterial;   // 确认保留这个

        [Tooltip("滑条边框材质")]
        public Material borderMaterial; // 确认保留这个

        [Tooltip("纹理平铺密度（每单位长度重复多少次纹理）")]
        public float textureTiling = 1.0f;

        [Header("跟踪球设置")]
        [Tooltip("跟踪球预制体")]
        public GameObject followBallPrefab;

        [Header("渐变效果")]
        [Tooltip("渐隐时间（秒）")]
        public float fadeOutDuration = 0.5f;

        [Tooltip("是否启用渐隐效果")]
        public bool enableFadeOut = true;

        [Header("osu! 风格组件")]
        public GameObject sliderHeadPrefab;    // 拖入 VisualHitCircle Prefab
        public GameObject reverseArrowPrefab;  // 拖入 ReverseArrow Prefab

        private GameObject headInstance;       // 实例化的头
        private GameObject arrowInstance;      // 实例化的箭头

        // 私有组件引用
        private RhythmGameManager gameManager;
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh bodyMesh;
        private MeshCollider meshCollider;
        private GameObject followBall;
        private Renderer followBallRenderer; // 缓存球体渲染器
        private GameObject borderObject;
        private MeshFilter borderMeshFilter;
        private MeshRenderer borderMeshRenderer;
        private Mesh borderMesh;

        // [新增] 嵌套物件判定索引
        private int currentNestedIndex = 0;
        // [新增] 记录获得了多少个 Tick (用于计算最终分数)
        private int ticksGot = 0;

        // [新增] 判定相关变量
        private SphereCollider ballCollider; // 用于射线的碰撞体
        private bool isTracking = false;     // 当前帧是否被射线照射
        private bool hasStarted = false;     // 滑条是否已经开始
        private bool headHit = false;        // 滑条头是否被击中
        private bool finished = false;       // 滑条是否结束

        // [新增] 避免每帧重复获取时间的缓存
        private double currentMusicTimeCache;

        // 性能优化：材质属性块（防止材质泄露）
        private MaterialPropertyBlock _propBlock;
        private static readonly int ColorPropertyId = Shader.PropertyToID("_Color"); // 缓存Shader属性ID

        // 路径数据（优化：增加累计长度缓存）
        private List<Vector3> worldPathPoints = new List<Vector3>();
        private List<float> cumulativeLengths = new List<float>(); // 路径节点的累计长度
        private float totalPathLength = 0f;

        // 状态变量
        private bool isInitialized = false;
        private bool isActive = true;
        private bool isFadingOut = false;
        private float fadeOutStartTime = 0f;
        private float currentAlpha = 1f;
        private double lastUpdateTime = 0;

        /// <summary>
        /// 初始化滑条控制器
        /// </summary>
        public void Initialize(SliderObject sliderData, float width, RhythmGameManager manager)
        {
            ResetState();

            if (sliderData == null || manager == null) return;

            this.sliderData = sliderData;
            this.sliderWidth = width;
            // 自动计算边框宽度 (例如比本体宽 15%)
            this.borderWidth = width * 1.25f;
            this.gameManager = manager;

            // -----------------------------------------------------------------
            // [修复] 暴力修复时间戳：遍历所有子物件，防止混合了绝对/相对时间
            // -----------------------------------------------------------------
            if (this.sliderData.NestedHitObjects != null)
            {
                foreach (var nested in this.sliderData.NestedHitObjects)
                {
                   
                    if (nested.Time < this.sliderData.StartTime)
                    {
                        // 累加 StartTime，将其变为绝对时间
                        nested.Time += this.sliderData.StartTime;
                    }
                }
            }

            // 1. 初始化本体组件 (原有的 MeshFilter/Renderer)
            if (!meshFilter) meshFilter = GetComponent<MeshFilter>();
            if (!meshRenderer) meshRenderer = GetComponent<MeshRenderer>();

            // [修改] 初始化边框组件
            if (borderObject == null)
            {
                borderObject = new GameObject("SliderBorder");
                borderObject.transform.SetParent(transform, false);

                borderObject.transform.localPosition = Vector3.forward * 0.01f;

                borderMeshFilter = borderObject.AddComponent<MeshFilter>();
                borderMeshRenderer = borderObject.AddComponent<MeshRenderer>();
            }
            borderObject.SetActive(true);

            // 3. [新增] 设置材质
            // Inspector 里 bodyMaterial 拖滑条颜色，borderMaterial 拖纯白材质
            if (bodyMaterial != null) meshRenderer.sharedMaterial = bodyMaterial;
            if (borderMaterial != null) borderMeshRenderer.sharedMaterial = borderMaterial;

            if (_propBlock == null) _propBlock = new MaterialPropertyBlock();

            // 4. 生成路径
            GenerateSliderPath();

            // 5. [核心修改] 生成双层网格 (本体 + 边框)
            GenerateMeshes();

            // 6. 创建跟踪球与碰撞体
            CreateFollowBall();

            // 7. 创建视觉元素 (头、箭头)
            CreateVisuals();

            // 重置计数器
            currentNestedIndex = 0;
            ticksGot = 0;
            isTracking = false;
            headHit = false;
            finished = false;
            currentAlpha = 1f;

            if (followBall) followBall.SetActive(false);

            UpdateMaterialAlpha();

            isInitialized = true;
            isActive = true;
        }

        private void ResetState()
        {
            worldPathPoints.Clear();
            cumulativeLengths.Clear();
            totalPathLength = 0f;
            isFadingOut = false;
            isActive = false;
            currentAlpha = 1f;

            // 清理所有 Mesh
            if (bodyMesh != null) bodyMesh.Clear();
            if (borderMesh != null) borderMesh.Clear();
        }

        /// <summary>
        /// 生成滑条路径并计算累计长度（用于二分查找）
        /// </summary>
        private void GenerateSliderPath()
        {

            PopulateWorldPointsFromData();

            // 核心优化：预计算累计长度
            cumulativeLengths.Clear();
            cumulativeLengths.Add(0f);
            totalPathLength = 0f;

            for (int i = 0; i < worldPathPoints.Count - 1; i++)
            {
                float dist = Vector3.Distance(worldPathPoints[i], worldPathPoints[i + 1]);
                totalPathLength += dist;
                cumulativeLengths.Add(totalPathLength);
            }
        }

        /// <summary>
        /// 将osu!的数据转换为本地坐标路径点 (修复脱位问题)
        /// </summary>
        private void PopulateWorldPointsFromData()
        {
            // 清理旧数据
            worldPathPoints.Clear();

            // 1. 获取 osu! 的绝对坐标路径点
            List<Vector2> osuPoints = sliderData.GetSliderPath();

            // 2. 计算滑条起点的世界坐标 (作为锚点)
            // 这一步非常重要：我们需要计算相对于这个起点的偏移量
            Vector3 startWorldPos = CoordinateMapper.MapToWorld(sliderData.Position);

            foreach (Vector2 p in osuPoints)
            {
                // 计算当前点的世界坐标
                Vector3 currentWorldPos = CoordinateMapper.MapToWorld(p);

                // 核心修复：转换为本地坐标
                // 本地坐标 = 当前世界坐标 - 起点世界坐标
                // 这样 Mesh 就会从 (0,0,0) 开始绘制，而不是从世界原点叠加
                Vector3 localPos = currentWorldPos - startWorldPos;

                worldPathPoints.Add(localPos);
            }

            // 3. 安全检查
            if (worldPathPoints.Count < 2)
            {
                // 如果路径点不足，手动添加一个本地的终点 (例如向右延伸 1 米)
                worldPathPoints.Add(Vector3.zero);
                worldPathPoints.Add(Vector3.right * 1.0f);
            }
        }
        /// <summary>
        /// [新增] 生成双层网格：一层本体，一层边框
        /// </summary>
        private void GenerateMeshes()
        {
            if (worldPathPoints.Count < 2) return;

            // 1. 生成本体 (Body) - 较窄
            if (bodyMesh == null) bodyMesh = new Mesh { name = "BodyMesh" };
            GenerateStrip(bodyMesh, sliderWidth);
            meshFilter.mesh = bodyMesh;

            // 2. 生成边框 (Border) - 较宽
            if (borderMesh == null) borderMesh = new Mesh { name = "BorderMesh" };
            GenerateStrip(borderMesh, borderWidth);
            borderMeshFilter.mesh = borderMesh;

            // 3. 更新碰撞体 (使用本体的 Mesh 即可)
            if (meshCollider != null)
            {
                meshCollider.sharedMesh = null;
                meshCollider.sharedMesh = bodyMesh;
            }
        }

        /// <summary>
        /// [新增] 通用条带生成算法
        /// </summary>
        private void GenerateStrip(Mesh targetMesh, float width)
        {
            targetMesh.Clear();
            List<Vector3> vertices = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> triangles = new List<int>();

            for (int i = 0; i < worldPathPoints.Count; i++)
            {
                Vector3 current = worldPathPoints[i];
                Vector3 dir = Vector3.forward;

                if (i < worldPathPoints.Count - 1)
                    dir = (worldPathPoints[i + 1] - current).normalized;
                else if (i > 0)
                    dir = (current - worldPathPoints[i - 1]).normalized;

                // 计算右向量 (垂直于路径方向和Z轴)
                Vector3 right = Vector3.Cross(dir, Vector3.forward).normalized;
                if (right.sqrMagnitude < 0.001f) right = Vector3.right;

                // 左右顶点
                vertices.Add(current - right * (width * 0.5f));
                vertices.Add(current + right * (width * 0.5f));

                // UV 映射
                float u = cumulativeLengths[i] * textureTiling;
                uvs.Add(new Vector2(u, 0));
                uvs.Add(new Vector2(u, 1));
            }

            for (int i = 0; i < worldPathPoints.Count - 1; i++)
            {
                int baseIdx = i * 2;
                triangles.Add(baseIdx);
                triangles.Add(baseIdx + 1);
                triangles.Add(baseIdx + 2);
                triangles.Add(baseIdx + 1);
                triangles.Add(baseIdx + 3);
                triangles.Add(baseIdx + 2);
            }

            targetMesh.SetVertices(vertices);
            targetMesh.SetUVs(0, uvs);
            targetMesh.SetTriangles(triangles, 0);
            targetMesh.RecalculateNormals();
        }

        /// <summary>
        /// [新增] 创建 osu! 风格的视觉元素 (头和箭头)
        /// </summary>
        private void CreateVisuals()
        {
            // 1. 创建滑条头 (Slider Head)
            if (sliderHeadPrefab != null)
            {
                if (headInstance != null) Destroy(headInstance);

                headInstance = Instantiate(sliderHeadPrefab, transform);
                headInstance.transform.localPosition = Vector3.zero;
                headInstance.transform.localPosition -= Vector3.forward * 0.005f;
                headInstance.SetActive(true);

                var scaler = headInstance.GetComponent<ApproachCircleScaler>();
                if (scaler != null)
                {
                    // 计算 AR 时间 (TimePreempt)
                    // 如果你还没有 Difficulty 数据，暂时先写死 1200ms
                    // 正确做法是从 beatmap.Difficulty 计算 AR
                    double arMs = 1200;
                    if (gameManager != null)
                    {
                        // 以后你可以从 gameManager 获取当前谱面的 AR
                    }

                    // 启动缩圈
                    scaler.Initialize(sliderData.StartTime, arMs);
                }
            }

            // 2. 创建折返箭头 (Reverse Arrow)
            // 只有当重复次数 > 1 时才需要箭头
            if (reverseArrowPrefab != null && sliderData.RepeatCount > 1)
            {
                if (arrowInstance != null) Destroy(arrowInstance);

                arrowInstance = Instantiate(reverseArrowPrefab, transform);
                UpdateArrowTransform(0); // 初始化箭头位置 (第0跨度)
                arrowInstance.SetActive(true);
            }
        }


        /// <summary>
        /// 更新箭头的位置和旋转
        /// spanIndex: 当前是第几段滑行
        /// </summary>
        private void UpdateArrowTransform(int spanIndex)
        {
            if (arrowInstance == null || worldPathPoints.Count < 2) return;

            // 逻辑：
            // 如果是偶数跨度 (0: A->B)，箭头应该在 B 端，指向 A。
            // 如果是奇数跨度 (1: B->A)，箭头应该在 A 端，指向 B。

            // 还有几次折返？
            int remainingSpans = sliderData.RepeatCount - spanIndex;

            // 如果只剩最后一次滑行，不需要箭头了 (直接滑向终点)
            if (remainingSpans <= 1)
            {
                arrowInstance.SetActive(false);
                return;
            }

            arrowInstance.SetActive(true);

            Vector3 position;
            Vector3 direction;

            if (spanIndex % 2 == 0)
            {
                // 当前 A->B，箭头在 B (末端)，指向 A (内部)
                position = worldPathPoints[worldPathPoints.Count - 1];

                // 计算末端切线：最后一个点 - 倒数第二个点
                Vector3 tangent = worldPathPoints[worldPathPoints.Count - 1] - worldPathPoints[worldPathPoints.Count - 2];

                // 箭头应该指向“回来”的方向，所以是切线的反方向
                direction = -tangent.normalized;
            }
            else
            {
                // 当前 B->A，箭头在 A (起点)，指向 B (内部)
                position = worldPathPoints[0];

                // 计算起点切线：第二个点 - 第一个点
                Vector3 tangent = worldPathPoints[1] - worldPathPoints[0];

                // 箭头应该指向“回来”的方向 (即指向 B)，那就是切线方向
                direction = tangent.normalized;
            }

            arrowInstance.transform.localPosition = position - Vector3.forward * 0.006f; // 防穿模

            // 设置旋转：让箭头的 "Up" (或 "Right") 对齐 direction
            // 假设你的箭头图片是向上的，我们需要让它的 Up 轴指向 direction，同时保持 Forward 轴指向相机 (Vector3.back)
            // 这样 VR 里看才是平面的
            arrowInstance.transform.rotation = Quaternion.LookRotation(Vector3.back, direction);

            // 如果你的箭头贴图是向右的，可能需要额外的旋转，例如：
            // arrowInstance.transform.Rotate(0, 0, -90);
        }


        private void CreateFollowBall()
        {
            if (followBall == null)
            {
                if (followBallPrefab != null)
                    followBall = Instantiate(followBallPrefab, transform);
                else
                {
                    followBall = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    followBall.transform.parent = transform;
                }
                followBallRenderer = followBall.GetComponent<Renderer>();

                // [新增] 添加碰撞体用于射线检测
                ballCollider = followBall.GetComponent<SphereCollider>();
                if (ballCollider == null) ballCollider = followBall.AddComponent<SphereCollider>();

                // 重要：设置 Layer 为 "Note" (确保你在 Unity 的 Tags & Layers 中添加了这个层，且 ID 与 LaserShooter 匹配)
                // 如果你还没有设置 Layer，暂时可以用代码查找，或者在 Inspector 手动设置
                // followBall.layer = LayerMask.NameToLayer("Note"); 
            }

            // 调整球体大小，比视觉宽度稍大一点，增加判定容错率
            followBall.transform.localScale = Vector3.one * (sliderWidth * 2.0f);

            if (followBallRenderer != null && bodyMaterial != null)
            {
                followBallRenderer.sharedMaterial = bodyMaterial;
            }

            followBall.SetActive(false);
        }

        /// <summary>
        /// 核心逻辑：更新跟踪球位置（包含折返算法）
        /// </summary>
        private void UpdateFollowBall()
        {
            double currentTime = gameManager.GetCurrentMusicTimeMs();

            // 简单防抖
            if (System.Math.Abs(currentTime - lastUpdateTime) < 0.001) return;
            lastUpdateTime = currentTime;

            double startTime = sliderData.StartTime;
            double endTime = sliderData.EndTime;
            double totalDuration = endTime - startTime;

            if (currentTime >= startTime && currentTime <= endTime)
            {
                if (!followBall.activeSelf) followBall.SetActive(true);

                // 1. 计算总进度 (0.0 ~ 1.0)
                double totalProgress = (currentTime - startTime) / totalDuration;

                // 2. 获取折返次数 (默认1次)
                // 注意：请确保你的 SliderObject 定义了 RepeatCount
                int repeatCount = sliderData.RepeatCount > 0 ? sliderData.RepeatCount : 1;

                // 3. 计算单次跨度进度 (Ping-Pong 算法)
                double spanRaw = totalProgress * repeatCount;
                int currentSpanIndex = (int)spanRaw;
                double spanProgress = spanRaw - currentSpanIndex;

                // 处理边界情况：刚好结束时
                if (currentSpanIndex >= repeatCount)
                {
                    currentSpanIndex = repeatCount - 1;
                    spanProgress = 1.0;
                }

                // 4. 如果是奇数次跨度（1, 3, 5...），则反向运动
                if (currentSpanIndex % 2 != 0)
                {
                    spanProgress = 1.0 - spanProgress;
                }

                // 5. 高性能获取位置
                Vector3 targetPos = GetPositionOnPathOptimized((float)spanProgress);
                followBall.transform.localPosition = targetPos - Vector3.forward * 0.002f;
            }
            else if (currentTime > endTime)
            {
                if (followBall.activeSelf) followBall.SetActive(false);
            }
            else // 未开始
            {
                if (followBall.activeSelf) followBall.SetActive(false);
            }
        }

        /// <summary>
        /// 优化版路径查找：二分查找 O(logN) 代替 线性查找 O(N)
        /// </summary>
        private Vector3 GetPositionOnPathOptimized(float progress)
        {
            if (worldPathPoints.Count < 2) return transform.position;

            progress = Mathf.Clamp01(progress);
            float targetDist = progress * totalPathLength;

            // 二分查找找到目标距离所在的线段
            // BinarySearch 如果没找到确切值，返回位补码 ~index，表示如果插入该值应该在的位置
            int index = cumulativeLengths.BinarySearch(targetDist);

            if (index < 0)
            {
                index = ~index; // 转换为插入点索引
            }

            // index 是第一个比 targetDist 大的点的索引
            // 所以目标在线段: (index-1) 到 (index) 之间

            // 边界检查
            if (index <= 0) return worldPathPoints[0];
            if (index >= cumulativeLengths.Count) return worldPathPoints[worldPathPoints.Count - 1];

            int indexA = index - 1;
            int indexB = index;

            float distA = cumulativeLengths[indexA];
            float distB = cumulativeLengths[indexB];

            // 局部插值
            float segmentLen = distB - distA;
            float t = (targetDist - distA) / segmentLen;

            return Vector3.Lerp(worldPathPoints[indexA], worldPathPoints[indexB], t);
        }

        private void StartFadeOut()
        {
            isFadingOut = true;
            fadeOutStartTime = Time.time;
        }

        private void UpdateFadeOut()
        {
            float elapsed = Time.time - fadeOutStartTime;
            float fadeProgress = elapsed / fadeOutDuration;

            if (fadeProgress >= 1f)
            {
                // 渐隐结束，停用对象（不要直接 Destroy，准备给对象池回收）
                gameObject.SetActive(false);
                // 如果你有对象池系统，在这里调用 ObjectPool.Return(this);
                return;
            }

            currentAlpha = 1f - fadeProgress;
            UpdateMaterialAlpha();
        }

        /// <summary>
        /// 使用 MaterialPropertyBlock 优化性能，避免材质实例化
        /// </summary>
        private void UpdateMaterialAlpha()
        {
            // 1. 更新本体颜色
            Color baseColor = bodyMaterial != null ? bodyMaterial.color : Color.white;
            baseColor.a = currentAlpha;

            if (meshRenderer)
            {
                meshRenderer.GetPropertyBlock(_propBlock);
                _propBlock.SetColor(ColorPropertyId, baseColor);
                meshRenderer.SetPropertyBlock(_propBlock);
            }

            // 2. [新增] 更新边框颜色
            if (borderMeshRenderer)
            {
                Color borderC = borderMaterial != null ? borderMaterial.color : Color.white;
                borderC.a = currentAlpha;

                borderMeshRenderer.GetPropertyBlock(_propBlock);
                _propBlock.SetColor(ColorPropertyId, borderC);
                borderMeshRenderer.SetPropertyBlock(_propBlock);
            }

            // 3. 更新球体颜色
            if (followBallRenderer)
            {
                followBallRenderer.GetPropertyBlock(_propBlock);
                _propBlock.SetColor(ColorPropertyId, baseColor);
                followBallRenderer.SetPropertyBlock(_propBlock);
            }
        }

        void Update()
        {
            // [修复] 优先处理渐隐逻辑
            // 如果正在渐隐，我们需要继续更新 Alpha 值，直到完全消失
            if (isFadingOut)
            {
                UpdateFadeOut();
                return; // 渐隐时不再处理判定和球的位置
            }

            if (headHit && headInstance != null && headInstance.activeSelf)
            {
                headInstance.SetActive(false); 
            }
            // 如果已经结束且不在渐隐中，或者数据为空，停止运行
            if (sliderData == null || finished) return;

            // 获取时间
            currentMusicTimeCache = gameManager.GetCurrentMusicTimeMs();

            // 1. 更新球体位置
            UpdateFollowBall();

            // 2. 判定逻辑 (头判、Tick、尾判)
            UpdateJudgement();

            // 3. 视觉反馈
            UpdateVisuals();

            // 帧末重置状态
            isTracking = false;
        }

        // 确保销毁时清理 Mesh 内存
        void OnDestroy()
        {
            if (bodyMesh != null) Destroy(bodyMesh);
        }

        // =========================================================
        // 判定与交互逻辑区域
        // =========================================================

        /// <summary>
        /// 被射线照射时调用 (由 LaserShooter 每帧调用)
        /// </summary>
        public void OnRayStay()
        {
            isTracking = true;
        }

        /// <summary>
        /// 尝试击打滑条头 (由 LaserShooter 在按下/进入瞬间调用)
        /// </summary>
        public void TryHitHead()
        {
            if (headHit) return;

            // 计算偏移量：当前时间 - 预期时间
            // 负数 = 提前 (Early), 正数 = 延迟 (Late)
            double offset = currentMusicTimeCache - sliderData.StartTime;

            // [核心逻辑] 
            // offset >= -20 : 只有缩圈几乎重合（只剩20ms）时才开始允许判定
            // offset <= 150 : 允许延迟 150ms
            if (offset >= -20 && offset <= 150)
            {
                headHit = true;

                // 击中头，球体显示
                if (followBall) followBall.SetActive(true);

                Debug.Log($"<color=green>Slider Head HIT!</color> Offset: {offset:F2}ms");
            }
            else if (offset < -20)
            {
                // [保护逻辑] 打太早了（比如提前了 100ms）
                // 此时直接 Return，不要让它算作 Miss，也不要算 Hit
                // 让玩家的手可以在那里等着缩圈到位
                return;
            }
        }

        /// <summary>
        /// 核心判定逻辑 (复刻 osu!droid)
        /// </summary>
        private void UpdateJudgement()
        {
            // 0. 前置检查：如果没有数据直接返回
            if (sliderData.NestedHitObjects == null) return;

            // 1. 头部判定 (Head)
            // 如果还没有击中头部，且时间还没超时，尝试自动判定 (Relax模式/VR特性)
            if (!headHit)
            {
                double diff = currentMusicTimeCache - sliderData.StartTime;

                // 判定窗口 (这里用 150ms 举例，对应 OD5)
                // osu!droid: onSliderHeadHit
                if (Mathf.Abs((float)diff) <= 150)
                {
                    // 如果是 Relax/VR，只要 Tracking 就算击中头
                    if (isTracking)
                    {
                        HitHead();
                    }
                }
                // 超时 Miss
                else if (diff > 150)
                {
                    headHit = true; // 标记处理过，防止重复进入
                    Debug.Log($"<color=red>Slider Head MISS</color>");
                    gameManager.OnNoteMiss(sliderData);
                    // 注意：在 osu! 中，Miss 头会导致整个滑条断连，但滑条不会消失，还能打后面的 Tick
                }
            }

            // 2. 嵌套物件判定 (Tick, Repeat, Tail)

            // 遍历所有时间已经到达的嵌套物件
            // 使用 while 循环是为了防止掉帧时漏掉中间的 Tick
            while (currentNestedIndex < sliderData.NestedHitObjects.Count)
            {
                var nestedObject = sliderData.NestedHitObjects[currentNestedIndex];

                // 如果当前时间还没到这个物件的判定时间，停止循环
                // 留一点点容错 (0.01ms) 避免浮点精度问题
                if (currentMusicTimeCache < nestedObject.Time - 0.01)
                {
                    break;
                }

                // 时间到了，开始判定！

                // 判定依据：当前帧是否 isTracking
                // 注意：即使 Head Miss 了，只要现在按住了，Tick 依然算击中 (TicksGot++)
                bool hit = isTracking;

                nestedObject.IsHit = hit;

                if (hit)
                {
                    ticksGot++;
                    // 播放音效 (需要在 SliderController 中实现 PlayHitSound)
                    // PlayHitSound(nestedObject.Type);

                    // 视觉反馈 & 分数
                    switch (nestedObject.Type)
                    {
                        case SliderEventType.Tick:
                            // Debug.Log("Tick Hit"); 
                            // gameManager.AddScore(10); 
                            // gameManager.AddCombo();
                            break;

                        case SliderEventType.Repeat:
                            Debug.Log("<color=cyan>Slider Repeat Hit</color>");
                            // gameManager.AddScore(30); 
                            // gameManager.AddCombo();
                            // 更新箭头位置到另一端
                            UpdateArrowTransform(nestedObject.SpanIndex + 1);
                            // 视觉：在这里可以做反转箭头的消失动画
                            break;

                        case SliderEventType.Tail:
                            Debug.Log("<color=green>Slider End Hit</color>");
                            // gameManager.AddScore(30); 
                            // gameManager.AddCombo(); // 尾部通常给 Combo
                            break;
                    }
                }
                else
                {
                    // Miss
                    // 只要跟丢了 Tick/Repeat/Tail 中的任何一个，都会触发 Miss (断连)
                    // Debug.Log($"<color=red>Slider {nestedObject.Type} MISS</color>");
                    gameManager.OnNoteMiss(sliderData);
                }

                // 移动到下一个物件
                currentNestedIndex++;
            }

            // 3. 结束检查
            // 只要时间超过了 EndTime，无论嵌套物件是否全部判定完（防止极少数情况下的死循环），都强制结束
            if (currentMusicTimeCache > sliderData.EndTime)
            {
                if (!finished)
                {
                    finished = true;

                    // 计算最终分数
                    CalculateFinalScore();

                    // ✅ 只有在这里，当时间彻底走完，才告诉 Manager "这个滑条结束了"
                    // 根据 ticksGot 计算是否全连/Miss，然后提交
                    // 如果你想简单点，只要 ticksGot > 0 就算 Hit，或者必须全中才算
                    if (ticksGot > 0)
                    {
                        // 提交给 Manager 进行销毁和加分
                        gameManager.OnNoteHit(sliderData, 0);
                    }
                    else
                    {
                        gameManager.OnNoteMiss(sliderData);
                    }

                    StartFadeOut();
                }
            }
        }

        /// <summary>
        /// 击中滑条头 (修改版：不再销毁滑条)
        /// </summary>
        private void HitHead()
        {
            if (headHit) return;
            headHit = true;

            // 1. 视觉反馈
            if (followBall) followBall.SetActive(true);
            if (headInstance) headInstance.SetActive(false); // 隐藏头，显示球

            Debug.Log($"<color=green>Slider Head HIT</color>");

            // ❌ 删除这一行！不要在这里告诉 Manager 击中了，否则它会把滑条删掉！
            // gameManager.OnNoteHit(sliderData, 0); 

            // ✅ 如果需要加分/音效，可以在这里单独处理，但绝不能调用会导致 Destroy 的方法
            ticksGot++;
        }

        private void PlayHitSound(SliderEventType type)
        {
            // 这里只是占位，你需要连接到你的 AudioManager
            // Tick 声音比较轻，Repeat 和 Tail 声音比较重
        }

        private void CalculateFinalScore()
        {
            // 简单的分数计算逻辑
            int totalTicks = sliderData.NestedHitObjects.Count + 1; // +1 是因为包含 Head
            float percentage = (float)ticksGot / totalTicks;

            // Debug.Log($"Slider Finished. Ticks: {ticksGot}/{totalTicks}");
        }

        /// <summary>
        /// 更新视觉反馈 (被射线击中时变色)
        /// </summary>
        private void UpdateVisuals()
        {
            if (followBallRenderer == null) return;

            followBallRenderer.GetPropertyBlock(_propBlock);

            Color targetColor = isTracking ? Color.yellow : (bodyMaterial ? bodyMaterial.color : Color.white);
            targetColor.a = currentAlpha;

            _propBlock.SetColor(ColorPropertyId, targetColor);

            followBallRenderer.SetPropertyBlock(_propBlock);
        }
    }
}

