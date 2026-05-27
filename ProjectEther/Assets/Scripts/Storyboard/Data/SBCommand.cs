using UnityEngine;

namespace OsuVR.Storyboard.Data
{
    /// <summary>
    /// Storyboard 命令类型 (对应 .osu/.osb 中的单字母标识)
    /// </summary>
    public enum SBCommandType
    {
        F,      // Fade (透明度)
        M,      // Move (位置)
        MX,     // MoveX (X轴移动)
        MY,     // MoveY (Y轴移动)
        S,      // Scale (缩放)
        R,      // Rotate (旋转)
        C,      // Color (颜色)
        P,      // Parameter (特殊参数: H=水平翻转, V=垂直翻转, A=加法混合)
    }

    /// <summary>
    /// Storyboard 命令基类：一个从 startTime 到 endTime 的带缓动插值动画
    /// </summary>
    public class SBCommand
    {
        public SBCommandType Type;
        public SBEasing Easing;
        public double StartTime;
        public double EndTime;

        public SBCommand(SBCommandType type, SBEasing easing, double startTime, double endTime)
        {
            Type = type;
            Easing = easing;
            StartTime = startTime;
            EndTime = endTime;
        }

        /// <summary>
        /// 计算当前时间在本命令中的归一化进度 [0,1]
        /// </summary>
        public float GetProgress(double currentTime)
        {
            if (EndTime <= StartTime) return 1f;
            float t = (float)((currentTime - StartTime) / (EndTime - StartTime));
            return Mathf.Clamp01(t);
        }

        /// <summary>
        /// 计算经过缓动后的插值进度
        /// </summary>
        public float GetEasedProgress(double currentTime)
        {
            return EasingMath.Interpolate(Easing, GetProgress(currentTime));
        }
    }

    // =====================================================
    //  具体命令类型：每种命令携带不同的起止值
    // =====================================================

    /// <summary>
    /// Fade 命令：透明度从 StartValue 到 EndValue
    /// </summary>
    public class SBFadeCommand : SBCommand
    {
        public float StartValue;
        public float EndValue;

        public SBFadeCommand(SBEasing easing, double start, double end, float startVal, float endVal)
            : base(SBCommandType.F, easing, start, end)
        {
            StartValue = startVal;
            EndValue = endVal;
        }

        public float Evaluate(double time)
        {
            float p = GetEasedProgress(time);
            return Mathf.Lerp(StartValue, EndValue, p);
        }
    }

    /// <summary>
    /// Move 命令：位置从 StartPos 到 EndPos
    /// </summary>
    public class SBMoveCommand : SBCommand
    {
        public Vector2 StartPos;
        public Vector2 EndPos;

        public SBMoveCommand(SBEasing easing, double start, double end, Vector2 startPos, Vector2 endPos)
            : base(SBCommandType.M, easing, start, end)
        {
            StartPos = startPos;
            EndPos = endPos;
        }

        public Vector2 Evaluate(double time)
        {
            float p = GetEasedProgress(time);
            return Vector2.Lerp(StartPos, EndPos, p);
        }
    }

    /// <summary>
    /// MoveX / MoveY 命令：单轴移动
    /// </summary>
    public class SBMoveAxisCommand : SBCommand
    {
        public float StartValue;
        public float EndValue;

        public SBMoveAxisCommand(SBCommandType type, SBEasing easing, double start, double end, float startVal, float endVal)
            : base(type, easing, start, end)
        {
            StartValue = startVal;
            EndValue = endVal;
        }

        public float Evaluate(double time)
        {
            float p = GetEasedProgress(time);
            return Mathf.Lerp(StartValue, EndValue, p);
        }
    }

    /// <summary>
    /// Scale 命令：统一缩放
    /// </summary>
    public class SBScaleCommand : SBCommand
    {
        public float StartValue;
        public float EndValue;

        public SBScaleCommand(SBEasing easing, double start, double end, float startVal, float endVal)
            : base(SBCommandType.S, easing, start, end)
        {
            StartValue = startVal;
            EndValue = endVal;
        }

        public float Evaluate(double time)
        {
            float p = GetEasedProgress(time);
            return Mathf.Lerp(StartValue, EndValue, p);
        }
    }

    /// <summary>
    /// Rotate 命令：旋转弧度
    /// </summary>
    public class SBRotateCommand : SBCommand
    {
        public float StartValue;
        public float EndValue;

        public SBRotateCommand(SBEasing easing, double start, double end, float startVal, float endVal)
            : base(SBCommandType.R, easing, start, end)
        {
            StartValue = startVal;
            EndValue = endVal;
        }

        public float Evaluate(double time)
        {
            float p = GetEasedProgress(time);
            return Mathf.Lerp(StartValue, EndValue, p);
        }
    }

    /// <summary>
    /// Color 命令：RGB 颜色插值 (值域 0-255 存储，使用时归一化)
    /// </summary>
    public class SBColorCommand : SBCommand
    {
        public Color32 StartColor;
        public Color32 EndColor;

        public SBColorCommand(SBEasing easing, double start, double end, Color32 startColor, Color32 endColor)
            : base(SBCommandType.C, easing, start, end)
        {
            StartColor = startColor;
            EndColor = endColor;
        }

        public Color32 Evaluate(double time)
        {
            float p = GetEasedProgress(time);
            byte r = (byte)Mathf.Lerp(StartColor.r, EndColor.r, p);
            byte g = (byte)Mathf.Lerp(StartColor.g, EndColor.g, p);
            byte b = (byte)Mathf.Lerp(StartColor.b, EndColor.b, p);
            return new Color32(r, g, b, 255);
        }
    }

    /// <summary>
    /// Parameter 命令：特殊参数 (H=水平翻转, V=垂直翻转, A=加法混合)
    /// </summary>
    public class SBParameterCommand : SBCommand
    {
        public string Parameter;

        public SBParameterCommand(SBEasing easing, double start, double end, string param)
            : base(SBCommandType.P, easing, start, end)
        {
            Parameter = param;
        }
    }
}
