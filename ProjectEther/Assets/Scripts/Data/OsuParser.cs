using System;
using System.Collections.Generic;
using System.Globalization; 
using System.IO;
using System.Linq;
using UnityEngine;

namespace OsuVR
{
    /// <summary>
    /// osu!谱面解析器（完整版，支持滑条和转盘）
    /// </summary>
    public static class OsuParser
    {
        // 正则表达式用于分割属性
        private static readonly char[] CommaSeparator = { ',' };
        private static readonly char[] PipeSeparator = { '|' };
        private static readonly char[] ColonSeparator = { ':' };
        private static readonly char[] PipeChar = { '|' };

        // [新增] 内部结构：用于存储时间点和对应的数值（例如 BPM 或速度倍率）
        private struct VolumePoint
        {
            public double Time;
            public int Volume;
        }

        // [新增] 用于标记当前解析段落的枚举
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

        // [新增] 完整解析入口：读取文件并分发到各个解析方法
        public static Beatmap Parse(string path)
        {
            var beatmap = new Beatmap();
            var section = Section.None;

            if (!File.Exists(path))
            {
                Debug.LogError($"文件未找到: {path}");
                return beatmap;
            }

            foreach (var line in File.ReadLines(path))
            {
                string trim = line.Trim();
                if (string.IsNullOrWhiteSpace(trim) || trim.StartsWith("//")) continue;

                // [新增] 解析文件版本号 (通常在第一行: osu file format v14)
                if (trim.StartsWith("osu file format v"))
                {
                    if (int.TryParse(trim.Substring(17), out int ver))
                        beatmap.FormatVersion = ver;
                    continue;
                }

                // 检测段落标记 (例如 [General])
                if (trim.StartsWith("["))
                {
                    string sectionName = trim.Trim('[', ']');
                    // 尝试解析枚举，如果失败则为 None
                    if (!Enum.TryParse(sectionName, true, out section))
                        section = Section.None;

                    // 特殊处理英式拼写 [Colours]
                    if (sectionName == "Colours") section = Section.Colours;

                    continue;
                }

                // 根据当前段落调用对应的解析方法
                try
                {
                    switch (section)
                    {
                        case Section.General:
                            ParseGeneral(trim, beatmap.General);
                            break;
                        case Section.Metadata:
                            ParseMetadata(trim, beatmap.Metadata);
                            break;
                        case Section.Difficulty:
                            ParseDifficulty(trim, beatmap.Difficulty);
                            break;
                        case Section.Events:
                            ParseEvents(trim, beatmap);
                            break;
                        case Section.TimingPoints:
                            ParseTimingPoints(trim, beatmap.ControlPoints);
                            break;
                        case Section.Colours:
                            ParseColors(trim, beatmap.ComboColors);
                            break;
                        case Section.HitObjects:
                            // 复用你原有的 HitObject 解析逻辑！
                            ParseHitObject(trim, beatmap);
                            break;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"解析行失败 [{section}]: {trim}\n错误: {e.Message}");
                }
            }

            // [新增] 后处理：在所有数据解析完毕后，重新校准滑条时间
            // 这样可以防止 TimingPoints 在 HitObjects 后面导致计算错误
            foreach (var hitObject in beatmap.HitObjects)
            {
                if (beatmap.ComboColors.Count == 0)
                {
                    beatmap.ComboColors.Add(new Color(1f, 0.3f, 0.3f)); // 红
                    beatmap.ComboColors.Add(new Color(0.3f, 0.6f, 1f)); // 蓝
                    beatmap.ComboColors.Add(new Color(0.3f, 1f, 0.3f)); // 绿
                    beatmap.ComboColors.Add(new Color(1f, 0.8f, 0.2f)); // 黄
                }

                // 重新计算滑条时间
                if (hitObject is SliderObject slider)
                {
                    var timingPoint = beatmap.GetTimingPointAt(slider.StartTime);
                    var diffPoint = beatmap.GetDifficultyPointAt(slider.StartTime);

                    double beatLength = timingPoint.MsPerBeat;
                    double speedMultiplier = diffPoint.SpeedMultiplier;
                    double globalMultiplier = beatmap.Difficulty.SliderMultiplier;

                    double pxPerBeat = globalMultiplier * 100.0 * speedMultiplier;
                    if (pxPerBeat < 0.001) pxPerBeat = 100.0; // 防呆

                    slider.Duration = (slider.PixelLength * slider.RepeatCount) / pxPerBeat * beatLength;
                    slider.EndTime = slider.StartTime + slider.Duration;
                }
            }
            ApplyControlPointSettings(beatmap);

            CalculateKiaiPeriods(beatmap);

            StackingProcessor.ApplyStacking(beatmap);

            ProcessCombos(beatmap);

            Debug.Log($"谱面解析完成: {beatmap.Metadata.Title} (Ver: {beatmap.Metadata.Version})");
            return beatmap;
        }

