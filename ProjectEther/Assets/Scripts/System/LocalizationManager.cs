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
                {"ui_cs", new[] {"CS", "圈距 (CS)", "CS"}},
                {"ui_ar", new[] {"AR", "缩圈 (AR)", "AR"}},
                {"ui_od", new[] {"OD", "判定 (OD)", "OD"}},
                {"ui_hp", new[] {"HP", "血量 (HP)", "HP"}},
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
                {"ui_difficulty", new[] {"Difficulty", "难度", "難易度"}},
                {"ui_mapper", new[] {"Mapper", "谱师", "マッパー"}},
                {"ui_spinner_bonus", new[] {"Spinner Bonus", "转盘奖励", "スピナーボーナス"}},
                {"ui_max", new[] {"MAX", "MAX", "MAX"}},
                {"ui_tab_controller", new[] {"Controller", "控制器", "コントローラー"}},
                {"ui_master_volume", new[] {"Master Volume", "主音量", "マスターボリューム"}},
                {"ui_music_volume", new[] {"Music Volume", "音乐音量", "音楽ボリューム"}},
                {"ui_sfx_volume", new[] {"SFX Volume", "音效音量", "効果音ボリューム"}},
                {"ui_audio_offset", new[] {"Audio Offset", "音频偏移", "オーディオオフセット"}},
                {"ui_quality", new[] {"Quality", "画质", "品質"}},
                {"ui_anti_aliasing", new[] {"Anti-Aliasing", "抗锯齿", "アンチエイリアス"}},
                {"ui_particle_density", new[] {"Particle Density", "粒子密度", "パーティクル密度"}},
                {"ui_enable_haptics", new[] {"Enable Haptics", "启用手柄震动", "触覚を有効化"}},
                {"ui_haptic_intensity", new[] {"Haptic Intensity", "震动强度", "触覚強度"}},
                {"ui_display_original_language", new[] {"Display Song Names in Original Language", "显示歌曲原名", "曲名を原語で表示"}},
                {"ui_enable_storyboard", new[] {"Background Screen", "背景板", "背景スクリーン"}},
                {"ui_enable_storyboard_playback", new[] {"Storyboard Playback", "故事板", "ストーリーボード"}},
                {"ui_storyboard_distance", new[] {"Screen Distance", "屏幕距离", "スクリーン距離"}},
                {"ui_storyboard_alpha", new[] {"Screen Opacity", "屏幕透明度", "スクリーン透明度"}},
                {"ui_left_controller_z_offset", new[] {"Left Controller Z Offset", "左手控制器Z轴偏移", "左コントローラーZオフセット"}},
                {"ui_right_controller_z_offset", new[] {"Right Controller Z Offset", "右手控制器Z轴偏移", "右コントローラーZオフセット"}},
                {"ui_left_controller_y_offset", new[] {"Left Controller Y Offset", "左手控制器Y轴偏移", "左コントローラーYオフセット"}},
                {"ui_right_controller_y_offset", new[] {"Right Controller Y Offset", "右手控制器Y轴偏移", "右コントローラーYオフセット"}},
                {"ui_controller_rotation_offset", new[] {"Controller Rotation", "控制器旋转偏移", "コントローラー回転オフセット"}},
                {"ui_reset", new[] {"RESET", "重置", "リセット"}},
                {"ui_save", new[] {"SAVE", "保存", "保存"}},
                {"ui_resume", new[] {"RESUME", "继续", "再開"}},
                {"ui_pause", new[] {"PAUSE", "暂停", "一時停止"}},
                {"ui_main_menu", new[] {"MAIN MENU", "主菜单", "メインメニュー"}},
                {"ui_song_select", new[] {"BACK", "返回选歌", "選曲へ"}},
                {"ui_low", new[] {"Low", "低", "低"}},
                {"ui_medium", new[] {"Medium", "中", "中"}},
                {"ui_high", new[] {"High", "高", "高"}},
                {"ui_ultra", new[] {"Ultra", "超高", "ウルトラ"}},
                {"ui_off", new[] {"Off", "关闭", "オフ"}},
                {"ui_on", new[] {"On", "开启", "オン"}},
                {"ui_sliders_info", new[] {"Sliders: {0}/{1} Perfect", "滑条: {0}/{1} 完美", "スライダー: {0}/{1} パーフェクト"}},
                {"ui_spinner_bonus_text", new[] {"+{0} Spinner Bonus", "+{0} 转盘奖励", "+{0} スピナーボーナス"}},
                {"ui_unknown_artist", new[] {"Unknown Artist", "未知艺术家", "不明なアーティスト"}},
                {"ui_unknown_mapper", new[] {"Unknown", "未知", "不明"}},
                {"ui_mapped_by", new[] {"Mapped by {0}", "谱师: {0}", "マッパー: {0}"}},
                {"ui_mods_display", new[] {"Mods: {0}", "Mods: {0}", "モッド: {0}"}},
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

        public static void ForceUpdateLanguage()
        {
            OnLanguageChanged?.Invoke();
        }

        public static void ReloadAndNotify()
        {
            LoadLanguage();
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
