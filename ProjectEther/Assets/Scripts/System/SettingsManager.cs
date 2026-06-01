using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering.Universal;

namespace OsuVR
{
    public class SettingsManager : MonoBehaviour
    {
        public static SettingsManager Instance { get; private set; }

        [Header("Settings Asset")]
        [SerializeField]
        private GameSettings settings;

        [Header("Audio Mixer")]
        [SerializeField]
        private AudioMixer audioMixer;

        public GameSettings Settings => settings;

        private const string PREF_KEY_AUDIO_OFFSET = "Settings_AudioOffset";
        private const string PREF_KEY_MASTER_VOLUME = "Settings_MasterVolume";
        private const string PREF_KEY_MUSIC_VOLUME = "Settings_MusicVolume";
        private const string PREF_KEY_SFX_VOLUME = "Settings_SFXVolume";
        private const string PREF_KEY_QUALITY_LEVEL = "Settings_QualityLevel";
        private const string PREF_KEY_VSYNC = "Settings_VSync";
        private const string PREF_KEY_AA = "Settings_AA";
        private const string PREF_KEY_PARTICLE_DENSITY = "Settings_ParticleDensity";
        private const string PREF_KEY_RENDER_SCALE = "Settings_RenderScale";
        private const string PREF_KEY_HAPTICS = "Settings_Haptics";
        private const string PREF_KEY_HAPTIC_INTENSITY = "Settings_HapticIntensity";
        private const string PREF_KEY_HEIGHT_OFFSET = "Settings_HeightOffset";
        private const string PREF_KEY_LEFT_CTRL_Z = "Settings_LeftCtrlZ";
        private const string PREF_KEY_RIGHT_CTRL_Z = "Settings_RightCtrlZ";
        private const string PREF_KEY_LEFT_CTRL_Y = "Settings_LeftCtrlY";
        private const string PREF_KEY_RIGHT_CTRL_Y = "Settings_RightCtrlY";
        private const string PREF_KEY_CTRL_ROT = "Settings_CtrlRot";
        private const string PREF_KEY_SHOW_ACCURACY = "Settings_ShowAccuracy";
        private const string PREF_KEY_DISPLAY_ORIGINAL_LANG = "Settings_DisplayOriginalLang";
        private const string PREF_KEY_ENABLE_STORYBOARD = "Settings_EnableStoryboard";
        private const string PREF_KEY_ENABLE_SB_PLAYBACK = "Settings_EnableSBPlayback";
        private const string PREF_KEY_SB_SCREEN_DISTANCE = "Settings_SBScreenDistance";
        private const string PREF_KEY_SB_SCREEN_ALPHA = "Settings_SBScreenAlpha";

        private const int DEFAULT_TARGET_FPS = -1; // -1 = 不限制 (PC), 由 FrameRateUnlocker 处理 Android

        // =========================================================
        // 跨平台画质预设
        // =========================================================

        public struct GraphicsPreset
        {
            public int quality;
            public int aa;
            public float renderScale;
            public float particleDensity;
            public int targetFPS; // 仅用于 Android，PC 始终为 -1（无限制）

            public GraphicsPreset(int quality, int aa, float renderScale, float particleDensity, int targetFPS = -1)
            {
                this.quality = quality;
                this.aa = aa;
                this.renderScale = renderScale;
                this.particleDensity = particleDensity;
                this.targetFPS = targetFPS;
            }
        }

        /// <summary>
        /// PC VR 预设：高画质优先，桌面 GPU 无压力
        /// </summary>
        private static readonly GraphicsPreset[] PC_PRESETS =
        {
            new GraphicsPreset(0, 0, 0.80f, 0.50f),   // Low
            new GraphicsPreset(1, 4, 0.90f, 0.80f),   // Medium
            new GraphicsPreset(2, 4, 1.00f, 1.00f),   // High (默认)
            new GraphicsPreset(3, 8, 1.00f, 1.00f),   // Ultra
        };