        /// <summary>
        /// 后处理：计算每个物件的 ComboIndex 并分配颜色
        /// </summary>
        private static void ProcessCombos(Beatmap beatmap)
        {
            // 1. 确保有颜色定义 (如果没有，使用默认 osu! 颜色)
            if (beatmap.ComboColors == null || beatmap.ComboColors.Count == 0)
            {
                beatmap.ComboColors = new List<Color> {
                    new Color(1f, 192/255f, 0f),       // 黄
                    new Color(0f, 202/255f, 0f),       // 绿
                    new Color(18/255f, 124/255f, 1f),  // 蓝
                    new Color(242/255f, 24/255f, 57/255f) // 红
                };
            }

            int currentComboIndex = 0;
            // 强制让第一个 Note 成为新 Combo，这样索引从 1 开始 (符合 osu 逻辑)
            bool forceNewCombo = true;

            foreach (var obj in beatmap.HitObjects)
            {
                // 如果是 Spinner，通常不影响 Combo 颜色计数，但会重置 Combo 计数器
                if (obj is SpinnerObject)
                {
                    forceNewCombo = true; // Spinner 结束后下一个通常是新 Combo
                    continue;
                }

                // 检查是否是新连击
                if (obj.IsNewCombo || forceNewCombo)
                {
                    currentComboIndex++;

                    // 应用 Combo Offset (跳过颜色)
                    currentComboIndex += obj.ComboOffset;

                    forceNewCombo = false;
                }

                // ✅ 关键：把计算出的索引赋值给对象
                obj.ComboIndex = currentComboIndex;

                // ✅ 关键：根据索引直接分配颜色 (可选，Manager里其实也会算，但这里存一份更稳)
                // 注意：osu 的 ComboIndex 是从 1 开始的，所以要 -1
                int colorIndex = (obj.ComboIndex - 1) % beatmap.ComboColors.Count;
                // 防止负数取模问题
                if (colorIndex < 0) colorIndex += beatmap.ComboColors.Count;

                obj.Color = beatmap.ComboColors[colorIndex];
            }
        }

        /// <summary>
        /// 解析击打对象行（融合版：位运算入口 + 原版滑条逻辑）
        /// </summary>
        public static void ParseHitObject(string line, Beatmap beatmap)
        {
            try
            {
                string[] parts = line.Split(CommaSeparator, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 4) return;

                // 1. 基础属性
                float x = float.Parse(parts[0], CultureInfo.InvariantCulture);
                float y = float.Parse(parts[1], CultureInfo.InvariantCulture);
                Vector2 position = new Vector2(x, y);
                double time = double.Parse(parts[2], CultureInfo.InvariantCulture);

                // 2. [优化] 使用位运算解析类型 (更稳健)
                int rawType = int.Parse(parts[3]);
                int hitSoundInt = int.Parse(parts[4]); // 获取基础 HitSound

                // 3. 连击逻辑
                int comboOffset = (rawType >> 4) & 7;
                bool isNewCombo = (rawType & 4) != 0;

                // 4. 分发 (调用你原来的逻辑)
                if ((rawType & 1) != 0) // Circle
                {
                    CreateHitCircle(parts, time, position, beatmap, isNewCombo, comboOffset, hitSoundInt);
                }
                else if ((rawType & 2) != 0) // Slider
                {
                    CreateSlider(parts, time, position, beatmap, isNewCombo, comboOffset, hitSoundInt);
                }
                else if ((rawType & 8) != 0) // Spinner
                {
                    CreateSpinner(parts, time, beatmap, isNewCombo, hitSoundInt);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"解析 HitObject 失败: {line}\n{e.Message}");
            }
        }

