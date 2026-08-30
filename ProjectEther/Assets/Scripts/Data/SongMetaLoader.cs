using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.Scripting;

namespace OsuVR
{
    [Preserve]
    [Serializable]
    public class BeatmapMetadata
    {
        public string Title;
        public string TitleUnicode;
        public string Artist;
        public string ArtistUnicode;
        public string Creator;
        public string Version;
        public string AudioFilename;
        public string BackgroundFilename;
        public string FolderPath;
        public string OsuFilePath;
        public float BPM = 120f;
        public float Length = 0f;
        public float PreviewTime = 0f;
        public float CircleSize = 5f;
        public float ApproachRate = 5f;
        public float OverallDifficulty = 5f;
        public float HPDrainRate = 5f;
        public int FileFormatVersion = 14;
        public int Mode = 0; // 0=osu!, 1=taiko, 2=ctb, 3=mania；本游戏仅支持 0

        public string GetDisplayTitle(bool useOriginalLanguage)
        {
            if (useOriginalLanguage && !string.IsNullOrEmpty(TitleUnicode))
                return TitleUnicode;
            return Title;
        }

        public string GetDisplayArtist(bool useOriginalLanguage)
        {
            if (useOriginalLanguage && !string.IsNullOrEmpty(ArtistUnicode))
                return ArtistUnicode;
            return Artist;
        }

        public string GetDisplayLength()
        {
            int totalSeconds = Mathf.RoundToInt(Length / 1000f);
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            return $"{minutes}:{seconds:D2}";
        }
    }

    [Preserve]
    public class BeatmapSet
    {
        public string Title;
        public string Artist;
        public string FolderPath;
        public string BackgroundPath;
        public List<BeatmapMetadata> Difficulties = new List<BeatmapMetadata>();

        public BeatmapMetadata GetDefaultDifficulty()
        {
            if (Difficulties.Count == 0) return null;
            
            foreach (var diff in Difficulties)
            {
                if (!string.IsNullOrEmpty(diff.Version) && 
                    (diff.Version.ToLower().Contains("normal") || diff.Version.ToLower().Contains("easy")))
                    return diff;
            }
            
            return Difficulties[Difficulties.Count / 2];
        }
    }

    public static class SongMetaLoader
    {
        // =========================================================
        // 谱面索引缓存：按 .osu 路径键控，mtime+文件大小校验命中。
        // 无效谱面（坏谱/非 osu! 模式）做负缓存，避免每次进选曲重扫全库。
        // =========================================================
        [Serializable]
        private class SongIndexEntry
        {
            public string path;            // .osu 绝对路径
            public long lastWriteTicks;    // LastWriteTimeUtc.Ticks
            public long fileSize;
            public BeatmapMetadata meta;   // null = 已知无效（坏谱/非 osu! 模式）
        }

        [Serializable]
        private class SongIndexCache
        {
            public int version = 1;
            public List<SongIndexEntry> entries = new List<SongIndexEntry>();
        }

        private const int IndexCacheVersion = 2; // 解析逻辑变更时 +1，使旧缓存自动失效重建
        private static string IndexCachePath => Path.Combine(Application.persistentDataPath, "song_index.json");

        private enum Section
        {
            None,
            General,
            Metadata,
            Difficulty,
            Events,
            TimingPoints,
            Colours,
            HitObjects
        }

        public static List<BeatmapMetadata> ScanSongFolder()
        {
            return ScanAllWithCache();
        }

        /// <summary>
        /// 全库扫描（带缓存）：只有新增/变更的 .osu 才重新解析，
        /// 命中缓存的复用元数据，删除的文件自动从缓存剔除。
        /// </summary>
        private static List<BeatmapMetadata> ScanAllWithCache()
        {
            var result = new List<BeatmapMetadata>();
            string rootPath = BeatmapImporter.SongsDirectory;

            if (!Directory.Exists(rootPath)) return result;

            var cache = LoadIndexCache();
            var cacheMap = new Dictionary<string, SongIndexEntry>();
            foreach (var e in cache.entries)
            {
                if (e != null && !string.IsNullOrEmpty(e.path))
                    cacheMap[e.path] = e;
            }

            var newEntries = new List<SongIndexEntry>();
            bool dirty = false;

            string[] songDirs = Directory.GetDirectories(rootPath);
            foreach (var dir in songDirs)
            {
                string[] osuFiles = Directory.GetFiles(dir, "*.osu");
                foreach (var osuFile in osuFiles)
                {
                    var fi = new FileInfo(osuFile);

                    bool hit = cacheMap.TryGetValue(osuFile, out SongIndexEntry entry)
                        && entry.lastWriteTicks == fi.LastWriteTimeUtc.Ticks
                        && entry.fileSize == fi.Length;

                    if (!hit)
                    {
                        entry = new SongIndexEntry
                        {
                            path = osuFile,
                            lastWriteTicks = fi.LastWriteTimeUtc.Ticks,
                            fileSize = fi.Length,
                            meta = ParseHeader(osuFile) // 坏谱/非 osu! 模式返回 null（负缓存）
                        };
                        dirty = true;
                    }

                    newEntries.Add(entry);

                    if (entry.meta == null) continue; // 已知无效，不入列表

                    entry.meta.FolderPath = dir;
                    entry.meta.OsuFilePath = osuFile;
                    result.Add(entry.meta);
                }
            }

            // 文件被删除（newEntries 变少）或有新增/变更时，回写缓存
            if (dirty || newEntries.Count != cache.entries.Count)
            {
                cache.entries = newEntries;
                SaveIndexCache(cache);
            }

            return result;
        }

