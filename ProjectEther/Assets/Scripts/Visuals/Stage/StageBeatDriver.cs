using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace OsuVR
{
    /// <summary>
    /// 舞台节拍源。按 BPM 生成节拍包络（编辑器里用 realtimeSinceStartup，所以编辑模式也会走），
    /// 也可以用下面的滑杆手动定格某一拍来调风格。频谱暂未接 AudioLink，
    /// 等舞台风格定下来、并入正式场景时再接项目既有的音频通道。
    /// </summary>
    [ExecuteAlways]
    public class StageBeatDriver : MonoBehaviour
    {
        [Header("手动预览（调风格用）")]
        [Tooltip("勾上后不再按 BPM 自动脉冲，改用下面的滑杆定格一个瞬间")]
        public bool manualPreview = false;
        [Range(0f, 1f)] public float previewBeat = 0.7f;
        [Range(0f, 1f)] public float previewLow = 0.7f;
        [Range(0f, 1f)] public float previewHigh = 0.35f;
        [Tooltip("手动预览时的拍序，奇偶决定交替色")]
        public int previewBeatIndex = 0;

        [Header("自动脉冲")]
        [Range(40f, 220f)] public float bpm = 128f;
        [Tooltip("拍点后包络的衰减时间常数（秒），越小越干脆")]
        [Range(0.02f, 0.6f)] public float pulseDecay = 0.22f;

        [Header("外部节拍源")]
        [Tooltip("从 EtherealEnvironment 读取节拍与 Kiai（全局舞台用）；关掉则自己按 BPM 跑")]
        public bool useEnvironmentBeat = false;

        [Header("编辑器")]
        [Tooltip("编辑模式下持续刷新场景视图，直接就能看到动画")]
        public bool animateInEditor = true;

        /// <summary>节拍包络 0..1：拍点为 1，之后指数衰减。</summary>
        public float Beat { get; private set; }
        /// <summary>低频能量 0..1（当前等于节拍包络）。</summary>
        public float Low { get; private set; }
        /// <summary>高频能量 0..1（当前由包络的高段近似）。</summary>
        public float High { get; private set; }
        /// <summary>第几拍，用于步进式跳变与奇偶交替。</summary>
        public long BeatIndex { get; private set; }

        float _lastRepaintTime;

        void Update()
        {
            if (useEnvironmentBeat && EtherealEnvironment.Instance != null)
            {
                EtherealEnvironment env = EtherealEnvironment.Instance;
                Beat = Mathf.Clamp01(env.BeatPulse);
                Low = Beat;
                High = Mathf.Clamp01((Beat - 0.45f) / 0.55f);
                BeatIndex = (long)(Time.realtimeSinceStartup * Mathf.Max(1f, bpm) / 60f);
            }
            else if (manualPreview)
            {
                Beat = previewBeat;
                Low = previewLow;
                High = previewHigh;
                BeatIndex = previewBeatIndex;
            }
            else
            {
                float secondsPerBeat = 60f / Mathf.Max(1f, bpm);
                float now = Time.realtimeSinceStartup;
                long index = (long)(now / secondsPerBeat);
                float sinceBeat = now - index * secondsPerBeat;

                BeatIndex = index;
                Beat = Mathf.Exp(-sinceBeat / Mathf.Max(0.01f, pulseDecay));
                Low = Beat;
                High = Mathf.Clamp01((Beat - 0.45f) / 0.55f);
            }

            FlushEditorRepaint();
        }

        void FlushEditorRepaint()
        {
#if UNITY_EDITOR
            PumpEditor(force: false);
#endif
        }

#if UNITY_EDITOR
        // 编辑模式没有自动的玩家循环：靠 EditorApplication.update 常驻请求刷新，
        // 否则 ExecuteAlways 的 Update 不会跑，舞台在域重载后也不会重建。
        static readonly System.Collections.Generic.List<StageBeatDriver> _pumps =
            new System.Collections.Generic.List<StageBeatDriver>();

        void OnEnable()
        {
            RegisterPump();
        }

        void OnDisable()
        {
            UnregisterPump();
        }

        void RegisterPump()
        {
            if (!_pumps.Contains(this)) _pumps.Add(this);
            EditorApplication.update -= PumpTick;
            EditorApplication.update += PumpTick;
            PumpEditor(force: true);
        }

        void UnregisterPump()
        {
            _pumps.Remove(this);
            if (_pumps.Count == 0)
            {
                EditorApplication.update -= PumpTick;
            }
        }

        static void PumpTick()
        {
            for (int i = _pumps.Count - 1; i >= 0; i--)
            {
                StageBeatDriver driver = _pumps[i];
                if (driver == null)
                {
                    _pumps.RemoveAt(i);
                    continue;
                }
                driver.PumpEditor(force: false);
            }
        }

        void PumpEditor(bool force)
        {
            if (Application.isPlaying) return;
            float now = Time.realtimeSinceStartup;
            if (!force)
            {
                if (!animateInEditor) return;
                if (now - _lastRepaintTime < 1f / 30f) return;
            }
            _lastRepaintTime = now;
            EditorApplication.QueuePlayerLoopUpdate();
            if (animateInEditor) SceneView.RepaintAll();
        }
#endif
    }
}
