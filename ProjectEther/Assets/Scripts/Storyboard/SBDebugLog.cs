using System.IO;
using UnityEngine;

namespace OsuVR.Storyboard
{
    /// <summary>
    /// Storyboard 诊断日志：写入文件，即使 Unity 卡死也能查看
    /// </summary>
    public static class SBDebugLog
    {
        static string _logPath;
        static StreamWriter _writer;

        public static void Begin()
        {
            try
            {
                _logPath = Path.Combine(Application.persistentDataPath, "sb_debug.log");
                _writer = new StreamWriter(_logPath, false);
                _writer.AutoFlush = true;
                _writer.WriteLine($"=== SB Debug Log ===");
                _writer.WriteLine($"Time: {System.DateTime.Now}");
                _writer.WriteLine($"Memory: {System.GC.GetTotalMemory(false) / 1048576}MB");
                _writer.WriteLine();
            }
            catch { }
        }

        public static void Log(string msg)
        {
            try
            {
                Debug.Log(msg);
                _writer?.WriteLine($"[{System.DateTime.Now:HH:mm:ss.fff}] {msg}");
            }
            catch { }
        }

        public static void Mem(string label)
        {
            long mem = System.GC.GetTotalMemory(false);
            Log($"[MEM] {label}: {mem / 1048576}MB ({mem:N0} bytes)");
        }

        public static void End()
        {
            try
            {
                _writer?.Flush();
                _writer?.Close();
                _writer = null;
            }
            catch { }
        }
    }
}
