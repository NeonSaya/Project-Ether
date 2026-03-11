using UnityEngine;

namespace OsuVR
{
    [CreateAssetMenu(fileName = "GameSettings", menuName = "Project Ether/GameSettings")]
    public class GameSettings : ScriptableObject
    {
        [Header("Audio Settings")]
        [Range(-200f, 200f)]
        [Tooltip("Audio offset in milliseconds. Positive = notes appear later, Negative = notes appear earlier")]
        public float audioOffsetMs = 0f;

        [Range(0f, 1f)]
        [Tooltip("Master volume for all audio")]
        public float masterVolume = 1.0f;

        [Range(0f, 1f)]
        [Tooltip("Music volume")]
        public float musicVolume = 0.8f;

        [Range(0f, 1f)]
        [Tooltip("Sound effects volume (hit sounds, slider sounds, etc.)")]
        public float sfxVolume = 1.0f;

        [Header("Graphics Settings")]
        [Tooltip("Quality level index (0=Low, 1=Medium, 2=High, 3=Ultra)")]
        [Range(0, 3)]
        public int qualityLevel = 2;

        [Tooltip("Enable VSync")]
        public bool enableVSync = false;

        [Tooltip("Anti-aliasing level (0=Disabled, 2=2x, 4=4x, 8=8x)")]
        [Range(0, 8)]
        public int antiAliasing = 4;

        [Range(0f, 1f)]
        [Tooltip("Particle density multiplier (0=Off, 1=Full)")]
        public float particleDensity = 1.0f;

        [Header("VR Settings")]
        [Tooltip("Enable haptic feedback")]
        public bool enableHaptics = true;

        [Range(0f, 1f)]
        [Tooltip("Haptic feedback intensity")]
        public float hapticIntensity = 0.8f;

        [Tooltip("Player height offset in meters")]
        [Range(-0.5f, 0.5f)]
        public float playerHeightOffset = 0f;

        [Header("Controller Offset Settings")]
        [Tooltip("Left controller Z offset (forward/backward) in meters")]
        [Range(-0.5f, 0.5f)]
        public float leftControllerZOffset = 0f;

        [Tooltip("Right controller Z offset (forward/backward) in meters")]
        [Range(-0.5f, 0.5f)]
        public float rightControllerZOffset = 0f;

        [Tooltip("Left controller Y offset (up/down) in meters")]
        [Range(-0.3f, 0.3f)]
        public float leftControllerYOffset = 0f;

        [Tooltip("Right controller Y offset (up/down) in meters")]
        [Range(-0.3f, 0.3f)]
        public float rightControllerYOffset = 0f;

        [Tooltip("Controller rotation offset in degrees")]
        [Range(-45f, 45f)]
        public float controllerRotationOffset = 0f;

        [Header("Gameplay Settings")]
        [Tooltip("Show accuracy")]
        public bool showAccuracy = true;

        [Tooltip("Display song names in original language (Unicode) instead of Romanized")]
        public bool displayOriginalLanguage = false;

        [Header("Editor Only")]
        [Tooltip("Reset to defaults on next load (Editor only)")]
        public bool resetToDefaults = false;

        public GameSettings Clone()
        {
            var clone = CreateInstance<GameSettings>();
            clone.audioOffsetMs = this.audioOffsetMs;
            clone.masterVolume = this.masterVolume;
            clone.musicVolume = this.musicVolume;
            clone.sfxVolume = this.sfxVolume;
            clone.qualityLevel = this.qualityLevel;
            clone.enableVSync = this.enableVSync;
            clone.antiAliasing = this.antiAliasing;
            clone.particleDensity = this.particleDensity;
            clone.enableHaptics = this.enableHaptics;
            clone.hapticIntensity = this.hapticIntensity;
            clone.playerHeightOffset = this.playerHeightOffset;
            clone.leftControllerZOffset = this.leftControllerZOffset;
            clone.rightControllerZOffset = this.rightControllerZOffset;
            clone.leftControllerYOffset = this.leftControllerYOffset;
            clone.rightControllerYOffset = this.rightControllerYOffset;
            clone.controllerRotationOffset = this.controllerRotationOffset;
            clone.showAccuracy = this.showAccuracy;
            clone.displayOriginalLanguage = this.displayOriginalLanguage;
            return clone;
        }

        public void CopyFrom(GameSettings other)
        {
            if (other == null) return;
            audioOffsetMs = other.audioOffsetMs;
            masterVolume = other.masterVolume;
            musicVolume = other.musicVolume;
            sfxVolume = other.sfxVolume;
            qualityLevel = other.qualityLevel;
            enableVSync = other.enableVSync;
            antiAliasing = other.antiAliasing;
            particleDensity = other.particleDensity;
            enableHaptics = other.enableHaptics;
            hapticIntensity = other.hapticIntensity;
            playerHeightOffset = other.playerHeightOffset;
            leftControllerZOffset = other.leftControllerZOffset;
            rightControllerZOffset = other.rightControllerZOffset;
            leftControllerYOffset = other.leftControllerYOffset;
            rightControllerYOffset = other.rightControllerYOffset;
            controllerRotationOffset = other.controllerRotationOffset;
            showAccuracy = other.showAccuracy;
            displayOriginalLanguage = other.displayOriginalLanguage;
        }

        public void ResetToDefaults()
        {
            audioOffsetMs = 0f;
            masterVolume = 1.0f;
            musicVolume = 0.8f;
            sfxVolume = 1.0f;
            qualityLevel = 2;
            enableVSync = false;
            antiAliasing = 4;
            particleDensity = 1.0f;
            enableHaptics = true;
            hapticIntensity = 0.8f;
            playerHeightOffset = 0f;
            leftControllerZOffset = 0f;
            rightControllerZOffset = 0f;
            leftControllerYOffset = 0f;
            rightControllerYOffset = 0f;
            controllerRotationOffset = 0f;
            showAccuracy = true;
            displayOriginalLanguage = false;
        }
    }
}
