using System.IO;
using UnityEngine;

namespace OsuVR.Storyboard
{
    /// <summary>
    /// 资产嗅探器：在谱面目录中检测视频、故事板(.osb)、静态背景图等媒体资产
    /// </summary>
    public static class MediaAssetScanner
    {
        public struct ScanResult
        {
            public bool HasVideo;
            public string VideoPath;
            public int VideoOffset;
            public bool HasStoryboard;
            public string OsbPath;
            public int StoryboardCommandCount;
            public string BackgroundPath;
        }

        /// <summary>Unity VideoPlayer 跨平台可解码的格式（Windows WMF / Android MediaCodec 均保证）</summary>
        static readonly string[] SupportedVideoExtensions = { ".mp4", ".webm", ".mov" };

        /// <summary>
        /// 扫描谱面目录，检测可用的媒体资产
        /// </summary>
        public static ScanResult Scan(Beatmap beatmap, string osuFilePath)
        {
            string folder = Path.GetDirectoryName(osuFilePath);
            var result = new ScanResult();

            // 1. 从 [Events] 解析结果获取视频引用
            if (!string.IsNullOrEmpty(beatmap.Events.VideoFilename))
            {
                string path = Path.Combine(folder, beatmap.Events.VideoFilename);
                if (File.Exists(path))
                {
                    string ext = Path.GetExtension(path).ToLowerInvariant();
                    if (System.Array.IndexOf(SupportedVideoExtensions, ext) >= 0)
                    {
                        result.HasVideo = true;
                        result.VideoPath = path;
                        result.VideoOffset = beatmap.Events.VideoOffset;
                        Debug.Log($"[MediaScanner] 检测到视频: {beatmap.Events.VideoFilename}");
                    }
                    else
                    {
                        // 此前直接把 .avi/.mkv/.flv 交给 VideoPlayer，必然解码失败
                        // 现在显式跳过并回退背景图，日志告知原因
                        Debug.LogWarning($"[MediaScanner] 视频格式 VideoPlayer 无法解码 ({ext})，回退到背景图: {beatmap.Events.VideoFilename}");
                    }
                }
                else
                {
                    Debug.LogWarning($"[MediaScanner] [Events] 引用了视频文件但不存在: {path}");
                }
            }

            // 2. 扫描目录中的 .osb 文件
            try
            {
                string[] osbFiles = Directory.GetFiles(folder, "*.osb");
                if (osbFiles.Length > 0)
                {
                    result.HasStoryboard = true;
                    result.OsbPath = osbFiles[0];
                    Debug.Log($"[MediaScanner] 检测到 .osb: {Path.GetFileName(osbFiles[0])}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[MediaScanner] 扫描 .osb 失败: {e.Message}");
            }

            // 3. 检查 [Events] 中是否有内联 Storyboard 命令
            if (beatmap.Events.StoryboardLines.Count > 0)
            {
                result.HasStoryboard = true;
                result.StoryboardCommandCount = beatmap.Events.StoryboardLines.Count;
                Debug.Log($"[MediaScanner] 检测到内联故事板命令: {result.StoryboardCommandCount} 行");
            }

            // 4. 查找静态背景图
            if (!string.IsNullOrEmpty(beatmap.Events.BackgroundFilename))
            {
                string bgPath = Path.Combine(folder, beatmap.Events.BackgroundFilename);
                if (File.Exists(bgPath))
                {
                    result.BackgroundPath = bgPath;
                }
            }

            return result;
        }
    }
}
