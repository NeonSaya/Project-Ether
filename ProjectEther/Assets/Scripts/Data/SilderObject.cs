using System.Collections.Generic;
using UnityEngine;

namespace OsuVR
{
    /// <summary>
    /// 表示一个滑条
    /// </summary>
    public class SliderObject : HitObject
    {
        /// <summary>
        /// 滑条路径类型
        /// </summary>
        public CurveType CurveType { get; set; }

        /// <summary>
        /// 滑条的控制点（相对于滑条起点）
        /// </summary>
        public List<Vector2> ControlPoints { get; set; }
        
        // 添加此属性用于存储滑条路径点
        public List<Vector2> PathPoints { get; set; }
  

        /// <summary>
        /// 滑条重复次数（折返次数）
        /// </summary>
        public int RepeatCount { get; set; }

        /// <summary>
        /// 滑条的像素长度
        /// </summary>
        public double PixelLength { get; set; }

        /// <summary>
        /// 滑条结束位置（计算缓存）
        /// </summary>
        private Vector2? _endPositionCache;

        /// <summary>
        /// 滑条跨数（重复次数+1）
        /// </summary>
        public int SpanCount => RepeatCount + 1;

        /// <summary>
        /// 滑条结束时间（根据像素长度和速度计算）
        /// </summary>
        public override double EndTime => StartTime + SpanCount * PixelLength / Velocity;

        /// <summary>
        /// 滑条速度（像素/毫秒）
        /// </summary>
        public double Velocity { get; set; } = 1.0;

        /// <summary>
        /// 滑条结束位置
        /// </summary>
        public override Vector2 EndPosition
        {
            get
            {
                if (!_endPositionCache.HasValue)
                {
                    // 计算滑条结束位置（根据曲线类型和控制点）
                    _endPositionCache = CalculateEndPosition();
                }
                return _endPositionCache.Value;
            }
        }

        /// <summary>
        /// 难度计算中的堆叠结束位置
        /// </summary>
        public override Vector2 DifficultyStackedEndPosition => EndPosition + DifficultyStackOffset;

        /// <summary>
        /// 游戏玩法中的堆叠结束位置
        /// </summary>
        public override Vector2 GameplayStackedEndPosition => EndPosition + GameplayStackOffset;

        /// <summary>
        /// 屏幕空间中的游戏玩法堆叠结束位置
        /// </summary>
        public override Vector2 ScreenSpaceGameplayStackedEndPosition =>
            ConvertPositionToRealCoordinates(GameplayStackedEndPosition);

        /// <summary>
        /// 滑条节点的音效列表
        /// </summary>
        public List<List<HitSampleInfo>> NodeSamples { get; set; }

        /// <summary>
        /// 滑条Tick距离乘数
        /// </summary>
        public double TickDistanceMultiplier { get; set; } = 1.0;

        /// <summary>
        /// 是否生成Tick
        /// </summary>
        public bool GenerateTicks { get; set; } = true;

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

        /// <summary>
        /// 计算滑条结束位置
        /// </summary>
        private Vector2 CalculateEndPosition()
        {
            if (ControlPoints.Count == 0)
                return Position;

            // 简单实现：对于线性滑条，结束位置是最后一个控制点
            // 实际osu中需要根据曲线类型计算
            Vector2 lastControlPoint = ControlPoints[ControlPoints.Count - 1];

            // 如果是折返滑条，需要考虑折返方向
            if (RepeatCount % 2 == 0)
            {
                // 偶数次重复，结束位置在起点
                return Position;
            }
            else
            {
                // 奇数次重复，结束位置在最后一个控制点
                return Position + lastControlPoint;
            }
        }

        /// <summary>
        /// 获取滑条在指定进度的位置
        /// </summary>
        /// <param name="progress">进度（0-1，0为起点，1为终点）</param>
        /// <returns>位置</returns>
        public Vector2 GetPositionAtProgress(double progress)
        {
            // 简化实现：线性插值
            // 实际osu中需要根据曲线类型进行更复杂的计算

            if (ControlPoints.Count < 2)
                return Position;

            // 计算在哪个线段上
            double totalLength = 0;
            List<double> segmentLengths = new List<double>();

            for (int i = 0; i < ControlPoints.Count - 1; i++)
            {
                float length = Vector2.Distance(ControlPoints[i], ControlPoints[i + 1]);
                segmentLengths.Add(length);
                totalLength += length;
            }

            if (totalLength == 0)
                return Position;

            // 找到目标线段
            double targetLength = progress * totalLength;
            double accumulatedLength = 0;

            for (int i = 0; i < segmentLengths.Count; i++)
            {
                if (accumulatedLength + segmentLengths[i] >= targetLength)
                {
                    // 在这个线段上
                    double segmentProgress = (targetLength - accumulatedLength) / segmentLengths[i];
                    Vector2 startPoint = Position + ControlPoints[i];
                    Vector2 endPoint = Position + ControlPoints[i + 1];

                    return Vector2.Lerp(startPoint, endPoint, (float)segmentProgress);
                }

                accumulatedLength += segmentLengths[i];
            }

            // 如果超出范围，返回最后一个点
            return Position + ControlPoints[ControlPoints.Count - 1];
        }

        /// <summary>
        /// 获取滑条跨的进度
        /// </summary>
        /// <param name="progress">总体进度（0-1）</param>
        /// <returns>当前跨内的进度（0-1）</returns>
        public double GetSpanProgress(double progress)
        {
            double spanProgress = progress * SpanCount % 1.0;

            // 如果是反向跨，需要反转进度
            int currentSpan = GetCurrentSpan(progress);
            if (currentSpan % 2 == 1)
            {
                spanProgress = 1.0 - spanProgress;
            }

            return spanProgress;
        }

        /// <summary>
        /// 获取当前跨索引
        /// </summary>
        /// <param name="progress">总体进度（0-1）</param>
        /// <returns>跨索引（0到SpanCount-1）</returns>
        public int GetCurrentSpan(double progress)
        {
            return (int)(progress * SpanCount);
        }

        /// <summary>
        /// 获取滑条持续时间
        /// </summary>
        public double GetSpanDuration()
        {
            return Duration / SpanCount;
        }

        /// <summary>
        /// 应用默认设置
        /// </summary>
        public override void ApplyDefaults(GameMode mode)
        {
            base.ApplyDefaults(mode);

            // 滑条特有的默认设置
            // 这里可以添加滑条速度计算等逻辑
        }

        /// <summary>
        /// 重新计算结束位置（当控制点改变时调用）
        /// </summary>
        public void RecalculateEndPosition()
        {
            _endPositionCache = null;
        }
    }
}