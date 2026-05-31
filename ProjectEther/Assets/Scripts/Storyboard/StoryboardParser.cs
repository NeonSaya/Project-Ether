using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using OsuVR.Storyboard.Data;

namespace OsuVR.Storyboard
{
    /// <summary>
    /// Storyboard 纯 C# 解析引擎：
    /// 将原始命令字符串列表转换为内存对象树 (SBStoryboard)
    /// 支持 Sprite, Animation, Loop, Trigger 及所有标准命令类型
    /// </summary>
    public static class StoryboardParser
    {
        /// <summary>
        /// 解析 StoryboardLines 列表，返回完整的内存对象树
        /// 自动提取并应用 [Variables] 段中的变量替换
        /// </summary>
        public static SBStoryboard Parse(List<string> lines)
        {
            var processedLines = PreprocessLines(lines, null);
            return ParseInternal(processedLines);
        }

        /// <summary>
        /// 解析 StoryboardLines 列表，使用外部传入的变量进行替换
        /// 用于 .osu 内联 SB 命令 (外部变量来自 .osu 的 [Variables] 段)
        /// </summary>
        public static SBStoryboard Parse(List<string> lines, Dictionary<string, string> externalVariables)
        {
            var processedLines = PreprocessLines(lines, externalVariables);
            return ParseInternal(processedLines);
        }

        /// <summary>
        /// 预处理: 提取 [Variables]、过滤注释和段头、执行变量替换
        /// </summary>
        static List<string> PreprocessLines(List<string> lines, Dictionary<string, string> externalVariables)
        {
            // 合并外部变量与内部变量
            var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (externalVariables != null)
            {
                foreach (var kv in externalVariables)
                    variables[kv.Key] = kv.Value;
            }

            // 第一遍: 提取 [Variables] 段中的变量定义
            bool inVariables = false;
            var filtered = new List<string>(lines.Count);
            foreach (var rawLine in lines)
            {
                if (string.IsNullOrWhiteSpace(rawLine)) continue;
                string trimmed = rawLine.Trim();

                // 跳过注释
                if (trimmed.StartsWith("//")) continue;

                // 检测段头
                if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                {
                    string sectionName = trimmed.Substring(1, trimmed.Length - 2);
                    if (string.Equals(sectionName, "Variables", StringComparison.OrdinalIgnoreCase))
                    {
                        inVariables = true;
                        continue;
                    }
                    inVariables = false;
                    filtered.Add(rawLine);
                    continue;
                }

                if (inVariables)
                {
                    // 解析 $key=value
                    if (trimmed.StartsWith("$"))
                    {
                        int eqIndex = trimmed.IndexOf('=');
                        if (eqIndex > 0)
                        {
                            string key = trimmed.Substring(0, eqIndex).Trim();
                            string value = trimmed.Substring(eqIndex + 1).Trim();
                            variables[key] = value;
                        }
                    }
                    continue;
                }

                filtered.Add(rawLine);
            }

            // 第二遍: 对所有行执行变量替换
            if (variables.Count == 0) return filtered;

            var result = new List<string>(filtered.Count);
            foreach (var line in filtered)
                result.Add(SubstituteVariables(line, variables));

            return result;
        }

        /// <summary>
        /// 递归替换行中的 $variable 引用 (防循环引用)
        /// </summary>
        static string SubstituteVariables(string line, Dictionary<string, string> variables)
        {
            if (string.IsNullOrEmpty(line) || !line.Contains("$")) return line;

            var replaced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string result = line;
            int safetyLimit = 100;

            while (safetyLimit-- > 0 && result.Contains("$"))
            {
                bool anyReplaced = false;
                foreach (var kv in variables)
                {
                    if (replaced.Contains(kv.Key)) continue;
                    if (result.Contains(kv.Key))
                    {
                        result = result.Replace(kv.Key, kv.Value);
                        replaced.Add(kv.Key);
                        anyReplaced = true;
                    }
                }
                if (!anyReplaced) break;
            }
            return result;
        }

