using UnityEngine;
using UnityEngine.Pool;
using System.Collections;

namespace OsuVR
{
    /// <summary>
    /// 纯代码特效：悬浮爆破 + 扇形残渣 (防循环/防堆积版)
    /// </summary>
    public class CodeOnlyVFX : MonoBehaviour
    {
        public static CodeOnlyVFX Instance { get; private set; }

        // ======================= 🎛️ 参数配置区 =======================

        [Header("💥 主爆破 (Main Burst)")]
        public int burstCountMin = 20;
        public int burstCountMax = 30;
        public float burstRadius = 0.3f;
        public float cubeSize = 0.035f;

        [Header("🧱 直飞掉落残渣 (Straight & Drop)")]
        public int debrisMin = 3;
        public int debrisMax = 5;
        [Tooltip("水平飞行速度 (控制飞多快)")]
        public float horizontalSpeed = 1.0f;        

        [Tooltip("下落速度 (控制掉多快)")]
        public float dropSpeed = 12.0f;              

        [Tooltip("直飞时间占比 (0.1-0.9，值越大直飞越久)")]
        [Range(0.1f, 0.9f)]
        public float hoverTimeFraction = 0.2f; // ✅ 核心参数：控制直飞多久

        [Tooltip("下落阶段的重力倍率")]
        public float gravityScale = 12.0f;


        [Header("🎨 视觉设置")]
        public float hdrIntensity = 6.0f; // 适配圈圈亮度

        

        // ======================= 🔧 内部资源 =======================

        private ObjectPool<ParticleSystem> pool;
        private Mesh cubeMesh;
        private Material particleMat;

        void Awake()
        {
            if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
            else { Destroy(gameObject); return; }

            PrepareResources();

            pool = new ObjectPool<ParticleSystem>(
                createFunc: CreateCombinedSystem,
                actionOnGet: OnGetItem,
                actionOnRelease: OnReleaseItem,
                actionOnDestroy: (ps) => { if (ps) Destroy(ps.gameObject); },
                defaultCapacity: 50,
                maxSize: 300
            );
        }

        void OnDestroy()
        {
            if (particleMat != null) Destroy(particleMat);
        }

        void PrepareResources()
        {
            // 1. Mesh
            GameObject tempCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cubeMesh = tempCube.GetComponent<MeshFilter>().sharedMesh;
            Destroy(tempCube);

            // 2. 纯白贴图 (方形粒子用)
            Texture2D whiteTex = new Texture2D(1, 1);
            whiteTex.SetPixel(0, 0, Color.white);
            whiteTex.Apply();

            // 3. Shader 选择 (优先 Additive 混合模式，实现发光效果)
            Shader shader = Shader.Find("Mobile/Particles/Additive");
            if (!shader) shader = Shader.Find("Legacy Shaders/Particles/Additive");
            if (!shader) shader = Shader.Find("Particles/Standard Unlit");
            if (!shader) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");

            particleMat = new Material(shader);
            particleMat.enableInstancing = true;

            // 4. 给贴图
            if (particleMat.HasProperty("_MainTex")) particleMat.SetTexture("_MainTex", whiteTex);
            if (particleMat.HasProperty("_BaseMap")) particleMat.SetTexture("_BaseMap", whiteTex);

            // 5. 基础色设为白
            if (particleMat.HasProperty("_Color")) particleMat.SetColor("_Color", Color.white);
            if (particleMat.HasProperty("_BaseColor")) particleMat.SetColor("_BaseColor", Color.white);
        }

        // ======================= 🎇 系统构建 =======================

