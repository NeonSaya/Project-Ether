using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace OsuVR
{
    /// <summary>
    /// 设置界面控制器
    /// VR 世界空间 UI
    /// </summary>
    public class SettingsController : MonoBehaviour
    {
        [Header("UI 面板")]
        public Transform settingsPanel;

        [Header("音频设置")]
        public Slider masterVolumeSlider;
        public Slider musicVolumeSlider;
        public Slider sfxVolumeSlider;
        public TextMeshProUGUI masterVolumeText;
        public TextMeshProUGUI musicVolumeText;
        public TextMeshProUGUI sfxVolumeText;

        [Header("游戏设置")]
        public Slider noteSpeedSlider;
        public TextMeshProUGUI noteSpeedText;
        public Toggle useAutoPlayToggle;

        [Header("视觉设置")]
        public Slider brightnessSlider;
        public TextMeshProUGUI brightnessText;
        public Toggle showFPSCounterToggle;

        [Header("VR 设置")]
        public Slider turnSpeedSlider;
        public TextMeshProUGUI turnSpeedText;
        public Toggle smoothTurnToggle;

        [Header("按钮")]
        public Button backButton;
        public Button resetButton;

        [Header("布局")]
        public float panelDistance = 3f;
        public float panelHeight = 1.2f;

        private SettingsData settings;

        [System.Serializable]
        public class SettingsData
        {
            public float masterVolume = 1f;
            public float musicVolume = 0.8f;
            public float sfxVolume = 1f;
            public float noteSpeed = 5f;
            public bool useAutoPlay = false;
            public float brightness = 1f;
            public bool showFPSCounter = false;
            public float turnSpeed = 45f;
            public bool smoothTurn = true;

            public const string PREFS_KEY = "ProjectEther_Settings";

            public void Save()
            {
                string json = JsonUtility.ToJson(this);
                PlayerPrefs.SetString(PREFS_KEY, json);
                PlayerPrefs.Save();
            }

            public static SettingsData Load()
            {
                if (PlayerPrefs.HasKey(PREFS_KEY))
                {
                    string json = PlayerPrefs.GetString(PREFS_KEY);
                    return JsonUtility.FromJson<SettingsData>(json);
                }
                return new SettingsData();
            }

            public void Reset()
            {
                masterVolume = 1f;
                musicVolume = 0.8f;
                sfxVolume = 1f;
                noteSpeed = 5f;
                useAutoPlay = false;
                brightness = 1f;
                showFPSCounter = false;
                turnSpeed = 45f;
                smoothTurn = true;
            }
        }

        void Start()
        {
            LoadSettings();
            SetupUI();
            SetupButtons();
            PositionPanel();
        }

        void LoadSettings()
        {
            settings = SettingsData.Load();
        }

        void SetupUI()
        {
            if (masterVolumeSlider != null)
            {
                masterVolumeSlider.value = settings.masterVolume;
                masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
            }

            if (musicVolumeSlider != null)
            {
                musicVolumeSlider.value = settings.musicVolume;
                musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
            }

            if (sfxVolumeSlider != null)
            {
                sfxVolumeSlider.value = settings.sfxVolume;
                sfxVolumeSlider.onValueChanged.AddListener(OnSfxVolumeChanged);
            }

            if (noteSpeedSlider != null)
            {
                noteSpeedSlider.value = settings.noteSpeed;
                noteSpeedSlider.onValueChanged.AddListener(OnNoteSpeedChanged);
            }

            if (brightnessSlider != null)
            {
                brightnessSlider.value = settings.brightness;
                brightnessSlider.onValueChanged.AddListener(OnBrightnessChanged);
            }

            if (turnSpeedSlider != null)
            {
                turnSpeedSlider.value = settings.turnSpeed;
                turnSpeedSlider.onValueChanged.AddListener(OnTurnSpeedChanged);
            }

            if (useAutoPlayToggle != null)
            {
                useAutoPlayToggle.isOn = settings.useAutoPlay;
                useAutoPlayToggle.onValueChanged.AddListener(OnAutoPlayChanged);
            }

            if (showFPSCounterToggle != null)
            {
                showFPSCounterToggle.isOn = settings.showFPSCounter;
                showFPSCounterToggle.onValueChanged.AddListener(OnShowFPSChanged);
            }

            if (smoothTurnToggle != null)
            {
                smoothTurnToggle.isOn = settings.smoothTurn;
                smoothTurnToggle.onValueChanged.AddListener(OnSmoothTurnChanged);
            }

            UpdateDisplayTexts();
        }

        void SetupButtons()
        {
            if (backButton != null)
            {
                backButton.onClick.AddListener(OnBackClicked);
            }

            if (resetButton != null)
            {
                resetButton.onClick.AddListener(OnResetClicked);
            }
        }

        void PositionPanel()
        {
            if (settingsPanel == null) return;

            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                Vector3 forward = mainCam.transform.forward;
                forward.y = 0;
                forward.Normalize();

                settingsPanel.position = mainCam.transform.position + forward * panelDistance + Vector3.up * panelHeight;
                settingsPanel.rotation = Quaternion.LookRotation(forward, Vector3.up);
            }
        }

        void UpdateDisplayTexts()
        {
            if (masterVolumeText != null)
                masterVolumeText.text = $"{(int)(settings.masterVolume * 100)}%";
            if (musicVolumeText != null)
                musicVolumeText.text = $"{(int)(settings.musicVolume * 100)}%";
            if (sfxVolumeText != null)
                sfxVolumeText.text = $"{(int)(settings.sfxVolume * 100)}%";
            if (noteSpeedText != null)
                noteSpeedText.text = $"{settings.noteSpeed:F1}";
            if (brightnessText != null)
                brightnessText.text = $"{(int)(settings.brightness * 100)}%";
            if (turnSpeedText != null)
                turnSpeedText.text = $"{(int)settings.turnSpeed}°/s";
        }

        void OnMasterVolumeChanged(float value)
        {
            settings.masterVolume = value;
            AudioListener.volume = value;
            UpdateDisplayTexts();
            settings.Save();
        }

        void OnMusicVolumeChanged(float value)
        {
            settings.musicVolume = value;
            UpdateDisplayTexts();
            settings.Save();
        }

        void OnSfxVolumeChanged(float value)
        {
            settings.sfxVolume = value;
            UpdateDisplayTexts();
            settings.Save();
        }

        void OnNoteSpeedChanged(float value)
        {
            settings.noteSpeed = value;
            UpdateDisplayTexts();
            settings.Save();
        }

        void OnBrightnessChanged(float value)
        {
            settings.brightness = value;
            UpdateDisplayTexts();
            settings.Save();
        }

        void OnTurnSpeedChanged(float value)
        {
            settings.turnSpeed = value;
            UpdateDisplayTexts();
            settings.Save();
        }

        void OnAutoPlayChanged(bool value)
        {
            settings.useAutoPlay = value;
            settings.Save();
        }

        void OnShowFPSChanged(bool value)
        {
            settings.showFPSCounter = value;
            settings.Save();
        }

        void OnSmoothTurnChanged(bool value)
        {
            settings.smoothTurn = value;
            settings.Save();
        }

        void OnBackClicked()
        {
            if (SceneFlowManager.Instance != null)
            {
                SceneFlowManager.Instance.GoToMainMenu();
            }
        }

        void OnResetClicked()
        {
            settings.Reset();
            SetupUI();
            settings.Save();
        }

        void Update()
        {
            UpdatePanelPosition();
        }

        void UpdatePanelPosition()
        {
            if (settingsPanel == null) return;

            Camera mainCam = Camera.main;
            if (mainCam == null) return;

            Vector3 targetPos = mainCam.transform.position + mainCam.transform.forward * panelDistance;
            targetPos.y = panelHeight;

            settingsPanel.position = Vector3.Lerp(settingsPanel.position, targetPos, Time.deltaTime * 3f);

            Vector3 lookDir = settingsPanel.position - mainCam.transform.position;
            lookDir.y = 0;
            if (lookDir.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(lookDir, Vector3.up);
                settingsPanel.rotation = Quaternion.Slerp(settingsPanel.rotation, targetRot, Time.deltaTime * 3f);
            }
        }

        public static SettingsData GetCurrentSettings()
        {
            return SettingsData.Load();
        }
    }
}