        /// <summary>
        /// 内部解析核心 (变量替换已完成)
        /// </summary>
        static SBStoryboard ParseInternal(List<string> lines)
        {
            var storyboard = new SBStoryboard();
            SBElement currentElement = null;
            SBLoop currentLoop = null;

            foreach (var rawLine in lines)
            {
                if (string.IsNullOrWhiteSpace(rawLine)) continue;

                try
                {
                    // 判断是主对象行还是命令行
                    // 主对象行: 不以空格开头 (Sprite, Animation)
                    // 命令行: 以空格或下划线开头, 或者是 L,/T, 开头的循环/触发器
                    string line = rawLine.Trim();
                    bool isCommand = rawLine.StartsWith(" ") || rawLine.StartsWith("_")
                        || line.StartsWith("L,") || line.StartsWith("l,")
                        || line.StartsWith("T,") || line.StartsWith("t,");

                    if (!isCommand)
                    {
                        // 主对象行
                        currentLoop = null;
                        currentElement = ParseElement(line);
                        if (currentElement != null)
                            storyboard.AddElement(currentElement);
                    }
                    else if (currentElement != null)
                    {
                        // 命令行
                        if (line.StartsWith("_L") || line.StartsWith("_l") || line.StartsWith("L,") || line.StartsWith("l,"))
                        {
                            // Loop 命令: _L,startTime,loopCount 或 L,startTime,loopCount
                            // 去掉前缀 ("_L," 或 "L,") 后传给 ParseLoop
                            string loopData = line;
                            if (loopData.StartsWith("_L,") || loopData.StartsWith("_l,"))
                                loopData = loopData.Substring(3);
                            else if (loopData.StartsWith("L,") || loopData.StartsWith("l,"))
                                loopData = loopData.Substring(2);
                            currentLoop = ParseLoop(loopData, currentElement);
                            if (currentLoop != null)
                                currentElement.Loops.Add(currentLoop);
                        }
                        else if (line.StartsWith("_T") || line.StartsWith("_t") || line.StartsWith("T,") || line.StartsWith("t,"))
                        {
                            // Trigger 命令: _T,triggerName,startTime,endTime 或 T,triggerName,startTime,endTime
                            currentLoop = null;
                            string triggerData = line;
                            if (triggerData.StartsWith("_T,") || triggerData.StartsWith("_t,"))
                                triggerData = triggerData.Substring(3);
                            else if (triggerData.StartsWith("T,") || triggerData.StartsWith("t,"))
                                triggerData = triggerData.Substring(2);
                            ParseTrigger(triggerData, currentElement);
                        }
                        else
                        {
                            // 普通命令
                            var target = currentLoop != null ? (object)currentLoop : currentElement;
                            ParseCommand(line, target);
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[SBParser] 解析行失败: {rawLine}\n{e.Message}");
                }
            }

            SBDebugLog.Mem($"ParseInternal 完成: {storyboard.TotalElementCount} 元素, {lines.Count} 行");
            return storyboard;
        }

        /// <summary>
        /// 从 .osb 文件解析 Storyboard
        /// </summary>
        public static SBStoryboard ParseFile(string osbPath)
        {
            if (string.IsNullOrEmpty(osbPath) || !File.Exists(osbPath))
            {
                Debug.LogWarning($"[SBParser] .osb 文件不存在: {osbPath}");
                return new SBStoryboard();
            }

            try
            {
                var lines = new List<string>(File.ReadAllLines(osbPath));
                Debug.Log($"[SBParser] 读取 .osb 文件: {Path.GetFileName(osbPath)}, {lines.Count} 行");
                return Parse(lines);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SBParser] 读取 .osb 文件失败: {e.Message}");
                return new SBStoryboard();
            }
        }

        // =====================================================
        //  元素解析
        // =====================================================

        static SBElement ParseElement(string line)
        {
            var parts = SplitCsv(line);
            if (parts.Count < 6) return null;

            string type = parts[0].Trim();

            // 远古谱面可能缺少 Layer/Origin 声明，使用 osu! 官方默认值
            SBLayer layer = SBLayer.Background;
            SBOrigin origin = SBOrigin.Centre;
            if (parts.Count > 1 && !string.IsNullOrWhiteSpace(parts[1]))
                Enum.TryParse(parts[1].Trim(), true, out layer);
            if (parts.Count > 2 && !string.IsNullOrWhiteSpace(parts[2]))
                Enum.TryParse(parts[2].Trim(), true, out origin);

            string imagePath = parts[3].Trim().Trim('"');
            float x = ParseFloat(parts[4]);
            float y = ParseFloat(parts[5]);

            var pos = new Vector2(x, y);

            if (type.Equals("Sprite", StringComparison.OrdinalIgnoreCase))
            {
                return new SBStoryboardSprite(layer, origin, imagePath, pos);
            }
            else if (type.Equals("Animation", StringComparison.OrdinalIgnoreCase))
            {
                // Animation,layer,origin,"imagePath",x,y,frameCount,frameDelay,loopType
                int frameCount = parts.Count > 6 ? ParseInt(parts[6]) : 1;
                double frameDelay = parts.Count > 7 ? ParseDouble(parts[7]) : 0;
                var loopType = parts.Count > 8 && ParseInt(parts[8]) == 1
                    ? SBAnimationLoopType.LoopOnce
                    : SBAnimationLoopType.LoopForever;
                return new SBStoryboardAnimation(layer, origin, imagePath, pos, frameCount, frameDelay, loopType);
            }
            else if (type.Equals("Sample", StringComparison.OrdinalIgnoreCase))
            {
                // Sample,time,layer,"filepath",volume
                double time = parts.Count > 1 ? ParseDouble(parts[1]) : 0;
                SBLayer sampleLayer = SBLayer.Background;
                if (parts.Count > 2) Enum.TryParse(parts[2].Trim(), true, out sampleLayer);
                string samplePath = parts.Count > 3 ? parts[3].Trim().Trim('"') : "";
                int volume = parts.Count > 4 ? ParseInt(parts[4]) : 100;
                // Sample 作为 Sprite 处理，使用单帧
                var elem = new SBStoryboardSprite(sampleLayer, SBOrigin.Centre, samplePath, Vector2.zero);
                // 在 time 时刻 FadeIn → FadeOut
                elem.FadeCommands.Add(new SBFadeCommand(SBEasing.Linear, time, time, 1f, 1f));
                return elem;
            }

            return null;
        }

        // =====================================================
        //  命令解析
        // =====================================================

        static void ParseCommand(string line, object target)
        {
            var parts = SplitCsv(line);
            if (parts.Count < 4) return;

            // osu! 格式: Type,Easing,StartTime,EndTime,Values...
            //             [0]    [1]      [2]       [3]     [4+]
            string typeStr = parts[0].Trim();

            // Easing (parts[1])
            SBEasing easing = SBEasing.Linear;
            if (parts.Count > 1)
            {
                // Easing 可以是数字 (0-34) 或名称
                if (int.TryParse(parts[1].Trim(), out int easingInt))
                {
                    if (easingInt >= 0 && easingInt <= 34)
                        easing = (SBEasing)easingInt;
                }
                else
                {
                    Enum.TryParse(parts[1].Trim(), true, out easing);
                }
            }

            // StartTime (parts[2]), EndTime (parts[3])
            double startTime = parts.Count > 2 ? ParseDouble(parts[2]) : 0;
            double endTime = parts.Count > 3 ? ParseDouble(parts[3]) : startTime;

            // 确保 endTime >= startTime
            if (endTime < startTime) endTime = startTime;

            switch (typeStr)
            {
                case "F":
                    // F,easing,startTime,endTime,startOpacity[,endOpacity]
                    // F,easing,startTime,,opacity (空 endTime = 立即设置)
                    {
                        // 检测空 endTime (如 "F,0,15501,,0.6")
                        bool emptyEnd = parts.Count > 3 && string.IsNullOrWhiteSpace(parts[3]);
                        if (emptyEnd) endTime = startTime;

                        float fadeStart = parts.Count > 4 ? ParseFloat(parts[4]) : 1f;
                        float fadeEnd = parts.Count > 5 ? ParseFloat(parts[5]) : fadeStart;
                        AddFadeCommand(target, easing, startTime, endTime, fadeStart, fadeEnd);
                    }
                    break;

                case "M":
                    // M,easing,startTime,endTime,startX,startY[,endX,endY]
                    if (parts.Count < 6) break; // 至少需要 startX,startY
                    AddMoveCommand(target, easing, startTime, endTime,
                        new Vector2(ParseFloat(parts[4]), ParseFloat(parts[5])),
                        parts.Count > 7
                            ? new Vector2(ParseFloat(parts[6]), ParseFloat(parts[7]))
                            : new Vector2(ParseFloat(parts[4]), ParseFloat(parts[5])));
                    break;

                case "MX":
                    // MX,easing,startTime,endTime,startX[,endX]
                    AddMoveXCommand(target, easing, startTime, endTime,
                        parts.Count > 4 ? ParseFloat(parts[4]) : 0f,
                        parts.Count > 5 ? ParseFloat(parts[5]) : (parts.Count > 4 ? ParseFloat(parts[4]) : 0f));
                    break;

                case "MY":
                    // MY,easing,startTime,endTime,startY[,endY]
                    AddMoveYCommand(target, easing, startTime, endTime,
                        parts.Count > 4 ? ParseFloat(parts[4]) : 0f,
                        parts.Count > 5 ? ParseFloat(parts[5]) : (parts.Count > 4 ? ParseFloat(parts[4]) : 0f));
                    break;

                case "S":
                    // S,easing,startTime,endTime,startScale[,endScale]
                    AddScaleCommand(target, easing, startTime, endTime,
                        parts.Count > 4 ? ParseFloat(parts[4]) : 1f,
                        parts.Count > 5 ? ParseFloat(parts[5]) : (parts.Count > 4 ? ParseFloat(parts[4]) : 1f));
                    break;

                case "V":
                    // V,easing,startTime,endTime,startScaleX,startScaleY,endScaleX,endScaleY
                    if (parts.Count < 6) break;
                    AddScaleVectorCommand(target, easing, startTime, endTime,
                        ParseFloat(parts[4]), ParseFloat(parts[5]),
                        parts.Count > 7 ? ParseFloat(parts[6]) : ParseFloat(parts[4]),
                        parts.Count > 7 ? ParseFloat(parts[7]) : ParseFloat(parts[5]));
                    break;

                case "R":
                    // R,easing,startTime,endTime,startRotation[,endRotation]
                    AddRotateCommand(target, easing, startTime, endTime,
                        parts.Count > 4 ? ParseFloat(parts[4]) : 0f,
                        parts.Count > 5 ? ParseFloat(parts[5]) : (parts.Count > 4 ? ParseFloat(parts[4]) : 0f));
                    break;

                case "C":
                    // C,easing,startTime,endTime,r,g,b[,r2,g2,b2]
                    if (parts.Count < 7) break; // 至少需要 r,g,b
                    AddColorCommand(target, easing, startTime, endTime,
                        new Color32(
                            (byte)ParseInt(parts[4]),
                            (byte)ParseInt(parts[5]),
                            (byte)ParseInt(parts[6]), 255),
                        parts.Count > 9
                            ? new Color32(
                                (byte)ParseInt(parts[7]),
                                (byte)ParseInt(parts[8]),
                                (byte)ParseInt(parts[9]), 255)
                            : new Color32(
                                (byte)ParseInt(parts[4]),
                                (byte)ParseInt(parts[5]),
                                (byte)ParseInt(parts[6]), 255));
                    break;

                case "P":
                    // P,easing,startTime,endTime,parameter
                    string param = parts.Count > 4 ? parts[4].Trim() : "";
                    AddParameterCommand(target, easing, startTime, endTime, param);
                    break;
            }
        }

        static SBLoop ParseLoop(string data, SBElement element)
        {
            // data 已去掉前缀: "startTime,loopCount"
            var parts = SplitCsv(data);
            if (parts.Count < 2) return null;

            double startTime = ParseDouble(parts[0]);
            int repeatCount = ParseInt(parts[1]);
            // osu! 格式: L,startTime,repeatCount
            // repeatCount = 重复次数 (不含首次播放)
            // osu!lazer: AddLoopingGroup(startTime, repeatCount - 1), TotalIterations = (repeatCount-1) + 1 = repeatCount
            // 所以总迭代次数 = repeatCount (与文件中的值一致)
            int totalIterations = repeatCount < 0 ? -1 : repeatCount;
            return new SBLoop(startTime, totalIterations);
        }

        static void ParseTrigger(string data, SBElement element)
        {
            // data 已去掉前缀: "triggerName,startTime,endTime"
            var parts = SplitCsv(data);
            if (parts.Count < 3) return;
            string name = parts[0].Trim();
            double start = ParseDouble(parts[1]);
            double end = ParseDouble(parts[2]);
            element.Triggers.Add(new SBTrigger(name, start, end));
        }

        // =====================================================
        //  命令分发到正确的容器
        // =====================================================

        static void AddFadeCommand(object target, SBEasing easing, double start, double end, float v1, float v2)
        {
            var cmd = new SBFadeCommand(easing, start, end, v1, v2);
            if (target is SBElement elem) elem.FadeCommands.Add(cmd);
            else if (target is SBLoop loop) loop.FadeCommands.Add(cmd);
        }

        static void AddMoveCommand(object target, SBEasing easing, double start, double end, Vector2 v1, Vector2 v2)
        {
            var cmd = new SBMoveCommand(easing, start, end, v1, v2);
            if (target is SBElement elem) elem.MoveCommands.Add(cmd);
            else if (target is SBLoop loop) loop.MoveCommands.Add(cmd);
        }

        static void AddMoveXCommand(object target, SBEasing easing, double start, double end, float v1, float v2)
        {
            var cmd = new SBMoveAxisCommand(SBCommandType.MX, easing, start, end, v1, v2);
            if (target is SBElement elem) elem.MoveXCommands.Add(cmd);
            else if (target is SBLoop loop) loop.MoveXCommands.Add(cmd);
        }

        static void AddMoveYCommand(object target, SBEasing easing, double start, double end, float v1, float v2)
        {
            var cmd = new SBMoveAxisCommand(SBCommandType.MY, easing, start, end, v1, v2);
            if (target is SBElement elem) elem.MoveYCommands.Add(cmd);
            else if (target is SBLoop loop) loop.MoveYCommands.Add(cmd);
        }

        static void AddScaleCommand(object target, SBEasing easing, double start, double end, float v1, float v2)
        {
            var cmd = new SBScaleCommand(easing, start, end, v1, v2);
            if (target is SBElement elem) elem.ScaleCommands.Add(cmd);
            else if (target is SBLoop loop) loop.ScaleCommands.Add(cmd);
        }

        static void AddScaleVectorCommand(object target, SBEasing easing, double start, double end,
            float startX, float startY, float endX, float endY)
        {
            // V 命令: 非均匀缩放
            var cmd = new SBScaleVectorCommand(easing, start, end, startX, startY, endX, endY);
            if (target is SBElement elem) elem.ScaleVectorCommands.Add(cmd);
            else if (target is SBLoop loop) loop.ScaleVectorCommands.Add(cmd);
        }

        static void AddRotateCommand(object target, SBEasing easing, double start, double end, float v1, float v2)
        {
            var cmd = new SBRotateCommand(easing, start, end, v1, v2);
            if (target is SBElement elem) elem.RotateCommands.Add(cmd);
            else if (target is SBLoop loop) loop.RotateCommands.Add(cmd);
        }

        static void AddColorCommand(object target, SBEasing easing, double start, double end, Color32 v1, Color32 v2)
        {
            var cmd = new SBColorCommand(easing, start, end, v1, v2);
            if (target is SBElement elem) elem.ColorCommands.Add(cmd);
            else if (target is SBLoop loop) loop.ColorCommands.Add(cmd);
        }

        static void AddParameterCommand(object target, SBEasing easing, double start, double end, string param)
        {
            var cmd = new SBParameterCommand(easing, start, end, param);
            if (target is SBElement elem) elem.ParameterCommands.Add(cmd);
            else if (target is SBLoop loop) loop.ParameterCommands.Add(cmd);
        }

        // =====================================================
        //  工具方法
        // =====================================================

        static List<string> SplitCsv(string line)
        {
            var result = new List<string>();
            bool inQuotes = false;
            int start = 0;

            for (int i = 0; i < line.Length; i++)
            {
                if (line[i] == '"') inQuotes = !inQuotes;
                else if (line[i] == ',' && !inQuotes)
                {
                    result.Add(line.Substring(start, i - start));
                    start = i + 1;
                }
            }
            result.Add(line.Substring(start));
            return result;
        }

        static float ParseFloat(string s)
        {
            if (float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
                return v;
            return 0f;
        }

        static double ParseDouble(string s)
        {
            if (double.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
                return v;
            return 0.0;
        }

        static int ParseInt(string s)
        {
            if (int.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v))
                return v;
            return 0;
        }
    }
}
