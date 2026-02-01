using UnityEngine;
using System.Reflection;

namespace OsuVR
{
    /// <summary>
    /// Relax 模式判定器
    /// 挂载在 Note/Slider 预制体上，自动检测悬停并触发打击
    /// </summary>
    public class RelaxMod : MonoBehaviour
    {
        [Header("Relax 设置")]
        public bool isRelaxEnabled = true;

        [Tooltip("打击偏移 (ms)：正数表示延迟打击。例如 -20ms 表示即使射线早就指到了，也要等到音符时间-20ms才触发。")]
        public float hitTimingOffsetMs = -20f;

        [Tooltip("最晚判定窗口 (ms)：如果超过这个时间还没打，就不再自动操作 (视为Miss)")]
        public float missWindowMs = 150f;

        [Header("手感优化")]
        [Tooltip("容错记忆时间 (ms)：只要在判定前的这段时间内指过音符，移开了也算瞄准。\n解决快速挥动(Flick)时因为判定延迟导致的漏打问题。")]
        public float latchMemoryMs = 50f;

        private double lastNoteHoverTime = -10000;
        private double lastSliderHoverTime = -10000;

        // 内部引用
        private NoteController noteController;
        private SliderController sliderController;
        private RhythmGameManager gameManager;

        // 状态锁，防止重复触发
        private bool hasTriggered = false;

        void Start()
        {
            // 自动获取同物体上的 Controller
            noteController = GetComponent<NoteController>();
            sliderController = GetComponent<SliderController>();

            // 寻找 GameManager (假设它是单例或在场景中唯一)
            gameManager = FindFirstObjectByType<RhythmGameManager>();
        }

        void Update()
        {
            if (!isRelaxEnabled || gameManager == null || hasTriggered) return;

            double currentTime = gameManager.GetCurrentMusicTimeMs();

            // --- 1. 处理 Note ---
            if (noteController != null && noteController.isActive)
            {
                // ✅ 更新记忆：只要指着，就刷新最后接触时间
                if (noteController.isHovered)
                {
                    lastNoteHoverTime = currentTime;
                }

                // 检查判定：现在改为检查“记忆是否有效”
                CheckHitTiming(currentTime, noteController.hitObject.StartTime, true, lastNoteHoverTime);
            }

            // --- 2. 处理 Slider ---
            if (sliderController != null)
            {
                // ✅ 更新记忆
                if (sliderController.isTracking)
                {
                    lastSliderHoverTime = currentTime;
                }

                // 检查判定
                CheckHitTiming(currentTime, sliderController.sliderData.StartTime, false, lastSliderHoverTime);
            }
        }


        // ✅ [新增] 核心压点逻辑方法
        private void CheckHitTiming(double currentTime, double targetTime, bool isNote, double lastHoverTime)
        {
            // 1. 检查瞄准状态 (核心优化)
            // 只要 (当前时间 - 最后接触时间) 小于 记忆窗口，就认为“瞄准有效”
            // 这样即使你提前划走了，只要还在 100ms 内，判定依然生效
            bool isAiming = (currentTime - lastHoverTime) <= latchMemoryMs;

            if (!isAiming) return; // 如果早就移开了，就不判定

            // 2. 检查时间压点 (保持你原本的逻辑)
            double diff = currentTime - targetTime;

            if (diff >= hitTimingOffsetMs && diff <= missWindowMs)
            {
                if (isNote) PerformNoteHit();
                else PerformSliderHeadHit();
            }
        }


        // --- 执行打击 (使用 SendMessage 调用私有方法) ---

        private void PerformNoteHit()
        {
            hasTriggered = true;

            // 1. 告诉 GameManager 加分 (模拟完美判定)
            gameManager.OnNoteHit(noteController.hitObject, 0); // 0 代表 300分/完美

            // 2. 播放打击音效 (如果有独立音效管理，可以在这里加)
            // gameManager.PlayHitSound(); 

            // 3. 销毁 Note
            // 技巧：NoteController 里有一个 "ReturnToPool" 私有方法
            // 我们用 SendMessage 强行调用它，这样不用改代码也能回收物体
            SendMessage("ReturnToPool", SendMessageOptions.DontRequireReceiver);

            // 双重保险：如果 SendMessage 没起作用（比如方法名改了），直接隐藏
            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }

        private void PerformSliderHeadHit()
        {
            hasTriggered = true;

            // 1. 触发 Slider 的打击逻辑 (让球开始动)
            SendMessage("HitHead", SendMessageOptions.DontRequireReceiver);

            // 2. ✅ [新增] 强制视觉反馈：打中瞬间隐藏滑条头
            // 这会让滑条的手感和 Note 一样干脆 (打中 -> 头消失 -> 球滑出)

            // 尝试查找常见的头部子物体名称
            Transform head = transform.Find("HitCircle");
            if (head == null) head = transform.Find("Head");
            if (head == null) head = transform.Find("Circle");

            // 如果找到了头部，就隐藏它
            if (head != null)
            {
                head.gameObject.SetActive(false);
            }
            else
            {
                // 如果找不到特定名字，尝试寻找第一个 MeshRenderer 且不是滑条轨道的物体 (备选方案)
                // var renderer = GetComponentInChildren<MeshRenderer>();
                // if (renderer) renderer.enabled = false;
            }
            }
            /// <summary>
            /// 当物体被对象池回收再复用时，重置状态
            /// </summary>
            void OnEnable()
        {
            hasTriggered = false;
        }
    }
}