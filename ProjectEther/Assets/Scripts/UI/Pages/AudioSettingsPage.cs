using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace OsuVR
{
    /// <summary>
    /// 音频设置页面：Audio Offset, Master/Music/SFX Volume
    /// </summary>
    public class AudioSettingsPage : SettingsPageBase
    {
        public override string TabLocalizationKey => "ui_tab_audio";

        private Slider audioOffsetSlider;
        private Slider masterVolumeSlider;
        private Slider musicVolumeSlider;
        private Slider sfxVolumeSlider;

        private const string OffsetFormat = "{0:F0} ms";
        private const string VolumeFormat = "{0:F0}%";

        public override void BuildContent(RectTransform parent, GameSettings tempSettings, float contentWidth)
        {
            // Volume sliders (Prefab 顺序: Master → Music → SFX)
            masterVolumeSlider = CreateSlider(parent, "Master Volume", "ui_master_volume",
                0f, 1f, tempSettings.masterVolume, VolumeFormat,
                v =>
                {
                    tempSettings.masterVolume = v;
                    SettingsManager.Instance.SetMasterVolume(v);
                }, valueScale: 100f);

            musicVolumeSlider = CreateSlider(parent, "Music Volume", "ui_music_volume",
                0f, 1f, tempSettings.musicVolume, VolumeFormat,
                v =>
                {
                    tempSettings.musicVolume = v;
                    SettingsManager.Instance.SetMusicVolume(v);
                }, valueScale: 100f);

            sfxVolumeSlider = CreateSlider(parent, "SFX Volume", "ui_sfx_volume",
                0f, 1f, tempSettings.sfxVolume, VolumeFormat,
                v =>
                {
                    tempSettings.sfxVolume = v;
                    SettingsManager.Instance.SetSFXVolume(v);
                }, valueScale: 100f);

            // Audio Offset（Prefab 精确结构：Header + Slider + FineTune 按钮行）
            audioOffsetSlider = CreateAudioOffsetRow(parent, "Audio Offset", "ui_audio_offset",
                -200f, 200f, tempSettings.audioOffsetMs, OffsetFormat,
                v =>
                {
                    tempSettings.audioOffsetMs = v;
                    SettingsManager.Instance.SetAudioOffset(v);
                },
                delta =>
                {
                    float newVal = Mathf.Clamp(tempSettings.audioOffsetMs + delta, -200f, 200f);
                    tempSettings.audioOffsetMs = newVal;
                    SettingsManager.Instance.SetAudioOffset(newVal);
                    audioOffsetSlider.SetValueWithoutNotify(newVal);
                    // FineTune 后播放点击音效
                    PlayClickSound();
                });
        }

        public override void RefreshUI(GameSettings tempSettings)
        {
            SetSliderValueWithoutNotify(masterVolumeSlider, tempSettings.masterVolume, VolumeFormat, 100f);
            SetSliderValueWithoutNotify(musicVolumeSlider, tempSettings.musicVolume, VolumeFormat, 100f);
            SetSliderValueWithoutNotify(sfxVolumeSlider, tempSettings.sfxVolume, VolumeFormat, 100f);
            SetSliderValueWithoutNotify(audioOffsetSlider, tempSettings.audioOffsetMs, OffsetFormat);
        }

        protected override string GetFormatForSlider(Slider slider)
        {
            if (slider == audioOffsetSlider) return OffsetFormat;
            return VolumeFormat;
        }
    }
}
