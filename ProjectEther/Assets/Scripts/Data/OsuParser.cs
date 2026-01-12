using System;
using System.Collections.Generic;
using UnityEngine;

namespace OsuVR
{
    /// <summary>
    /// osu!谱面解析器（简化版）
    /// </summary>
    public static class OsuParser
    {
        // 正则表达式用于分割属性
        private static readonly char[] CommaSeparator = { ',' };
        private static readonly char[] PipeSeparator = { '|' };
        private static readonly char[] ColonSeparator = { ':' };

        // 击打对象类型枚举（简化版）
        private enum HitObjectType
        {
            Normal = 1,
            Slider = 2,
            Spinner = 8,
            NewCombo = 4,
            ComboColorOffset = 112 // 11110000 in binary
        }

        /// <summary>
        /// 解析击打对象行
        /// </summary>
        /// <param name="line">谱面文件中的一行</param>
        /// <param name="beatmap">谱面数据</param>
        public static void ParseHitObject(string line, Beatmap beatmap)
        {
            try
            {
                // 使用逗号分割行
                string[] parts = line.Split(CommaSeparator, StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length < 4) return; // 格式不对直接跳过

                // 解析基础信息
                float x = float.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture);
                float y = float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
                Vector2 position = new Vector2(x, y);
                double time = double.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture);

                // --- 核心修复区 ---
                int type = int.Parse(parts[3]);

                // 1. 提取“新连击”标志
                bool isNewCombo = (type & (int)HitObjectType.NewCombo) != 0;

                // 2. 提取“连击颜色偏移”
                int comboOffset = (type & (int)HitObjectType.ComboColorOffset) >> 4;

                // 3. 关键：把上面两个标志位“扣掉”，剩下的就是纯粹的物体类型
                // 比如：5 (Circle + NewCombo) -> 扣掉 4 -> 变成 1 (Circle)
                int actualType = type & ~((int)HitObjectType.NewCombo | (int)HitObjectType.ComboColorOffset);
                // ------------------

                // 解析音效 (如果有)
                int soundType = parts.Length > 4 ? int.Parse(parts[4]) : 0;

                switch (actualType)
                {
                    case (int)HitObjectType.Normal:
                        CreateHitCircle(parts, time, position, beatmap, isNewCombo, comboOffset);
                        break;

                    case (int)HitObjectType.Slider:
                        Debug.Log($"检测到滑条 (Slider)，暂时跳过");
                        break;

                    case (int)HitObjectType.Spinner:
                        Debug.Log($"检测到转盘 (Spinner)，暂时跳过");
                        break;

                    default:
                        // 如果还有警告，那就是真的未知类型了
                        Debug.LogWarning($"修正后依然未知的类型: {actualType} (原始type: {type})");
                        break;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"解析出错: {e.Message}");
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
                                   (beatmap.HitObjects.Count > 0 && beatmap.HitObjects[beatmap.HitObjects.Count - 1] is Spinner) ||
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

        /// <summary>
        /// 解析音效信息
        /// </summary>
        private static void ParseSampleInfo(HitCircle circle, string[] sampleParts)
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

                    // 这里可以进一步处理音效信息
                    // 暂时只记录日志
                    Debug.Log($"解析到音效信息 - 正常库: {normalSampleBank}, 打击库: {addSampleBank}");
                }

                if (sampleParts.Length >= 3)
                {
                    // 解析自定义音效库
                    int customSampleBank = int.Parse(sampleParts[2]);
                }

                if (sampleParts.Length >= 4)
                {
                    // 解析音量
                    int volume = int.Parse(sampleParts[3]);
                }

                if (sampleParts.Length >= 5)
                {
                    // 解析文件名
                    string filename = sampleParts[4];
                }
            }
            catch (FormatException)
            {
                Debug.LogWarning("音效信息格式错误");
            }
        }
    }

    /// <summary>
    /// 谱面数据类（简化版）
    /// </summary>
    public class Beatmap
    {
        /// <summary>
        /// 击打对象列表
        /// </summary>
        public List<HitObject> HitObjects { get; } = new List<HitObject>();

        /// <summary>
        /// 谱面格式版本
        /// </summary>
        public int FormatVersion { get; set; } = 14; // 默认使用较新版本

        /// <summary>
        /// 获取偏移时间（简化版，实际应该根据谱面偏移调整）
        /// </summary>
        public double GetOffsetTime(double time)
        {
            // 这里可以添加偏移量计算
            // 目前直接返回原时间
            return time;
        }
    }

    /// <summary>
    /// 简化版使用示例
    /// </summary>
    public class OsuParserExample : MonoBehaviour
    {
        void Start()
        {
            // 示例：解析一个点击圆圈行
            // osu!文件格式：x,y,time,type,hitSound,objectParams,hitSample
            string exampleLine = "256,192,1000,1,0,0:0:0:0:";

            Beatmap beatmap = new Beatmap();
            OsuParser.ParseHitObject(exampleLine, beatmap);

            if (beatmap.HitObjects.Count > 0)
            {
                Debug.Log($"成功解析 {beatmap.HitObjects.Count} 个击打对象");

                foreach (var hitObject in beatmap.HitObjects)
                {
                    if (hitObject is HitCircle circle)
                    {
                        Debug.Log($"点击圆圈 - 时间: {circle.StartTime}, 位置: {circle.Position}");
                    }
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

                foreach (string line in lines)
                {
                    string trimmedLine = line.Trim();

                    // 检测是否进入击打对象部分
                    if (trimmedLine == "[HitObjects]")
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
            }
            catch (System.Exception e)
            {
                Debug.LogError($"加载谱面失败: {e.Message}");
            }
        }
    }
}