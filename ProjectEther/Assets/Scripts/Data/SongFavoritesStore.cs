using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace OsuVR
{
    /// <summary>与可重建的谱面索引分开保存收藏。目录身份相对于 Songs 根目录。</summary>
    public sealed class SongFavoritesStore
    {
        [Serializable]
        private sealed class Document
        {
            public int version = 1;
            public List<string> folderKeys = new List<string>();
        }

        private readonly string filePath;
        private readonly string rootPrefix;
        private readonly StringComparison pathComparison;
        private readonly HashSet<string> favorites;
        private bool primaryValid;
        public bool CanWrite { get; private set; } = true;

        public SongFavoritesStore(string songsDirectory, string storagePath)
        {
            filePath = storagePath;
            rootPrefix =
                Path.GetFullPath(songsDirectory).TrimEnd('/', '\\') + Path.DirectorySeparatorChar;
            bool windows = Path.DirectorySeparatorChar == '\\';
            pathComparison = windows
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            favorites = new HashSet<string>(
                windows ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal
            );
            primaryValid = TryLoad(filePath);
            if (!primaryValid && CanWrite)
                TryLoad(filePath + ".bak");
        }

        public bool IsFavorite(string folderPath)
        {
            string key = GetKey(folderPath);
            return key != null && favorites.Contains(key);
        }

        public bool TryToggle(string folderPath)
        {
            string key = GetKey(folderPath);
            if (!CanWrite || key == null)
                return false;
            bool wasFavorite = favorites.Remove(key);
            if (!wasFavorite)
                favorites.Add(key);
            try
            {
                Save();
                return true;
            }
            catch (Exception exception)
            {
                if (wasFavorite)
                    favorites.Add(key);
                else
                    favorites.Remove(key);
                Debug.LogWarning($"[SongFavorites] 收藏保存失败: {exception.Message}");
                return false;
            }
        }

        private string GetKey(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath))
                return null;
            string fullPath = Path.GetFullPath(folderPath).TrimEnd('/', '\\');
            if (!fullPath.StartsWith(rootPrefix, pathComparison))
                return null;
            return fullPath.Substring(rootPrefix.Length).Replace('\\', '/');
        }

        private bool TryLoad(string path)
        {
            if (!File.Exists(path))
                return false;
            try
            {
                var document = JsonUtility.FromJson<Document>(File.ReadAllText(path));
                if (document != null && document.version > 1)
                {
                    CanWrite = false;
                    Debug.LogWarning("[SongFavorites] 收藏文件版本较新，保留文件并暂停写入");
                    return false;
                }
                if (document == null || document.version != 1 || document.folderKeys == null)
                    return false;
                foreach (string key in document.folderKeys)
                    if (
                        !string.IsNullOrEmpty(key)
                        && !Path.IsPathRooted(key)
                        && Array.IndexOf(key.Replace('\\', '/').Split('/'), "..") < 0
                    )
                        favorites.Add(key.Replace('\\', '/'));
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[SongFavorites] 收藏读取失败，将尝试备份: {exception.Message}");
                return false;
            }
        }

        private void Save()
        {
            var keys = new List<string>(favorites);
            keys.Sort(StringComparer.Ordinal);
            string json = JsonUtility.ToJson(new Document { folderKeys = keys }, true);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(filePath)));
            string pending = filePath + ".tmp";
            File.WriteAllText(pending, json, new UTF8Encoding(false));
            if (File.Exists(filePath))
            {
                if (!primaryValid)
                {
                    File.Copy(filePath, filePath + ".invalid-" + Guid.NewGuid().ToString("N"));
                    File.Delete(filePath);
                }
                else
                {
                    try
                    {
                        File.Replace(pending, filePath, filePath + ".bak");
                        primaryValid = true;
                        return;
                    }
                    catch (PlatformNotSupportedException)
                    {
                        // 不支持 Replace 的平台保留可恢复备份后，再发布已写完的临时文件。
                        File.Copy(filePath, filePath + ".bak", true);
                        File.Delete(filePath);
                    }
                }
            }
            File.Move(pending, filePath);
            primaryValid = true;
        }
    }
}
