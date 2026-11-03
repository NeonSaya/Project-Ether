using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OsuVR
{
    /// <summary>
    /// 游戏设置页面：Language, Display Original Language, Haptics, 导入谱面
    /// </summary>
    public class GameplaySettingsPage : SettingsPageBase
    {
        public override string TabLocalizationKey => "ui_tab_game";

        private Toggle hapticsToggle;
        private Slider hapticIntensitySlider;
        private Toggle displayOriginalLanguageToggle;
        private TMP_Dropdown languageDropdown;

        private const string PercentFormat = "{0:F0}%";

        public override void BuildContent(
            RectTransform parent,
            GameSettings tempSettings,
            float contentWidth
        )
        {
            // 语言下拉框
            var languageNames = new List<string>(LocalizationManager.GetAllLanguageNames());
            int currentLangIndex = LocalizationManager.GetCurrentLanguageIndex();
            languageDropdown = CreateDropdown(
                parent,
                "Language",
                "ui_language",
                languageNames,
                currentLangIndex,
                v =>
                {
                    LocalizationManager.SetLanguageByIndex(v);
                    SettingsManager.Instance.SaveSettings();
                    PlayClickSound();
                }
            );

            // 以原文显示歌名开关
            displayOriginalLanguageToggle = CreateToggle(
                parent,
                "Display Song Names in Original Language",
                "ui_display_original_language",
                tempSettings.displayOriginalLanguage,
                v =>
                {
                    tempSettings.displayOriginalLanguage = v;
                    SettingsManager.Instance.Settings.displayOriginalLanguage = v;
                    SettingsManager.Instance.SaveSettings();
                    LocalizationManager.ForceUpdateLanguage();
                    PlayClickSound();
                }
            );

            // 触觉强度滑条
            hapticIntensitySlider = CreateSlider(
                parent,
                "Haptic Intensity",
                "ui_haptic_intensity",
                0f,
                1f,
                tempSettings.hapticIntensity,
                PercentFormat,
                v =>
                {
                    tempSettings.hapticIntensity = v;
                    SettingsManager.Instance.SetHapticIntensity(v);
                },
                valueScale: 100f
            );

            // 触觉反馈开关
            hapticsToggle = CreateToggle(
                parent,
                "Enable Haptics",
                "ui_enable_haptics",
                tempSettings.enableHaptics,
                v =>
                {
                    tempSettings.enableHaptics = v;
                    SettingsManager.Instance.SetHapticsEnabled(v);
                    PlayClickSound();
                }
            );

            // --- 导入谱面按钮 ---
            UILayoutHelper.CreateButton(
                parent,
                LocalizationManager.GetText("ui_import_osz"),
                () =>
                {
                    PlayClickSound();
                    OnImportButtonClicked();
                },
                localizationKey: "ui_import_osz",
                width: 360f,
                height: 50f,
                fontSize: 18f,
                addBoxCollider: true
            );
        }

        public override void RefreshUI(GameSettings tempSettings)
        {
            SetDropdownValueWithoutNotify(
                languageDropdown,
                LocalizationManager.GetCurrentLanguageIndex()
            );
            SetToggleValueWithoutNotify(
                displayOriginalLanguageToggle,
                tempSettings.displayOriginalLanguage
            );
            SetSliderValueWithoutNotify(
                hapticIntensitySlider,
                tempSettings.hapticIntensity,
                PercentFormat,
                100f
            );
            SetToggleValueWithoutNotify(hapticsToggle, tempSettings.enableHaptics);
        }

        private void OnImportButtonClicked()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            BeatmapImporter.OpenAndroidFilePicker();
#else
            BeatmapImporter.OpenSongsDirectory();
            ShowToast(
                LocalizationManager.GetText("ui_import_folder_opened"),
                new Color(0.4f, 0.8f, 1f)
            );
#endif
        }

        // ============================================================
        //  Toast 提示
        // ============================================================

        private void ShowToast(string message, Color color, float duration = 3f)
        {
            VRToast.Show(message, color, duration);
        }

        public void RefreshLanguageDropdown()
        {
            if (languageDropdown == null)
                return;
            var languageNames = new List<string>(LocalizationManager.GetAllLanguageNames());
            languageDropdown.ClearOptions();
            languageDropdown.AddOptions(languageNames);
            SetDropdownValueWithoutNotify(
                languageDropdown,
                LocalizationManager.GetCurrentLanguageIndex()
            );
        }

        protected override string GetFormatForSlider(Slider slider)
        {
            return PercentFormat;
        }
    }
}
