using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace OsuVR
{
    // [新增] 定义滑条内部的事件类型
    public enum SliderEventType
    {
        Tick,
        Repeat,
        Tail
    }

    // [新增] 滑条嵌套物件（代表滑条过程中的每一个判定点）
    public class SliderNestedObject
    {
        public double Time;          // 判定时间
        public Vector2 Position;     // 判定发生时的球位置（相对坐标）
        public SliderEventType Type; // 类型
        public int SpanIndex;        // 属于第几个跨度
        public bool IsHit;           // 是否已击中（运行时状态）
    }
    /// <summary>
    /// 表示一个滑条击打对象
    /// </summary>
    public class SliderObject : HitObject
    {
        #region 核心属性

        /// <summary>
        /// 滑条的曲线类型 (Bezier, Linear, Perfect, Catmull)
        /// </summary>
        public CurveType CurveType { get; set; }

        /// <summary>
        /// 定义曲线的原始控制点（相对于滑条起点 Position）
        /// </summary>
        public List<Vector2> ControlPoints { get; set; }

        private List<Vector2> _pathPoints;
        /// <summary>
        /// 计算后的真实路径点（相对于滑条起点 Position）
        /// </summary>
        public List<Vector2> PathPoints
        {
            get => _pathPoints;
            set
            {
                _pathPoints = value;
                // 当路径更新时，重新计算长度缓存和结束位置
                RecalculatePathCache();
                _endPositionCache = null; 
            }
        }

        /// <summary>
        /// 滑条总跨数（即 osu! 中的 repeat count，1=单次，2=往返）
        /// </summary>
        public int RepeatCount { get; set; }

        /// <summary>
        /// 为了代码可读性，提供 SpanCount 别名
        /// </summary>
        public int SpanCount => RepeatCount;

        /// <summary>
        /// 滑条预期的像素长度
        /// </summary>
        public double PixelLength { get; set; }

        /// <summary>
        /// 滑条持续时间（毫秒）
        /// </summary>
        public double Duration { get; set; }

        /// <summary>
        /// 滑条结束时间
        /// </summary>
        public double EndTime
        {
            get => StartTime + Duration;
            set => Duration = value - StartTime;
        }

        /// <summary>
        /// 滑条速度（osu! 像素/毫秒），用于难度计算
        /// </summary>
        public double Velocity { get; set; } = 1.0;

        // [新增] 存储所有 Tick (检查点) 的时间
        public List<SliderNestedObject> NestedHitObjects { get; private set; } = new List<SliderNestedObject>();

        /// <summary>
        /// 滑条节点的音效列表 [节点索引][音效列表]
        /// </summary>
        public List<List<HitSampleInfo>> NodeSamples { get; set; } = new List<List<HitSampleInfo>>();

        #endregion

        #region 缓存与计算属性

        private Vector2? _endPositionCache;
        // 预计算的路径累计长度，用于二分查找位置
        private float[] _cumulativeLengths;
        private float _totalPathDistance;

        /// <summary>
        /// 获取滑条最终的结束位置（考虑了折返）
        /// </summary>
        public override Vector2 EndPosition
        {
            get
            {
                if (!_endPositionCache.HasValue)
                {
                    _endPositionCache = CalculateEndPosition();
                }
                return _endPositionCache.Value;
            }
        }

        #endregion

        #region 构造函数

        /// <summary>
        /// 构造函数
        /// </summary>
        public SliderObject(
            double startTime,
            Vector2 position,
            CurveType curveType,
            List<Vector2> controlPoints,
            int repeatCount,
            double pixelLength,
            bool isNewCombo,
            int comboOffset)
            : base(startTime, position, HitObjectType.Slider, isNewCombo, comboOffset)
        {
            CurveType = curveType;
            ControlPoints = controlPoints ?? new List<Vector2>();
            RepeatCount = repeatCount;
            PixelLength = pixelLength;
            NodeSamples = new List<List<HitSampleInfo>>();

            // 确保控制点列表中包含起点（0,0）
            if (ControlPoints.Count == 0 || ControlPoints[0] != Vector2.zero)
            {
                ControlPoints.Insert(0, Vector2.zero);
            }
        }

        #endregion

        #region 核心逻辑

        /// <summary>
        /// 重新计算路径长度缓存
        /// </summary>
        private void RecalculatePathCache()
        {
            if (_pathPoints == null || _pathPoints.Count < 2)
            {
                _cumulativeLengths = new float[] { 0 };
                _totalPathDistance = 0;
                return;
            }

            int count = _pathPoints.Count;
            _cumulativeLengths = new float[count];
            _cumulativeLengths[0] = 0;
            _totalPathDistance = 0;

            for (int i = 0; i < count - 1; i++)
            {
                float dist = Vector2.Distance(_pathPoints[i], _pathPoints[i + 1]);
                _totalPathDistance += dist;
                _cumulativeLengths[i + 1] = _totalPathDistance;
            }
        }

        /// <summary>
        /// 计算滑条结束时的世界坐标（考虑折返）
        /// </summary>
        private Vector2 CalculateEndPosition()
        {
            // 如果没有路径点，回退到起点
            if (_pathPoints == null || _pathPoints.Count == 0)
                return Position;

            // 获取单次路径的终点（相对于 Position）
            Vector2 pathEnd = _pathPoints[_pathPoints.Count - 1];

            // 偶数次跨越 (A->B->A)，终点在起点
            if (SpanCount % 2 == 0)
            {
                return Position;
            }
            // 奇数次跨越 (A->B)，终点在路径末端
            else
            {
                return Position + pathEnd;
            }
        }

        /// <summary>
        /// 获取滑条在指定进度的位置（基于真实计算路径）
        /// </summary>
        /// <param name="progress">总体进度 (0-1)，包含所有折返</param>
        public Vector2 GetPositionAtProgress(double progress)
        {
            if (_pathPoints == null || _pathPoints.Count == 0) return Position;
            if (_pathPoints.Count == 1) return Position + _pathPoints[0];

            // 1. 计算当前跨内的进度 (0~1)
            double spanFullProgress = progress * SpanCount;
            int currentSpanIndex = (int)spanFullProgress;
            double currentSpanProgress = spanFullProgress - currentSpanIndex;

            // 边界处理：正好结束时
            if (currentSpanIndex >= SpanCount)
            {
                currentSpanIndex = SpanCount - 1;
                currentSpanProgress = 1.0;
            }

            // 2. 处理折返 (奇数跨度 A<-B 需要反转进度)
            if (currentSpanIndex % 2 != 0)
            {
                currentSpanProgress = 1.0 - currentSpanProgress;
            }

            // 3. 在路径上查找位置
            return Position + GetPositionOnPath((float)currentSpanProgress);
        }

        /// <summary>
        /// 在单次路径上查找位置 (使用二分查找优化)
        /// </summary>
        /// <param name="t">单次路径进度 (0-1)</param>
        public Vector2 GetPositionOnPath(float t)
        {
            if (_cumulativeLengths == null || _cumulativeLengths.Length == 0) return _pathPoints[0];
            
            t = Mathf.Clamp01(t);
            float targetDist = t * _totalPathDistance;

            // 二分查找
            int index = System.Array.BinarySearch(_cumulativeLengths, targetDist);
            if (index < 0) index = ~index; // 获取插入点

            // 处理边界
            if (index <= 0) return _pathPoints[0];
            if (index >= _cumulativeLengths.Length) return _pathPoints[_pathPoints.Count - 1];

            // 在 index-1 和 index 之间插值
            int iA = index - 1;
            int iB = index;
            
            float distA = _cumulativeLengths[iA];
            float distB = _cumulativeLengths[iB];
            float segmentLen = distB - distA;

            if (segmentLen <= 0.0001f) return _pathPoints[iA];

            float segmentT = (targetDist - distA) / segmentLen;
            return Vector2.Lerp(_pathPoints[iA], _pathPoints[iB], segmentT);
        }

        #endregion

        /// <summary>
        /// 更新 Combo 信息 (例如堆叠处理)
        /// </summary>
        public void UpdateComboInformation(HitObject previousObject)
        {
            // 这里可以实现 osu! 的堆叠逻辑 (Stacking)
            // 需要判断位置是否重叠、时间间隔是否足够小
            // 如果重叠，增加 StackHeight
        }
        /// <summary>
        /// [核心重写] 按照 osu! 逻辑生成所有嵌套判定物件 (Ticks, Repeats, Tail)
        /// </summary>
        public void CalculateNestedHitObjects(double tickRate, double beatLength)
        {
            NestedHitObjects.Clear();
            if (tickRate <= 0 || beatLength <= 0 || RepeatCount == 0) return;

            // 1. 计算 Tick 间隔
            double tickInterval = beatLength / tickRate;
            // 2. 单次滑行的时间
            double spanDuration = Duration / RepeatCount;
            // 3. 最小 Tick 距离 (防止 Tick 离头尾太近)
            double minTickDistanceFromEnd = 0.01 * spanDuration;

            for (int span = 0; span < RepeatCount; span++)
            {
                double spanStartTime = StartTime + (span * spanDuration);
                bool reversed = span % 2 == 1;

                // --- A. 生成 Ticks ---
                // Ticks 是基于长度生成的，这里简化为基于时间
                double currentTickTime = tickInterval;

                while (currentTickTime < spanDuration - minTickDistanceFromEnd)
                {
                    double absoluteTime = spanStartTime + currentTickTime;

                    // 计算 Tick 的位置 (用于特效生成等)
                    double progressInSpan = currentTickTime / spanDuration;
                    // 如果是反向跨度，位置也要反过来算
                    if (reversed) progressInSpan = 1.0 - progressInSpan;

                    Vector2 pos = GetPositionAtProgress(progressInSpan); // 这里是简化调用，实际需要 GetPositionOnPath

                    NestedHitObjects.Add(new SliderNestedObject
                    {
                        Time = absoluteTime,
                        Type = SliderEventType.Tick,
                        SpanIndex = span,
                        Position = pos,
                        IsHit = false
                    });

                    currentTickTime += tickInterval;
                }

                // --- B. 生成 Repeat (折返点) 或 Tail (终点) ---
                double spanEndTime = spanStartTime + spanDuration;
                Vector2 endPos = reversed ? Vector2.zero : PathPoints.Last(); // 简化：偶数次终点在末端，奇数次在起点

                if (span < RepeatCount - 1)
                {
                    // 这是一个折返点 (Repeat)
                    NestedHitObjects.Add(new SliderNestedObject
                    {
                        Time = spanEndTime,
                        Type = SliderEventType.Repeat,
                        SpanIndex = span,
                        Position = endPos,
                        IsHit = false
                    });
                }
                else
                {
                    // 最后一个跨度，这是整个滑条的终点 (Tail)
                    NestedHitObjects.Add(new SliderNestedObject
                    {
                        Time = spanEndTime,
                        Type = SliderEventType.Tail,
                        SpanIndex = span,
                        Position = endPos,
                        IsHit = false
                    });
                }
            }

            // 确保按时间排序 (理论上已经是排序的，但为了保险)
            NestedHitObjects.Sort((a, b) => a.Time.CompareTo(b.Time));
        }

        // 辅助：获取进度位置 (需要配合 SliderPathCalculator，这里假设你有类似逻辑)
        // 注意：这里的 progress 是 0~1 对应整个 PathPoints
    }
}