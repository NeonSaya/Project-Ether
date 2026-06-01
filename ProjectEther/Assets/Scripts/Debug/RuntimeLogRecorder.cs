using System;
using System.IO;
using UnityEngine;

namespace OsuVR
{
    /// <summary>
    /// 全局日志记录器：捕获所有 Error/Exception 写入文件，用于 Android 端调试
    /// 挂载在首个场景的空物体上，自动 DontDestroyOnLoad
    /// </summary>
    public class RuntimeLogRecorder : MonoBehaviour
    {
        private string logPath;
        private static readonly object fileLock = new object();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoCreate()
        {
            if (FindObjectOfType<RuntimeLogRecorder>() != null) return;
            var go = new GameObject("[RuntimeLogRecorder]");
            go.AddComponent<RuntimeLogRecorder>();
        }

        void Awake()
        {
            DontDestroyOnLoad(gameObject);
            logPath = Application.persistentDataPath + "/ProjectEther_CrashLog.txt";

            try
            {
                using (var writer = new StreamWriter(logPath, true))
                {
                    writer.WriteLine("");
                    writer.WriteLine($"--- New Session [{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ---");
                    writer.WriteLine($"[Platform] {Application.platform}, [Device] {SystemInfo.deviceModel}, [GPU] {SystemInfo.graphicsDeviceName}");
                    writer.Flush();
                }
                Debug.Log($"[LogRecorder] 日志路径: {logPath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[LogRecorder] 初始化失败: {e.Message}");
            }
        }

        void OnEnable()
        {
            Application.logMessageReceivedThreaded += HandleLog;
        }

        void OnDisable()
        {
            Application.logMessageReceivedThreaded -= HandleLog;
        }

        private void HandleLog(string logString, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception)
                return;

            try
            {
                lock (fileLock)
                {
                    using (var writer = new StreamWriter(logPath, true))
                    {
                        writer.WriteLine($"[{DateTime.Now:HH:mm:ss}] [{type}] {logString}");
                        if (!string.IsNullOrEmpty(stackTrace))
                            writer.WriteLine(stackTrace);
                        writer.WriteLine("");
                        writer.Flush();
                    }
                }
            }
            catch
            {
                // 写日志本身不能再抛异常
            }
        }
    }
}
