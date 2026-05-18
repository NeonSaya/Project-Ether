using UnityEngine;
using UnityEngine.Audio;

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

        private const int DEFAULT_TARGET_FPS = 120;

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
            settings.audioOffsetMs = PlayerPrefs.GetFloat(PREF_KEY_AUDIO_OFFSET, 0f);
            settings.masterVolume = PlayerPrefs.GetFloat(PREF_KEY_MASTER_VOLUME, 1.0f);
            settings.musicVolume = PlayerPrefs.GetFloat(PREF_KEY_MUSIC_VOLUME, 0.8f);
            settings.sfxVolume = PlayerPrefs.GetFloat(PREF_KEY_SFX_VOLUME, 1.0f);
            settings.qualityLevel = PlayerPrefs.GetInt(PREF_KEY_QUALITY_LEVEL, 2);
            settings.enableVSync = PlayerPrefs.GetInt(PREF_KEY_VSYNC, 0) == 1;
            settings.antiAliasing = PlayerPrefs.GetInt(PREF_KEY_AA, 4);
            settings.particleDensity = PlayerPrefs.GetFloat(PREF_KEY_PARTICLE_DENSITY, 1.0f);
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
            }
        }

        public void ApplyGraphicsSettings()
        {
            QualitySettings.SetQualityLevel(settings.qualityLevel, true);
            Application.targetFrameRate = DEFAULT_TARGET_FPS;
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

            if (EtherealEnvironment.Instance != null)
            {
                EtherealEnvironment.Instance.SetParticleDensity(settings.particleDensity);
            }

            Debug.Log($"[SettingsManager] Graphics applied: Quality={settings.qualityLevel}, FPS={DEFAULT_TARGET_FPS}, AA={settings.antiAliasing}");
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
            settings.qualityLevel = Mathf.Clamp(level, 0, 3);
            ApplyGraphicsSettings();
            SaveSettings();
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

        #endregion
    }
}
