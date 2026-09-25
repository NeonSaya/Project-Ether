using System.Collections.Generic;
using OsuVR.Storyboard.Data;
using Unity.Collections;
using UnityEngine;

namespace OsuVR.Storyboard.Engine
{
    /// <summary>
    /// 加载时一次性转换: SBElement 树 → NativeArray 扁平数据
    /// 复用 SBCommandGroupBuilder 进行命令展开 (M→X+Y, S→SX+SY, V→SX+SY)
    /// 输出的 NativeArray 使用 Allocator.Persistent, 运行时只读, 由 StoryboardRenderer 管理生命周期
    /// </summary>
    public struct SBFlatTimelineData : System.IDisposable
    {
        public NativeArray<SBSpriteFlatData> Sprites;
        public NativeArray<SBCommandFlatData> Commands;
        public NativeArray<SBLoopFlatData> Loops;

        /// <summary>动画帧映射: 每个动画 sprite 的声明帧→纹理切片索引 (-1=该帧文件缺失, 不绘制). 按 sprite.AnimFrameMapOffset 寻址</summary>
        public NativeArray<int> FrameMap;
        public NativeArray<SBTriggeredCommand> TriggerCommands;
        public int TriggerCommandCount;
        public int SpriteCount;

        public void Dispose()
        {
            if (Sprites.IsCreated)
                Sprites.Dispose();
            if (Commands.IsCreated)
                Commands.Dispose();
            if (Loops.IsCreated)
                Loops.Dispose();
            if (FrameMap.IsCreated)
                FrameMap.Dispose();
            if (TriggerCommands.IsCreated)
                TriggerCommands.Dispose();
        }
    }

    public static class SBTimelineFlattener
    {
        /// <summary>
        /// 将 SBStoryboard 扁平化为 NativeArray 数据, 供 Burst Job 零 GC 消费
        /// </summary>
        /// <param name="storyboard">解析器输出的 SB 对象树</param>
        /// <param name="textureIndexMap">纹理路径→索引映射 (静态 sprite 使用)</param>
        /// <param name="textureDimensions">纹理尺寸数组</param>
        public static SBFlatTimelineData Flatten(
            SBStoryboard storyboard,
            Dictionary<string, int> textureIndexMap,
            Vector2Int[] textureDimensions
        )
        {
            var flatSprites = new List<SBSpriteFlatData>();
            var flatCommands = new List<SBCommandFlatData>();
            var flatLoops = new List<SBLoopFlatData>();
            var frameMap = new List<int>();

            int totalElements = storyboard.TotalElementCount;
            if (totalElements == 0)
            {
                return new SBFlatTimelineData
                {
                    Sprites = new NativeArray<SBSpriteFlatData>(0, Allocator.Persistent),
                    Commands = new NativeArray<SBCommandFlatData>(0, Allocator.Persistent),
                    Loops = new NativeArray<SBLoopFlatData>(0, Allocator.Persistent),
                    FrameMap = new NativeArray<int>(0, Allocator.Persistent),
                    TriggerCommands = new NativeArray<SBTriggeredCommand>(0, Allocator.Persistent),
                    SpriteCount = 0,
                };
            }

            // 按层遍历所有元素 (Background → Overlay)
            for (int layer = 0; layer < 5; layer++)
            {
                if (layer == (int)SBLayer.Fail && !storyboard.IsFailState)
                    continue;
                if (layer == (int)SBLayer.Pass && storyboard.IsFailState)
                    continue;
                var elements = storyboard.Layers[layer];
                if (elements == null)
                    continue;

                for (int ei = 0; ei < elements.Count; ei++)
                {
                    var element = elements[ei];
                    FlattenElement(
                        element,
                        textureIndexMap,
                        textureDimensions,
                        flatSprites,
                        flatCommands,
                        flatLoops,
                        frameMap
                    );
                }
            }

            SBDebugLog.Log(
                $"[Flattener] {flatSprites.Count} sprites, {flatCommands.Count} commands, {flatLoops.Count} loops, {frameMap.Count} anim frames"
            );

            // 转为 NativeArray
            var result = new SBFlatTimelineData
            {
                TriggerCommands = new NativeArray<SBTriggeredCommand>(0, Allocator.Persistent),
                SpriteCount = flatSprites.Count,
                Sprites = new NativeArray<SBSpriteFlatData>(
                    flatSprites.Count,
                    Allocator.Persistent
                ),
                Commands = new NativeArray<SBCommandFlatData>(
                    flatCommands.Count,
                    Allocator.Persistent
                ),
                Loops = new NativeArray<SBLoopFlatData>(flatLoops.Count, Allocator.Persistent),
                FrameMap = new NativeArray<int>(frameMap.Count, Allocator.Persistent),
            };

            if (flatSprites.Count > 0)
                NativeArray<SBSpriteFlatData>.Copy(flatSprites.ToArray(), result.Sprites);
            if (flatCommands.Count > 0)
                NativeArray<SBCommandFlatData>.Copy(flatCommands.ToArray(), result.Commands);
            if (flatLoops.Count > 0)
                NativeArray<SBLoopFlatData>.Copy(flatLoops.ToArray(), result.Loops);
            if (frameMap.Count > 0)
                NativeArray<int>.Copy(frameMap.ToArray(), result.FrameMap);

            return result;
        }

