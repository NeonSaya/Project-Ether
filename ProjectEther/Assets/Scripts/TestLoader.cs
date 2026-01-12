using UnityEngine;
using System.IO;
using System.Collections.Generic;
using OsuVR; // <--- 关键修改：必须引用你代码里的命名空间

public class TestLoader : MonoBehaviour
{
    // 在编辑器里填入你的文件名，比如 "test.osu"
    public string fileName = "test.osu";

    void Start()
    {
        Debug.Log("== 脚本开始运行了！ ==");
        // 1. 拼凑出文件的完整路径
        string filePath = Path.Combine(Application.dataPath, "Songs", fileName);

        Debug.Log("正在尝试读取文件: " + filePath);

        // 2. 检查文件存不存在
        if (!File.Exists(filePath))
        {
            Debug.LogError("找不到文件！请检查 Assets/Songs 文件夹里有没有 " + fileName);
            return;
        }

        // --- 适配新代码的解析逻辑 ---

        // A. 创建一个空的谱面容器
        Beatmap beatmap = new Beatmap();

        // B. 读取文件所有行
        string[] lines = File.ReadAllLines(filePath);
        bool isHitObjectsSection = false;

        // C. 开始一行一行扫描
        foreach (string line in lines)
        {
            string trimmedLine = line.Trim();

            // 只有当读到 [HitObjects] 这一行之后，才开始解析音符
            if (trimmedLine == "[HitObjects]")
            {
                isHitObjectsSection = true;
                continue;
            }

            // 如果遇到下一个像 [Editor] 这样的标题，就停止解析
            if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
            {
                isHitObjectsSection = false;
            }

            // 如果在音符区域，且这一行不是空的，就解析它
            if (isHitObjectsSection && !string.IsNullOrEmpty(trimmedLine))
            {
                // 调用你上传的 OsuParser 中的静态方法
                OsuParser.ParseHitObject(trimmedLine, beatmap);
            }
        }

        // 4. 验证结果
        if (beatmap.HitObjects.Count > 0)
        {
            Debug.Log($"🎉 成功啦！一共解析了 {beatmap.HitObjects.Count} 个音符！");

            // 打印第一个音符的信息
            HitObject first = beatmap.HitObjects[0];
            Debug.Log($"第一个音符 - 时间: {first.StartTime}ms, 位置: {first.Position}");

            // 检查类型
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