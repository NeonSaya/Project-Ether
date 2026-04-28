using UnityEngine;
using UnityEngine.Pool;
using TMPro;
using System.Collections;
using System.Collections.Generic;

namespace OsuVR
{
    /// <summary>
    /// 判定可视化器：显示击中判定结果（300/100/50/Miss）
    /// 核心功能：
    /// - 对象池管理：预创建判定文字对象，避免运行时 GC
    /// - 颜色编码：不同判定结果使用不同颜色
    /// - 动画效果：文字弹出后渐隐消失
    /// </summary>
    public class JudgementVisualizer : MonoBehaviour
    {
        // 缓存 Shader 属性 ID
        private static readonly int PropTintColor = Shader.PropertyToID("_TintColor");
        private static readonly int PropBaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int PropColor = Shader.PropertyToID("_Color");

        public static JudgementVisualizer Instance { get; private set; }

        [Header("核心配置 (必须赋值!)")]
        [Tooltip("请务必在这里拖入一个 TMP Font Asset (例如 LiberationSans SDF)")]
        public TMP_FontAsset mainFont;

        [Header("尺寸设置")]
        [Tooltip("全局缩放倍率 (觉得小就改大这个)")]
        public float globalScale = 0.6f; // 之前是 0.2 左右，现在调大 3 倍

        [Header("颜色配置")]
        public Color color300 = new Color(0.2f, 0.8f, 1f);
        public Color color100 = new Color(0.4f, 0.9f, 0.4f);
        public Color color50 = new Color(1f, 0.8f, 0.2f);
        public Color colorMiss = new Color(1f, 0.2f, 0.2f);

        // 对象池
        private ObjectPool<JudgementItem> pool;
        private Mesh quadMesh;
        private Material flashMat;
        private Material overlayFontMat;
        private Material missMat;
        private bool isPrewarmed = false;

        // 专门存放特效的父容器
        private Texture2D _softDotTex;
        private Transform poolContainer;
        private MaterialPropertyBlock _propBlock;

        void Awake()
        {
            Instance = this;
        }

        void Start()
        {
            if (!isPrewarmed) Prewarm();
        }

        public void Prewarm()
        {
            if (isPrewarmed) return;

            // 1. 创建专门的容器物体，防止 Hierarchy 爆炸
            GameObject containerObj = new GameObject("Judgement Pool Container");
            containerObj.transform.SetParent(transform); // 挂在当前脚本物体下
            containerObj.transform.localPosition = Vector3.zero;
            containerObj.transform.localRotation = Quaternion.identity;
            containerObj.transform.localScale = Vector3.one;
            poolContainer = containerObj.transform;

            // 2. 检查字体
            if (mainFont == null)
            {
                mainFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
                if (mainFont == null)
                    Debug.LogError("❌ [JudgementVisualizer] 严重错误：没有字体！请在 Inspector 中拖入 Font Asset！");
            }

            // 3. 生成网格和材质
            PrepareResources();

            // 4. 初始化对象池
            pool = new ObjectPool<JudgementItem>(
                createFunc: CreateItem,
                actionOnGet: (item) => item.Root.SetActive(true),
                actionOnRelease: (item) => item.Root.SetActive(false),
                actionOnDestroy: (item) => Destroy(item.Root),
                defaultCapacity: 30,
                maxSize: 100
            );

            // 5. 预热
            var tempItems = new List<JudgementItem>();
            for (int i = 0; i < 20; i++) tempItems.Add(pool.Get());
            foreach (var item in tempItems) pool.Release(item);

            isPrewarmed = true;
            Debug.Log("✅ JudgementVisualizer 预热完成 (层级已优化)");
        }

