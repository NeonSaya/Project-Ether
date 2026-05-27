using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using OsuVR.Storyboard;

namespace OsuVR
{
    /// <summary>
    /// 游戏设置页面：Haptics Toggle, Haptic Intensity, Display Original Language, Language Dropdown
    /// </summary>
    public class GameplaySettingsPage : SettingsPageBase
    {
        public override string TabLocalizationKey => "ui_tab_game";

        private Toggle hapticsToggle;
        private Slider hapticIntensitySlider;
        private Toggle displayOriginalLanguageToggle;
        private TMP_Dropdown languageDropdown;

        private Toggle enableStoryboardToggle;
        private Toggle enableStoryboardPlaybackToggle;
        private Slider storyboardDistanceSlider;
        private Slider storyboardAlphaSlider;

        private const string PercentFormat = "{0:F0}%";

        public override void BuildContent(RectTransform parent, GameSettings tempSettings, float contentWidth)
        {
            // Prefab 行顺序: Language → DisplayOriginal → HapticIntensity → EnableHaptics

            // Language dropdown
            var languageNames = new List<string>(LocalizationManager.GetAllLanguageNames());
            int currentLangIndex = LocalizationManager.GetCurrentLanguageIndex();
            languageDropdown = CreateDropdown(parent, "Language", "ui_language",
                languageNames, currentLangIndex,
                v =>
                {
                    LocalizationManager.SetLanguageByIndex(v);
                    SettingsManager.Instance.SaveSettings();
                    PlayClickSound();
                });

            // Display original language toggle
            displayOriginalLanguageToggle = CreateToggle(parent,
                "Display Song Names in Original Language", "ui_display_original_language",
                tempSettings.displayOriginalLanguage,
                v =>
                {
                    tempSettings.displayOriginalLanguage = v;
                    SettingsManager.Instance.Settings.displayOriginalLanguage = v;
                    SettingsManager.Instance.SaveSettings();
                    LocalizationManager.ForceUpdateLanguage();
                    PlayClickSound();
                });

            // --- 背景板设置 ---

            // 背景板总开关
            enableStoryboardToggle = CreateToggle(parent,
                "Background Screen", "ui_enable_storyboard",
                tempSettings.enableStoryboard,
                v =>
                {
                    tempSettings.enableStoryboard = v;
                    SettingsManager.Instance.Settings.enableStoryboard = v;
                    SettingsManager.Instance.SaveSettings();
                    HolographicScreenManager.Instance?.OnSettingsChanged();
                    PlayClickSound();
                });

            // 故事板播放开关 (关闭后仅显示背景图)
            enableStoryboardPlaybackToggle = CreateToggle(parent,
                "Storyboard Playback", "ui_enable_storyboard_playback",
                tempSettings.enableStoryboardPlayback,
                v =>
                {
                    tempSettings.enableStoryboardPlayback = v;
                    SettingsManager.Instance.Settings.enableStoryboardPlayback = v;
                    SettingsManager.Instance.SaveSettings();
                    HolographicScreenManager.Instance?.OnSettingsChanged();
                    PlayClickSound();
                });

            // 屏幕距离
            storyboardDistanceSlider = CreateSlider(parent, "Screen Distance", "ui_storyboard_distance",
                7.5f, 15f, tempSettings.storyboardScreenDistance, "{0:F1}m",
                v =>
                {
                    tempSettings.storyboardScreenDistance = v;
                    SettingsManager.Instance.Settings.storyboardScreenDistance = v;
                    SettingsManager.Instance.SaveSettings();
                    HolographicScreenManager.Instance?.OnSettingsChanged();
                });

            // 屏幕透明度
            storyboardAlphaSlider = CreateSlider(parent, "Screen Opacity", "ui_storyboard_alpha",
                0f, 1f, tempSettings.storyboardScreenAlpha, PercentFormat,
                v =>
                {
                    tempSettings.storyboardScreenAlpha = v;
                    SettingsManager.Instance.Settings.storyboardScreenAlpha = v;
                    SettingsManager.Instance.SaveSettings();
                    HolographicScreenManager.Instance?.OnSettingsChanged();
                }, valueScale: 100f);

            // --- Haptics 设置 ---

            // Haptic intensity slider
            hapticIntensitySlider = CreateSlider(parent, "Haptic Intensity", "ui_haptic_intensity",
                0f, 1f, tempSettings.hapticIntensity, PercentFormat,
                v =>
                {
                    tempSettings.hapticIntensity = v;
                    SettingsManager.Instance.SetHapticIntensity(v);
                }, valueScale: 100f);

            // Haptics toggle
            hapticsToggle = CreateToggle(parent, "Enable Haptics", "ui_enable_haptics",
                tempSettings.enableHaptics,
                v =>
                {
                    tempSettings.enableHaptics = v;
                    SettingsManager.Instance.SetHapticsEnabled(v);
                    PlayClickSound();
                });
        }

        public override void RefreshUI(GameSettings tempSettings)
        {
            SetDropdownValueWithoutNotify(languageDropdown, LocalizationManager.GetCurrentLanguageIndex());
            SetToggleValueWithoutNotify(displayOriginalLanguageToggle, tempSettings.displayOriginalLanguage);
            SetToggleValueWithoutNotify(enableStoryboardToggle, tempSettings.enableStoryboard);
            SetToggleValueWithoutNotify(enableStoryboardPlaybackToggle, tempSettings.enableStoryboardPlayback);
            SetSliderValueWithoutNotify(storyboardDistanceSlider, tempSettings.storyboardScreenDistance, "{0:F1}m");
            SetSliderValueWithoutNotify(storyboardAlphaSlider, tempSettings.storyboardScreenAlpha, PercentFormat, 100f);
            SetSliderValueWithoutNotify(hapticIntensitySlider, tempSettings.hapticIntensity, PercentFormat, 100f);
            SetToggleValueWithoutNotify(hapticsToggle, tempSettings.enableHaptics);
        }

        /// <summary>
        /// 刷新语言下拉框选项（语言切换时调用）
        /// </summary>
        public void RefreshLanguageDropdown()
        {
            if (languageDropdown == null) return;
            var languageNames = new List<string>(LocalizationManager.GetAllLanguageNames());
            languageDropdown.ClearOptions();
            languageDropdown.AddOptions(languageNames);
            SetDropdownValueWithoutNotify(languageDropdown, LocalizationManager.GetCurrentLanguageIndex());
        }

        protected override string GetFormatForSlider(Slider slider)
        {
            return PercentFormat;
        }
    }
}
