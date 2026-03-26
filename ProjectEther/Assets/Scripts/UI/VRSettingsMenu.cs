using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

namespace OsuVR
{
    [RequireComponent(typeof(Canvas))]
    [RequireComponent(typeof(CanvasGroup))]
    public class VRSettingsMenu : MonoBehaviour
    {
        [Header("Tab System")]
        public Button[] tabButtons;
        public GameObject[] tabPanels;
        private int currentTabIndex = 0;

        [Header("Audio Settings UI")]
        public Slider audioOffsetSlider;
        public TextMeshProUGUI audioOffsetValueText;
        public Slider masterVolumeSlider;
        public TextMeshProUGUI masterVolumeValueText;
        public Slider musicVolumeSlider;
        public TextMeshProUGUI musicVolumeValueText;
        public Slider sfxVolumeSlider;
        public TextMeshProUGUI sfxVolumeValueText;

        [Header("Graphics Settings UI")]
        public TMP_Dropdown qualityDropdown;
        public TMP_Dropdown antiAliasingDropdown;
        public Slider particleDensitySlider;
        public TextMeshProUGUI particleDensityValueText;

        [Header("Game Settings UI")]
        public Toggle hapticsToggle;
        public Slider hapticIntensitySlider;
        public TextMeshProUGUI hapticIntensityValueText;
        public Toggle displayOriginalLanguageToggle;
        public TMP_Dropdown languageDropdown;

        [Header("Controller Offset UI")]
        public Slider leftControllerZOffsetSlider;
        public TextMeshProUGUI leftControllerZOffsetValueText;
        public Slider rightControllerZOffsetSlider;
        public TextMeshProUGUI rightControllerZOffsetValueText;
        public Slider leftControllerYOffsetSlider;
        public TextMeshProUGUI leftControllerYOffsetValueText;
        public Slider rightControllerYOffsetSlider;
        public TextMeshProUGUI rightControllerYOffsetValueText;
        public Slider controllerRotationOffsetSlider;
        public TextMeshProUGUI controllerRotationOffsetValueText;

        [Header("Buttons")]
        public Button backButton;
        public Button resetButton;

        [Header("Audio")]
        public AudioSource audioSource;
        public AudioClip hoverSound;
        public AudioClip clickSound;

        private Canvas canvas;
        private CanvasGroup canvasGroup;
        private GameSettings tempSettings;
        private bool isInitialized = false;

        void Awake()
        {
            canvas = GetComponent<Canvas>();
            canvasGroup = GetComponent<CanvasGroup>();
        }

        void Start()
        {
            EnsureWorldCamera();
            AutoAttachLocalizedTexts();
            InitializeMenu();
            LocalizationManager.ReloadAndNotify();
        }

        private void AutoAttachLocalizedTexts()
        {
            var allTexts = GetComponentsInChildren<TextMeshProUGUI>(true);
            var mapping = new System.Collections.Generic.Dictionary<string, string>
            {
                { "Master Volume", "ui_master_volume" },
                { "Music Volume", "ui_music_volume" },
                { "SFX Volume", "ui_sfx_volume" },
                { "Audio Offset", "ui_audio_offset" },
                { "Quality", "ui_quality" },
                { "Anti-Aliasing", "ui_anti_aliasing" },
                { "Particle Density", "ui_particle_density" },
                { "Enable Haptics", "ui_enable_haptics" },
                { "Haptic Intensity", "ui_haptic_intensity" },
                { "Display Song Names in Original Language", "ui_display_original_language" },
                { "Language", "ui_language" },
                { "Left Controller Z Offset", "ui_left_controller_z_offset" },
                { "Right Controller Z Offset", "ui_right_controller_z_offset" },
                { "Left Controller Y Offset", "ui_left_controller_y_offset" },
                { "Right Controller Y Offset", "ui_right_controller_y_offset" },
                { "Controller Rotation", "ui_controller_rotation_offset" },
                { "Reset", "ui_reset" },
                { "Back", "ui_back" },
                { "RESET", "ui_reset" },
                { "BACK", "ui_back" }
            };

            foreach (var text in allTexts)
            {
                if (mapping.TryGetValue(text.text, out string key))
                {
                    if (text.GetComponent<LocalizedText>() == null)
                    {
                        var lt = text.gameObject.AddComponent<LocalizedText>();
                        lt.localizationKey = key;
                    }
                }
            }
        }