        /// <summary>
        /// Standalone VR 预设：不锁帧，跑满设备最高刷新率
        /// Medium+ 至少 2x AA，Ultra 用 100% RenderScale
        /// </summary>
        private static readonly GraphicsPreset[] STANDALONE_PRESETS =
        {
            new GraphicsPreset(0, 0, 0.55f, 0.45f),  // Low
            new GraphicsPreset(1, 2, 0.70f, 0.55f),  // Medium (2x AA)
            new GraphicsPreset(2, 2, 0.85f, 0.65f),  // High (默认, 2x AA)
            new GraphicsPreset(3, 4, 1.00f, 0.75f),  // Ultra (4x AA, RS=1.0)
        };

        /// <summary>
        /// 根据当前平台返回对应的 4 档预设
        /// </summary>
        public GraphicsPreset[] GetPlatformPresets()
        {
            return Application.platform == RuntimePlatform.Android
                ? STANDALONE_PRESETS
                : PC_PRESETS;
        }

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeSettings();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void InitializeSettings()
        {
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<GameSettings>();
            }

            LoadSettings();
            ApplyAllSettings();
        }

        #region Load/Save

        public void LoadSettings()
        {
            // 检测是否首次运行（无任何 PlayerPrefs 记录）
            bool isFirstRun = !PlayerPrefs.HasKey(PREF_KEY_QUALITY_LEVEL);

            settings.audioOffsetMs = PlayerPrefs.GetFloat(PREF_KEY_AUDIO_OFFSET, 0f);
            settings.masterVolume = PlayerPrefs.GetFloat(PREF_KEY_MASTER_VOLUME, 1.0f);
            settings.musicVolume = PlayerPrefs.GetFloat(PREF_KEY_MUSIC_VOLUME, 0.8f);
            settings.sfxVolume = PlayerPrefs.GetFloat(PREF_KEY_SFX_VOLUME, 1.0f);
            settings.enableVSync = PlayerPrefs.GetInt(PREF_KEY_VSYNC, 0) == 1;

            if (isFirstRun)
            {
                // 首次运行：应用平台 High 预设作为默认值
                var presets = GetPlatformPresets();
                var defaultPreset = presets[1]; // Medium = index 1，新玩家默认中画质，防止卡顿
                settings.qualityLevel = defaultPreset.quality;
                settings.antiAliasing = defaultPreset.aa;
                settings.renderScale = defaultPreset.renderScale;
                settings.particleDensity = defaultPreset.particleDensity;
                Debug.Log($"[SettingsManager] First run: applied platform defaults (Quality={defaultPreset.quality}, AA={defaultPreset.aa}, RS={defaultPreset.renderScale:F2}, PD={defaultPreset.particleDensity:F2})");
            }
            else
            {
                settings.qualityLevel = PlayerPrefs.GetInt(PREF_KEY_QUALITY_LEVEL, 1);
                settings.antiAliasing = PlayerPrefs.GetInt(PREF_KEY_AA, 4);
                settings.renderScale = PlayerPrefs.GetFloat(PREF_KEY_RENDER_SCALE, 1.0f);
                settings.particleDensity = PlayerPrefs.GetFloat(PREF_KEY_PARTICLE_DENSITY, 1.0f);
            }
            settings.enableHaptics = PlayerPrefs.GetInt(PREF_KEY_HAPTICS, 1) == 1;
            settings.hapticIntensity = PlayerPrefs.GetFloat(PREF_KEY_HAPTIC_INTENSITY, 0.8f);
            settings.playerHeightOffset = PlayerPrefs.GetFloat(PREF_KEY_HEIGHT_OFFSET, 0f);
            settings.leftControllerZOffset = PlayerPrefs.GetFloat(PREF_KEY_LEFT_CTRL_Z, 0f);
            settings.rightControllerZOffset = PlayerPrefs.GetFloat(PREF_KEY_RIGHT_CTRL_Z, 0f);
            settings.leftControllerYOffset = PlayerPrefs.GetFloat(PREF_KEY_LEFT_CTRL_Y, 0f);
            settings.rightControllerYOffset = PlayerPrefs.GetFloat(PREF_KEY_RIGHT_CTRL_Y, 0f);
            settings.controllerRotationOffset = PlayerPrefs.GetFloat(PREF_KEY_CTRL_ROT, 0f);
            settings.showAccuracy = PlayerPrefs.GetInt(PREF_KEY_SHOW_ACCURACY, 1) == 1;
            settings.displayOriginalLanguage = PlayerPrefs.GetInt(PREF_KEY_DISPLAY_ORIGINAL_LANG, 0) == 1;
            settings.enableStoryboard = PlayerPrefs.GetInt(PREF_KEY_ENABLE_STORYBOARD, 1) == 1;
            settings.enableStoryboardPlayback = PlayerPrefs.GetInt(PREF_KEY_ENABLE_SB_PLAYBACK, 1) == 1;
            settings.storyboardScreenDistance = PlayerPrefs.GetFloat(PREF_KEY_SB_SCREEN_DISTANCE, 12.5f);
            settings.storyboardScreenAlpha = PlayerPrefs.GetFloat(PREF_KEY_SB_SCREEN_ALPHA, 0.5f);

            Debug.Log("[SettingsManager] Settings loaded from PlayerPrefs");
        }