        static void RemoveAbortedParameterWindows(SBCommandGroup group)
        {
            var parameters = new List<(SBBoolCommand command, double offset)>();
            foreach (var command in group.Commands)
            {
                if (command is SBBoolCommand parameter)
                    parameters.Add((parameter, 0));
                if (command is SBLoopCommand loop)
                    foreach (var inner in loop.InnerGroup.Commands)
                        if (inner is SBBoolCommand p)
                            parameters.Add((p, loop.StartTime));
            }
            parameters.Sort(
                (a, b) =>
                {
                    int order = (a.command.StartTime + a.offset).CompareTo(
                        b.command.StartTime + b.offset
                    );
                    if (order != 0)
                        return order;
                    order = (a.command.EndTime + a.offset).CompareTo(b.command.EndTime + b.offset);
                    return order != 0 ? order : a.command.Sequence.CompareTo(b.command.Sequence);
                }
            );
            // Framework 的 AddTransform 会移除同一属性上尚未生效的 transform。
            // 在前一个 P 窗口内加入新 P 的起点，会移除它排队中的重置；
            // 中止该序列同样会移除它尚未应用的循环起点。
            // 保持源时间不变，因为 sprite 的生命周期仍由声明决定。
            for (int i = 0; i < parameters.Count; i++)
            for (int j = 0; j < i; j++)
            {
                var earlier = parameters[j];
                var later = parameters[i];
                if (
                    earlier.command.Target != later.command.Target
                    || earlier.command.Suppressed
                    || earlier.command.ResetSuppressed
                    || earlier.command.StartTime == earlier.command.EndTime
                )
                    continue;
                if (
                    earlier.command.EndTime + earlier.offset
                    <= later.command.StartTime + later.offset
                )
                    continue;
                if (earlier.command.StartTime + earlier.offset > 0)
                    earlier.command.Suppressed = true;
                else
                    earlier.command.ResetSuppressed = true;
            }
        }

