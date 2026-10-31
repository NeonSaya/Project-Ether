using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

namespace OsuVR
{
    /// <summary>
    /// 选歌场景独立的短试听播放器，不接管游戏音乐或选曲音效。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SongPreviewPlayer : MonoBehaviour
    {
        private const float PreviewDuration = 15f;
        private const float SelectionDelay = 0.12f;
        private const float FadeOutDuration = 0.18f;
        private const float FadeInDuration = 0.25f;
        private const float LoadTimeout = 15f;

        private AudioSource previewSource;
        private AudioClip previewClip;
        private string loadedAudioPath;
        private string requestedAudioPath;
        private float requestedPreviewTimeMs;
        private Coroutine selectionRoutine;
        private UnityWebRequest pendingRequest;
        private uint requestVersion;
        private float loadDeadline;
        private float fadeGain;
        private bool previewPlaying;
        private double previewEndDspTime;

        private void Awake()
        {
            // 专用子物体避免复用同对象上已有的选曲音效 AudioSource。
            var sourceObject = new GameObject("SongPreviewAudio");
            sourceObject.transform.SetParent(transform, false);
            previewSource = sourceObject.AddComponent<AudioSource>();
            previewSource.playOnAwake = false;
            previewSource.loop = false;
            previewSource.spatialBlend = 0f;
            previewSource.volume = 0f;
        }

        private void OnEnable()
        {
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        private void OnDisable()
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            StopPreview();
        }

        private void OnDestroy()
        {
            StopPreview();
            if (previewSource != null)
                Destroy(previewSource.gameObject);
        }

        /// <summary>
        /// 按最终选择加载试听；同一音频与预览点的难度切换保留当前进度。
        /// </summary>
        public void RequestPreview(BeatmapMetadata metadata)
        {
            if (!isActiveAndEnabled)
                return;

            string audioPath = ResolveAudioPath(metadata);
            if (audioPath == null)
            {
                StopPreview();
                return;
            }

            if (
                SameAudioPath(audioPath, requestedAudioPath)
                && metadata.PreviewTime == requestedPreviewTimeMs
                && (selectionRoutine != null || previewPlaying)
            )
                return;

            CancelSelection();
            requestedAudioPath = audioPath;
            requestedPreviewTimeMs = metadata.PreviewTime;
            selectionRoutine = StartCoroutine(
                PlaySelection(audioPath, metadata.PreviewTime, requestVersion)
            );
        }

        /// <summary>
        /// 退出选歌时立即停止，并取消加载与释放此播放器持有的音频。
        /// </summary>
        public void StopPreview()
        {
            CancelSelection();
            ReleaseClip();
            requestedAudioPath = null;
            fadeGain = 0f;
            if (previewSource != null)
                previewSource.volume = 0f;
        }

        private IEnumerator PlaySelection(string audioPath, float previewTimeMs, uint version)
        {
            // 等待短暂稳定选择，滚动或连续点选不会解码每一首经过的歌曲。
            yield return new WaitForSecondsRealtime(SelectionDelay);
            yield return FadeTo(0f, FadeOutDuration);
            previewPlaying = false;
            previewSource.Stop();

            if (previewClip == null || !SameAudioPath(audioPath, loadedAudioPath))
            {
                ReleaseClip();
                UnityWebRequestAsyncOperation operation = BeginLoad(audioPath);
                if (operation == null)
                {
                    selectionRoutine = null;
                    yield break;
                }

                // 本地解码也使用实际时间上限，防止底层请求卡住后相同选曲永远无法重试。
                while (!operation.isDone && Time.realtimeSinceStartup < loadDeadline)
                    yield return null;
                if (version != requestVersion)
                    yield break;
                if (Time.realtimeSinceStartup >= loadDeadline)
                {
                    Debug.LogWarning("[SongPreview] 试听音频加载超时，请重新选择重试");
                    ReleaseRequest();
                    selectionRoutine = null;
                    requestedAudioPath = null;
                    yield break;
                }

                if (pendingRequest.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[SongPreview] 试听音频加载失败: {pendingRequest.error}");
                    ReleaseRequest();
                    selectionRoutine = null;
                    yield break;
                }

                previewClip = DownloadHandlerAudioClip.GetContent(pendingRequest);
                ReleaseRequest();
                if (previewClip == null || previewClip.samples <= 0)
                {
                    ReleaseClip();
                    selectionRoutine = null;
                    yield break;
                }
                previewClip.name = Path.GetFileName(audioPath);
                loadedAudioPath = audioPath;
            }

            float startSeconds = GetPreviewStart(previewTimeMs, previewClip.length);
            float duration = Mathf.Min(PreviewDuration, previewClip.length - startSeconds);
            previewSource.clip = previewClip;
            previewSource.time = startSeconds;
            previewSource.volume = 0f;
            // 正常结算返回已停旧曲；直接进入选歌的路径在新试听准备成功后再兜底。
            if (
                MusicManager.Instance != null
                && MusicManager.Instance.GetAudioSource() != null
                && MusicManager.Instance.GetAudioSource() != previewSource
            )
                MusicManager.Instance.StopAndDestroy();
            previewSource.Play();
            previewPlaying = true;
            previewEndDspTime = AudioSettings.dspTime + duration;
            yield return FadeTo(1f, Mathf.Min(FadeInDuration, duration * 0.5f));
            selectionRoutine = null;
        }

        private UnityWebRequestAsyncOperation BeginLoad(string audioPath)
        {
            try
            {
                // Uri 正确转义空格、中文、# 与 %，不能直接拼接 file://。
                string uri = new Uri(Path.GetFullPath(audioPath)).AbsoluteUri;
                pendingRequest = UnityWebRequestMultimedia.GetAudioClip(
                    uri,
                    GetAudioType(audioPath)
                );
                ((DownloadHandlerAudioClip)pendingRequest.downloadHandler).streamAudio = false;
                loadDeadline = Time.realtimeSinceStartup + LoadTimeout;
                return pendingRequest.SendWebRequest();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[SongPreview] 无法开始试听: {exception.Message}");
                ReleaseRequest();
                return null;
            }
        }

        private IEnumerator FadeTo(float target, float duration)
        {
            float from = fadeGain;
            if (from == target)
                yield break;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                fadeGain = Mathf.Lerp(from, target, Mathf.Clamp01(elapsed / duration));
                ApplyVolume();
                yield return null;
            }
            fadeGain = target;
            ApplyVolume();
        }