        void OnEnable()
        {
            EnsureWorldCamera();
            LocalizationManager.OnLanguageChanged += OnLanguageChanged;
        }

        void OnDisable()
        {
            LocalizationManager.OnLanguageChanged -= OnLanguageChanged;
        }

        private void OnLanguageChanged()
        {
            if (languageDropdown != null)
            {
                languageDropdown.SetValueWithoutNotify(LocalizationManager.GetCurrentLanguageIndex());
            }
            RefreshAllLocalizedText();
        }

        private void RefreshAllLocalizedText()
        {
            RefreshQualityDropdownOptions();
            RefreshAntiAliasingDropdownOptions();
            RefreshTabTexts();
        }

        private void RefreshQualityDropdownOptions()
        {
            if (qualityDropdown == null) return;
            int currentValue = qualityDropdown.value;
            qualityDropdown.ClearOptions();
            qualityDropdown.AddOptions(new System.Collections.Generic.List<string> 
            { 
                LocalizationManager.GetText("ui_low"),
                LocalizationManager.GetText("ui_medium"),
                LocalizationManager.GetText("ui_high"),
                LocalizationManager.GetText("ui_ultra")
            });
            qualityDropdown.SetValueWithoutNotify(currentValue);
        }

        private void RefreshAntiAliasingDropdownOptions()
        {
            if (antiAliasingDropdown == null) return;
            int currentValue = antiAliasingDropdown.value;
            antiAliasingDropdown.ClearOptions();
            antiAliasingDropdown.AddOptions(new System.Collections.Generic.List<string> 
            { 
                LocalizationManager.GetText("ui_off"),
                "2x",
                "4x",
                "8x"
            });
            antiAliasingDropdown.SetValueWithoutNotify(currentValue);
        }

        private void RefreshTabTexts()
        {
            for (int i = 0; i < tabButtons.Length; i++)
            {
                if (tabButtons[i] == null) continue;
                var tmp = tabButtons[i].GetComponentInChildren<TextMeshProUGUI>();
                if (tmp == null) continue;
                
                string key = i switch
                {
                    0 => "ui_tab_game",
                    1 => "ui_tab_audio",
                    2 => "ui_tab_graphics",
                    3 => "ui_tab_controller",
                    _ => null
                };
                
                if (!string.IsNullOrEmpty(key))
                {
                    tmp.text = LocalizationManager.GetText(key);
                }
            }
        }

        private void EnsureWorldCamera()
        {
            if (canvas.worldCamera == null)
            {
                canvas.worldCamera = Camera.main;
            }
        }

        private void InitializeMenu()
        {
            if (SettingsManager.Instance == null)
            {
                Debug.LogError("[VRSettingsMenu] SettingsManager not found!");
                return;
            }

            tempSettings = SettingsManager.Instance.Settings.Clone();

            currentTabIndex = 0;
            for (int i = 0; i < tabPanels.Length; i++)
            {
                if (tabPanels[i] != null)
                {
                    tabPanels[i].SetActive(i == 0);
                }
            }

            SetupTabs();
            SetupAudioSettings();
            SetupGraphicsSettings();
            SetupGameSettings();
            SetupControllerOffsetSettings();
            SetupButtons();
            RefreshTabTexts();

            isInitialized = true;
            Debug.Log("[VRSettingsMenu] Menu initialized");
        }

        #region Tab System

        private void SetupTabs()
        {
            for (int i = 0; i < tabButtons.Length; i++)
            {
                int index = i;
                if (tabButtons[i] != null)
                {
                    tabButtons[i].onClick.AddListener(() => SwitchTab(index));
                    AddHoverEffect(tabButtons[i].gameObject);
                }
            }
            UpdateTabVisuals();
        }

        private void SwitchTab(int index)
        {
            PlayClickSound();
            currentTabIndex = index;
            
            for (int i = 0; i < tabPanels.Length; i++)
            {
                if (tabPanels[i] != null)
                {
                    tabPanels[i].SetActive(i == index);
                }
            }
            UpdateTabVisuals();
        }

