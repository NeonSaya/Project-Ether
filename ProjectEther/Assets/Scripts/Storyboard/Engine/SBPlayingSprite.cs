using UnityEngine;
using OsuVR.Storyboard.Data;

namespace OsuVR.Storyboard.Engine
{
    /// <summary>
    /// 运行时 Sprite (对应 osu!droid PlayingSprite)
    /// 持有 10 条 CommandTimeline (每个 Target 一条)，输出 SBRenderState
    /// 绝不引用任何 Unity 组件
    /// </summary>
    public class SBPlayingSprite
    {
        // ---- 侵入式双向链表 (O(1) add/remove) ----
        internal SBPlayingSprite Prev;
        internal SBPlayingSprite Next;

        // ---- 静态数据 (来自 SBElement) ----
        public SBElement Element;

        // ---- 动态渲染状态 (每帧由 Timeline 更新) ----
        public SBRenderState State;

        // ---- 每 Target 一条时间线 ----
        private SBFloatTimeline _alpha;
        private SBFloatTimeline _x;
        private SBFloatTimeline _y;
        private SBFloatTimeline _scaleX;
        private SBFloatTimeline _scaleY;
        private SBFloatTimeline _rotation;
        private SBColorTimeline _color;
        private SBBoolTimeline _blending;
        private SBBoolTimeline _flipH;
        private SBBoolTimeline _flipV;

        /// <summary>
        /// 从 SBElement + SBCommandGroup 加载
        /// </summary>
        public void Load(SBElement element, SBCommandGroup group)
        {
            Element = element;

            // 初始化默认渲染状态
            State = SBRenderState.Default;
            State.X = element.InitialPosition.x;
            State.Y = element.InitialPosition.y;

            // 创建时间线
            _alpha = null; _x = null; _y = null;
            _scaleX = null; _scaleY = null; _rotation = null;
            _color = null; _blending = null; _flipH = null; _flipV = null;

            for (int i = 0; i < group.Commands.Count; i++)
            {
                var cmd = group.Commands[i];
                GetOrCreateTimeline(cmd.Target).AddCommand(cmd);
            }

            // 在起始时间评估一次
            double startTime = group.StartTime();
            if (startTime < double.MaxValue)
                Update(startTime);
        }

        /// <summary>
        /// 评估所有时间线，更新 State
        /// </summary>
        public void Update(double time)
        {
            _alpha?.Update(time);
            _x?.Update(time);
            _y?.Update(time);
            _scaleX?.Update(time);
            _scaleY?.Update(time);
            _rotation?.Update(time);
            _color?.Update(time);
            _blending?.Update(time);
            _flipH?.Update(time);
            _flipV?.Update(time);

            // 将时间线输出写入 State
            if (_alpha != null) State.Alpha = _alpha.CurrentValue;
            if (_x != null) State.X = _x.CurrentValue;
            if (_y != null) State.Y = _y.CurrentValue;
            if (_scaleX != null) State.ScaleX = _scaleX.CurrentValue;
            if (_scaleY != null) State.ScaleY = _scaleY.CurrentValue;
            if (_rotation != null) State.Rotation = _rotation.CurrentValue;
            if (_color != null)
            {
                State.R = _color.CurrentValue.r / 255f;
                State.G = _color.CurrentValue.g / 255f;
                State.B = _color.CurrentValue.b / 255f;
            }
            if (_blending != null) State.Additive = _blending.CurrentValue;
            if (_flipH != null) State.FlipH = _flipH.CurrentValue;
            if (_flipV != null) State.FlipV = _flipV.CurrentValue;
        }

        SBCommandTimeline GetOrCreateTimeline(SBCommandTarget target)
        {
            switch (target)
            {
                case SBCommandTarget.Alpha:       return _alpha   ?? (_alpha   = new SBFloatTimeline());
                case SBCommandTarget.X:            return _x      ?? (_x      = new SBFloatTimeline());
                case SBCommandTarget.Y:            return _y      ?? (_y      = new SBFloatTimeline());
                case SBCommandTarget.ScaleX:       return _scaleX  ?? (_scaleX  = new SBFloatTimeline());
                case SBCommandTarget.ScaleY:       return _scaleY  ?? (_scaleY  = new SBFloatTimeline());
                case SBCommandTarget.Rotation:     return _rotation ?? (_rotation = new SBFloatTimeline());
                case SBCommandTarget.Color:        return _color   ?? (_color   = new SBColorTimeline());
                case SBCommandTarget.BlendingMode: return _blending ?? (_blending = new SBBoolTimeline());
                case SBCommandTarget.FlipH:        return _flipH   ?? (_flipH   = new SBBoolTimeline());
                case SBCommandTarget.FlipV:        return _flipV   ?? (_flipV   = new SBBoolTimeline());
                default: return null;
            }
        }
    }
}