        public void SaveSettings()
        {
            PlayerPrefs.SetFloat(PREF_KEY_AUDIO_OFFSET, settings.audioOffsetMs);
            PlayerPrefs.SetFloat(PREF_KEY_MASTER_VOLUME, settings.masterVolume);
            PlayerPrefs.SetFloat(PREF_KEY_MUSIC_VOLUME, settings.musicVolume);
            PlayerPrefs.SetFloat(PREF_KEY_SFX_VOLUME, settings.sfxVolume);
            PlayerPrefs.SetInt(PREF_KEY_QUALITY_LEVEL, settings.qualityLevel);
            PlayerPrefs.SetInt(PREF_KEY_VSYNC, settings.enableVSync ? 1 : 0);
            PlayerPrefs.SetInt(PREF_KEY_AA, settings.antiAliasing);
            PlayerPrefs.SetFloat(PREF_KEY_PARTICLE_DENSITY, settings.particleDensity);
            PlayerPrefs.SetFloat(PREF_KEY_RENDER_SCALE, settings.renderScale);
            PlayerPrefs.SetInt(PREF_KEY_HAPTICS, settings.enableHaptics ? 1 : 0);
            PlayerPrefs.SetFloat(PREF_KEY_HAPTIC_INTENSITY, settings.hapticIntensity);
            PlayerPrefs.SetFloat(PREF_KEY_HEIGHT_OFFSET, settings.playerHeightOffset);
            PlayerPrefs.SetFloat(PREF_KEY_LEFT_CTRL_Z, settings.leftControllerZOffset);
            PlayerPrefs.SetFloat(PREF_KEY_RIGHT_CTRL_Z, settings.rightControllerZOffset);
            PlayerPrefs.SetFloat(PREF_KEY_LEFT_CTRL_Y, settings.leftControllerYOffset);
            PlayerPrefs.SetFloat(PREF_KEY_RIGHT_CTRL_Y, settings.rightControllerYOffset);
            PlayerPrefs.SetFloat(PREF_KEY_CTRL_ROT, settings.controllerRotationOffset);
            PlayerPrefs.SetInt(PREF_KEY_SHOW_ACCURACY, settings.showAccuracy ? 1 : 0);
            PlayerPrefs.SetInt(PREF_KEY_DISPLAY_ORIGINAL_LANG, settings.displayOriginalLanguage ? 1 : 0);
            PlayerPrefs.SetInt(PREF_KEY_ENABLE_STORYBOARD, settings.enableStoryboard ? 1 : 0);
            PlayerPrefs.SetInt(PREF_KEY_ENABLE_SB_PLAYBACK, settings.enableStoryboardPlayback ? 1 : 0);
            PlayerPrefs.SetFloat(PREF_KEY_SB_SCREEN_DISTANCE, settings.storyboardScreenDistance);
            PlayerPrefs.SetFloat(PREF_KEY_SB_SCREEN_ALPHA, settings.storyboardScreenAlpha);

            PlayerPrefs.Save();
            Debug.Log("[SettingsManager] Settings saved to PlayerPrefs");
        }

