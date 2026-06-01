using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

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

        public override void BuildContent(RectTransform parent, GameSettings tempSettings, float contentWidth)
        {
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

            // --- 导入谱面按钮 ---
            UILayoutHelper.CreateButton(parent,
                LocalizationManager.GetText("ui_import_osz"),
                () =>
                {
                    PlayClickSound();
                    OnImportButtonClicked();
                },
                localizationKey: "ui_import_osz",
                width: 360f, height: 50f, fontSize: 18f,
                addBoxCollider: true);
        }

        public override void RefreshUI(GameSettings tempSettings)
        {
            SetDropdownValueWithoutNotify(languageDropdown, LocalizationManager.GetCurrentLanguageIndex());
            SetToggleValueWithoutNotify(displayOriginalLanguageToggle, tempSettings.displayOriginalLanguage);
            SetSliderValueWithoutNotify(hapticIntensitySlider, tempSettings.hapticIntensity, PercentFormat, 100f);
            SetToggleValueWithoutNotify(hapticsToggle, tempSettings.enableHaptics);
        }

        private void OnImportButtonClicked()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            BeatmapImporter.OpenAndroidFilePicker((result, detail) =>
            {
                switch (result)
                {
                    case ImportResult.Success:
                        ShowToast(string.Format(LocalizationManager.GetText("ui_import_success"), detail), Color.green);
                        break;
                    case ImportResult.Cancelled:
                        ShowToast(LocalizationManager.GetText("ui_import_cancelled"), new Color(1f, 0.85f, 0.3f));
                        break;
                    case ImportResult.Error:
                        string errMsg = LocalizationManager.GetText("ui_import_error");
                        if (!string.IsNullOrEmpty(detail))
                            errMsg += "\n" + detail;
                        ShowToast(errMsg, Color.red, 8f);
                        break;
                }
            });
#else
            BeatmapImporter.OpenSongsDirectory();
            ShowToast(LocalizationManager.GetText("ui_import_folder_opened"), new Color(0.4f, 0.8f, 1f));
#endif
        }

        // ============================================================
        //  Toast 提示
        // ============================================================

        private GameObject _toastObj;
        private TextMeshProUGUI _toastTmp;
        private float _toastFadeStart;
        private float _toastDuration;
        private Color _toastColor;
        private bool _toastActive;

        private void ShowToast(string message, Color color, float duration = 3f)
        {
            if (_toastObj == null)
            {
                // 独立世界空间 Canvas，悬浮在玩家面前
                _toastObj = new GameObject("Toast_Import");
                _toastObj.transform.position = new Vector3(0f, 2.5f, 1f);
                _toastObj.transform.localScale = Vector3.one * 0.002f;

                var canvas = _toastObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                var scaler = _toastObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                scaler.dynamicPixelsPerUnit = 10f;
                _toastObj.AddComponent<CanvasGroup>();

                var toastText = new GameObject("Text");
                toastText.transform.SetParent(_toastObj.transform, false);
                var rt = toastText.AddComponent<RectTransform>();
                rt.sizeDelta = new Vector2(800f, 120f);

                _toastTmp = toastText.AddComponent<TextMeshProUGUI>();
                _toastTmp.fontSize = 22f;
                _toastTmp.fontStyle = FontStyles.Bold;
                _toastTmp.alignment = TextAlignmentOptions.Center;
                _toastTmp.enableAutoSizing = false;
                _toastTmp.enableWordWrapping = true;
                _toastTmp.overflowMode = TextOverflowModes.Ellipsis;
            }

            _toastTmp.text = message;
            _toastColor = color;
            _toastTmp.color = color;
            _toastObj.SetActive(true);
            _toastFadeStart = Time.unscaledTime;
            _toastDuration = duration;
            _toastActive = true;
        }

        public void UpdateToast()
        {
            if (!_toastActive || _toastObj == null) return;

            float elapsed = Time.unscaledTime - _toastFadeStart;
            if (elapsed >= _toastDuration)
            {
                _toastObj.SetActive(false);
                _toastActive = false;
                return;
            }

            float fadeStart = _toastDuration * 0.6f;
            if (elapsed > fadeStart)
            {
                float alpha = 1f - (elapsed - fadeStart) / (_toastDuration - fadeStart);
                _toastTmp.color = new Color(_toastColor.r, _toastColor.g, _toastColor.b, alpha);
            }
        }

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
