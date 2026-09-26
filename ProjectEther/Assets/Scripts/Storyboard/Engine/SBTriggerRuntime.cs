using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using OsuVR.Storyboard.Data;
using Unity.Collections;
using UnityEngine;

namespace OsuVR.Storyboard.Engine
{
    /// <summary>把触发的 transform 追加到 sprite 时间轴上，保留延迟/重复触发的事件。</summary>
    public sealed class SBTriggerRuntime
    {
        sealed class Definition
        {
            public int Sprite;
            public SBTrigger Source;
            public SBCommandFlatData[] Commands;
            public bool HitSound;
            public SampleSet NormalBank,
                AdditionBank;
            public HitSoundType Addition;
            public int? CustomIndex;
        }

        readonly List<Definition> definitions = new List<Definition>();
        static readonly Regex hitSoundPattern = new Regex(
            @"^HitSound(?<bank1>All|Normal|Soft|Drum)?(?<bank2>All|Normal|Soft|Drum)?(?<addition>Whistle|Clap|Finish)?(?<index>[0-9]+)?$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
        );

        public SBTriggerRuntime(SBStoryboard storyboard)
        {
            int spriteIndex = 0;
            foreach (var sprite in storyboard.GetAllElementsInRenderOrder())
            {
                foreach (var trigger in sprite.Triggers)
                {
                    var group = SBCommandGroupBuilder.BuildTriggerGroup(trigger);
                    var definition = new Definition
                    {
                        Sprite = spriteIndex,
                        Source = trigger,
                        Commands = new SBCommandFlatData[group.Commands.Count],
                    };
                    for (int i = 0; i < group.Commands.Count; i++)
                        definition.Commands[i] = SBTimelineFlattener.ConvertCommand(
                            group.Commands[i]
                        );
                    var match = hitSoundPattern.Match(trigger.TriggerName);
                    if (match.Success)
                    {
                        definition.HitSound = true;
                        definition.NormalBank = Bank(match.Groups["bank1"].Value);
                        definition.AdditionBank = Bank(match.Groups["bank2"].Value);
                        Enum.TryParse(
                            match.Groups["addition"].Value,
                            true,
                            out definition.Addition
                        );
                        if (
                            match.Groups["bank1"].Success
                            && !match.Groups["bank2"].Success
                            && definition.Addition != 0
                        )
                        {
                            definition.AdditionBank = definition.NormalBank;
                            definition.NormalBank = SampleSet.None;
                        }
                        if (int.TryParse(match.Groups["index"].Value, out int index))
                            definition.CustomIndex = index;
                    }
                    definitions.Add(definition);
                }
                spriteIndex++;
            }
        }

        static SampleSet Bank(string name) =>
            Enum.TryParse(name, true, out SampleSet bank) ? bank : SampleSet.None;

        public static void ClearHistory(ref SBFlatTimelineData timeline)
        {
            if (timeline.Sprites.IsCreated)
            {
                for (int i = 0; i < timeline.SpriteCount; i++)
                {
                    var sprite = timeline.Sprites[i];
                    sprite.TriggerHead = -1;
                    timeline.Sprites[i] = sprite;
                }
            }
            timeline.TriggerCommandCount = 0;
            if (timeline.TriggerCommands.IsCreated && timeline.TriggerCommands.Length > 256)
            {
                timeline.TriggerCommands.Dispose();
                timeline.TriggerCommands = new NativeArray<SBTriggeredCommand>(
                    0,
                    Allocator.Persistent
                );
            }
        }

        // 仅已撤销命令不会复活；未来触发仍可能让较早的有效命令重新胜出。
        public static void CompactInvalidated(ref SBFlatTimelineData timeline)
        {
            int count = timeline.TriggerCommandCount;
            if (count < 128)
                return;

            int invalid = 0;
            for (int i = 0; i < count; i++)
                if (timeline.TriggerCommands[i].Command.Target == -1)
                    invalid++;
            if (invalid < 64 || invalid * 4 < count)
                return;

            int retained = count - invalid;
            var remap = new NativeArray<int>(count, Allocator.Temp);
            var compacted = new NativeArray<SBTriggeredCommand>(retained, Allocator.Temp);
            try
            {
                for (int i = 0; i < count; i++)
                    remap[i] = -1;
                int write = 0;
                for (int i = 0; i < count; i++)
                {
                    var entry = timeline.TriggerCommands[i];
                    if (entry.Command.Target == -1)
                        continue;
                    remap[i] = write;
                    compacted[write++] = entry;
                }

                for (int i = 0; i < retained; i++)
                {
                    var entry = compacted[i];
                    int next = entry.Next;
                    while (next >= 0 && remap[next] < 0)
                        next = timeline.TriggerCommands[next].Next;
                    entry.Next = next >= 0 ? remap[next] : -1;
                    compacted[i] = entry;
                }
                for (int i = 0; i < timeline.SpriteCount; i++)
                {
                    var sprite = timeline.Sprites[i];
                    int head = sprite.TriggerHead;
                    while (head >= 0 && remap[head] < 0)
                        head = timeline.TriggerCommands[head].Next;
                    sprite.TriggerHead = head >= 0 ? remap[head] : -1;
                    timeline.Sprites[i] = sprite;
                }

                int capacity = timeline.TriggerCommands.Length;
                if (capacity > 256 && retained * 4 < capacity)
                {
                    var smaller = new NativeArray<SBTriggeredCommand>(
                        Mathf.NextPowerOfTwo(Math.Max(32, retained * 2)),
                        Allocator.Persistent
                    );
                    if (retained > 0)
                        NativeArray<SBTriggeredCommand>.Copy(compacted, smaller, retained);
                    timeline.TriggerCommands.Dispose();
                    timeline.TriggerCommands = smaller;
                }
                else if (retained > 0)
                    NativeArray<SBTriggeredCommand>.Copy(
                        compacted,
                        timeline.TriggerCommands,
                        retained
                    );
                timeline.TriggerCommandCount = retained;
            }
            finally
            {
                compacted.Dispose();
                remap.Dispose();
            }
        }

