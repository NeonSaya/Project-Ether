using System;
using System.Collections.Generic;
using System.IO;
using OsuVR.Storyboard;
using OsuVR.Storyboard.Engine;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

/// <summary>将生产环境的 Burst 输出与已安装的 osu!lazer 导出的采样进行比较。</summary>
public static class StoryboardTimelineConformance
{
    [Serializable]
    public class Oracle
    {
        public string input,
            gameVersion,
            frameworkVersion;
        public Sprite[] sprites;
    }

    [Serializable]
    public class Sprite
    {
        public int index;
        public string layer,
            path;
        public double startTime,
            endTimeForDisplay;
        public Sample[] samples;
    }

    [Serializable]
    public class XY
    {
        public float x,
            y;
    }

    [Serializable]
    public class RGB
    {
        public float r,
            g,
            b,
            a;
    }

    [Serializable]
    public class Blend
    {
        public string destination;
    }

    [Serializable]
    public class Sample
    {
        public double time;
        public XY position,
            scale,
            vectorScale;
        public float rotationDegrees,
            alphaBeforeUpdate,
            alpha;
        public RGB colour;
        public bool flipH,
            flipV;
        public Blend blending;
    }

    [UnityEditor.MenuItem("Tools/Storyboard/Run lazer timeline conformance")]
    public static void RunMenu()
    {
        string root = Path.GetFullPath("../Tools/StoryboardReference/expected");
        Debug.Log(Compare(Path.Combine(root, "parity.json")));
        Debug.Log(Compare(Path.Combine(root, "easing.json")));
        Debug.Log(Compare(Path.Combine(root, "mixed-timelines.json")));
        Debug.Log(StoryboardParserConformance.Run());
    }

    public static string Compare(string oraclePath, string inputPath = null)
    {
        var oracle = JsonUtility.FromJson<Oracle>(File.ReadAllText(oraclePath));
        string source =
            inputPath
            ?? (
                Path.IsPathRooted(oracle.input)
                    ? oracle.input
                    : Path.Combine(Path.GetDirectoryName(oraclePath), oracle.input)
            );
        var storyboard = StoryboardParser.ParseFile(source);
        var textures = new Dictionary<string, int>();
        var elements = new List<OsuVR.Storyboard.Data.SBElement>(
            storyboard.GetAllElementsInRenderOrder()
        );
        foreach (var e in elements)
        {
            string key = e.ImagePath.Replace((char)92, '/').ToLowerInvariant();
            if (!textures.ContainsKey(key))
                textures[key] = textures.Count;
        }
        var sizes = new Vector2Int[textures.Count];
        for (int i = 0; i < sizes.Length; i++)
            sizes[i] = new Vector2Int(1, 1);
        var timeline = SBTimelineFlattener.Flatten(storyboard, textures, sizes);
        var output = new NativeArray<SpriteInputData>(timeline.SpriteCount, Allocator.TempJob);
        var failures = new List<string>();
        int samples = 0,
            comparisons = 0;
        try
        {
            foreach (var expected in oracle.sprites)
            {
                if (expected.layer == "Fail")
                    continue;
                int spriteIndex = elements.FindIndex(e => e.ImagePath == expected.path);
                if (spriteIndex < 0)
                {
                    failures.Add("Missing sprite " + expected.path);
                    continue;
                }
                foreach (var sample in expected.samples)
                {
                    if (
                        sample.time < expected.startTime
                        || sample.time >= expected.endTimeForDisplay
                    )
                        continue;
                    var job = new SBEvaluateTimelineJob
                    {
                        Sprites = timeline.Sprites,
                        Commands = timeline.Commands,
                        Loops = timeline.Loops,
                        FrameMap = timeline.FrameMap,
                        TriggerCommands = timeline.TriggerCommands,
                        Output = output,
                        CurrentTime = sample.time,
                        SpriteCount = timeline.SpriteCount,
                    };
                    job.Schedule(timeline.SpriteCount, 64).Complete();
                    var actual = output[spriteIndex];
                    samples++;
                    string label = expected.path + "@" + sample.time;
                    Compare(actual.X, sample.position.x, "X", 0.002f);
                    Compare(actual.Y, sample.position.y, "Y", 0.002f);
                    Compare(
                        actual.ScaleX,
                        sample.scale.x * sample.vectorScale.x,
                        "scaleX",
                        0.0002f
                    );
                    Compare(
                        actual.ScaleY,
                        sample.scale.y * sample.vectorScale.y,
                        "scaleY",
                        0.0002f
                    );
                    Compare(
                        actual.Rotation * Mathf.Rad2Deg,
                        sample.rotationDegrees,
                        "rotation",
                        0.002f
                    );
                    Compare(actual.Alpha, sample.alphaBeforeUpdate, "alpha", 0.00002f);
                    Compare(actual.R, sample.colour.r, "R", 0.00002f);
                    Compare(actual.G, sample.colour.g, "G", 0.00002f);
                    Compare(actual.B, sample.colour.b, "B", 0.00002f);
                    Compare(actual.FlipH, sample.flipH ? 1 : 0, "flipH", 0);
                    Compare(actual.FlipV, sample.flipV ? 1 : 0, "flipV", 0);
                    Compare(
                        actual.Additive,
                        sample.blending.destination == "One" ? 1 : 0,
                        "additive",
                        0
                    );
                    void Compare(float a, float b, string field, float tolerance)
                    {
                        comparisons++;
                        if (float.IsNaN(a) || Mathf.Abs(a - b) > tolerance)
                            failures.Add(label + " " + field + ": Unity=" + a + ", lazer=" + b);
                    }
                }
            }
            string report =
                "lazer "
                + oracle.gameVersion
                + " / framework "
                + oracle.frameworkVersion
                + ": "
                + samples
                + " samples, "
                + comparisons
                + " property comparisons, "
                + failures.Count
                + " failures";
            string details =
                report + Environment.NewLine + string.Join(Environment.NewLine, failures);
            File.WriteAllText(Path.ChangeExtension(oraclePath, "unity-results.txt"), details);
            return details;
        }
        finally
        {
            output.Dispose();
            timeline.Dispose();
        }
    }
}
