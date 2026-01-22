using System.IO;
using System.Globalization; 
using System.Collections.Generic;
using UnityEngine;
using System;

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

                ApplyStacking(beatmap);

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
            Debug.Log($"谱面解析完成: {beatmap.Metadata.Title} (Ver: {beatmap.Metadata.Version})");
            return beatmap;
        }

        /// <summary>
        /// 解析击打对象行（修正版：移除外部枚举依赖，使用纯位运算）
        /// </summary>
        public static void ParseHitObject(string line, Beatmap beatmap)
        {
            try
            {
                // 使用逗号分割行，获取各个属性
                string[] parts = line.Split(CommaSeparator, StringSplitOptions.RemoveEmptyEntries);

                // 检查是否有足够的属性
                if (parts.Length < 4)
                {
                    Debug.LogWarning($"击打对象格式错误: {line}");
                    return;
                }

                // 解析X坐标
                float x = float.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture);
                // 解析Y坐标
                float y = float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
                Vector2 position = new Vector2(x, y);

                // 解析时间
                double time = double.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture);

                // --- 核心修复开始 ---

                // 解析原始类型值 (Bitmask)
                int rawType = int.Parse(parts[3]);

                // 1. 处理连击偏移量 (Combo Offset)
                // 逻辑：获取第 4, 5, 6 位 (掩码 112, 即 0x70)，然后右移 4 位
                int comboOffset = (rawType >> 4) & 7;

                // 2. 检查是否是新连击 (New Combo)
                // 逻辑：检查第 2 位 (值 4) 是否为 1
                bool isNewCombo = (rawType & 4) != 0;

                // 3. 获取实际对象类型
                // 逻辑：虽然可以剥离 Flag，但在 osu! 中，直接检查 Bit 0, Bit 1, Bit 3 更标准
                // 1 = Circle, 2 = Slider, 8 = Spinner

                if ((rawType & 1) != 0) // Type 1: Circle
                {
                    CreateHitCircle(parts, time, position, beatmap, isNewCombo, comboOffset);
                }
                else if ((rawType & 2) != 0) // Type 2: Slider
                {
                    CreateSlider(parts, time, position, beatmap, isNewCombo, comboOffset);
                }
                else if ((rawType & 8) != 0) // Type 8: Spinner
                {
                    CreateSpinner(parts, time, beatmap, isNewCombo);
                }
                else
                {
                    // 忽略未知类型或 Mania Hold (128)
                    // Debug.LogWarning($"忽略未知类型: {rawType} at {time}");
                }

                // --- 核心修复结束 ---
            }
            catch (FormatException e)
            {
                Debug.LogError($"解析击打对象时格式错误: {e.Message} \nLine: {line}");
            }
            catch (Exception e)
            {
                Debug.LogError($"解析击打对象时发生错误: {e.Message} \nLine: {line}");
            }
        }

        /// <summary>
        /// 创建点击圆圈
        /// </summary>
        private static void CreateHitCircle(string[] parts, double time, Vector2 position,
            Beatmap beatmap, bool isNewCombo, int comboOffset)
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

            // 如果有上一个对象，更新连击信息
            if (beatmap.HitObjects.Count > 0)
            {
                circle.UpdateComboInformation(beatmap.HitObjects[beatmap.HitObjects.Count - 1]);
            }

            // 将对象添加到谱面
            beatmap.HitObjects.Add(circle);

            // 解析音效信息（如果有）
            if (parts.Length > 5 && !string.IsNullOrEmpty(parts[5]))
            {
                // 使用冒号分割音效信息
                string[] sampleParts = parts[5].Split(ColonSeparator);
                ParseSampleInfo(circle, sampleParts);
            }
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
                case "Artist": metadata.Artist = value; break;
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
            // 第7个参数决定是否继承 (1=Red Line/BPM变化, 0=Green Line/速度倍率变化)
            bool uninherited = parts.Length <= 6 || parts[6] == "1";

            if (uninherited)
            {
                // 红线 (BPM Change)
                int timeSignature = parts.Length > 2 ? int.Parse(parts[2]) : 4;
                controlPoints.Timing.Add(new TimingPoint(time, beatLength, timeSignature));
            }
            else
            {
                // 绿线 (Velocity Change)
                // beatLength 为负数百分比，例如 -100 = 1.0x, -50 = 2.0x, -200 = 0.5x
                double speedMultiplier = beatLength < 0 ? 100.0 / -beatLength : 1.0;
                controlPoints.Difficulty.Add(new DifficultyPoint(time, speedMultiplier));
            }
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
        /// 创建滑条
        /// </summary>
        private static void CreateSlider(string[] parts, double time, Vector2 startPosition,
               Beatmap beatmap, bool isNewCombo, int comboOffset)
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
        /// 计算并裁剪滑条路径 (修复版)
        /// </summary>
        private static void CalculateSliderPath(SliderObject slider)
        {
            try
            {
                // 1. 计算原始路径 (由数学库生成)
                List<Vector2> rawPoints = SliderPathCalculator.CalculatePoints(
                    slider.CurveType,
                    slider.ControlPoints
                );

                // 2. 🔥 关键修复：根据 PixelLength 裁剪路径
                // osu!的滑条控制点生成的曲线通常比实际定义的 PixelLength 长
                // 必须截取，否则滑条视觉会超长，时间也会对不上
                slider.PathPoints = TrimPathToLength(rawPoints, slider.PixelLength);

                // 3. 🔥 关键修复：赋值给对象 (之前被注释掉了)
                // slider.PathPoints = pathPoints; // <-- AI 原来的代码注释掉了这行

                Debug.Log($"计算滑条路径: 类型={slider.CurveType}, 最终点数={slider.PathPoints.Count}");
            }
            catch (Exception e)
            {
                Debug.LogError($"计算滑条路径时发生错误: {e.Message}");
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
        /// </summary>
        private static void ParseSliderNodeSamples(SliderObject slider, string[] parts)
        {
            try
            {
                // 第8个参数: 节点音效类型（用|分隔）
                // 格式: "2|0|2" 表示三个节点的音效类型
                if (parts.Length > 8 && !string.IsNullOrEmpty(parts[8]))
                {
                    string[] nodeSoundTypes = parts[8].Split(PipeChar, StringSplitOptions.RemoveEmptyEntries);

                    // 滑条节点数 = 重复次数 + 2 (起点和终点)
                    int nodeCount = slider.RepeatCount + 2;

                    // 初始化节点音效列表
                    slider.NodeSamples = new List<List<HitSampleInfo>>();

                    for (int i = 0; i < nodeCount; i++)
                    {
                        int soundType = 0;
                        if (i < nodeSoundTypes.Length)
                        {
                            soundType = int.Parse(nodeSoundTypes[i]);
                        }

                        // 根据音效类型创建音效列表
                        List<HitSampleInfo> nodeSamples = ConvertSoundType(soundType, new SampleBankInfo());
                        slider.NodeSamples.Add(nodeSamples);
                    }
                }

                // 第9个参数: 节点音效库（用|分隔）
                // 格式: "0:0:0:0:|2:0:0:0:|0:0:0:0:" 表示三个节点的音效库
                if (parts.Length > 9 && !string.IsNullOrEmpty(parts[9]))
                {
                    string[] nodeSampleSets = parts[9].Split(PipeChar, StringSplitOptions.RemoveEmptyEntries);

                    // 确保NodeSamples已初始化
                    if (slider.NodeSamples == null)
                    {
                        slider.NodeSamples = new List<List<HitSampleInfo>>();
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"解析滑条节点音效时发生错误: {e.Message}");
            }
        }

        /// <summary>
        /// 创建转盘
        /// </summary>
        private static void CreateSpinner(string[] parts, double time, Beatmap beatmap, bool isNewCombo)
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

                // 如果有上一个对象，更新连击信息
                if (beatmap.HitObjects.Count > 0)
                {
                    spinner.UpdateComboInformation(beatmap.HitObjects[beatmap.HitObjects.Count - 1]);
                }

                // 解析音效信息（如果有）
                if (parts.Length > 6 && !string.IsNullOrEmpty(parts[6]))
                {
                    string[] sampleParts = parts[6].Split(ColonSeparator);
                    ParseSampleInfo(spinner, sampleParts);
                }

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

        /// <summary>
        /// 解析滑条的示例
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

        // 在 OsuParser 类中添加 ApplyStacking 方法（原本定义在 OsuParserExample 内部，需移到 OsuParser 并设为 public）
        private static void ApplyStacking(Beatmap beatmap)
        {
            float stackThreshold = 3.0f; // 坐标判定阈值
                                         // 遍历物件，如果坐标极其接近，给 StackOrder 计数
            for (int i = 0; i < beatmap.HitObjects.Count; i++)
            {
                var current = beatmap.HitObjects[i];
                if (i == 0) continue;

                var prev = beatmap.HitObjects[i - 1];

                // 如果位置几乎重叠，且时间间隔小于预取时间 (TimePreempt) 的一部分
                if (Vector2.Distance(current.Position, prev.Position) < stackThreshold)
                {
                    // 给物件打上堆叠标签，Controller 绘图时会用到
                    current.StackOrder = prev.StackOrder + 1;
                }
            }
        }
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
    /// 简化版使用示例
    /// </summary>
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
}