        private void UpdateTabVisuals()
        {
            for (int i = 0; i < tabButtons.Length; i++)
            {
                if (tabButtons[i] != null)
                {
                    Image img = tabButtons[i].GetComponent<Image>();
                    if (img != null)
                    {
                        img.color = (i == currentTabIndex) 
                            ? new Color(0.2f, 0.35f, 0.55f, 1f) 
                            : new Color(0.15f, 0.15f, 0.22f, 1f);
                    }

                    Transform indicator = tabButtons[i].transform.Find("Indicator");
                    if (indicator != null)
                    {
                        Image indicatorImg = indicator.GetComponent<Image>();
                        if (indicatorImg != null)
                        {
                            indicatorImg.color = (i == currentTabIndex)
                                ? new Color(0.25f, 0.55f, 0.85f, 1f)
                                : new Color(0.25f, 0.55f, 0.85f, 0f);
                        }
                    }
                }
            }
        }

        #endregion

        #region Audio Settings Setup

        private void SetupAudioSettings()
        {
            if (audioOffsetSlider != null)
            {
                audioOffsetSlider.minValue = -200f;
                audioOffsetSlider.maxValue = 200f;
                audioOffsetSlider.value = tempSettings.audioOffsetMs;
                audioOffsetSlider.onValueChanged.AddListener(OnAudioOffsetChanged);
                UpdateAudioOffsetText(tempSettings.audioOffsetMs);
                AddHoverEffect(audioOffsetSlider.gameObject);
            }

            if (masterVolumeSlider != null)
            {
                masterVolumeSlider.minValue = 0f;
                masterVolumeSlider.maxValue = 1f;
                masterVolumeSlider.value = tempSettings.masterVolume;
                masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
                UpdateMasterVolumeText(tempSettings.masterVolume);
                AddHoverEffect(masterVolumeSlider.gameObject);
            }

            if (musicVolumeSlider != null)
            {
                musicVolumeSlider.minValue = 0f;
                musicVolumeSlider.maxValue = 1f;
                musicVolumeSlider.value = tempSettings.musicVolume;
                musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
                UpdateMusicVolumeText(tempSettings.musicVolume);
                AddHoverEffect(musicVolumeSlider.gameObject);
            }

            if (sfxVolumeSlider != null)
            {
                sfxVolumeSlider.minValue = 0f;
                sfxVolumeSlider.maxValue = 1f;
                sfxVolumeSlider.value = tempSettings.sfxVolume;
                sfxVolumeSlider.onValueChanged.AddListener(OnSFXVolumeChanged);
                UpdateSFXVolumeText(tempSettings.sfxVolume);
                AddHoverEffect(sfxVolumeSlider.gameObject);
            }
        }

        private void OnAudioOffsetChanged(float value)
        {
            tempSettings.audioOffsetMs = value;
            UpdateAudioOffsetText(value);
            if (SettingsManager.Instance != null)
            {
                SettingsManager.Instance.SetAudioOffset(value);
            }
            PlayHoverSound();
        }

        private void UpdateAudioOffsetText(float value)
        {
            if (audioOffsetValueText != null)
            {
                audioOffsetValueText.text = $"{value:F0} ms";
            }
        }

        private void OnMasterVolumeChanged(float value)
        {
            tempSettings.masterVolume = value;
            UpdateMasterVolumeText(value);
            if (SettingsManager.Instance != null)
            {
                SettingsManager.Instance.SetMasterVolume(value);
            }
            PlayHoverSound();
        }

        private void UpdateMasterVolumeText(float value)
        {
            if (masterVolumeValueText != null)
            {
                masterVolumeValueText.text = $"{Mathf.RoundToInt(value * 100)}%";
            }
        }

        private void OnMusicVolumeChanged(float value)
        {
            tempSettings.musicVolume = value;
            UpdateMusicVolumeText(value);
            if (SettingsManager.Instance != null)
            {
                SettingsManager.Instance.SetMusicVolume(value);
            }
            PlayHoverSound();
        }

