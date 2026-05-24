using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace OsuVR
{
    /// <summary>
    /// 画面设置页面：Quality, Anti-Aliasing, Particle Density
    /// </summary>
    public class GraphicsSettingsPage : SettingsPageBase
    {
        public override string TabLocalizationKey => "ui_tab_graphics";

        private TMP_Dropdown qualityDropdown;
        private TMP_Dropdown antiAliasingDropdown;
        private Slider particleDensitySlider;

        // Anti-aliasing bidirectional mapping: index <-> value
        private static readonly int[] AaIndexToValue = { 0, 2, 4, 8 };
        private const string PercentFormat = "{0:F0}%";

        public override void BuildContent(RectTransform parent, GameSettings tempSettings, float contentWidth)
        {
            // Quality dropdown
            var qualityOptions = new List<string>
            {
                LocalizationManager.GetText("ui_low"),
                LocalizationManager.GetText("ui_medium"),
                LocalizationManager.GetText("ui_high"),
                LocalizationManager.GetText("ui_ultra")
            };
            qualityDropdown = CreateDropdown(parent, "Quality", "ui_quality",
                qualityOptions, tempSettings.qualityLevel,
                v =>
                {
                    tempSettings.qualityLevel = v;
                    SettingsManager.Instance.SetQualityLevel(v);
                    PlayClickSound();
                });

            // Anti-aliasing dropdown
            var aaOptions = new List<string>
            {
                LocalizationManager.GetText("ui_off"),
                "2x", "4x", "8x"
            };
            int aaIndex = AaValueToIndex(tempSettings.antiAliasing);
            antiAliasingDropdown = CreateDropdown(parent, "Anti-Aliasing", "ui_anti_aliasing",
                aaOptions, aaIndex,
                v =>
                {
                    int aaValue = AaIndexToValue[Mathf.Clamp(v, 0, AaIndexToValue.Length - 1)];
                    tempSettings.antiAliasing = aaValue;
                    SettingsManager.Instance.SetAntiAliasing(aaValue);
                    PlayClickSound();
                });

            // Particle density slider
            particleDensitySlider = CreateSlider(parent, "Particle Density", "ui_particle_density",
                0f, 1f, tempSettings.particleDensity, PercentFormat,
                v =>
                {
                    tempSettings.particleDensity = v;
                    SettingsManager.Instance.SetParticleDensity(v);
                }, valueScale: 100f);
        }

        public override void RefreshUI(GameSettings tempSettings)
        {
            SetDropdownValueWithoutNotify(qualityDropdown, tempSettings.qualityLevel);
            SetDropdownValueWithoutNotify(antiAliasingDropdown, AaValueToIndex(tempSettings.antiAliasing));
            SetSliderValueWithoutNotify(particleDensitySlider, tempSettings.particleDensity, PercentFormat, 100f);
        }

        protected override string GetFormatForSlider(Slider slider)
        {
            return PercentFormat;
        }

        private static int AaValueToIndex(int value)
        {
            for (int i = 0; i < AaIndexToValue.Length; i++)
            {
                if (AaIndexToValue[i] == value) return i;
            }
            return 2; // default to 4x
        }
    }
}
