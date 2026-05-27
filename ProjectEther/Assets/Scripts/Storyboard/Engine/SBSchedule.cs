using System;
using System.Collections.Generic;

namespace OsuVR.Storyboard.Engine
{
    /// <summary>
    /// 事件调度器 (对应 osu!droid Schedule)
    /// 按时间戳排序的任务队列，O(1) 均摊触发
    /// 绝不全量遍历——只在时间推进到任务时间点时触发回调
    /// </summary>
    public class SBSchedule
    {
        private struct Task
        {
            public double Time;
            public Action Callback;
        }

        private List<Task> _tasks;
        private int _currentIndex;

        public SBSchedule(int capacity = 64)
        {
            _tasks = new List<Task>(capacity);
        }

        /// <summary>
        /// 设置任务列表 (自动按时间排序)
        /// </summary>
        public void SetTasks(List<(double time, Action callback)> tasks)
        {
            _tasks.Clear();
            for (int i = 0; i < tasks.Count; i++)
                _tasks.Add(new Task { Time = tasks[i].time, Callback = tasks[i].callback });
            _tasks.Sort((a, b) => a.Time.CompareTo(b.Time));
            _currentIndex = 0;
        }

        /// <summary>
        /// 推进时间，触发所有到期任务
        /// </summary>
        public void Update(double time)
        {
            while (_currentIndex < _tasks.Count && _tasks[_currentIndex].Time <= time)
            {
                _tasks[_currentIndex].Callback();
                _currentIndex++;
            }
        }

        /// <summary>
        /// 重置到起始位置 (用于重新播放)
        /// </summary>
        public void Reset()
        {
            _currentIndex = 0;
        }
    }
}
