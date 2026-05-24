using UnityEngine;

namespace OsuVR
{
    /// <summary>
    /// HitObject 工厂类：纯代码生成 Note、滑条头、Tick、跟随球等游戏对象
    /// 
    /// 核心设计原则：
    /// 1. 零 Prefab 依赖 - 所有对象通过代码动态构建
    /// 2. 静态缓存 - Mesh、Material、Texture 只创建一次，全游戏复用
    /// 3. 内存安全 - 避免运行时材质泄漏，支持 Cleanup 清理
    /// 
    /// 生成的对象结构：
    /// - HitCircle: Sphere(根) + Body + SolidBody + ApproachCircle + Halo + NoteController
    /// - SliderHead: Sphere(根) + Body + ApproachCircle + Halo + ApproachCircleScaler
    /// - SliderTick: Quad + Material
    /// - FollowBall: Sphere + SphereCollider
    /// </summary>
    public static class HitObjectFactory
    {
        private static bool isInitialized = false;

        #region 缓存资源

        // 外部传入的材质引用（可选）
        private static Material cachedBodyMaterial;
        private static Material cachedApproachMaterial;

        // 程序化生成的光晕材质（全局唯一）
        private static Material cachedHaloMaterial;

        // 程序化生成的缩圈材质（使用置顶 Shader）
        private static Material cachedApproachCircleMaterial;

        // 基础 Mesh 缓存
        private static Mesh cachedQuadMesh;
        private static Mesh cachedSphereMesh;

        // 程序化生成的纹理
        private static Texture2D cachedGlowTexture;
        private static Texture2D cachedApproachTexture;
        private static Texture2D cachedBodyTexture; // 实心圆形贴图

        private static Texture2D cachedSolidTexture; // 【新增】实心圆贴图

        #endregion

        #region 初始化

        /// <summary>
        /// 初始化工厂，创建所有缓存资源
        /// 应在游戏启动时调用一次
        /// </summary>
        /// <param name="bodyMaterial">Note 主体材质（可选）</param>
        /// <param name="overlayMaterial">覆盖层材质（可选）</param>
        /// <param name="approachMaterial">缩圈材质（可选）</param>
        /// <param name="glowMaterial">发光材质（可选）</param>
        public static void Initialize(
            Material bodyMaterial = null,
            Material overlayMaterial = null,
            Material approachMaterial = null,
            Material glowMaterial = null)
        {
            if (isInitialized) return;
            if (cachedSolidTexture == null) cachedSolidTexture = CreateSolidCircleTexture();

            // 缓存外部材质
            cachedBodyMaterial = bodyMaterial;
            cachedApproachMaterial = approachMaterial;

            // 创建内部缓存资源
            CreateCachedMeshes();
            CreateCachedTextures();
            CreateCachedHaloMaterial();
            CreateCachedApproachCircleMaterial();

            isInitialized = true;
            Debug.Log("[HitObjectFactory] 初始化完成 - 纯代码生成模式");
        }

        /// <summary>
        /// 创建并缓存基础 Mesh（Quad 和 Sphere）
        /// 避免每次创建对象时调用 CreatePrimitive
        /// </summary>
        private static void CreateCachedMeshes()
        {
            // 缓存 Quad Mesh（用于 2D 面片：Body、ApproachCircle、Halo）
            if (cachedQuadMesh == null)
            {
                GameObject tempQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                cachedQuadMesh = tempQuad.GetComponent<MeshFilter>().sharedMesh;
                Object.DestroyImmediate(tempQuad);
            }

            // 缓存 Sphere Mesh（用于 3D 球体：HitCircle 根物体、FollowBall）
            if (cachedSphereMesh == null)
            {
                GameObject tempSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                cachedSphereMesh = tempSphere.GetComponent<MeshFilter>().sharedMesh;
                Object.DestroyImmediate(tempSphere);
            }
        }

        /// <summary>
        /// 创建并缓存程序化纹理
        /// </summary>
        private static void CreateCachedTextures()
        {
            if (cachedGlowTexture == null)
            {
                cachedGlowTexture = CreateGlowTexture();
            }

            if (cachedApproachTexture == null)
            {
                cachedApproachTexture = CreateApproachTexture();
            }

            if (cachedBodyTexture == null)
            {
                cachedBodyTexture = CreateBodyTexture();
            }
        }

