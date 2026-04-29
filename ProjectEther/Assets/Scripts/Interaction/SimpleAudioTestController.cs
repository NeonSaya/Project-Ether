#if UNITY_EDITOR
using UnityEngine;

namespace OsuVR
{
    /// <summary>
    /// 简单的音频测试控制器：按空格键播放/暂停音频
    /// 仅在编辑器模式下编译
    /// </summary>
    public class SimpleAudioTestController : MonoBehaviour
    {
        [Header("测试设置")]
        [Tooltip("要测试的AudioSource")]
        public AudioSource testAudioSource;

        [Tooltip("是否在Start时自动播放")]
        public bool autoPlayOnStart = false;

        void Start()
        {
            if (testAudioSource == null)
            {
                testAudioSource = FindFirstObjectByType<AudioSource>();
            }

            if (testAudioSource == null)
            {
                Debug.LogError("[SimpleAudioTestController] 未找到AudioSource！");
                return;
            }

            if (autoPlayOnStart && testAudioSource.clip != null)
            {
                testAudioSource.Play();
                Debug.Log($"[SimpleAudioTestController] 自动播放: {testAudioSource.clip.name}");
            }
        }

        void Update()
        {
            if (testAudioSource == null) return;

            // 空格键：播放/暂停
            if (Input.GetKeyDown(KeyCode.Space))
            {
                if (testAudioSource.isPlaying)
                {
                    testAudioSource.Pause();
                    Debug.Log("[SimpleAudioTestController] 暂停播放");
                }
                else
                {
                    if (testAudioSource.clip == null)
                    {
                        Debug.LogWarning("[SimpleAudioTestController] 没有分配AudioClip！");
                        return;
                    }
                    testAudioSource.Play();
                    Debug.Log($"[SimpleAudioTestController] 开始播放: {testAudioSource.clip.name}");
                }
            }

            // R键：重新播放
            if (Input.GetKeyDown(KeyCode.R))
            {
                testAudioSource.Stop();
                testAudioSource.time = 0;
                testAudioSource.Play();
                Debug.Log("[SimpleAudioTestController] 重新播放");
            }
        }

        void OnGUI()
        {
            if (testAudioSource == null) return;

            GUILayout.BeginArea(new Rect(Screen.width - 250, 10, 240, 100));
            GUILayout.BeginVertical("box");

            GUILayout.Label("<b>音频测试控制</b>", new GUIStyle(GUI.skin.label) { richText = true });
            GUILayout.Label($"Clip: {testAudioSource.clip?.name ?? "未分配"}");
            GUILayout.Label($"状态: {(testAudioSource.isPlaying ? "▶ 播放中" : "⏸ 暂停")}");
            GUILayout.Label($"时间: {testAudioSource.time:F1}s / {testAudioSource.clip?.length ?? 0:F1}s");
            GUILayout.Label("<color=yellow>[Space] 播放/暂停  [R] 重播</color>", new GUIStyle(GUI.skin.label) { richText = true });

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
    }
}
#endif
