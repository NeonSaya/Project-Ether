using System.Collections.Generic;
using UnityEngine;

namespace OsuVR
{
    /// <summary>
    /// 表示一个击打对象（基类）
    /// </summary>
    public abstract class HitObject
    {
        // 常量定义
        public const float OBJECT_RADIUS = 64f;
        public const int CONTROL_POINT_LENIENCY = 5;
        public const double PREEMPT_MAX = 1800.0;
        public const double PREEMPT_MID = 1200.0;
        public const double PREEMPT_MIN = 450.0;

        /// <summary>
        /// 击打对象开始的时间（毫秒）
        /// </summary>
        public readonly double StartTime;

        /// <summary>
        /// 击打对象的位置（osu!像素坐标）
        /// </summary>
        public Vector2 Position
        {
            get => _position;
            set
            {
                _position = value;
                // 位置改变时需要刷新缓存
                _difficultyStackedPositionCache = null;
                _gameplayStackedPositionCache = null;
            }
        }
        private Vector2 _position;

        /// <summary>
        /// 击打对象的类型
        /// </summary>
        public HitObjectType ObjectType { get; protected set; }

        /// <summary>
        /// 是否开始新连击
        /// </summary>
        public readonly bool IsNewCombo;

        /// <summary>
        /// 连击偏移量
        /// </summary>
        public readonly int ComboOffset;

        /// <summary>
        /// 击打对象的结束时间（虚拟属性，子类需要重写）
        /// </summary>
        public virtual double EndTime => StartTime;

        /// <summary>
        /// 击打对象的持续时间（毫秒）
        /// </summary>
        public double Duration;

        /// <summary>
        /// 击打对象的结束位置（虚拟属性，子类需要重写）
        /// </summary>
        public virtual Vector2 EndPosition => Position;

        /// <summary>
        /// 在当前连击中的索引
        /// </summary>
        public int IndexInCurrentCombo { get; internal set; }

        /// <summary>
        /// 在谱面中的连击索引
        /// </summary>
        public int ComboIndex { get; internal set; }

        /// <summary>
        /// 应用了偏移后的连击索引
        /// </summary>
        public int ComboIndexWithOffsets { get; internal set; }

        /// <summary>
        /// 是否当前连击的最后一个对象
        /// </summary>
        public bool IsLastInCombo { get; internal set; }

        /// <summary>
        /// 圆圈出现时间（毫秒）
        /// </summary>
        public double TimePreempt = 600.0;

        /// <summary>
        /// 圆圈淡入时间（毫秒）
        /// </summary>
        public double TimeFadeIn = 400.0;

        /// <summary>
        /// 击打时播放的音效列表
        /// </summary>
        public List<HitSampleInfo> Samples = new List<HitSampleInfo>();

        /// <summary>
        /// 任何可能被此[击打对象]使用的非标准样本
        /// </summary>
        public List<SequenceHitSampleInfo> AuxiliarySamples = new List<SequenceHitSampleInfo>();

        /// <summary>
        /// 是否处于Kiai时间
        /// </summary>
        public bool Kiai = false;

        /// <summary>
        /// 判定窗口
        /// </summary>
        public HitWindow HitWindow;

        /// <summary>
        /// 是否谱面第一个音符
        /// </summary>
        public bool IsFirstNote => ComboIndex == 1 && IndexInCurrentCombo == 0;

        /// <summary>
        /// 堆叠偏移乘数
        /// </summary>
        public float StackOffsetMultiplier
        {
            get => _stackOffsetMultiplier;
            set
            {
                if (_stackOffsetMultiplier != value)
                {
                    _stackOffsetMultiplier = value;
                    // 刷新缓存
                    _difficultyStackOffsetCache = null;
                    _difficultyStackedPositionCache = null;
                    _gameplayStackOffsetCache = null;
                    _gameplayStackedPositionCache = null;
                }
            }
        }
        private float _stackOffsetMultiplier;

        // 难度计算相关属性
        private int _difficultyStackHeight;
        private float _difficultyScale;
        private Vector2? _difficultyStackOffsetCache;
        private Vector2? _difficultyStackedPositionCache;

        /// <summary>
        /// 难度计算中的半径
        /// </summary>
        public double DifficultyRadius => OBJECT_RADIUS * DifficultyScale;

        /// <summary>
        /// 难度计算中的堆叠偏移
        /// </summary>
        public Vector2 DifficultyStackOffset
        {
            get
            {
                if (!_difficultyStackOffsetCache.HasValue)
                {
                    float offset = _difficultyStackHeight * DifficultyScale * StackOffsetMultiplier;
                    _difficultyStackOffsetCache = new Vector2(offset, offset);
                }
                return _difficultyStackOffsetCache.Value;
            }
        }

