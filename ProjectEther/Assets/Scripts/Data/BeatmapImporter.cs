using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using UnityEngine;

namespace OsuVR
{
    public enum ImportResult
    {
        Success,
        Cancelled,
        Error,
    }

    /// <summary>
    /// 静态工具类：负责解压 .osz 文件 + 跨平台导入
    /// </summary>
    public static class BeatmapImporter
    {
        /// <summary>标记: 有新歌曲导入完成, 选歌界面需要刷新</summary>
        public static bool HasNewImport { get; set; }

        public static string SongsDirectory
        {
            get
            {
                string path = Path.Combine(Application.persistentDataPath, "Songs");
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);
                return path;
            }
        }

        // ============================================================
        //  PC: 打开 Songs 文件夹
        // ============================================================

        public static void OpenSongsDirectory()
        {
            string path = SongsDirectory;
            try
            {
#if UNITY_STANDALONE_WIN
                System.Diagnostics.Process.Start("explorer.exe", path.Replace("/", "\\"));
#elif UNITY_STANDALONE_OSX
                System.Diagnostics.Process.Start("open", path);
#elif UNITY_STANDALONE_LINUX
                System.Diagnostics.Process.Start("xdg-open", path);
#endif
                Debug.Log($"[Importer] 已打开 Songs 目录: {path}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Importer] 打开目录失败: {e.Message}");
            }
        }

        // ============================================================
        //  Android: 原生文件选择器导入 .osz
        //
        //  系统选择器切换焦点，部分环境也可能重建 Activity。复制由 Java 后台
        //  完成，结果保存到 SharedPreferences；快通道只唤醒取件，避免重复处理。
        //  提示独立于设置页面，并等待 VR 可显示后再计时：
        //    1. 调用 OpenAndroidFilePicker -> startActivityForResult(1001)
        //    2. Java 复制完成 -> 保存结果 + UnitySendMessage 唤醒取件
        //    3. HandleImportMessage 按本次文件逐个解压并报告实际结果
        // ============================================================

        /// <summary>必须与 FilePickerActivity.java 的 REQUEST_PICK_OSU 一致</summary>
        private const int FilePickerRequestCode = 1001;

        private static System.Action<ImportResult, string> _onFilePicked;
        private static BeatmapImporterHelper _helper;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitializeAndroidImports()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            GetOrCreateHelper();
#endif
        }

        public static void OpenAndroidFilePicker(
            System.Action<ImportResult, string> onComplete = null
        )
        {
            PullPendingImports();
            _onFilePicked = onComplete;

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                GetOrCreateHelper();

                using (var intent = new AndroidJavaObject("android.content.Intent"))
                {
                    intent.Call<AndroidJavaObject>(
                        "setAction",
                        "android.intent.action.GET_CONTENT"
                    );
                    intent.Call<AndroidJavaObject>("setType", "*/*");
                    intent.Call<AndroidJavaObject>(
                        "addCategory",
                        "android.intent.category.OPENABLE"
                    );
                    intent.Call<AndroidJavaObject>(
                        "putExtra",
                        "android.intent.extra.ALLOW_MULTIPLE",
                        true
                    );

                    using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                    {
                        activity.Call("startActivityForResult", intent, FilePickerRequestCode);
                    }
                }

                Debug.Log("[Importer] Android 文件选择器已打开");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Importer] Android 文件选择器启动失败: {e.Message}");
                // 典型场景: 设备未注册任何文件选择器 (ActivityNotFoundException)
                string hint =
                    e.Message != null && e.Message.Contains("No Activity found")
                        ? "此设备的系统未提供文件选择器，请手动将 .osz 文件复制到 Songs 目录"
                        : e.Message;
                CompleteImport(ImportResult.Error, hint);
            }
#else
            Debug.Log("[Importer] 非 Android 平台，跳过文件选择器");
            CompleteImport(ImportResult.Cancelled, null);
