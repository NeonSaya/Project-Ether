using System.Collections.Generic;
using UnityEngine;
using OsuVR.Storyboard.Data;

namespace OsuVR.Storyboard.Engine
{
    /// <summary>
    /// 命令时间线基类 (对应 osu!droid CommandHandleTimeline)
    /// 使用前向索引搜索实现 O(1) 均摊查找
    /// </summary>
    public abstract class SBCommandTimeline
    {
        protected List<SBSpriteCommand> _commands;
        protected SBSpriteCommand _currentCommand;
        protected int _currentIndex = -1;

        public int Count => _commands.Count;

        protected SBCommandTimeline(int capacity)
        {
            _commands = new List<SBSpriteCommand>(capacity);
        }

        public void AddCommand(SBSpriteCommand cmd)
        {
            _commands.Add(cmd);
        }

        /// <summary>
        /// 前向索引搜索 + 应用值 (对应 osu!droid CommandHandleTimeline.update)
        /// </summary>
        public void Update(double time)
        {
            int count = _commands.Count;
            if (count == 0) return;

            if (_currentCommand == null)
            {
                _currentCommand = _commands[0];
                _currentIndex = 0;
            }

            // 已超出当前命令范围，向前搜索
            if (_currentIndex < count - 1 && _currentCommand.EndTime < time)
            {
                int idx = _currentIndex + 1;
                while (idx < count && _commands[idx].EndTime < time)
                    idx++;

                if (idx < count)
                {
                    var cmd = _commands[idx];
                    if (cmd.StartTime < time)
                    {
                        _currentCommand = cmd;
                    }
                    else
                    {
                        idx--;
                        _currentCommand = _commands[idx];
                    }
                    _currentIndex = idx;
                }
                else
                {
                    _currentIndex = count - 1;
                    _currentCommand = _commands[_currentIndex];
                }
            }

            ApplyValue(time);
        }

        protected abstract void ApplyValue(double time);

        public void Reset()
        {
            _currentCommand = null;
            _currentIndex = -1;
        }
    }

    /// <summary>
    /// 浮点时间线: Alpha, X, Y, ScaleX, ScaleY, Rotation
    /// </summary>
    public class SBFloatTimeline : SBCommandTimeline
    {
        public float CurrentValue;

        public SBFloatTimeline(int capacity = 4) : base(capacity) { }

        protected override void ApplyValue(double time)
        {
            var cmd = (SBFloatCommand)_currentCommand;
            if (cmd.EndTime < time)
            {
                CurrentValue = cmd.EndValue;
            }
            else
            {
                float p = cmd.GetProgress(time);
                CurrentValue = cmd.StartValue + (cmd.EndValue - cmd.StartValue) * p;
            }
        }
    }

    /// <summary>
    /// 颜色时间线: Color
    /// </summary>
    public class SBColorTimeline : SBCommandTimeline
    {
        public Color32 CurrentValue = new Color32(255, 255, 255, 255);

        public SBColorTimeline(int capacity = 4) : base(capacity) { }

        protected override void ApplyValue(double time)
        {
            var cmd = (SBColorCommand)_currentCommand;
            if (cmd.EndTime < time)
            {
                CurrentValue = cmd.EndValue;
            }
            else
            {
                float p = cmd.GetProgress(time);
                byte r = (byte)(cmd.StartValue.r + (cmd.EndValue.r - cmd.StartValue.r) * p);
                byte g = (byte)(cmd.StartValue.g + (cmd.EndValue.g - cmd.StartValue.g) * p);
                byte b = (byte)(cmd.StartValue.b + (cmd.EndValue.b - cmd.StartValue.b) * p);
                CurrentValue = new Color32(r, g, b, 255);
            }
        }
    }

    /// <summary>
    /// 布尔时间线: BlendingMode, FlipH, FlipV
    /// </summary>
    public class SBBoolTimeline : SBCommandTimeline
    {
        public bool CurrentValue;

        public SBBoolTimeline(int capacity = 2) : base(capacity) { }

        protected override void ApplyValue(double time)
        {
            var cmd = (SBBoolCommand)_currentCommand;
            if (cmd.EndTime < time)
                CurrentValue = cmd.EndValue;
            else if (time >= cmd.StartTime)
                CurrentValue = cmd.StartValue;
        }
    }
}
