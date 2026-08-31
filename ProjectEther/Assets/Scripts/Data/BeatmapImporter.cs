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
        //  真机（Pico/Quest）上从 VR 切到 2D 选择器再返回时，Activity 可能
        //  重建、Unity 重新初始化，因此「复制文件」由 FilePickerActivity.java
        //  的后台线程直接完成（写入 persistentDataPath/Songs），不依赖
        //  Unity 的恢复时序。本类只负责解压与提示：
        //    1. OpenAndroidFilePicker -> startActivityForResult(1001)
        //    2. Java 复制完成 -> UnitySendMessage("ok:..|err:..") 快通道
        //       + SharedPreferences 慢通道（消息丢失时 C# 主动拉取）
        //    3. HandleImportMessage -> ImportNewOszFiles() 解压 + toast
        // ============================================================

        /// <summary>必须与 FilePickerActivity.java 的 REQUEST_PICK_OSU 一致</summary>
        private const int FilePickerRequestCode = 1001;

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
        /// 格式: ""=取消; "ok:a.osz|b.osz[|err:原因]"=至少一个复制成功; "err:原因"=全部失败
        /// </summary>
        internal static void HandleImportMessage(string message)
        {
            // 消费掉 Java 侧存档，防止 OnApplicationFocus 兜底通道重复处理
            ConsumePendingResult();

            if (string.IsNullOrEmpty(message))
            {
                Debug.Log("[Importer] 文件选择已取消");
                InvokeCallback(ImportResult.Cancelled, null);
                return;
            }

            if (message.StartsWith("ok:"))
            {
                // Java 已把文件复制进 Songs，这里只做解压
                ImportNewOszFiles();
                HasNewImport = true;

                var names = new List<string>();
                string partialError = null;
                foreach (var token in message.Substring(3).Split('|'))
                {
                    if (token.StartsWith("err:"))
                    {
                        partialError = token.Substring(4);
                        continue;
                    }
                    if (token.Length > 0)
                        names.Add(token.EndsWith(".osz") ? token.Substring(0, token.Length - 4) : token);
                }

                string detail = string.Join(", ", names);
                if (partialError != null)
                    detail += $"\n(部分文件导入失败: {partialError})";
                InvokeCallback(ImportResult.Success, detail);
            }
            else if (message.StartsWith("err:"))
            {
                InvokeCallback(ImportResult.Error, message.Substring(4));
            }
            else
            {
                InvokeCallback(ImportResult.Error, message);
            }
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

        /// <summary>取走并清空 Java 侧保存的导入结果（空串表示无）</summary>
        private static string ConsumePendingResult()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                {
                    var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                    using (var cls = new AndroidJavaClass("com.nyaon.projectether.FilePickerActivity"))
                        return cls.CallStatic<string>("consumePendingResult", activity);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Importer] 消费导入结果失败: {e.Message}");
                return "";
            }
#else
            return "";
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
        /// message: ""=取消; "ok:a.osz|b.osz[|err:原因]"=Java 复制完成; "err:原因"=失败
        /// </summary>
        public void OnFilesPicked(string message)
        {
            BeatmapImporter.HandleImportMessage(message);
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