        private void PrepareResources()
        {
            if (quadMesh == null)
            {
                GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quadMesh = temp.GetComponent<MeshFilter>().sharedMesh;
                Destroy(temp);
            }

            if (_softDotTex == null) _softDotTex = GenerateSoftDotTexture();

            // 1. 准备发光材质 (用于 Great/Ok 闪光)
            if (flashMat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (!shader) shader = Shader.Find("Mobile/Particles/Additive");
                if (!shader) shader = Shader.Find("Particles/Standard Unlit");

                flashMat = new Material(shader);
                flashMat.SetFloat("_Surface", 1);
                flashMat.SetFloat("_Blend", 0);
                flashMat.SetInt("_ZWrite", 0);

                // ✅ 彻底置顶三板斧
                flashMat.SetInt("_ZTest", 8); // 8 = Always (无视深度遮挡)
                flashMat.SetInt("_Cull", 0);  // 0 = Off (双面渲染，绝不会因为背面朝向而隐形)
                flashMat.renderQueue = 4000;  // 4000 = Overlay (最后渲染，凌驾于所有物体之上)

                flashMat.mainTexture = _softDotTex;
                if (flashMat.HasProperty("_BaseMap")) flashMat.SetTexture("_BaseMap", _softDotTex);
            }

            // 2. 准备 Miss 专属材质 (实心、正常透明度混合、永远置顶)
            if (missMat == null)
            {
                missMat = new Material(flashMat.shader);
                missMat.SetFloat("_Surface", 1);
                missMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                missMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                missMat.SetInt("_ZWrite", 0);

                // ✅ 彻底置顶三板斧 (红叉专属)
                missMat.SetInt("_ZTest", 8);
                missMat.SetInt("_Cull", 0);
                missMat.renderQueue = 4000;

                missMat.mainTexture = Texture2D.whiteTexture;
                if (missMat.HasProperty("_BaseMap")) missMat.SetTexture("_BaseMap", Texture2D.whiteTexture);
            }

            // 3. 准备置顶的字体材质
            if (overlayFontMat == null && mainFont != null)
            {
                overlayFontMat = new Material(mainFont.material);

                // ✅ 字体置顶
                overlayFontMat.SetInt("_ZTestMode", 8);
                overlayFontMat.SetInt("unity_GUIZTestMode", 8);
                overlayFontMat.SetInt("_ZTest", 8);
                overlayFontMat.renderQueue = 4000;
            }
        }

        private Texture2D GenerateSoftDotTexture()
        {
            int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] cols = new Color[size * size];
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float radius = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    float t = Mathf.Clamp01(1f - dist / radius);
                    float alpha = t * t * t;
                    cols[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            tex.SetPixels(cols);
            tex.Apply();
            return tex;
        }

        private JudgementItem CreateItem()
        {
            GameObject root = new GameObject("Judgement_Instance");
            root.transform.SetParent(poolContainer);
            root.layer = 0;

            // --- 文本 ---
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(root.transform);
            textObj.transform.localPosition = new Vector3(0, 0, -0.05f);
            textObj.layer = 0;

            TextMeshPro tmp = textObj.AddComponent<TextMeshPro>();
            RectTransform rect = textObj.GetComponent<RectTransform>();

            // ✅ 改动 1：加大容器尺寸以容纳大字
            rect.sizeDelta = new Vector2(120, 30);

            tmp.alignment = TextAlignmentOptions.Center;
            // ✅ 改动 2：基础字号直接拉到 100，这样 globalScale 就可以填 1.0 了
            tmp.fontSize = 100;
            tmp.fontStyle = FontStyles.Bold;
            tmp.enableWordWrapping = false;

            // 开启额外对齐方式，保证字距散开时始终居中
            tmp.horizontalAlignment = HorizontalAlignmentOptions.Center;

            if (mainFont != null) tmp.font = mainFont;
            if (overlayFontMat != null) tmp.fontSharedMaterial = overlayFontMat;

            // --- 闪光 ---
            GameObject flashObj = new GameObject("Flash");
            flashObj.transform.SetParent(root.transform);
            flashObj.transform.localPosition = new Vector3(0, 0, 0.05f);
            flashObj.layer = 0;

            // 闪光片基础大小微调
            flashObj.transform.localScale = Vector3.one * 1.5f;

            MeshFilter mf = flashObj.AddComponent<MeshFilter>();
            mf.sharedMesh = quadMesh;
            MeshRenderer mr = flashObj.AddComponent<MeshRenderer>();
            mr.sharedMaterial = flashMat;

            // --- 小红叉 ---
            GameObject xObj = new GameObject("Small_X");
            xObj.transform.SetParent(root.transform);
            xObj.SetActive(false);
            xObj.layer = 0;

            GameObject bar1 = new GameObject("Bar1");
            bar1.transform.SetParent(xObj.transform);
            bar1.AddComponent<MeshFilter>().sharedMesh = quadMesh;
            bar1.AddComponent<MeshRenderer>().sharedMaterial = missMat;
            bar1.transform.localScale = new Vector3(0.3f, 2.0f, 1f);
            bar1.transform.localRotation = Quaternion.Euler(0, 0, 45);
            bar1.layer = 0;

            GameObject bar2 = new GameObject("Bar2");
            bar2.transform.SetParent(xObj.transform);
            bar2.AddComponent<MeshFilter>().sharedMesh = quadMesh;
            bar2.AddComponent<MeshRenderer>().sharedMaterial = missMat;
            bar2.transform.localScale = new Vector3(0.3f, 2.0f, 1f);
            bar2.transform.localRotation = Quaternion.Euler(0, 0, -45);
            bar2.layer = 0;

            return new JudgementItem(root, tmp, mr, xObj, bar1.GetComponent<MeshRenderer>(), bar2.GetComponent<MeshRenderer>());
        }

