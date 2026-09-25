using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using OsuVR.Storyboard;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>针对生产渲染的 GPU 一致性探针，与 VR 显示处理无关。</summary>
public static class StoryboardRenderConformance
{
    [MenuItem("Tools/Storyboard/Run GPU conformance")]
    public static void RunMenu() => Debug.Log(Run());

    public static string Run()
    {
        if (StoryboardRenderer.Instance != null)
            throw new InvalidOperationException("Run in Edit Mode with no active storyboard renderer.");
        string directory = Path.GetFullPath("Temp/StoryboardValidation");
        Directory.CreateDirectory(directory);
        SaveTexture(directory, "white.png", 1, 1, new[] { Color.white });
        SaveTexture(directory, "red.png", 1, 1, new[] { Color.red });
        SaveTexture(directory, "blue.png", 1, 1, new[] { Color.blue });
        SaveTexture(directory, "grey.png", 1, 1, new[] { new Color(0.5f, 0.5f, 0.5f, 1) });
        SaveTexture(directory, "pair.png", 2, 1, new[] { Color.red, Color.blue });
        SaveTexture(directory, "frame0.png", 1, 1, new[] { Color.red });
        SaveTexture(directory, "frame1.png", 1, 2, new[] { Color.blue, Color.blue });
        SaveTexture(directory, "frame2.png", 2, 1, new[] { Color.green, Color.green });
        var scene = EditorSceneManager.NewPreviewScene();
        var go = new GameObject("Storyboard conformance");
        SceneManager.MoveGameObjectToScene(go, scene);
        var renderer = go.AddComponent<StoryboardRenderer>();
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        if (StoryboardRenderer.Instance == null)
            typeof(StoryboardRenderer).GetMethod("Awake", flags).Invoke(renderer, null);
        var results = new List<string>();
        try
        {
            Check(renderer, directory, results, "source gamma bytes", new[] {
                "Sprite,Foreground,Centre,grey.png,320,240", " F,0,0,1000,1", " S,0,0,1000,100" }, true,
                960, 540, new Color(128f/255,128f/255,128f/255,1));
            Check(renderer, directory, results, "source alpha coverage", new[] {
                "Sprite,Foreground,Centre,red.png,320,240", " F,0,0,1000,0.5", " S,0,0,1000,100" }, true,
                960, 540, new Color(0.5f,0,0,1));
            Check(renderer, directory, results, "additive under alpha", new[] {
                "Sprite,Foreground,Centre,red.png,320,240", " F,0,0,1000,1", " S,0,0,1000,100", " P,0,0,1000,A",
                "Sprite,Foreground,Centre,blue.png,320,240", " F,0,0,1000,0.5", " S,0,0,1000,100" }, true,
                960, 540, new Color(0.5f,0,0.5f,1));
            Check(renderer, directory, results, "additive over alpha", new[] {
                "Sprite,Foreground,Centre,blue.png,320,240", " F,0,0,1000,0.5", " S,0,0,1000,100",
                "Sprite,Foreground,Centre,red.png,320,240", " F,0,0,1000,1", " S,0,0,1000,100", " P,0,0,1000,A" }, true,
                960, 540, new Color(1,0,0.5f,1));
            Check(renderer, directory, results, "passing layer only", new[] {
                "Sprite,Pass,Centre,blue.png,320,240", " F,0,0,1000,1", " S,0,0,1000,100",
                "Sprite,Fail,Centre,red.png,320,240", " F,0,0,1000,1", " S,0,0,1000,100" }, true,
                960, 540, Color.blue);
            Check(renderer, directory, results, "alpha overflow flicker", new[] {
                "Sprite,Foreground,Centre,red.png,320,240", " F,0,0,1000,1.25", " S,0,0,1000,100" }, true,
                960, 540, new Color(0.25f,0,0,1));
            Check(renderer, directory, results, "colour interpolation", new[] {
                "Sprite,Foreground,Centre,white.png,320,240", " F,0,0,1000,1", " S,0,0,1000,100", " C,0,0,1000,0,0,0,255,255,255" }, true,
                960, 540, new Color(0.7353569f,0.7353569f,0.7353569f,1));
            Check(renderer, directory, results, "4:3 clipping", new[] {
                "Sprite,Foreground,Centre,red.png,-40,240", " F,0,0,1000,1", " S,0,0,1000,50" }, false,
                150, 540, Color.black);
            Check(renderer, directory, results, "widescreen original coordinates", new[] {
                "Sprite,Foreground,Centre,red.png,-40,240", " F,0,0,1000,1", " S,0,0,1000,50" }, true,
                150, 540, Color.red);
            Check(renderer, directory, results, "clockwise rotation at top-left", new[] {
                "Sprite,Foreground,TopLeft,white.png,320,240", " F,0,0,1000,1", " V,0,0,1000,80,40", " R,0,0,1000,1.57079632679" }, true,
                915, 450, Color.white);
            Check(renderer, directory, results, "horizontal flip retains anchor", new[] {
                "Sprite,Foreground,TopLeft,pair.png,320,240", " F,0,0,1000,1", " V,0,0,1000,40,40", " P,0,0,1000,H" }, true,
                985, 500, Color.blue);
            Check(renderer, directory, results, "negative vector scale flips once", new[] {
                "Sprite,Foreground,TopLeft,pair.png,320,240", " F,0,0,1000,1", " V,0,0,1000,-40,40" }, true,
                985, 500, Color.blue);
            Check(renderer, directory, results, "flip cancels negative vector scale", new[] {
                "Sprite,Foreground,TopLeft,pair.png,320,240", " F,0,0,1000,1", " V,0,0,1000,-40,40", " P,0,0,1000,H" }, true,
                985, 500, Color.red);
            Check(renderer, directory, results, "LoopOnce holds last frame and its dimensions", new[] {
                "Animation,Foreground,Centre,frame.png,320,240,3,125,LoopOnce", " F,0,0,1000,1", " S,0,0,1000,50" }, true,
                1040, 540, Color.green);
            Check(renderer, directory, results, "LoopForever advances declared frame sequence", new[] {
                "Animation,Foreground,Centre,frame.png,320,240,3,125,LoopForever", " F,0,0,1000,1", " S,0,0,1000,50" }, true,
                960, 540, Color.blue);
            var many = new List<string>();
            for (int i = 0; i < 8193; i++)
            {
                many.Add("Sprite,Foreground,Centre," + (i == 8192 ? "blue.png" : "red.png") + ",320,240");
                many.Add(" F,0,0,1000," + (i == 8192 ? "1" : "0"));
                many.Add(" S,0,0,1000,100");
            }
            Check(renderer, directory, results, "sprite beyond former 8192 cap", many.ToArray(), true, 960,540,Color.blue);
            renderer.LoadStoryboard(StoryboardParser.Parse(new List<string>{
                "Sprite,Foreground,Centre,red.png,320,240", " F,0,0,1000,0.5", " S,0,0,1000,100"
            }),directory,true);
            var blue=new Texture2D(1,1,TextureFormat.RGBA32,false,true);
            blue.SetPixel(0,0,Color.blue);blue.Apply();
            renderer.SetUnderlay(blue,true);
            renderer.RenderAtTime(500);
            AssertPixel(renderer.GetRenderTexture(),960,540,new Color(0.5f,0,0.5f,1),"background blends before scene conversion");
            results.Add("PASS background blends before scene conversion");
            renderer.LoadStoryboard(StoryboardParser.Parse(new List<string>{
                "Sprite,Foreground,Centre,white.png,320,240", " F,0,0,1000,1", " S,0,0,1000,100", " P,0,0,1000,A",
                "Sprite,Foreground,Centre,white.png,320,240", " F,0,0,1000,0.5", " C,0,0,1000,0,0,0", " S,0,0,1000,100"
            }),directory,true);
            renderer.SetUnderlay(blue,true);
            renderer.RenderAtTime(500);
            AssertPixel(renderer.GetRenderTexture(),960,540,new Color(0.5f,0.5f,0.5f,1),"additive clamps with backdrop before later alpha");
            results.Add("PASS additive clamps with backdrop before later alpha");
            renderer.UnloadAll();
            UnityEngine.Object.DestroyImmediate(blue);
            AssertPixel(renderer.GetRenderTexture(), 960, 540, Color.clear, "unload clears prior frame");
            results.Add("PASS unload clears prior frame");
            string report = string.Join(Environment.NewLine, results);
            File.WriteAllText(Path.Combine(directory, "gpu-results.txt"), report);
            return report;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    public static string CaptureAndCompare(string inputPath, string assetDirectory, double time, string referencePng, string outputPrefix, bool widescreen = true)
    {
        if (StoryboardRenderer.Instance != null) throw new InvalidOperationException("Capture requires an idle Edit Mode editor.");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPrefix)));
        var scene = EditorSceneManager.NewPreviewScene();
        var go = new GameObject("Storyboard comparison");
        SceneManager.MoveGameObjectToScene(go, scene);
        var renderer = go.AddComponent<StoryboardRenderer>();
        if (StoryboardRenderer.Instance == null)
            typeof(StoryboardRenderer).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(renderer, null);
        Texture2D actual = null, reference = null, heatmap = null;
        try
        {
            renderer.LoadStoryboard(StoryboardParser.ParseFile(inputPath), assetDirectory, widescreen);
            renderer.RenderAtTime(time);
            actual = Read(renderer.GetRenderTexture());
            File.WriteAllBytes(outputPrefix + ".png", actual.EncodeToPNG());
            reference = new Texture2D(2,2,TextureFormat.RGBA32,false,true);
            reference.LoadImage(File.ReadAllBytes(referencePng));
            if (actual.width != reference.width || actual.height != reference.height) throw new Exception("Reference dimensions differ");
            Color32[] a = actual.GetPixels32(), b = reference.GetPixels32();
            var diff = new Color32[a.Length];
            long total = 0; int different = 0, aboveTwo = 0, maximum = 0;
            for (int i=0; i<a.Length; i++)
            {
                int dr=Math.Abs(a[i].r-b[i].r), dg=Math.Abs(a[i].g-b[i].g), db=Math.Abs(a[i].b-b[i].b);
                int d=Math.Max(dr,Math.Max(dg,db));
                total+=dr+dg+db;
                if(d>0) different++;
                if(d>2) aboveTwo++;
                maximum=Math.Max(maximum,d);
                diff[i]=new Color32((byte)Math.Min(255,dr*8),(byte)Math.Min(255,dg*8),(byte)Math.Min(255,db*8),255);
            }
            heatmap=new Texture2D(actual.width,actual.height,TextureFormat.RGBA32,false,true);
            heatmap.SetPixels32(diff);heatmap.Apply();
            File.WriteAllBytes(outputPrefix+"-difference-x8.png",heatmap.EncodeToPNG());
            string report="Time="+time+"ms; pixels="+a.Length+"; different="+different+"; over2="+aboveTwo+
                "; maxChannelDelta="+maximum+"; meanChannelDelta="+(total/(double)(a.Length*3)).ToString("G8",System.Globalization.CultureInfo.InvariantCulture);
            File.WriteAllText(outputPrefix+"-comparison.txt",report);
            return report;
        }
        finally
        {
            if(actual!=null) UnityEngine.Object.DestroyImmediate(actual);
            if(reference!=null) UnityEngine.Object.DestroyImmediate(reference);
            if(heatmap!=null) UnityEngine.Object.DestroyImmediate(heatmap);
            UnityEngine.Object.DestroyImmediate(go);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    static void Check(StoryboardRenderer renderer, string directory, List<string> results,
        string name, string[] lines, bool wide, int x, int y, Color expected)
    {
        renderer.LoadStoryboard(StoryboardParser.Parse(new List<string>(lines)), directory, wide);
        renderer.RenderAtTime(500);
        var frame = Read(renderer.GetRenderTexture());
        try
        {
            Color actual = frame.GetPixel(x, y);
            AssertNear(actual, expected, name);
            File.WriteAllBytes(Path.Combine(directory, name.Replace(' ', '-').Replace(':', '-') + ".png"), frame.EncodeToPNG());
            results.Add("PASS " + name + ": " + actual);
        }
        finally { UnityEngine.Object.DestroyImmediate(frame); }
    }
    static void AssertPixel(RenderTexture rt, int x, int y, Color expected, string name)
    {
        var image = Read(rt);
        try { AssertNear(image.GetPixel(x,y), expected, name); }
        finally { UnityEngine.Object.DestroyImmediate(image); }
    }
    static void AssertNear(Color actual, Color expected, string name)
    {
        const float tolerance = 2f / 255;
        if (Mathf.Abs(actual.r-expected.r)>tolerance || Mathf.Abs(actual.g-expected.g)>tolerance ||
            Mathf.Abs(actual.b-expected.b)>tolerance || Mathf.Abs(actual.a-expected.a)>tolerance)
            throw new Exception(name + ": expected " + expected + ", got " + actual);
    }
    public static Texture2D Read(RenderTexture rt)
    {
        var previous = RenderTexture.active;
        try
        {
            RenderTexture.active = rt;
            var image = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false, true);
            image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);
            image.Apply();
            return image;
        }
        finally { RenderTexture.active = previous; }
    }
    static void SaveTexture(string directory, string name, int width, int height, Color[] pixels)
    {
        var texture = new Texture2D(width,height,TextureFormat.RGBA32,false,true);
        texture.SetPixels(pixels); texture.Apply();
        File.WriteAllBytes(Path.Combine(directory,name),texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
    }
}