        /// <summary>
        /// 创建光晕材质（全局唯一）
        /// 使用 Additive 混合模式，实现柔和的发光效果
        /// </summary>
        private static void CreateCachedHaloMaterial()
        {
            if (cachedHaloMaterial == null)
            {
                // 优先使用移动端优化的粒子 Shader
                Shader shader = Shader.Find("Mobile/Particles/Additive");
                if (shader == null) shader = Shader.Find("Legacy Shaders/Particles/Additive");

                cachedHaloMaterial = new Material(shader);
                cachedHaloMaterial.mainTexture = cachedGlowTexture;

                // HDR 白色发光：RGB 乘以 2.5 实现更强的 Bloom 效果
                Color whiteGlow = new Color(2.5f, 2.5f, 2.5f, 0.85f);
                if (cachedHaloMaterial.HasProperty("_TintColor"))
                    cachedHaloMaterial.SetColor("_TintColor", whiteGlow);
                else if (cachedHaloMaterial.HasProperty("_BaseColor"))
                    cachedHaloMaterial.SetColor("_BaseColor", whiteGlow);
                else
                    cachedHaloMaterial.SetColor("_Color", whiteGlow);
            }
        }

        /// <summary>
        /// 创建缩圈材质（全局唯一）
        /// 使用 Osu/ApproachCircle_SmartDepth Shader，确保缩圈始终置顶显示
        /// 
        /// Shader 特性：
        /// - Queue = Transparent+10：比普通透明物体更晚渲染
        /// - Offset -1, -1：解决 Z-Fighting，确保在滑条之上
        /// - ZTest LEqual：正常深度测试，但配合 Offset 实现置顶
        /// </summary>
        private static void CreateCachedApproachCircleMaterial()
        {
            if (cachedApproachCircleMaterial == null)
            {
                // 使用专用的置顶 Shader
                Shader shader = Shader.Find("Osu/ApproachCircle_SmartDepth");
                if (shader == null)
                {
                    // 回退到标准透明 Shader
                    shader = Shader.Find("Universal Render Pipeline/Unlit");
                    if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
                }

                cachedApproachCircleMaterial = new Material(shader);
                cachedApproachCircleMaterial.mainTexture = cachedApproachTexture;
                if (cachedApproachCircleMaterial.HasProperty("_BaseColor"))
                    cachedApproachCircleMaterial.SetColor("_BaseColor", Color.white);
                else
                    cachedApproachCircleMaterial.color = Color.white;
            }
        }

        #endregion

        #region 程序化纹理生成

        /// <summary>
        /// 生成空心光环纹理（用于 Note 和滑条头的光晕效果）
        /// 
        /// 数学原理：
        /// - 内部 (r < 0.72): 完全透明，避免遮挡 Note 主体
        /// - 过渡区 (0.72 ~ 0.80): SmoothStep 平滑淡入
        /// - 外部 (0.80 ~ 1.0): 三次方衰减，模拟真实光照散落
        /// 
        /// 这种三次方衰减让光晕紧贴边缘很亮，远处很柔，不会像"边框"
        /// </summary>
        private static Texture2D CreateGlowTexture()
        {
            int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] colors = new Color[size * size];
            Vector2 center = new Vector2(size / 2f, size / 2f);
            float maxRadius = size / 2f;

            // 光环参数：控制发光的形状和范围
            float startRadius = 0.72f;  // 内边界：Note 边缘稍内
            float peakRadius = 0.80f;   // 峰值位置：Note 物理边缘（最亮）
            float endRadius = 1.0f;     // 外边界：光晕扩散范围

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    float r = dist / maxRadius;
                    float alpha = 0f;

                    if (r < startRadius)
                    {
                        // 内部完全透明
                        alpha = 0f;
                    }
                    else if (r < peakRadius)
                    {
                        // 过渡区：SmoothStep 平滑淡入
                        float t = (r - startRadius) / (peakRadius - startRadius);
                        alpha = t * t * (3f - 2f * t);
                    }
                    else
                    {
                        // 外部：三次方衰减（模拟真实光照）
                        float t = (r - peakRadius) / (endRadius - peakRadius);
                        float linear = 1f - Mathf.Clamp01(t);
                        alpha = linear * linear * linear;
                    }

                    // 整体透明度调整
                    alpha *= 0.8f;
                    colors[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(colors);
            tex.Apply();
            return tex;
        }
        /// <summary>
        /// 【新增】生成绝对实心的圆形纹理
        /// 拒绝柔和，拒绝中心透明，就要一个大实心圆饼
        /// </summary>
        private static Texture2D CreateSolidCircleTexture()
        {
            int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] colors = new Color[size * size];
            Vector2 center = new Vector2(size / 2f, size / 2f);
            float maxRadius = size / 2f;
            