        public void ResetToDefaults()
        {
            settings.ResetToDefaults();
            SaveSettings();
            ApplyAllSettings();
            Debug.Log("[SettingsManager] Settings reset to defaults");
        }

        #endregion

        #region Apply Settings

        public void ApplyAllSettings()
        {
            ApplyAudioSettings();
            ApplyGraphicsSettings();
            ApplyVRSettings();
            ApplyControllerOffsets();
            ApplyStoryboardSettings();
        }

        public void ApplyAudioSettings()
        {
            if (audioMixer != null)
            {
                float masterDb = LinearToDecibel(settings.masterVolume);
                float musicDb = LinearToDecibel(settings.musicVolume);
                float sfxDb = LinearToDecibel(settings.sfxVolume);

                audioMixer.SetFloat("MasterVolume", masterDb);
                audioMixer.SetFloat("MusicVolume", musicDb);
                audioMixer.SetFloat("SFXVolume", sfxDb);
            }

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.SetMasterVolume(settings.masterVolume);
                AudioManager.Instance.SetSFXVolume(settings.sfxVolume);
            }
        }

        public void ApplyGraphicsSettings()
        {
            QualitySettings.SetQualityLevel(settings.qualityLevel, true);
            // 不锁帧：PC 由 VR SDK 控制，Android 由 FrameRateUnlocker 跑满设备刷新率
            Application.targetFrameRate = -1;
            QualitySettings.vSyncCount = settings.enableVSync ? 1 : 0;

            int aaValue = settings.antiAliasing;
            if (aaValue == 0) aaValue = 1;
            else if (aaValue > 0 && (aaValue & (aaValue - 1)) != 0)
            {
                if (aaValue < 2) aaValue = 2;
                else if (aaValue < 4) aaValue = 2;
                else if (aaValue < 8) aaValue = 4;
                else aaValue = 8;
            }
            QualitySettings.antiAliasing = aaValue;

            // 应用 URP Render Scale
            var urpAsset = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
            if (urpAsset != null)
            {
                urpAsset.renderScale = Mathf.Clamp(settings.renderScale, 0.5f, 1.0f);
            }

            if (EtherealEnvironment.Instance != null)
            {
                EtherealEnvironment.Instance.SetParticleDensity(settings.particleDensity);
            }

            Debug.Log($"[SettingsManager] Graphics: Q={settings.qualityLevel}, AA={settings.antiAliasing}, RS={settings.renderScale:F2}, PD={settings.particleDensity:F2}, FPS={Application.targetFrameRate}");
        }

        public void ApplyVRSettings()
        {
            if (HapticManager.Instance != null)
            {
                HapticManager.Instance.SetEnabled(settings.enableHaptics);
                HapticManager.Instance.SetIntensity(settings.hapticIntensity);
            }

            Debug.Log($"[SettingsManager] VR settings applied: Haptics={settings.enableHaptics}");
        }

        public void ApplyControllerOffsets()
        {
            var rayControllers = FindObjectsOfType<RayController>();
            foreach (var rc in rayControllers)
            {
                if (rc.isRightHand)
                {
                    rc.directOffset = new Vector3(settings.controllerRotationOffset, settings.rightControllerYOffset, settings.rightControllerZOffset);
                }
                else
                {
                    rc.directOffset = new Vector3(settings.controllerRotationOffset, settings.leftControllerYOffset, settings.leftControllerZOffset);
                }
            }
            Debug.Log($"[SettingsManager] Controller offsets applied: Rot={settings.controllerRotationOffset}°, L_Z={settings.leftControllerZOffset}, L_Y={settings.leftControllerYOffset}, R_Z={settings.rightControllerZOffset}, R_Y={settings.rightControllerYOffset}");
        }

        public void ApplyStoryboardSettings()
        {
            if (OsuVR.Storyboard.HolographicScreenManager.Instance != null)
            {
                OsuVR.Storyboard.HolographicScreenManager.Instance.OnSettingsChanged();
            }
            Debug.Log($"[SettingsManager] Storyboard applied: Enable={settings.enableStoryboard}, Playback={settings.enableStoryboardPlayback}, Distance={settings.storyboardScreenDistance:F1}m, Alpha={settings.storyboardScreenAlpha:F2}");
        }

        #endregion

        #region Helper Methods

        private float LinearToDecibel(float linear)
        {
            if (linear <= 0.0001f)
                return -80f;
            return 20f * Mathf.Log10(linear);
        }

        private float DecibelToLinear(float db)
        {
            return Mathf.Pow(10f, db / 20f);
        }

        #endregion

        #region Runtime Setters

        public void SetAudioOffset(float offsetMs)
        {
            settings.audioOffsetMs = Mathf.Clamp(offsetMs, -200f, 200f);
            SaveSettings();
        }

        public void SetMasterVolume(float volume)
        {
            settings.masterVolume = Mathf.Clamp01(volume);
            ApplyAudioSettings();
            SaveSettings();
        }

        public void SetMusicVolume(float volume)
        {
            settings.musicVolume = Mathf.Clamp01(volume);
            ApplyAudioSettings();
            SaveSettings();
        }

        public void SetSFXVolume(float volume)
        {
            settings.sfxVolume = Mathf.Clamp01(volume);
            ApplyAudioSettings();
            SaveSettings();
        }

        public void SetQualityLevel(int level)
        {
            // 应用该平台对应档位的完整预设（AA/RenderScale/Particle 联动）
            var presets = GetPlatformPresets();
            int idx = Mathf.Clamp(level, 0, presets.Length - 1);
            ApplyPreset(presets[idx]);
        }

        public void SetAntiAliasing(int aa)
        {
            settings.antiAliasing = aa;
            ApplyGraphicsSettings();
            SaveSettings();
        }

        public void SetParticleDensity(float density)
        {
            settings.particleDensity = Mathf.Clamp01(density);
            if (EtherealEnvironment.Instance != null)
            {
                EtherealEnvironment.Instance.SetParticleDensity(density);
            }
            SaveSettings();
        }

        public void SetRenderScale(float scale)
        {
            settings.renderScale = Mathf.Clamp(scale, 0.5f, 1.0f);
            ApplyGraphicsSettings();
            SaveSettings();
        }

        /// <summary>
        /// 一次性应用画质预设的所有参数（Quality/AA/RenderScale/ParticleDensity）
        /// </summary>
        public void ApplyPreset(GraphicsPreset preset)
        {
            settings.qualityLevel = preset.quality;
            settings.antiAliasing = preset.aa;
            settings.renderScale = preset.renderScale;
            settings.particleDensity = preset.particleDensity;
            ApplyGraphicsSettings();
            SaveSettings();
        }

        public void SetHapticsEnabled(bool enabled)
        {
            settings.enableHaptics = enabled;
            ApplyVRSettings();
            SaveSettings();
        }

        public void SetHapticIntensity(float intensity)
        {
            settings.hapticIntensity = Mathf.Clamp01(intensity);
            ApplyVRSettings();
            SaveSettings();
        }

        public void SetControllerOffsets(float leftZ, float rightZ, float leftY, float rightY, float rotation)
        {
            settings.leftControllerZOffset = leftZ;
            settings.rightControllerZOffset = rightZ;
            settings.leftControllerYOffset = leftY;
            settings.rightControllerYOffset = rightY;
            settings.controllerRotationOffset = rotation;
            ApplyControllerOffsets();
            SaveSettings();
        }

        public void SetStoryboardEnabled(bool enabled)
        {
            settings.enableStoryboard = enabled;
            ApplyStoryboardSettings();
            SaveSettings();
        }

        public void SetStoryboardPlaybackEnabled(bool enabled)
        {
            settings.enableStoryboardPlayback = enabled;
            ApplyStoryboardSettings();
            SaveSettings();
        }

        public void SetStoryboardScreenDistance(float distance)
        {
            settings.storyboardScreenDistance = Mathf.Clamp(distance, 7.5f, 15f);
            ApplyStoryboardSettings();
            SaveSettings();
        }

        public void SetStoryboardScreenAlpha(float alpha)
        {
            settings.storyboardScreenAlpha = Mathf.Clamp01(alpha);
            ApplyStoryboardSettings();
            SaveSettings();
        }

        #endregion
    }
}
