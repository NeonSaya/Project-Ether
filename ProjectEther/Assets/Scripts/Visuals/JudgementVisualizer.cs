using UnityEngine;
using UnityEngine.Pool;
using TMPro;
using System.Collections;
using System.Collections.Generic;

namespace OsuVR
{
    public class JudgementVisualizer : MonoBehaviour
    {
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
        private bool isPrewarmed = false;

        // 专门存放特效的父容器
        private Transform poolContainer;

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

            if (flashMat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (!shader) shader = Shader.Find("Mobile/Particles/Additive");
                if (!shader) shader = Shader.Find("Particles/Standard Unlit");

                flashMat = new Material(shader);
                flashMat.SetFloat("_Surface", 1);
                flashMat.SetFloat("_Blend", 0);
                flashMat.SetInt("_ZWrite", 0);

                Texture2D tex = GenerateSoftDotTexture();
                flashMat.mainTexture = tex;
                if (flashMat.HasProperty("_BaseMap")) flashMat.SetTexture("_BaseMap", tex);

                if (flashMat.HasProperty("_Color")) flashMat.SetColor("_Color", Color.white);
                if (flashMat.HasProperty("_BaseColor")) flashMat.SetColor("_BaseColor", Color.white);
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
            // ✅ 改动：放入专门的容器里
            root.transform.SetParent(poolContainer);
            root.layer = 0;

            // --- 文本 ---
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(root.transform);
            textObj.transform.localPosition = new Vector3(0, 0, -0.05f);
            textObj.layer = 0;

            TextMeshPro tmp = textObj.AddComponent<TextMeshPro>();
            RectTransform rect = textObj.GetComponent<RectTransform>();

            // ✅ 改动：加大容器尺寸
            rect.sizeDelta = new Vector2(25, 10);

            tmp.alignment = TextAlignmentOptions.Center;
            // ✅ 改动：加大字体
            tmp.fontSize = 12;
            tmp.fontStyle = FontStyles.Bold;
            tmp.enableWordWrapping = false;

            if (mainFont != null) tmp.font = mainFont;

            // --- 闪光 ---
            GameObject flashObj = new GameObject("Flash");
            flashObj.transform.SetParent(root.transform);
            flashObj.transform.localPosition = new Vector3(0, 0, 0.05f);
            flashObj.layer = 0;
            // ✅ 改动：加大闪光片
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
            bar1.AddComponent<MeshRenderer>().sharedMaterial = flashMat;
            bar1.transform.localScale = new Vector3(0.2f, 0.8f, 1f); // 加粗加大
            bar1.transform.localRotation = Quaternion.Euler(0, 0, 45);
            bar1.layer = 0;

            GameObject bar2 = new GameObject("Bar2");
            bar2.transform.SetParent(xObj.transform);
            bar2.AddComponent<MeshFilter>().sharedMesh = quadMesh;
            bar2.AddComponent<MeshRenderer>().sharedMaterial = flashMat;
            bar2.transform.localScale = new Vector3(0.2f, 0.8f, 1f); // 加粗加大
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
            else
            {
                item.Root.transform.position = pos;
                item.Root.transform.rotation = Quaternion.identity;
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

            if (score > 0)
            {
                item.FlashRenderer.gameObject.SetActive(true);
                Color flashCol = Color.Lerp(comboColor, mainColor, 0.5f);
                flashCol.a = 0.6f;
                if (item.FlashRenderer.material.HasProperty("_TintColor"))
                    item.FlashRenderer.material.SetColor("_TintColor", flashCol);
                else if (item.FlashRenderer.material.HasProperty("_BaseColor"))
                    item.FlashRenderer.material.SetColor("_BaseColor", flashCol);
                else
                    item.FlashRenderer.material.SetColor("_Color", flashCol);
            }
            else
            {
                item.FlashRenderer.gameObject.SetActive(false);
            }

            StartCoroutine(AnimateJudgement(item, score == 0, scaleMult));
        }

        public void ShowTailMiss(Vector3 pos)
        {
            JudgementItem item = pool.Get();
            item.ResetState();

            if (Camera.main != null)
            {
                item.Root.transform.position = pos;
                item.Root.transform.rotation = Quaternion.LookRotation(item.Root.transform.position - Camera.main.transform.position);
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
            if (r.material.HasProperty("_TintColor")) r.material.SetColor("_TintColor", c);
            else if (r.material.HasProperty("_BaseColor")) r.material.SetColor("_BaseColor", c);
            else r.material.SetColor("_Color", c);
        }

        IEnumerator AnimateJudgement(JudgementItem item, bool isMiss, float scaleMult)
        {
            float duration = 0.5f;
            float time = 0f;
            Vector3 startPos = item.Root.transform.position;
            Vector3 endPos = startPos + Vector3.up * 0.3f; // 加大上浮距离
            if (isMiss) endPos = startPos - Vector3.up * 0.5f; // 加大下坠距离

            item.Root.transform.localScale = Vector3.zero;

            while (time < duration)
            {
                time += Time.deltaTime;
                float t = time / duration;

                float scaleT = isMiss ? EaseOutBack(t * 2f) : EaseOutElastic(t * 2f);

                // ✅ 改动：使用 globalScale 变量控制整体大小
                item.Root.transform.localScale = Vector3.one * (scaleT * scaleMult * globalScale);

                if (!isMiss && item.FlashRenderer.gameObject.activeSelf)
                {
                    float flashScale = 1.0f + t * 1.5f;
                    item.FlashRenderer.transform.localScale = new Vector3(flashScale, flashScale, 1f);
                    Color c = item.FlashRenderer.material.HasProperty("_BaseColor") ? item.FlashRenderer.material.GetColor("_BaseColor") : item.FlashRenderer.material.color;
                    c.a = Mathf.Lerp(0.6f, 0f, t * 2.5f);
                    SetRendererColor(item.FlashRenderer, c);
                }

                item.Root.transform.position = Vector3.Lerp(startPos, endPos, t);

                if (t > 0.6f) item.Tmp.alpha = 1f - (t - 0.6f) * 2.5f;

                yield return null;
            }
            pool.Release(item);
        }

        IEnumerator AnimateTailMiss(JudgementItem item)
        {
            float duration = 0.4f;
            float time = 0f;
            while (time < duration)
            {
                time += Time.deltaTime;
                float t = time / duration;
                // ✅ 改动：小红叉也受 globalScale 影响
                float scale = EaseOutBack(t * 3f) * globalScale * 0.8f;
                item.XRoot.transform.localScale = Vector3.one * scale;
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

        float EaseOutElastic(float x) => x == 0 ? 0 : x >= 1 ? 1 : Mathf.Pow(2, -10 * x) * Mathf.Sin((x * 10 - 0.75f) * ((2 * Mathf.PI) / 3)) + 1;
        float EaseOutBack(float x) => 1 + 2.70158f * Mathf.Pow(x - 1, 3) + 1.70158f * Mathf.Pow(x - 1, 2);

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
                Tmp.alpha = 1f;
                Tmp.transform.localPosition = new Vector3(0, 0, -0.05f);
                FlashRenderer.transform.localScale = Vector3.one;
                XRoot.transform.localRotation = Quaternion.identity;
            }
        }
    }
}