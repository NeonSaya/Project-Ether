#if UNITY_EDITOR
using UnityEngine;
using System.IO;
using OsuVR;

/// <summary>
/// 测试加载器：仅在编辑器模式下编译
/// </summary>
public class TestLoader : MonoBehaviour
{
    public string fileName = "test.osu";

    void Start()
    {
        Debug.Log("== 脚本开始运行了！ ==");
        string filePath = Path.Combine(Application.dataPath, "Songs", fileName);

        Debug.Log("正在尝试读取文件: " + filePath);

        if (!File.Exists(filePath))
        {
            Debug.LogError("找不到文件！请检查 Assets/Songs 文件夹里有没有 " + fileName);
            return;
        }

        Beatmap beatmap = new Beatmap();
        string[] lines = File.ReadAllLines(filePath);
        bool isHitObjectsSection = false;

        foreach (string line in lines)
        {
            string trimmedLine = line.Trim();

            if (trimmedLine == "[HitObjects]")
            {
                isHitObjectsSection = true;
                continue;
            }

            if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
            {
                isHitObjectsSection = false;
            }

            if (isHitObjectsSection && !string.IsNullOrEmpty(trimmedLine))
            {
                OsuParser.ParseHitObject(trimmedLine, beatmap);
            }
        }

        if (beatmap.HitObjects.Count > 0)
        {
            Debug.Log($"成功！一共解析了 {beatmap.HitObjects.Count} 个音符！");

            HitObject first = beatmap.HitObjects[0];
            Debug.Log($"第一个音符 - 时间: {first.StartTime}ms, 位置: {first.Position}");

            if (first is HitCircle)
            {
                Debug.Log("类型确认：这是一个 HitCircle (点击圆圈)");
            }
        }
        else
        {
            Debug.LogError("解析完成，但没有找到任何音符。请检查 .osu 文件里有没有 [HitObjects] 这一段。");
        }
    }
}
#endif