#endif
        }

        internal static void InvokeCallback(ImportResult result, string detail)
        {
            var callback = _onFilePicked;
            _onFilePicked = null;
            callback?.Invoke(result, detail);
        }

        private static void CompleteImport(
            ImportResult result,
            string detail,
            bool duplicatesOnly = false
        )
        {
            string text;
            Color color;
            if (result == ImportResult.Success)
            {
                text = string.Format(
                    LocalizationManager.GetText(
                        duplicatesOnly ? "ui_import_duplicate" : "ui_import_success"
                    ),
                    detail
                );
                color = duplicatesOnly ? new Color(1f, 0.85f, 0.3f) : Color.green;
            }
            else if (result == ImportResult.Cancelled)
            {
                text = LocalizationManager.GetText("ui_import_cancelled");
                color = new Color(1f, 0.85f, 0.3f);
            }
            else
            {
                text = LocalizationManager.GetText("ui_import_error");
                if (!string.IsNullOrEmpty(detail))
                    text += "\n" + detail;
                color = Color.red;
            }

            VRToast.Show(text, color, result == ImportResult.Error ? 8f : 5f);
            Debug.Log($"[Importer] 导入结果: {result}; {detail}");
            InvokeCallback(result, detail);
        }

        private static BeatmapImporterHelper GetOrCreateHelper()
        {
            if (_helper == null)
            {
                // GameObject 名必须与 FilePickerActivity.java 的 UNITY_HELPER_OBJECT 一致
                var go = new GameObject("[BeatmapImporterHelper]");
                _helper = go.AddComponent<BeatmapImporterHelper>();
                Object.DontDestroyOnLoad(go);
            }
            return _helper;
        }

        /// <summary>
        /// 处理 Java 端回传的导入结果消息。
        /// 格式: "cancel:"=取消; "ok:a.osz|b.osz[|err:原因]"=复制结果; "err:原因"=失败。
        /// 此处只处理已取出的结果，不再次消费 Java 存档，避免误删下一份结果。
        /// </summary>
        internal static void HandleImportMessage(string message)
        {
            if (string.IsNullOrEmpty(message) || message == "cancel:")
            {
                Debug.Log("[Importer] 文件选择已取消");
                CompleteImport(ImportResult.Cancelled, null);
                return;
            }

            if (message.StartsWith("ok:"))
            {
                var imported = new List<string>();
                var duplicates = new List<string>();
                var errors = new List<string>();
                foreach (var token in message.Substring(3).Split('|'))
                {
                    if (token.StartsWith("err:"))
                    {
                        errors.Add(token.Substring(4));
                        continue;
                    }
                    if (token.Length == 0)
                        continue;
                    if (
                        token != Path.GetFileName(token)
                        || token.Contains("\\")
                        || !token.EndsWith(".osz", System.StringComparison.OrdinalIgnoreCase)
                    )
                    {
                        errors.Add(LocalizationManager.GetText("ui_import_invalid_filename"));
                        continue;
                    }

                    string name = Path.GetFileNameWithoutExtension(token);
                    if (string.IsNullOrEmpty(name))
                    {
                        errors.Add(LocalizationManager.GetText("ui_import_invalid_filename"));
                        continue;
                    }
                    string path = Path.Combine(SongsDirectory, token);
                    // 选歌启动扫描可能先完成解压，迟到的保存结果仍可正确报告已存在。
                    if (!File.Exists(path) && Directory.Exists(Path.Combine(SongsDirectory, name)))
                    {
                        duplicates.Add(name);
                        continue;
                    }
                    if (ImportOsz(path, out bool duplicate, out string error))
                    {
                        if (duplicate)
                            duplicates.Add(name);
                        else
                            imported.Add(name);
                    }
                    else
                        errors.Add($"{name}: {error}");
                }

                if (imported.Count == 0 && duplicates.Count == 0)
                {
                    CompleteImport(ImportResult.Error, string.Join("\n", errors));
                    return;
                }

                if (imported.Count > 0)
                    HasNewImport = true;
                bool duplicatesOnly = imported.Count == 0;
                string detail = string.Join(", ", duplicatesOnly ? duplicates : imported);
                if (!duplicatesOnly && duplicates.Count > 0)
                    detail +=
                        "\n"
                        + string.Format(
                            LocalizationManager.GetText("ui_import_duplicate"),
                            string.Join(", ", duplicates)
                        );
                if (errors.Count > 0)
                    detail +=
                        "\n"
                        + string.Format(
                            LocalizationManager.GetText("ui_import_partial_error"),
                            string.Join("\n", errors)
                        );
                CompleteImport(ImportResult.Success, detail, duplicatesOnly);
            }
            else if (message.StartsWith("err:"))
            {
                string detail = message.Substring(4);
                CompleteImport(
                    ImportResult.Error,
                    detail == "empty_result"
                        ? LocalizationManager.GetText("ui_import_empty_result")
                        : detail
                );
            }
            else
            {
                CompleteImport(ImportResult.Error, message);
            }
        }

        internal static void ReceiveNativeResult(string notification)
        {
            string pending = ConsumePendingResult();
            if (!string.IsNullOrEmpty(pending))
                HandleImportMessage(pending);
            else if (pending == null || string.IsNullOrEmpty(notification))
                // 存储不可读时保留快通道；空消息兼容旧版 Java 的取消回调。
                HandleImportMessage(notification);
        }

        /// <summary>
        /// 兜底通道: 从 Java 侧拉取未送达的导入结果（UnitySendMessage 因
        /// Activity 重建/Unity 重启丢失时，结果仍保存在 SharedPreferences）。
        /// 在 OnApplicationFocus 与选歌界面启动时调用。
        /// </summary>
        public static void PullPendingImports()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                string message = ConsumePendingResult();
                if (!string.IsNullOrEmpty(message))
                {
                    Debug.Log($"[Importer] 拉取到未送达的导入结果: {message}");
                    HandleImportMessage(message);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Importer] 拉取导入结果失败: {e.Message}");
            }
