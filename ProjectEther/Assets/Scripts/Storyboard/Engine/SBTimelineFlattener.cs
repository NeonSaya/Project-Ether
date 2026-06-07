using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using OsuVR.Storyboard.Data;

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
        public int SpriteCount;

        public void Dispose()
        {
            if (Sprites.IsCreated) Sprites.Dispose();
            if (Commands.IsCreated) Commands.Dispose();
            if (Loops.IsCreated) Loops.Dispose();
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
            Vector2Int[] textureDimensions)
        {
            var flatSprites = new List<SBSpriteFlatData>();
            var flatCommands = new List<SBCommandFlatData>();
            var flatLoops = new List<SBLoopFlatData>();

            int totalElements = storyboard.TotalElementCount;
            if (totalElements == 0)
            {
                return new SBFlatTimelineData
                {
                    Sprites = new NativeArray<SBSpriteFlatData>(0, Allocator.Persistent),
                    Commands = new NativeArray<SBCommandFlatData>(0, Allocator.Persistent),
                    Loops = new NativeArray<SBLoopFlatData>(0, Allocator.Persistent),
                    SpriteCount = 0
                };
            }

            // 按层遍历所有元素 (Background → Overlay)
            for (int layer = 0; layer < 5; layer++)
            {
                var elements = storyboard.Layers[layer];
                if (elements == null) continue;

                for (int ei = 0; ei < elements.Count; ei++)
                {
                    var element = elements[ei];
                    FlattenElement(element, textureIndexMap, textureDimensions,
                        flatSprites, flatCommands, flatLoops);
                }
            }

            SBDebugLog.Log($"[Flattener] {flatSprites.Count} sprites, {flatCommands.Count} commands, {flatLoops.Count} loops");

            // 转为 NativeArray
            var result = new SBFlatTimelineData
            {
                SpriteCount = flatSprites.Count,
                Sprites = new NativeArray<SBSpriteFlatData>(flatSprites.Count, Allocator.Persistent),
                Commands = new NativeArray<SBCommandFlatData>(flatCommands.Count, Allocator.Persistent),
                Loops = new NativeArray<SBLoopFlatData>(flatLoops.Count, Allocator.Persistent)
            };

            if (flatSprites.Count > 0)
                NativeArray<SBSpriteFlatData>.Copy(flatSprites.ToArray(), result.Sprites);
            if (flatCommands.Count > 0)
                NativeArray<SBCommandFlatData>.Copy(flatCommands.ToArray(), result.Commands);
            if (flatLoops.Count > 0)
                NativeArray<SBLoopFlatData>.Copy(flatLoops.ToArray(), result.Loops);

            return result;
        }

        static void FlattenElement(
            SBElement element,
            Dictionary<string, int> textureIndexMap,
            Vector2Int[] textureDimensions,
            List<SBSpriteFlatData> outSprites,
            List<SBCommandFlatData> outCommands,
            List<SBLoopFlatData> outLoops)
        {
            // 1. 使用现有 SBCommandGroupBuilder 展开命令 (M→X+Y, S→SX+SY, etc.)
            var group = SBCommandGroupBuilder.Build(element);

            int cmdOffset = outCommands.Count;
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

            // 3. 直接命令按 StartTime 排序后写入 flat array
            directCmds.Sort((a, b) =>
            {
                int cmp = a.StartTime.CompareTo(b.StartTime);
                return cmp != 0 ? cmp : a.EndTime.CompareTo(b.EndTime);
            });

            for (int i = 0; i < directCmds.Count; i++)
                outCommands.Add(ConvertCommand(directCmds[i]));

            int cmdCount = outCommands.Count - cmdOffset;
            int loopCount = outLoops.Count - loopOffset;

            // 4. 纹理索引
            int texIndex = -1;
            int texWidth = 0, texHeight = 0;
            int animFrameCount = 0;
            double animFrameDelay = 0;
            int animLoopType = 0;
            int animBaseTexIndex = -1;

            if (!string.IsNullOrEmpty(element.ImagePath) && textureIndexMap != null)
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

            if (element is SBStoryboardAnimation anim)
            {
                animFrameCount = anim.FrameCount;
                animFrameDelay = anim.FrameDelay;
                animLoopType = (int)anim.LoopType;
                animBaseTexIndex = texIndex; // 第0帧的索引
                // 动画 sprite 的 TexIndex 由 Burst Job 动态计算
                texIndex = -1;
            }

            // 5. 构建 SpriteFlatData
            double startTime = group.StartTime();
            double endTime = group.EndTime();
            if (startTime >= double.MaxValue) startTime = 0;
            if (endTime <= double.MinValue) endTime = startTime;

            int originIdx = (int)element.Origin;
            if ((uint)originIdx > 9) originIdx = 1; // fallback to Centre

            var sprite = new SBSpriteFlatData
            {
                InitX = element.InitialPosition.x,
                InitY = element.InitialPosition.y,
                InitAlpha = 1f,
                InitScaleX = 1f,
                InitScaleY = 1f,
                InitRotation = 0f,
                InitR = 1f,
                InitG = 1f,
                InitB = 1f,
                InitFlipH = 0,
                InitFlipV = 0,
                InitAdditive = 0,

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
                AnimBaseTexIndex = animBaseTexIndex
            };

            // 应用初始值 (从最早的直接命令中提取)
            ApplyInitialValues(directCmds, ref sprite);

            outSprites.Add(sprite);
        }

        static void FlattenLoopInner(
            SBLoopCommand loopCmd,
            List<SBCommandFlatData> outCommands,
            List<SBLoopFlatData> outLoops)
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

            innerCmds.Sort((a, b) =>
            {
                int cmp = a.StartTime.CompareTo(b.StartTime);
                return cmp != 0 ? cmp : a.EndTime.CompareTo(b.EndTime);
            });

            for (int i = 0; i < innerCmds.Count; i++)
                outCommands.Add(ConvertCommand(innerCmds[i]));

            int innerCount = outCommands.Count - innerOffset;

            // 计算 LoopDuration (内层命令的最大 EndTime)
            double loopDuration = 0;
            for (int i = 0; i < innerCmds.Count; i++)
            {
                if (innerCmds[i].EndTime > loopDuration)
                    loopDuration = innerCmds[i].EndTime;
            }
            if (loopDuration <= 0) loopDuration = 1;

            outLoops.Add(new SBLoopFlatData
            {
                StartTime = loopCmd.StartTime,
                LoopDuration = loopDuration,
                LoopCount = loopCmd.LoopCount,
                InnerCmdOffset = innerOffset,
                InnerCmdCount = innerCount
            });
        }

        static SBCommandFlatData ConvertCommand(SBSpriteCommand cmd)
        {
            var flat = new SBCommandFlatData
            {
                StartTime = cmd.StartTime,
                EndTime = cmd.EndTime,
                Easing = (int)cmd.Easing,
                Target = (int)cmd.Target
            };

            switch (cmd)
            {
                case SBFloatCommand fc:
                    flat.FloatStart = fc.StartValue;
                    flat.FloatEnd = fc.EndValue;
                    break;

                case SBColorCommand cc:
                    flat.ColorStartR = cc.StartValue.r / 255f;
                    flat.ColorStartG = cc.StartValue.g / 255f;
                    flat.ColorStartB = cc.StartValue.b / 255f;
                    flat.ColorEndR = cc.EndValue.r / 255f;
                    flat.ColorEndG = cc.EndValue.g / 255f;
                    flat.ColorEndB = cc.EndValue.b / 255f;
                    break;

                case SBBoolCommand bc:
                    flat.BoolStart = bc.StartValue ? (byte)1 : (byte)0;
                    flat.BoolEnd = bc.EndValue ? (byte)1 : (byte)0;
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
            for (int i = 0; i < cmds.Count && found.Count < 10; i++)
            {
                var cmd = cmds[i];
                int target = (int)cmd.Target;
                if (found.Contains(target)) continue;
                found.Add(target);

                switch (cmd)
                {
                    case SBFloatCommand fc:
                        switch (fc.Target)
                        {
                            case SBCommandTarget.Alpha: sprite.InitAlpha = fc.StartValue; break;
                            case SBCommandTarget.X: sprite.InitX = fc.StartValue; break;
                            case SBCommandTarget.Y: sprite.InitY = fc.StartValue; break;
                            case SBCommandTarget.ScaleX: sprite.InitScaleX = fc.StartValue; break;
                            case SBCommandTarget.ScaleY: sprite.InitScaleY = fc.StartValue; break;
                            case SBCommandTarget.Rotation: sprite.InitRotation = fc.StartValue; break;
                        }
                        break;

                    case SBColorCommand cc:
                        sprite.InitR = cc.StartValue.r / 255f;
                        sprite.InitG = cc.StartValue.g / 255f;
                        sprite.InitB = cc.StartValue.b / 255f;
                        break;

                    case SBBoolCommand bc:
                        switch (bc.Target)
                        {
                            case SBCommandTarget.BlendingMode: sprite.InitAdditive = bc.StartValue ? (byte)1 : (byte)0; break;
                            case SBCommandTarget.FlipH: sprite.InitFlipH = bc.StartValue ? (byte)1 : (byte)0; break;
                            case SBCommandTarget.FlipV: sprite.InitFlipV = bc.StartValue ? (byte)1 : (byte)0; break;
                        }
                        break;
                }
            }
        }
    }
}
