using System;
using System.IO;
using System.Reflection;
using OsuVR.Storyboard;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class StoryboardDisplayConformance
{
    public static string RunOpacity()
    {
        Directory.CreateDirectory("Temp/StoryboardValidation");
        string imagePath = "Temp/StoryboardValidation/opaque-red-frame.png";
        var image = new Texture2D(1, 1, TextureFormat.RGBA32, false, true);
        image.SetPixel(0, 0, Color.red);
        image.Apply();
        File.WriteAllBytes(imagePath, image.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(image);
        string prefix = "Temp/StoryboardValidation/whole-plane-opacity";
        string report = Run(imagePath, imagePath, prefix, true);
        var rendered = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
        rendered.LoadImage(File.ReadAllBytes(prefix + "-unity-screen.png"));
        Color actual = rendered.GetPixel(640, 360);
        UnityEngine.Object.DestroyImmediate(rendered);
        float setting = OsuVR.SettingsManager.Instance?.Settings.storyboardScreenAlpha ?? 0.5f;
        float opacity = setting * setting; // 沿用现有的视频滑条映射
        float expected = Mathf.LinearToGammaSpace(1 - opacity);
        if (
            Mathf.Abs(actual.r - 1) > 2f / 255
            || Mathf.Abs(actual.g - expected) > 2f / 255
            || Mathf.Abs(actual.b - expected) > 2f / 255
        )
            throw new Exception(
                "Whole-plane opacity failed: "
                    + actual
                    + ", expected (1,"
                    + expected
                    + ","
                    + expected
                    + ")"
            );
        return "PASS whole-frame transparency uses the same RGB/alpha factor as video: "
            + actual
            + Environment.NewLine
            + report;
    }

    public static string Run(
        string unityFrame,
        string lazerFrame,
        string prefix,
        bool whiteBackground = false
    )
    {
        var scene = EditorSceneManager.NewPreviewScene();
        var go = new GameObject("Original storyboard screen validation");
        SceneManager.MoveGameObjectToScene(go, scene);
        var screen = go.AddComponent<HolographicScreenManager>();
        var cameraObject = new GameObject("Display validation camera");
        SceneManager.MoveGameObjectToScene(cameraObject, scene);
        var camera = cameraObject.AddComponent<Camera>();
        camera.scene = scene;
        camera.enabled = false;
        camera.orthographic = true;
        camera.orthographicSize = 4.5f;
        camera.transform.position = new Vector3(0, 2.5f, 0);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = whiteBackground ? Color.white : Color.black;
        camera.stereoTargetEye = StereoTargetEyeMask.None;
        camera.allowHDR = false;
        camera.allowMSAA = false;
        var cameraData = camera.GetUniversalAdditionalCameraData();
        cameraData.renderPostProcessing = false;
        cameraData.allowXRRendering = false;
        var target = new RenderTexture(
            1280,
            720,
            24,
            RenderTextureFormat.ARGB32,
            RenderTextureReadWrite.sRGB
        );
        target.Create();
        var actualSource = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
        var referenceSource = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
        actualSource.LoadImage(File.ReadAllBytes(unityFrame));
        referenceSource.LoadImage(File.ReadAllBytes(lazerFrame));
        var input = new RenderTexture(
            actualSource.width,
            actualSource.height,
            0,
            RenderTextureFormat.ARGB32,
            RenderTextureReadWrite.Linear
        );
        input.Create();
        Texture2D actual = null,
            reference = null;
        try
        {
            foreach (var pixel in actualSource.GetPixels32())
                if (pixel.a != 255)
                    throw new Exception(
                        "Use an opaque storyboard frame for the final-display comparison."
                    );
            screen.Setup(new MediaAssetScanner.ScanResult { HasStoryboard = true }, "");
            screen.SetRenderTexture(input);
            var display = go.GetComponentInChildren<MeshRenderer>();
            if (display == null)
                throw new Exception("Original screen is hidden by current settings");
            var mesh = display.GetComponent<MeshFilter>().sharedMesh;
            if (
                Mathf.Abs(mesh.bounds.size.x - 12) > 0.001f
                || Mathf.Abs(mesh.bounds.size.y - 8) > 0.001f
                || Mathf.Abs(mesh.bounds.size.z - 0.36f) > 0.001f
            )
                throw new Exception("Original 12x8 curved screen geometry changed");
            float alpha = display.sharedMaterial.GetFloat("_ScreenAlpha");
            bool srgb = GL.sRGBWrite;
            GL.sRGBWrite = false;
            Graphics.Blit(actualSource, input);
            GL.sRGBWrite = srgb;
            RenderPipeline.SubmitRenderRequest(
                camera,
                new UniversalRenderPipeline.SingleCameraRequest { destination = target }
            );
            actual = StoryboardRenderConformance.Read(target);
            GL.sRGBWrite = false;
            Graphics.Blit(referenceSource, input);
            GL.sRGBWrite = srgb;
            RenderPipeline.SubmitRenderRequest(
                camera,
                new UniversalRenderPipeline.SingleCameraRequest { destination = target }
            );
            reference = StoryboardRenderConformance.Read(target);
            File.WriteAllBytes(prefix + "-unity-screen.png", actual.EncodeToPNG());
            File.WriteAllBytes(prefix + "-lazer-screen.png", reference.EncodeToPNG());
            var a = actual.GetPixels32();
            var b = reference.GetPixels32();
            int changed = 0,
                max = 0;
            long sum = 0;
            int nonBlack = 0;
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i].r + a[i].g + a[i].b > 0)
                    nonBlack++;
                int dr = Math.Abs(a[i].r - b[i].r),
                    dg = Math.Abs(a[i].g - b[i].g),
                    db = Math.Abs(a[i].b - b[i].b);
                int d = Math.Max(dr, Math.Max(dg, db));
                if (d > 0)
                    changed++;
                max = Math.Max(max, d);
                sum += dr + dg + db;
            }
            if (nonBlack < 10000)
                throw new Exception("Display capture did not render the screen");
            string report =
                "Original curved screen 12x8, depth0.36, alpha="
                + alpha
                + ", source wired through HolographicScreenManager.SetRenderTexture; pixels="
                + a.Length
                + ", nonBlack="
                + nonBlack
                + ", different="
                + changed
                + ", maxChannelDelta="
                + max
                + ", meanChannelDelta="
                + (sum / (double)(a.Length * 3));
            File.WriteAllText(prefix + "-screen-comparison.txt", report);
            return report;
        }
        finally
        {
            var fields = typeof(HolographicScreenManager).GetFields(
                BindingFlags.Instance | BindingFlags.NonPublic
            );
            foreach (var field in fields)
                if (
                    field.FieldType == typeof(Material)
                    || field.FieldType == typeof(Texture2D)
                    || field.FieldType == typeof(Mesh)
                )
                {
                    var value = field.GetValue(screen) as UnityEngine.Object;
                    field.SetValue(screen, null);
                    if (value != null)
                        UnityEngine.Object.DestroyImmediate(value);
                }
            if (actual != null)
                UnityEngine.Object.DestroyImmediate(actual);
            if (reference != null)
                UnityEngine.Object.DestroyImmediate(reference);
            UnityEngine.Object.DestroyImmediate(actualSource);
            UnityEngine.Object.DestroyImmediate(referenceSource);
            input.Release();
            UnityEngine.Object.DestroyImmediate(input);
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(cameraObject);
            UnityEngine.Object.DestroyImmediate(go);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }
}