        ParticleSystem CreateCombinedSystem()
        {
            GameObject root = new GameObject("VFX_PoolItem");
            // 先隐藏，防止 AddComponent 时自动播放报错
            root.SetActive(false);
            root.transform.SetParent(transform);

            ParticleSystem rootPS = root.AddComponent<ParticleSystem>();
            var main = rootPS.main;

            main.duration = 1.0f;
            main.loop = false; // 预设不循环
            main.playOnAwake = false;
            main.stopAction = ParticleSystemStopAction.None;

            root.GetComponent<ParticleSystemRenderer>().enabled = false;

            CreateSubEmitter(root.transform, "Burst_Floating", isDebris: false);
            CreateSubEmitter(root.transform, "Burst_Debris", isDebris: true);

            return rootPS;
        }

        void CreateSubEmitter(Transform parent, string name, bool isDebris)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;

            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ParticleSystemRenderer psr = go.GetComponent<ParticleSystemRenderer>();

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false; // 预设不循环
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            psr.renderMode = ParticleSystemRenderMode.Mesh;
            psr.mesh = cubeMesh;
            psr.material = particleMat;
            psr.enableGPUInstancing = true;
            psr.alignment = ParticleSystemRenderSpace.Local;

            main.startSize3D = false;
            main.startSize = cubeSize;

            if (!isDebris)
            {
                // === [悬浮爆破] ===
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.4f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 6.0f);
                main.gravityModifier = 0f;

                main.startRotation3D = true;
                main.startRotationX = new ParticleSystem.MinMaxCurve(0, 360 * Mathf.Deg2Rad);
                main.startRotationY = new ParticleSystem.MinMaxCurve(0, 360 * Mathf.Deg2Rad);

                var sh = ps.shape;
                sh.enabled = true;
                sh.shapeType = ParticleSystemShapeType.Sphere;
                sh.radius = burstRadius;

                var limit = ps.limitVelocityOverLifetime;
                limit.enabled = true;
                limit.dampen = 0.2f;
                limit.limit = 0f;
            }
            else
            {
                // === [悬停掉落残渣] (沉重版) ===

                // 1. 寿命控制：0.4~0.6秒
                // 配合 12.0 的重力，0.5秒能掉大概 1.5米 (y = 0.5 * g * t^2)
                // 这个距离刚好够它从打击点掉到视线底部，然后消失
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.6f);

                // 2. 重力曲线
              
                AnimationCurve gravCurve = new AnimationCurve();
                gravCurve.AddKey(0.0f, 0.0f);
                gravCurve.AddKey(hoverTimeFraction, 0.0f); // 悬停结束

                // 曲线稍微陡峭一点
                Keyframe endKey = new Keyframe(1.0f, 1.0f);
                endKey.inTangent = 2.0f;
                gravCurve.AddKey(endKey);

                main.gravityModifier = new ParticleSystem.MinMaxCurve(gravityScale, gravCurve);

                
                // 我们要它掉得越快越好，不要阻力
                var limit = ps.limitVelocityOverLifetime;
                limit.enabled = false;

                // 4. 其他基础设置
                main.startSpeed = 0; // 代码控制初速度

                var rotOverLife = ps.rotationOverLifetime;
                rotOverLife.enabled = true;
                rotOverLife.x = new ParticleSystem.MinMaxCurve(-360f, 360f); // 翻滚

                var sh = ps.shape;
                sh.enabled = false;

