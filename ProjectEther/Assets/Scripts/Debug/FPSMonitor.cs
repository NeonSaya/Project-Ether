using UnityEngine;

namespace OsuVR
{
    /// <summary>
    /// 专业级 FPS 监控组件 (零 GC 热路径)
    ///
    /// 数据维度:
    ///   - 实时 FPS (瞬时帧率)
    ///   - 平均 FPS (1024 样本滑动窗口)
    ///   - 最高/最低 FPS 波动
    ///   - 1% Low 帧 (最差 1% 帧的平均帧率，衡量卡顿严重程度)
    ///   - 帧生成时间 (ms)
    ///
    /// 架构:
    ///   - 环形缓冲区 (1024 float) 存储帧时间，覆盖 ~17 秒 @60fps
    ///   - 每帧计算统计: O(n) 遍历 + Array.Sort (in-place, 无 GC)
    ///   - 每 0.5 秒刷新显示字符串 (微量 string.Format，非热路径)
    ///   - OnGUI 渲染 (最轻量，无 Canvas/DrawCall 开销)
    ///   - F1 切换显示/隐藏
    /// </summary>
    public class FPSMonitor : MonoBehaviour
    {
        // =========================================================
        //  环形缓冲区
        // =========================================================

        const int SampleCount = 1024;
        float[] frameTimes;     // 帧时间环形缓冲区
        float[] sortedBuffer;   // 预分配排序缓冲区 (1% Low 计算)
        int sampleIndex;
        int sampleCount;

        // =========================================================
        //  统计数据 (每帧更新，零 GC)
        // =========================================================

        float currentFPS;
        float avgFPS;
        float minFPS;
        float maxFPS;
        float onePercentLow;    // 1% Low: 最差 1% 帧的平均帧率
        float fivePercentLow;   // 5% Low: 最差 5% 帧的平均帧率
        float frameTimeMs;
        float frameTimeJitter;  // 帧时间标准差 (抖动)

        // =========================================================
        //  显示
        // =========================================================

        float nextDisplayUpdate;
        const float DisplayInterval = 0.25f; // 每 0.25 秒刷新一次
        string displayText = "";
        bool isVisible = true;

        // =========================================================
        //  样式缓存
        // =========================================================

        GUIStyle styleGood;
        GUIStyle styleWarn;
        GUIStyle styleBad;
        GUIStyle styleShadow;
        bool stylesInitialized;

        // =========================================================
        //  自动初始化
        // =========================================================

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoInitialize()
        {
            var go = new GameObject("[FPSMonitor]");
            go.AddComponent<FPSMonitor>();
            DontDestroyOnLoad(go);
        }

        void Awake()
        {
            frameTimes = new float[SampleCount];
            sortedBuffer = new float[SampleCount];
            sampleIndex = 0;
            sampleCount = 0;
        }

        // =========================================================
        //  热循环 (零 GC)
        // =========================================================

        void Update()
        {
            // F1 切换显示
            if (Input.GetKeyDown(KeyCode.F1))
                isVisible = !isVisible;

            if (!isVisible) return;

            float dt = Time.unscaledDeltaTime;

            // 写入环形缓冲区
            frameTimes[sampleIndex] = dt;
            sampleIndex = (sampleIndex + 1) % SampleCount;
            if (sampleCount < SampleCount) sampleCount++;

            // 计算统计 (纯算术，零 GC)
            CalculateStats();

            // 节流刷新显示字符串 (微量分配，非热路径)
            if (Time.unscaledTime >= nextDisplayUpdate)
            {
                nextDisplayUpdate = Time.unscaledTime + DisplayInterval;
                UpdateDisplayText();
            }
        }

        // =========================================================
        //  统计计算 (零 GC: 无 new, 无 LINQ, 无闭包)
        // =========================================================

