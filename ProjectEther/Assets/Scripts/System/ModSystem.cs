using System;
using System.Collections.Generic;
using UnityEngine;

namespace OsuVR
{
    /// <summary>
    /// Mod 类型枚举
    /// 定义所有可用的游戏修改器
    /// </summary>
    public enum ModType
    {
        None = 0,

        // 难度调整
        HardRock = 1,
        Easy = 2,

        // 自动化
        Auto = 10,

        // 速度调整
        DoubleTime = 30,
        HalfTime = 31,

        // 视觉效果 (待实现)
        Hidden = 40,
        Flashlight = 42,
    }

    /// <summary>
    /// Mod 信息数据结构
    /// 存储单个 Mod 的所有元数据
    /// </summary>
    [Serializable]
    public class ModInfo
    {
        public ModType type;
        public string shortName;
        public string fullName;
        public string description;
        public float scoreMultiplier;
        public bool isRanked;
        public ModCategory category;
        public Color displayColor;

        public ModInfo(ModType type, string shortName, string fullName, string description,
                       float scoreMultiplier, bool isRanked, ModCategory category, Color displayColor)
        {
            this.type = type;
            this.shortName = shortName;
            this.fullName = fullName;
            this.description = description;
            this.scoreMultiplier = scoreMultiplier;
            this.isRanked = isRanked;
            this.category = category;
            this.displayColor = displayColor;
        }
    }

    /// <summary>
    /// Mod 分类枚举
    /// 用于 UI 分组显示
    /// </summary>
    public enum ModCategory
    {
        Difficulty,
        Automation,
        Speed,
        Visual
    }

    /// <summary>
    /// Mod 数据库 (静态类)
    /// 存储所有 Mod 的定义信息和互斥关系
    /// </summary>
    public static class ModDatabase
    {
        private static Dictionary<ModType, ModInfo> modInfos;
        private static Dictionary<ModType, ModType[]> incompatibleMods;

        static ModDatabase()
        {
            InitializeModInfos();
            InitializeIncompatibilities();
        }

        /// <summary>
        /// 初始化所有 Mod 的信息
        /// </summary>
        private static void InitializeModInfos()
        {
            modInfos = new Dictionary<ModType, ModInfo>
            {
                // =========================================================
                // 难度调整 Mod
                // =========================================================
                { ModType.HardRock, new ModInfo(
                    ModType.HardRock, "HR", "Hard Rock",
                    "一切变得更难了...\nCS×1.3, AR×1.4 (上限10)\nY轴镜像翻转",
                    1.06f, true, ModCategory.Difficulty,
                    new Color(1f, 0.3f, 0.3f)
                )},

                { ModType.Easy, new ModInfo(
                    ModType.Easy, "EZ", "Easy",
                    "放松一下...\nCS×0.5, AR×0.5",
                    0.5f, true, ModCategory.Difficulty,
                    new Color(0.3f, 1f, 0.5f)
                )},

                // =========================================================
                // 自动化 Mod
                // =========================================================
                { ModType.Auto, new ModInfo(
                    ModType.Auto, "AT", "Auto",
                    "自动游玩\n分数不计入排名",
                    0f, false, ModCategory.Automation,
                    new Color(0.3f, 0.7f, 1f)
                )},

                // =========================================================
                // 速度 Mod
                // =========================================================
                { ModType.DoubleTime, new ModInfo(
                    ModType.DoubleTime, "DT", "Double Time",
                    "速度提升至150%\n音乐加速1.5倍\n判定窗口固定250ms",
                    1.12f, true, ModCategory.Speed,
                    new Color(1f, 0.5f, 0.8f)
                )},

                { ModType.HalfTime, new ModInfo(
                    ModType.HalfTime, "HT", "Half Time",
                    "速度降低至75%\n音乐减速0.75倍\n判定窗口固定250ms",
                    0.3f, true, ModCategory.Speed,
                    new Color(0.5f, 0.7f, 1f)
                )},

                // =========================================================
                // 视觉 Mod (占位，待实现)
                // =========================================================
                { ModType.Hidden, new ModInfo(
                    ModType.Hidden, "HD", "Hidden",
                    "音符逐渐消失\n(视觉Mod - 待实现)",
                    1.06f, true, ModCategory.Visual,
                    new Color(0.6f, 0.6f, 0.6f)
                )},

                { ModType.Flashlight, new ModInfo(
                    ModType.Flashlight, "FL", "Flashlight",
                    "视野受限\n(视觉Mod - 待实现)",
                    1.12f, true, ModCategory.Visual,
                    new Color(1f, 1f, 0.6f)
                )},
            };
        }

        /// <summary>
        /// 初始化 Mod 互斥关系
        /// 互斥的 Mod 不能同时启用
        /// </summary>
        private static void InitializeIncompatibilities()
        {
            incompatibleMods = new Dictionary<ModType, ModType[]>
            {
                { ModType.HardRock, new[] { ModType.Easy } },
                { ModType.Easy, new[] { ModType.HardRock } },
                { ModType.DoubleTime, new[] { ModType.HalfTime } },
                { ModType.HalfTime, new[] { ModType.DoubleTime } },
            };
        }

        /// <summary>
        /// 获取指定 Mod 的信息
        /// </summary>
        public static ModInfo GetModInfo(ModType type)
        {
            if (modInfos.TryGetValue(type, out var info))
                return info;
            return null;
        }

