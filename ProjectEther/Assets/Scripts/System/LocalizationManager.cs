using System;
using System.Collections.Generic;
using UnityEngine;

namespace OsuVR
{
    public enum Language
    {
        English = 0,
        Chinese = 1,
        Japanese = 2
    }

    public static class LocalizationManager
    {
        private const string PREF_KEY = "GameLanguage";
        private static Language currentLanguage;
        private static Dictionary<string, string[]> localizationDict;

        public static event Action OnLanguageChanged;

        static LocalizationManager()
        {
            InitializeDictionary();
            LoadLanguage();
        }

        private static void InitializeDictionary()
        {
            localizationDict = new Dictionary<string, string[]>
            {
                {"ui_beatmaps", new[] {"BEATMAPS", "曲目列表", "ビートマップ"}},
                {"ui_mods", new[] {"MODS", "玩法修改", "モッド"}},
                {"ui_confirm_mods", new[] {"CONFIRM MODS", "确认修改", "モッド確定"}},
                {"ui_play", new[] {"PLAY", "开始游戏", "ゲーム開始"}},
                {"ui_back", new[] {"BACK", "返回", "戻る"}},
                {"ui_cs", new[] {"CS", "圈距 (CS)", "サークルサイズ"}},
                {"ui_ar", new[] {"AR", "缩圈 (AR)", "アプローチ率"}},
                {"ui_od", new[] {"OD", "判定 (OD)", "全体難易度"}},
                {"ui_hp", new[] {"HP", "血量 (HP)", "HPドレイン"}},
                {"ui_settings", new[] {"SETTINGS", "游戏设置", "設定"}},
                {"ui_language", new[] {"Language", "游戏语言", "言語"}},
                {"ui_lang_name", new[] {"English", "简体中文", "日本語"}},
                {"ui_no_mod", new[] {"No Mod", "无 Mod", "モッドなし"}},
                {"ui_select_beatmap", new[] {"Select a Beatmap", "选择一首曲目", "ビートマップを選択"}},
                {"ui_unknown_title", new[] {"Unknown Title", "未知曲目", "不明なタイトル"}},
                {"ui_normal", new[] {"Normal", "普通", "ノーマル"}},
                {"ui_play_button", new[] {"PLAY", "开始", "プレイ"}},
                {"ui_quit", new[] {"Quit", "退出", "終了"}},
                {"ui_credits", new[] {"Credits", "制作人员", "クレジット"}},
                {"ui_multiplier", new[] {"Multiplier:", "分数倍率:", "スコア倍率:"}},
                {"ui_tab_game", new[] {"Game", "游戏", "ゲーム"}},
                {"ui_tab_audio", new[] {"Audio", "音频", "オーディオ"}},
                {"ui_tab_graphics", new[] {"Graphics", "画面", "グラフィック"}},
                {"mod_hardrock_name", new[] {"Hard Rock", "困难模式", "ハードロック"}},
                {"mod_hardrock_desc", new[] {"Everything becomes harder...", "一切变得更难了...", "すべてが難しくなる..."}},
                {"mod_easy_name", new[] {"Easy", "简单模式", "イージー"}},
                {"mod_easy_desc", new[] {"Relax and take it easy...", "放轻松，慢慢来...", "リラックスして楽しもう..."}},
                {"mod_auto_name", new[] {"Auto", "自动演示", "オート"}},
                {"mod_auto_desc", new[] {"Watch a perfect autoplay", "观看完美的自动演示", "完璧なオートプレイを見る"}},
                {"mod_doubletime_name", new[] {"Double Time", "双倍速", "ダブルタイム"}},
                {"mod_doubletime_desc", new[] {"Speed up to 150%", "加速至 150%", "150%に加速"}},
                {"mod_halftime_name", new[] {"Half Time", "半速", "ハーフタイム"}},
                {"mod_halftime_desc", new[] {"Slow down to 75%", "减速至 75%", "75%に減速"}},
                {"mod_hidden_name", new[] {"Hidden", "隐藏", "ヒドゥン"}},
                {"mod_hidden_desc", new[] {"Notes fade out gradually", "音符逐渐消失", "ノーツが徐々に消える"}},
                {"mod_flashlight_name", new[] {"Flashlight", "手电筒", "フラッシュライト"}},
                {"mod_flashlight_desc", new[] {"Restricted visibility area", "有限的可见区域", "視界が制限される"}},
                {"ui_retry", new[] {"RETRY", "重试", "リトライ"}},
                {"ui_watch_replay", new[] {"WATCH REPLAY", "观看回放", "リプレイを見る"}},
                {"ui_score", new[] {"Score", "分数", "スコア"}},
                {"ui_accuracy", new[] {"Accuracy", "准确率", "精度"}},
                {"ui_max_combo", new[] {"Max Combo", "最大连击", "最大コンボ"}},
                {"ui_rank", new[] {"Rank", "评级", "ランク"}},
                {"ui_hit300", new[] {"300", "300", "300"}},
                {"ui_hit100", new[] {"100", "100", "100"}},
                {"ui_hit50", new[] {"50", "50", "50"}},
                {"ui_miss", new[] {"Miss", "Miss", "Miss"}},
                {"ui_result_title", new[] {"RESULT", "结算", "リザルト"}},
                {"ui_length", new[] {"Length", "时长", "長さ"}},
            };
        }

        private static void LoadLanguage()
        {
            int savedIndex = PlayerPrefs.GetInt(PREF_KEY, 0);
            currentLanguage = (Language)savedIndex;
        }

        public static void SetLanguage(Language language)
        {
            if (currentLanguage == language) return;
            
            currentLanguage = language;
            PlayerPrefs.SetInt(PREF_KEY, (int)language);
            PlayerPrefs.Save();
            
            OnLanguageChanged?.Invoke();
        }

        public static Language GetCurrentLanguage()
        {
            return currentLanguage;
        }

        public static int GetCurrentLanguageIndex()
        {
            return (int)currentLanguage;
        }

        public static void SetLanguageByIndex(int index)
        {
            if (index >= 0 && index <= 2)
            {
                SetLanguage((Language)index);
            }
        }

        public static void CycleLanguage()
        {
            int nextIndex = ((int)currentLanguage + 1) % 3;
            currentLanguage = (Language)nextIndex;
            PlayerPrefs.SetInt(PREF_KEY, nextIndex);
            PlayerPrefs.Save();
            
            OnLanguageChanged?.Invoke();
        }

        public static string GetText(string key)
        {
            if (string.IsNullOrEmpty(key)) return key;

            if (localizationDict.TryGetValue(key, out var translations))
            {
                int index = (int)currentLanguage;
                if (index >= 0 && index < translations.Length)
                {
                    return translations[index];
                }
            }

            return key;
        }

        public static string GetText(string key, Language language)
        {
            if (string.IsNullOrEmpty(key)) return key;

            if (localizationDict.TryGetValue(key, out var translations))
            {
                int index = (int)language;
                if (index >= 0 && index < translations.Length)
                {
                    return translations[index];
                }
            }

            return key;
        }

        public static bool HasKey(string key)
        {
            return !string.IsNullOrEmpty(key) && localizationDict.ContainsKey(key);
        }

        public static string GetLanguageName(Language language)
        {
            return language switch
            {
                Language.English => "English",
                Language.Chinese => "简体中文",
                Language.Japanese => "日本語",
                _ => "English"
            };
        }

        public static string GetCurrentLanguageName()
        {
            return GetLanguageName(currentLanguage);
        }

        public static string[] GetAllLanguageNames()
        {
            return new[]
            {
                "English",
                "简体中文",
                "日本語"
            };
        }
    }
}