        // =========================================================
        // 🔥 核心修复：完全还原的 CreateSlider 方法
        // =========================================================
        /// <summary>
        /// 创建滑条
        /// </summary>
        private static void CreateSlider(string[] parts, double time, Vector2 startPosition,
               Beatmap beatmap, bool isNewCombo, int comboOffset,int hitSoundInt)
        {
            try
            {
                // 检查是否有足够的滑条参数
                if (parts.Length < 8)
                {
                    Debug.LogError($"滑条格式错误: 参数不足 ({parts.Length}/8)");
                    return;
                }

                // 第5个参数是滑条曲线信息，格式如: "B|150:250|200:300"
                string curveData = parts[5];

                // 使用管道符分割曲线数据
                string[] curveParts = curveData.Split(PipeSeparator, StringSplitOptions.RemoveEmptyEntries);

                if (curveParts.Length < 2)
                {
                    Debug.LogError($"滑条曲线格式错误: {curveData}");
                    return;
                }

                // 第一个部分是曲线类型（单个字符）
                string curveTypeStr = curveParts[0];
                CurveType curveType = ParseCurveType(curveTypeStr);

                // 解析控制点（从第二个部分开始）
                List<Vector2> controlPoints = new List<Vector2>();

                // 第一个控制点是起点 (0, 0) - 相对于滑条起点
                controlPoints.Add(Vector2.zero);

                // 解析后续控制点
                for (int i = 1; i < curveParts.Length; i++)
                {
                    string pointStr = curveParts[i];
                    string[] coords = pointStr.Split(ColonSeparator);

                    if (coords.Length < 2)
                    {
                        Debug.LogWarning($"控制点格式错误: {pointStr}");
                        continue;
                    }

                    // 解析坐标
                    float pointX = float.Parse(coords[0], System.Globalization.CultureInfo.InvariantCulture);
                    float pointY = float.Parse(coords[1], System.Globalization.CultureInfo.InvariantCulture);

                    // 控制点是相对于滑条起点的
                    Vector2 controlPoint = new Vector2(pointX, pointY) - startPosition;
                    controlPoints.Add(controlPoint);
                }

                // 解析重复次数（第6个参数）
                int repeatCount = int.Parse(parts[6]);

                // 解析滑条长度（第7个参数）
                double pixelLength = Math.Max(0.0, double.Parse(parts[7], System.Globalization.CultureInfo.InvariantCulture));

                // 判断是否真正开始新连击
                bool actuallyNewCombo = beatmap.HitObjects.Count == 0 ||
                                       (beatmap.HitObjects.Count > 0 && beatmap.HitObjects[beatmap.HitObjects.Count - 1] is SpinnerObject) ||
                                       isNewCombo;

                // 创建滑条对象
                SliderObject slider = new SliderObject(
                    startTime: time,
                    position: startPosition,
                    curveType: curveType,
                    controlPoints: controlPoints,
                    repeatCount: repeatCount,
                    pixelLength: pixelLength,
                    isNewCombo: actuallyNewCombo,
                    comboOffset: comboOffset
                );

                slider.HitSound = (HitSoundType)hitSoundInt;

                // 更新连击信息
                if (beatmap.HitObjects.Count > 0)
                {
                    slider.UpdateComboInformation(beatmap.HitObjects[beatmap.HitObjects.Count - 1]);
                }

                // 解析滑条节点音效
                if (parts.Length > 8 && !string.IsNullOrEmpty(parts[8]))
                {
                    ParseSliderNodeSamples(slider, parts);
                }

                // --- 🔥 核心修复：计算滑条持续时间 (Timing Calculation) 🔥 ---

                // 1. 获取当前的 BPM 信息 (红线)
                var timingPoint = beatmap.GetTimingPointAt(time);

                // 2. 获取当前的速度倍率 (绿线)
                var diffPoint = beatmap.GetDifficultyPointAt(time);

                // 3. 计算每拍滑行的像素距离 (osu! 标准速度公式)
                // 速度 = 全局倍率 * 100 * 局部倍率
                double pxPerBeat = beatmap.Difficulty.SliderMultiplier * 100.0 * diffPoint.SpeedMultiplier;

                // 防止除以零保护
                if (pxPerBeat < 0.001) pxPerBeat = 0.001;

                // 4. 计算总拍数 = (长度 * 折返次数) / 每拍距离
                double totalBeats = (pixelLength * repeatCount) / pxPerBeat;

                // 5. 持续时间 = 拍数 * 每拍毫秒数
                slider.Duration = totalBeats * timingPoint.MsPerBeat;
                slider.EndTime = time + slider.Duration;

                // --- 修复结束 ---

                // 计算滑条路径点 (裁剪路径)
                CalculateSliderPath(slider);

                //计算滑条打点
                slider.CalculateNestedHitObjects(beatmap.Difficulty.SliderTickRate, timingPoint.MsPerBeat);

                // 将滑条添加到谱面
                beatmap.HitObjects.Add(slider);

                // Debug.Log($"创建滑条: 时间={time}ms, 持续={slider.Duration:F2}ms, 速度倍率={diffPoint.SpeedMultiplier:F2}");
            }
            catch (FormatException e)
            {
                Debug.LogError($"解析滑条时格式错误: {e.Message}");
            }
            catch (Exception e)
            {
                Debug.LogError($"创建滑条时发生错误: {e.Message}");
            }
        }

        /// <summary>
        /// 创建点击圆圈
        /// </summary>
        private static void CreateHitCircle(string[] parts, double time, Vector2 position,
            Beatmap beatmap, bool isNewCombo, int comboOffset, int hitSoundInt)
        {
            // 判断是否真正开始新连击的条件：
            // 1. 这是第一个对象
            // 2. 上一个对象是旋转圆圈
            // 3. 对象本身标记为新连击
            bool actuallyNewCombo = beatmap.HitObjects.Count == 0 ||
                                   (beatmap.HitObjects.Count > 0 && beatmap.HitObjects[beatmap.HitObjects.Count - 1] is SpinnerObject) ||
                                   isNewCombo;

            // 创建点击圆圈对象
            HitCircle circle = new HitCircle(time, position, actuallyNewCombo, comboOffset);
            circle.HitSound = (HitSoundType)hitSoundInt;

            // 如果有上一个对象，更新连击信息
            if (beatmap.HitObjects.Count > 0)
            {
                circle.UpdateComboInformation(beatmap.HitObjects[beatmap.HitObjects.Count - 1]);
            }

            // 将对象添加到谱面
            beatmap.HitObjects.Add(circle);

            // 2. 解析 SampleInfo (parts[5] 是 extras)
            if (parts.Length > 5 && !string.IsNullOrEmpty(parts[5]))
            {
                ParseExtras(circle, parts[5]);
            }
        }