        /// <summary>
        /// 难度计算中的堆叠位置
        /// </summary>
        public virtual Vector2 DifficultyStackedPosition
        {
            get
            {
                if (!_difficultyStackedPositionCache.HasValue)
                {
                    _difficultyStackedPositionCache = Position + DifficultyStackOffset;
                }
                return _difficultyStackedPositionCache.Value;
            }
        }

        /// <summary>
        /// 难度计算中的堆叠结束位置（虚拟属性，子类需要重写）
        /// </summary>
        public virtual Vector2 DifficultyStackedEndPosition => DifficultyStackedPosition;

        // 游戏玩法相关属性
        private int _gameplayStackHeight;
        private float _gameplayScale;
        private Vector2? _gameplayStackOffsetCache;
        private Vector2? _gameplayStackedPositionCache;
        private Vector2? _screenSpaceGameplayStackedPositionCache;

        /// <summary>
        /// 游戏玩法中的半径
        /// </summary>
        public double GameplayRadius => OBJECT_RADIUS * GameplayScale;

        /// <summary>
        /// 屏幕空间中的游戏玩法缩放
        /// </summary>
        public float ScreenSpaceGameplayScale => GameplayScale * 720f / 480f; // 假设屏幕高度720

        /// <summary>
        /// 屏幕空间中的游戏玩法半径
        /// </summary>
        public double ScreenSpaceGameplayRadius => OBJECT_RADIUS * ScreenSpaceGameplayScale;

        /// <summary>
        /// 游戏玩法中的堆叠偏移
        /// </summary>
        public Vector2 GameplayStackOffset
        {
            get
            {
                if (!_gameplayStackOffsetCache.HasValue)
                {
                    float offset = _gameplayStackHeight * GameplayScale * StackOffsetMultiplier;
                    _gameplayStackOffsetCache = new Vector2(offset, offset);
                }
                return _gameplayStackOffsetCache.Value;
            }
        }

        /// <summary>
        /// 游戏玩法中的堆叠位置
        /// </summary>
        public virtual Vector2 GameplayStackedPosition
        {
            get
            {
                if (!_gameplayStackedPositionCache.HasValue)
                {
                    _gameplayStackedPositionCache = Position + GameplayStackOffset;
                }
                return _gameplayStackedPositionCache.Value;
            }
        }

        /// <summary>
        /// 游戏玩法中的堆叠结束位置（虚拟属性，子类需要重写）
        /// </summary>
        public virtual Vector2 GameplayStackedEndPosition => GameplayStackedPosition;

        /// <summary>
        /// 屏幕空间中的游戏玩法位置
        /// </summary>
        public Vector2 ScreenSpaceGameplayPosition => ConvertPositionToRealCoordinates(Position);

        /// <summary>
        /// 屏幕空间中的游戏玩法堆叠位置
        /// </summary>
        public virtual Vector2 ScreenSpaceGameplayStackedPosition
        {
            get
            {
                if (!_screenSpaceGameplayStackedPositionCache.HasValue)
                {
                    _screenSpaceGameplayStackedPositionCache = ConvertPositionToRealCoordinates(GameplayStackedPosition);
                }
                return _screenSpaceGameplayStackedPositionCache.Value;
            }
        }

        /// <summary>
        /// 屏幕空间中的游戏玩法堆叠结束位置（虚拟属性，子类需要重写）
        /// </summary>
        public virtual Vector2 ScreenSpaceGameplayStackedEndPosition => ScreenSpaceGameplayStackedPosition;

        // 构造函数
        protected HitObject(double startTime, Vector2 position, HitObjectType type, bool isNewCombo, int comboOffset)
        {
            StartTime = startTime;
            _position = position;
            ObjectType = type;
            IsNewCombo = isNewCombo;
            ComboOffset = comboOffset;
        }

        /// <summary>
        /// 难度计算堆叠高度
        /// </summary>
        public int DifficultyStackHeight
        {
            get => _difficultyStackHeight;
            set
            {
                if (_difficultyStackHeight != value)
                {
                    _difficultyStackHeight = value;
                    _difficultyStackOffsetCache = null;
                    _difficultyStackedPositionCache = null;
                }
            }
        }

        /// <summary>
        /// 难度计算缩放
        /// </summary>
        public float DifficultyScale
        {
            get => _difficultyScale;
            set
            {
                if (_difficultyScale != value)
                {
                    _difficultyScale = value;
                    _difficultyStackOffsetCache = null;
                    _difficultyStackedPositionCache = null;
                }
            }
        }

        /// <summary>
        /// 游戏玩法堆叠高度
        /// </summary>
        public int GameplayStackHeight
        {
            get => _gameplayStackHeight;
            set
            {
                if (_gameplayStackHeight != value)
                {
                    _gameplayStackHeight = value;
                    _gameplayStackOffsetCache = null;
                    _gameplayStackedPositionCache = null;
                }
            }
        }

