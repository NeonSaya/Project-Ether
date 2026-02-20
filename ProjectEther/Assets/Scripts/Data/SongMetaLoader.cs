using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace OsuVR
{
    // 简化的谱面元数据，用于选歌界面显示
    public class BeatmapMetadata
    {
        public string Title;
        public string Artist;
        public string Creator;
        public string Version;
        public string AudioFilename;
        public string BackgroundFilename;
        public string FolderPath;
        public string OsuFilePath;
        public float BPM = 120f;
        public float Length = 180f;
        public float PreviewTime = 0f;
    }

    public static class SongMetaLoader
    {
        /// <summary>
        /// 扫描 Songs 目录，返回所有可玩的谱面信息
        /// </summary>
        public static List<BeatmapMetadata> ScanSongFolder()
        {
            List<BeatmapMetadata> maps = new List<BeatmapMetadata>();
            string rootPath = BeatmapImporter.SongsDirectory;


            if (!Directory.Exists(rootPath)) return maps;

            // 遍历每个歌曲文件夹
            string[] songDirs = Directory.GetDirectories(rootPath);
            foreach (var dir in songDirs)
            {
                // 在文件夹里找 .osu 文件
                string[] osuFiles = Directory.GetFiles(dir, "*.osu");
                foreach (var osuFile in osuFiles)
                {
                    BeatmapMetadata meta = ParseHeader(osuFile);
                    if (meta != null)
                    {
                        meta.FolderPath = dir;
                        meta.OsuFilePath = osuFile;
                        maps.Add(meta);
                    }
                }
            }
            return maps;
        }

        // 只读取头部信息，不解析 HitObjects
        private static BeatmapMetadata ParseHeader(string filePath)
        {
            BeatmapMetadata meta = new BeatmapMetadata();
            try
            {
                foreach (string line in File.ReadAllLines(filePath))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    if (line.StartsWith("[HitObjects]")) break; // 读到这里就停，后面不需要

                    if (line.StartsWith("AudioFilename:")) meta.AudioFilename = line.Substring(14).Trim();
                    else if (line.StartsWith("Title:")) meta.Title = line.Substring(6).Trim();
                    else if (line.StartsWith("Artist:")) meta.Artist = line.Substring(7).Trim();
                    else if (line.StartsWith("Creator:")) meta.Creator = line.Substring(8).Trim();
                    else if (line.StartsWith("Version:")) meta.Version = line.Substring(8).Trim();
                    // 处理背景图通常比较复杂，通常在 [Events] 下，这里先略过
                }
                if (string.IsNullOrEmpty(meta.AudioFilename)) return null;

                return meta;
            }
            catch
            {
                return null;
            }
        }
    }
}