using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using OsuVR.Storyboard.Data;
using Unity.Collections;
using UnityEngine;

namespace OsuVR.Storyboard.Engine
{
    /// <summary>Append fired transforms to the sprite timeline, retaining delayed/repeated events.</summary>
    public sealed class SBTriggerRuntime
    {
        sealed class Definition
        {
            public int Sprite;
            public SBTrigger Source;
            public SBCommandFlatData[] Commands;
            public bool HitSound;
            public SampleSet NormalBank, AdditionBank;
            public HitSoundType Addition;
            public int? CustomIndex;
        }
        readonly List<Definition> definitions = new List<Definition>();
        static readonly Regex hitSoundPattern = new Regex(
            @"^HitSound(?<bank1>All|Normal|Soft|Drum)?(?<bank2>All|Normal|Soft|Drum)?(?<addition>Whistle|Clap|Finish)?(?<index>[0-9]+)?$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public SBTriggerRuntime(SBStoryboard storyboard)
        {
            int spriteIndex = 0;
            foreach (var sprite in storyboard.GetAllElementsInRenderOrder())
            {
                foreach (var trigger in sprite.Triggers)
                {
                    var group = SBCommandGroupBuilder.BuildTriggerGroup(trigger);
                    var definition = new Definition { Sprite = spriteIndex, Source = trigger,
                        Commands = new SBCommandFlatData[group.Commands.Count] };
                    for (int i = 0; i < group.Commands.Count; i++)
                        definition.Commands[i] = SBTimelineFlattener.ConvertCommand(group.Commands[i]);
                    var match = hitSoundPattern.Match(trigger.TriggerName);
                    if (match.Success)
                    {
                        definition.HitSound = true;
                        definition.NormalBank = Bank(match.Groups["bank1"].Value);
                        definition.AdditionBank = Bank(match.Groups["bank2"].Value);
                        Enum.TryParse(match.Groups["addition"].Value, true, out definition.Addition);
                        if (match.Groups["bank1"].Success && !match.Groups["bank2"].Success && definition.Addition != 0)
                        {
                            definition.AdditionBank = definition.NormalBank;
                            definition.NormalBank = SampleSet.None;
                        }
                        if (int.TryParse(match.Groups["index"].Value, out int index)) definition.CustomIndex = index;
                    }
                    definitions.Add(definition);
                }
                spriteIndex++;
            }
        }

        static SampleSet Bank(string name)
            => Enum.TryParse(name, true, out SampleSet bank) ? bank : SampleSet.None;
        public bool HasTriggers => definitions.Count > 0;

        public void FireNamed(ref SBFlatTimelineData timeline, string name, double time)
        {
            foreach (var definition in definitions)
                if (definition.Source.TriggerName.Equals(name, StringComparison.OrdinalIgnoreCase))
                    Fire(ref timeline, definition, time);
        }

        public void FireHitSound(ref SBFlatTimelineData timeline, double time,
            SampleSet normal, SampleSet addition, HitSoundType sounds, int customIndex)
        {
            const HitSoundType additions = HitSoundType.Whistle | HitSoundType.Finish | HitSoundType.Clap;
            foreach (var d in definitions)
            {
                if (!d.HitSound || (d.NormalBank != 0 && d.NormalBank != normal)) continue;
                if (d.Addition != 0 && (sounds & d.Addition) == 0) continue;
                if (d.AdditionBank != 0 && ((sounds & additions) == 0 || d.AdditionBank != addition)) continue;
                if (d.CustomIndex.HasValue && d.CustomIndex.Value != customIndex) continue;
                Fire(ref timeline, d, time);
            }
        }

        public void FireHitSamples(ref SBFlatTimelineData timeline, double time, List<HitSampleInfo> samples)
        {
            foreach (var d in definitions)
            {
                if (!d.HitSound) continue;
                bool hasAddition = d.Addition == 0, hasBank = d.AdditionBank == 0, matches = true;
                foreach (var sample in samples)
                {
                    if (!(sample is BankHitSampleInfo bank)) continue;
                    SampleSet sampleSet = bank.Bank == SampleBank.Soft ? SampleSet.Soft
                        : bank.Bank == SampleBank.Drum ? SampleSet.Drum : SampleSet.Normal;
                    if (bank.Name == BankHitSampleInfo.HIT_NORMAL)
                    {
                        if (d.NormalBank != 0 && sampleSet != d.NormalBank) matches = false;
                    }
                    else
                    {
                        HitSoundType type = bank.Name == BankHitSampleInfo.HIT_WHISTLE ? HitSoundType.Whistle
                            : bank.Name == BankHitSampleInfo.HIT_CLAP ? HitSoundType.Clap
                            : bank.Name == BankHitSampleInfo.HIT_FINISH ? HitSoundType.Finish : HitSoundType.None;
                        if (type == d.Addition) hasAddition = true;
                        if (sampleSet == d.AdditionBank) hasBank = true;
                    }
                    if (d.CustomIndex.HasValue && d.CustomIndex.Value != bank.CustomSampleBank) matches = false;
                }
                if (matches && hasAddition && hasBank) Fire(ref timeline, d, time);
            }
        }

        static void Fire(ref SBFlatTimelineData timeline, Definition definition, double time)
        {
            if (time < definition.Source.StartTime || time > definition.Source.EndTime || definition.Commands.Length == 0) return;
            int required = timeline.TriggerCommandCount + definition.Commands.Length;
            if (required > timeline.TriggerCommands.Length)
            {
                var larger = new NativeArray<SBTriggeredCommand>(Mathf.NextPowerOfTwo(Math.Max(32, required)), Allocator.Persistent);
                if (timeline.TriggerCommandCount > 0)
                    NativeArray<SBTriggeredCommand>.Copy(timeline.TriggerCommands, larger, timeline.TriggerCommandCount);
                timeline.TriggerCommands.Dispose();
                timeline.TriggerCommands = larger;
            }
            var sprite = timeline.Sprites[definition.Sprite];
            foreach (var template in definition.Commands)
            {
                var command = template;
                command.StartTime += time;
                command.EndTime += time;
                // A new trigger cancels queued future changes to the same property.
                // Cancelling an older P reset keeps the retriggered pulse alive.
                for (int previous = sprite.TriggerHead; previous >= 0; previous = timeline.TriggerCommands[previous].Next)
                {
                    var older = timeline.TriggerCommands[previous];
                    if (older.Command.Target != command.Target) continue;
                    if (older.Command.StartTime > command.StartTime)
                        older.Command.Target = -1;
                    else if (command.Target >= 7 && command.Target <= 9 && older.Command.EndTime > command.StartTime)
                    {
                        if (older.Command.StartTime > time) older.Command.Target = -1;
                        else
                        {
                            older.Command.EndTime = older.Command.StartTime;
                            older.Command.BoolEnd = older.Command.BoolStart;
                        }
                    }
                    timeline.TriggerCommands[previous] = older;
                }
                int index = timeline.TriggerCommandCount++;
                timeline.TriggerCommands[index] = new SBTriggeredCommand { Command = command, Next = sprite.TriggerHead };
                sprite.TriggerHead = index;
            }
            // As in lazer, triggers add transforms; they do not invent an HP state or
            // replace the declared sprite lifetime. GroupNumber is retained by the parser.
            timeline.Sprites[definition.Sprite] = sprite;
        }
    }
}
