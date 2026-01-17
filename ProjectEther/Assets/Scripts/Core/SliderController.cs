using System.Collections;
using System.Collections.Generic;
using TMPro;
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
        public GameObject sliderTickPrefab;
        private GameObject headInstance;       // 实例化的头
        private GameObject arrowInstance;      // 实例化的箭头


        [Header("调试设置")]
        public bool showDebugLabel = true; // 开关
        public GameObject debugTextPrefab; // 需在 Inspector 拖入一个带 TextMeshPro 的 Prefab
        private TextMeshPro debugTextInstance; // 实例化的文本


        // [新增] 这里的颜色用于生成 Vertex Colors
        public Color customBodyColor = new Color(0.2f, 0.6f, 1f, 0.9f); // 默认 osu! 蓝
        public Color customBorderColor = Color.white;

        // [新增] 专用材质球 (拖入 Mat_Osu_Slider)
        [Tooltip("osu! 风格专用材质 (使用 OsuSlider Shader)")]
        public Material sharedMaterial;

        // 用于管理生成的 Tick 物体 (Key: 数据对象, Value: 场景物体)
        private Dictionary<SliderNestedObject, GameObject> tickVisuals = new Dictionary<SliderNestedObject, GameObject>();

        // 私有组件引用
        private RhythmGameManager gameManager;
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private MeshCollider meshCollider;
        private GameObject followBall;
        private Renderer followBallRenderer; // 缓存球体渲染器
        private Mesh combinedMesh;


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


            // 确保滑条的 AR 时间与全局管理器一致
            if (this.sliderData.TimePreempt < 100)
            {
                double defaultAR = (manager != null && manager.spawnOffsetMs > 100) ? manager.spawnOffsetMs : 1200;
                this.sliderData.TimePreempt = defaultAR;
            }
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

            if (sharedMaterial != null)
            {
                meshRenderer.sharedMaterial = sharedMaterial;
            }

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

            // 8. 创建调试标签
            CreateDebugLabel();
        }

        private void ResetState()
        {
            worldPathPoints.Clear();
            cumulativeLengths.Clear();
            totalPathLength = 0f;
            isFadingOut = false;
            isActive = false;
            currentAlpha = 1f;

            // [修复] 清理 combinedMesh
            if (combinedMesh != null) combinedMesh.Clear();
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
        /// [完全重写] 使用 SliderMeshGenerator 生成单次绘制的完美滑条
        /// </summary>
        private void GenerateMeshes()
        {
            if (worldPathPoints.Count < 2) return;

            // 1. 计算尺寸
            // radius: 本体半径 (宽度的一半)
            float radius = sliderWidth * 0.5f;

            // borderThickness: 边框线的厚度
            // 假设你的 borderWidth 是滑条的总宽度 (包含边框)，那么单边厚度 = (总宽 - 本体宽) / 2
            float borderThickness = (borderWidth - sliderWidth) * 0.5f;

            // 2. 调用生成器 (你需要确保 SliderMeshGenerator.cs 已经创建)
            combinedMesh = SliderMeshGenerator.GenerateSmoothSlider(
                worldPathPoints,
                radius,
                borderThickness,
                customBodyColor,
                customBorderColor
            );
            // [新增] 调试校验逻辑
            bool isMeshValid = (combinedMesh != null && combinedMesh.vertexCount > 0);

            if (!isMeshValid)
            {
                Debug.LogError($"❌ 滑条生成失败! Time: {sliderData.StartTime}ms, Points: {worldPathPoints.Count}");

                // 如果生成失败，把调试文字变红！
                if (debugTextInstance != null)
                {
                    debugTextInstance.color = Color.red;
                    debugTextInstance.text += "\n[MESH ERROR]";
                }
            }
            else
            {
                // 如果成功，显示顶点数 (方便观察性能)
                if (debugTextInstance != null)
                {
                    debugTextInstance.text += $"\n({combinedMesh.vertexCount}v)";
                }
            }

            // 3. 赋值
            if (meshFilter) meshFilter.mesh = combinedMesh;

            // 4. 更新碰撞体
            if (meshCollider) meshCollider.sharedMesh = combinedMesh;

            // [清理] 如果场景里还有旧的 SliderBorder 子物体，删掉它
            Transform oldBorder = transform.Find("SliderBorder");
            if (oldBorder != null) Destroy(oldBorder.gameObject);
        }

        /// <summary>
        /// [新增] 创建 osu! 风格的视觉元素 (头和箭头)
        /// </summary>
        private void CreateVisuals()
        {
            // 创建滑条头 (Slider Head)
            if (sliderHeadPrefab != null)
            {
                if (headInstance != null) Destroy(headInstance);

                headInstance = Instantiate(sliderHeadPrefab, transform);
                headInstance.transform.localPosition = Vector3.zero;
                // 防止 Z-Fighting，稍微往前一点点
                headInstance.transform.localPosition -= Vector3.forward * 0.05f;
                headInstance.SetActive(true);

                var scaler = headInstance.GetComponent<ApproachCircleScaler>();
                if (scaler != null)
                {
                    // [修改] 直接使用 sliderData 里的正确 AR，不再硬编码 1200
                    double arMs = sliderData.TimePreempt;

                    // 双重保险：如果数据里还是错的，用 Manager 的
                    if (arMs < 100 && gameManager != null)
                        arMs = gameManager.spawnOffsetMs;

                    // 启动缩圈
                    scaler.Initialize(sliderData.StartTime, arMs);
                }
            }

          

            // 2. [新增] 生成 Tick (小圆点)
            if (sliderTickPrefab != null && sliderData.NestedHitObjects != null)
            {
                // 清理旧的 Tick (如果是对象池模式需改为回收)
                foreach (var kvp in tickVisuals) if (kvp.Value) Destroy(kvp.Value);
                tickVisuals.Clear();

                foreach (var nested in sliderData.NestedHitObjects)
                {
                    if (nested.Type == SliderEventType.Tick)
                    {
                        GameObject tickObj = Instantiate(sliderTickPrefab, transform);

                        // 计算 Tick 在路径上的位置
                        // 我们利用 CalculatePositionAtTime 辅助函数 (下面会写)
                        Vector3 tickPos = GetPositionAtTime(nested.Time);

                        // 设置位置 (Z轴稍微靠前，避免被滑条体挡住)
                        tickObj.transform.localPosition = tickPos - Vector3.forward * 0.065f;

                        // 存入字典，以便打中时隐藏
                        tickVisuals[nested] = tickObj;
                        tickObj.SetActive(true);
                    }
                }
            }

            // 创建折返箭头 (Reverse Arrow)
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

            arrowInstance.transform.localPosition = position - Vector3.forward * 0.075f; // 防穿模

            // 设置旋转：让箭头的 "Up" (或 "Right") 对齐 direction
            // 假设你的箭头图片是向上的，我们需要让它的 Up 轴指向 direction，同时保持 Forward 轴指向相机 (Vector3.back)
            // 这样 VR 里看才是平面的
            Quaternion baseRotation = Quaternion.LookRotation(Vector3.back, direction);


            // 如果你的箭头贴图是向右的，可能需要额外的旋转，例如：
            arrowInstance.transform.localRotation = baseRotation * Quaternion.Euler(-180, 0, -90);
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

                ballCollider = followBall.GetComponent<SphereCollider>();
                if (ballCollider == null) ballCollider = followBall.AddComponent<SphereCollider>();

                followBall.transform.localScale = Vector3.one * (sliderWidth * 2.0f);


                followBall.SetActive(false);
            }
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

                Vector3 targetPos = GetPositionAtTime(currentTime);
                followBall.transform.localPosition = targetPos - Vector3.forward * 0.035f;
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
        /// 使用 MaterialPropertyBlock 优化性能
        /// </summary>
        private void UpdateMaterialAlpha()
        {
            // 1. 更新滑条主体透明度 (OsuSlider Shader)
            if (meshRenderer)
            {
                meshRenderer.GetPropertyBlock(_propBlock);
                _propBlock.SetFloat("_MainAlpha", currentAlpha);
                meshRenderer.SetPropertyBlock(_propBlock);
            }

            // 2. 更新跟随球透明度
            if (followBallRenderer)
            {
                followBallRenderer.GetPropertyBlock(_propBlock);
                Color ballColor = customBodyColor;
                ballColor.a = currentAlpha;

                _propBlock.SetColor(ColorPropertyId, ballColor);
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
            if (combinedMesh != null) Destroy(combinedMesh);
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
                            // 1. 播放呼吸感动画
                            StartCoroutine(FollowBallPulse());

                            // 2. 隐藏场景里的这个 Tick (它被吃掉了)
                            if (tickVisuals.ContainsKey(nestedObject))
                            {
                                GameObject tickObj = tickVisuals[nestedObject];
                                if (tickObj != null) tickObj.SetActive(false);
                            }

                            // gameManager.AddScore(30); 
                            // gameManager.AddCombo();
                            Debug.Log("Tick Hit");
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

            Color targetColor = isTracking ? Color.yellow : customBodyColor;

            targetColor.a = currentAlpha;

            _propBlock.SetColor(ColorPropertyId, targetColor);

            followBallRenderer.SetPropertyBlock(_propBlock);
        }


        /// <summary>
        /// 跟随球的呼吸/脉冲效果 (Tick 击中反馈)
        /// </summary>
        private IEnumerator FollowBallPulse()
        {
            if (followBall == null) yield break;

            float duration = 0.15f; // 动画时长
            float timer = 0f;

            // 原始大小 (假设是 sliderWidth * 2)
            Vector3 baseScale = Vector3.one * (sliderWidth * 2.0f);
            Vector3 targetScale = baseScale * 1.3f; // 瞬间变大 1.3 倍

            while (timer < duration)
            {
                timer += Time.deltaTime;
                float t = timer / duration;

                // 简单的 Sine 曲线：先变大后变小
                float scaleMultiplier = Mathf.Sin(t * Mathf.PI); // 0 -> 1 -> 0

                // Lerp 插值
                followBall.transform.localScale = Vector3.Lerp(baseScale, targetScale, scaleMultiplier);

                yield return null;
            }

            // 确保恢复原样
            followBall.transform.localScale = baseScale;
        }

        /// <summary>
        /// [新增] 根据时间计算路径上的本地坐标 (复用 Ping-Pong 逻辑)
        /// </summary>
        private Vector3 GetPositionAtTime(double time)
        {
            double startTime = sliderData.StartTime;
            double duration = sliderData.EndTime - startTime;

            // 计算总进度 (0.0 ~ 1.0)
            double totalProgress = (time - startTime) / duration;
            totalProgress = System.Math.Clamp(totalProgress, 0.0, 1.0); // 钳制范围

            // 获取折返次数
            int repeatCount = sliderData.RepeatCount > 0 ? sliderData.RepeatCount : 1;

            // Ping-Pong 算法
            double spanRaw = totalProgress * repeatCount;
            int currentSpanIndex = (int)spanRaw;
            double spanProgress = spanRaw - currentSpanIndex;

            // 边界处理
            if (currentSpanIndex >= repeatCount)
            {
                currentSpanIndex = repeatCount - 1;
                spanProgress = 1.0;
            }

            // 奇数跨度反向
            if (currentSpanIndex % 2 != 0)
            {
                spanProgress = 1.0 - spanProgress;
            }

            // 调用你现有的优化寻路
            return GetPositionOnPathOptimized((float)spanProgress);
        }
        /// <summary>
        /// [调试] 创建头顶的调试标签
        /// </summary>
        private void CreateDebugLabel()
        {
            if (!showDebugLabel) return;

            // 如果没有分配 Prefab，就代码动态生成一个临时的
            GameObject labelObj = null;
            if (debugTextPrefab != null)
            {
                labelObj = Instantiate(debugTextPrefab, transform);
            }
            else
            {
                labelObj = new GameObject("DebugLabel");
                labelObj.transform.parent = transform;
                labelObj.transform.localScale = Vector3.one * 0.05f; // 缩小一点

                // 挂载 TextMesh (如果没有 TMP，这是最简单的原生方案)
                TextMesh tm = labelObj.AddComponent<TextMesh>();
                tm.characterSize = 0.1f;
                tm.fontSize = 40;
                tm.anchor = TextAnchor.LowerCenter;
                tm.alignment = TextAlignment.Center;
                tm.color = Color.white;
            }

            // 设置位置：在滑条头上方 0.1 米
            labelObj.transform.localPosition = new Vector3(0, 0.1f, 0);
            // 旋转：朝向相机 (简单起见，直接反向)
            labelObj.transform.localRotation = Quaternion.identity;

            // 设置文字内容
            // 显示：开始时间 | 连击号
            string info = $"{sliderData.StartTime}ms\n#{sliderData.ComboIndex}";

            // 尝试获取组件 (兼容 TextMeshPro 和 TextMesh)
            var tmp = labelObj.GetComponent<TextMeshPro>();
            if (tmp)
            {
                tmp.text = info;
                debugTextInstance = tmp;
            }
            else
            {
                var tm = labelObj.GetComponent<TextMesh>();
                if (tm) tm.text = info;
            }
        }

        /// <summary>
        /// 调试用：在场景视图中绘制滑条路径
        /// </summary>
        private void OnDrawGizmos()
        {
            // 只有当游戏运行时且有数据才画
            if (!Application.isPlaying || worldPathPoints == null || worldPathPoints.Count < 2) return;

            // 1. 画路径线 (黄色)
            Gizmos.color = Color.yellow;
            for (int i = 0; i < worldPathPoints.Count - 1; i++)
            {
                // 注意 worldPathPoints 是本地坐标，要转成世界坐标画
                Vector3 p1 = transform.TransformPoint(worldPathPoints[i]);
                Vector3 p2 = transform.TransformPoint(worldPathPoints[i + 1]);
                Gizmos.DrawLine(p1, p2);
            }

            // 2. 画起终点 (绿色/红色球)
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(transform.TransformPoint(worldPathPoints[0]), 0.02f);

            Gizmos.color = Color.red;
            Gizmos.DrawSphere(transform.TransformPoint(worldPathPoints[worldPathPoints.Count - 1]), 0.02f);
        }
    }


}