                // 尺寸稍微大一点，显眼
                main.startSize = new ParticleSystem.MinMaxCurve(cubeSize * 1.0f, cubeSize * 1.6f);
            }

            var em = ps.emission;
            em.enabled = true;
            em.rateOverTime = 0;
            em.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 0) });
        }

        // ======================= 🚀 接口与逻辑 =======================

        void OnGetItem(ParticleSystem ps)
        {
            ps.gameObject.SetActive(true);
        }

        void OnReleaseItem(ParticleSystem ps)
        {
            ps.gameObject.SetActive(false);
        }


        /// <summary>
        /// 播放打击特效 (防卡顿/防丢失终极版)
        /// </summary>
        public void PlayHit(Vector3 pos, Quaternion rot, float size, Color color, Vector3? avoidPos = null)
        {
            // 1. 从池中获取
            ParticleSystem rootPS = pool.Get();

            // 🛑 [保险措施 1] 确保物体是激活的，否则协程和粒子都不工作
            if (!rootPS.gameObject.activeSelf) rootPS.gameObject.SetActive(true);

            // 🛑 [保险措施 2] 暴力重置状态
            // Stop: 停止播放
            // Clear: 清除残留粒子
            // time=0: 强制时间归零
            rootPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            rootPS.Clear(true);
            rootPS.time = 0f;

            // 设置位置
            Transform rootTrans = rootPS.transform;
            rootTrans.SetPositionAndRotation(pos, rot);
            rootTrans.localScale = Vector3.one;

            // 2. 避让概率计算
            float leftProbability = 0.5f;
            if (avoidPos.HasValue)
            {
                Vector3 toNext = (avoidPos.Value - pos).normalized;
                Vector3 localToNext = Quaternion.Inverse(rot) * toNext;
                if (localToNext.x > 0.1f) leftProbability = 0.8f;
                else if (localToNext.normalized.x < -0.1f) leftProbability = 0.2f;
            }

            Color hdrColor = new Color(color.r * hdrIntensity, color.g * hdrIntensity, color.b * hdrIntensity, 1.0f);

            // 3. 配置子粒子系统 (直接获取，零GC)
            // 索引 0: Burst_Floating (主爆破)
            ParticleSystem psFloating = rootTrans.GetChild(0).GetComponent<ParticleSystem>();
            var mainFloating = psFloating.main;
            mainFloating.startColor = hdrColor;
            mainFloating.startSize = new ParticleSystem.MinMaxCurve(cubeSize * size, cubeSize * size * 1.2f);

            var shapeFloating = psFloating.shape;
            shapeFloating.radius = burstRadius * size;

            // 设置爆破数量
            var emFloating = psFloating.emission;
            short burstCount = (short)Random.Range(burstCountMin, burstCountMax);
            emFloating.SetBurst(0, new ParticleSystem.Burst(0f, burstCount));

            // 索引 1: Burst_Debris (残渣)
            ParticleSystem psDebris = rootTrans.GetChild(1).GetComponent<ParticleSystem>();
            // 残渣不需要设置 Emission，全靠手动 Emit

            // 🛑 [保险措施 3] 启动播放
            // 必须先 Play，让系统进入"运行态"，随后的 Emit 才会生效
            rootPS.Play(true);

            // 4. 手动发射残渣
            int debrisCount = Random.Range(debrisMin, debrisMax + 1);
            var emitParams = new ParticleSystem.EmitParams();
            emitParams.startColor = hdrColor;

            for (int i = 0; i < debrisCount; i++)
            {
                float dirX = (Random.value < leftProbability) ? -1f : 1f;
                dirX += Random.Range(-0.25f, 0.25f);
                float dirZ = Random.Range(0.1f, 0.6f);
                float dirY = Random.Range(-0.15f, 0.15f);

                Vector3 localDir = new Vector3(dirX, dirY, dirZ).normalized;
                Vector3 baseVelocity = rot * localDir;
                float finalSpeed = Random.Range(horizontalSpeed * 0.8f, horizontalSpeed * 1.2f);
                Vector3 subtleNoise = Random.insideUnitSphere * 0.5f;

                emitParams.velocity = (baseVelocity * finalSpeed) + subtleNoise;
                emitParams.startLifetime = Random.Range(0.4f, 0.6f);
                emitParams.startSize = Random.Range(cubeSize * size * 1.0f, cubeSize * size * 1.6f);

                psDebris.Emit(emitParams, 1);
            }

            // 5. 快速回收
            StartCoroutine(ForceRecycle(rootPS, 1.0f));
        }


        /// <summary>
        /// 播放转盘完成时的白色破碎特效 (完美复刻 Note 手感版)
        /// </summary>
        /// <param name="pos">转盘中心位置</param>
        public void PlaySpinnerClear(Vector3 pos)
        {
            ParticleSystem rootPS = pool.Get();

            rootPS.transform.position = pos;
            rootPS.transform.rotation = Quaternion.identity;
            rootPS.transform.localScale = Vector3.one; // 🛑 必须为1，保证重力正常

            // 强制纯白，亮度跟 PlayHit 保持一致 (2.5)
            Color whiteHdr = new Color(2.5f, 2.5f, 2.5f, 1f);

            ParticleSystem[] children = rootPS.GetComponentsInChildren<ParticleSystem>();
            foreach (var ps in children)
            {
                var main = ps.main;
                main.loop = false;
                ps.Clear();

                if (ps == rootPS) continue;

                // A. 主爆破 (Burst_Floating)
                if (ps.name == "Burst_Floating")
                {
                    main.startColor = whiteHdr;

                    // 尺寸回归正常 Note 大小 (假设 size=1.0)
                    main.startSize = new ParticleSystem.MinMaxCurve(cubeSize, cubeSize * 1.2f);

                    var shape = ps.shape;
                    shape.radius = burstRadius; // 回归正常半径

                    var em = ps.emission;
                    // 数量稍微多一点点 (普通是 20-30，这里给 30-45)
                    em.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, (short)Random.Range(30, 45)) });

                    ps.Play();
                }
                // B. 残渣 (Burst_Debris) - 核心修改
                else if (ps.name == "Burst_Debris")
                {
                    // 数量：普通是 debrisMin~debrisMax (3-5)
                    // 转盘：稍微多一点 (比如 6-10 个)，不要太多太乱
                    int count = Random.Range(6, 11);

                    var emitParams = new ParticleSystem.EmitParams();
                    emitParams.startColor = whiteHdr;

                    for (int i = 0; i < count; i++)
                    {
                        // === 100% 复刻 PlayHit 的物理逻辑 ===

                        // 1. 方向 (X轴左右各50%概率，因为转盘在中间)
                        float dirX = (Random.value < 0.5f) ? -1f : 1f;
                        dirX += Random.Range(-0.25f, 0.25f);

                        // Z轴 (向深处)
                        float dirZ = Random.Range(0.1f, 0.6f);

                        // Y轴 (厚度微扰)
                        float dirY = Random.Range(-0.15f, 0.15f);

                        Vector3 localDir = new Vector3(dirX, dirY, dirZ).normalized;

                        // 世界方向 (转盘正对，直接用 localDir 即可)
                        Vector3 baseVelocity = localDir;

                        // 2. 速度 (完全一致)
                        float finalSpeed = Random.Range(horizontalSpeed * 0.8f, horizontalSpeed * 1.2f);

                        // 3. 噪声 (完全一致)
                        Vector3 subtleNoise = Random.insideUnitSphere * 0.5f;

                        emitParams.velocity = (baseVelocity * finalSpeed) + subtleNoise;

                        // 4. 寿命 (完全一致)
                        emitParams.startLifetime = Random.Range(0.4f, 0.6f);

                        // 5. 尺寸 (完全一致，不再放大了)
                        emitParams.startSize = Random.Range(cubeSize * 0.2f, cubeSize * 0.4f);

                        ps.Emit(emitParams, 1);
                    }
                    ps.Play();
                }
            }

            rootPS.Play();
            StartCoroutine(ForceRecycle(rootPS, 1.0f));
        }

        /// <summary>
        /// 强制回收协程：解决“粒子卡住不回收”和“无限创建对象”的问题
        /// </summary>
        IEnumerator ForceRecycle(ParticleSystem ps, float delay)
        {
            yield return new WaitForSeconds(delay);

            // 只有当对象还是激活状态时才回收（防止已经被其他逻辑回收了导致报错）
            if (ps != null && ps.gameObject.activeSelf)
            {
                pool.Release(ps);
            }
        }
    }
}