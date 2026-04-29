#if UNITY_EDITOR
using UnityEngine;

namespace OsuVR
{
    /// <summary>
    /// 音频可视化调试工具：帮助排查音频响应问题
    /// 仅在编辑器模式下编译
    /// </summary>
    public class AudioVisualizationDebugger : MonoBehaviour
    {
        [Header("调试信息")]
        [SerializeField] private bool showDebugInfo = true;
        [SerializeField] private bool logToConsole = true;

        [Header("实时数据")]
        [SerializeField] private float bassValue;
        [SerializeField] private float midValue;
        [SerializeField] private float trebleValue;
        [SerializeField] private bool hasAudioSource;
        [SerializeField] private bool isAudioPlaying;
        [SerializeField] private string audioSourceName;
        [SerializeField] private string audioClipName;

        void Update()
        {
            if (!showDebugInfo) return;

            var manager = AudioVisualizationManager.Instance;
            if (manager == null)
            {
                if (logToConsole) Debug.LogError("[AudioVisualizationDebugger] AudioVisualizationManager.Instance 为 null！");
                return;
            }

            bassValue = manager.Bass;
            midValue = manager.Mid;
            trebleValue = manager.Treble;

            var audioSource = manager.targetAudioSource;
            hasAudioSource = audioSource != null;

            if (audioSource != null)
            {
                isAudioPlaying = audioSource.isPlaying;
                audioSourceName = audioSource.gameObject.name;
                audioClipName = audioSource.clip?.name ?? "null";
            }
            else
            {
                isAudioPlaying = false;
                audioSourceName = "null";
                audioClipName = "null";
            }
        }

        void OnGUI()
        {
            if (!showDebugInfo) return;

            GUILayout.BeginArea(new Rect(10, 10, 400, 200));
            GUILayout.BeginVertical("box");

            GUILayout.Label("<b>Audio Visualization Debugger</b>", new GUIStyle(GUI.skin.label) { richText = true });
            GUILayout.Space(5);

            var manager = AudioVisualizationManager.Instance;
            if (manager == null)
            {
                GUILayout.Label("<color=red>AudioVisualizationManager 未找到！</color>", new GUIStyle(GUI.skin.label) { richText = true });
                GUILayout.Label("请确保场景中有 AudioVisualizationSystem 物体", new GUIStyle(GUI.skin.label) { richText = true });
            }
            else
            {
                GUILayout.Label("<color=green>AudioVisualizationManager 存在</color>", new GUIStyle(GUI.skin.label) { richText = true });

                GUILayout.Space(5);
                GUILayout.Label($"Bass: {bassValue:F3}");
                GUILayout.Label($"Mid:  {midValue:F3}");
                GUILayout.Label($"Treble: {trebleValue:F3}");

                GUILayout.Space(5);
                if (hasAudioSource)
                {
                    GUILayout.Label($"<color=green>AudioSource: {audioSourceName}</color>", new GUIStyle(GUI.skin.label) { richText = true });
                    GUILayout.Label($"Clip: {audioClipName}");
                    GUILayout.Label(isAudioPlaying ? "<color=green>正在播放</color>" : "<color=yellow>未播放</color>", new GUIStyle(GUI.skin.label) { richText = true });
                }
                else
                {
                    GUILayout.Label("<color=red>AudioSource 未设置！</color>", new GUIStyle(GUI.skin.label) { richText = true });
                }
            }

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        [ContextMenu("完整诊断")]
        public void FullDiagnosis()
        {
            Debug.Log("=== AudioVisualization 完整诊断 ===");

            var manager = AudioVisualizationManager.Instance;
            if (manager == null)
            {
                Debug.LogError("AudioVisualizationManager.Instance 为 null");
                Debug.LogError("解决方案：在场景中创建物体并挂载 AudioVisualizationManager 脚本");
                return;
            }

            Debug.Log($"AudioVisualizationManager 存在于: {manager.gameObject.name}");
            Debug.Log($"  - Lock Target: {manager.lockTargetSource}");
            Debug.Log($"  - Bass: {manager.Bass:F3}");
            Debug.Log($"  - Mid: {manager.Mid:F3}");
            Debug.Log($"  - Treble: {manager.Treble:F3}");

            var audioSource = manager.targetAudioSource;
            if (audioSource == null)
            {
                Debug.LogError("targetAudioSource 为 null");
                Debug.LogError("解决方案：");
                Debug.LogError("  1. 确保场景中有 AudioSource");
                Debug.LogError("  2. 或手动设置 AudioVisualizationManager.targetAudioSource");
                Debug.LogError("  3. 或取消勾选 lockTargetSource 让系统自动查找");
            }
            else
            {
                Debug.Log($"AudioSource: {audioSource.gameObject.name}");
                Debug.Log($"  - Clip: {audioSource.clip?.name ?? "null"}");
                Debug.Log($"  - Is Playing: {audioSource.isPlaying}");
                Debug.Log($"  - Volume: {audioSource.volume}");
                Debug.Log($"  - Time: {audioSource.time:F2}s");

                if (audioSource.clip == null)
                {
                    Debug.LogError("AudioClip 为 null！请分配音频文件到 AudioSource");
                }
                else if (!audioSource.isPlaying)
                {
                    Debug.LogWarning("AudioSource 未在播放！");
                    Debug.LogWarning("解决方案：确保 playOnAwake 为 true，或在代码中调用 Play()");
                }
            }

            var vfxDrivers = FindObjectsByType<AudioVFXDriver>(FindObjectsSortMode.None);
            Debug.Log($"找到 {vfxDrivers.Length} 个 AudioVFXDriver");
            foreach (var driver in vfxDrivers)
            {
                Debug.Log($"  - {driver.gameObject.name}: Band={driver.frequencyBand}, Target={driver.driveTarget}");
            }

            Debug.Log("=== 诊断完成 ===");
        }
    }
}
#endif