        static void FlattenElement(
            SBElement element,
            Dictionary<string, int> textureIndexMap,
            Vector2Int[] textureDimensions,
            List<SBSpriteFlatData> outSprites,
            List<SBCommandFlatData> outCommands,
            List<SBLoopFlatData> outLoops,
            List<int> frameMap
        )
        {
            // 1. 使用现有 SBCommandGroupBuilder 展开命令 (M→X+Y, S→SX+SY, etc.)
            var group = SBCommandGroupBuilder.Build(element);
            RemoveAbortedParameterWindows(group);

            int loopOffset = outLoops.Count;

            // 2. 分离直接命令和 Loop 命令
            var directCmds = new List<SBSpriteCommand>();
            for (int i = 0; i < group.Commands.Count; i++)
            {
                var cmd = group.Commands[i];
                if (cmd is SBLoopCommand loopCmd)
                {
                    // 扁平化 Loop 内层命令
                    FlattenLoopInner(loopCmd, outCommands, outLoops);
                }
                else
                {
                    directCmds.Add(cmd);
                }
            }

            // [修复] cmdOffset 必须在 FlattenLoopInner 追加内层命令之后再采样，
            // 否则 sprite 的直接命令窗口会混入 Loop 内层命令，Job 在 Loop 开始前就按绝对时间求值它们
            int cmdOffset = outCommands.Count;

            // 3. 直接命令按 StartTime 排序后写入 flat array
            directCmds.Sort(
                (a, b) =>
                {
                    return CompareCommands(a, b);
                }
            );

            for (int i = 0; i < directCmds.Count; i++)
                outCommands.Add(ConvertCommand(directCmds[i]));

            int cmdCount = outCommands.Count - cmdOffset;
            int loopCount = outLoops.Count - loopOffset;

            // 4. 纹理索引
            int texIndex = -1;
            int texWidth = 0,
                texHeight = 0;
            int animFrameCount = 0;
            double animFrameDelay = 0;
            int animLoopType = 0;
            int animFrameMapOffset = 0;

            if (element is SBStoryboardAnimation anim)
            {
                // 动画: 为每个声明帧建立 帧序号→纹理切片 映射
                // 缺失帧 (文件不存在, 未打包进纹理数组) 记为 -1, 渲染时该帧不绘制
                // (与 storybrew/osu! 行为一致: 帧序号按声明的 FrameCount 推进, 缺失帧留空)
                animFrameCount = anim.FrameCount;
                animFrameDelay = anim.FrameDelay;
                animLoopType = (int)anim.LoopType;
                animFrameMapOffset = frameMap.Count;

                bool dimsResolved = false;
                for (int f = 0; f < anim.FrameCount; f++)
                {
                    string framePath = anim.BuildFramePath(f).Replace('\\', '/').ToLowerInvariant();
                    int slice = -1;
                    if (
                        textureIndexMap != null
                        && textureIndexMap.TryGetValue(framePath, out int idx)
                    )
                    {
                        slice = idx;
                        if (
                            !dimsResolved
                            && textureDimensions != null
                            && idx < textureDimensions.Length
                        )
                        {
                            texWidth = textureDimensions[idx].x;
                            texHeight = textureDimensions[idx].y;
                            dimsResolved = true;
                        }
                    }
                    frameMap.Add(slice);
                }
                // 动画 sprite 的 TexIndex 由 Burst Job 经 FrameMap 动态解析
                texIndex = -1;
            }
            else if (!string.IsNullOrEmpty(element.ImagePath) && textureIndexMap != null)
            {
                string normalized = element.ImagePath.Replace('\\', '/').ToLowerInvariant();
                if (textureIndexMap.TryGetValue(normalized, out int idx))
                {
                    texIndex = idx;
                    if (textureDimensions != null && idx < textureDimensions.Length)
                    {
                        texWidth = textureDimensions[idx].x;
                        texHeight = textureDimensions[idx].y;
                    }
                }
            }

            // 5. 构建 SpriteFlatData
            double startTime = double.MaxValue;
            foreach (var command in group.Commands)
            {
                double first =
                    command is SBLoopCommand loop && loop.InnerGroup.Commands.Count > 0
                        ? loop.StartTime + loop.InnerGroup.StartTime()
                        : command.StartTime;
                startTime = System.Math.Min(startTime, first);
            }
            double endTime = group.EndTime();
            if (startTime >= double.MaxValue)
                startTime = 0;
            if (endTime <= double.MinValue)
                endTime = startTime;

            int originIdx = (int)element.Origin;
            if ((uint)originIdx > 9)
                originIdx = 1; // 回退到 Centre

            var sprite = new SBSpriteFlatData
            {
                InitX = element.InitialPosition.x,
                InitY = element.InitialPosition.y,
                InitAlpha = 1f,
                InitScaleX = 1f,
                InitScaleY = 1f,
                InitUniformScale = 1f,
                InitVectorScaleX = 1f,
                InitVectorScaleY = 1f,
                InitRotation = 0f,
                InitR = 1f,
                InitG = 1f,
                InitB = 1f,
                InitFlipH = 0,
                InitFlipV = 0,
                InitAdditive = 0,

                TriggerHead = -1,
                CmdOffset = cmdOffset,
                CmdCount = cmdCount,
                LoopOffset = loopOffset,
                LoopCount = loopCount,

                StartTime = startTime,
                EndTime = endTime,

                OriginIndex = originIdx,
                TexIndex = texIndex,
                TexWidth = texWidth,
                TexHeight = texHeight,

                AnimFrameCount = animFrameCount,
                AnimFrameDelay = animFrameDelay,
                AnimLoopType = animLoopType,
                AnimFrameMapOffset = animFrameMapOffset,
            };

            // 应用初始值 (从最早的直接命令中提取)
            var initialCommands = new List<SBSpriteCommand>(directCmds);
            foreach (var command in group.Commands)
                if (command is SBLoopCommand loop)
                    foreach (var inner in loop.InnerGroup.Commands)
                        initialCommands.Add(inner.CreateOffsetCommand(loop.StartTime));
            initialCommands.Sort(CompareCommands);
            ApplyInitialValues(initialCommands, ref sprite);

            outSprites.Add(sprite);
        }

        static void FlattenLoopInner(
            SBLoopCommand loopCmd,
            List<SBCommandFlatData> outCommands,
            List<SBLoopFlatData> outLoops
        )
        {
            int innerOffset = outCommands.Count;

            // 展开内层命令
            var innerCmds = new List<SBSpriteCommand>();
            for (int i = 0; i < loopCmd.InnerGroup.Commands.Count; i++)
            {
                var cmd = loopCmd.InnerGroup.Commands[i];
                if (cmd is SBLoopCommand)
                {
                    // 嵌套 Loop: 直接展平内层命令 (osu! 实践中极少出现)
                    // 简化处理: 忽略嵌套 Loop, 仅取直接命令
                    SBDebugLog.Log("[Flattener] 警告: 嵌套 Loop 被展平");
                    continue;
                }
                innerCmds.Add(cmd);
            }

            innerCmds.Sort(
                (a, b) =>
                {
                    return CompareCommands(a, b);
                }
            );

            for (int i = 0; i < innerCmds.Count; i++)
                outCommands.Add(ConvertCommand(innerCmds[i]));

            int innerCount = outCommands.Count - innerOffset;

            double loopDuration = loopCmd.LoopDuration;

            outLoops.Add(
                new SBLoopFlatData
                {
                    StartTime = loopCmd.StartTime,
                    LoopDuration = loopDuration,
                    LoopCount = loopCmd.LoopCount,
                    InnerCmdOffset = innerOffset,
                    InnerCmdCount = innerCount,
                }
            );
        }

