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

        [Tooltip("滑条边框宽度 (必须比本体宽)")]
        public float borderWidth = 0.06f;

        [Tooltip("滑条高度（轻微凸起效果）")]
        public float sliderHeight = 0.01f;

        [Tooltip("纹理平铺密度（每单位长度重复多少次纹理）")]
        public float textureTiling = 1.0f;

        [Header("判定设置")]
        [Tooltip("跟随判定半径倍率 (osu!标准约为 2x CS半径)")]
        public float followRadiusMultiplier = 2f;

        [Header("渐变效果")]
        [Tooltip("渐隐时间（秒）")]
        public float fadeOutDuration = 0.1f;

        [Tooltip("是否启用渐隐效果")]
        public bool enableFadeOut = true;

        private GameObject headInstance;
        private GameObject arrowInstance;

        [Header("调试设置")]
        public bool showDebugLabel = true;
        public GameObject debugTextPrefab;
        private TextMeshPro debugTextInstance;


        private bool headHitValid = false;

        // 折返粒子特效引用
        private ParticleSystem headReversePS;
        private ParticleSystem tailReversePS;
        private ParticleSystem.ColorOverLifetimeModule headColorOL;
        private ParticleSystem.ColorOverLifetimeModule tailColorOL;
        private bool tailShowing = true;  // 尾部标记当前是否显示
        private bool headShowing = false; // 头部标记当前是否显示
        private Gradient reverseGradient; // 缓存 Gradient，避免每帧 new

        // 用于生成 Vertex Colors
        public Color customBodyColor = new Color(0.2f, 0.6f, 1f, 0.9f); // 默认 osu! 蓝
        public Color customBorderColor = Color.white;

        // 专用材质球 (拖入 Mat_Osu_Slider)
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
        private MeshRenderer borderMeshRenderer; // 用于同步透明度
        private RhythmGameManager gameManager;
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private MeshCollider meshCollider;
        private GameObject followBall;
        private Renderer followBallRenderer; // 缓存球体渲染器
        private Mesh combinedMesh;

        // 记录球体的基础大小，防止吃Tick后变大回不去
        private float baseBallScale = 1.0f;
        // 管理协程，防止连续吃Tick时动画冲突
        private Coroutine pulseCoroutine;
        // 嵌套物件判定索引
        private int currentNestedIndex = 0;
        private int nextVisualIndex = 0;

        // 记录获得了多少个 Tick (用于计算最终分数)
        private int ticksGot = 0;

        // 判定相关变量
        private SphereCollider ballCollider; // 用于射线的碰撞体

        // 淡入效果组件
        private ObjectFadeIn fadeInComponent;

        // 状态变量
      
        private bool isTrackingAudioPlaying = false; // 是否正在播放跟踪音效

        private bool hasStarted = false;     // 滑条是否已经开始
        private bool headHit = false;        // 滑条头是否被击中
        private bool finished = false;       // 滑条是否结束
        // 上一次 TryHitHead 采样到的 (当前时间 - 打击时间)，用于帧量化修正
        private double lastHeadCheckDiff = double.NaN;

        // 公共属性：供 AutoPlayManager 访问
        public bool IsHeadHit => headHit;

        // 双手独立跟踪状态
        private bool isLeftHandTracking = false;
        private bool isRightHandTracking = false;
        private Vector3 leftHandPos;
        private Vector3 rightHandPos;

        // 辅助属性：只要有一只手在跟踪，就视为有效
        public bool isTracking => isLeftHandTracking || isRightHandTracking;

        // 脱离容错计时器（秒）- 允许脱离约1帧（60fps ≈ 16.67ms）后才算断滑条
        private float leftHandGraceTimer = 0f;
        private float rightHandGraceTimer = 0f;
        private const float GRACE_PERIOD_SECONDS = 0.018f; // 约1帧的容错时间

        // 防短滑条断机制：记录最后一次有效跟踪的时间（音乐时间，毫秒）
        // 滑条结束前约2帧内离开判定范围，仍算接住尾巴
        private double lastEffectiveTrackTime = 0;
        private const double TAIL_GRACE_PERIOD_MS = 33.0; // 约2帧的容错时间


        // 避免每帧重复获取时间的缓存
        private double currentMusicTimeCache;

        // 性能优化：材质属性块（防止材质泄露）
        private MaterialPropertyBlock _propBlock;
        private static readonly int ColorPropertyId = Shader.PropertyToID("_Color"); // 缓存Shader属性ID
        private static readonly int PropBaseColor = Shader.PropertyToID("_BaseColor");

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

        // 缓存 Combo 颜色给头部使用
        private Color currentComboColor;

        // 全局唯一的 Stencil ID 计数器
        private int currentStencilId = 1;

        // 用来控制视觉显隐的索引
        private int myRenderIndex = 0;

        // 缓存当前滑条的 baseQueue，供子物体使用
        private int cachedBaseQueue = 3900;

        // 记录下一个 Tick 位置的缓存（优化）
        private Vector3? nextNotePosition;


        // 在 SliderController 类中添加这个列表，用来记录所有生成的子物体（Tick, 箭头等）
        private List<GameObject> garbageList = new List<GameObject>();

        // 通过 r.material 触发的材质实例克隆（渲染队列/ZWrite 定制）。
        // 这些克隆不会随 GameObject 销毁而释放，必须手动 Destroy，否则整局泄漏
        private readonly List<Material> clonedMaterials = new List<Material>();

        private static Texture2D cachedSoftDotTex;
        private static Material cachedReverseMat;

        /// <summary>
        /// 生成实心柔光粒子贴图 (用于打击粒子和折返粒子)
        /// </summary>
        private Texture2D GetSoftDotTexture()
        {
            if (cachedSoftDotTex != null) return cachedSoftDotTex;

            int size = 128; // 与光晕贴图一致大小
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
                        // 三次方衰减：与Note/滑条头光晕完全一致
                        // 中心实心亮白，边缘柔和衰减
                        float v = Mathf.Clamp01(1f - r);
                        alpha = v * v * v; // 三次方让中心更亮，边缘更柔和
                    }

                    colors[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(colors);
            tex.Apply();
            cachedSoftDotTex = tex;
            return tex;
        }


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
            var noise = ps.noise;
            var colOL = ps.colorOverLifetime;
            var limitVel = ps.limitVelocityOverLifetime;
            var renderer = go.GetComponent<ParticleSystemRenderer>();

            // 1. 基础设置：彻底打乱
            main.duration = 1.0f;
            main.loop = true;

            // 寿命范围 (0.4 ~ 0.8)，更长的寿命让粒子更明显
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);

            // 速度极慢且差异大，模拟悬浮微尘
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.0f);
            // 尺寸更大更明显
            float baseSize = sliderWidth * 0.18f;
            main.startSize = new ParticleSystem.MinMaxCurve(baseSize * 0.6f, baseSize * 1.8f);

            // HDR高亮combo色
            Color hdrColor = new Color(color.r * 6.0f, color.g * 6.0f, color.b * 6.0f, 1.0f);
            main.startColor = hdrColor;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            // 2. 形状：体积填充
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.arc = 180f;
            shape.radius = sliderWidth * 0.25f; // 稍微扩大一点范围

            // 设为 1，确保粒子在半圆"内部"随机生成
            shape.radiusThickness = 1.0f;

            // 3. 发射率：持续发射，由 Stop/Play 控制开关
            emission.rateOverTime = 800f;

            // 4. 噪声模块：制造"朦胧"和"扰动"
            noise.enabled = true;
            noise.strength = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
            noise.frequency = 0.5f; // 扰动频率 (低频显得更像烟雾)
            noise.scrollSpeed = 0.5f; // 噪声纹理滚动
            noise.damping = true; // 阻尼，让乱动更柔和

            // 5. 阻力：让粒子飞出来后迅速减速
            limitVel.enabled = true;
            limitVel.limit = 0.1f; // 限制最大速度为 0.1 (几乎静止)
            limitVel.dampen = 0.2f; // 阻尼系数 (0~1)，越大减速越快

            // 6. 透明度渐变 (Fade In -> Fade Out)
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
                if (!shader) shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (!shader) shader = Shader.Find("Standard");
                if (!shader) { Debug.LogError("[SliderController] 反转粒子 Shader 不可用!"); }
                else
                {
                    cachedReverseMat = new Material(shader);
                    cachedReverseMat.renderQueue = 3005;
                    cachedReverseMat.mainTexture = GetSoftDotTexture();

                    Color matHdrColor = Color.white * 6.0f;
                    if (cachedReverseMat.HasProperty("_TintColor")) cachedReverseMat.SetColor("_TintColor", matHdrColor);
                    else if (cachedReverseMat.HasProperty("_BaseColor")) cachedReverseMat.SetColor("_BaseColor", matHdrColor);
                    else cachedReverseMat.SetColor("_Color", matHdrColor);
                }
            }

            // 使用 sharedMaterial
            renderer.sharedMaterial = cachedReverseMat;

            go.SetActive(true);
            garbageList.Add(go);

            ps.Play();
            return ps;
        }

        void OnEnable()
        {
            isActive = true;
            isLeftHandTracking = false;
            isRightHandTracking = false;

            leftHandGraceTimer = 0f;
            rightHandGraceTimer = 0f;
            lastEffectiveTrackTime = 0;

            if (fadeInComponent != null)
            {
                fadeInComponent.ResetState();
            }

            headHit = false;
            lastHeadCheckDiff = double.NaN;
            finished = false;

            // 重置折返标记状态（对象池复用时必须重置）
            tailShowing = true;
            headShowing = false;
        }


        /// <summary>
        /// 初始化滑条控制器 (对象池版)
        /// </summary>
        /// <param name="renderIndex">安全渲染索引 (safeRenderIndex)，由 RhythmGameManager 计算</param>
        public void Initialize(SliderObject sliderData, float beatmapCS, Color comboColor, RhythmGameManager manager, IObjectPool<GameObject> pool, IObjectPool<GameObject> tPool, int renderIndex, Vector3? nextPos = null)
        {
            CleanUpEverything();

            this.myRenderIndex = renderIndex;

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

            // ---------------------------------------------------------
            // 纯 2D 平面模式：所有滑条基准物理 Z 轴偏移为 0
            // 遮挡关系 100% 交由 RenderQueue 接管
            // 十步长画家算法：
            // - Body = baseQueue + 1
            // - Border = baseQueue + 2
            // - Tick = baseQueue + 3
            // - Halo = baseQueue + 4
            // - Head/Body = baseQueue + 5
            // - Overlay = baseQueue + 6
            // - FollowBall = baseQueue + 7
            // - ApproachCircle = baseQueue + 8 (最高层，置顶)
            // ---------------------------------------------------------
            Vector3 startPos = CoordinateMapper.MapToWorld(sliderData.Position);
            transform.position = startPos;

            // CS 尺寸计算
            float finalSize = RhythmGameManager.CalculateVROsuSize(beatmapCS);
            this.sliderWidth = finalSize;
            this.borderWidth = finalSize * 1.25f;

            // 颜色设置 (腹黑滑条)
            this.customBodyColor = new Color(0.05f, 0.05f, 0.05f, 0.7f);
            this.customBorderColor = new Color(0.5f, 0.5f, 0.5f, 0.9f);

            

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
                    // 运行时判定状态重置：重试时复用的是同一份解析期数据，
                    // 上一局写入的 IsHit 会导致本局 Tick 视觉被 UpdateVisuals 隐藏
                    nested.IsHit = false;

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

            // 初始化淡入效果（必须在所有视觉元素创建和颜色设置之后）
            if (fadeInComponent == null)
            {
                fadeInComponent = gameObject.GetComponent<ObjectFadeIn>();
                if (fadeInComponent == null)
                    fadeInComponent = gameObject.AddComponent<ObjectFadeIn>();
            }
            // 使用 SliderBody 模式，这样本体会延迟渐隐
            fadeInComponent.Initialize(sliderData.StartTime, sliderData.TimePreempt, manager, FadeMode.SliderBody, sliderData.EndTime);

            // 重置计数器
            currentNestedIndex = 0;
            nextVisualIndex = 0;
            currentMusicTimeCache = gameManager.GetCurrentMusicTimeMs();

            ticksGot = 0;
            headHit = false;
            lastHeadCheckDiff = double.NaN;
            finished = false;
            currentAlpha = 1f;
            UpdateMaterialAlpha();
            if (combinedMesh != null) combinedMesh.RecalculateBounds();
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
            if (_propBlock != null) _propBlock.Clear();

            headHit = false;
            lastHeadCheckDiff = double.NaN;
            finished = false;

            isLeftHandTracking = false;
            isRightHandTracking = false;

            leftHandGraceTimer = 0f;
            rightHandGraceTimer = 0f;
            lastEffectiveTrackTime = 0;

            // 4. 处理子物体
            if (headInstance) headInstance.SetActive(false);
            if (arrowInstance) arrowInstance.SetActive(false);
            if (followBall) followBall.SetActive(false);

            // 销毁旧的调试文本
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

            // ---------------------------------------------------------
            // 机制2: 十步长画家算法
            // 目标：确保先生成的物件永远盖在后生成的物件之上
            // ---------------------------------------------------------
            // 动态生成唯一模板 ID（限制在 8-bit 内，1-254）
            currentStencilId = (this.myRenderIndex % 254) + 1;

            // 基准队列：3900 - safeRenderIndex * 10
            int baseQueue = 3900 - (this.myRenderIndex * 10);
            this.cachedBaseQueue = baseQueue;

            // 层级分配法则：
            // - 滑条本体 (Body) = baseQueue + 1 (垫底)
            // - 滑条边框 (Border) = baseQueue + 2 (盖在本体上)
            // - Tick = baseQueue + 3
            // - 头部圈圈 (Head) = baseQueue + 5 ~ +8

            var (borderMesh, bodyMesh, borderMat, bodyMat) = SliderMeshGenerator.GeneratePhysicalSlider(
                    worldPathPoints, radius, borderThickness, customBorderColor, customBodyColor, currentStencilId);

            bodyMesh.RecalculateBounds();
            borderMesh.RecalculateBounds();
            Bounds safeBounds = bodyMesh.bounds;
            safeBounds.Expand(15f); // 强行扩充 15 米的视野范围
            bodyMesh.bounds = safeBounds;
            borderMesh.bounds = safeBounds;

            // 3. 渲染主体网格
            // 滑条本体 = baseQueue + 1 (垫底)
            combinedMesh = bodyMesh;
            if (meshFilter) meshFilter.mesh = combinedMesh;
            if (meshRenderer)
            {
                bodyMat.renderQueue = baseQueue + 1;
                meshRenderer.sharedMaterial = bodyMat;
            }

            // 4. 渲染边框网格
            // 滑条边框 = baseQueue + 2 (盖在本体上)
            GameObject borderObject = new GameObject("SliderBorder");
            borderObject.transform.SetParent(transform, false);
            borderObject.transform.localPosition = Vector3.zero;
            borderObject.transform.localRotation = Quaternion.identity;

            MeshFilter borderMeshFilter = borderObject.AddComponent<MeshFilter>();
            borderMeshFilter.mesh = borderMesh;

            borderMat.renderQueue = baseQueue + 2;

            borderMeshRenderer = borderObject.AddComponent<MeshRenderer>();
            borderMeshRenderer.sharedMaterial = borderMat;

            // 调试校验逻辑
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
        /// 创建滑条视觉元素：滑条头、Tick小点、折返箭头粒子
        /// 支持对象池和纯代码生成双模式
        /// </summary>
        private void CreateVisuals()
        {
            if (this == null || this.gameObject == null) return;

            var poolMgr = NotePoolManager.Instance;
            bool usePool = poolMgr != null;

            // =========================================================
            // 1. 创建滑条头 (Slider Head)
            // 优先从对象池获取，否则使用工厂类纯代码生成
            // =========================================================
            if (usePool)
            {
                headInstance = poolMgr.GetSliderHead();
                if (headInstance != null)
                {
                    headInstance.transform.SetParent(transform);
                    garbageList.Add(headInstance);
                }
            }
            else
            {
                headInstance = HitObjectFactory.CreateSliderHead();
                if (headInstance != null)
                {
                    headInstance.transform.SetParent(transform);
                    garbageList.Add(headInstance);
                }
            }

            // =========================================================
            // 2. 配置滑条头视觉属性
            // 纯 2D 平面模式：滑条头与本体在同一 Z 平面
            // 遮挡关系由 RenderQueue 控制 (头部 = baseQueue + 5 ~ +7)
            // =========================================================
            if (headInstance != null)
            {
                // 设置位置和缩放
                headInstance.transform.position = transform.position;
                
                float headScale = this.sliderWidth;
                headInstance.transform.localScale = new Vector3(headScale, headScale, 0.02f);
                headInstance.SetActive(true);

                // 机制2: 十步长画家算法
                // 头部圈圈子图层分配：
                // - Halo = baseQueue + 4
                // - Body/SliderHead = baseQueue + 5
                // - Overlay = baseQueue + 6
                // - FollowBall = baseQueue + 7
                // - ApproachCircle = baseQueue + 8 (最高层，置顶)
                int baseQueue = 3900 - (this.myRenderIndex * 10);

                // 计算 HDR 高亮颜色
                float headGlowIntensity = 2.3f;
                Color hdrComboColor = new Color(
                    currentComboColor.r * headGlowIntensity,
                    currentComboColor.g * headGlowIntensity,
                    currentComboColor.b * headGlowIntensity,
                    1.0f
                );

                // 应用 Combo 颜色到所有渲染器
                Renderer[] headRenderers = headInstance.GetComponentsInChildren<Renderer>();
                MaterialPropertyBlock headMbp = new MaterialPropertyBlock();

                foreach (var r in headRenderers)
                {
                    if (r == null) continue;

                    int targetQueue = baseQueue + 5;
                    string objName = r.gameObject.name;
                    if (objName.Contains("Halo")) targetQueue = baseQueue + 4;
                    else if (objName.Contains("Body") || objName.Contains("SliderHead")) targetQueue = baseQueue + 5;
                    else if (objName.Contains("Overlay")) targetQueue = baseQueue + 6;
                    else if (objName.Contains("ApproachCircle")) targetQueue = baseQueue + 8;

                    // .material 首次访问会克隆共享材质（本滑条专属渲染队列），需登记待销毁
                    Material headMat = r.material;
                    headMat.renderQueue = targetQueue;
                    headMat.SetInt("_ZWrite", 0);
                    clonedMaterials.Add(headMat);

                    r.GetPropertyBlock(headMbp);
                    headMbp.SetColor(ColorPropertyId, hdrComboColor);
                    headMbp.SetColor(PropBaseColor, hdrComboColor);
                    r.SetPropertyBlock(headMbp);
                }

            // 初始化缩圈动画组件
            var scaler = headInstance.GetComponent<ApproachCircleScaler>();
            if (scaler != null)
            {
                double arMs = sliderData.TimePreempt;
                if (arMs < 100 && gameManager != null)
                    arMs = gameManager.spawnOffsetMs;
                scaler.Initialize(sliderData.StartTime, arMs, gameManager);
            }

                // =========================================================
                // 3. 动态生成滑条头光晕效果
                // 使用程序化生成的空心光环贴图，实现边缘柔和的发光效果
                // =========================================================
                Transform bodyTr = headInstance.transform.Find("Body");
                GameObject targetBody = bodyTr != null ? bodyTr.gameObject : headInstance;

                GameObject headHalo = new GameObject("Head_Halo");
                headHalo.transform.SetParent(targetBody.transform);
                garbageList.Add(headHalo);

                // 使用工厂缓存的 Quad Mesh
                var dstMF = headHalo.AddComponent<MeshFilter>();
                dstMF.sharedMesh = HitObjectFactory.GetQuadMesh();

                // 使用工厂缓存的光晕材质（含程序化生成的空心光环贴图）
                var dstMR = headHalo.AddComponent<MeshRenderer>();
                dstMR.material = HitObjectFactory.GetHaloMaterial();
                // 工厂材质是全滑条共享资产，改 renderQueue 前的 .material 访问会产生克隆
                Material haloMat = dstMR.material;
                haloMat.renderQueue = baseQueue + 4;
                clonedMaterials.Add(haloMat);

                // 光晕变换：1.25倍大小，纯 2D 平面模式
                headHalo.transform.localPosition = Vector3.zero;
                headHalo.transform.localRotation = Quaternion.identity;
                headHalo.transform.localScale = Vector3.one * 1.25f;

                // 禁用阴影投射，保持纯净的发光效果
                dstMR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                // 初始化滑条头的独立淡入效果
                // [Fix] 必须在光晕(Head_Halo)创建之后初始化，否则 ObjectFadeIn
                // 的缓存扫描不会包含光晕的 Renderer，导致 HD 模式下光晕无法淡出。
                // 对齐 NoteController 的做法：先 CreateHalo，再 Initialize ObjectFadeIn。
                var headFadeIn = headInstance.GetComponent<ObjectFadeIn>();
                if (headFadeIn == null)
                    headFadeIn = headInstance.AddComponent<ObjectFadeIn>();
                headFadeIn.Initialize(sliderData.StartTime, sliderData.TimePreempt, gameManager, FadeMode.Standard);
            }

            // =========================================================
            // 4. 生成滑条 Tick 小点
            // Tick 沿滑条路径分布，玩家跟随时需要经过
            // =========================================================
            if (sliderData.NestedHitObjects != null)
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
                            tickObj.SetActive(false); // 初始隐藏，由 UpdateVisuals 控制

                            // Tick 大小为滑条宽度的 30%
                            float tickScale = this.sliderWidth * 0.3f;
                            tickObj.transform.localScale = new Vector3(tickScale, tickScale, tickScale);

                            // 根据 Tick 时间计算在路径上的位置
                            Vector3 tickPos = GetPositionAtTime(nested.Time);
                            tickObj.transform.localPosition = tickPos;
                            tickObj.GetComponent<Renderer>().material.renderQueue = this.cachedBaseQueue + 3;

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
                // 基于本地坐标的 2D 旋转计算

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
                if (headReversePS)
                {
                    headColorOL = headReversePS.colorOverLifetime;
                    // 头部初始透明（不可见）
                    SetReverseAlpha(headColorOL, currentComboColor, 0f);
                }


                // 2. 尾部粒子 (位置：point[last])
                int last = worldPathPoints.Count - 1;
                Vector3 tailLocalDir = (worldPathPoints[last] - worldPathPoints[last - 1]);
                float tailAngle = Mathf.Atan2(tailLocalDir.y, tailLocalDir.x) * Mathf.Rad2Deg;
                Quaternion tailRot = transform.rotation * Quaternion.Euler(0, 0, tailAngle - 90f);
                Vector3 tailWorldPos = transform.TransformPoint(worldPathPoints[last]);
                tailReversePS = CreateReverseParticle(tailWorldPos, tailRot, currentComboColor);
                if (tailReversePS)
                {
                    tailColorOL = tailReversePS.colorOverLifetime;
                    // 尾部初始可见
                    SetReverseAlpha(tailColorOL, currentComboColor, 0.75f);
                }
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
                if (headReversePS) { var em = headReversePS.emission; em.enabled = false; headReversePS.Clear(); }
                if (tailReversePS) { var em = tailReversePS.emission; em.enabled = false; tailReversePS.Clear(); }
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
                bool wasEnabled = em.enabled;
                em.enabled = showTail;

                if (showTail && !wasEnabled)
                {
                    // 刚开启：瞬间发射，消除前摇
                    tailReversePS.Emit(30);
                }
                else if (!showTail && wasEnabled)
                {
                    // 刚关闭：立刻清除残留粒子，消除延迟消失感
                    tailReversePS.Clear();
                }
            }

            // --- 控制头部 ---
            if (headReversePS)
            {
                var em = headReversePS.emission;
                bool wasEnabled = em.enabled;
                em.enabled = showHead;

                if (showHead && !wasEnabled)
                {
                    headReversePS.Emit(30);
                }
                else if (!showHead && wasEnabled)
                {
                    headReversePS.Clear();
                }
            }
        }

        /// <summary>
        /// 设置折返粒子的透明度（0=不可见，1=完全可见）
        /// 通过 colorOverLifetime 的 alpha 通道控制
        /// </summary>
        private void SetReverseAlpha(ParticleSystem.ColorOverLifetimeModule colorOL, Color baseColor, float alpha)
        {
            if (reverseGradient == null) reverseGradient = new Gradient();
            Color c = baseColor * 6.0f; // HDR 亮度
            c.a = alpha;
            reverseGradient.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(new Color(c.r, c.g, c.b), 0f),
                    new GradientColorKey(new Color(c.r, c.g, c.b), 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(alpha, 0.2f),
                    new GradientAlphaKey(alpha, 0.6f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOL.color = reverseGradient;
        }

        /// <summary>
        /// 折返标记：时间驱动 + alpha 控制可见性
        /// 两个粒子系统始终 Play + 持续发射，通过 alpha 0/1 切换可见性
        ///
        /// 尾部标记：尾部位置（偶数 span）还有后续 Repeat → 保持显示
        /// 头部标记：头部位置（奇数 span）还有后续 Repeat 且 headHit → 保持显示
        /// 自己位置的最后一个 Repeat 打完才关闭
        /// </summary>
        private void UpdateReverseMarkerByTime()
        {
            if (sliderData == null || sliderData.RepeatCount <= 1) return;
            if (headReversePS == null || tailReversePS == null) return;

            // 滑条还没开始，保持初始状态
            if (currentMusicTimeCache < sliderData.StartTime) return;

            // 滑条结束，隐藏所有
            if (currentMusicTimeCache > sliderData.EndTime)
            {
                if (tailShowing) { tailShowing = false; SetReverseAlpha(tailColorOL, currentComboColor, 0f); tailReversePS.Clear(); }
                if (headShowing) { headShowing = false; SetReverseAlpha(headColorOL, currentComboColor, 0f); headReversePS.Clear(); }
                return;
            }

            // 当前处于第几个 span（基于时间）
            double totalDur = sliderData.EndTime - sliderData.StartTime;
            double spanDur = totalDur / sliderData.RepeatCount;
            double timeSinceStart = currentMusicTimeCache - sliderData.StartTime;
            int currentSpan = Mathf.Clamp((int)((timeSinceStart + 0.01) / spanDur), 0, sliderData.RepeatCount - 1);

            // 尾部位置（偶数 span）是否还有未处理的 Repeat（当前或之后）
            bool tailHasRepeat = false;
            for (int i = currentNestedIndex; i < sliderData.NestedHitObjects.Count; i++)
            {
                var obj = sliderData.NestedHitObjects[i];
                if (obj.Type == SliderEventType.Repeat && obj.SpanIndex % 2 == 0)
                {
                    tailHasRepeat = true;
                    break;
                }
            }

            // 头部位置（奇数 span）是否还有未处理的 Repeat（当前或之后）
            bool headHasRepeat = false;
            for (int i = currentNestedIndex; i < sliderData.NestedHitObjects.Count; i++)
            {
                var obj = sliderData.NestedHitObjects[i];
                if (obj.Type == SliderEventType.Repeat && obj.SpanIndex % 2 == 1)
                {
                    headHasRepeat = true;
                    break;
                }
            }

            // 尾部标记：当前在前往尾部 或 尾部还有未处理的 Repeat → 保持显示
            // 最后一程不显示折返符号（已无下一段可折返）
            bool isLastSpan = (currentSpan == sliderData.RepeatCount - 1);
            bool headingToTail = (currentSpan % 2 == 0);
            bool showTail = (headingToTail && !isLastSpan) || tailHasRepeat;
            // 头部标记：headHit 且 头部还有未处理的 Repeat
            bool showHead = headHit && headHasRepeat;

            // 更新尾部标记
            if (showTail != tailShowing)
            {
                tailShowing = showTail;
                if (showTail) { SetReverseAlpha(tailColorOL, currentComboColor, 0.75f); }
                else { SetReverseAlpha(tailColorOL, currentComboColor, 0f); tailReversePS.Clear(); }
            }

            // 更新头部标记
            if (showHead != headShowing)
            {
                headShowing = showHead;
                if (showHead) { SetReverseAlpha(headColorOL, currentComboColor, 0.75f); }
                else { SetReverseAlpha(headColorOL, currentComboColor, 0f); headReversePS.Clear(); }
            }
        }


        private void CreateFollowBall()
        {
            if (followBall == null)
            {
                var poolMgr = NotePoolManager.Instance;

                if (poolMgr != null)
                {
                    followBall = poolMgr.GetFollowBall();
                    if (followBall != null)
                    {
                        followBall.transform.SetParent(transform);
                    }
                }
                else
                {
                    followBall = HitObjectFactory.CreateFollowBall();
                    if (followBall != null)
                    {
                        followBall.transform.SetParent(transform);
                    }
                }

                if (followBall != null && garbageList != null)
                {
                    garbageList.Add(followBall);
                }

                baseBallScale = sliderWidth;
                if (followBall != null)
                {
                    followBall.transform.localScale = Vector3.one * baseBallScale;
                    followBallRenderer = followBall.GetComponent<Renderer>();
                    if (followBallRenderer != null)
                    {
                        // .material 克隆（专属渲染队列），登记待销毁
                        Material ballMat = followBallRenderer.material;
                        ballMat.renderQueue = this.cachedBaseQueue + 7;
                        clonedMaterials.Add(ballMat);
                    }
                    ballCollider = followBall.GetComponent<SphereCollider>();
                    if (ballCollider == null) ballCollider = followBall.AddComponent<SphereCollider>();
                    followBall.SetActive(false);
                }
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
                followBall.transform.localPosition = targetPos;
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

            // 隐藏折返标记
            if (headReversePS) { SetReverseAlpha(headColorOL, currentComboColor, 0f); headReversePS.Clear(); }
            if (tailReversePS) { SetReverseAlpha(tailColorOL, currentComboColor, 0f); tailReversePS.Clear(); }
            headShowing = false;
            tailShowing = false;
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
                _propBlock.SetColor(PropBaseColor, c);
                meshRenderer.SetPropertyBlock(_propBlock);
            }

            // 2. 更新边框透明度
            if (borderMeshRenderer)
            {
                borderMeshRenderer.GetPropertyBlock(_propBlock);
                Color bc = customBorderColor;

                Color hdrBorder = new Color(bc.r * 2.0f, bc.g * 2.0f, bc.b * 2.0f, 1f);

                hdrBorder.a = bc.a * currentAlpha;

                _propBlock.SetColor(ColorPropertyId, hdrBorder);
                // 确保兼容 URP/Unlit
                _propBlock.SetColor(PropBaseColor, hdrBorder);

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
            // 优先处理渐隐逻辑
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

            // 2. 更新容错计时器（在判定之前）
            UpdateGraceTimers();

            // 3. 判定逻辑 (头判、Tick、尾判)
            UpdateJudgement();

            // 4. 视觉反馈
            UpdateVisuals();


            if (isTracking && isActive)
            {
                // 1. 震动 (持续的微震)
                if (HapticManager.Instance != null)
                {
                    // [修改] 谁在摸就震谁
                    if (isRightHandTracking)
                        HapticManager.Instance.PlayContinuous(true, HapticManager.Instance.profile.SliderSlideIntensity);

                    if (isLeftHandTracking)
                        HapticManager.Instance.PlayContinuous(false, HapticManager.Instance.profile.SliderSlideIntensity);
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

            // 5. 时间驱动折返标记（独立于判定事件，0延迟）
            UpdateReverseMarkerByTime();
        }

        /// <summary>
        /// 更新容错计时器 - 实现1帧容错机制
        /// 当手脱离2x判定范围时，给予约1帧的缓冲时间
        /// </summary>
        private void UpdateGraceTimers()
        {
            if (followBall == null || !headHit) return;

            float allowedRadius = (sliderWidth * 0.5f) * followRadiusMultiplier;
            float dt = Time.deltaTime;
            bool anyHandEffective = false;

            // 处理左手
            if (isLeftHandTracking)
            {
                float dist = Vector3.Distance(leftHandPos, followBall.transform.position);
                if (dist <= allowedRadius)
                {
                    leftHandGraceTimer = GRACE_PERIOD_SECONDS;
                    anyHandEffective = true;
                }
                else
                {
                    leftHandGraceTimer -= dt;
                }
            }
            else
            {
                leftHandGraceTimer -= dt;
            }

            // 处理右手
            if (isRightHandTracking)
            {
                float dist = Vector3.Distance(rightHandPos, followBall.transform.position);
                if (dist <= allowedRadius)
                {
                    rightHandGraceTimer = GRACE_PERIOD_SECONDS;
                    anyHandEffective = true;
                }
                else
                {
                    rightHandGraceTimer -= dt;
                }
            }
            else
            {
                rightHandGraceTimer -= dt;
            }

            // 记录最后一次有效跟踪的时间（用于防短滑条断机制）
            if (anyHandEffective)
            {
                lastEffectiveTrackTime = currentMusicTimeCache;
            }

            // 钳制到非负值
            leftHandGraceTimer = Mathf.Max(0, leftHandGraceTimer);
            rightHandGraceTimer = Mathf.Max(0, rightHandGraceTimer);
        }

        /// <summary>
        /// 检查手是否在有效跟踪状态（考虑容错期）
        /// </summary>
        private bool IsHandEffectivelyTracking(bool isRightHand)
        {
            if (isRightHand)
            {
                return isRightHandTracking || rightHandGraceTimer > 0;
            }
            else
            {
                return isLeftHandTracking || leftHandGraceTimer > 0;
            }
        }

        public void OnRayExit(bool isRightHand)
        {
            // 只重置对应手柄的状态
            if (isRightHand)
                isRightHandTracking = false;
            else
                isLeftHandTracking = false;
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
            // 独立记录每只手的位置和状态
            if (isRightHand)
            {
                isRightHandTracking = true;
                rightHandPos = hitPosition;
            }
            else
            {
                isLeftHandTracking = true;
                leftHandPos = hitPosition;
            }

            // 尝试判定滑条头 (传入当前这只手的位置)
            if (!headHit)
            {
                TryHitHead(isRightHand, hitPosition);
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
        public void TryHitHead(bool isRightHand, Vector3 hitPos)
        {
            if (headHit) return;

            // 计算偏移量：当前时间 - 预期时间
            // 负数 = 提前 (Early), 正数 = 延迟 (Late)
            double offset = currentMusicTimeCache - sliderData.StartTime;

            // [核心修复] 计算击中点到 "滑条头中心" 的距离
            // 注意：这里不能用 followBall，因为头判定时球可能还没生成或位置不对
            Vector3 headWorldPos = transform.position; // 滑条挂载点即为起点
            float distToHead = Vector3.Distance(hitPos, headWorldPos);

            // 允许的半径：滑条半径 * 宽松系数 (Relax可以稍微宽一点，比如 2.0x ~ 3.0x)
            float allowedRadius = (sliderWidth * 0.5f) * 3.0f;

            // 只有在半径内才允许判定
            if (distToHead > allowedRadius)
            {
                return;
            }

            double prevHeadCheckDiff = lastHeadCheckDiff;
            lastHeadCheckDiff = offset;

            // AutoPlay 模式：允许最多提前 16ms 判定（约一帧），确保精确同步
            // 正常模式：最早判定区间为 -13ms (osu! 标准的提前判定窗口)
            // 加上音效延迟补偿，使音效与视觉打击同步
            double audioLatencyCompensation = 20.0;
            if (AudioManager.Instance != null)
            {
                audioLatencyCompensation = AudioManager.Instance.audioLatencyCompensation;
            }

            bool isAutoPlay = gameManager != null && gameManager.useAutoPlay;
            double earlyWindow = isAutoPlay ? -16 : -13;
            // 判定窗口随谱面 OD 变化（OD8=250ms 基准，OD 越高越早 Miss）；无 Manager 时回退 250ms
            double hitWindowMs = gameManager != null ? gameManager.JudgementWindowMs : 250.0;

            if (offset >= earlyWindow && offset <= hitWindowMs)
            {
                headHit = true;

                if (isRightHand) isRightHandTracking = true;
                else isLeftHandTracking = true;

                // 2. 视觉与触觉反馈
                if (followBall)
                {
                    followBall.SetActive(true);
                    StartCoroutine(FollowBallPulse());
                }
                // Head 使用节点索引 0 的音效
                if (AudioManager.Instance != null) AudioManager.Instance.PlaySliderNodeSound(sliderData, 0);
                // 音量 = TimingPoint音量 × 样本倍率
                float vol = (sliderData.TimingPointVolume / 100f) * (sliderData.SampleVolume / 100f);

                if (HapticManager.Instance != null) HapticManager.Instance.PlayHitHaptic(isRightHand, (int)sliderData.HitSound, vol);
                if (CodeOnlyVFX.Instance != null)
                {
                    double absDiff01 = 1.0 - (System.Math.Abs(offset) / hitWindowMs);
                    int vfxScore = RhythmGameManager.CalculateScoreFromAccuracy(System.Math.Clamp(absDiff01, 0.0, 1.0));
                    CodeOnlyVFX.Instance.PlayHit(transform.position, transform.rotation, this.sliderWidth, currentComboColor, this.nextNotePosition, vfxScore);
                }

                headHitValid = true;
                ticksGot++;

                // 3. 计算动态分数 (300/100/50)
                // AutoPlay 模式：强制判定时间为 0ms
                double effectiveOffset = isAutoPlay ? 0 : offset;
                // 帧量化修正：判定窗口在本帧与上次检查之间开启（玩家早已在半径内悬停）时，
                // 真实命中时刻即窗口边界，而非本帧采样时间（消除 0~1 帧量化误差）
                // 两次检查间隔大于 40ms 说明射线中途离开过，无法用上次采样推断，回退到当前采样
                if (!isAutoPlay && !double.IsNaN(prevHeadCheckDiff)
                    && prevHeadCheckDiff < earlyWindow && (offset - prevHeadCheckDiff) <= 40.0)
                    effectiveOffset = earlyWindow;
                double maxWindow = hitWindowMs;
                double absDiff = System.Math.Abs(effectiveOffset);
                double accuracy01 = 1.0 - (absDiff / maxWindow);
                accuracy01 = System.Math.Clamp(accuracy01, 0.0, 1.0);

                int headScore = RhythmGameManager.CalculateScoreFromAccuracy(accuracy01);
                if (headScore == 0) headScore = 50; // 只要接住就给保底 50 分

                // 4. 提交分数
                if (gameManager != null && gameManager.scoreManager != null)
                {
                    gameManager.scoreManager.RegisterHit(headScore);
                }

                // 5. ShowJudgement 调用
                if (JudgementVisualizer.Instance != null)
                {
                    JudgementVisualizer.Instance.ShowJudgement(transform.position, headScore, currentComboColor);
                }

#if UNITY_EDITOR
                Debug.Log($"<color=green>Slider Head HIT!</color> Offset: {offset:F2}ms, Score: {headScore}");
#endif
            }
            else if (offset < earlyWindow)
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

            // AutoPlay 模式判定窗口
            // 加上音效延迟补偿，使音效与视觉打击同步
            double audioLatencyCompensation = 20.0;
            if (AudioManager.Instance != null)
            {
                audioLatencyCompensation = AudioManager.Instance.audioLatencyCompensation;
            }

            bool isAutoPlay = gameManager != null && gameManager.useAutoPlay;
            double earlyWindow = isAutoPlay ? 0 : -13;

            // -------------------------------------------------------------
            // 1. 头部判定 (Head)
            // -------------------------------------------------------------
            if (!headHit)
            {
                double diff = currentMusicTimeCache - sliderData.StartTime;

                // 安全计算单次折返的持续时间（防止 RepeatCount 为 0 导致报错）
                double spanDuration = sliderData.RepeatCount > 0
                    ? (sliderData.EndTime - sliderData.StartTime) / sliderData.RepeatCount
                    : (sliderData.EndTime - sliderData.StartTime);

                // 触发 Head Miss 的三大条件：
                // 1. 时间超过判定窗口 (随 OD 变化，OD8=250ms 基准)
                // 2. 球已经跑完了一个折返段的时间 (针对快速滑条，球走远了算漏)
                // 3. 整个滑条已经彻底结束 (解决极短滑条不显示 Miss 也不消失的问题)
                double headWindowMs = gameManager != null ? gameManager.JudgementWindowMs : 250.0;
                bool isTimeoutMiss = (diff > headWindowMs) || (diff > spanDuration && diff > 0) || (currentMusicTimeCache >= sliderData.EndTime);

                if (isTimeoutMiss)
                {
                    headHit = true; // 锁定状态，防止重复触发
#if UNITY_EDITOR
                    Debug.Log($"<color=red>Slider Head MISS</color>");
#endif

                    // 1. 立即隐藏滑条头 (视觉上 Head 直接消失)
                    if (headInstance != null) headInstance.SetActive(false);

                    // 2. 告诉分数系统：断连了 (扣血/断Combo)
                    if (gameManager != null && gameManager.scoreManager != null)
                    {
                        gameManager.scoreManager.RegisterMiss(300);
                    }

                    // 3. 弹小红叉文字 (位置在滑条头当前位置)
                    if (JudgementVisualizer.Instance != null)
                    {
                        JudgementVisualizer.Instance.ShowJudgement(transform.position, 0, Color.red);
                    }
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

                // [修改] 使用容错机制判定
                if (followBall != null)
                {
                    float allowedRadius = (sliderWidth * 0.5f) * followRadiusMultiplier;
                    float minDist = float.MaxValue;

                    // 检查左手（考虑容错期）
                    bool leftEffective = IsHandEffectivelyTracking(false);
                    if (leftEffective && isLeftHandTracking)
                    {
                        minDist = Mathf.Min(minDist, Vector3.Distance(leftHandPos, followBall.transform.position));
                    }

                    // 检查右手（考虑容错期）
                    bool rightEffective = IsHandEffectivelyTracking(true);
                    if (rightEffective && isRightHandTracking)
                    {
                        minDist = Mathf.Min(minDist, Vector3.Distance(rightHandPos, followBall.transform.position));
                    }

                    // 只要任意手在容错期内且距离有效，就算hit
                    if ((leftEffective || rightEffective) && minDist <= allowedRadius)
                    {
                        hit = true;
                    }

                    // 防短滑条断机制：Tail特殊处理
                    // 如果最后有效跟踪时间在滑条结束前约2帧内，仍算接住尾巴
                    if (!hit && nestedObject.Type == SliderEventType.Tail)
                    {
                        double timeSinceLastTrack = currentMusicTimeCache - lastEffectiveTrackTime;
                        double timeUntilEnd = sliderData.EndTime - lastEffectiveTrackTime;

                        // 条件：曾经有效跟踪过，且最后一次跟踪在结束前2帧内
                        if (lastEffectiveTrackTime > sliderData.StartTime && timeUntilEnd <= TAIL_GRACE_PERIOD_MS)
                        {
                            hit = true;
                        }
                    }
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

                            if (HapticManager.Instance != null)
                            {
                                float tickVol = (sliderData.TimingPointVolume / 100f) * (sliderData.SampleVolume / 100f);
                                if (isRightHandTracking) HapticManager.Instance.PlaySliderTick(true, tickVol);
                                if (isLeftHandTracking) HapticManager.Instance.PlaySliderTick(false, tickVol);
                            }
                            if (AudioManager.Instance != null)
                            {
                                float tickVol = (sliderData.TimingPointVolume / 100f) * (sliderData.SampleVolume / 100f);
                                AudioManager.Instance.PlaySliderTick(sliderData.SampleSet, sliderData.CustomIndex, tickVol);
                            }

                            if (gameManager?.scoreManager != null) gameManager.scoreManager.RegisterComboHit(10);

                            // 🚫 绝对不要在这里加 ShowJudgement！Tick 不弹字！
                            break;

                        case SliderEventType.Repeat:
                            // Repeat 使用节点索引 = SpanIndex + 1
                            int repeatNodeIndex = nestedObject.SpanIndex + 1;
                            if (AudioManager.Instance != null) AudioManager.Instance.PlaySliderNodeSound(sliderData, repeatNodeIndex);
                            if (HapticManager.Instance != null)
                            {
                                float vol = (sliderData.TimingPointVolume / 100f) * (sliderData.SampleVolume / 100f);
                                int soundType = (int)sliderData.HitSound;
                                if (isRightHandTracking) HapticManager.Instance.PlayHitHaptic(true, soundType, vol);
                                if (isLeftHandTracking) HapticManager.Instance.PlayHitHaptic(false, soundType, vol);
                            }

                            if (CodeOnlyVFX.Instance != null)
                            {
                                bool atTail = (nestedObject.SpanIndex % 2 == 0);
                                Vector3 vfxLocalPos = atTail ? worldPathPoints[worldPathPoints.Count - 1] : worldPathPoints[0];
                                CodeOnlyVFX.Instance.PlayHit(transform.TransformPoint(vfxLocalPos), transform.rotation, this.sliderWidth, currentComboColor, this.nextNotePosition, 300);
                            }

                            if (gameManager?.scoreManager != null) gameManager.scoreManager.RegisterComboHit(30);
                            break;

                        case SliderEventType.Tail:
                            // Tail 使用节点索引 = RepeatCount + 1
                            int tailNodeIndex = sliderData.RepeatCount + 1;
                            if (AudioManager.Instance != null) AudioManager.Instance.PlaySliderNodeSound(sliderData, tailNodeIndex);
                            // 尾巴发爆破粒子
                            if (CodeOnlyVFX.Instance != null)
                            {
                                bool endsAtTail = (sliderData.RepeatCount % 2 != 0);
                                Vector3 endLocalPos = endsAtTail ? worldPathPoints[worldPathPoints.Count - 1] : worldPathPoints[0];
                                CodeOnlyVFX.Instance.PlayHit(transform.TransformPoint(endLocalPos), transform.rotation, this.sliderWidth, currentComboColor, this.nextNotePosition, 300);
                            }
                            break;
                    }
                }
                else
                {
                    // --- MISS 处理 ---
                    // 折返标记已由 UpdateReverseMarkerByTime() 在 Update() 中实时驱动，无需在此更新
                    if (nestedObject.Type == SliderEventType.Repeat)
                    {
                        // 标记更新已由时间驱动处理
                    }
                    else if (nestedObject.Type == SliderEventType.Tail)
                    {
                        // 只有尾巴漏了，才在尾巴的真实坐标显示小红叉
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

                        // 按物件统计：滑条内部扣分不重复计 Miss，只扣分断连
                        gameManager.scoreManager.RegisterMissScoreOnly(maxScore);
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

                    // 注册滑条统计结果
                    if (gameManager?.scoreManager != null)
                    {
                        bool isPerfect = finalAcc > 0.9f;
                        bool isOk = finalAcc > 0.5f;
                        gameManager.scoreManager.RegisterSliderResult(isPerfect, isOk);
                    }

                    if (ticksGot > 0)
                    {
                        // 滑条成功完成：Tail 音效已在 UpdateJudgement 的 Tail case 中播放
                        if (HapticManager.Instance != null)
                        {
                            float vol = (sliderData.TimingPointVolume / 100f) * (sliderData.SampleVolume / 100f);
                            int soundType = (int)sliderData.HitSound;
                            if (isRightHandTracking) HapticManager.Instance.PlayHitHaptic(true, soundType, vol);
                            if (isLeftHandTracking) HapticManager.Instance.PlayHitHaptic(false, soundType, vol);
                        }

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

            float minDist = float.MaxValue;
            if (isLeftHandTracking) minDist = Mathf.Min(minDist, Vector3.Distance(leftHandPos, followBall.transform.position));
            if (isRightHandTracking) minDist = Mathf.Min(minDist, Vector3.Distance(rightHandPos, followBall.transform.position));
            float allowedRadius = (sliderWidth * 0.5f) * followRadiusMultiplier;
            bool isEffectiveTracking = isTracking && (minDist <= allowedRadius);

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
        /// 根据时间计算路径上的本地坐标 (复用 Ping-Pong 逻辑)
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
                    DestroyImmediate(obj);
                }
            }
            garbageList.Clear(); // 清空列表，断开所有”尸体”引用

            // 1.5 销毁本滑条定制渲染队列时产生的材质克隆（防显存泄漏）
            for (int i = 0; i < clonedMaterials.Count; i++)
            {
                if (clonedMaterials[i] != null)
                    DestroyImmediate(clonedMaterials[i]);
            }
            clonedMaterials.Clear();

            // 清空折返粒子引用（GameObject 已被 Destroy，引用变成”假非空”）
            headReversePS = null;
            tailReversePS = null;

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
                DestroyImmediate(combinedMesh);
                combinedMesh = null;
            }
            if (meshRenderer != null)
            {
                meshRenderer.SetPropertyBlock(null);

                if (meshRenderer.sharedMaterial != null)
                {
                    if (meshRenderer.sharedMaterial != sharedMaterial)
                    {
                        DestroyImmediate(meshRenderer.sharedMaterial);
                    }
                    meshRenderer.sharedMaterial = null;
                }
            }

            // 2. 清理边框的 Mesh 和 Material
            Transform oldBorder = transform.Find("SliderBorder");
            if (oldBorder != null)
            {
                MeshFilter mf = oldBorder.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null) DestroyImmediate(mf.sharedMesh);


                MeshRenderer mr = oldBorder.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    // 清理边框材质属性
                    mr.SetPropertyBlock(null);

                    if (mr.sharedMaterial != null)
                        DestroyImmediate(mr.sharedMaterial);
                }

                DestroyImmediate(oldBorder.gameObject);
            }
        }
    }
}