#endif
        }

        /// <summary>取走 Java 保存结果：空串表示无，null 表示存储不可读。</summary>
        private static string ConsumePendingResult()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    using (
                        var cls = new AndroidJavaClass("com.nyaon.projectether.FilePickerActivity")
                    )
                        return cls.CallStatic<string>("consumePendingResult", activity);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Importer] 消费导入结果失败: {e.Message}");
                return null;
            }
#else
            return "";
#endif
        }

        // ============================================================
        //  OSZ 解压
        // ============================================================

        /// <summary>已导入 .osz 的 MD5 记录文件（Songs 目录下，每行一个哈希）。
        /// 用于内容去重：同一文件改名后再次导入会被跳过。
        /// 注：记录在本功能上线之前的存量歌曲无哈希可查，改名重导会重复一次，属已知限制。</summary>
        private static readonly string HashFilePath = Path.Combine(
            Application.persistentDataPath,
            "Songs",
            ".imported.md5"
        );

        public static void ImportNewOszFiles()
        {
            string[] oszFiles = Directory.GetFiles(SongsDirectory, "*.osz");
            if (oszFiles.Length == 0)
                return;

            Debug.Log($"[Importer] 发现 {oszFiles.Length} 个新 .osz 文件，准备解压...");
            var duplicates = new List<string>();
            var errors = new List<string>();
            foreach (var oszPath in oszFiles)
            {
                if (ImportOsz(oszPath, out bool duplicate, out string error))
                {
                    if (duplicate)
                        duplicates.Add(Path.GetFileNameWithoutExtension(oszPath));
                }
                else
                    errors.Add($"{Path.GetFileName(oszPath)}: {error}");
            }

            if (duplicates.Count > 0)
            {
                string detail = string.Join(", ", duplicates);
                Debug.Log($"[Importer] 跳过 {duplicates.Count} 个重复谱面: {detail}");
                VRToast.Show(
                    string.Format(LocalizationManager.GetText("ui_import_duplicate"), detail),
                    new Color(1f, 0.85f, 0.3f),
                    5f
                );
            }
            if (errors.Count > 0)
                VRToast.Show(
                    LocalizationManager.GetText("ui_import_error")
                        + "\n"
                        + string.Join("\n", errors),
                    Color.red,
                    8f
                );
        }

        /// <summary>
        /// 安全解压 zip：逐条校验条目路径，拒绝路径穿越（zip-slip）。
        /// .osz 是不可信的社区内容，恶意条目（../ 或绝对路径）可能写出 Songs 目录之外。
        /// </summary>
        private static void ExtractZipSafely(string zipPath, string targetFolder)
        {
            Directory.CreateDirectory(targetFolder);
            string fullTarget = Path.GetFullPath(targetFolder);
            if (!fullTarget.EndsWith(Path.DirectorySeparatorChar.ToString()))
                fullTarget += Path.DirectorySeparatorChar;

            using (var archive = ZipFile.OpenRead(zipPath))
            {
                foreach (var entry in archive.Entries)
                {
                    string destPath = Path.GetFullPath(
                        Path.Combine(
                            fullTarget,
                            entry.FullName.Replace('/', Path.DirectorySeparatorChar)
                        )
                    );
                    if (!destPath.StartsWith(fullTarget, System.StringComparison.Ordinal))
                    {
                        Debug.LogWarning(
                            $"[Importer] 跳过可疑 zip 条目（路径越界）: {entry.FullName}"
                        );
                        continue;
                    }

                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        Directory.CreateDirectory(destPath);
                        continue;
                    }

                    string parentDir = Path.GetDirectoryName(destPath);
                    if (!string.IsNullOrEmpty(parentDir))
                        Directory.CreateDirectory(parentDir);
                    entry.ExtractToFile(destPath, true);
                }
            }
        }

        /// <summary>解压单个 .osz；返回是否成功，duplicate 单独标明已存在。</summary>
        private static bool ImportOsz(string oszPath, out bool duplicate, out string error)
        {
            duplicate = false;
            error = null;
            string fileName = Path.GetFileNameWithoutExtension(oszPath);
            string targetFolder = Path.Combine(SongsDirectory, fileName);
            bool createdTarget = false;
            try
            {
                if (!File.Exists(oszPath) || string.IsNullOrEmpty(fileName))
                    throw new FileNotFoundException(
                        LocalizationManager.GetText("ui_import_file_missing")
                    );

                // 内容去重：同一 .osz 改名后再次导入（MD5 一致）则直接跳过
                string hash = ComputeFileMd5(oszPath);
                var imported = LoadImportedHashes();
                if (hash != null && imported.Contains(hash))
                {
                    TryDeleteOsz(oszPath, "重复内容");
                    duplicate = true;
                    return true;
                }

                if (Directory.Exists(targetFolder))
                {
                    // 重复导入: 目标文件夹已存在，直接清掉 .osz，
                    // 否则它会一直残留在 Songs 里，导致每次启动都被重复扫描。
                    // 同名不代表相同内容；未解压的新内容不能记作已导入。
                    TryDeleteOsz(oszPath, "重复文件名");
                    duplicate = true;
                    return true;
                }

                Debug.Log($"正在解压: {fileName}...");
                createdTarget = true;
                ExtractZipSafely(oszPath, targetFolder);
                File.Delete(oszPath);
                if (hash != null)
                    SaveImportedHashes(hash, imported);
                Debug.Log($"<color=green>导入成功:</color> {fileName}");
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"解压失败 {oszPath}: {e.Message}");
                error = e.Message;
                // 只清理本次创建的目录，不删除用户原有曲目。
                if (createdTarget && Directory.Exists(targetFolder))
                {
                    try
                    {
                        Directory.Delete(targetFolder, true);
                    }
                    catch (System.Exception cleanupError)
                    {
                        Debug.LogWarning($"[Importer] 清理失败目录失败: {cleanupError.Message}");
                    }
                }
                return false;
            }
        }

        private static void TryDeleteOsz(string oszPath, string reason)
        {
            try
            {
                File.Delete(oszPath);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Importer] 清理重复的 .osz 失败 ({reason}): {e.Message}");
            }
        }

        /// <summary>流式计算文件 MD5（返回小写十六进制；失败返回 null，不阻塞导入）</summary>
        private static string ComputeFileMd5(string path)
        {
            try
            {
                using (var md5 = MD5.Create())
                using (var stream = File.OpenRead(path))
                    return System
                        .BitConverter.ToString(md5.ComputeHash(stream))
                        .Replace("-", "")
                        .ToLowerInvariant();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Importer] 计算 MD5 失败 ({path}): {e.Message}");
                return null;
            }
        }

        private static HashSet<string> LoadImportedHashes()
        {
            var set = new HashSet<string>();
            try
            {
                if (File.Exists(HashFilePath))
                    foreach (var line in File.ReadAllLines(HashFilePath))
                        if (line.Length == 32)
                            set.Add(line);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Importer] 读取导入记录失败: {e.Message}");
            }
            return set;
        }

        private static void SaveImportedHashes(string newHash, HashSet<string> imported)
        {
            try
            {
                imported.Add(newHash);
                File.WriteAllLines(HashFilePath, imported);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Importer] 写入导入记录失败: {e.Message}");
            }
        }
    }

    /// <summary>
    /// Android 回调辅助 MonoBehaviour。
    /// 由 FilePickerActivity.java 通过 UnitySendMessage("[BeatmapImporterHelper]", "OnFilesPicked", ...) 调用
    /// </summary>
    public class BeatmapImporterHelper : MonoBehaviour
    {
        /// <summary>
        /// message: ""=取消; "ok:a.osz|b.osz[|err:原因]"=Java 复制完成; "err:原因"=失败
        /// </summary>
        public void OnFilesPicked(string message)
        {
            BeatmapImporter.ReceiveNativeResult(message);
        }

        private void Start()
        {
            BeatmapImporter.PullPendingImports();
        }

        private void OnApplicationPause(bool paused)
        {
            if (!paused)
                BeatmapImporter.PullPendingImports();
        }

        // 从 2D 选择器返回 VR 时触发。若 UnitySendMessage 因 Activity 重建
        // 丢失，这里从 Java 侧 SharedPreferences 拉取同一结果兜底
        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus)
                BeatmapImporter.PullPendingImports();
        }
    }
}