        private void Update()
        {
            if (previewPlaying)
            {
                float remaining = (float)(previewEndDspTime - AudioSettings.dspTime);
                if (remaining <= 0f)
                {
                    // 当前片段到期与最终选择分开处理，不能取消仍在去抖或加载的新曲。
                    ReleaseClip();
                    fadeGain = 0f;
                    if (selectionRoutine == null)
                        requestedAudioPath = null;
                }
                else if (selectionRoutine == null)
                    fadeGain = Mathf.Min(1f, remaining / FadeOutDuration);
            }
            ApplyVolume();
        }

        private void ApplyVolume()
        {
            if (previewSource == null)
                return;
            GameSettings settings =
                SettingsManager.Instance != null ? SettingsManager.Instance.Settings : null;
            // 专用音源不路由 Mixer，主音量与音乐音量在这里各应用一次。
            previewSource.volume =
                (previewPlaying ? fadeGain : 0f)
                * (
                    settings != null
                        ? Mathf.Clamp01(settings.masterVolume) * Mathf.Clamp01(settings.musicVolume)
                        : 0.8f
                );
        }

        private void CancelSelection()
        {
            requestVersion++;
            if (selectionRoutine != null)
            {
                StopCoroutine(selectionRoutine);
                selectionRoutine = null;
            }
            ReleaseRequest();
        }