        public void ShowJudgement(Vector3 pos, int score, Color comboColor)
        {
            if (!isPrewarmed) Prewarm();

            JudgementItem item = pool.Get();
            item.ResetState();

            if (Camera.main != null)
            {
                item.Root.transform.position = pos;
                item.Root.transform.rotation = Quaternion.LookRotation(item.Root.transform.position - Camera.main.transform.position);
            }

            string text = "";
            Color mainColor = Color.white;
            float scaleMult = 1.0f;

            switch (score)
            {
                case 300: text = "GREAT"; mainColor = color300; scaleMult = 1.2f; break;
                case 100: text = "OK"; mainColor = color100; scaleMult = 1.0f; break;
                case 50: text = "MEH"; mainColor = color50; scaleMult = 0.9f; break;
                case 0: text = "MISS"; mainColor = colorMiss; scaleMult = 1.1f; break;
            }

            item.Tmp.text = text;
            item.Tmp.color = mainColor;
            item.Tmp.gameObject.SetActive(true);

            Color flashCol = Color.clear;
            if (score > 0)
            {
                item.FlashRenderer.gameObject.SetActive(true);
                flashCol = Color.Lerp(comboColor, mainColor, 0.5f);
                flashCol.a = 0.6f;
                SetRendererColor(item.FlashRenderer, flashCol);
            }
            else
            {
                item.FlashRenderer.gameObject.SetActive(false);
            }

            // 把 flashCol 传进协程
            StartCoroutine(AnimateJudgement(item, score == 0, scaleMult, flashCol));
        }

        public void ShowTailMiss(Vector3 pos)
        {
            JudgementItem item = pool.Get();
            item.ResetState();

            if (Camera.main != null)
            {
                item.Root.transform.position = pos;
                Vector3 dir = item.Root.transform.position - Camera.main.transform.position;
                item.Root.transform.rotation = Quaternion.LookRotation(dir);

                // ✅ 防穿模：稍微往玩家方向拉近 0.05 米，防止陷进滑条模型里看不见
                item.Root.transform.position -= dir.normalized * 0.05f;
            }

            item.Tmp.gameObject.SetActive(false);
            item.FlashRenderer.gameObject.SetActive(false);
            item.XRoot.SetActive(true);

            Color xColor = colorMiss;
            SetRendererColor(item.XRenderer1, xColor);
            SetRendererColor(item.XRenderer2, xColor);

            StartCoroutine(AnimateTailMiss(item));
        }

        private void SetRendererColor(Renderer r, Color c)
        {
            if (_propBlock == null) _propBlock = new MaterialPropertyBlock();

            r.GetPropertyBlock(_propBlock);

            // 注意：这里用 sharedMaterial 判断，绝不产生新实例
            if (r.sharedMaterial.HasProperty(PropTintColor)) _propBlock.SetColor(PropTintColor, c);
            else if (r.sharedMaterial.HasProperty(PropBaseColor)) _propBlock.SetColor(PropBaseColor, c);
            else _propBlock.SetColor(PropColor, c);

            r.SetPropertyBlock(_propBlock);
        }