        static int CompareCommands(SBSpriteCommand a, SBSpriteCommand b)
        {
            int order = a.StartTime.CompareTo(b.StartTime);
            if (order != 0)
                return order;
            order = a.EndTime.CompareTo(b.EndTime);
            return order != 0 ? order : a.Sequence.CompareTo(b.Sequence);
        }

        internal static SBCommandFlatData ConvertCommand(SBSpriteCommand cmd)
        {
            var flat = new SBCommandFlatData
            {
                Sequence = cmd.Sequence,
                StartTime = cmd.StartTime,
                EndTime = cmd.EndTime,
                Easing = (int)cmd.Easing,
                Target = (int)cmd.Target,
            };

            switch (cmd)
            {
                case SBFloatCommand fc:
                    flat.FloatStart = fc.StartValue;
                    flat.FloatEnd = fc.EndValue;
                    break;

                case SBColorCommand cc:
                    flat.ColorStartR = cc.StartValue.r;
                    flat.ColorStartG = cc.StartValue.g;
                    flat.ColorStartB = cc.StartValue.b;
                    flat.ColorEndR = cc.EndValue.r;
                    flat.ColorEndG = cc.EndValue.g;
                    flat.ColorEndB = cc.EndValue.b;
                    break;

                case SBBoolCommand bc:
                    if (bc.Suppressed)
                        flat.Target = -1;
                    if (bc.ResetSuppressed)
                        flat.EndTime = flat.StartTime;
                    flat.BoolStart = bc.StartValue ? (byte)1 : (byte)0;
                    flat.BoolEnd = bc.EndValue || bc.ResetSuppressed ? (byte)1 : (byte)0;
                    break;
            }

            return flat;
        }

        /// <summary>
        /// 从最早命令中提取初始值 (与 SBPlayingSprite.ApplyInitialValues 逻辑一致)
        /// </summary>
        static void ApplyInitialValues(List<SBSpriteCommand> cmds, ref SBSpriteFlatData sprite)
        {
            var found = new HashSet<int>();
            for (int i = 0; i < cmds.Count; i++)
            {
                var cmd = cmds[i];
                if (cmd is SBBoolCommand suppressed && suppressed.Suppressed)
                    continue;
                int target = (int)cmd.Target;
                if (found.Contains(target))
                    continue;
                found.Add(target);

                switch (cmd)
                {
                    case SBFloatCommand fc:
                        switch (fc.Target)
                        {
                            case SBCommandTarget.Alpha:
                                sprite.InitAlpha = fc.StartValue;
                                break;
                            case SBCommandTarget.X:
                                sprite.InitX = fc.StartValue;
                                break;
                            case SBCommandTarget.Y:
                                sprite.InitY = fc.StartValue;
                                break;
                            case SBCommandTarget.ScaleX:
                                sprite.InitScaleX = fc.StartValue;
                                break;
                            case SBCommandTarget.ScaleY:
                                sprite.InitScaleY = fc.StartValue;
                                break;
                            case SBCommandTarget.Rotation:
                                sprite.InitRotation = fc.StartValue;
                                break;
                            case SBCommandTarget.UniformScale:
                                sprite.InitUniformScale = fc.StartValue;
                                break;
                            case SBCommandTarget.VectorScaleX:
                                sprite.InitVectorScaleX = fc.StartValue;
                                break;
                            case SBCommandTarget.VectorScaleY:
                                sprite.InitVectorScaleY = fc.StartValue;
                                break;
                        }
                        break;

                    case SBColorCommand cc:
                        sprite.InitR = cc.StartValue.r;
                        sprite.InitG = cc.StartValue.g;
                        sprite.InitB = cc.StartValue.b;
                        break;

                    case SBBoolCommand bc:
                        // 在 lazer 中，零时长的参数命令是一个永久性的初始值。
                        if (bc.UseInitialValue)
                        {
                            byte value = bc.StartValue ? (byte)1 : (byte)0;
                            if (bc.Target == SBCommandTarget.FlipH)
                                sprite.InitFlipH = value;
                            if (bc.Target == SBCommandTarget.FlipV)
                                sprite.InitFlipV = value;
                            if (bc.Target == SBCommandTarget.BlendingMode)
                                sprite.InitAdditive = value;
                        }
                        break;
                }
            }
        }
    }
}