        /// <summary>
        /// 获取所有 Mod 信息列表
        /// </summary>
        public static List<ModInfo> GetAllMods()
        {
            return new List<ModInfo>(modInfos.Values);
        }

        /// <summary>
        /// 按分类获取 Mod 列表
        /// </summary>
        public static List<ModInfo> GetModsByCategory(ModCategory category)
        {
            var result = new List<ModInfo>();
            foreach (var info in modInfos.Values)
            {
                if (info.category == category)
                    result.Add(info);
            }
            return result;
        }

        /// <summary>
        /// 检查两个 Mod 是否互斥
        /// </summary>
        public static bool AreIncompatible(ModType modA, ModType modB)
        {
            if (incompatibleMods.TryGetValue(modA, out var incompatible))
            {
                return Array.IndexOf(incompatible, modB) >= 0;
            }
            return false;
        }

        /// <summary>
        /// 获取与指定 Mod 互斥的所有 Mod
        /// </summary>
        public static ModType[] GetIncompatibleMods(ModType mod)
        {
            if (incompatibleMods.TryGetValue(mod, out var incompatible))
                return incompatible;
            return Array.Empty<ModType>();
        }
    }

    /// <summary>
    /// Mod 选择状态管理类
    /// 管理当前激活的 Mod 集合，处理互斥逻辑
    /// </summary>
    [Serializable]
    public class ModSelection
    {
        private HashSet<ModType> activeMods = new HashSet<ModType>();

        /// <summary>
        /// Mod 状态变化事件
        /// 参数: (ModType, 是否启用)
        /// </summary>
        public event Action<ModType, bool> OnModChanged;

        /// <summary>
        /// 切换 Mod 的启用状态
        /// 启用时会自动移除互斥的 Mod
        /// </summary>
        public bool ToggleMod(ModType mod)
        {
            if (activeMods.Contains(mod))
            {
                activeMods.Remove(mod);
                OnModChanged?.Invoke(mod, false);
                return false;
            }
            else
            {
                // 移除所有与新 Mod 互斥的已激活 Mod
                var modsToRemove = new List<ModType>();
                foreach (var active in activeMods)
                {
                    if (ModDatabase.AreIncompatible(mod, active))
                    {
                        modsToRemove.Add(active);
                    }
                }
                
                // 安全地移除互斥的 Mods
                foreach (var modToRemove in modsToRemove)
                {
                    activeMods.Remove(modToRemove);
                    OnModChanged?.Invoke(modToRemove, false);
                }
                
                activeMods.Add(mod);
                OnModChanged?.Invoke(mod, true);
                return true;
            }
        }

        /// <summary>
        /// 检查是否启用了指定 Mod
        /// </summary>
        public bool HasMod(ModType mod) => activeMods.Contains(mod);

        /// <summary>
        /// 设置指定 Mod 的启用状态
        /// </summary>
        public void SetMod(ModType mod, bool enabled)
        {
            if (enabled)
            {
                if (!activeMods.Contains(mod))
                {
                    // 移除所有互斥 Mod
                    var modsToRemove = new List<ModType>();
                    foreach (var active in activeMods)
                    {
                        if (ModDatabase.AreIncompatible(mod, active))
                        {
                            modsToRemove.Add(active);
                        }
                    }
                    
                    // 安全地移除互斥的 Mods
                    foreach (var modToRemove in modsToRemove)
                    {
                        activeMods.Remove(modToRemove);
                        OnModChanged?.Invoke(modToRemove, false);
                    }
                    
                    activeMods.Add(mod);
                    OnModChanged?.Invoke(mod, true);
                }
            }
            else
            {
                if (activeMods.Remove(mod))
                {
                    OnModChanged?.Invoke(mod, false);
                }
            }
        }

        /// <summary>
        /// 清除所有已选 Mod
        /// </summary>
        public void Clear()
        {
            var modsToRemove = new List<ModType>(activeMods);
            foreach (var mod in modsToRemove)
            {
                activeMods.Remove(mod);
                OnModChanged?.Invoke(mod, false);
            }
        }

        /// <summary>
        /// 获取所有已激活的 Mod 列表
        /// </summary>
        public List<ModType> GetActiveMods()
        {
            return new List<ModType>(activeMods);
        }

        /// <summary>
        /// 计算总分数倍率 (所有 Mod 倍率的乘积)
        /// </summary>
        public float GetTotalScoreMultiplier()
        {
            float total = 1f;
            foreach (var mod in activeMods)
            {
                var info = ModDatabase.GetModInfo(mod);
                if (info != null)
                {
                    total *= info.scoreMultiplier;
                }
            }
            return total;
        }

        /// <summary>
        /// 获取 Mod 显示字符串 (如 "HR DT")
        /// </summary>
        public string GetModString()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var mod in activeMods)
            {
                var info = ModDatabase.GetModInfo(mod);
                if (info != null)
                {
                    sb.Append(info.shortName);
                    sb.Append(" ");
                }
            }
            return sb.ToString().Trim();
        }

        /// <summary>
        /// 检查当前选择是否可排名
        /// 包含不可排名 Mod (如 Auto) 时返回 false
        /// </summary>
        public bool IsRanked()
        {
            foreach (var mod in activeMods)
            {
                var info = ModDatabase.GetModInfo(mod);
                if (info != null && !info.isRanked)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 深拷贝当前选择
        /// </summary>
        public ModSelection Clone()
        {
            var clone = new ModSelection();
            foreach (var mod in activeMods)
            {
                clone.activeMods.Add(mod);
            }
            return clone;
        }
    }
}
