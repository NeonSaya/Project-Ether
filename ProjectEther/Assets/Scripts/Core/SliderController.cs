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

        [Header("滑条设置")]
        [Tooltip("滑条宽度（世界单位）")]
        public float sliderWidth = 0.05f;

        [Tooltip("滑条高度（轻微凸起效果）")]
        public float sliderHeight = 0.01f;

        [Tooltip("滑条材质")]
        public Material sliderMaterial;

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

        // 私有组件引用
        private RhythmGameManager gameManager;
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh sliderMesh;
        private MeshCollider meshCollider;
        private GameObject followBall;
        private Renderer followBallRenderer; // 缓存球体渲染器

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
            // 重置状态（对象池复用时必须）
            ResetState();

            if (sliderData == null || manager == null)
            {
                Debug.LogError("SliderController: 初始化参数为空");
                return;
            }

            this.sliderData = sliderData;
            this.sliderWidth = width;
            this.gameManager = manager;

            // 获取组件
            if (!meshFilter) meshFilter = GetComponent<MeshFilter>();
            if (!meshRenderer) meshRenderer = GetComponent<MeshRenderer>();
            // [新增] 获取或添加 MeshCollider
            if (!meshCollider) meshCollider = GetComponent<MeshCollider>();
            if (!meshCollider) meshCollider = gameObject.AddComponent<MeshCollider>();

            // 初始化属性块
            if (_propBlock == null) _propBlock = new MaterialPropertyBlock();

            // 设置材质
            if (sliderMaterial != null)
            {
                meshRenderer.sharedMaterial = sliderMaterial; // 使用 sharedMaterial 防止实例化
            }

            // 1. 生成路径并预计算长度
            GenerateSliderPath();

            // 2. 生成网格
            GenerateSliderMesh();

            // 3. 创建或重置跟踪球
            CreateFollowBall();

            // 初始设置为完全不透明
            currentAlpha = 1f;
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

            if (sliderMesh != null) sliderMesh.Clear();
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
        /// 生成滑条网格并更新碰撞体
        /// </summary>
        private void GenerateSliderMesh()
        {
            if (worldPathPoints.Count < 2) return;

            if (sliderMesh == null)
            {
                sliderMesh = new Mesh();
                sliderMesh.name = "SliderMesh";
            }
            else
            {
                sliderMesh.Clear();
            }

            List<Vector3> vertices = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> triangles = new List<int>();

            for (int i = 0; i < worldPathPoints.Count; i++)
            {
                Vector3 currentPoint = worldPathPoints[i];
                Vector3 direction = Vector3.forward;

                if (i < worldPathPoints.Count - 1)
                    direction = (worldPathPoints[i + 1] - currentPoint).normalized;
                else if (i > 0)
                    direction = (currentPoint - worldPathPoints[i - 1]).normalized;

                Vector3 rightDir = Vector3.Cross(direction, Vector3.forward).normalized;

                if(rightDir.sqrMagnitude < 0.001f) rightDir = Vector3.right;

                vertices.Add(currentPoint - rightDir * (sliderWidth * 0.5f));
                vertices.Add(currentPoint + rightDir * (sliderWidth * 0.5f));

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

            sliderMesh.SetVertices(vertices);
            sliderMesh.SetUVs(0, uvs);
            sliderMesh.SetTriangles(triangles, 0);
            sliderMesh.RecalculateNormals();

            meshFilter.mesh = sliderMesh;

            // [新增] 动态更新 MeshCollider
            if (meshCollider != null)
            {
                meshCollider.sharedMesh = null; // 强制刷新
                meshCollider.sharedMesh = sliderMesh;
            }
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
            }

            followBall.transform.localScale = Vector3.one * (sliderWidth * 1.5f);

            // 确保球体也使用属性块，防止球体颜色不一致
            if (followBallRenderer != null && sliderMaterial != null)
            {
                followBallRenderer.sharedMaterial = sliderMaterial;
            }

            followBall.SetActive(false); // 默认隐藏，直到滑条开始
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
                if (!isFadingOut && enableFadeOut) StartFadeOut();
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
            // 获取当前颜色
            Color baseColor = sliderMaterial != null ? sliderMaterial.color : Color.white;
            baseColor.a = currentAlpha;

            // 1. 设置滑条网格颜色
            if (meshRenderer)
            {
                meshRenderer.GetPropertyBlock(_propBlock);
                _propBlock.SetColor(ColorPropertyId, baseColor);
                meshRenderer.SetPropertyBlock(_propBlock);
            }

            // 2. 设置跟踪球颜色
            if (followBallRenderer)
            {
                followBallRenderer.GetPropertyBlock(_propBlock);
                _propBlock.SetColor(ColorPropertyId, baseColor);
                followBallRenderer.SetPropertyBlock(_propBlock);
            }
        }

        void Update()
        {
            if (!isInitialized || !isActive) return;

            UpdateFollowBall();

            if (isFadingOut)
            {
                UpdateFadeOut();
            }
        }

        // 确保销毁时清理 Mesh 内存
        void OnDestroy()
        {
            if (sliderMesh != null) Destroy(sliderMesh);
        }
    }
}