        private void UpdateMusicVolumeText(float value)
        {
            if (musicVolumeValueText != null)
            {
                musicVolumeValueText.text = $"{Mathf.RoundToInt(value * 100)}%";
            }
        }

        private void OnSFXVolumeChanged(float value)
        {
            tempSettings.sfxVolume = value;
            UpdateSFXVolumeText(value);
            if (SettingsManager.Instance != null)
            {
                SettingsManager.Instance.SetSFXVolume(value);
            }
            PlayHoverSound();
        }

        private void UpdateSFXVolumeText(float value)
        {
            if (sfxVolumeValueText != null)
            {
                sfxVolumeValueText.text = $"{Mathf.RoundToInt(value * 100)}%";
            }
        }

        #endregion

        #region Graphics Settings Setup

        private void SetupGraphicsSettings()
        {
            if (qualityDropdown != null)
            {
                qualityDropdown.ClearOptions();
                qualityDropdown.AddOptions(new System.Collections.Generic.List<string> 
                { 
                    LocalizationManager.GetText("ui_low"),
                    LocalizationManager.GetText("ui_medium"),
                    LocalizationManager.GetText("ui_high"),
                    LocalizationManager.GetText("ui_ultra")
                });
                qualityDropdown.value = tempSettings.qualityLevel;
                qualityDropdown.onValueChanged.AddListener(OnQualityChanged);
                AddHoverEffect(qualityDropdown.gameObject);
            }

            if (antiAliasingDropdown != null)
            {
                antiAliasingDropdown.ClearOptions();
                antiAliasingDropdown.AddOptions(new System.Collections.Generic.List<string> 
                { 
                    LocalizationManager.GetText("ui_off"),
                    "2x",
                    "4x",
                    "8x"
                });
                int aaIndex = tempSettings.antiAliasing switch
                {
                    0 => 0,
                    2 => 1,
                    4 => 2,
                    8 => 3,
                    _ => 2
                };
                antiAliasingDropdown.value = aaIndex;
                antiAliasingDropdown.onValueChanged.AddListener(OnAntiAliasingChanged);
                AddHoverEffect(antiAliasingDropdown.gameObject);
            }

            if (particleDensitySlider != null)
            {
                particleDensitySlider.minValue = 0f;
                particleDensitySlider.maxValue = 1f;
                particleDensitySlider.value = tempSettings.particleDensity;
                particleDensitySlider.onValueChanged.AddListener(OnParticleDensityChanged);
                UpdateParticleDensityText(tempSettings.particleDensity);
                AddHoverEffect(particleDensitySlider.gameObject);
            }
        }

        private void OnQualityChanged(int index)
        {
            tempSettings.qualityLevel = index;
            if (SettingsManager.Instance != null)
            {
                SettingsManager.Instance.SetQualityLevel(index);
            }
            PlayClickSound();
        }

        private void OnAntiAliasingChanged(int index)
        {
            tempSettings.antiAliasing = index switch
            {
                0 => 0,
                1 => 2,
                2 => 4,
                3 => 8,
                _ => 4
            };
            if (SettingsManager.Instance != null)
            {
                SettingsManager.Instance.SetAntiAliasing(tempSettings.antiAliasing);
            }
            PlayClickSound();
        }

        private void OnParticleDensityChanged(float value)
        {
            tempSettings.particleDensity = value;
            UpdateParticleDensityText(value);
            if (SettingsManager.Instance != null)
            {
                SettingsManager.Instance.SetParticleDensity(value);
            }
            PlayHoverSound();
        }

        private void UpdateParticleDensityText(float value)
        {
            if (particleDensityValueText != null)
            {
                particleDensityValueText.text = $"{Mathf.RoundToInt(value * 100)}%";
            }
        }

        #endregion

        #region Game Settings Setup

