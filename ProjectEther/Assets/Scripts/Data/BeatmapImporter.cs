using System.Collections.Generic;
using System.IO;
using System.IO.Compression; // 确保 Unity 项目设置里 Api Compatibility Level 是 .NET Standard 2.1
using UnityEngine;

namespace OsuVR
{
    /// <summary>
    /// 纯静态工具类：负责解压 .osz 文件
    /// </summary>
    public static class BeatmapImporter
    {
        // 游戏存放歌曲的根目录
        // PC: C:/Users/用户名/AppData/LocalLow/公司名/游戏名/Songs
        // Quest: /sdcard/Android/data/包名/files/Songs
        public static string SongsDirectory
        {
            get
            {
                string path = Path.Combine(Application.persistentDataPath, "Songs");
                if (!Directory.Exists(path)) Directory.CreateDirectory(path);
                return path;
            }
        }

        /// <summary>
        /// 扫描 Songs 目录，解压所有未解压的 .osz 文件
        /// </summary>
        public static void ImportNewOszFiles()
        {
            // 1. 获取目录下所有的 .osz 文件
            string[] oszFiles = Directory.GetFiles(SongsDirectory, "*.osz");

            if (oszFiles.Length == 0) return;

            Debug.Log($"[Importer] 发现 {oszFiles.Length} 个新 .osz 文件，准备解压...");

            foreach (var oszPath in oszFiles)
            {
                ImportOsz(oszPath);
            }
        }

        /// <summary>
        /// 解压单个 .osz 文件
        /// </summary>
        private static void ImportOsz(string oszPath)
        {
            try
            {
                string fileName = Path.GetFileNameWithoutExtension(oszPath);
                // 目标文件夹路径：Songs/歌曲名
                string targetFolder = Path.Combine(SongsDirectory, fileName);

                // 如果文件夹已存在，说明解压过了（或者为了安全起见跳过）
                if (Directory.Exists(targetFolder))
                {
                    return;
                }

                Debug.Log($"正在解压: {fileName}...");

                // 核心解压
                ZipFile.ExtractToDirectory(oszPath, targetFolder);

                // 解压成功后，删除原 .osz 文件以节省空间 (可选，如果想保留就把下面这行注释掉)
                File.Delete(oszPath);

                Debug.Log($"<color=green>导入成功:</color> {fileName}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"解压失败 {oszPath}: {e.Message}");
                // 如果解压失败，尝试清理残留的空文件夹
                string fileName = Path.GetFileNameWithoutExtension(oszPath);
                string targetFolder = Path.Combine(SongsDirectory, fileName);
                if (Directory.Exists(targetFolder)) Directory.Delete(targetFolder, true);
            }
        }
    }
}