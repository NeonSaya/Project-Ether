using System;
using System.Collections.Generic;
using System.Globalization;
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
        /// </summary>
        public static SBStoryboard Parse(List<string> lines)
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
                    // 命令行: 以空格或下划线开头
                    bool isCommand = rawLine.StartsWith(" ") || rawLine.StartsWith("_");
                    string line = rawLine.Trim();

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
                        if (line.StartsWith("L") || line.StartsWith("l"))
                        {
                            // Loop 命令: L,startTime,loopCount
                            currentLoop = ParseLoop(line, currentElement);
                            if (currentLoop != null)
                                currentElement.Loops.Add(currentLoop);
                        }
                        else if (line.StartsWith("T") || line.StartsWith("t"))
                        {
                            // Trigger 命令: T,triggerName,startTime,endTime
                            currentLoop = null;
                            ParseTrigger(line, currentElement);
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

            Debug.Log($"[SBParser] 解析完成: {storyboard.TotalElementCount} 个元素");
            return storyboard;
        }

        // =====================================================
        //  元素解析
        // =====================================================

        static SBElement ParseElement(string line)
        {
            var parts = SplitCsv(line);
            if (parts.Count < 6) return null;

            string type = parts[0].Trim();
            if (!Enum.TryParse(parts[1].Trim(), out SBLayer layer))
                layer = SBLayer.Background;
            if (!Enum.TryParse(parts[2].Trim(), out SBOrigin origin))
                origin = SBOrigin.Centre;
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

            return null;
        }

        // =====================================================
        //  命令解析
        // =====================================================

        static void ParseCommand(string line, object target)
        {
            var parts = SplitCsv(line);
            if (parts.Count < 4) return;

            string typeStr = parts[0].Trim();
            if (!Enum.TryParse(typeStr, out SBEasing easing))
                easing = SBEasing.Linear;

            double startTime = ParseDouble(parts[1]);
            double endTime = parts.Count > 2 ? ParseDouble(parts[2]) : startTime;

            // 确保 endTime >= startTime
            if (endTime < startTime) endTime = startTime;

            switch (typeStr)
            {
                case "F":
                    AddFadeCommand(target, easing, startTime, endTime,
                        ParseFloat(parts[3]),
                        parts.Count > 4 ? ParseFloat(parts[4]) : ParseFloat(parts[3]));
                    break;

                case "M":
                    AddMoveCommand(target, easing, startTime, endTime,
                        new Vector2(ParseFloat(parts[3]), ParseFloat(parts[4])),
                        parts.Count > 5
                            ? new Vector2(ParseFloat(parts[5]), ParseFloat(parts[6]))
                            : new Vector2(ParseFloat(parts[3]), ParseFloat(parts[4])));
                    break;

                case "MX":
                    AddMoveXCommand(target, easing, startTime, endTime,
                        ParseFloat(parts[3]),
                        parts.Count > 4 ? ParseFloat(parts[4]) : ParseFloat(parts[3]));
                    break;

                case "MY":
                    AddMoveYCommand(target, easing, startTime, endTime,
                        ParseFloat(parts[3]),
                        parts.Count > 4 ? ParseFloat(parts[4]) : ParseFloat(parts[3]));
                    break;

                case "S":
                    AddScaleCommand(target, easing, startTime, endTime,
                        ParseFloat(parts[3]),
                        parts.Count > 4 ? ParseFloat(parts[4]) : ParseFloat(parts[3]));
                    break;

                case "R":
                    AddRotateCommand(target, easing, startTime, endTime,
                        ParseFloat(parts[3]),
                        parts.Count > 4 ? ParseFloat(parts[4]) : ParseFloat(parts[3]));
                    break;

                case "C":
                    AddColorCommand(target, easing, startTime, endTime,
                        new Color32(
                            (byte)ParseInt(parts[3]),
                            (byte)ParseInt(parts[4]),
                            (byte)ParseInt(parts[5]), 255),
                        parts.Count > 8
                            ? new Color32(
                                (byte)ParseInt(parts[6]),
                                (byte)ParseInt(parts[7]),
                                (byte)ParseInt(parts[8]), 255)
                            : new Color32(
                                (byte)ParseInt(parts[3]),
                                (byte)ParseInt(parts[4]),
                                (byte)ParseInt(parts[5]), 255));
                    break;

                case "P":
                    string param = parts.Count > 3 ? parts[3].Trim() : "";
                    AddParameterCommand(target, easing, startTime, endTime, param);
                    break;
            }
        }

        static SBLoop ParseLoop(string line, SBElement element)
        {
            // L,startTime,loopCount
            var parts = SplitCsv(line);
            if (parts.Count < 3) return null;
            double startTime = ParseDouble(parts[1]);
            int loopCount = ParseInt(parts[2]);
            return new SBLoop(startTime, loopCount);
        }

        static void ParseTrigger(string line, SBElement element)
        {
            // T,triggerName,startTime,endTime
            var parts = SplitCsv(line);
            if (parts.Count < 4) return;
            string name = parts[1].Trim();
            double start = ParseDouble(parts[2]);
            double end = ParseDouble(parts[3]);
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
            // Parameter 不加入 Loop
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