        private void SetupGameSettings()
        {
            if (hapticsToggle != null)
            {
                hapticsToggle.isOn = tempSettings.enableHaptics;
                hapticsToggle.onValueChanged.AddListener(OnHapticsChanged);
                AddHoverEffect(hapticsToggle.gameObject);
            }

            if (hapticIntensitySlider != null)
            {
                hapticIntensitySlider.minValue = 0f;
                hapticIntensitySlider.maxValue = 1f;
                hapticIntensitySlider.value = tempSettings.hapticIntensity;
                hapticIntensitySlider.onValueChanged.AddListener(OnHapticIntensityChanged);
                UpdateHapticIntensityText(tempSettings.hapticIntensity);
                AddHoverEffect(hapticIntensitySlider.gameObject);
            }

            if (displayOriginalLanguageToggle != null)
            {
                displayOriginalLanguageToggle.isOn = tempSettings.displayOriginalLanguage;
                displayOriginalLanguageToggle.onValueChanged.AddListener(OnDisplayOriginalLanguageChanged);
                AddHoverEffect(displayOriginalLanguageToggle.gameObject);
            }

            if (languageDropdown != null)
            {
                languageDropdown.ClearOptions();
                languageDropdown.AddOptions(new System.Collections.Generic.List<string>(LocalizationManager.GetAllLanguageNames()));
                languageDropdown.value = LocalizationManager.GetCurrentLanguageIndex();
                languageDropdown.onValueChanged.AddListener(OnLanguageDropdownChanged);
                AddHoverEffect(languageDropdown.gameObject);
            }
        }

        private void OnLanguageDropdownChanged(int index)
        {
            LocalizationManager.SetLanguageByIndex(index);
            if (SettingsManager.Instance != null)
            {
                SettingsManager.Instance.SaveSettings();
            }
            PlayClickSound();
        }

        private void OnHapticsChanged(bool enabled)
        {
            tempSettings.enableHaptics = enabled;
            if (SettingsManager.Instance != null)
            {
                SettingsManager.Instance.Settings.enableHaptics = enabled;
                SettingsManager.Instance.SaveSettings();
            }
            PlayClickSound();
        }

        private void OnHapticIntensityChanged(float value)
        {
            tempSettings.hapticIntensity = value;
            UpdateHapticIntensityText(value);
            if (SettingsManager.Instance != null)
            {
                SettingsManager.Instance.Settings.hapticIntensity = value;
                SettingsManager.Instance.SaveSettings();
            }
            PlayHoverSound();
        }

        private void UpdateHapticIntensityText(float value)
        {
            if (hapticIntensityValueText != null)
            {
                hapticIntensityValueText.text = $"{Mathf.RoundToInt(value * 100)}%";
            }
        }

        private void OnDisplayOriginalLanguageChanged(bool enabled)
        {
            tempSettings.displayOriginalLanguage = enabled;
            if (SettingsManager.Instance != null)
            {
                SettingsManager.Instance.Settings.displayOriginalLanguage = enabled;
                SettingsManager.Instance.SaveSettings();
            }
            LocalizationManager.ForceUpdateLanguage();
            PlayClickSound();
        }

        #endregion

        #region Controller Offset Settings Setup

        private void SetupControllerOffsetSettings()
        {
            if (leftControllerZOffsetSlider != null)
            {
                leftControllerZOffsetSlider.minValue = -0.5f;
                leftControllerZOffsetSlider.maxValue = 0.5f;
                leftControllerZOffsetSlider.value = tempSettings.leftControllerZOffset;
                leftControllerZOffsetSlider.onValueChanged.AddListener(OnLeftControllerZOffsetChanged);
                UpdateLeftControllerZOffsetText(tempSettings.leftControllerZOffset);
                AddHoverEffect(leftControllerZOffsetSlider.gameObject);
            }

            if (rightControllerZOffsetSlider != null)
            {
                rightControllerZOffsetSlider.minValue = -0.5f;
                rightControllerZOffsetSlider.maxValue = 0.5f;
                rightControllerZOffsetSlider.value = tempSettings.rightControllerZOffset;
                rightControllerZOffsetSlider.onValueChanged.AddListener(OnRightControllerZOffsetChanged);
                UpdateRightControllerZOffsetText(tempSettings.rightControllerZOffset);
                AddHoverEffect(rightControllerZOffsetSlider.gameObject);
            }

            if (leftControllerYOffsetSlider != null)
            {
                leftControllerYOffsetSlider.minValue = -0.3f;
                leftControllerYOffsetSlider.maxValue = 0.3f;
                leftControllerYOffsetSlider.value = tempSettings.leftControllerYOffset;
                leftControllerYOffsetSlider.onValueChanged.AddListener(OnLeftControllerYOffsetChanged);
                UpdateLeftControllerYOffsetText(tempSettings.leftControllerYOffset);
                AddHoverEffect(leftControllerYOffsetSlider.gameObject);
            }

            if (rightControllerYOffsetSlider != null)
            {
                rightControllerYOffsetSlider.minValue = -0.3f;
                rightControllerYOffsetSlider.maxValue = 0.3f;
                rightControllerYOffsetSlider.value = tempSettings.rightControllerYOffset;
                rightControllerYOffsetSlider.onValueChanged.AddListener(OnRightControllerYOffsetChanged);
                UpdateRightControllerYOffsetText(tempSettings.rightControllerYOffset);
                AddHoverEffect(rightControllerYOffsetSlider.gameObject);
            }

            if (controllerRotationOffsetSlider != null)
            {
                controllerRotationOffsetSlider.minValue = -45f;
                controllerRotationOffsetSlider.maxValue = 45f;
                controllerRotationOffsetSlider.value = tempSettings.controllerRotationOffset;
                controllerRotationOffsetSlider.onValueChanged.AddListener(OnControllerRotationOffsetChanged);
                UpdateControllerRotationOffsetText(tempSettings.controllerRotationOffset);
                AddHoverEffect(controllerRotationOffsetSlider.gameObject);
            }
        }