        void CalculateStats()
        {
            if (sampleCount == 0) return;

            // 瞬时 FPS
            float dt = frameTimes[(sampleIndex - 1 + SampleCount) % SampleCount];
            currentFPS = dt > 0.0001f ? 1f / dt : 0f;

            // 单遍扫描: sum, min, max
            float sum = 0f;
            float min = float.MaxValue;
            float max = float.MinValue;

            for (int i = 0; i < sampleCount; i++)
            {
                float t = frameTimes[i];
                sum += t;
                if (t < min) min = t;
                if (t > max) max = t;
            }

            float mean = sum / sampleCount;
            frameTimeMs = mean * 1000f;
            avgFPS = 1f / mean;
            minFPS = 1f / max;   // 最差帧 = 最大帧时间
            maxFPS = 1f / min;   // 最佳帧 = 最小帧时间

            // 帧时间标准差 (抖动指标)
            float varianceSum = 0f;
            for (int i = 0; i < sampleCount; i++)
            {
                float diff = frameTimes[i] - mean;
                varianceSum += diff * diff;
            }
            frameTimeJitter = Mathf.Sqrt(varianceSum / sampleCount) * 1000f;

            // 1% Low + 5% Low: 复制到排序缓冲区，in-place 排序 (无 GC)
            System.Array.Copy(frameTimes, sortedBuffer, sampleCount);
            System.Array.Sort(sortedBuffer, 0, sampleCount);

            onePercentLow = CalculateLowPercent(sortedBuffer, sampleCount, 1);
            fivePercentLow = CalculateLowPercent(sortedBuffer, sampleCount, 5);
        }

        /// <summary>
        /// 计算 worst N% 帧的平均帧率 (in-place 排序后调用，零 GC)
        /// </summary>
        static float CalculateLowPercent(float[] sorted, int count, int percent)
        {
            int n = Mathf.Max(1, count * percent / 100);
            float sum = 0f;
            // sorted 升序，最差帧在末尾
            for (int i = count - n; i < count; i++)
                sum += sorted[i];
            return n / sum; // 平均帧率
        }

        // =========================================================
        //  显示文本 (每 0.25s 刷新，微量 string.Format)
        // =========================================================

        void UpdateDisplayText()
        {
            // 每 0.25s 分配一次 ~120 字节，对 90fps 无影响
            displayText = string.Format(
                "FPS {0:F0}  Avg {1:F0}  1%L {2:F0}  5%L {3:F0}\n" +
                "Min {4:F0}  Max {5:F0}  {6:F2}ms  J {7:F2}",
                currentFPS, avgFPS, onePercentLow, fivePercentLow,
                minFPS, maxFPS, frameTimeMs, frameTimeJitter);
        }

        // =========================================================
        //  OnGUI 渲染 (最轻量方案: 无 Canvas, 无 DrawCall)
        // =========================================================

        void InitStyles()
        {
            if (stylesInitialized) return;

            styleGood = new GUIStyle(GUI.skin.label);
            styleGood.fontSize = 18;
            styleGood.fontStyle = FontStyle.Bold;
            styleGood.normal.textColor = new Color(0.2f, 1f, 0.3f);

            styleWarn = new GUIStyle(styleGood);
            styleWarn.normal.textColor = new Color(1f, 0.9f, 0.2f);

            styleBad = new GUIStyle(styleGood);
            styleBad.normal.textColor = new Color(1f, 0.3f, 0.2f);

            styleShadow = new GUIStyle(styleGood);
            styleShadow.normal.textColor = new Color(0f, 0f, 0f, 0.7f);

            stylesInitialized = true;
        }

        void OnGUI()
        {
            if (!isVisible) return;
            if (Event.current.type != EventType.Repaint) return;

            InitStyles();

            // 根据 1% Low 选择颜色
            GUIStyle style;
            if (onePercentLow >= 88f) style = styleGood;   // VR 90fps 达标
            else if (onePercentLow >= 58f) style = styleWarn; // 60fps 勉强
            else style = styleBad;                           // 卡顿

            const float x = 12f;
            const float y = 12f;
            const float w = 420f;
            const float h = 56f;

            // 半透明背景
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(x - 4, y - 4, w + 8, h + 8), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // 文字阴影
            GUI.Label(new Rect(x + 1, y + 1, w, h), displayText, styleShadow);
            // 正文
            GUI.Label(new Rect(x, y, w, h), displayText, style);
        }
    }
}
