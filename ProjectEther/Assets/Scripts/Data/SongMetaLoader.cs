using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.Scripting;

namespace OsuVR
{
    [Preserve]
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
            List<BeatmapMetadata> maps = new List<BeatmapMetadata>();
            string rootPath = BeatmapImporter.SongsDirectory;

            if (!Directory.Exists(rootPath)) return maps;

            string[] songDirs = Directory.GetDirectories(rootPath);
            foreach (var dir in songDirs)
            {
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

        public static List<BeatmapSet> ScanSongFolderGrouped()
        {
            List<BeatmapSet> sets = new List<BeatmapSet>();
            string rootPath = BeatmapImporter.SongsDirectory;

            if (!Directory.Exists(rootPath)) return sets;

            string[] songDirs = Directory.GetDirectories(rootPath);
            foreach (var dir in songDirs)
            {
                BeatmapSet set = new BeatmapSet
                {
                    FolderPath = dir
                };

                string[] osuFiles = Directory.GetFiles(dir, "*.osu");
                foreach (var osuFile in osuFiles)
                {
                    BeatmapMetadata meta = ParseHeader(osuFile);
                    if (meta != null)
                    {
                        meta.FolderPath = dir;
                        meta.OsuFilePath = osuFile;
                        set.Difficulties.Add(meta);

                        if (string.IsNullOrEmpty(set.Title))
                            set.Title = meta.Title;
                        if (string.IsNullOrEmpty(set.Artist))
                            set.Artist = meta.Artist;
                        if (string.IsNullOrEmpty(set.BackgroundPath) && !string.IsNullOrEmpty(meta.BackgroundFilename))
                            set.BackgroundPath = Path.Combine(dir, meta.BackgroundFilename);
                    }
                }

                if (set.Difficulties.Count > 0)
                {
                    set.Difficulties.Sort((a, b) => a.OverallDifficulty.CompareTo(b.OverallDifficulty));
                    sets.Add(set);
                }
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
                            ParseDifficulty(line, meta, ref hasExplicitAR, ref hasExplicitOD, ref hasExplicitCS, ref hasExplicitHP);
                            break;
                        case Section.Events:
                            ParseEvents(line, meta, ref section);
                            break;
                        case Section.TimingPoints:
                            ParseTimingPoints(line, meta);
                            break;
                        case Section.HitObjects:
                            double hitTime = ParseHitObjectTime(line);
                            if (hitTime > lastHitObjectTime)
                                lastHitObjectTime = hitTime;
                            break;
                    }
                }

                meta.Length = (float)lastHitObjectTime;
                return FinalizeMetadata(meta, hasExplicitAR);
            }
            catch
            {
                return null;
            }
        }

        private static double ParseHitObjectTime(string line)
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
                            double sliderDuration = pixelLength * repeatCount * 2.4;
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
            ref bool hasExplicitAR, ref bool hasExplicitOD, ref bool hasExplicitCS, ref bool hasExplicitHP)
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

        private static void ParseTimingPoints(string line, BeatmapMetadata meta)
        {
            string[] parts = line.Split(',');
            if (parts.Length < 2) return;

            if (double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double time) &&
                double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double beatLength))
            {
                bool uninherited = parts.Length <= 6 || parts[6].Trim() == "1";

                if (uninherited && beatLength > 0)
                {
                    float bpm = 60000f / (float)beatLength;
                    if (meta.BPM < 1f || bpm < meta.BPM)
                    {
                        meta.BPM = bpm;
                    }
                }
            }
        }

        private static BeatmapMetadata FinalizeMetadata(BeatmapMetadata meta, bool hasExplicitAR)
        {
            if (!hasExplicitAR)
            {
                if (meta.FileFormatVersion < 8)
                {
                    meta.ApproachRate = meta.OverallDifficulty;
                }
                else
                {
                    meta.ApproachRate = meta.OverallDifficulty;
                }
            }

            meta.CircleSize = Mathf.Clamp(meta.CircleSize, 0f, 10f);
            meta.ApproachRate = Mathf.Clamp(meta.ApproachRate, 0f, 10f);
            meta.OverallDifficulty = Mathf.Clamp(meta.OverallDifficulty, 0f, 10f);
            meta.HPDrainRate = Mathf.Clamp(meta.HPDrainRate, 0f, 10f);

            if (string.IsNullOrEmpty(meta.AudioFilename)) return null;

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