        private void ReleaseRequest()
        {
            if (pendingRequest == null)
                return;

            // 完成解码但还未被协程接收的 Clip 也归此播放器释放。
            if (pendingRequest.isDone && pendingRequest.result == UnityWebRequest.Result.Success)
            {
                var handler = pendingRequest.downloadHandler as DownloadHandlerAudioClip;
                AudioClip downloadedClip = handler != null ? handler.audioClip : null;
                if (downloadedClip != null && downloadedClip != previewClip)
                    Destroy(downloadedClip);
            }
            if (!pendingRequest.isDone)
                pendingRequest.Abort();
            pendingRequest.Dispose();
            pendingRequest = null;
        }

        private void ReleaseClip()
        {
            previewPlaying = false;
            if (previewSource != null)
            {
                previewSource.Stop();
                previewSource.clip = null;
            }
            if (previewClip != null)
                Destroy(previewClip);
            previewClip = null;
            loadedAudioPath = null;
        }

        private void OnActiveSceneChanged(Scene previous, Scene next) => StopPreview();

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene != gameObject.scene)
                StopPreview();
        }

        private void OnSceneUnloaded(Scene scene)
        {
            if (scene == gameObject.scene)
                StopPreview();
        }

        private static float GetPreviewStart(float previewTimeMs, float clipLength)
        {
            float desired =
                float.IsNaN(previewTimeMs) || float.IsInfinity(previewTimeMs) || previewTimeMs < 0f
                    ? clipLength * 0.5f
                    : previewTimeMs / 1000f;
            // 留下至少一个短片段；短于试听时长的音频自然播放到末尾。
            return Mathf.Clamp(desired, 0f, Mathf.Max(0f, clipLength - 0.25f));
        }

        private static bool SameAudioPath(string left, string right) =>
            string.Equals(
                left,
                right,
                Application.platform == RuntimePlatform.Android
                    ? StringComparison.Ordinal
                    : StringComparison.OrdinalIgnoreCase
            );

        private static AudioType GetAudioType(string path)
        {
            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".mp3":
                    return AudioType.MPEG;
                case ".ogg":
                    return AudioType.OGGVORBIS;
                case ".wav":
                    return AudioType.WAV;
                case ".aiff":
                case ".aif":
                    return AudioType.AIFF;
                default:
                    return AudioType.UNKNOWN;
            }
        }

        private static string ResolveAudioPath(BeatmapMetadata metadata)
        {
            if (metadata == null || string.IsNullOrWhiteSpace(metadata.AudioFilename))
                return null;
            try
            {
                string folder = metadata.FolderPath;
                if (string.IsNullOrEmpty(folder) && !string.IsNullOrEmpty(metadata.OsuFilePath))
                    folder = Path.GetDirectoryName(metadata.OsuFilePath);
                if (string.IsNullOrEmpty(folder))
                    return null;

                string relative = metadata.AudioFilename.Trim().Trim('"').Replace('\\', '/');
                string exact = Path.GetFullPath(Path.Combine(folder, relative));
                if (File.Exists(exact))
                    return exact;

                // Android 逐段回退，处理谱面引用与真实目录、文件的大小写差异。
                string current = Path.GetFullPath(folder);
                string[] parts = relative.Split('/');
                for (int i = 0; i < parts.Length; i++)
                {
                    string part = parts[i];
                    if (string.IsNullOrEmpty(part) || part == ".")
                        continue;
                    string candidate = Path.Combine(current, part);
                    if (Directory.Exists(candidate) || File.Exists(candidate))
                    {
                        current = candidate;
                        continue;
                    }
                    if (!Directory.Exists(current))
                        return null;
                    string match = null;
                    foreach (string entry in Directory.EnumerateFileSystemEntries(current))
                    {
                        if (
                            string.Equals(
                                Path.GetFileName(entry),
                                part,
                                StringComparison.OrdinalIgnoreCase
                            )
                        )
                        {
                            match = entry;
                            break;
                        }
                    }
                    if (match == null)
                        return null;
                    current = match;
                }
                return File.Exists(current) ? current : null;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[SongPreview] 无法定位试听音频: {exception.Message}");
                return null;
            }
        }
    }
}