        public bool HasTriggers => definitions.Count > 0;

        public void FireNamed(ref SBFlatTimelineData timeline, string name, double time)
        {
            foreach (var definition in definitions)
                if (definition.Source.TriggerName.Equals(name, StringComparison.OrdinalIgnoreCase))
                    Fire(ref timeline, definition, time);
        }

        public void FireHitSound(
            ref SBFlatTimelineData timeline,
            double time,
            SampleSet normal,
            SampleSet addition,
            HitSoundType sounds,
            int customIndex
        )
        {
            const HitSoundType additions =
                HitSoundType.Whistle | HitSoundType.Finish | HitSoundType.Clap;
            foreach (var d in definitions)
            {
                if (!d.HitSound || (d.NormalBank != 0 && d.NormalBank != normal))
                    continue;
                if (d.Addition != 0 && (sounds & d.Addition) == 0)
                    continue;
                if (
                    d.AdditionBank != 0
                    && ((sounds & additions) == 0 || d.AdditionBank != addition)
                )
                    continue;
                if (d.CustomIndex.HasValue && d.CustomIndex.Value != customIndex)
                    continue;
                Fire(ref timeline, d, time);
            }
        }

        public void FireHitSamples(
            ref SBFlatTimelineData timeline,
            double time,
            List<HitSampleInfo> samples
        )
        {
            foreach (var d in definitions)
            {
                if (!d.HitSound)
                    continue;
                bool hasAddition = d.Addition == 0,
                    hasBank = d.AdditionBank == 0,
                    matches = true;
                foreach (var sample in samples)
                {
                    if (!(sample is BankHitSampleInfo bank))
                        continue;
                    SampleSet sampleSet =
                        bank.Bank == SampleBank.Soft ? SampleSet.Soft
                        : bank.Bank == SampleBank.Drum ? SampleSet.Drum
                        : SampleSet.Normal;
                    if (bank.Name == BankHitSampleInfo.HIT_NORMAL)
                    {
                        if (d.NormalBank != 0 && sampleSet != d.NormalBank)
                            matches = false;
                    }
                    else
                    {
                        HitSoundType type =
                            bank.Name == BankHitSampleInfo.HIT_WHISTLE ? HitSoundType.Whistle
                            : bank.Name == BankHitSampleInfo.HIT_CLAP ? HitSoundType.Clap
                            : bank.Name == BankHitSampleInfo.HIT_FINISH ? HitSoundType.Finish
                            : HitSoundType.None;
                        if (type == d.Addition)
                            hasAddition = true;
                        if (sampleSet == d.AdditionBank)
                            hasBank = true;
                    }
                    if (d.CustomIndex.HasValue && d.CustomIndex.Value != bank.CustomSampleBank)
                        matches = false;
                }
                if (matches && hasAddition && hasBank)
                    Fire(ref timeline, d, time);
            }
        }

        static void Fire(ref SBFlatTimelineData timeline, Definition definition, double time)
        {
            if (
                time < definition.Source.StartTime
                || time > definition.Source.EndTime
                || definition.Commands.Length == 0
            )
                return;
            int required = timeline.TriggerCommandCount + definition.Commands.Length;
            if (required > timeline.TriggerCommands.Length)
            {
                var larger = new NativeArray<SBTriggeredCommand>(
                    Mathf.NextPowerOfTwo(Math.Max(32, required)),
                    Allocator.Persistent
                );
                if (timeline.TriggerCommandCount > 0)
                    NativeArray<SBTriggeredCommand>.Copy(
                        timeline.TriggerCommands,
                        larger,
                        timeline.TriggerCommandCount
                    );
                timeline.TriggerCommands.Dispose();
                timeline.TriggerCommands = larger;
            }
            var sprite = timeline.Sprites[definition.Sprite];
            foreach (var template in definition.Commands)
            {
                var command = template;
                command.StartTime += time;
                command.EndTime += time;
                // 新触发会取消同一属性上排队中的未来变更。
                // 取消较旧的 P 重置，可以让重新触发的脉冲继续生效。
                for (
                    int previous = sprite.TriggerHead;
                    previous >= 0;
                    previous = timeline.TriggerCommands[previous].Next
                )
                {
                    var older = timeline.TriggerCommands[previous];
                    if (older.Command.Target != command.Target)
                        continue;
                    if (older.Command.StartTime > command.StartTime)
                        older.Command.Target = -1;
                    else if (
                        command.Target >= 7
                        && command.Target <= 9
                        && older.Command.EndTime > command.StartTime
                    )
                    {
                        if (older.Command.StartTime > time)
                            older.Command.Target = -1;
                        else
                        {
                            older.Command.EndTime = older.Command.StartTime;
                            older.Command.BoolEnd = older.Command.BoolStart;
                        }
                    }
                    timeline.TriggerCommands[previous] = older;
                }
                int index = timeline.TriggerCommandCount++;
                timeline.TriggerCommands[index] = new SBTriggeredCommand
                {
                    Command = command,
                    Next = sprite.TriggerHead,
                };
                sprite.TriggerHead = index;
            }
            // 与 lazer 一致，触发器只添加 transform；它们不会产生 HP 状态，也
            // 不会替换声明的 sprite 生命周期。GroupNumber 由解析器保留。
            timeline.Sprites[definition.Sprite] = sprite;
        }
    }
}