        /// <summary>
        /// 创建转盘
        /// </summary>
        private static void CreateSpinner(string[] parts, double time, Beatmap beatmap, bool isNewCombo, int hitSoundInt)
        {
            try
            {
                // 转盘格式: x,y,time,type,hitSound,endTime
                if (parts.Length < 6)
                {
                    Debug.LogError($"转盘格式错误: 参数不足 ({parts.Length}/6)");
                    return;
                }

                // 解析结束时间
                double endTime = double.Parse(parts[5], System.Globalization.CultureInfo.InvariantCulture);

                // 判断是否真正开始新连击
                bool actuallyNewCombo = beatmap.HitObjects.Count == 0 ||
                                       (beatmap.HitObjects.Count > 0 && beatmap.HitObjects[beatmap.HitObjects.Count - 1] is SpinnerObject) ||
                                       isNewCombo;

                // 创建转盘对象
                SpinnerObject spinner = new SpinnerObject(time, endTime, actuallyNewCombo);


                spinner.HitSound = (HitSoundType)hitSoundInt;

                // 如果有上一个对象，更新连击信息
                if (beatmap.HitObjects.Count > 0)
                {
                    spinner.UpdateComboInformation(beatmap.HitObjects[beatmap.HitObjects.Count - 1]);
                }

                // 解析音效信息（如果有）

                if (parts.Length > 6) ParseExtras(spinner, parts[6]);

                // 将转盘添加到谱面
                beatmap.HitObjects.Add(spinner);

                Debug.Log($"创建转盘: 开始时间={time}ms, 结束时间={endTime}ms, 持续时间={(endTime - time)}ms");
            }
            catch (FormatException e)
            {
                Debug.LogError($"解析转盘时格式错误: {e.Message}");
            }
            catch (Exception e)
            {
                Debug.LogError($"创建转盘时发生错误: {e.Message}");
            }
        }

        private static void CalculateSliderPath(SliderObject slider)
        {
            // 假设你项目里有 SliderPathCalculator
            // 如果没有，请告诉我，我再给你补贝塞尔计算
            List<Vector2> rawPoints = SliderPathCalculator.CalculatePoints(slider.CurveType, slider.ControlPoints);
            slider.PathPoints = TrimPathToLength(rawPoints, slider.PixelLength);
        }

        /// <summary>
        /// 解析 Extras 字符串 (SampleSet:AdditionSet:Index:Volume:Filename)
        /// </summary>
       private static void ParseExtras(HitObject obj, string extras)
        {
            if (string.IsNullOrEmpty(extras)) return;
            string[] p = extras.Split(ColonSeparator);
            
            // 格式: sampleSet:addSet:index:volume:filename
            if (p.Length > 0 && int.TryParse(p[0], out int ss)) obj.SampleSet = (SampleSet)ss;
            if (p.Length > 1 && int.TryParse(p[1], out int ads)) obj.AdditionSet = (SampleSet)ads;
            if (p.Length > 2 && int.TryParse(p[2], out int idx)) obj.CustomIndex = idx;
            if (p.Length > 3 && float.TryParse(p[3], out float vol)) obj.SampleVolume = vol;
            if (p.Length > 4) obj.AudioFilename = p[4];

            // 兜底逻辑
            if (obj.SampleSet == SampleSet.None) obj.SampleSet = SampleSet.Normal;
            if (obj.AdditionSet == SampleSet.None) obj.AdditionSet = SampleSet.Normal;
        }

        // [新增] 解析 [General]
        private static void ParseGeneral(string line, GeneralSection general)
        {
            var pair = line.Split(':');
            if (pair.Length < 2) return;
            var key = pair[0].Trim();
            var value = pair[1].Trim();

            switch (key)
            {
                case "AudioFilename": general.AudioFilename = value; break;
                case "AudioLeadIn": int.TryParse(value, out int leadIn); general.AudioLeadIn = leadIn; break;
                case "PreviewTime": int.TryParse(value, out int preview); general.PreviewTime = preview; break;
                case "Mode": int.TryParse(value, out int mode); general.Mode = mode; break;
                case "StackLeniency": float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float stack); general.StackLeniency = stack; break;
            }
        }

        // [新增] 解析 [Metadata]
        private static void ParseMetadata(string line, MetadataSection metadata)
        {
            var pair = line.Split(':');
            if (pair.Length < 2) return;
            var key = pair[0].Trim();
            var value = pair[1].Trim();

            switch (key)
            {
                case "Title": metadata.Title = value; break;
                case "TitleUnicode": metadata.TitleUnicode = value; break;
                case "Artist": metadata.Artist = value; break;
                case "ArtistUnicode": metadata.ArtistUnicode = value; break;
                case "Creator": metadata.Creator = value; break;
                case "Version": metadata.Version = value; break;
                case "BeatmapID": int.TryParse(value, out int bid); metadata.BeatmapID = bid; break;
            }
        }