            // 半径控制：留一点边距防止贴图采样越界
            float circleRadius = 0.90f; 

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    float r = dist / maxRadius;

                    float alpha = 0f;

                    if (r <= circleRadius)
                    {
                        // 内部完全不透明 (Alpha = 1)
                        alpha = 1f;
                        
                        // 边缘稍微做一点点平滑 (0.05的宽度)，避免锯齿太难看，但整体还是硬的
                        float edgeWidth = 0.05f;
                        if (r > circleRadius - edgeWidth)
                        {
                            float t = (r - (circleRadius - edgeWidth)) / edgeWidth;
                            alpha = 1f - t;
                        }
                    }
                    
                    // 颜色设为白色，Alpha 根据上面计算
                    colors[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(colors);
            tex.Apply();
            return tex;
        }

        /// <summary>
        /// 生成缩圈纹理（Approach Circle）
        /// 
        /// 缩圈是一个从外向内收缩的圆环，用于指示打击时机
        /// 纹理设计：内边缘清晰，外边缘柔和淡出，带发光效果
        /// </summary>
        private static Texture2D CreateApproachTexture()
        {
            int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] colors = new Color[size * size];
            Vector2 center = new Vector2(size / 2f, size / 2f);
            float maxRadius = size / 2f;

            // 圆环参数
            float innerRadius = 0.82f;  // 内边界：圆环内侧
            float peakRadius = 0.88f;   // 峰值位置（最亮）
            float outerRadius = 1.0f;   // 外边界：圆环外侧

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    float r = dist / maxRadius;

                    float alpha = 0f;
                    if (r >= innerRadius && r <= outerRadius)
                    {
                        if (r < peakRadius)
                        {
                            // 内侧：平滑淡入
                            float t = (r - innerRadius) / (peakRadius - innerRadius);
                            alpha = t * t * (3f - 2f * t); // SmoothStep
                        }
                        else
                        {
                            // 外侧：柔和发光淡出
                            float t = (r - peakRadius) / (outerRadius - peakRadius);
                            float linear = 1f - Mathf.Clamp01(t);
                            alpha = linear * linear * linear; // 三次方衰减
                        }
                    }

                    colors[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(colors);
            tex.Apply();
            return tex;
        }

        /// <summary>
        /// 生成实心圆形纹理（用于 Note 主体）
        /// 
        /// 纹理设计：中心实心，边缘柔和淡出
        /// </summary>
        private static Texture2D CreateBodyTexture()
        {
            int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] colors = new Color[size * size];
            Vector2 center = new Vector2(size / 2f, size / 2f);
            float maxRadius = size / 2f;

            // 圆形参数
            float solidRadius = 0.75f;   // 实心区域
            float fadeStart = 0.75f;     // 淡出开始
            float fadeEnd = 0.92f;       // 淡出结束

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    float r = dist / maxRadius;

                    float alpha = 0f;
                    if (r < solidRadius)
                    {
                        // 实心区域
                        alpha = 1f;
                    }
                    else if (r < fadeEnd)
                    {
                        // 柔和淡出
                        float t = (r - fadeStart) / (fadeEnd - fadeStart);
                        float linear = 1f - Mathf.Clamp01(t);
                        alpha = linear * linear * linear; // 三次方衰减
                    }

                    colors[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(colors);
            tex.Apply();
            return tex;
        }

        #endregion

        #region 公共工厂方法

        /// <summary>
        /// 创建完整的 HitCircle 对象
        /// 
        /// 结构层次：
        /// HitCircle_Procedural (根)
        /// ├── Sphere Mesh (主球体碰撞)
        /// ├── NoteController + SphereCollider
        /// ├── Body (Quad) - 主贴图层
        /// ├── SolidBody (Quad) - 实心层
        /// ├── ApproachCircle (Quad) - 缩圈
        /// └── Halo_Glow (Quad) - 光晕效果
        /// </summary>
        public static GameObject CreateHitCircle()
        {
            EnsureInitialized();

            GameObject root = new GameObject("HitCircle_Procedural");

            // 构建层次结构
            CreateSphereBody(root); 
            
            // 【核心修改】
            // 1. (可选) 保留原有的 Body 作为底部的光晕/辉光
            CreateBodyLayer(root); 
            
            // 2. 【新增】叠加一个绝对实心的圆层！
            // 这就是你要的"再加一个圆形的贴图贴上去"
            CreateSolidLayer(root); 

            CreateApproachCircle(root);     
            CreateHalo(root);               
            AddNoteController(root);        
            AddCollider(root);              

            root.layer = 6; 

            return root;
        }