        // 注意：加了 flashCol 参数
        IEnumerator AnimateJudgement(JudgementItem item, bool isMiss, float scaleMult, Color flashCol)
        {
            float duration = isMiss ? 0.7f : 0.6f;
            float time = 0f;
            Vector3 startPos = item.Root.transform.position;
            Vector3 endPos = isMiss ? startPos - Vector3.up * 0.6f : startPos;

            item.Root.transform.localScale = Vector3.zero;

            float startSpacing = -10f;
            float endSpacing = 30f;

            while (time < duration)
            {
                time += Time.deltaTime;
                float t = time / duration;

                if (!isMiss)
                {
                    // --- HIT ---
                    float scaleT = EaseOutCubic(Mathf.Clamp01(t * 3f));
                    item.Root.transform.localScale = Vector3.one * (scaleT * scaleMult * globalScale);

                    float spreadT = EaseOutCubic(t);
                    item.Tmp.characterSpacing = Mathf.Lerp(startSpacing, endSpacing, spreadT);
                    item.Root.transform.position = startPos;

                    if (item.FlashRenderer.gameObject.activeSelf)
                    {
                        float flashScale = 1.0f + t * 1.5f;
                        item.FlashRenderer.transform.localScale = new Vector3(flashScale, flashScale, 1f);

                        // ✅ 修复卡顿：直接操作传进来的颜色，不要 GetColor()，且用无泄漏的 SetRendererColor
                        flashCol.a = Mathf.Lerp(0.6f, 0f, t * 2.5f);
                        SetRendererColor(item.FlashRenderer, flashCol);
                    }
                }
                else
                {
                    // --- MISS ---
                    float scaleT = EaseOutBack(Mathf.Clamp01(t * 5f));
                    item.Root.transform.localScale = Vector3.one * (scaleT * scaleMult * globalScale);

                    float fallProgress = Mathf.Clamp01((t - 0.25f) / 0.75f);
                    float gravityT = EaseInCubic(fallProgress);

                    item.Tmp.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(0, -90f, gravityT));
                    item.Root.transform.position = Vector3.Lerp(startPos, endPos, gravityT);
                }

                if (t > 0.5f) item.Tmp.alpha = 1f - (t - 0.5f) * 2f;

                yield return null;
            }
            pool.Release(item);
        }

        IEnumerator AnimateTailMiss(JudgementItem item)
        {
            float duration = 0.5f;
            float time = 0f;

            // ✅ 必须初始为 0
            item.XRoot.transform.localScale = Vector3.zero;

            while (time < duration)
            {
                time += Time.deltaTime;
                float t = time / duration;

                // ✅ 核心修复：加上 Mathf.Clamp01，防止曲线数值突破天际！
                float scaleT = EaseOutBack(Mathf.Clamp01(t * 4f));
                item.XRoot.transform.localScale = Vector3.one * (scaleT * globalScale * 4.0f);
                if (t > 0.5f)
                {
                    Color c = colorMiss;
                    c.a = 1f - (t - 0.5f) * 2f;
                    SetRendererColor(item.XRenderer1, c);
                    SetRendererColor(item.XRenderer2, c);
                }
                yield return null;
            }
            pool.Release(item);
        }

        // ================= 缓动函数库 =================
        float EaseOutElastic(float x) => x == 0 ? 0 : x >= 1 ? 1 : Mathf.Pow(2, -10 * x) * Mathf.Sin((x * 10 - 0.75f) * ((2 * Mathf.PI) / 3)) + 1;
        float EaseOutBack(float x) => 1 + 2.70158f * Mathf.Pow(x - 1, 3) + 1.70158f * Mathf.Pow(x - 1, 2);
        float EaseOutCubic(float x) => 1 - Mathf.Pow(1 - x, 3);
        float EaseInCubic(float x) => x * x * x;
        private class JudgementItem
        {
            public GameObject Root;
            public TextMeshPro Tmp;
            public MeshRenderer FlashRenderer;
            public GameObject XRoot;
            public MeshRenderer XRenderer1;
            public MeshRenderer XRenderer2;

            public JudgementItem(GameObject root, TextMeshPro tmp, MeshRenderer flash, GameObject xRoot, MeshRenderer xr1, MeshRenderer xr2)
            {
                Root = root; Tmp = tmp; FlashRenderer = flash; XRoot = xRoot; XRenderer1 = xr1; XRenderer2 = xr2;
            }

            public void ResetState()
            {
                Root.transform.localScale = Vector3.one;

                Tmp.alpha = 1f;
                Tmp.characterSpacing = -10f;
                Tmp.transform.localPosition = new Vector3(0, 0, -0.05f);
                Tmp.transform.localRotation = Quaternion.identity;

                FlashRenderer.transform.localScale = Vector3.one;
                XRoot.transform.localRotation = Quaternion.identity;
                // 确保所有组件都从干净状态开始
                Tmp.gameObject.SetActive(false);
                FlashRenderer.gameObject.SetActive(false);
                XRoot.SetActive(false);
            }
        }
    }
}