        /// <summary>
        /// 游戏玩法缩放
        /// </summary>
        public float GameplayScale
        {
            get => _gameplayScale;
            set
            {
                if (_gameplayScale != value)
                {
                    _gameplayScale = value;
                    _gameplayStackOffsetCache = null;
                    _gameplayStackedPositionCache = null;
                    _screenSpaceGameplayStackedPositionCache = null;
                }
            }
        }

        /// <summary>
        /// 应用默认设置
        /// </summary>
        public virtual void ApplyDefaults(GameMode mode)
        {
            // 这里可以添加默认设置逻辑
            // 根据模式设置堆叠偏移乘数
            StackOffsetMultiplier = mode switch
            {
                GameMode.Droid => -4f,
                GameMode.Standard => -6.4f,
                _ => -6.4f
            };
        }

        /// <summary>
        /// 更新连击信息
        /// </summary>
        public void UpdateComboInformation(HitObject lastObj)
        {
            ComboIndex = lastObj?.ComboIndex ?? 0;
            ComboIndexWithOffsets = lastObj?.ComboIndexWithOffsets ?? 0;
            IndexInCurrentCombo = lastObj != null ? lastObj.IndexInCurrentCombo + 1 : 0;

            if (IsNewCombo || lastObj == null || lastObj is SpinnerObject)
            {
                IndexInCurrentCombo = 0;
                ComboIndex++;

                // 旋转圆圈不影响连击颜色偏移
                if (!(this is SpinnerObject))
                {
                    ComboIndexWithOffsets += ComboOffset + 1;
                }

                if (lastObj != null)
                {
                    lastObj.IsLastInCombo = true;
                }
            }
        }

        /// <summary>
        /// 将osu!像素坐标转换为真实屏幕坐标
        /// </summary>
        protected Vector2 ConvertPositionToRealCoordinates(Vector2 position)
        {
            // 将位置缩放到屏幕上的实际游玩区域大小
            float scaleX = 640f / 512f;  // 假设实际宽度640，原始宽度512
            float scaleY = 480f / 384f;  // 假设实际高度480，原始高度384

            Vector2 scaledPosition = new Vector2(
                position.x * scaleX,
                position.y * scaleY
            );

            // 将位置居中到屏幕
            float screenWidth = 1920f;  // 假设屏幕宽度1920
            float screenHeight = 1080f; // 假设屏幕高度1080

            Vector2 centeredPosition = new Vector2(
                scaledPosition.x + (screenWidth - 640f) / 2f,
                scaledPosition.y + (screenHeight - 480f) / 2f
            );

            return centeredPosition;
        }

        /// <summary>
        /// 获取击打对象的字符串表示
        /// </summary>
        public override string ToString()
        {
            return $"{ObjectType} at {StartTime}ms, Position: {Position}";
        }
    }

    /// <summary>
    /// 判定窗口基类
    /// </summary>
    public abstract class HitWindow
    {
        public float OverallDifficulty { get; set; }
    }

    /// <summary>
    /// 音效信息基类
    /// </summary>
    public abstract class HitSampleInfo
    {
        public string Name { get; set; }
        public SampleBank Bank { get; set; }
        public int Volume { get; set; }

        public virtual HitSampleInfo Copy()
        {
            return (HitSampleInfo)MemberwiseClone();
        }
    }

    /// <summary>
    /// 默认音效库信息 (修复构造函数版)
    /// </summary>
    public class BankHitSampleInfo : HitSampleInfo
    {
        // 这里的常量用于 OsuParser 中的引用
        public const string HIT_NORMAL = "hitnormal";
        public const string HIT_WHISTLE = "hitwhistle";
        public const string HIT_FINISH = "hitfinish";
        public const string HIT_CLAP = "hitclap";

        public string Name { get; set; }
        public SampleBank Bank { get; set; }
        public int CustomSampleBank { get; set; }
        public int Volume { get; set; }
        public bool IsLayered { get; set; }
        // 注意最后参数 isLayered = false 是默认值，这样也可以兼容 4 个参数的调用
        public BankHitSampleInfo(string name, SampleBank bank, int customBank, int volume, bool isLayered = false)
        {
            Name = name;
            Bank = bank;
            CustomSampleBank = customBank;
            Volume = volume;
            IsLayered = isLayered;
        }
    }
    /// <summary>
    /// 序列音效信息
    /// </summary>
    public class SequenceHitSampleInfo : HitSampleInfo
    {
        public List<(double time, HitSampleInfo sample)> Sequence { get; set; }

        public SequenceHitSampleInfo()
        {
            Sequence = new List<(double, HitSampleInfo)>();
        }

        public SequenceHitSampleInfo(List<(double time, HitSampleInfo sample)> sequence)
        {
            Sequence = sequence;
        }
    }
}