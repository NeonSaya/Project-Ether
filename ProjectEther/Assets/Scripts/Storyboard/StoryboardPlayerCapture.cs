#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace OsuVR.Storyboard
{
    /// <summary>按需启用的开发版播放器截图功能；不影响普通游戏的启动流程。</summary>
    public sealed class StoryboardPlayerCapture : MonoBehaviour
    {
        string mapPath,
            outputPath;
        double sampleTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartIfRequested()
        {
            string[] args = Environment.GetCommandLineArgs();
            int flag = Array.IndexOf(args, "--storyboard-capture");
            if (flag < 0 || flag + 3 >= args.Length)
                return;
            var go = new GameObject("Storyboard development capture");
            DontDestroyOnLoad(go);
            var capture = go.AddComponent<StoryboardPlayerCapture>();
            capture.mapPath = args[flag + 1];
            capture.sampleTime = double.Parse(args[flag + 2], CultureInfo.InvariantCulture);
            capture.outputPath = args[flag + 3];
        }

        IEnumerator Start()
        {
            // 等待渲染管线和常规运行时单例完成初始化。
            yield return null;
            yield return null;
            int exitCode = 0;
            var previous = RenderTexture.active;
            Texture2D image = null;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath)));
                var renderer = StoryboardRenderer.Instance;
                if (renderer == null)
                    throw new InvalidOperationException("Storyboard renderer did not initialise");
                renderer.enabled = false;
                renderer.LoadStoryboard(
                    StoryboardParser.ParseFile(mapPath),
                    Path.GetDirectoryName(mapPath),
                    true
                );
                renderer.RenderAtTime(sampleTime);
                var rt = renderer.GetRenderTexture();
                RenderTexture.active = rt;
                image = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false, true);
                image.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                image.Apply();
                File.WriteAllBytes(outputPath, image.EncodeToPNG());
                string report =
                    "PASS player capture: "
                    + SystemInfo.graphicsDeviceType
                    + "; "
                    + rt.width
                    + "x"
                    + rt.height
                    + "; time="
                    + sampleTime;
                File.WriteAllText(outputPath + ".txt", report);
                Debug.Log(report);
                renderer.UnloadAll();
            }
            catch (Exception e)
            {
                exitCode = 1;
                File.WriteAllText(outputPath + ".txt", "FAILED: " + e);
                Debug.LogException(e);
            }
            finally
            {
                RenderTexture.active = previous;
                if (image != null)
                    Destroy(image);
            }
            Application.Quit(exitCode);
        }
    }
}
#endif
