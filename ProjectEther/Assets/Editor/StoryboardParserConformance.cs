using System;
using System.Collections.Generic;
using OsuVR;
using OsuVR.Storyboard;
using OsuVR.Storyboard.Data;
using OsuVR.Storyboard.Engine;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

public static class StoryboardParserConformance
{
    public static string Run()
    {
        int checks = 0;
        void Check(bool condition, string name)
        {
            if (!condition)
                throw new Exception(name);
            checks++;
        }
        var parsed = StoryboardParser.Parse(
            new List<string>
            {
                "[Events]",
                "4,Foreground,TopLeft,test.png,10,20",
                "_F,0,-100,1000,1",
                "_S,0,-100,,2",
                "_L,100,3",
                "__MX,0,50,150,10,30",
                "_MY,0,500,800,20,40",
                "_T,HitSoundClap,200,900,2",
                "__F,0,0,100,1,0",
                "6,Background,Centre,anim.png,320,240,3,100,LoopOnce",
                " F,0,0,1000,1",
            }
        );
        var sprite = parsed.Layers[3][0];
        Check(sprite.FadeCommands.Count == 1, "underscore command decoding");
        Check(
            sprite.ScaleCommands[0].StartTime == -100 && sprite.ScaleCommands[0].EndTime == -100,
            "negative shorthand EndTime"
        );
        Check(
            sprite.Loops.Count == 1
                && sprite.Loops[0].LoopCount == 3
                && sprite.Loops[0].MoveXCommands.Count == 1,
            "loop parsing and indentation"
        );
        Check(sprite.MoveYCommands.Count == 1, "loop scope closes on unindent");
        Check(
            sprite.Triggers.Count == 1
                && sprite.Triggers[0].Commands.Count == 1
                && sprite.Triggers[0].GroupNumber == -2,
            "trigger scope and group"
        );
        Check(
            ((SBStoryboardAnimation)parsed.Layers[0][0]).LoopType == SBAnimationLoopType.LoopOnce,
            "named LoopOnce"
        );
        Check(
            sprite.FadeCommands[0].Sequence < sprite.ScaleCommands[0].Sequence,
            "source declaration sequence"
        );
        var sb = StoryboardParser.Parse(
            new List<string>
            {
                "Sprite,Foreground,Centre,test.png,320,240",
                " F,0,0,2000,0",
                " T,HitSoundClap,100,1500",
                "  F,0,0,100,1,0",
                "  MX,0,200,400,320,420",
                "  P,0,0,100,A",
            }
        );
        var timeline = SBTimelineFlattener.Flatten(
            sb,
            new Dictionary<string, int> { { "test.png", 0 } },
            new[] { new Vector2Int(1, 1) }
        );
        var runtime = new SBTriggerRuntime(sb);
        var output = new NativeArray<SpriteInputData>(1, Allocator.TempJob);
        try
        {
            SpriteInputData At(double time)
            {
                new SBEvaluateTimelineJob
                {
                    Sprites = timeline.Sprites,
                    Commands = timeline.Commands,
                    Loops = timeline.Loops,
                    FrameMap = timeline.FrameMap,
                    TriggerCommands = timeline.TriggerCommands,
                    Output = output,
                    CurrentTime = time,
                    SpriteCount = 1,
                }
                    .Schedule(1, 1)
                    .Complete();
                return output[0];
            }
            Check(At(500).Alpha == 0, "trigger stays dormant without event");
            runtime.FireHitSound(
                ref timeline,
                500,
                SampleSet.Normal,
                SampleSet.Soft,
                HitSoundType.Whistle,
                0
            );
            Check(timeline.TriggerCommandCount == 0, "unmatched hitsound ignored");
            runtime.FireHitSound(
                ref timeline,
                50,
                SampleSet.Normal,
                SampleSet.Soft,
                HitSoundType.Clap,
                0
            );
            Check(timeline.TriggerCommandCount == 0, "trigger active time window");
            runtime.FireHitSound(
                ref timeline,
                500,
                SampleSet.Normal,
                SampleSet.Soft,
                HitSoundType.Clap,
                0
            );
            Check(
                Mathf.Abs(At(525).Alpha - 0.75f) < 0.0001,
                "trigger-relative alpha interpolation"
            );
            runtime.FireHitSound(
                ref timeline,
                550,
                SampleSet.Normal,
                SampleSet.Soft,
                HitSoundType.Clap,
                0
            );
            Check(Mathf.Abs(At(575).Alpha - 0.75f) < 0.0001, "retrigger overrides active alpha");
            Check(At(625).Additive == 1, "retrigger cancels older parameter reset");
            Check(At(650).Additive == 0, "new parameter reset remains active");
            Check(
                Mathf.Abs(At(725).X - 332.5f) < 0.0001,
                "pending earlier delayed transform retained"
            );
            Check(
                Mathf.Abs(At(750).X - 320) < 0.0001,
                "new delayed transform starts in chronological order"
            );
            Check(At(2000).Alpha == 0, "lifetime end remains exclusive");
        }
        finally
        {
            output.Dispose();
            timeline.Dispose();
        }
        return "PASS " + checks + " parser/trigger regression checks";
    }
}