        private static void CreateSolidLayer(GameObject parent)
        {
            GameObject solidObj = new GameObject("SolidBody"); // 改个名字，方便你在Inspector里找
            solidObj.transform.SetParent(parent.transform);
            solidObj.transform.localPosition = Vector3.zero;
            solidObj.transform.localRotation = Quaternion.identity;
            solidObj.transform.localScale = Vector3.one; // 大小设为1

            var mf = solidObj.AddComponent<MeshFilter>();
            mf.sharedMesh = cachedQuadMesh;

            var mr = solidObj.AddComponent<MeshRenderer>();

            // ▼▼▼▼▼ 核心：材质设置 ▼▼▼▼▼
            
            // 1. 尝试找最普通的透明 Shader
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (!shader) shader = Shader.Find("Unlit/Transparent");
            if (!shader) shader = Shader.Find("Universal Render Pipeline/Lit"); // 兜底

            Material mat = new Material(shader);

            // 2. 赋予刚才生成的"大实心圆"贴图
            if (cachedSolidTexture == null) cachedSolidTexture = CreateSolidCircleTexture();
            mat.mainTexture = cachedSolidTexture;
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", cachedSolidTexture);
            mat.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);


            // 3. 强制设置为 Alpha 混合模式 (实心遮挡)
            // 针对 URP/Unlit 的设置
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1.0f); // Transparent
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0.0f);     // Alpha (不是 Additive!)
            if (mat.HasProperty("_SrcBlend")) mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            
            // 关闭深度写入，防止遮挡问题
            mat.SetInt("_ZWrite", 0);

            // 颜色设为纯白，等待 NoteController 染色
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", Color.white);
            else
                mat.color = Color.white;

            mr.material = mat;
    
            solidObj.layer = 6;
        }

        /// <summary>
        /// 创建滑条头对象
        /// 
        /// 与 HitCircle 的区别：
        /// - 无 Overlay 层
        /// - 使用 ApproachCircleScaler 而非 NoteController
        /// - 碰撞体更大（radius = 0.8）
        /// </summary>
        public static GameObject CreateSliderHead()
        {
            EnsureInitialized();

            GameObject root = new GameObject("SliderHead_Procedural");

            // 1. 创建物理球体 (隐形，用于碰撞)
            CreateSphereBody(root);

            // 2. 【核心修改】创建实心层
            CreateSolidLayer(root);

            // 默认是 1.0，改成 1.1 可以消除与光晕之间的黑边，让视觉上看起来更饱满
            Transform solidTr = root.transform.Find("SolidBody");
            if (solidTr != null)
            {
                solidTr.localScale = Vector3.one * 1.01f;
            }

            // 4. 创建其他组件
            // 注意：不要再调用 CreateBodyLayer(root) 了，它会产生多余的半透明黑底
            CreateApproachCircle(root);
            CreateHalo(root);
            AddApproachCircleScaler(root);
            AddCollider(root, trigger: true, radius: 0.8f);

            root.layer = 6;

            return root;
        }

        /// <summary>
        /// 创建滑条 Tick 小点
        /// 简单的 Quad + 材质，用于滑条路径上的小点
        /// </summary>
        public static GameObject CreateSliderTick()
        {
            EnsureInitialized();

            GameObject tick = new GameObject("SliderTick_Procedural");

            var mf = tick.AddComponent<MeshFilter>();
            mf.sharedMesh = cachedQuadMesh;

            var mr = tick.AddComponent<MeshRenderer>();
            if (cachedBodyMaterial != null)
            {
                mr.material = cachedBodyMaterial;
            }
            else
            {
                mr.material = CreateDefaultTickMaterial();
            }

            tick.layer = 6;

            return tick;
        }

        /// <summary>
        /// 创建跟随球对象
        /// 玩家在滑条上滑动时显示的跟踪球
        /// </summary>
        public static GameObject CreateFollowBall()
        {
            EnsureInitialized();

            GameObject ball = new GameObject("FollowBall_Procedural");

            var mf = ball.AddComponent<MeshFilter>();
            mf.sharedMesh = cachedSphereMesh;

            var mr = ball.AddComponent<MeshRenderer>();
            mr.material = CreateFollowBallMaterial();

            var sc = ball.AddComponent<SphereCollider>();
            sc.isTrigger = true;
            sc.radius = 0.5f;

            ball.layer = 6;

            return ball;
        }

        #endregion

        #region 私有构建方法

        private static void EnsureInitialized()
        {
            if (!isInitialized)
            {
                Initialize();
            }
        }

        /// <summary>
        /// 创建根物体的空 Transform（不再渲染 3D 球体）
        /// 纯 2D 平面模式：根物体仅作为容器，碰撞体由 AddCollider 添加
        /// </summary>
        private static void CreateSphereBody(GameObject parent)
        {
            // 根物体不再添加 MeshFilter 和 MeshRenderer
            // 所有视觉元素由子物体（Body、SolidBody、ApproachCircle、Halo）承担
            // 碰撞体由 AddCollider 方法单独添加
        }

        /// <summary>
        /// 创建 Body 层（主贴图层）
        /// 位于根物体下，显示 Note 的主体颜色（实心圆形）
        /// </summary>
        private static void CreateBodyLayer(GameObject parent)
        {
            GameObject body = new GameObject("Body");
            body.transform.SetParent(parent.transform);
            body.transform.localPosition = Vector3.zero;
            body.transform.localRotation = Quaternion.identity;
            body.transform.localScale = Vector3.one;

            var mf = body.AddComponent<MeshFilter>();
            mf.sharedMesh = cachedQuadMesh;

            var mr = body.AddComponent<MeshRenderer>();
            // 始终使用默认 Body 材质（确保透明支持）
            mr.material = CreateDefaultBodyMaterial();

            body.layer = 6;
        }

        /// <summary>
        /// 创建缩圈
        /// 纯 2D 平面模式：与 Note 主体在同一 Z 平面
        /// 初始缩放 1.5 倍，缩圈会随时间收缩到 Note 大小，指示打击时机
        /// 使用置顶 Shader 确保缩圈始终在滑条之上显示
        /// </summary>
        private static void CreateApproachCircle(GameObject parent)
        {
            GameObject approach = new GameObject("ApproachCircle");
            approach.transform.SetParent(parent.transform);
            approach.transform.localPosition = Vector3.zero;
            approach.transform.localRotation = Quaternion.identity;
            approach.transform.localScale = Vector3.one * 1.5f;

            var mf = approach.AddComponent<MeshFilter>();
            mf.sharedMesh = cachedQuadMesh;

            var mr = approach.AddComponent<MeshRenderer>();

            // 优先级：外部材质 > 置顶 Shader 材质 > 默认材质
            if (cachedApproachMaterial != null)
            {
                mr.material = new Material(cachedApproachMaterial);
                mr.material.mainTexture = cachedApproachTexture;
            }
            else if (cachedApproachCircleMaterial != null)
            {
                mr.material = cachedApproachCircleMaterial;
            }
            else
            {
                mr.material = CreateDefaultApproachMaterial();
            }

            approach.layer = 6;
        }

        /// <summary>
        /// 创建光晕效果
        /// 纯 2D 平面模式：与 Body 在同一 Z 平面
        /// 缩放 1.25 倍，使用程序化生成的空心光环纹理，实现边缘柔和的发光
        /// </summary>
        private static void CreateHalo(GameObject parent)
        {
            GameObject halo = new GameObject("Halo_Glow");
            halo.transform.SetParent(parent.transform);
            halo.transform.localPosition = Vector3.zero;
            halo.transform.localRotation = Quaternion.identity;
            halo.transform.localScale = Vector3.one * 1.25f;

            var mf = halo.AddComponent<MeshFilter>();
            mf.sharedMesh = cachedQuadMesh;

            var mr = halo.AddComponent<MeshRenderer>();
            mr.material = cachedHaloMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            halo.layer = 6;
        }

        /// <summary>
        /// 添加 NoteController 组件
        /// 并关联 ApproachCircle 引用
        /// </summary>
        private static void AddNoteController(GameObject go)
        {
            var nc = go.AddComponent<NoteController>();
            Transform approachTr = go.transform.Find("ApproachCircle");
            if (approachTr != null)
            {
                nc.approachCircle = approachTr;
                nc.approachCircleObject = approachTr;
            }
        }

        /// <summary>
        /// 添加 ApproachCircleScaler 组件
        /// 用于滑条头的缩圈动画
        /// </summary>
        private static void AddApproachCircleScaler(GameObject go)
        {
            var scaler = go.AddComponent<ApproachCircleScaler>();
            Transform approachTr = go.transform.Find("ApproachCircle");
            if (approachTr != null)
            {
                scaler.targetTransform = approachTr;
            }
        }

        /// <summary>
        /// 添加球形碰撞体
        /// </summary>
        private static void AddCollider(GameObject go, bool trigger = true, float radius = 0.5f)
        {
            var sc = go.AddComponent<SphereCollider>();
            sc.isTrigger = trigger;
            sc.radius = radius;
        }

        #endregion

        #region 默认材质创建

        private static Material CreateDefaultBodyMaterial()
        {
            // 使用支持透明的 Shader
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Mobile/Particles/Alpha Blended");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");

            var mat = new Material(shader);
            
            // 设置贴图（兼容不同 Shader 的属性名）
            if (mat.HasProperty("_BaseMap"))
                mat.SetTexture("_BaseMap", cachedBodyTexture);
            if (mat.HasProperty("_MainTex"))
                mat.SetTexture("_MainTex", cachedBodyTexture);
            mat.mainTexture = cachedBodyTexture;

            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", Color.white);
            else
                mat.color = Color.white;

            // 如果是 URP/Unlit，需要设置透明模式
            if (mat.HasProperty("_Surface"))
            {
                mat.SetInt("_Surface", 1); // 1 = Transparent
                mat.SetInt("_Blend", 0);   // 0 = Alpha
            }
            if (mat.HasProperty("_SrcBlend"))
            {
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
            }
            
            return mat;
        }

        private static Material CreateDefaultApproachMaterial()
        {
            // 使用支持透明的 Shader
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Mobile/Particles/Alpha Blended");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");

            var mat = new Material(shader);
            mat.mainTexture = cachedApproachTexture;
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", Color.white);
            else
                mat.color = Color.white;

            // 如果是 URP/Unlit，需要设置透明模式
            if (mat.HasProperty("_Surface"))
            {
                mat.SetInt("_Surface", 1); // 1 = Transparent
                mat.SetInt("_Blend", 0);   // 0 = Alpha
            }
            if (mat.HasProperty("_SrcBlend"))
            {
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
            }
            
            return mat;
        }

        private static Material CreateDefaultTickMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");

            var mat = new Material(shader);
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", Color.yellow);
            else
                mat.color = Color.yellow;
            return mat;
        }

        private static Material CreateFollowBallMaterial()
        {
            Shader shader = Shader.Find("Mobile/Particles/Additive");
            if (shader == null) shader = Shader.Find("Legacy Shaders/Particles/Additive");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");

            var mat = new Material(shader);

            // HDR 发光白球：亮度适中
            Color whiteGlow = new Color(2.5f, 2.5f, 2.5f, 1.0f);
            if (mat.HasProperty("_TintColor"))
                mat.SetColor("_TintColor", whiteGlow);
            else if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", whiteGlow);
            else
                mat.SetColor("_Color", whiteGlow);
            
            return mat;
        }

        #endregion

        #region 公共访问器

        /// <summary>
        /// 获取光晕材质（用于滑条头动态生成光晕）
        /// </summary>
        public static Material GetHaloMaterial()
        {
            EnsureInitialized();
            return cachedHaloMaterial;
        }

        /// <summary>
        /// 获取缓存的 Quad Mesh
        /// </summary>
        public static Mesh GetQuadMesh()
        {
            EnsureInitialized();
            return cachedQuadMesh;
        }

        #endregion

        #region 清理

        /// <summary>
        /// 清理所有缓存资源
        /// 应在场景卸载或游戏退出时调用
        /// </summary>
        public static void Cleanup()
        {
            // 清理程序化生成的纹理
            if (cachedGlowTexture != null)
            {
                Object.Destroy(cachedGlowTexture);
                cachedGlowTexture = null;
            }

            if (cachedApproachTexture != null)
            {
                Object.Destroy(cachedApproachTexture);
                cachedApproachTexture = null;
            }

            // 清理程序化生成的材质
            if (cachedHaloMaterial != null)
            {
                Object.Destroy(cachedHaloMaterial);
                cachedHaloMaterial = null;
            }

            if (cachedApproachCircleMaterial != null)
            {
                Object.Destroy(cachedApproachCircleMaterial);
                cachedApproachCircleMaterial = null;
            }
            if (cachedSolidTexture != null) {
                Object.Destroy(cachedSolidTexture);
                cachedSolidTexture = null;
            }

            if (cachedBodyTexture != null) {
                Object.Destroy(cachedBodyTexture);
                cachedBodyTexture = null;
            }
            isInitialized = false;
        }

        #endregion
    }
}
