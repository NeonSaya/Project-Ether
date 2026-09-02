using UnityEngine;

namespace OsuVR
{
    /// <summary>
    /// Mod 效果应用器
    /// 将 ModSelection 转换为实际的游戏参数修改
    /// </summary>
    public class ModEffectsApplier
    {
        private ModSelection modSelection;

        // =========================================================
        // 修改后的参数
        // =========================================================

        /// <summary>
        /// 速度倍率 (DT=1.5x, HT=0.75x)
        /// </summary>
        public float SpeedMultiplier { get; private set; } = 1f;

        /// <summary>
        /// Circle Size 乘数 (HR=1.3x, EZ=0.5x)
        /// </summary>
        public float CircleSizeMultiplier { get; private set; } = 1f;

        /// <summary>
        /// Approach Rate 乘数 (HR=1.4x, EZ=0.5x)
        /// </summary>
        public float ApproachRateMultiplier { get; private set; } = 1f;

        /// <summary>
        /// 是否启用 Hard Rock (Y轴镜像翻转)
        /// </summary>
        public bool IsHardRock { get; private set; } = false;

        /// <summary>
        /// 是否启用自动游玩
        /// </summary>
        public bool IsAutoPlay { get; private set; } = false;

        /// <summary>
        /// 是否启用 Hidden (音符逐渐消失)
        /// </summary>
        public bool IsHidden { get; private set; } = false;

        /// <summary>
        /// 是否启用 Flashlight (视野受限)
        /// </summary>
        public bool IsFlashlight { get; private set; } = false;

        /// <summary>
        /// 总分数倍率 (所有 Mod 倍率的乘积，用于 UI 显示)
        /// AT Mod 会使此值变为 0.00x
        /// </summary>
        public float ScoreMultiplier { get; private set; } = 1f;

        /// <summary>
        /// 用于实际分数计算的倍率 (排除 AT Mod)
        /// AT Mod 不影响实际分数计算，但会在 UI 上显示 0.00x
        /// </summary>
        public float ScoreMultiplierForCalculation { get; private set; } = 1f;

        /// <summary>
        /// 构造函数：根据 ModSelection 初始化效果
        /// </summary>
        public ModEffectsApplier(ModSelection selection)
        {
            modSelection = selection ?? new ModSelection();
            ApplyEffects();
        }

        // =========================================================
        // 效果应用
        // =========================================================

        /// <summary>
        /// 应用所有已激活 Mod 的效果
        /// 重置所有参数后遍历应用每个 Mod 的效果
        /// </summary>
        public void ApplyEffects()
        {
            // 重置为默认值
            SpeedMultiplier = 1f;
            CircleSizeMultiplier = 1f;
            ApproachRateMultiplier = 1f;
            IsHardRock = false;
            IsAutoPlay = false;
            IsHidden = false;
            IsFlashlight = false;

            ScoreMultiplier = modSelection.GetTotalScoreMultiplier();

            ScoreMultiplierForCalculation = 1f;
            foreach (var mod in modSelection.GetActiveMods())
            {
                var info = ModDatabase.GetModInfo(mod);
                if (info != null && mod != ModType.Auto)
                {
                    ScoreMultiplierForCalculation *= info.scoreMultiplier;
                }
            }

            // 遍历应用每个 Mod 的效果
            foreach (var mod in modSelection.GetActiveMods())
            {
                ApplyModEffect(mod);
            }
        }

        /// <summary>
        /// 应用单个 Mod 的效果
        /// </summary>
        private void ApplyModEffect(ModType mod)
        {
            switch (mod)
            {
                case ModType.HardRock:
                    ApplyHardRock();
                    break;

                case ModType.Easy:
                    ApplyEasy();
                    break;

                case ModType.Auto:
                    ApplyAuto();
                    break;

                case ModType.DoubleTime:
                    ApplyDoubleTime();
                    break;

                case ModType.HalfTime:
                    ApplyHalfTime();
                    break;

                case ModType.Hidden:
                    ApplyHidden();
                    break;

                case ModType.Flashlight:
                    ApplyFlashlight();
                    break;
            }
        }

        // =========================================================
        // 具体 Mod 效果实现
        // =========================================================

        /// <summary>
        /// Hard Rock: 全面提升难度
        /// CS×1.3, AR×1.4 (上限10)
        /// Y轴镜像翻转
        /// </summary>
        private void ApplyHardRock()
        {
            CircleSizeMultiplier = 1.3f;
            ApproachRateMultiplier = 1.4f;
            IsHardRock = true;
        }

        /// <summary>
        /// Easy: 所有难度属性减半
        /// CS×0.5, AR×0.5
        /// </summary>
        private void ApplyEasy()
        {
            CircleSizeMultiplier = 0.5f;
            ApproachRateMultiplier = 0.5f;
        }

        /// <summary>
        /// Auto: 自动游玩
        /// 标记为自动模式，分数不计入排名
        /// </summary>
        private void ApplyAuto()
        {
            IsAutoPlay = true;
        }

        /// <summary>
        /// Double Time: 加速 1.5 倍
        /// 判定窗口保持原速绝对时间
        /// </summary>
        private void ApplyDoubleTime()
        {
            SpeedMultiplier = 1.5f;
        }

        /// <summary>
        /// Half Time: 减速至 0.75 倍
        /// 判定窗口保持原速绝对时间
        /// </summary>
        private void ApplyHalfTime()
        {
            SpeedMultiplier = 0.75f;
        }

        /// <summary>
        /// Hidden: 音符逐渐消失
        /// 在 ObjectFadeIn 中实现：前 40% 时间淡入，40%-70% 时间淡出
        /// </summary>
        private void ApplyHidden()
        {
            IsHidden = true;
        }

        /// <summary>
        /// Flashlight: 视野受限
        /// 在 FlashlightEffect 中实现：手电筒遮罩跟随射线位置
        /// </summary>
        private void ApplyFlashlight()
        {
            IsFlashlight = true;
        }

        // =========================================================
        // 参数计算接口
        // =========================================================

        /// <summary>
        /// 获取修改后的 Circle Size
        /// 上限为 10
        /// </summary>
        public float GetModifiedCS(float baseCS)
        {
            return Mathf.Min(baseCS * CircleSizeMultiplier, 10f);
        }

        /// <summary>
        /// 获取修改后的 Approach Rate
        /// 上限为 10
        /// </summary>
        public float GetModifiedAR(float baseAR)
        {
            return Mathf.Min(baseAR * ApproachRateMultiplier, 10f);
        }

        /// <summary>
        /// 获取修改后的预判时间 (毫秒)
        /// 基于 AR 计算：AR<5 时 1200+120*(5-AR)，AR>=5 时 1200-150*(AR-5)
        /// </summary>
        public double GetModifiedTimePreempt(float baseAR)
        {
            float modifiedAR = GetModifiedAR(baseAR);

            if (modifiedAR < 5)
            {
                return 1200 + 120 * (5 - modifiedAR);
            }
            else
            {
                return 1200 - 150 * (modifiedAR - 5);
            }
        }

        // =========================================================
        // 辅助接口
        // =========================================================

        /// <summary>
        /// 获取 Mod 显示字符串
        /// </summary>
        public string GetModString()
        {
            return modSelection.GetModString();
        }

        /// <summary>
        /// 检查当前选择是否可排名
        /// </summary>
        public bool IsRanked()
        {
            return modSelection.IsRanked();
        }
    }
}
