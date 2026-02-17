using System;
using System.Collections.Generic;
using UnityEngine;

namespace OsuVR
{
    public enum ModType
    {
        None = 0,

        // 难度调整 Mod
        HardRock = 1,
        Easy = 2,

        // 自动化 Mod
        Auto = 10,

        // 挑战 Mod
        SuddenDeath = 20,
        Perfect = 21,

        // 速度 Mod
        DoubleTime = 30,
        HalfTime = 31,

        // 视觉 Mod (占位)
        Hidden = 40,
        FadeIn = 41,
        Flashlight = 42,
    }

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

    public enum ModCategory
    {
        Difficulty,
        Automation,
        Challenge,
        Speed,
        Visual
    }

    public static class ModDatabase
    {
        private static Dictionary<ModType, ModInfo> modInfos;
        private static Dictionary<ModType, ModType[]> incompatibleMods;

        static ModDatabase()
        {
            InitializeModInfos();
            InitializeIncompatibilities();
        }

        private static void InitializeModInfos()
        {
            modInfos = new Dictionary<ModType, ModInfo>
            {
                { ModType.HardRock, new ModInfo(
                    ModType.HardRock, "HR", "Hard Rock",
                    "一切变得更难了...\nCS +0.4, AR +0.4, OD +0.4\n音符缩小10%",
                    1.06f, true, ModCategory.Difficulty,
                    new Color(1f, 0.3f, 0.3f)
                )},

                { ModType.Easy, new ModInfo(
                    ModType.Easy, "EZ", "Easy",
                    "放松一下...\nCS -0.4, AR -0.4, OD -0.4\n音符放大10%",
                    0.5f, true, ModCategory.Difficulty,
                    new Color(0.3f, 1f, 0.5f)
                )},

                { ModType.Auto, new ModInfo(
                    ModType.Auto, "AT", "Auto",
                    "自动游玩\n分数不计入排名",
                    0f, false, ModCategory.Automation,
                    new Color(0.3f, 0.7f, 1f)
                )},

                { ModType.SuddenDeath, new ModInfo(
                    ModType.SuddenDeath, "SD", "Sudden Death",
                    "一次Miss即失败\n与Perfect互斥",
                    1.0f, true, ModCategory.Challenge,
                    new Color(1f, 0.2f, 0.5f)
                )},

                { ModType.Perfect, new ModInfo(
                    ModType.Perfect, "PF", "Perfect",
                    "只能获得300判定\n一次非300即失败\n与SuddenDeath互斥",
                    1.0f, true, ModCategory.Challenge,
                    new Color(1f, 0.8f, 0.2f)
                )},

                { ModType.DoubleTime, new ModInfo(
                    ModType.DoubleTime, "DT", "Double Time",
                    "速度提升至150%\n音乐加速1.5倍",
                    1.12f, true, ModCategory.Speed,
                    new Color(1f, 0.5f, 0.8f)
                )},

                { ModType.HalfTime, new ModInfo(
                    ModType.HalfTime, "HT", "Half Time",
                    "速度降低至75%\n音乐减速0.75倍",
                    0.3f, true, ModCategory.Speed,
                    new Color(0.5f, 0.7f, 1f)
                )},

                { ModType.Hidden, new ModInfo(
                    ModType.Hidden, "HD", "Hidden",
                    "音符逐渐消失\n(视觉Mod - 待实现)",
                    1.06f, true, ModCategory.Visual,
                    new Color(0.6f, 0.6f, 0.6f)
                )},

                { ModType.FadeIn, new ModInfo(
                    ModType.FadeIn, "FI", "Fade In",
                    "音符逐渐显现\n(视觉Mod - 待实现)",
                    1.06f, true, ModCategory.Visual,
                    new Color(0.8f, 0.8f, 0.8f)
                )},

                { ModType.Flashlight, new ModInfo(
                    ModType.Flashlight, "FL", "Flashlight",
                    "视野受限\n(视觉Mod - 待实现)",
                    1.12f, true, ModCategory.Visual,
                    new Color(1f, 1f, 0.6f)
                )},
            };
        }

        private static void InitializeIncompatibilities()
        {
            incompatibleMods = new Dictionary<ModType, ModType[]>
            {
                { ModType.HardRock, new[] { ModType.Easy } },
                { ModType.Easy, new[] { ModType.HardRock } },
                { ModType.DoubleTime, new[] { ModType.HalfTime } },
                { ModType.HalfTime, new[] { ModType.DoubleTime } },
                { ModType.SuddenDeath, new[] { ModType.Perfect } },
                { ModType.Perfect, new[] { ModType.SuddenDeath } },
                { ModType.Hidden, new[] { ModType.FadeIn } },
                { ModType.FadeIn, new[] { ModType.Hidden } },
            };
        }

        public static ModInfo GetModInfo(ModType type)
        {
            if (modInfos.TryGetValue(type, out var info))
                return info;
            return null;
        }

        public static List<ModInfo> GetAllMods()
        {
            return new List<ModInfo>(modInfos.Values);
        }

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

        public static bool AreIncompatible(ModType modA, ModType modB)
        {
            if (incompatibleMods.TryGetValue(modA, out var incompatible))
            {
                return Array.IndexOf(incompatible, modB) >= 0;
            }
            return false;
        }

        public static ModType[] GetIncompatibleMods(ModType mod)
        {
            if (incompatibleMods.TryGetValue(mod, out var incompatible))
                return incompatible;
            return Array.Empty<ModType>();
        }
    }

    [Serializable]
    public class ModSelection
    {
        private HashSet<ModType> activeMods = new HashSet<ModType>();

        public event Action<ModType, bool> OnModChanged;

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
                foreach (var active in activeMods)
                {
                    if (ModDatabase.AreIncompatible(mod, active))
                    {
                        activeMods.Remove(active);
                        OnModChanged?.Invoke(active, false);
                    }
                }
                activeMods.Add(mod);
                OnModChanged?.Invoke(mod, true);
                return true;
            }
        }

        public bool HasMod(ModType mod) => activeMods.Contains(mod);

        public void SetMod(ModType mod, bool enabled)
        {
            if (enabled)
            {
                if (!activeMods.Contains(mod))
                {
                    foreach (var active in activeMods)
                    {
                        if (ModDatabase.AreIncompatible(mod, active))
                        {
                            activeMods.Remove(active);
                            OnModChanged?.Invoke(active, false);
                        }
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

        public void Clear()
        {
            var modsToRemove = new List<ModType>(activeMods);
            foreach (var mod in modsToRemove)
            {
                activeMods.Remove(mod);
                OnModChanged?.Invoke(mod, false);
            }
        }

        public List<ModType> GetActiveMods()
        {
            return new List<ModType>(activeMods);
        }

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