        private void OnLeftControllerZOffsetChanged(float value)
        {
            tempSettings.leftControllerZOffset = value;
            UpdateLeftControllerZOffsetText(value);
            ApplyControllerOffsets();
            PlayHoverSound();
        }

        private void UpdateLeftControllerZOffsetText(float value)
        {
            if (leftControllerZOffsetValueText != null)
            {
                leftControllerZOffsetValueText.text = $"{value:F2} m";
            }
        }

        private void OnRightControllerZOffsetChanged(float value)
        {
            tempSettings.rightControllerZOffset = value;
            UpdateRightControllerZOffsetText(value);
            ApplyControllerOffsets();
            PlayHoverSound();
        }

        private void UpdateRightControllerZOffsetText(float value)
        {
            if (rightControllerZOffsetValueText != null)
            {
                rightControllerZOffsetValueText.text = $"{value:F2} m";
            }
        }

        private void OnLeftControllerYOffsetChanged(float value)
        {
            tempSettings.leftControllerYOffset = value;
            UpdateLeftControllerYOffsetText(value);
            ApplyControllerOffsets();
            PlayHoverSound();
        }

        private void UpdateLeftControllerYOffsetText(float value)
        {
            if (leftControllerYOffsetValueText != null)
            {
                leftControllerYOffsetValueText.text = $"{value:F2} m";
            }
        }

        private void OnRightControllerYOffsetChanged(float value)
        {
            tempSettings.rightControllerYOffset = value;
            UpdateRightControllerYOffsetText(value);
            ApplyControllerOffsets();
            PlayHoverSound();
        }

        private void UpdateRightControllerYOffsetText(float value)
        {
            if (rightControllerYOffsetValueText != null)
            {
                rightControllerYOffsetValueText.text = $"{value:F2} m";
            }
        }

        private void OnControllerRotationOffsetChanged(float value)
        {
            tempSettings.controllerRotationOffset = value;
            UpdateControllerRotationOffsetText(value);
            ApplyControllerOffsets();
            PlayHoverSound();
        }

        private void UpdateControllerRotationOffsetText(float value)
        {
            if (controllerRotationOffsetValueText != null)
            {
                controllerRotationOffsetValueText.text = $"{value:F0}°";
            }
        }

