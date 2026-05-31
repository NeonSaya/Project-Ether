using System;
using System.Collections.Generic;
using OsuVR.Storyboard;
using OsuVR.Storyboard.Data;

namespace OsuVR.Storyboard.Engine
{
    /// <summary>
    /// 播放图层 (对应 osu!droid PlayingLayer)
    /// 管理活跃 Sprite 的双向链表 + Schedule 事件驱动
    /// 绝不全量遍历所有 Sprite——只遍历当前活跃的
    /// </summary>
    public class SBPlayingLayer
    {
        private SBSchedule _schedule;
        private SBPlayingSprite[] _sprites;

        // 侵入式双向链表哨兵节点
        private SBPlayingSprite _head = new SBPlayingSprite();
        private SBPlayingSprite _tail = new SBPlayingSprite();

        public SBPlayingLayer()
        {
            _schedule = new SBSchedule();
            _head.Next = _tail;
            _tail.Prev = _head;
        }

        /// <summary>
        /// 加载一组 Sprite，注册 Schedule 事件
        /// </summary>
        public void LoadSprites(List<SBElement> elements, Func<SBElement, SBCommandGroup> groupBuilder)
        {
            _sprites = new SBPlayingSprite[elements.Count];
            var tasks = new List<(double time, Action callback)>(elements.Count * 2);
            int totalCmds = 0;

            for (int i = 0; i < elements.Count; i++)
            {
                var element = elements[i];
                var group = groupBuilder(element);
                totalCmds += group.Commands.Count;

                var sprite = new SBPlayingSprite();
                sprite.Load(element, group);
                _sprites[i] = sprite;

                double start = group.StartTime();
                double end = group.EndTime();

                if (start < double.MaxValue)
                {
                    int idx = i;
                    tasks.Add((start, () => AddToActive(idx)));
                    if (end > double.MinValue)
                        tasks.Add((end, () => RemoveFromActive(idx)));
                }
            }

            SBDebugLog.Log($"[PlayingLayer] {elements.Count} sprites → {totalCmds} expanded commands, {tasks.Count} schedule tasks");
            _schedule.SetTasks(tasks);
        }

        /// <summary>
        /// 推进时间: 触发 Schedule 事件 + 遍历活跃链表更新
        /// </summary>
        public void Update(double time)
        {
            _schedule.Update(time);

            var node = _head.Next;
            while (node != _tail)
            {
                node.Update(time);
                node = node.Next;
            }
        }

        /// <summary>
        /// 获取活跃链表头 (用于渲染层遍历)
        /// </summary>
        public SBPlayingSprite ActiveHead => _head.Next;
        public SBPlayingSprite ActiveTail => _tail;

        void AddToActive(int index)
        {
            var sprite = _sprites[index];
            sprite.Prev = _tail.Prev;
            sprite.Next = _tail;
            _tail.Prev.Next = sprite;
            _tail.Prev = sprite;
        }

        void RemoveFromActive(int index)
        {
            var sprite = _sprites[index];
            if (sprite.Prev != null)
            {
                sprite.Prev.Next = sprite.Next;
                sprite.Next.Prev = sprite.Prev;
                sprite.Prev = null;
                sprite.Next = null;
            }
        }

        public void Reset()
        {
            _schedule.Reset();
            // 清空活跃链表
            _head.Next = _tail;
            _tail.Prev = _head;
            // 重置所有 sprite 的链表指针
            if (_sprites != null)
            {
                for (int i = 0; i < _sprites.Length; i++)
                {
                    _sprites[i].Prev = null;
                    _sprites[i].Next = null;
                }
            }
        }

        public void ForEachSprite(System.Action<SBPlayingSprite> action)
        {
            if (_sprites == null) return;
            for (int i = 0; i < _sprites.Length; i++)
                action(_sprites[i]);
        }
    }
}
