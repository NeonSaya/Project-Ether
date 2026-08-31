using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEngine;

namespace OsuVR
{
    public enum ImportResult { Success, Cancelled, Error }

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
                if (!Directory.Exists(path)) Directory.CreateDirectory(path);
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
        //  回调链路（缺一不可）:
        //    1. 这里通过 startActivityForResult(intent, 1001) 拉起系统选择器
        //    2. FilePickerActivity.java (Assets/Plugins/Android) 接收
        //       onActivityResult，把 URI 列表（'\n' 分隔，取消时为空串）
        //       经 UnitySendMessage 发给 GameObject "[BeatmapImporterHelper]"
        //    3. BeatmapImporterHelper.OnFilesPicked 在此分发给下面的处理流程
        // ============================================================

        /// <summary>必须与 FilePickerActivity.java 的 REQUEST_PICK_OSU 一致</summary>
        private const int FilePickerRequestCode = 1001;

        /// <summary>多选时 Java 端以 '\n' 连接各 URI</summary>
        internal static readonly char[] UriSeparator = { '\n' };

        private static System.Action<ImportResult, string> _onFilePicked;
        private static BeatmapImporterHelper _helper;

        public static void OpenAndroidFilePicker(System.Action<ImportResult, string> onComplete = null)
        {
            _onFilePicked = onComplete;

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                GetOrCreateHelper();

                using (var intent = new AndroidJavaObject("android.content.Intent"))
                {
                    intent.Call<AndroidJavaObject>("setAction", "android.intent.action.GET_CONTENT");
                    intent.Call<AndroidJavaObject>("setType", "*/*");
                    intent.Call<AndroidJavaObject>("addCategory", "android.intent.category.OPENABLE");
                    intent.Call<AndroidJavaObject>("putExtra", "android.intent.extra.ALLOW_MULTIPLE", true);

                    using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    {
                        var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                        activity.Call("startActivityForResult", intent, FilePickerRequestCode);
                    }
                }

                Debug.Log("[Importer] Android 文件选择器已打开");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Importer] Android 文件选择器启动失败: {e.Message}");
                // 典型场景: 设备未注册任何文件选择器 (ActivityNotFoundException)
                string hint = e.Message != null && e.Message.Contains("No Activity found")
                    ? "此设备的系统未提供文件选择器，请手动将 .osz 文件复制到 Songs 目录"
                    : e.Message;
                _onFilePicked?.Invoke(ImportResult.Error, hint);
            }
#else
            Debug.Log("[Importer] 非 Android 平台，跳过文件选择器");
            _onFilePicked?.Invoke(ImportResult.Cancelled, null);
