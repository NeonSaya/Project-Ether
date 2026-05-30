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
        // ============================================================

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
                        activity.Call("startActivityForResult", intent, 1001);
                    }
                }

                Debug.Log("[Importer] Android 文件选择器已打开");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Importer] Android 文件选择器启动失败: {e.Message}");
                _onFilePicked?.Invoke(ImportResult.Error, e.Message);
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

        internal static void ProcessAndroidResult(string uriString)
        {
            var helper = GetOrCreateHelper();
            helper.StartCoroutine(ProcessAndroidUriCoroutine(uriString));
        }

        private static BeatmapImporterHelper GetOrCreateHelper()
        {
            if (_helper == null)
            {
                var go = new GameObject("[BeatmapImporterHelper]");
                _helper = go.AddComponent<BeatmapImporterHelper>();
                Object.DontDestroyOnLoad(go);
            }
            return _helper;
        }

        private static System.Collections.IEnumerator ProcessAndroidUriCoroutine(string uriString)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            string destPath = null;
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                {
                    var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                    var resolver = activity.Call<AndroidJavaObject>("getContentResolver");

                    using (var uriClass = new AndroidJavaClass("android.net.Uri"))
                    {
                        var uri = uriClass.CallStatic<AndroidJavaObject>("parse", uriString);
                        var inputStream = resolver.Call<AndroidJavaObject>("openInputStream", uri);

                        string fileName = "imported.osz";
                        using (var cursor = resolver.Call<AndroidJavaObject>("query", uri, null, null, null, null))
                        {
                            if (cursor != null && cursor.Call<bool>("moveToFirst"))
                            {
                                int nameIndex = cursor.Call<int>("getColumnIndex", "_display_name");
                                if (nameIndex >= 0)
                                    fileName = cursor.Call<string>("getString", nameIndex);
                            }
                        }

                        if (!fileName.EndsWith(".osz"))
                            fileName += ".osz";

                        destPath = Path.Combine(SongsDirectory, fileName);
                        using (var outputStream = new FileStream(destPath, FileMode.Create))
                        {
                            byte[] buffer = new byte[8192];
                            int bytesRead;
                            while (true)
                            {
                                bytesRead = inputStream.Call<int>("read", buffer);
                                if (bytesRead <= 0) break;
                                outputStream.Write(buffer, 0, bytesRead);
                            }
                        }

                        inputStream.Call("close");
                    }
                }

                Debug.Log($"[Importer] 文件已复制到: {destPath}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Importer] 复制文件失败: {e.Message}");
            }

            if (!string.IsNullOrEmpty(destPath) && File.Exists(destPath))
            {
                ImportOsz(destPath);
                HasNewImport = true;
                string fileName = Path.GetFileNameWithoutExtension(destPath);
                _onFilePicked?.Invoke(ImportResult.Success, fileName);
            }
            else
            {
                _onFilePicked?.Invoke(ImportResult.Error, "文件复制失败");
            }
#else
            yield return null;
            _onFilePicked?.Invoke(ImportResult.Cancelled, null);
#endif
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

                if (Directory.Exists(targetFolder)) return;

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
    /// Android 回调辅助 MonoBehaviour (由 UnitySendMessage 调用)
    /// </summary>
    public class BeatmapImporterHelper : MonoBehaviour
    {
        public void OnFilePicked(string uriString)
        {
            if (string.IsNullOrEmpty(uriString))
            {
                Debug.Log("[Importer] 文件选择已取消");
                BeatmapImporter.InvokeCallback(ImportResult.Cancelled, null);
                return;
            }

            Debug.Log($"[Importer] 收到文件: {uriString}");
            BeatmapImporter.ProcessAndroidResult(uriString);
        }
    }
}
