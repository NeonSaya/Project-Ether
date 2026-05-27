using System.Collections.Generic;

namespace OsuVR.Storyboard.Engine
{
    /// <summary>
    /// 命令组：扁平化命令列表 (对应 osu!droid CommandGroup)
    /// 支持 Loop 展开、按 (StartTime, EndTime) 排序
    /// </summary>
    public class SBCommandGroup
    {
        public List<SBSpriteCommand> Commands;

        private double _cachedStartTime = double.MaxValue;
        private double _cachedEndTime = double.MinValue;
        private bool _startTimeCached;
        private bool _endTimeCached;

        public SBCommandGroup(int capacity = 8)
        {
            Commands = new List<SBSpriteCommand>(capacity);
        }

        public double StartTime()
        {
            if (!_startTimeCached)
            {
                _startTimeCached = true;
                _cachedStartTime = double.MaxValue;
                for (int i = 0; i < Commands.Count; i++)
                {
                    if (Commands[i].StartTime < _cachedStartTime)
                        _cachedStartTime = Commands[i].StartTime;
                }
            }
            return _cachedStartTime;
        }

        public double EndTime()
        {
            if (!_endTimeCached)
            {
                _endTimeCached = true;
                _cachedEndTime = double.MinValue;
                for (int i = 0; i < Commands.Count; i++)
                {
                    double end = Commands[i].EndTime;
                    if (end > _cachedEndTime)
                        _cachedEndTime = end;
                }
            }
            return _cachedEndTime;
        }

        /// <summary>
        /// 按 (StartTime, EndTime) 排序
        /// </summary>
        public void Sort()
        {
            Commands.Sort(CompareCommands);
            InvalidateCache();
        }

        /// <summary>
        /// 将所有 SBLoopCommand 展开为偏移后的普通命令
        /// 递归处理嵌套 Loop
        /// </summary>
        public void ToFlatGroup()
        {
            var expanded = new List<SBSpriteCommand>(Commands.Count);
            for (int i = 0; i < Commands.Count; i++)
            {
                if (Commands[i] is SBLoopCommand loop)
                    ExpandLoop(expanded, loop);
                else
                    expanded.Add(Commands[i]);
            }
            Commands = expanded;
            InvalidateCache();
        }

        public void Clear()
        {
            Commands.Clear();
            InvalidateCache();
        }

        void ExpandLoop(List<SBSpriteCommand> target, SBLoopCommand loop)
        {
            // 递归展开内层 Loop
            loop.InnerGroup.ToFlatGroup();

            var inner = loop.InnerGroup;
            if (inner.Commands.Count == 0) return;

            double innerStart = inner.StartTime();
            double innerEnd = inner.EndTime();
            double loopLength = innerEnd - innerStart;
            if (loopLength <= 0) return;

            double loopTime = loop.StartTime;
            for (int i = 0; i < loop.LoopCount; i++)
            {
                for (int j = 0; j < inner.Commands.Count; j++)
                    target.Add(inner.Commands[j].CreateOffsetCommand(loopTime));
                loopTime += loopLength;
            }
        }

        void InvalidateCache()
        {
            _startTimeCached = false;
            _endTimeCached = false;
        }

        static int CompareCommands(SBSpriteCommand a, SBSpriteCommand b)
        {
            int cmp = a.StartTime.CompareTo(b.StartTime);
            return cmp != 0 ? cmp : a.EndTime.CompareTo(b.EndTime);
        }
    }
}