        private static SongIndexCache LoadIndexCache()
        {
            try
            {
                if (File.Exists(IndexCachePath))
                {
                    string json = File.ReadAllText(IndexCachePath);
                    var cache = JsonUtility.FromJson<SongIndexCache>(json);
                    if (cache != null && cache.version == IndexCacheVersion && cache.entries != null)
                        return cache;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SongMetaLoader] 谱面索引缓存读取失败，将重建: {e.Message}");
            }
            return new SongIndexCache { version = IndexCacheVersion };
        }

        private static void SaveIndexCache(SongIndexCache cache)
        {
            try
            {
                File.WriteAllText(IndexCachePath, JsonUtility.ToJson(cache));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SongMetaLoader] 谱面索引缓存写入失败: {e.Message}");
            }
        }

        public static List<BeatmapSet> ScanSongFolderGrouped()
        {
            List<BeatmapSet> sets = new List<BeatmapSet>();
            var setMap = new Dictionary<string, BeatmapSet>();

            var metas = ScanAllWithCache();
            foreach (var meta in metas)
            {
                if (!setMap.TryGetValue(meta.FolderPath, out BeatmapSet set))
                {
                    set = new BeatmapSet { FolderPath = meta.FolderPath };
                    setMap[meta.FolderPath] = set;
                    sets.Add(set);
                }

                set.Difficulties.Add(meta);

                if (string.IsNullOrEmpty(set.Title))
                    set.Title = meta.Title;
                if (string.IsNullOrEmpty(set.Artist))
                    set.Artist = meta.Artist;
                if (string.IsNullOrEmpty(set.BackgroundPath) && !string.IsNullOrEmpty(meta.BackgroundFilename))
                    set.BackgroundPath = Path.Combine(meta.FolderPath, meta.BackgroundFilename);
            }

            foreach (var set in sets)
            {
                set.Difficulties.Sort((a, b) => a.OverallDifficulty.CompareTo(b.OverallDifficulty));
            }
            return sets;
        }

        private static BeatmapMetadata ParseHeader(string filePath)
        {
            BeatmapMetadata meta = new BeatmapMetadata();
            Section section = Section.None;
            bool hasExplicitAR = false;
            bool hasExplicitOD = false;
            bool hasExplicitCS = false;
            bool hasExplicitHP = false;
            double lastHitObjectTime = 0;

            // 红线 (uninherited timing points)，按文件顺序收集，用于主导 BPM 与滑条时长计算
            var redLines = new List<(double time, double beatLength)>();
            // osu! 默认 1.4，滑行速度 = 100 * SliderMultiplier (px/拍)
            float sliderMultiplier = 1.4f;

            try
            {
                foreach (var rawLine in File.ReadLines(filePath))
                {
                    string line = rawLine.Trim();
                    if (string.IsNullOrEmpty(line) || line.StartsWith("//")) continue;

                    if (line.StartsWith("osu file format v"))
                    {
                        string versionStr = line.Substring(17);
                        int.TryParse(versionStr, out meta.FileFormatVersion);
                        continue;
                    }

                    if (line.StartsWith("["))
                    {
                        string sectionName = line.Trim('[', ']');
                        if (!System.Enum.TryParse(sectionName, true, out section))
                            section = Section.None;
                        if (sectionName == "Colours") section = Section.Colours;
                        continue;
                    }

                    switch (section)
                    {
                        case Section.General:
                            ParseGeneral(line, meta);
                            break;
                        case Section.Metadata:
                            ParseMetadata(line, meta);
                            break;
                        case Section.Difficulty:
                            ParseDifficulty(line, meta, ref hasExplicitAR, ref hasExplicitOD, ref hasExplicitCS, ref hasExplicitHP, ref sliderMultiplier);
                            break;
                        case Section.Events:
                            ParseEvents(line, meta, ref section);
                            break;
                        case Section.TimingPoints:
                            ParseTimingPoints(line, redLines);
                            break;
                        case Section.HitObjects:
                            double hitTime = ParseHitObjectTime(line, sliderMultiplier, redLines);
                            if (hitTime > lastHitObjectTime)
                                lastHitObjectTime = hitTime;
                            break;
                    }
                }

                meta.Length = (float)lastHitObjectTime;
                return FinalizeMetadata(meta, hasExplicitAR, redLines, lastHitObjectTime);
            }
            catch (Exception e)
            {
                // 坏谱不再静默丢失：记录文件名与原因，便于排查库中问题谱面
                Debug.LogWarning($"[SongMetaLoader] 谱面解析失败，已跳过: {Path.GetFileName(filePath)} - {e.Message}");
                return null;
            }
        }