        private void ApplyControllerOffsets()
        {
            if (SettingsManager.Instance != null)
            {
                SettingsManager.Instance.Settings.leftControllerZOffset = tempSettings.leftControllerZOffset;
                SettingsManager.Instance.Settings.rightControllerZOffset = tempSettings.rightControllerZOffset;
                SettingsManager.Instance.Settings.leftControllerYOffset = tempSettings.leftControllerYOffset;
                SettingsManager.Instance.Settings.rightControllerYOffset = tempSettings.rightControllerYOffset;
                SettingsManager.Instance.Settings.controllerRotationOffset = tempSettings.controllerRotationOffset;
                SettingsManager.Instance.SaveSettings();
            }

            var rayControllers = FindObjectsOfType<RayController>();
            foreach (var rc in rayControllers)
            {
                if (rc.isRightHand)
                {
                    rc.directOffset = new Vector3(tempSettings.controllerRotationOffset, 0, 0);
                }
                else
                {
                    rc.directOffset = new Vector3(tempSettings.controllerRotationOffset, 0, 0);
                }
            }
        }

        #endregion

        #region Buttons Setup

        private void SetupButtons()
        {
            if (backButton != null)
            {
                backButton.onClick.AddListener(OnBackClicked);
                AddHoverEffect(backButton.gameObject);
            }

            if (resetButton != null)
            {
                resetButton.onClick.AddListener(OnResetClicked);
                AddHoverEffect(resetButton.gameObject);
            }
        }

        private void OnBackClicked()
        {
            PlayClickSound();
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenuScene");
        }

        private void OnResetClicked()
        {
            PlayClickSound();
            tempSettings.ResetToDefaults();
            RefreshUIFromSettings();
            if (SettingsManager.Instance != null)
            {
                SettingsManager.Instance.ResetToDefaults();
            }
        }

        private void RefreshUIFromSettings()
        {
            if (audioOffsetSlider != null) audioOffsetSlider.value = tempSettings.audioOffsetMs;
            if (masterVolumeSlider != null) masterVolumeSlider.value = tempSettings.masterVolume;
            if (musicVolumeSlider != null) musicVolumeSlider.value = tempSettings.musicVolume;
            if (sfxVolumeSlider != null) sfxVolumeSlider.value = tempSettings.sfxVolume;
            if (qualityDropdown != null) qualityDropdown.value = tempSettings.qualityLevel;
            if (hapticsToggle != null) hapticsToggle.isOn = tempSettings.enableHaptics;
            if (hapticIntensitySlider != null) hapticIntensitySlider.value = tempSettings.hapticIntensity;
            if (displayOriginalLanguageToggle != null) displayOriginalLanguageToggle.isOn = tempSettings.displayOriginalLanguage;
            if (particleDensitySlider != null) particleDensitySlider.value = tempSettings.particleDensity;
            if (leftControllerZOffsetSlider != null) leftControllerZOffsetSlider.value = tempSettings.leftControllerZOffset;
            if (rightControllerZOffsetSlider != null) rightControllerZOffsetSlider.value = tempSettings.rightControllerZOffset;
            if (leftControllerYOffsetSlider != null) leftControllerYOffsetSlider.value = tempSettings.leftControllerYOffset;
            if (rightControllerYOffsetSlider != null) rightControllerYOffsetSlider.value = tempSettings.rightControllerYOffset;
            if (controllerRotationOffsetSlider != null) controllerRotationOffsetSlider.value = tempSettings.controllerRotationOffset;
        }

        #endregion

        #region Audio Feedback

        private void AddHoverEffect(GameObject obj)
        {
            var trigger = obj.GetComponent<EventTrigger>();
            if (trigger == null)
            {
                trigger = obj.AddComponent<EventTrigger>();
            }

            var enterEntry = new EventTrigger.Entry
            {
                eventID = EventTriggerType.PointerEnter
            };
            enterEntry.callback.AddListener((_) => PlayHoverSound());
            trigger.triggers.Add(enterEntry);
        }

        private void PlayHoverSound()
        {
            if (audioSource != null && hoverSound != null)
            {
                audioSource.PlayOneShot(hoverSound, 0.5f);
            }
        }

        private void PlayClickSound()
        {
            if (audioSource != null && clickSound != null)
            {
                audioSource.PlayOneShot(clickSound, 0.8f);
            }
        }

        #endregion

        #region Public Methods

        public void Show()
        {
            gameObject.SetActive(true);
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;

            if (!isInitialized)
            {
                InitializeMenu();
            }
            else
            {
                tempSettings = SettingsManager.Instance?.Settings.Clone();
                RefreshUIFromSettings();
            }
        }

        public void Hide()
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        #endregion
    }
}
