using System.Collections.Generic;
using UnityEngine;

namespace OsuVR
{
    /// <summary>
    /// 完整的谱面数据结构
    /// </summary>
    public class Beatmap
    {
        public int FormatVersion { get; set; }
        public GeneralSection General { get; set; } = new GeneralSection();
        public MetadataSection Metadata { get; set; } = new MetadataSection();
        public DifficultySection Difficulty { get; set; } = new DifficultySection();
        public EventsSection Events { get; set; } = new EventsSection();
        public ControlPoints ControlPoints { get; set; } = new ControlPoints();
        public List<Color> ComboColors { get; set; } = new List<Color>();
        public List<HitObject> HitObjects { get; set; } = new List<HitObject>();

        // 默认 Combo 颜色 (osu! 默认值)
        public Beatmap()
        {
            ComboColors.Add(new Color(1f, 0.75f, 0.80f)); // Pink
            ComboColors.Add(new Color(0.61f, 0.83f, 0.30f)); // Green
            ComboColors.Add(new Color(0.34f, 0.80f, 0.93f)); // Blue
            ComboColors.Add(new Color(0.97f, 0.86f, 0.38f)); // Yellow
        }
        /// <summary>
        /// 获取指定时间的红线 (BPM)
        /// </summary>
        public TimingPoint GetTimingPointAt(double time)
        {
            if (ControlPoints.Timing.Count == 0)
                return new TimingPoint(0, 500, 4); // 默认 120 BPM

            // 找到最后一个 时间 <= time 的红线
            // 列表通常是排序的，为了性能最好用二分查找，这里为了简单先用 FindLast
            var point = ControlPoints.Timing.FindLast(x => x.Time <= time);

            // 如果比第一根红线还早，就用第一根
            return point ?? ControlPoints.Timing[0];
        }

        /// <summary>
        /// 获取指定时间的绿线 (速度倍率)
        /// </summary>
        public DifficultyPoint GetDifficultyPointAt(double time)
        {
            // 如果没有绿线，默认倍率为 1.0
            if (ControlPoints.Difficulty.Count == 0)
                return new DifficultyPoint(0, 1.0);

            var point = ControlPoints.Difficulty.FindLast(x => x.Time <= time);

            // 注意：绿线的作用域通常是从它开始，如果没有找到，默认倍率是 1.0
            return point ?? new DifficultyPoint(0, 1.0);
        }
    }

    public class GeneralSection
    {
        public string AudioFilename { get; set; }
        public int AudioLeadIn { get; set; }
        public int PreviewTime { get; set; }
        public int Countdown { get; set; }
        public string SampleSet { get; set; }
        public float StackLeniency { get; set; } = 0.7f;
        public int Mode { get; set; }
        public bool LetterboxInBreaks { get; set; }
    }

    public class MetadataSection
    {
        public string Title { get; set; }
        public string TitleUnicode { get; set; }
        public string Artist { get; set; }
        public string ArtistUnicode { get; set; }
        public string Creator { get; set; }
        public string Version { get; set; }
        public string Source { get; set; }
        public string Tags { get; set; }
        public int BeatmapID { get; set; }
        public int BeatmapSetID { get; set; }
    }

    public class DifficultySection
    {
        public float HPDrainRate { get; set; } = 5;
        public float CircleSize { get; set; } = 5;
        public float OverallDifficulty { get; set; } = 5;
        public float ApproachRate { get; set; } = 5;
        public double SliderMultiplier { get; set; } = 1.4;
        public double SliderTickRate { get; set; } = 1;
    }

    public class EventsSection
    {
        public string BackgroundFilename { get; set; }
        public List<BreakPeriod> Breaks { get; set; } = new List<BreakPeriod>();
    }

    public class BreakPeriod
    {
        public double StartTime;
        public double EndTime;
        public BreakPeriod(double start, double end) { StartTime = start; EndTime = end; }
    }

    public class ControlPoints
    {
        public List<TimingPoint> Timing { get; set; } = new List<TimingPoint>();
        public List<DifficultyPoint> Difficulty { get; set; } = new List<DifficultyPoint>();
    }

    public class TimingPoint
    {
        public double Time;
        public double MsPerBeat; // 60000 / BPM
        public int TimeSignature;
        public TimingPoint(double time, double msPerBeat, int timeSignature)
        {
            Time = time; MsPerBeat = msPerBeat; TimeSignature = timeSignature;
        }
    }

    public class DifficultyPoint
    {
        public double Time;
        public double SpeedMultiplier; // 1.0 = normal, 0.5 = half speed (inherited timing point)
        public DifficultyPoint(double time, double speedMultiplier)
        {
            Time = time; SpeedMultiplier = speedMultiplier;
        }
    }
}