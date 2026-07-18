using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.XR;

namespace OsuVR
{
    public class FrameRateUnlocker : MonoBehaviour
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        [DllImport("user32.dll")]
        static extern IntPtr GetActiveWindow();
        [DllImport("user32.dll")]
        static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")]
        static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        const int GWL_STYLE = -16;
        const int WS_MAXIMIZEBOX = 0x00010000;
        const int WS_THICKFRAME = 0x00040000; // 可拖拽缩放边框

        static void MakeWindowResizable()
        {
            IntPtr hwnd = GetActiveWindow();
            if (hwnd == IntPtr.Zero) return;
            int style = GetWindowLong(hwnd, GWL_STYLE);
            style |= WS_THICKFRAME | WS_MAXIMIZEBOX;
            SetWindowLong(hwnd, GWL_STYLE, style);
        }
#endif

        [Header("设置")]
        [Tooltip("是否尝试强制开启 120Hz (如果设备支持)")]
        public bool tryForce120Hz = true;

        [Tooltip("PCVR 目标帧率 (-1 表示不限制，完全交给 SteamVR/Oculus 运行时)")]
        public int pcTargetFrameRate = -1;

        private static FrameRateUnlocker _instance;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                // 如果已经有一个实例存在，说明我是多余的（比如从游戏退回菜单时重新加载出来的那个）
                // 直接销毁我自己
                Destroy(this.gameObject);
                return;
            }

            //如果没有实例，那我就是老大
            _instance = this;
            DontDestroyOnLoad(this.gameObject);
        }

        IEnumerator Start()
        {
            // 1. 关闭 Unity 内部 VSync (VR SDK 自带同步，Unity 的会导致冲突)
            QualitySettings.vSyncCount = 0;

            // 减少输入延迟
            QualitySettings.maxQueuedFrames = 1;

            if (Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor)
            {
                // PC 模式: 窗口化 + 可缩放 + 可最大化
                Screen.fullScreenMode = FullScreenMode.Windowed;
                Application.targetFrameRate = pcTargetFrameRate;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
                // 等一帧让窗口创建完成，然后修改窗口样式
                yield return null;
                MakeWindowResizable();
#endif
                Debug.Log($"[FrameRate] PC Mode: Windowed (resizable), Target: {pcTargetFrameRate}, VSync: 0");
            }
            else if (Application.platform == RuntimePlatform.Android)
            {
                // 一体机模式 (Quest/PICO)
                // 不锁帧，交给 AttemptUnlockRate 跑满设备最高刷新率
                Application.targetFrameRate = -1;

                // 等待 XR 系统完全初始化
                yield return new WaitForSeconds(1.0f);

                AttemptUnlockRate();
            }
        }

        private void AttemptUnlockRate()
        {
            var displays = new List<XRDisplaySubsystem>();
            SubsystemManager.GetSubsystems(displays);

            if (displays.Count == 0)
            {
                Debug.LogWarning("[FrameRate] No XR Display found!");
                return;
            }

            var display = displays[0];

            // 目标帧率优先级
            float[] targets = tryForce120Hz ? new float[] { 120f, 90f, 80f, 72f } : new float[] { 90f, 80f, 72f };

            // =========================================================
            // 方案 A: 使用 Unity 2022+ 标准 API (如果有)
            // =========================================================
            // 注意：为了防止编译报错，我们这里不直接调用可能不存在的 API
            // 而是检查该实例是否直接支持该方法 (动态调用)
            // =========================================================

            bool success = false;

            // 尝试获取可用刷新率列表
            // 某些版本的 Unity API 是 GetRefreshRates，有的是 TryGet...
            // 这里我们直接尝试"盲设"，这是最简单有效的方法

            foreach (float targetRate in targets)
            {
                // 1. 尝试调用 TrySetDisplayRefreshRate (标准 API)
                // 这里的关键是：如果你直接写 display.TrySetDisplayRefreshRate(targetRate) 报错
                // 说明你的 XR Plugin 包版本较旧或使用了不同的实现。
                // 我们尝试用 C# 反射来调用它，这样代码永远不会报错。

                if (CallTrySetRateReflectively(display, targetRate))
                {
                    Debug.Log($"<color=green>[FrameRate] Success (XR API)! Locked to {targetRate}Hz</color>");
                    Application.targetFrameRate = (int)targetRate;
                    success = true;
                    break;
                }
            }

            // =========================================================
            // 方案 B: Android 原生调用 (Quest 专用兜底)
            // =========================================================
            if (!success && Application.platform == RuntimePlatform.Android)
            {
                Debug.Log("[FrameRate] XR API failed, trying Oculus Android Native...");
                foreach (float targetRate in targets)
                {
                    if (SetRateViaOculusAndroid(targetRate))
                    {
                        Debug.Log($"<color=green>[FrameRate] Success (Android Native)! Locked to {targetRate}Hz</color>");
                        Application.targetFrameRate = (int)targetRate;
                        success = true;
                        break;
                    }
                }
            }

            if (!success)
            {
                Debug.LogWarning("[FrameRate] Failed to set high refresh rate. Running at system default.");
            }
        }

        /// <summary>
        /// 使用反射调用 TrySetDisplayRefreshRate，避免编译错误
        /// </summary>
        private bool CallTrySetRateReflectively(XRDisplaySubsystem display, float rate)
        {
            try
            {
                var type = display.GetType();
                var method = type.GetMethod("TrySetDisplayRefreshRate");

                if (method != null)
                {
                    // 调用: bool TrySetDisplayRefreshRate(float rate)
                    object result = method.Invoke(display, new object[] { rate });
                    return (bool)result;
                }
            }
            catch
            {
                // 忽略反射错误
            }
            return false;
        }

        /// <summary>
        /// 针对 Oculus Quest/Quest 2/Pro/3 的底层 Android 调用
        /// 这是最强力的修改方式，直接绕过 Unity XR SDK
        /// </summary>
        private bool SetRateViaOculusAndroid(float rate)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var window = activity.Call<AndroidJavaObject>("getWindow"))
                using (var display = window.Call<AndroidJavaObject>("getWindowManager").Call<AndroidJavaObject>("getDefaultDisplay"))
                {
                    var modes = display.Call<AndroidJavaObject[]>("getSupportedModes");
                    
                    if (modes == null) return false;

                    foreach (var mode in modes)
                    {
                        float modeRate = mode.Call<float>("getRefreshRate");
                        // 允许 0.5 的误差 (例如 90.0 vs 89.9)
                        if (Mathf.Abs(modeRate - rate) < 0.5f)
                        {
                            int modeId = mode.Call<int>("getModeId");
                            
                            using (var layoutParams = window.Call<AndroidJavaObject>("getAttributes"))
                            {
                                layoutParams.Set("preferredDisplayModeId", modeId);
                                window.Call("setAttributes", layoutParams);
                            }
                            return true;
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                // 静默失败，可能是非 Quest 设备
            }
#endif
            return false;
        }
    }
}