        private static double ParseHitObjectTime(string line, float sliderMultiplier, List<(double time, double beatLength)> redLines)
        {
            string[] parts = line.Split(',');
            if (parts.Length < 3) return 0;

            if (double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double time))
            {
                int rawType = 0;
                if (parts.Length > 3 && int.TryParse(parts[3], out rawType))
                {
                    if ((rawType & 2) != 0 && parts.Length > 7)
                    {
                        double pixelLength = 0;
                        int repeatCount = 1;
                        if (double.TryParse(parts[7], NumberStyles.Float, CultureInfo.InvariantCulture, out pixelLength))
                        {
                            if (parts.Length > 6) int.TryParse(parts[6], out repeatCount);
                            // osu! 官方公式：时长 = 长度 / (100 * SliderMultiplier) 拍 * beatLength * 折返数
                            double beatLength = GetBeatLengthAt(redLines, time);
                            double sliderDuration = pixelLength / (100.0 * sliderMultiplier) * beatLength * repeatCount;
                            return time + sliderDuration;
                        }
                    }
                    else if ((rawType & 8) != 0 && parts.Length > 5)
                    {
                        if (double.TryParse(parts[5], NumberStyles.Float, CultureInfo.InvariantCulture, out double endTime))
                        {
                            return endTime;
                        }
                    }
                }
                return time;
            }
            return 0;
        }

        /// <summary>
        /// 获取指定时间点生效的红线 beatLength（红线按文件顺序即时间序），兜底 120BPM
        /// </summary>
        private static double GetBeatLengthAt(List<(double time, double beatLength)> redLines, double time)
        {
            double beat = 500.0; // 120 BPM
            for (int i = 0; i < redLines.Count; i++)
            {
                if (redLines[i].time <= time) beat = redLines[i].beatLength;
                else break;
            }
            return beat;
        }

        private static void ParseGeneral(string line, BeatmapMetadata meta)
        {
            int colonIndex = line.IndexOf(':');
            if (colonIndex < 0) return;

            string key = line.Substring(0, colonIndex).Trim();
            string value = line.Substring(colonIndex + 1).Trim();

            switch (key)
            {
                case "AudioFilename":
                    meta.AudioFilename = value;
                    break;
                case "PreviewTime":
                    int.TryParse(value, out int preview);
                    meta.PreviewTime = preview;
                    break;
                case "Mode":
                    int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int mode);
                    meta.Mode = mode;
                    break;
            }
        }

        private static void ParseMetadata(string line, BeatmapMetadata meta)
        {
            int colonIndex = line.IndexOf(':');
            if (colonIndex < 0) return;

            string key = line.Substring(0, colonIndex).Trim();
            string value = line.Substring(colonIndex + 1).Trim();

            switch (key)
            {
                case "Title":
                    meta.Title = value;
                    break;
                case "TitleUnicode":
                    meta.TitleUnicode = value;
                    break;
                case "Artist":
                    meta.Artist = value;
                    break;
                case "ArtistUnicode":
                    meta.ArtistUnicode = value;
                    break;
                case "Creator":
                    meta.Creator = value;
                    break;
                case "Version":
                    meta.Version = value;
                    break;
            }
        }

        private static void ParseDifficulty(string line, BeatmapMetadata meta,
            ref bool hasExplicitAR, ref bool hasExplicitOD, ref bool hasExplicitCS, ref bool hasExplicitHP,
            ref float sliderMultiplier)
        {
            int colonIndex = line.IndexOf(':');
            if (colonIndex < 0) return;

            string key = line.Substring(0, colonIndex).Trim();
            string value = line.Substring(colonIndex + 1).Trim();

            switch (key)
            {
                case "HPDrainRate":
                    if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float hp))
                    {
                        meta.HPDrainRate = hp;
                        hasExplicitHP = true;
                    }
                    break;
                case "CircleSize":
                    if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float cs))
                    {
                        meta.CircleSize = cs;
                        hasExplicitCS = true;
                    }
                    break;
                case "OverallDifficulty":
                    if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float od))
                    {
                        meta.OverallDifficulty = od;
                        hasExplicitOD = true;
                    }
                    break;
                case "ApproachRate":
                    if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float ar))
                    {
                        meta.ApproachRate = ar;
                        hasExplicitAR = true;
                    }
                    break;
                case "SliderMultiplier":
                    if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float sm) && sm > 0)
                    {
                        sliderMultiplier = sm;
                    }
                    break;
            }
        }

        private static void ParseEvents(string line, BeatmapMetadata meta, ref Section section)
        {
            string[] parts = line.Split(',');
            if (parts.Length < 3) return;

            if (parts[0].Trim() == "0" && parts[1].Trim() == "0")
            {
                string filename = parts[2].Trim('"');
                meta.BackgroundFilename = filename;
            }
        }

        private static void ParseTimingPoints(string line, List<(double time, double beatLength)> redLines)
        {
            string[] parts = line.Split(',');
            if (parts.Length < 2) return;

            if (double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double time) &&
                double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double beatLength))
            {
                bool uninherited = parts.Length <= 6 || parts[6].Trim() == "1";

                if (uninherited && beatLength > 0)
                {
                    redLines.Add((time, beatLength));
                }
            }
        }

        private static BeatmapMetadata FinalizeMetadata(BeatmapMetadata meta, bool hasExplicitAR,
            List<(double time, double beatLength)> redLines, double lastHitObjectTime)
        {
            // 无显式 AR 时沿用 osu! 规则：AR = OD（与文件格式版本无关，原 if/else 两分支相同）
            if (!hasExplicitAR)
            {
                meta.ApproachRate = meta.OverallDifficulty;
            }

            // 主导 BPM：按每条红线的生效时长加权，取覆盖时间最长者（替代原先简单的最小值）
            if (redLines.Count > 0)
            {
                double bestBeat = redLines[0].beatLength;
                double bestWeight = -1;
                for (int i = 0; i < redLines.Count; i++)
                {
                    double end = (i + 1 < redLines.Count)
                        ? redLines[i + 1].time
                        : Math.Max(lastHitObjectTime, redLines[i].time);
                    double weight = Math.Max(0, end - redLines[i].time);
                    if (weight > bestWeight)
                    {
                        bestWeight = weight;
                        bestBeat = redLines[i].beatLength;
                    }
                }
                meta.BPM = (float)(60000.0 / bestBeat);
            }

            meta.CircleSize = Mathf.Clamp(meta.CircleSize, 0f, 10f);
            meta.ApproachRate = Mathf.Clamp(meta.ApproachRate, 0f, 10f);
            meta.OverallDifficulty = Mathf.Clamp(meta.OverallDifficulty, 0f, 10f);
            meta.HPDrainRate = Mathf.Clamp(meta.HPDrainRate, 0f, 10f);

            if (string.IsNullOrEmpty(meta.AudioFilename)) return null;

            // 过滤非 osu! 模式谱面（taiko/ctb/mania），按难度粒度跳过：
            // 混模式的谱集会只保留 osu! 难度，整个谱面被按 osu! 解析会产生乱谱
            if (meta.Mode != 0)
            {
#if UNITY_EDITOR
                Debug.Log($"[SongMetaLoader] 跳过非 osu! 模式谱面 (Mode={meta.Mode}): {meta.Title} [{meta.Version}]");
#endif
                return null;
            }

            return meta;
        }

        public static Texture2D LoadBackground(string backgroundPath)
        {
            if (string.IsNullOrEmpty(backgroundPath) || !File.Exists(backgroundPath))
                return null;

            try
            {
                byte[] fileData = File.ReadAllBytes(backgroundPath);
                Texture2D texture = new Texture2D(2, 2);
                if (texture.LoadImage(fileData))
                {
                    return texture;
                }
            }
            catch
            {
            }
            return null;
        }
    }
}
