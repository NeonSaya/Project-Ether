using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Pool;
using static UnityEngine.UI.Slider;

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

        [Header("判定设置")]
        [Tooltip("跟随判定半径倍率 (osu!标准约为 2x CS半径)")]
        public float followRadiusMultiplier = 2f; // [新增] 控制跟随判定的容错范围

        [Header("渐变效果")]
        [Tooltip("渐隐时间（秒）")]
        public float fadeOutDuration = 0.5f;

        [Tooltip("是否启用渐隐效果")]
        public bool enableFadeOut = true;

        [Header("osu! 风格组件")]
        public GameObject sliderHeadPrefab;    // 拖入 VisualHitCircle Prefab
        public GameObject sliderTickPrefab;
        private GameObject headInstance;       // 实例化的头
        private GameObject arrowInstance;      // 实例化的箭头

       
        [Header("调试设置")]
        public bool showDebugLabel = true; // 开关
        public GameObject debugTextPrefab; // 需在 Inspector 拖入一个带 TextMeshPro 的 Prefab
        private TextMeshPro debugTextInstance; // 实例化的文本


        private bool headHitValid = false;

        // [新增] 折返粒子特效引用
        private ParticleSystem headReversePS;
        private ParticleSystem tailReversePS;

        // [新增] 这里的颜色用于生成 Vertex Colors
        public Color customBodyColor = new Color(0.2f, 0.6f, 1f, 0.9f); // 默认 osu! 蓝
        public Color customBorderColor = Color.white;

        // [新增] 专用材质球 (拖入 Mat_Osu_Slider)
        [Tooltip("osu! 风格专用材质 (使用 OsuSlider Shader)")]
        public Material sharedMaterial;

        // 用于管理生成的 Tick 物体 (Key: 数据对象, Value: 场景物体)
        private struct TickVisualInfo
        {
            public SliderNestedObject data;
            public GameObject gameObject;
        }
        private List<TickVisualInfo> tickVisuals = new List<TickVisualInfo>();

        // 私有组件引用
        private MeshRenderer borderMeshRenderer; // [新增] 用于同步透明度
        private RhythmGameManager gameManager;
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private MeshCollider meshCollider;
        private GameObject followBall;
        private Renderer followBallRenderer; // 缓存球体渲染器
        private Mesh combinedMesh;

        // [新增] 用于记录球体的基础大小，防止吃Tick后变大回不去
        private float baseBallScale = 1.0f;
        // [新增] 用于管理协程，防止连续吃Tick时动画冲突
        private Coroutine pulseCoroutine;
        // [新增] 嵌套物件判定索引
        private int currentNestedIndex = 0;
        // [新增] 记录获得了多少个 Tick (用于计算最终分数)
        private int ticksGot = 0;

        // [新增] 判定相关变量
        private SphereCollider ballCollider; // 用于射线的碰撞体

        // 状态变量
        public bool isTracking = false;     // 当前帧是否被射线照射
        private bool isTrackingAudioPlaying = false; // 是否正在播放跟踪音效
        private bool isTrackingRightHand = true; // 当前跟踪的是右手还是左手
        private bool hasStarted = false;     // 滑条是否已经开始
        private bool headHit = false;        // 滑条头是否被击中
        private bool finished = false;       // 滑条是否结束

        // [新增] 记录上一帧的击中位置，用于计算是否在球的半径内
        private Vector3 lastHitPosition;

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

        // 存储对象池引用
        private IObjectPool<GameObject> myPool;
        private IObjectPool<GameObject> tickPool;

        // [新增] 缓存 Combo 颜色给头部使用
        private Color currentComboColor;

        // 全局唯一的 Stencil ID 计数器
        private int currentStencilId = 1;

        // 用来控制视觉显隐的索引
        private int nextVisualIndex = 0;

        // 记录下一个 Tick 位置的缓存（优化）
        private Vector3? nextNotePosition;


        // 在 SliderController 类中添加这个列表，用来记录所有生成的子物体（Tick, 箭头等）
        private List<GameObject> garbageList = new List<GameObject>();

        // 缓存滑条头部的空心光环 Mesh（如果需要的话）
        private static Mesh cachedSliderHaloMesh;

        private static Texture2D cachedSoftDotTex;
        // 缓存用于折返粒子的柔光圆点贴图
        private static Material cachedHaloMat;
        private static Material cachedReverseMat;

        /// <summary>
        /// [新增] 生成实心柔光粒子贴图 (用于打击粒子和折返粒子)
        /// </summary>
        private Texture2D GetSoftDotTexture()
        {
            if (cachedSoftDotTex != null) return cachedSoftDotTex;

            int size = 64; // 小粒子不需要太大
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] colors = new Color[size * size];
            Vector2 center = new Vector2(size / 2f, size / 2f);
            float maxRadius = size / 2f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    float r = dist / maxRadius;
                    float alpha = 0f;

                    if (r <= 1.0f)
                    {
                        // 径向渐变：中心实心，边缘柔和
                        // 使用 Cosine 曲线模拟光晕衰减
                        float v = Mathf.Clamp01(1f - r);
                        alpha = v * v; // 平方让边缘衰减更快，中心更亮
                    }

                    colors[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(colors);
            tex.Apply();
            cachedSoftDotTex = tex;
            return tex;
        }


        private static Texture2D cachedSliderGlowTex;


        /// <summary>
        /// [新增] 生成和滑条融为一体的折返粒子特效
        /// </summary>
        private ParticleSystem CreateReverseParticle(Vector3 pos, Quaternion rot, Color color)
        {
            GameObject go = new GameObject("VFX_Reverse_Energy");
            go.transform.SetParent(transform);
            go.transform.position = pos;
            go.transform.rotation = rot;

            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.playOnAwake = false;

            var emission = ps.emission;
            var shape = ps.shape;
            var noise = ps.noise; // ✅ [新增] 噪声模块
            var colOL = ps.colorOverLifetime; // ✅ [新增] 颜色/透明度渐变
            var limitVel = ps.limitVelocityOverLifetime; // ✅ [新增] 阻力模块
            var renderer = go.GetComponent<ParticleSystemRenderer>();

            // 1. 基础设置：彻底打乱
            main.duration = 1.0f;
            main.loop = true;

            // ✅ [随机化] 寿命范围拉大 (0.3 ~ 0.6)
            // 有的死得早(在内部)，有的死得晚(飘得远)，打破边缘整齐感
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);

            // ✅ [随机化] 速度极慢且差异大
            // 模拟悬浮微尘，只有微弱的向外趋势
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.0f);
            // 尺寸随机
            float baseSize = sliderWidth * 0.12f;
            main.startSize = new ParticleSystem.MinMaxCurve(baseSize * 0.5f, baseSize * 1.5f);

            main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            // 2. 形状：体积填充
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.arc = 180f;
            shape.radius = sliderWidth * 0.25f; // 稍微扩大一点范围

            // ✅ [核心] 设为 1，确保粒子在半圆"内部"随机生成，而不是只在边缘
            shape.radiusThickness = 1.0f;

            // 3. 发射率：极高密度 (因为粒子很小且淡)
            emission.rateOverTime = 400f;
            emission.enabled = false;

            // 4. ✅ [新增] 噪声模块：制造"朦胧"和"扰动"的关键
            noise.enabled = true;
            noise.strength = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
            noise.frequency = 0.5f; // 扰动频率 (低频显得更像烟雾)
            noise.scrollSpeed = 0.5f; // 噪声纹理滚动
            noise.damping = true; // 阻尼，让乱动更柔和

            // 5. ✅ [新增] 阻力 (关键！)
            // 让粒子飞出来后迅速减速，停在半空中变成"散沙"
            limitVel.enabled = true;
            limitVel.limit = 0.1f; // 限制最大速度为 0.1 (几乎静止)
            limitVel.dampen = 0.2f; // 阻尼系数 (0~1)，越大减速越快

            // 5. ✅ [新增] 透明度渐变 (Fade In -> Fade Out)
            // 这是消除"边缘感"的神器，让粒子在半空中慢慢消失
            colOL.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(Color.white, 1.0f) },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0f, 0f),    // 初始透明
                    new GradientAlphaKey(1f, 0.2f),  // 迅速显现
                    new GradientAlphaKey(1f, 0.6f),  // 保持可见
                    new GradientAlphaKey(0f, 1.0f)   // 慢慢消失 (0透明度)
                }
            );
            colOL.color = grad;

            // 6. 渲染器
            renderer.renderMode = ParticleSystemRenderMode.Billboard;

            if (cachedReverseMat == null)
            {
                Shader shader = Shader.Find("Mobile/Particles/Additive");
                if (!shader) shader = Shader.Find("Legacy Shaders/Particles/Additive");
                cachedReverseMat = new Material(shader);
                cachedReverseMat.mainTexture = GetSoftDotTexture();

                // 粒子系统会自带颜色，所以材质颜色设为纯白高亮即可
                Color hdrColor = Color.white * 3.0f;
                if (cachedReverseMat.HasProperty("_TintColor")) cachedReverseMat.SetColor("_TintColor", hdrColor);
                else if (cachedReverseMat.HasProperty("_BaseColor")) cachedReverseMat.SetColor("_BaseColor", hdrColor);
                else cachedReverseMat.SetColor("_Color", hdrColor);
            }

            // 使用 sharedMaterial
            renderer.sharedMaterial = cachedReverseMat;

            go.SetActive(true);
            garbageList.Add(go);

            ps.Play();
            return ps;
        }

        /// <summary>
        /// [修复版] 完美匹配 1.25x 缩放的空心光环
        /// </summary>
        private Texture2D GetGlowTexture()
        {
            if (cachedSliderGlowTex != null) return cachedSliderGlowTex;

            int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);

            Color[] colors = new Color[size * size];
            Vector2 center = new Vector2(size / 2f, size / 2f);
            float maxRadius = size / 2f;

            float startRadius = 0.72f;
            float peakRadius = 0.80f;
            float endRadius = 1.0f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    float r = dist / maxRadius;
                    float alpha = 0f;

                    if (r < startRadius) alpha = 0f;
                    else if (r < peakRadius)
                    {
                        float t = (r - startRadius) / (peakRadius - startRadius);
                        alpha = t * t * (3f - 2f * t);
                    }
                    else
                    {
                        // 外部三次方衰减
                        float t = (r - peakRadius) / (endRadius - peakRadius);
                        float linear = 1f - Mathf.Clamp01(t);
                        alpha = linear * linear * linear;
                    }

                    alpha *= 0.8f;

                    colors[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(colors);
            tex.Apply();
            cachedSliderGlowTex = tex;
            return tex;
        }

        // Slider 生命周期修复
        void OnEnable()
        {
            isActive = true;
            isTracking = false;
            headHit = false;
            finished = false;
        }


        /// <summary>
        /// 初始化滑条控制器 (对象池版)
        /// </summary>
        public void Initialize(SliderObject sliderData, float beatmapCS, Color comboColor, RhythmGameManager manager, IObjectPool<GameObject> pool, IObjectPool<GameObject> tPool, Vector3? nextPos = null)
        {
            CleanUpEverything();

            // 1. 检查滑条头 (headInstance)
            // 如果它在 Unity 引擎层被销毁了 (Equals(null))，但在 C# 里还留着引用
            // 我们必须手动把它设为 true null，后续 CreateVisuals 才会重新 Instantiate
            if (headInstance != null && headInstance.Equals(null))
            {
                headInstance = null;
            }

            // 2. 检查跟随球 (followBall) - 注意你代码里变量名可能是 followBall 或 followBallInstance
            if (followBall != null && followBall.Equals(null))
            {
                followBall = null;
            }

            // 3. 检查 Tick 缓存 (防止 ResetState 遍历时报错)
            if (tickVisuals != null)
            {
                // List 的写法比 Dictionary 简单得多，直接使用 RemoveAll 配合 Lambda 表达式
                // 这行代码会移除所有对应的 GameObject 已经在 Unity 引擎底层被销毁的条目
                tickVisuals.RemoveAll(info => info.gameObject == null || info.gameObject.Equals(null));
            }


            // 存下池子引用
            this.myPool = pool;
            this.tickPool = tPool;

            // 彻底重置状态 (清理上一条滑条的残留数据)
            ResetState();

            if (sliderData == null || manager == null) return;

            this.sliderData = sliderData;
            this.gameManager = manager;
            this.currentComboColor = comboColor;

            // 缓存下一个 Note 位置
            this.nextNotePosition = nextPos;

            // 设置位置，考虑 Stack
            Vector3 startPos = CoordinateMapper.MapToWorld(sliderData.Position);

            float stackOffset = sliderData.StackOrder * 0.01f;
            startPos.z -= stackOffset;

            transform.position = startPos;

            // CS 尺寸计算
            float finalSize = RhythmGameManager.CalculateVROsuSize(beatmapCS);
            this.sliderWidth = finalSize;
            this.borderWidth = finalSize * 1.25f;

            // 颜色设置 (腹黑滑条)
            this.customBodyColor = new Color(0.05f, 0.05f, 0.05f, 0.7f);
            this.customBorderColor = new Color(0.2f, 0.2f, 0.2f, 0.9f);

            

            // AR 时间修正
            if (this.sliderData.TimePreempt < 100)
            {
                double defaultAR = (manager != null && manager.spawnOffsetMs > 100) ? manager.spawnOffsetMs : 1200;
                this.sliderData.TimePreempt = defaultAR;
            }

            // 修正嵌套物件时间
            if (this.sliderData.NestedHitObjects != null)
            {
                foreach (var nested in this.sliderData.NestedHitObjects)
                {
                    if (!nested.IsTimeFixed && nested.Time < this.sliderData.StartTime)
                    {
                        nested.Time += this.sliderData.StartTime;
                        nested.IsTimeFixed = true; // 确保只加一次
                    }

                }
            }

            // 初始化组件
            if (!meshFilter) meshFilter = GetComponent<MeshFilter>();
            if (!meshRenderer) meshRenderer = GetComponent<MeshRenderer>();
            if (!meshCollider) meshCollider = GetComponent<MeshCollider>();
            if (!meshCollider) meshCollider = gameObject.AddComponent<MeshCollider>();
            if (sharedMaterial != null) meshRenderer.sharedMaterial = sharedMaterial;
            if (_propBlock == null) _propBlock = new MaterialPropertyBlock();

            // 生成逻辑
            GenerateSliderPath();
            GenerateMeshes();

            // 设置 VR 尺寸
            if (headInstance) headInstance.transform.localScale = new Vector3(finalSize, finalSize, 0.02f);
            if (followBall) followBall.transform.localScale = Vector3.one * (finalSize * 1.1f);

            CreateFollowBall();
            CreateVisuals(); // 内部会处理 Tick 的池化生成

            // 重置计数器
            currentNestedIndex = 0;
            nextVisualIndex = 0;
            currentMusicTimeCache = gameManager.GetCurrentMusicTimeMs();

            ticksGot = 0;
            isTracking = false;
            headHit = false;
            finished = false;
            currentAlpha = 1f;
            UpdateMaterialAlpha();
            // 更新视觉

            UpdateVisuals();
            
            isInitialized = true;
            isActive = true;
        }

        /// <summary>
        /// 重置状态 (每次出池前调用)
        /// </summary>
        private void ResetState()
        {
            // 1. 回收 Tick (保持不变)
            RecycleAllTicks();

            // 2. 清理路径数据 (保持不变)
            worldPathPoints.Clear();
            cumulativeLengths.Clear();
            totalPathLength = 0f;

            // 3. 重置状态位 (保持不变)
            isFadingOut = false;
            isActive = true;
            currentAlpha = 1f;
            headHit = false;
            finished = false;

            // 4. 处理子物体
            if (headInstance) headInstance.SetActive(false);
            if (arrowInstance) arrowInstance.SetActive(false);
            if (followBall) followBall.SetActive(false);

            // ✅ 核心修复：销毁旧的调试文本
            // 如果变量引用还在，直接销毁
            if (debugTextInstance != null)
            {
                Destroy(debugTextInstance.gameObject);
                debugTextInstance = null;
            }
            // 双重保险：以防引用丢失但物体还在（比如代码重编译后），按名字找一下
            Transform oldDebug = transform.Find("DebugLabel");
            if (oldDebug != null)
            {
                Destroy(oldDebug.gameObject);
            }

            // 5. 清理 Mesh
            if (combinedMesh != null)
            {
                Destroy(combinedMesh);
                combinedMesh = null;
            }
            // 6. 重置计数器
            ticksGot = 0;
            currentNestedIndex = 0;
        }

        /// <summary>
        /// 回收所有 Tick (回池)
        /// </summary>
        private void RecycleAllTicks()
        {
            if (tickVisuals == null) return;

            for (int i = 0; i < tickVisuals.Count; i++)
            {
                var obj = tickVisuals[i].gameObject;
                if (obj != null)
                {
                    obj.SetActive(false);
                    if (tickPool != null)
                        tickPool.Release(obj);
                    else
                        Destroy(obj);
                }
            }
            tickVisuals.Clear();
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

            // 0. 先清理旧的边框物体，防止重复
            CleanUpMeshes();

            // 1. 计算尺寸
            // radius: 本体半径 (宽度的一半)
            float radius = sliderWidth * 0.5f;

            // borderThickness: 边框线的厚度
            // 假设你的 borderWidth 是滑条的总宽度 (包含边框)，那么单边厚度 = (总宽 - 本体宽) / 2
            float borderThickness = (borderWidth - sliderWidth) * 0.5f;


            // 2. 生成唯一的 Stencil ID
            currentStencilId = (NoteController.GlobalRenderOrder++ % 50) + 1;

            // 调用生成器，传入 currentStencilId
            var (borderMesh, bodyMesh, borderMat, bodyMat) = SliderMeshGenerator.GeneratePhysicalSlider(
                    worldPathPoints,
                    radius,
                    borderThickness,
                    customBorderColor,
                    customBodyColor,
                    currentStencilId
            );

            // 3. 渲染主体网格
            combinedMesh = bodyMesh;
            if (meshFilter) meshFilter.mesh = combinedMesh;
            if (meshRenderer)
            {
                meshRenderer.sharedMaterial = bodyMat;
            }

            // 4. 渲染边框网格
            GameObject borderObject = new GameObject("SliderBorder");
            borderObject.transform.SetParent(transform, false);
            borderObject.transform.localPosition = Vector3.zero;
            borderObject.transform.localRotation = Quaternion.identity;

            MeshFilter borderMeshFilter = borderObject.AddComponent<MeshFilter>();
            borderMeshFilter.mesh = borderMesh;

            borderMeshRenderer = borderObject.AddComponent<MeshRenderer>();
            borderMeshRenderer.sharedMaterial = borderMat;

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
        }


        /// <summary>
        /// [新增] 创建 osu! 风格的视觉元素 (头和箭头)
        /// </summary>
        private void CreateVisuals()
        {
            if (this == null || this.gameObject == null) return;

            // 创建滑条头 (Slider Head)
            if (sliderHeadPrefab != null)
            {
                headInstance = Instantiate(sliderHeadPrefab, transform);
                garbageList.Add(headInstance);
                headInstance.transform.localPosition = Vector3.zero;
                // 防止 Z-Fighting，稍微往前一点点
                float headScale = this.sliderWidth;
                headInstance.transform.localScale = new Vector3(headScale, headScale, 0.02f);

                headInstance.transform.localPosition -= Vector3.forward * 0.01f;
                headInstance.SetActive(true);

                // 计算这根滑条的基准层级
                int baseQueue = 3500 - (currentStencilId * 5);

                // 应用当前 Combo 颜色
                Renderer[] headRenderers = headInstance.GetComponentsInChildren<Renderer>();
                MaterialPropertyBlock headMbp = new MaterialPropertyBlock();

                foreach (var r in headRenderers)
                {
                    if (r == null) continue;
                    r.material.renderQueue = baseQueue + 2;
                    r.GetPropertyBlock(headMbp);
                    headMbp.SetColor("_Color", currentComboColor);
                    headMbp.SetColor("_BaseColor", currentComboColor); // 兼容 URP
                    r.SetPropertyBlock(headMbp);
                }

                // 初始化缩圈组件
                var scaler = headInstance.GetComponent<ApproachCircleScaler>();
                if (scaler != null)
                {
                    double arMs = sliderData.TimePreempt;
                    if (arMs < 100 && gameManager != null)
                        arMs = gameManager.spawnOffsetMs;
                    scaler.Initialize(sliderData.StartTime, arMs);
                }

                // =========================================================
                // 给滑条头加完美圆形光晕 (已修复材质内存泄漏)
                // =========================================================
                Transform bodyTr = headInstance.transform.Find("Body");
                GameObject targetBody = bodyTr != null ? bodyTr.gameObject : headInstance;

                GameObject headHalo = new GameObject("Head_Halo");
                headHalo.transform.SetParent(targetBody.transform);
                garbageList.Add(headHalo);

                // 1. 强制使用 Quad Mesh
                var dstMF = headHalo.AddComponent<MeshFilter>();
                if (cachedSliderHaloMesh == null)
                {
                    // 检查缓存，如果没有就创建一个临时 Quad 来获取 Mesh
                    GameObject tempQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    cachedSliderHaloMesh = tempQuad.GetComponent<MeshFilter>().sharedMesh;
                    Destroy(tempQuad);
                }
                dstMF.sharedMesh = cachedSliderHaloMesh;

                // 2. 材质 + 贴图 (核心修复：使用静态缓存材质，保证全游戏只 new 一次)
                if (cachedHaloMat == null)
                {
                    Shader shader = Shader.Find("Mobile/Particles/Additive");
                    if (!shader) shader = Shader.Find("Legacy Shaders/Particles/Additive");

                    cachedHaloMat = new Material(shader);
                    cachedHaloMat.mainTexture = GetGlowTexture();

                    Color whiteGlow = new Color(2.5f, 2.5f, 2.5f, 0.75f);
                    if (cachedHaloMat.HasProperty("_TintColor"))
                        cachedHaloMat.SetColor("_TintColor", whiteGlow);
                    else if (cachedHaloMat.HasProperty("_BaseColor"))
                        cachedHaloMat.SetColor("_BaseColor", whiteGlow); // 兼容 URP
                    else
                        cachedHaloMat.SetColor("_Color", whiteGlow);
                }

                var dstMR = headHalo.AddComponent<MeshRenderer>();

                // 必须用 .material 实例化修改，让光晕也盖在边框之上
                dstMR.material = cachedHaloMat;
                dstMR.material.renderQueue = baseQueue + 2;

                // 3. 变换
                headHalo.transform.localPosition = new Vector3(0, 0, 0.02f);
                headHalo.transform.localRotation = Quaternion.identity;
                headHalo.transform.localScale = Vector3.one * 1.25f; // 大一点

                dstMR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                // =========================================================
            }



            // 2.  生成 Tick (小圆点)
            if (sliderTickPrefab != null && sliderData.NestedHitObjects != null)
            {
                foreach (var nested in sliderData.NestedHitObjects)
                {
                    if (nested.Type == SliderEventType.Tick)
                    {
                        GameObject tickObj = null;
                        try
                        {
                            tickObj = tickPool.Get();
                        }
                        catch
                        {
                            Debug.LogWarning("Tick Pool Empty!");
                            break;
                        }

                        if (tickObj != null)
                        {
                            tickObj.transform.SetParent(transform);
                            tickObj.transform.localRotation = Quaternion.identity;

                            tickObj.SetActive(false); // 初始隐藏

                            float tickScale = this.sliderWidth * 0.3f;
                            tickObj.transform.localScale = new Vector3(tickScale, tickScale, tickScale);

                            Vector3 tickPos = GetPositionAtTime(nested.Time);
                            tickObj.transform.localPosition = tickPos - Vector3.forward * 0.065f;

                            // [修改] 直接存入 List，不检查 Key 冲突
                            tickVisuals.Add(new TickVisualInfo
                            {
                                data = nested,
                                gameObject = tickObj
                            });
                        }
                    }
                }
            }

            if (sliderData.RepeatCount > 1 && worldPathPoints.Count > 1)
            {
                // =========================================================
                // ✅ [终极修复] 基于本地坐标的 2D 旋转计算
                // =========================================================

                // 1. 头部粒子 (位置：point[0])
                // 计算本地流出方向 (从 p[1] -> p[0])
                Vector3 headLocalDir = (worldPathPoints[0] - worldPathPoints[1]);

                // 计算 2D 角度 (Atan2)
                float headAngle = Mathf.Atan2(headLocalDir.y, headLocalDir.x) * Mathf.Rad2Deg;

                // Unity 的 Circle Shape 0度默认指向右(X+)，且 Arc 居中需要偏移
                // 如果 Arc 是 120度，它覆盖 -60 到 +60 度 (相对于 X+)
                // 所以我们直接把 X+ 轴对准方向即可
                Quaternion headRot = transform.rotation * Quaternion.Euler(0, 0, headAngle - 90f);
                // 世界位置
                Vector3 headWorldPos = transform.TransformPoint(worldPathPoints[0]);
                headReversePS = CreateReverseParticle(headWorldPos, headRot, currentComboColor);


                // 2. 尾部粒子 (位置：point[last])
                int last = worldPathPoints.Count - 1;
                // 计算本地流出方向 (从 p[last-1] -> p[last])
                Vector3 tailLocalDir = (worldPathPoints[last] - worldPathPoints[last - 1]);

                float tailAngle = Mathf.Atan2(tailLocalDir.y, tailLocalDir.x) * Mathf.Rad2Deg;

                Quaternion tailRot = transform.rotation * Quaternion.Euler(0, 0, tailAngle - 90f);

                Vector3 tailWorldPos = transform.TransformPoint(worldPathPoints[last]);
                tailReversePS = CreateReverseParticle(tailWorldPos, tailRot, currentComboColor);

                // 3. 初始激活
                UpdateReverseVFX(1);
            }
        }


        // <summary>
        /// [修正版] 消除前摇，瞬时响应
        /// </summary>
        private void UpdateReverseVFX(int nextSpanIndex)
        {
            // 1. 如果已经跑完了所有段落，立刻关闭所有
            if (nextSpanIndex > sliderData.RepeatCount)
            {
                if (headReversePS) { var em = headReversePS.emission; em.enabled = false; }
                if (tailReversePS) { var em = tailReversePS.emission; em.enabled = false; }
                return;
            }

            // 2. 判断我们正在前往哪里 (目标点)
            bool targetIsTail = (nextSpanIndex % 2 != 0);

            // 3. 核心判断：目标点是否是"最终终点"？
            bool isFinalTrip = (nextSpanIndex == sliderData.RepeatCount);

            // 目标需要显示的条件：是目标方向 且 不是最后一趟
            bool showTail = targetIsTail && !isFinalTrip;
            bool showHead = !targetIsTail && !isFinalTrip;

            // --- 控制尾部 ---
            if (tailReversePS)
            {
                var em = tailReversePS.emission;
                // 检查状态是否发生了改变 (从关 -> 开)
                bool wasEnabled = em.enabled;
                em.enabled = showTail;

                // ✅ [关键] 如果刚被开启，手动发射一波粒子，填补"前摇"
                if (!wasEnabled && showTail)
                {
                    tailReversePS.Emit(30); // 瞬间生成30个粒子
                }
            }

            // --- 控制头部 ---
            if (headReversePS)
            {
                var em = headReversePS.emission;
                bool wasEnabled = em.enabled;
                em.enabled = showHead;

                // ✅ [关键] 同理，瞬间发射
                if (!wasEnabled && showHead)
                {
                    headReversePS.Emit(30);
                }
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

                if (garbageList != null) garbageList.Add(followBall);

                baseBallScale = sliderWidth;
                followBall.transform.localScale = Vector3.one * baseBallScale;

                followBallRenderer = followBall.GetComponent<Renderer>();
                ballCollider = followBall.GetComponent<SphereCollider>();
                if (ballCollider == null) ballCollider = followBall.AddComponent<SphereCollider>();

                followBall.SetActive(false);
            }
        }

        /// <summary>
        /// 核心逻辑：更新跟踪球位置（包含折返算法）
        /// </summary>
        private void UpdateFollowBall()
        {
            if (followBall == null) return;
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
                RecycleAllTicks();

                if (myPool != null)
                {
                    myPool.Release(gameObject);
                }
                else
                {
                    gameObject.SetActive(false); // 兜底
                    Destroy(gameObject);
                }
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
            // 1. 更新滑条主体透明度
            if (meshRenderer)
            {
                meshRenderer.GetPropertyBlock(_propBlock);
                Color c = customBodyColor;
                c.a *= currentAlpha; // 结合自定义颜色的初始透明度
                _propBlock.SetColor(ColorPropertyId, c);
                meshRenderer.SetPropertyBlock(_propBlock);
            }

            // 2. [新增] 更新边框透明度
            if (borderMeshRenderer)
            {
                borderMeshRenderer.GetPropertyBlock(_propBlock);
                Color bc = customBorderColor;

                Color hdrBorder = new Color(bc.r * 2.0f, bc.g * 2.0f, bc.b * 2.0f, 1f);

                hdrBorder.a = bc.a * currentAlpha;

                _propBlock.SetColor(ColorPropertyId, hdrBorder);
                // 确保兼容 URP/Unlit
                _propBlock.SetColor("_BaseColor", hdrBorder);

                borderMeshRenderer.SetPropertyBlock(_propBlock);
            }

            // 3. 更新跟随球透明度
            if (followBallRenderer)
            {
                followBallRenderer.GetPropertyBlock(_propBlock);
                Color ballColor = isTracking ? Color.yellow : customBodyColor;
                ballColor.a *= currentAlpha;
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

            // 只要射线指着 (isTracking) 且头还没被打中 (!headHit)
            // 就每一帧都尝试去判定一下时间 (TryHitHead 内部会判断 offset)
            if (isTracking && !headHit)
            {
                TryHitHead(isTrackingRightHand);
            }

            // 2. 判定逻辑 (头判、Tick、尾判)
            UpdateJudgement();

            // 3. 视觉反馈
            UpdateVisuals();


            if (isTracking && isActive)
            {
                // 1. 震动 (持续的微震)
                if (HapticManager.Instance != null)
                {
                    // ✅ [修改] 使用 isTrackingRightHand 代替原来的 true
                    HapticManager.Instance.PlayContinuous(isTrackingRightHand, HapticManager.Instance.profile.SliderSlideIntensity);
                }

                // 2. 音效 (保持不变，音效通常不分左右声道，或者由 AudioSource 3D 设置决定)
                if (!isTrackingAudioPlaying && AudioManager.Instance != null)
                {
                    AudioManager.Instance.ToggleSliderLoop(true, sliderData.SampleSet, sliderData.CustomIndex);
                    isTrackingAudioPlaying = true;
                }
            }
            else
            {
                // 停止音效
                if (isTrackingAudioPlaying && AudioManager.Instance != null)
                {
                    AudioManager.Instance.ToggleSliderLoop(false);
                    isTrackingAudioPlaying = false;
                }
            }

            // 实时驱动折返特效 (0延迟的核心)
            // -------------------------------------------------------------
            if (sliderData != null && !finished)
            {
                // 计算单程持续时间
                double totalDur = sliderData.EndTime - sliderData.StartTime;
                double spanDur = totalDur / sliderData.RepeatCount;

                // 计算当前时间相对于开始时间的进度
                double timeSinceStart = currentMusicTimeCache - sliderData.StartTime;

                // 如果还没开始 (负数)，就是第0段 (去往尾部)
                // 如果已经开始，计算当前处于第几段 (0, 1, 2...)
                int currentSpanIndex = 0;

                if (timeSinceStart > 0)
                {
                    currentSpanIndex = (int)(timeSinceStart / spanDur);
                }

                // 下一段的索引 = 当前段 + 1
                // 比如当前在跑第0段，目标就是第1个折返点
                int nextTargetIndex = currentSpanIndex + 1;

                // 实时更新特效
                UpdateReverseVFX(nextTargetIndex);
            }
            // -------------------------------------------------------------
        }
        public void OnRayExit(bool isRightHand)
        {
            // ✅ 核心防干扰：如果离开的射线，根本不是当初打中滑条头的那只手，直接无视它！
            if (headHit && isTrackingRightHand != isRightHand) return;

            isTracking = false;

            // 下面保留你原有的视觉隐藏代码，比如：
            // if (followBall != null) followBall.SetActive(false);
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
        public void OnRayStay(bool isRightHand, Vector3 hitPosition)
        {
            isTracking = true;
            isTrackingRightHand = isRightHand;
            lastHitPosition = hitPosition; // 记录击中点

            if (!headHit)
            {
                TryHitHead(isRightHand);
            }
        }

        void OnDisable()
        {
            // 当物体被隐藏/回收时，清理残留
            CleanUpEverything();
        }
        /// <summary>
        /// 尝试击打滑条头 (由 LaserShooter 在按下/进入瞬间调用)
        /// </summary>
        public void TryHitHead(bool isRightHand)
        {
            if (headHit) return;

            // 计算偏移量：当前时间 - 预期时间
            // 负数 = 提前 (Early), 正数 = 延迟 (Late)
            double offset = currentMusicTimeCache - sliderData.StartTime;

            // [核心修复] 计算击中点到 "滑条头中心" 的距离
            // 注意：这里不能用 followBall，因为头判定时球可能还没生成或位置不对
            Vector3 headWorldPos = transform.position; // 滑条挂载点即为起点
            float distToHead = Vector3.Distance(lastHitPosition, headWorldPos);

            // 允许的半径：滑条半径 * 宽松系数 (Relax可以稍微宽一点，比如 2.0x ~ 3.0x)
            float allowedRadius = (sliderWidth * 0.5f) * 3.0f;

            // 只有在半径内才允许判定
            if (distToHead > allowedRadius)
            {
                return;
            }

            if (offset >= -20 && offset <= 250)
            {
                // ✅ 1. 立即锁定状态，防止下一帧重复触发
                headHit = true;
                isTrackingRightHand = isRightHand;
                isTracking = true;

                // 2. 视觉与触觉反馈
                if (followBall) followBall.SetActive(true);
                if (AudioManager.Instance != null) AudioManager.Instance.PlayHitSound(sliderData);
                if (HapticManager.Instance != null) HapticManager.Instance.PlayHitHaptic(isRightHand, (int)sliderData.HitSound);
                if (CodeOnlyVFX.Instance != null) CodeOnlyVFX.Instance.PlayHit(transform.position, transform.rotation, this.sliderWidth, currentComboColor, this.nextNotePosition);

                headHitValid = true;
                ticksGot++;

                // 3. 计算动态分数 (300/100/50)
                double maxWindow = 250.0;
                double absDiff = System.Math.Abs(offset);
                double accuracy01 = 1.0 - (absDiff / maxWindow);
                accuracy01 = System.Math.Clamp(accuracy01, 0.0, 1.0);

                int headScore = RhythmGameManager.CalculateScoreFromAccuracy(accuracy01);
                if (headScore == 0) headScore = 50; // 只要接住就给保底 50 分

                // 4. 提交分数
                if (gameManager != null && gameManager.scoreManager != null)
                {
                    gameManager.scoreManager.RegisterHit(headScore);
                }

                // ✅ 5. 确保只有这里有唯一的一次 ShowJudgement 调用！
                if (JudgementVisualizer.Instance != null)
                {
                    JudgementVisualizer.Instance.ShowJudgement(transform.position, headScore, currentComboColor);
                }

                Debug.Log($"<color=green>Slider Head HIT!</color> Offset: {offset:F2}ms, Score: {headScore}");
            }
            else if (offset < -20)
            {
                // 打太早，等待
                return;
            }

        }

        /// <summary>
        /// 核心判定逻辑 (修复版：Miss不销毁，允许中途上车)
        /// </summary>
        private void UpdateJudgement()
        {
            // 0. 前置检查
            if (sliderData.NestedHitObjects == null) return;

            // -------------------------------------------------------------
            // 1. 头部判定 (Head)
            // -------------------------------------------------------------
            if (!headHit)
            {
                double diff = currentMusicTimeCache - sliderData.StartTime;
                double spanDuration = (sliderData.EndTime - sliderData.StartTime) / sliderData.RepeatCount;

                // 判定窗口内，且被追踪 -> 在 TryHitHead 里处理 Hit
                if (isTracking && Mathf.Abs((float)diff) <= 250)
                {
                    // 等待 TryHitHead 触发
                }
                // 超时 Miss
                else if (diff > 250 || (diff > spanDuration && diff > 0))
                {
                    headHit = true;
                    Debug.Log($"<color=red>Slider Head MISS</color>");

                    // 1. 立即隐藏滑条头 (视觉上 Head 没了)
                    if (headInstance != null) headInstance.SetActive(false);

                    // 2. 绝对不要调用 gameManager.OnNoteMiss(sliderData)! 这会杀死滑条
                    // 3. 而是告诉分数系统：断连了 (0分)
                    if (gameManager != null && gameManager.scoreManager != null)
                    {
                        gameManager.scoreManager.RegisterMiss(300);
                    }

                    if (JudgementVisualizer.Instance != null)
                    {
                        JudgementVisualizer.Instance.ShowJudgement(transform.position, 0, Color.red);
                    }
                    // 此时滑条本体还在，Update 还会继续跑，后面的 Tick 还能吃
                }
            }

            // -------------------------------------------------------------
            // 2. 嵌套物件判定 (Tick, Repeat, Tail)
            // -------------------------------------------------------------
            while (currentNestedIndex < sliderData.NestedHitObjects.Count)
            {
                var nestedObject = sliderData.NestedHitObjects[currentNestedIndex];

                // 时间没到，退出循环
                if (currentMusicTimeCache < nestedObject.Time - 0.01) break;

                // --- 判定开始 ---
                bool hit = false;

                if (isTracking && followBall != null)
                {
                    float allowedRadius = (sliderWidth * 0.5f) * followRadiusMultiplier;
                    float dist = Vector3.Distance(lastHitPosition, followBall.transform.position);
                    if (dist <= allowedRadius) hit = true;
                }

                nestedObject.IsHit = hit;

                if (hit)
                {
                    ticksGot++;

                    // Hit 逻辑
                    switch (nestedObject.Type)
                    {
                        case SliderEventType.Tick:
                            if (pulseCoroutine != null) StopCoroutine(pulseCoroutine);
                            StartCoroutine(FollowBallPulse());

                            for (int i = 0; i < tickVisuals.Count; i++)
                            {
                                if (tickVisuals[i].data == nestedObject)
                                {
                                    tickVisuals[i].gameObject.SetActive(false);
                                    break;
                                }
                            }

                            if (HapticManager.Instance != null) HapticManager.Instance.PlaySliderTick(isTrackingRightHand);
                            if (AudioManager.Instance != null) AudioManager.Instance.PlaySliderTick(sliderData.SampleSet, sliderData.CustomIndex, sliderData.SampleVolume / 100f);

                            if (gameManager?.scoreManager != null) gameManager.scoreManager.RegisterComboHit(10);

                            // 🚫 绝对不要在这里加 ShowJudgement！Tick 不弹字！
                            break;

                        case SliderEventType.Repeat:
                            if (AudioManager.Instance != null) AudioManager.Instance.PlayHitSound(sliderData);
                            if (HapticManager.Instance != null) HapticManager.Instance.PlayHitHaptic(isTrackingRightHand, (int)sliderData.HitSound);

                            if (CodeOnlyVFX.Instance != null)
                            {
                                bool atTail = (nestedObject.SpanIndex % 2 == 0);
                                Vector3 vfxLocalPos = atTail ? worldPathPoints[worldPathPoints.Count - 1] : worldPathPoints[0];
                                CodeOnlyVFX.Instance.PlayHit(transform.TransformPoint(vfxLocalPos), transform.rotation, this.sliderWidth, currentComboColor, this.nextNotePosition);
                            }

                            if (gameManager?.scoreManager != null) gameManager.scoreManager.RegisterComboHit(30);
                            UpdateReverseVFX(nestedObject.SpanIndex + 1);
                            break;

                        case SliderEventType.Tail:
                            // 尾巴只发爆破粒子
                            if (CodeOnlyVFX.Instance != null)
                            {
                                bool endsAtTail = (sliderData.RepeatCount % 2 != 0);
                                Vector3 endLocalPos = endsAtTail ? worldPathPoints[worldPathPoints.Count - 1] : worldPathPoints[0];
                                CodeOnlyVFX.Instance.PlayHit(transform.TransformPoint(endLocalPos), transform.rotation, this.sliderWidth, currentComboColor, this.nextNotePosition);
                            }
                            break;
                    }
                }
                else
                {
                    // --- MISS 处理 ---
                    if (nestedObject.Type == SliderEventType.Repeat)
                    {
                        UpdateReverseVFX(nestedObject.SpanIndex + 1);
                    }
                    else if (nestedObject.Type == SliderEventType.Tail)
                    {
                        // ✅ [正确逻辑] 只有尾巴漏了，才在尾巴的真实坐标显示小红叉！
                        if (JudgementVisualizer.Instance != null)
                        {
                            bool endsAtTail = (sliderData.RepeatCount % 2 != 0);
                            Vector3 endLocalPos = endsAtTail ? worldPathPoints[worldPathPoints.Count - 1] : worldPathPoints[0];
                            JudgementVisualizer.Instance.ShowTailMiss(transform.TransformPoint(endLocalPos));
                        }
                    }


                    if (gameManager != null && gameManager.scoreManager != null)
                    {
                        // 精准扣分：Tick 漏打加 10，Repeat 加 30，Tail 加 300
                        int maxScore = 300;
                        if (nestedObject.Type == SliderEventType.Tick) maxScore = 10;
                        else if (nestedObject.Type == SliderEventType.Repeat) maxScore = 30;

                        gameManager.scoreManager.RegisterMiss(maxScore);
                    }
                }
                currentNestedIndex++;
            }

            // -------------------------------------------------------------
            // 3. 结束检查 (End Time Reached)
            // -------------------------------------------------------------
            if (currentMusicTimeCache > sliderData.EndTime)
            {
                if (!finished)
                {
                    finished = true;
                    float finalAcc = CalculateFinalScore();

                    if (ticksGot > 0)
                    {
                        if (AudioManager.Instance != null) AudioManager.Instance.PlayHitSound(sliderData);
                        if (HapticManager.Instance != null) HapticManager.Instance.PlayHitHaptic(isTrackingRightHand, (int)sliderData.HitSound);

                        // 提交给 Manager，Manager 会根据 finalAcc 决定是给 300(>0.9), 100(>0.5) 还是 50
                        gameManager.OnNoteHit(sliderData, finalAcc);
                    }
                    else
                    {
                        // 彻底没打中 (ticksGot == 0)，才算 Miss
                        gameManager.OnNoteMiss(sliderData);
                    }

                    StartFadeOut();
                }
            }
        }

        /// <summary>
        /// 计算滑条的最终完成度 (0.0 ~ 1.0)
        /// </summary>
        private float CalculateFinalScore()
        {
            // 总判定点 = Head + 所有嵌套物件 (Tick + Repeat + Tail)
            // NestedHitObjects 列表里已经包含了 Tail
            int totalJudgements = sliderData.NestedHitObjects.Count + 1;

            // 计算命中率
            float accuracy = (float)ticksGot / totalJudgements;

            // 钳制在 0~1 之间 (防止逻辑溢出)
            return Mathf.Clamp01(accuracy);
        }

        /// <summary>
        /// 更新视觉反馈 (被射线击中时变色)
        /// </summary>
        private void UpdateVisuals()
        {
            if (tickVisuals == null || tickVisuals.Count == 0) return;

            // 预取数据
            double timePreempt = sliderData.TimePreempt;

            // 直接遍历 List，简单可靠
            for (int i = 0; i < tickVisuals.Count; i++)
            {
                var tickInfo = tickVisuals[i];

                // 如果已经被击中，确保隐藏
                if (tickInfo.data.IsHit)
                {
                    if (tickInfo.gameObject.activeSelf) tickInfo.gameObject.SetActive(false);
                    continue;
                }

                // 如果已经激活了，跳过后续计算（性能优化）
                if (tickInfo.gameObject.activeSelf) continue;

                // 贪吃蛇逻辑
                double timeOffset = tickInfo.data.Time - sliderData.StartTime;
                double snakeDelay = timeOffset / 3.0;

                if (snakeDelay > 400) snakeDelay = 400;

                double appearTime = (sliderData.StartTime - timePreempt) + snakeDelay;

                double forceShowTime = tickInfo.data.Time - (timePreempt * 0.5);

                if (currentMusicTimeCache >= appearTime || currentMusicTimeCache >= forceShowTime)
                {
                    tickInfo.gameObject.SetActive(true);
                }
            }
            if (followBallRenderer == null) return;

            float dist = Vector3.Distance(lastHitPosition, followBall.transform.position);
            float allowedRadius = (sliderWidth * 0.5f) * followRadiusMultiplier;
            bool isEffectiveTracking = isTracking && (dist <= allowedRadius);

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

            float duration = 0.12f; // 动画时长
            float timer = 0f;

            while (timer < duration)
            {
                timer += Time.deltaTime;
                float progress = timer / duration;

                // 使用 Sin 曲线实现 变大->变回原样 (0 -> 1 -> 0)
                // 1.2f 表示最大放大到 1.2 倍
                float scaleMultiplier = 1.0f + Mathf.Sin(progress * Mathf.PI) * 0.2f;

                if (followBall != null)
                {
                    // 关键：基于 baseBallScale 计算，而不是基于当前 localScale
                    followBall.transform.localScale = Vector3.one * (baseBallScale * scaleMultiplier);
                }

                yield return null;
            }

            // 确保恢复原样
            if (followBall != null)
            {
                followBall.transform.localScale = Vector3.one * baseBallScale;
            }

            // 清空协程引用
            pulseCoroutine = null;
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

        /// <summary>
        /// 彻底清理上一轮留下的所有视觉残留
        /// </summary>
        private void CleanUpEverything()
        {
            // 0. 首先清理动态 Mesh 和 Material，防止显存泄漏导致物体消失
            CleanUpMeshes();

            // 1. 清理垃圾桶 (Head, Arrow)
            // 倒序遍历，方便移除
            for (int i = garbageList.Count - 1; i >= 0; i--)
            {
                GameObject obj = garbageList[i];

                if (obj != null)
                {
                    Destroy(obj);
                }
            }
            garbageList.Clear(); // 清空列表，断开所有“尸体”引用

            // 2. 清理 Tick (池化)
            if (tickVisuals != null && tickPool != null)
            {
                // 使用 for 循环或 foreach 遍历 List
                for (int i = 0; i < tickVisuals.Count; i++)
                {
                    GameObject obj = tickVisuals[i].gameObject;
                    if (obj != null)
                    {
                        // 1. 隐藏物体
                        obj.SetActive(false);

                        // 2. 还给池子
                        tickPool.Release(obj);
                    }
                }
                // 3. 彻底清空列表
                tickVisuals.Clear();
            }

            // 3. 重置变量 (防止 CreateVisuals 误用)
            headInstance = null;
            arrowInstance = null;
        }

        /// <summary>
        /// 彻底清理动态生成的 Mesh 和 Material，防止显存泄漏导致物体消失！
        /// </summary>
        private void CleanUpMeshes()
        {
            // 1. 清理本体的 Mesh 和 Material
            if (combinedMesh != null)
            {
                Destroy(combinedMesh);
                combinedMesh = null;
            }
            if (meshRenderer != null && meshRenderer.sharedMaterial != null)
            {
                // 只销毁代码 new 出来的材质，绝不误删预制体自带的 sharedMaterial
                if (meshRenderer.sharedMaterial != sharedMaterial)
                {
                    Destroy(meshRenderer.sharedMaterial);
                }
                meshRenderer.sharedMaterial = null;
            }

            // 2. 清理边框的 Mesh 和 Material
            Transform oldBorder = transform.Find("SliderBorder");
            if (oldBorder != null)
            {
                MeshFilter mf = oldBorder.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null) Destroy(mf.sharedMesh);

                MeshRenderer mr = oldBorder.GetComponent<MeshRenderer>();
                if (mr != null && mr.sharedMaterial != null) Destroy(mr.sharedMaterial);

                Destroy(oldBorder.gameObject);
            }
        }
    }
}