        // [新增] 解析 [Difficulty]
        private static void ParseDifficulty(string line, DifficultySection difficulty)
        {
            var pair = line.Split(':');
            if (pair.Length < 2) return;
            var key = pair[0].Trim();
            var value = pair[1].Trim();

            // 使用 CultureInfo.InvariantCulture 确保小数解析正确
            switch (key)
            {
                case "HPDrainRate": float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float hp); difficulty.HPDrainRate = hp; break;
                case "CircleSize": float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float cs); difficulty.CircleSize = cs; break;
                case "OverallDifficulty": float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float od); difficulty.OverallDifficulty = od; break;
                case "ApproachRate": float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float ar); difficulty.ApproachRate = ar; break;
                case "SliderMultiplier": double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double sm); difficulty.SliderMultiplier = sm; break;
                case "SliderTickRate": double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double str); difficulty.SliderTickRate = str; break;
            }
        }

        // [新增] 解析 [Events] (主要是背景图和休息时间)
        private static void ParseEvents(string line, Beatmap beatmap)
        {
            var parts = line.Split(',');
            if (parts.Length < 3) return;

            // 背景图事件: 0,0,"filename",0,0
            if (parts[0] == "0" && parts[1] == "0")
            {
                string filename = parts[2].Trim('"');
                beatmap.Events.BackgroundFilename = filename;
            }
            // 休息时间: 2,Start,End 或 Break,Start,End
            else if (parts[0] == "2" || parts[0] == "Break")
            {
                if (double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double start) &&
                    double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double end))
                {
                    beatmap.Events.Breaks.Add(new BreakPeriod(start, end));
                }
            }
        }

        // [新增] 解析 [TimingPoints] (BPM 和 速度变化)
        private static void ParseTimingPoints(string line, ControlPoints controlPoints)
        {
            var parts = line.Split(',');
            if (parts.Length < 2) return;

            double time = double.Parse(parts[0], CultureInfo.InvariantCulture);
            double beatLength = double.Parse(parts[1], CultureInfo.InvariantCulture);
            int volume = 100;
            if (parts.Length > 5) int.TryParse(parts[5], out volume);

            bool uninherited = parts.Length <= 6 || parts[6] == "1";

            int effects = 0;
            if (parts.Length > 7) int.TryParse(parts[7], out effects);
            bool isKiai = (effects & 1) != 0;

            if (uninherited)
            {
                int timeSignature = parts.Length > 2 ? int.Parse(parts[2]) : 4;
                var tp = new TimingPoint(time, beatLength, timeSignature);
                tp.Volume = volume;
                tp.IsKiai = isKiai;
                controlPoints.Timing.Add(tp);
            }
            else
            {
                double speedMultiplier = beatLength < 0 ? 100.0 / -beatLength : 1.0;
                var dp = new DifficultyPoint(time, speedMultiplier);
                dp.Volume = volume;
                dp.IsKiai = isKiai;
                controlPoints.Difficulty.Add(dp);
            }
        }

        // 新增：应用控制点设置 (音量继承)
        private static void ApplyControlPointSettings(Beatmap beatmap)
        {
            // 1. 将所有控制点（红线和绿线）合并按时间排序，因为它们都可能改变音量
            var allPoints = new List<VolumePoint>();

            // 假设你的 TimingPoint 和 DifficultyPoint 类现在都有 Volume 属性
            foreach (var tp in beatmap.ControlPoints.Timing)
                allPoints.Add(new VolumePoint { Time = tp.Time, Volume = tp.Volume });

            foreach (var dp in beatmap.ControlPoints.Difficulty)
                allPoints.Add(new VolumePoint { Time = dp.Time, Volume = dp.Volume });

            // 按时间排序
            var sortedPoints = allPoints.OrderBy(p => p.Time).ToList();

            // 2. 遍历所有 HitObject
            foreach (var obj in beatmap.HitObjects)
            {
                // 查找当前对象时间点生效的最后一个控制点
                // 默认音量 100
                int currentVolume = 100;

                // 找到时间 <= obj.StartTime 的最后一个点
                int foundIndex = sortedPoints.FindLastIndex(p => p.Time <= obj.StartTime);
                if (foundIndex != -1)
                {
                    currentVolume = sortedPoints[foundIndex].Volume;
                }

                // 存储 TimingPoint 音量，供 AudioManager 计算最终音量
                obj.TimingPointVolume = currentVolume;

                // 3. 如果对象自身音量为 0 (未设置)，设为 100 表示 100% 倍率
                // 最终音量 = TimingPointVolume × (SampleVolume / 100)
                if (obj.SampleVolume <= 0.01f) // 考虑到 float 精度，判断接近0
                {
                    obj.SampleVolume = 100f;
                }

                // 4. 特殊处理：滑条的节点音量 (Slider Nodes)
                if (obj is SliderObject slider)
                {
                    // 滑条的节点通常在 ParseSliderNodeSamples 中解析
                    // 如果那里也没解析出音量（也是0），设为 100 表示 100% 倍率
                    if (slider.NodeSamples != null)
                    {
                        foreach (var nodeSampleList in slider.NodeSamples)
                        {
                            foreach (var sample in nodeSampleList)
                            {
                                var bankSample = sample as BankHitSampleInfo;
                                if (bankSample != null && bankSample.Volume <= 0.01f)
                                {
                                    bankSample.Volume = 100;
                                }
                            }
                        }
                    }
                }
            }
        }

        private static void CalculateKiaiPeriods(Beatmap beatmap)
        {
            var allPoints = new List<(double Time, bool IsKiai)>();

            foreach (var tp in beatmap.ControlPoints.Timing)
                allPoints.Add((tp.Time, tp.IsKiai));

            foreach (var dp in beatmap.ControlPoints.Difficulty)
                allPoints.Add((dp.Time, dp.IsKiai));

            allPoints = allPoints.OrderBy(p => p.Time).ToList();

            double? kiaiStartTime = null;

            for (int i = 0; i < allPoints.Count; i++)
            {
                var current = allPoints[i];
                double nextTime = (i + 1 < allPoints.Count) ? allPoints[i + 1].Time : double.MaxValue;

                if (current.IsKiai && !kiaiStartTime.HasValue)
                {
                    kiaiStartTime = current.Time;
                }
                else if (!current.IsKiai && kiaiStartTime.HasValue)
                {
                    beatmap.ControlPoints.KiaiPeriods.Add(new KiaiPeriod(kiaiStartTime.Value, current.Time));
                    kiaiStartTime = null;
                }
            }

            if (kiaiStartTime.HasValue)
            {
                double lastHitObjectTime = beatmap.HitObjects.Count > 0 
                    ? beatmap.HitObjects.Max(h => h.EndTime) 
                    : double.MaxValue;
                beatmap.ControlPoints.KiaiPeriods.Add(new KiaiPeriod(kiaiStartTime.Value, lastHitObjectTime));
            }

            Debug.Log($"[OsuParser] 检测到 {beatmap.ControlPoints.KiaiPeriods.Count} 个 Kiai 时间段");
        }

        // [新增] 解析 [Colours] (Combo 颜色)
        private static void ParseColors(string line, List<Color> colors)
        {
            var pair = line.Split(':');
            if (pair.Length < 2) return;

            var key = pair[0].Trim();
            if (key.StartsWith("Combo"))
            {
                var rgb = pair[1].Trim().Split(',');
                if (rgb.Length == 3)
                {
                    float r = int.Parse(rgb[0]) / 255f;
                    float g = int.Parse(rgb[1]) / 255f;
                    float b = int.Parse(rgb[2]) / 255f;
                    colors.Add(new Color(r, g, b));
                }
            }
        }
      

        /// <summary>
        /// 辅助函数：将路径裁剪到指定像素长度
        /// </summary>
        private static List<Vector2> TrimPathToLength(List<Vector2> points, double targetLength)
        {
            if (points == null || points.Count < 2) return points;

            List<Vector2> newPoints = new List<Vector2>();
            newPoints.Add(points[0]);

            double currentLength = 0;

            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector2 p1 = points[i];
                Vector2 p2 = points[i + 1];
                double dist = Vector2.Distance(p1, p2);

                // 如果加上这一段还没超长，就直接加
                if (currentLength + dist <= targetLength)
                {
                    newPoints.Add(p2);
                    currentLength += dist;
                }
                else
                {
                    // 如果超长了，就只加一部分，然后直接结束
                    double remaining = targetLength - currentLength;
                    Vector2 direction = (p2 - p1).normalized;
                    newPoints.Add(p1 + direction * (float)remaining);
                    break;
                }
            }
            return newPoints;
        }

        /// <summary>
        /// 解析曲线类型
        /// </summary>
        private static CurveType ParseCurveType(string curveTypeStr)
        {
            if (string.IsNullOrEmpty(curveTypeStr))
                return CurveType.Bezier;

            char curveChar = curveTypeStr[0];

            switch (curveChar)
            {
                case 'L':
                    return CurveType.Linear;
                case 'P':
                    return CurveType.Perfect;
                case 'B':
                    return CurveType.Bezier;
                case 'C':
                    return CurveType.Catmull;
                default:
                    Debug.LogWarning($"未知的曲线类型: {curveChar}，使用贝塞尔曲线");
                    return CurveType.Bezier;
            }
        }

        /// <summary>
        /// 解析滑条节点音效
        /// osu! 格式：
        /// - 第8个参数: 节点音效类型（用|分隔）如 "2|0|2"
        /// - 第9个参数: 节点音效库（用|分隔）如 "0:0:0:0:|2:0:0:0:|0:0:0:0:"
        /// </summary>
        private static void ParseSliderNodeSamples(SliderObject slider, string[] parts)
        {
            try
            {
                // 滑条节点数 = 重复次数 + 1 (Head + Repeats + Tail)
                // 注意：osu! 格式中节点数 = RepeatCount + 1
                int nodeCount = slider.RepeatCount + 1;

                // 初始化节点音效列表
                slider.NodeSamples = new List<List<HitSampleInfo>>();

                // 解析第8个参数: 节点音效类型
                string[] nodeSoundTypes = null;
                if (parts.Length > 8 && !string.IsNullOrEmpty(parts[8]))
                {
                    nodeSoundTypes = parts[8].Split(PipeChar, StringSplitOptions.RemoveEmptyEntries);
                }

                // 解析第9个参数: 节点音效库
                string[] nodeSampleSets = null;
                if (parts.Length > 9 && !string.IsNullOrEmpty(parts[9]))
                {
                    nodeSampleSets = parts[9].Split(PipeChar, StringSplitOptions.RemoveEmptyEntries);
                }

                // 为每个节点创建音效列表
                for (int i = 0; i < nodeCount; i++)
                {
                    // 获取音效类型
                    int soundType = 0;
                    if (nodeSoundTypes != null && i < nodeSoundTypes.Length)
                    {
                        soundType = int.Parse(nodeSoundTypes[i]);
                    }

                    // 获取音效库信息
                    SampleBankInfo bankInfo = new SampleBankInfo();
                    if (nodeSampleSets != null && i < nodeSampleSets.Length)
                    {
                        // 格式: normalBank:addBank:customIndex:volume:filename
                        string[] bankParts = nodeSampleSets[i].Split(':');
                        if (bankParts.Length >= 1 && int.TryParse(bankParts[0], out int normalBank))
                            bankInfo.Normal = ParseSampleBank(normalBank);
                        if (bankParts.Length >= 2 && int.TryParse(bankParts[1], out int addBank))
                            bankInfo.Add = ParseSampleBank(addBank);
                        if (bankParts.Length >= 3 && int.TryParse(bankParts[2], out int customIndex))
                            bankInfo.CustomSampleBank = customIndex;
                        if (bankParts.Length >= 4 && int.TryParse(bankParts[3], out int volume))
                            bankInfo.Volume = volume;
                    }

                    // 根据音效类型和音效库创建音效列表
                    List<HitSampleInfo> nodeSamples = ConvertSoundType(soundType, bankInfo);
                    slider.NodeSamples.Add(nodeSamples);
                }

                Debug.Log($"[OsuParser] 滑条节点音效解析完成: {nodeCount} 个节点");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"解析滑条节点音效时发生错误: {e.Message}");
            }
        }

       

        /// <summary>
        /// 解析音效信息
        /// </summary>
        private static void ParseSampleInfo(HitObject hitObject, string[] sampleParts)
        {
            try
            {
                // 根据osu!文件格式，音效信息包含：
                // 正常音效库:打击音效库:自定义音效库:音量:文件名
                if (sampleParts.Length >= 2)
                {
                    // 解析正常音效库和打击音效库
                    int normalSampleBank = int.Parse(sampleParts[0]);
                    int addSampleBank = int.Parse(sampleParts[1]);

                    // 创建音效信息
                    SampleBankInfo bankInfo = new SampleBankInfo
                    {
                        Normal = ParseSampleBank(normalSampleBank),
                        Add = ParseSampleBank(addSampleBank)
                    };

                    if (sampleParts.Length >= 3)
                    {
                        bankInfo.CustomSampleBank = int.Parse(sampleParts[2]);
                    }

                    if (sampleParts.Length >= 4)
                    {
                        bankInfo.Volume = int.Parse(sampleParts[3]);
                    }

                    if (sampleParts.Length >= 5)
                    {
                        bankInfo.Filename = sampleParts[4];
                    }

                    // 转换音效类型（暂时使用默认音效类型0）
                    List<HitSampleInfo> samples = ConvertSoundType(0, bankInfo);
                    hitObject.Samples.AddRange(samples);
                }
            }
            catch (FormatException)
            {
                Debug.LogWarning("音效信息格式错误");
            }
        }

        /// <summary>
        /// 将音效类型转换为音效信息列表
        /// </summary>
        private static List<HitSampleInfo> ConvertSoundType(int soundType, SampleBankInfo bankInfo)
        {
            List<HitSampleInfo> samples = new List<HitSampleInfo>();

            if (!string.IsNullOrEmpty(bankInfo.Filename))
            {
                // 使用自定义音效文件
                samples.Add(new FileHitSampleInfo
                {
                    Filename = bankInfo.Filename,
                    Volume = bankInfo.Volume
                });
            }
            else
            {
                // 使用默认音效库
                bool isLayered = (soundType != (int)HitSoundType.None) &&
                                ((soundType & (int)HitSoundType.Normal) == 0);

                samples.Add(new BankHitSampleInfo(
                    BankHitSampleInfo.HIT_NORMAL,
                    bankInfo.Normal,
                    bankInfo.CustomSampleBank,
                    bankInfo.Volume,
                    isLayered
                ));

                // 添加其他音效类型
                if ((soundType & (int)HitSoundType.Finish) != 0)
                {
                    samples.Add(new BankHitSampleInfo(
                        BankHitSampleInfo.HIT_FINISH,
                        bankInfo.Add,
                        bankInfo.CustomSampleBank,
                        bankInfo.Volume
                    ));
                }

                if ((soundType & (int)HitSoundType.Whistle) != 0)
                {
                    samples.Add(new BankHitSampleInfo(
                        BankHitSampleInfo.HIT_WHISTLE,
                        bankInfo.Add,
                        bankInfo.CustomSampleBank,
                        bankInfo.Volume
                    ));
                }

                if ((soundType & (int)HitSoundType.Clap) != 0)
                {
                    samples.Add(new BankHitSampleInfo(
                        BankHitSampleInfo.HIT_CLAP,
                        bankInfo.Add,
                        bankInfo.CustomSampleBank,
                        bankInfo.Volume
                    ));
                }
            }

            return samples;
        }

        /// <summary>
        /// 解析音效库枚举
        /// </summary>
        private static SampleBank ParseSampleBank(int sampleBank)
        {
            switch (sampleBank)
            {
                case 0: return SampleBank.None;
                case 1: return SampleBank.Normal;
                case 2: return SampleBank.Soft;
                case 3: return SampleBank.Drum;
                default: return SampleBank.None;
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// 解析滑条的示例（仅编辑器）
        /// </summary>
        public static void TestSliderParsing()
        {
            // 测试滑条解析
            string sliderLine = "100,200,1000,2,0,B|150:250|200:300,1,100.5";

            Beatmap testBeatmap = new Beatmap();
            ParseHitObject(sliderLine, testBeatmap);

            if (testBeatmap.HitObjects.Count > 0 && testBeatmap.HitObjects[0] is SliderObject slider)
            {
                Debug.Log($"滑条解析测试成功:");
                Debug.Log($"  开始时间: {slider.StartTime}ms");
                Debug.Log($"  位置: {slider.Position}");
                Debug.Log($"  曲线类型: {slider.CurveType}");
                Debug.Log($"  控制点数: {slider.ControlPoints.Count}");
                Debug.Log($"  重复次数: {slider.RepeatCount}");
                Debug.Log($"  像素长度: {slider.PixelLength}");
            }
        }
#endif

    }

    /// <summary>
    /// 音效库信息（用于解析）
    /// </summary>
    public class SampleBankInfo
    {
        public string Filename = "";
        public SampleBank Normal = SampleBank.None;
        public SampleBank Add = SampleBank.None;
        public int Volume = 0;
        public int CustomSampleBank = 0;
    }

    /// <summary>
    /// 文件音效信息
    /// </summary>
    public class FileHitSampleInfo : HitSampleInfo
    {
        public string Filename { get; set; }
        public int Volume { get; set; }
    }

    /// <summary>
    /// 简化版使用示例（仅编辑器）
    /// </summary>
#if UNITY_EDITOR
    public class OsuParserExample : MonoBehaviour
    {
        void Start()
        {
            // 示例：解析不同类型的击打对象
            Debug.Log("开始解析示例击打对象...");

            Beatmap beatmap = new Beatmap();

            // 1. 解析点击圆圈
            string circleLine = "256,192,1000,1,0,0:0:0:0:";
            OsuParser.ParseHitObject(circleLine, beatmap);

            // 2. 解析滑条
            string sliderLine = "100,200,2000,2,0,B|150:250|200:300,1,100.5";
            OsuParser.ParseHitObject(sliderLine, beatmap);

            // 3. 解析转盘
            string spinnerLine = "256,192,4000,12,0,6000";
            OsuParser.ParseHitObject(spinnerLine, beatmap);

            // 输出结果
            Debug.Log($"共解析 {beatmap.HitObjects.Count} 个击打对象:");

            foreach (var hitObject in beatmap.HitObjects)
            {
                if (hitObject is HitCircle circle)
                {
                    Debug.Log($"  点击圆圈 - 时间: {circle.StartTime}ms, 位置: {circle.Position}");
                }
                else if (hitObject is SliderObject slider)
                {
                    Debug.Log($"  滑条 - 时间: {slider.StartTime}ms, 位置: {slider.Position}, 类型: {slider.CurveType}, 重复: {slider.RepeatCount}");
                }
                else if (hitObject is SpinnerObject spinner)
                {
                    Debug.Log($"  转盘 - 开始时间: {spinner.StartTime}ms, 结束时间: {spinner.EndTime}ms");
                }
            }
        }

        /// <summary>
        /// 从文件加载并解析谱面
        /// </summary>
        public void LoadBeatmapFromFile(string filePath)
        {
            try
            {
                // 读取文件所有行
                string[] lines = System.IO.File.ReadAllLines(filePath);

                Beatmap beatmap = new Beatmap();
                bool inHitObjectsSection = false;

                // 先解析谱面基本信息
                foreach (string line in lines)
                {
                    string trimmedLine = line.Trim();

                    if (trimmedLine.StartsWith("osu file format v"))
                    {
                        // 解析格式版本
                        string versionStr = trimmedLine.Replace("osu file format v", "");
                        beatmap.FormatVersion = int.Parse(versionStr);
                        Debug.Log($"谱面格式版本: {beatmap.FormatVersion}");
                    }
                    else if (trimmedLine == "[HitObjects]")
                    {
                        inHitObjectsSection = true;
                        continue;
                    }
                    else if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                    {
                        // 进入其他部分，结束击打对象解析
                        inHitObjectsSection = false;
                        continue;
                    }

                    // 解析击打对象
                    if (inHitObjectsSection && !string.IsNullOrEmpty(trimmedLine))
                    {
                        OsuParser.ParseHitObject(trimmedLine, beatmap);
                    }
                }

                Debug.Log($"谱面加载完成，共 {beatmap.HitObjects.Count} 个击打对象");

                // 统计不同类型击打对象的数量
                int circleCount = 0;
                int sliderCount = 0;
                int spinnerCount = 0;

                foreach (var hitObject in beatmap.HitObjects)
                {
                    if (hitObject is HitCircle) circleCount++;
                    else if (hitObject is SliderObject) sliderCount++;
                    else if (hitObject is SpinnerObject) spinnerCount++;
                }

                Debug.Log($"  点击圆圈: {circleCount}");
                Debug.Log($"  滑条: {sliderCount}");
                Debug.Log($"  转盘: {spinnerCount}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"加载谱面失败: {e.Message}");
            }
        }
    }
#endif
}