#endif
        }

        internal static void InvokeCallback(ImportResult result, string detail)
        {
            _onFilePicked?.Invoke(result, detail);
        }

        /// <summary>把 Java 端回传的 URI 列表字符串拆成数组（空串/取消 -> 空数组）</summary>
        public static string[] SplitUriList(string uriList)
        {
            if (string.IsNullOrEmpty(uriList))
                return System.Array.Empty<string>();
            return uriList.Split(UriSeparator, System.StringSplitOptions.RemoveEmptyEntries);
        }

        internal static void ProcessAndroidResult(string[] uris)
        {
            var helper = GetOrCreateHelper();
            helper.StartCoroutine(ProcessAndroidUrisCoroutine(uris));
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

        private static System.Collections.IEnumerator ProcessAndroidUrisCoroutine(string[] uris)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            var importedNames = new List<string>();
            string firstError = null;

            foreach (var uriString in uris)
            {
                string destPath = null;
                try
                {
                    destPath = CopyUriToSongs(uriString);
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[Importer] 复制文件失败 ({uriString}): {e.Message}\n{e.StackTrace}");
                    if (firstError == null) firstError = e.Message;
                    continue;
                }

                if (destPath == null)
                {
                    if (firstError == null) firstError = $"文件复制失败: {uriString}";
                    continue;
                }

                try
                {
                    ImportOsz(destPath);
                    HasNewImport = true;
                    importedNames.Add(Path.GetFileNameWithoutExtension(destPath));
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[Importer] 解压失败 ({destPath}): {e.Message}\n{e.StackTrace}");
                    if (firstError == null) firstError = $"解压失败: {e.Message}";
                }
            }

            if (importedNames.Count > 0)
            {
                string detail = string.Join(", ", importedNames);
                if (firstError != null)
                    detail += $"\n(部分文件导入失败: {firstError})";
                _onFilePicked?.Invoke(ImportResult.Success, detail);
            }
            else
            {
                _onFilePicked?.Invoke(ImportResult.Error, firstError ?? "未选择任何有效文件");
            }
            yield return null;
#else
            yield return null;
            _onFilePicked?.Invoke(ImportResult.Cancelled, null);
#endif
        }

        /// <summary>
        /// 通过 ContentResolver 把选中文件的流复制到 Songs 目录，返回目标路径（失败抛异常）
        /// </summary>
        private static string CopyUriToSongs(string uriString)
        {
            Debug.Log($"[Importer] 开始处理 URI: {uriString}");

            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                var resolver = activity.Call<AndroidJavaObject>("getContentResolver");

                using (var uriClass = new AndroidJavaClass("android.net.Uri"))
                {
                    var uri = uriClass.CallStatic<AndroidJavaObject>("parse", uriString);
                    var inputStream = resolver.Call<AndroidJavaObject>("openInputStream", uri);

                    string fileName = "imported.osz";
                    try
                    {
                        using (var cursor = resolver.Call<AndroidJavaObject>("query", uri, null, null, null, null))
                        {
                            if (cursor != null && cursor.Call<bool>("moveToFirst"))
                            {
                                int nameIndex = cursor.Call<int>("getColumnIndex", "_display_name");
                                if (nameIndex >= 0)
                                    fileName = cursor.Call<string>("getString", nameIndex);
                            }
                        }
                    }
                    catch (System.Exception queryEx)
                    {
                        Debug.LogWarning($"[Importer] 查询文件名失败，使用默认名: {queryEx.Message}");
                    }

                    if (!fileName.EndsWith(".osz"))
                        fileName += ".osz";

                    // 防御: 个别 provider 返回的 _display_name 可能带路径分隔符等非法字符
                    foreach (char c in Path.GetInvalidFileNameChars())
                        fileName = fileName.Replace(c, '_');

                    string destPath = Path.Combine(SongsDirectory, fileName);
                    Debug.Log($"[Importer] 目标路径: {destPath}");

                    using (var outputStream = new FileStream(destPath, FileMode.Create))
                    {
                        byte[] buffer = new byte[8192];
                        int bytesRead;
                        int totalBytes = 0;
                        while (true)
                        {
                            bytesRead = inputStream.Call<int>("read", buffer);
                            if (bytesRead <= 0) break;
                            outputStream.Write(buffer, 0, bytesRead);
                            totalBytes += bytesRead;
                        }
                        Debug.Log($"[Importer] 文件复制完成: {totalBytes} bytes");
                    }

                    inputStream.Call("close");
                    return destPath;
                }
            }
        }

        // ============================================================
        //  OSZ 解压
        // ============================================================

        public static void ImportNewOszFiles()
        {
            string[] oszFiles = Directory.GetFiles(SongsDirectory, "*.osz");
            if (oszFiles.Length == 0) return;

            Debug.Log($"[Importer] 发现 {oszFiles.Length} 个新 .osz 文件，准备解压...");
            foreach (var oszPath in oszFiles)
                ImportOsz(oszPath);
        }

        private static void ImportOsz(string oszPath)
        {
            try
            {
                string fileName = Path.GetFileNameWithoutExtension(oszPath);
                string targetFolder = Path.Combine(SongsDirectory, fileName);

                if (Directory.Exists(targetFolder))
                {
                    // 重复导入: 目标文件夹已存在，直接清掉 .osz，
                    // 否则它会一直残留在 Songs 里，导致每次启动都被重复扫描
                    try { File.Delete(oszPath); }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"[Importer] 清理重复的 .osz 失败: {e.Message}");
                    }
                    return;
                }

                Debug.Log($"正在解压: {fileName}...");
                ZipFile.ExtractToDirectory(oszPath, targetFolder);
                File.Delete(oszPath);
                Debug.Log($"<color=green>导入成功:</color> {fileName}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"解压失败 {oszPath}: {e.Message}");
                string fileName = Path.GetFileNameWithoutExtension(oszPath);
                string targetFolder = Path.Combine(SongsDirectory, fileName);
                if (Directory.Exists(targetFolder)) Directory.Delete(targetFolder, true);
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
        /// uriList: 选中文件的 URI 列表（多选时以 '\n' 分隔）；取消/失败时为空串
        /// </summary>
        public void OnFilesPicked(string uriList)
        {
            string[] uris = BeatmapImporter.SplitUriList(uriList);
            if (uris.Length == 0)
            {
                Debug.Log("[Importer] 文件选择已取消");
                BeatmapImporter.InvokeCallback(ImportResult.Cancelled, null);
                return;
            }

            Debug.Log($"[Importer] 收到 {uris.Length} 个文件: {string.Join(" | ", uris)}");
            BeatmapImporter.ProcessAndroidResult(uris);
        }
    }
}
