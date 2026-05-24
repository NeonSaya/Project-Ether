using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

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
