#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using OsuVR;

namespace OsuVR.Editor
{
    public class VRSettingsMenuCreator : EditorWindow
    {
        [MenuItem("Project Ether/工具/创建VR设置菜单Prefab")]
        public static void ShowWindow()
        {
            GetWindow<VRSettingsMenuCreator>("VR设置菜单创建器");
        }

        private string prefabName = "VRSettingsMenu";
        private string savePath = "Assets/Prefabs/UI";
        private bool createScene = true;
        private string scenePath = "Assets/Scenes";

        void OnGUI()
        {
            GUILayout.Label("VR设置菜单创建器", EditorStyles.boldLabel);
            GUILayout.Space(10);

            prefabName = EditorGUILayout.TextField("Prefab名称", prefabName); 
            savePath = EditorGUILayout.TextField("Prefab保存路径", savePath);
            GUILayout.Space(5);
            createScene = EditorGUILayout.Toggle("创建场景", createScene);
            if (createScene)
            {
                scenePath = EditorGUILayout.TextField("场景保存路径", scenePath);
            }

            GUILayout.Space(20);

            if (GUILayout.Button("创建VR设置菜单", GUILayout.Height(40)))
            {
                CreateVRSettingsMenu();
            }
        }

        private void CreateVRSettingsMenu()
        {
            GameObject root = new GameObject(prefabName);

            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 10;

            var graphicRaycaster = root.AddComponent<GraphicRaycaster>();
            graphicRaycaster.ignoreReversedGraphics = false;
            graphicRaycaster.blockingObjects = GraphicRaycaster.BlockingObjects.None;

            CanvasGroup canvasGroup = root.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;

            root.AddComponent<VRSettingsMenu>();

            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(600, 450);
            rootRect.localScale = new Vector3(0.0025f, 0.0025f, 0.0025f);

            CreateUIStructure(root);

            if (!AssetDatabase.IsValidFolder(savePath))
            {
                System.IO.Directory.CreateDirectory(savePath);
            }

            string prefabFullPath = System.IO.Path.Combine(savePath, prefabName + ".prefab");
            PrefabUtility.SaveAsPrefabAsset(root, prefabFullPath);
            Debug.Log($"[VRSettingsMenu] Prefab已保存到: {prefabFullPath}");

            if (createScene)
            {
                CreateSettingsScene(root);
            }
            else
            {
                DestroyImmediate(root);
            }

            AssetDatabase.Refresh();
        }

        private void CreateUIStructure(GameObject root)
        {
            VRSettingsMenu settingsMenu = root.GetComponent<VRSettingsMenu>();

            GameObject mainContainer = CreateContainer("SettingsContainer", root.transform);
            RectTransform containerRect = mainContainer.GetComponent<RectTransform>();
            containerRect.anchorMin = Vector2.zero;
            containerRect.anchorMax = Vector2.one;
            containerRect.sizeDelta = Vector2.zero;
            containerRect.anchoredPosition = Vector2.zero;

            Image bgImage = mainContainer.AddComponent<Image>();
            bgImage.color = new Color(0.05f, 0.05f, 0.08f, 0.85f);

            CreateTabBar(mainContainer.transform, settingsMenu);

            GameObject contentArea = CreateContainer("ContentArea", mainContainer.transform);
            RectTransform contentRect = contentArea.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 0);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.offsetMin = new Vector2(25, 75);
            contentRect.offsetMax = new Vector2(-25, -55);

            CreateAudioPanel(contentArea.transform, settingsMenu);
            CreateGraphicsPanel(contentArea.transform, settingsMenu);
            CreateGamePanel(contentArea.transform, settingsMenu);
            CreateControllerOffsetPanel(contentArea.transform, settingsMenu);

            CreateBottomButtons(mainContainer.transform, settingsMenu);

            AudioSource audioSource = root.AddComponent<AudioSource>();
            settingsMenu.audioSource = audioSource;
        }

        private void CreateTabBar(Transform parent, VRSettingsMenu settingsMenu)
        {
            GameObject tabBar = new GameObject("TabBar");
            tabBar.transform.SetParent(parent, false);

            RectTransform tabRect = tabBar.AddComponent<RectTransform>();
            tabRect.anchorMin = new Vector2(0, 1);
            tabRect.anchorMax = new Vector2(1, 1);
            tabRect.pivot = new Vector2(0.5f, 1);
            tabRect.sizeDelta = new Vector2(0, 50);
            tabRect.anchoredPosition = Vector2.zero;

            HorizontalLayoutGroup layout = tabBar.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8;
            layout.padding = new RectOffset(25, 25, 8, 8);
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childAlignment = TextAnchor.MiddleLeft;

            settingsMenu.tabButtons = new Button[4];
            settingsMenu.tabPanels = new GameObject[4];

            string[] tabNames = { "Audio", "Graphics", "Game", "Controller" };
            string[] tabKeys = { "ui_tab_audio", "ui_tab_graphics", "ui_tab_game", "ui_tab_controller" };
            for (int i = 0; i < 4; i++)
            {
                GameObject tabBtn = CreateTabButton($"Tab_{tabNames[i]}", tabBar.transform, tabNames[i], tabKeys[i], out Button btn);
                settingsMenu.tabButtons[i] = btn;
            }
        }

        private GameObject CreateTabButton(string name, Transform parent, string text, string localizationKey, out Button button)
        {
            GameObject btn = new GameObject(name);
            btn.transform.SetParent(parent, false);

            RectTransform rect = btn.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(90, 34);

            Image image = btn.AddComponent<Image>();
            image.color = new Color(0.12f, 0.12f, 0.18f, 0.7f);

            button = btn.AddComponent<Button>();
            button.targetGraphic = image;

            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.12f, 0.12f, 0.18f, 0.7f);
            colors.highlightedColor = new Color(0.2f, 0.25f, 0.35f, 0.85f);
            colors.selectedColor = new Color(0.15f, 0.3f, 0.5f, 0.9f);
            button.colors = colors;

            GameObject textObj = CreateText("Text", btn.transform, text, Vector2.zero, Vector2.zero, 15, TextAlignmentOptions.Center);
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            
            if (!string.IsNullOrEmpty(localizationKey))
            {
                AddLocalizedText(textObj, localizationKey);
            }

            GameObject indicator = new GameObject("Indicator");
            indicator.transform.SetParent(btn.transform, false);
            Image indicatorImage = indicator.AddComponent<Image>();
            indicatorImage.color = new Color(0.25f, 0.55f, 0.85f, 0f);
            RectTransform indicatorRect = indicator.GetComponent<RectTransform>();
            indicatorRect.anchorMin = new Vector2(0, 0);
            indicatorRect.anchorMax = new Vector2(1, 0);
            indicatorRect.pivot = new Vector2(0.5f, 0);
            indicatorRect.sizeDelta = new Vector2(0, 3);
            indicatorRect.anchoredPosition = Vector2.zero;

            return btn;
        }

        private void CreateAudioPanel(Transform parent, VRSettingsMenu settingsMenu)
        {
            GameObject panel = CreatePanel("AudioPanel", parent, 0);
            settingsMenu.tabPanels[0] = panel;

            CreateSliderRow(panel.transform, "Master Volume", "ui_master_volume", 0, 1, 1, "percent", out Slider masterVolSlider, out TextMeshProUGUI masterVolText);
            settingsMenu.masterVolumeSlider = masterVolSlider;
            settingsMenu.masterVolumeValueText = masterVolText;

            CreateSliderRow(panel.transform, "Music Volume", "ui_music_volume", 0, 1, 0.8f, "percent", out Slider musicVolSlider, out TextMeshProUGUI musicVolText);
            settingsMenu.musicVolumeSlider = musicVolSlider;
            settingsMenu.musicVolumeValueText = musicVolText;

            CreateSliderRow(panel.transform, "SFX Volume", "ui_sfx_volume", 0, 1, 1, "percent", out Slider sfxVolSlider, out TextMeshProUGUI sfxVolText);
            settingsMenu.sfxVolumeSlider = sfxVolSlider;
            settingsMenu.sfxVolumeValueText = sfxVolText;

            CreateFullWidthSliderRow(panel.transform, "Audio Offset", "ui_audio_offset", -200, 200, 0, "ms", out Slider audioOffsetSlider, out TextMeshProUGUI audioOffsetText);
            settingsMenu.audioOffsetSlider = audioOffsetSlider;
            settingsMenu.audioOffsetValueText = audioOffsetText;
        }

        private void CreateGraphicsPanel(Transform parent, VRSettingsMenu settingsMenu)
        {
            GameObject panel = CreatePanel("GraphicsPanel", parent, 1);
            settingsMenu.tabPanels[1] = panel;

            CreateDropdownRow(panel.transform, "Quality", "ui_quality", new[] { "Low", "Medium", "High", "Ultra" }, new[] { "ui_low", "ui_medium", "ui_high", "ui_ultra" }, 2, out TMP_Dropdown qualityDropdown);
            settingsMenu.qualityDropdown = qualityDropdown;

            CreateDropdownRow(panel.transform, "Anti-Aliasing", "ui_anti_aliasing", new[] { "Off", "2x", "4x", "8x" }, null, 2, out TMP_Dropdown aaDropdown);
            settingsMenu.antiAliasingDropdown = aaDropdown;

            CreateSliderRow(panel.transform, "Particle Density", "ui_particle_density", 0, 1, 1, "percent", out Slider particleSlider, out TextMeshProUGUI particleText);
            settingsMenu.particleDensitySlider = particleSlider;
            settingsMenu.particleDensityValueText = particleText;
        }

        private void CreateGamePanel(Transform parent, VRSettingsMenu settingsMenu)
        {
            GameObject panel = CreatePanel("GamePanel", parent, 2);
            settingsMenu.tabPanels[2] = panel;

            CreateLanguageRow(panel.transform, out TMP_Dropdown languageDropdown);
            settingsMenu.languageDropdown = languageDropdown;

            CreateToggleRow(panel.transform, "Enable Haptics", "ui_enable_haptics", true, out Toggle hapticsToggle);
            settingsMenu.hapticsToggle = hapticsToggle;

            CreateSliderRow(panel.transform, "Haptic Intensity", "ui_haptic_intensity", 0, 1, 0.8f, "percent", out Slider hapticIntSlider, out TextMeshProUGUI hapticIntText);
            settingsMenu.hapticIntensitySlider = hapticIntSlider;
            settingsMenu.hapticIntensityValueText = hapticIntText;

            CreateToggleRow(panel.transform, "Display Song Names in Original Language", "ui_display_original_language", false, out Toggle originalLangToggle);
            settingsMenu.displayOriginalLanguageToggle = originalLangToggle;
        }

        private void CreateLanguageRow(Transform parent, out TMP_Dropdown dropdown)
        {
            GameObject row = new GameObject("LanguageRow");
            row.transform.SetParent(parent, false);

            RectTransform rect = row.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 0.5f);
            rect.anchorMax = new Vector2(1, 0.5f);
            rect.sizeDelta = new Vector2(0, 36);
            rect.anchoredPosition = Vector2.zero;

            HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childAlignment = TextAnchor.MiddleLeft;

            GameObject labelObj = CreateText("Label", row.transform, "Language", Vector2.zero, new Vector2(160, 36), 14, TextAlignmentOptions.Left);
            LayoutElement labelLayout = labelObj.AddComponent<LayoutElement>();
            labelLayout.preferredWidth = 160;
            labelLayout.flexibleWidth = 0;
            AddLocalizedText(labelObj, "ui_language");

            string[] languageOptions = { "English", "简体中文", "日本語" };
            int currentLangIndex = LocalizationManager.GetCurrentLanguageIndex();
            GameObject dropdownObj = CreateLanguageDropdown("LanguageDropdown", row.transform, languageOptions, currentLangIndex, out dropdown);
            LayoutElement dropdownLayout = dropdownObj.AddComponent<LayoutElement>();
            dropdownLayout.flexibleWidth = 1;
            dropdownLayout.preferredWidth = 180;
        }

        private GameObject CreateLanguageDropdown(string name, Transform parent, string[] options, int defaultValue, out TMP_Dropdown dropdown)
        {
            GameObject dropdownObj = CreateDropdown(name, parent, options, defaultValue, out dropdown);
            
            dropdown.onValueChanged.AddListener((index) =>
            {
                LocalizationManager.SetLanguageByIndex(index);
            });

            return dropdownObj;
        }

        private void AddLocalizedText(GameObject textObj, string key)
        {
            if (textObj == null || string.IsNullOrEmpty(key)) return;
            
            var localizedText = textObj.AddComponent<LocalizedText>();
            SerializedObject so = new SerializedObject(localizedText);
            so.FindProperty("localizationKey").stringValue = key;
            so.ApplyModifiedProperties();
        }

        private void CreateControllerOffsetPanel(Transform parent, VRSettingsMenu settingsMenu)
        {
            GameObject panel = CreatePanel("ControllerOffsetPanel", parent, 3);
            settingsMenu.tabPanels[3] = panel;

            CreateSliderRow(panel.transform, "Left Controller Z Offset", null, -0.5f, 0.5f, 0, "m", out Slider leftZSlider, out TextMeshProUGUI leftZText);
            settingsMenu.leftControllerZOffsetSlider = leftZSlider;
            settingsMenu.leftControllerZOffsetValueText = leftZText;

            CreateSliderRow(panel.transform, "Right Controller Z Offset", null, -0.5f, 0.5f, 0, "m", out Slider rightZSlider, out TextMeshProUGUI rightZText);
            settingsMenu.rightControllerZOffsetSlider = rightZSlider;
            settingsMenu.rightControllerZOffsetValueText = rightZText;

            CreateSliderRow(panel.transform, "Left Controller Y Offset", null, -0.3f, 0.3f, 0, "m", out Slider leftYSlider, out TextMeshProUGUI leftYText);
            settingsMenu.leftControllerYOffsetSlider = leftYSlider;
            settingsMenu.leftControllerYOffsetValueText = leftYText;

            CreateSliderRow(panel.transform, "Right Controller Y Offset", null, -0.3f, 0.3f, 0, "m", out Slider rightYSlider, out TextMeshProUGUI rightYText);
            settingsMenu.rightControllerYOffsetSlider = rightYSlider;
            settingsMenu.rightControllerYOffsetValueText = rightYText;

            CreateSliderRow(panel.transform, "Controller Rotation", null, -45, 45, 0, "deg", out Slider rotSlider, out TextMeshProUGUI rotText);
            settingsMenu.controllerRotationOffsetSlider = rotSlider;
            settingsMenu.controllerRotationOffsetValueText = rotText;
        }

        private GameObject CreatePanel(string name, Transform parent, int tabIndex)
        {
            GameObject panel = new GameObject(name);
            panel.transform.SetParent(parent, false);

            RectTransform rect = panel.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;

            Image bg = panel.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.08f, 0.12f, 0.6f);

            VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(15, 15, 10, 10);
            layout.spacing = 6;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childAlignment = TextAnchor.UpperCenter;

            if (tabIndex != 0)
            {
                panel.SetActive(false);
            }

            return panel;
        }

        private void CreateSliderRow(Transform parent, string label, string localizationKey, float min, float max, float defaultValue, string format, out Slider slider, out TextMeshProUGUI valueText)
        {
            GameObject row = new GameObject(label + "Row");
            row.transform.SetParent(parent, false);

            RectTransform rect = row.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 0.5f);
            rect.anchorMax = new Vector2(1, 0.5f);
            rect.sizeDelta = new Vector2(0, 36);
            rect.anchoredPosition = Vector2.zero;

            HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childAlignment = TextAnchor.MiddleLeft;

            GameObject labelObj = CreateText("Label", row.transform, label, Vector2.zero, new Vector2(160, 36), 14, TextAlignmentOptions.Left);
            LayoutElement labelLayout = labelObj.AddComponent<LayoutElement>();
            labelLayout.preferredWidth = 160;
            labelLayout.flexibleWidth = 0;
            
            if (!string.IsNullOrEmpty(localizationKey))
            {
                AddLocalizedText(labelObj, localizationKey);
            }

            GameObject sliderObj = CreateSlider("Slider", row.transform, min, max, defaultValue, out slider);
            LayoutElement sliderLayout = sliderObj.AddComponent<LayoutElement>();
            sliderLayout.flexibleWidth = 1;
            sliderLayout.preferredWidth = 280;

            string displayValue = FormatValue(defaultValue, format);
            GameObject valueObj = CreateText("Value", row.transform, displayValue, Vector2.zero, new Vector2(70, 36), 15, TextAlignmentOptions.Right);
            valueText = valueObj.GetComponent<TextMeshProUGUI>();
            LayoutElement valueLayout = valueObj.AddComponent<LayoutElement>();
            valueLayout.preferredWidth = 70;
            valueLayout.flexibleWidth = 0;
        }

        private string FormatValue(float value, string format)
        {
            switch (format)
            {
                case "percent":
                    return Mathf.RoundToInt(value * 100) + "%";
                case "ms":
                    return Mathf.RoundToInt(value) + " ms";
                case "m":
                    return value.ToString("F2") + " m";
                case "deg":
                    return Mathf.RoundToInt(value) + "°";
                default:
                    return Mathf.RoundToInt(value).ToString();
            }
        }

        private void CreateFullWidthSliderRow(Transform parent, string label, string localizationKey, float min, float max, float defaultValue, string format, out Slider slider, out TextMeshProUGUI valueText)
        {
            GameObject row = new GameObject(label + "Row");
            row.transform.SetParent(parent, false);

            RectTransform rect = row.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 0.5f);
            rect.anchorMax = new Vector2(1, 0.5f);
            rect.sizeDelta = new Vector2(0, 95);
            rect.anchoredPosition = Vector2.zero;

            VerticalLayoutGroup layout = row.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 5;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.padding = new RectOffset(0, 0, 0, 0);

            GameObject headerRow = new GameObject("Header");
            headerRow.transform.SetParent(row.transform, false);
            RectTransform headerRect = headerRow.AddComponent<RectTransform>();
            headerRect.sizeDelta = new Vector2(0, 24);

            HorizontalLayoutGroup headerLayout = headerRow.AddComponent<HorizontalLayoutGroup>();
            headerLayout.spacing = 10;
            headerLayout.childControlWidth = true;
            headerLayout.childControlHeight = false;
            headerLayout.childAlignment = TextAnchor.MiddleLeft;

            GameObject labelObj = CreateBoldText("Label", headerRow.transform, label, Vector2.zero, new Vector2(0, 24), 15, TextAlignmentOptions.Left);
            LayoutElement labelLayout = labelObj.AddComponent<LayoutElement>();
            labelLayout.flexibleWidth = 1;
            
            if (!string.IsNullOrEmpty(localizationKey))
            {
                AddLocalizedText(labelObj, localizationKey);
            }

            string displayValue = FormatValue(defaultValue, format);
            GameObject valueObj = CreateBoldText("Value", headerRow.transform, displayValue, Vector2.zero, new Vector2(100, 24), 16, TextAlignmentOptions.Right);
            valueText = valueObj.GetComponent<TextMeshProUGUI>();
            LayoutElement valueLayout = valueObj.AddComponent<LayoutElement>();
            valueLayout.preferredWidth = 100;
            valueLayout.flexibleWidth = 0;

            GameObject sliderObj = CreateFullWidthSlider("Slider", row.transform, min, max, defaultValue, out slider);
            LayoutElement sliderLayout = sliderObj.AddComponent<LayoutElement>();
            sliderLayout.flexibleWidth = 1;
            sliderLayout.preferredHeight = 32;

            GameObject fineTuneRow = new GameObject("FineTune");
            fineTuneRow.transform.SetParent(row.transform, false);
            RectTransform fineTuneRect = fineTuneRow.AddComponent<RectTransform>();
            fineTuneRect.sizeDelta = new Vector2(0, 28);

            HorizontalLayoutGroup fineTuneLayout = fineTuneRow.AddComponent<HorizontalLayoutGroup>();
            fineTuneLayout.spacing = 6;
            fineTuneLayout.childControlWidth = false;
            fineTuneLayout.childControlHeight = false;
            fineTuneLayout.childAlignment = TextAnchor.MiddleCenter;

            GameObject spacerLeft = new GameObject("SpacerLeft");
            spacerLeft.transform.SetParent(fineTuneRow.transform, false);
            LayoutElement spacerLeftLayout = spacerLeft.AddComponent<LayoutElement>();
            spacerLeftLayout.flexibleWidth = 1;

            CreateFineTuneButton("DecreaseBtn", fineTuneRow.transform, "-10");
            CreateFineTuneButton("DecreaseBtn5", fineTuneRow.transform, "-5");
            CreateFineTuneButton("DecreaseBtn1", fineTuneRow.transform, "-1");
            CreateFineTuneButton("IncreaseBtn1", fineTuneRow.transform, "+1");
            CreateFineTuneButton("IncreaseBtn5", fineTuneRow.transform, "+5");
            CreateFineTuneButton("IncreaseBtn", fineTuneRow.transform, "+10");

            GameObject spacerRight = new GameObject("SpacerRight");
            spacerRight.transform.SetParent(fineTuneRow.transform, false);
            LayoutElement spacerRightLayout = spacerRight.AddComponent<LayoutElement>();
            spacerRightLayout.flexibleWidth = 1;

            FineTuneSlider fineTuneComponent = row.AddComponent<FineTuneSlider>();
            fineTuneComponent.Initialize(slider, valueText, min, max, format);
        }

        private GameObject CreateFineTuneButton(string name, Transform parent, string text)
        {
            GameObject btn = new GameObject(name);
            btn.transform.SetParent(parent, false);

            RectTransform rect = btn.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(42, 24);

            Image image = btn.AddComponent<Image>();
            image.color = new Color(0.15f, 0.18f, 0.25f, 0.7f);

            Button button = btn.AddComponent<Button>();
            button.targetGraphic = image;

            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.15f, 0.18f, 0.25f, 0.7f);
            colors.highlightedColor = new Color(0.25f, 0.35f, 0.5f, 0.9f);
            colors.pressedColor = new Color(0.2f, 0.25f, 0.35f, 0.8f);
            colors.selectedColor = new Color(0.2f, 0.3f, 0.45f, 0.85f);
            button.colors = colors;

            GameObject textObj = CreateText("Text", btn.transform, text, Vector2.zero, Vector2.zero, 12, TextAlignmentOptions.Center);
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            return btn;
        }

        private GameObject CreateBoldText(string name, Transform parent, string text, Vector2 anchorPos, Vector2 size, int fontSize, TextAlignmentOptions alignment)
        {
            GameObject textObj = new GameObject(name);
            textObj.transform.SetParent(parent, false);

            RectTransform rect = textObj.AddComponent<RectTransform>();
            rect.anchoredPosition = anchorPos;
            rect.sizeDelta = size;

            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = alignment;
            tmp.color = Color.white;
            tmp.fontStyle = FontStyles.Bold;

            return textObj;
        }

        private GameObject CreateFullWidthSlider(string name, Transform parent, float min, float max, float defaultValue, out Slider slider)
        {
            GameObject sliderObj = new GameObject(name);
            sliderObj.transform.SetParent(parent, false);

            RectTransform sliderRect = sliderObj.AddComponent<RectTransform>();
            sliderRect.sizeDelta = new Vector2(0, 36);

            Image sliderBg = sliderObj.AddComponent<Image>();
            sliderBg.color = new Color(0.1f, 0.1f, 0.15f, 0.5f);

            slider = sliderObj.AddComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = defaultValue;

            GameObject bgObj = new GameObject("Background");
            bgObj.transform.SetParent(sliderObj.transform, false);
            Image bgImage = bgObj.AddComponent<Image>();
            bgImage.color = new Color(0.1f, 0.1f, 0.15f, 0.6f);
            RectTransform bgRect = bgObj.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0, 0.25f);
            bgRect.anchorMax = new Vector2(1, 0.75f);
            bgRect.sizeDelta = Vector2.zero;

            CreateSliderTicks(bgObj.transform, min, max);

            GameObject fillAreaObj = new GameObject("Fill Area");
            fillAreaObj.transform.SetParent(sliderObj.transform, false);
            RectTransform fillAreaRect = fillAreaObj.AddComponent<RectTransform>();
            fillAreaRect.anchorMin = new Vector2(0, 0.25f);
            fillAreaRect.anchorMax = new Vector2(1, 0.75f);
            fillAreaRect.offsetMin = new Vector2(2, 0);
            fillAreaRect.offsetMax = new Vector2(-2, 0);

            GameObject fillObj = new GameObject("Fill");
            fillObj.transform.SetParent(fillAreaObj.transform, false);
            Image fillImage = fillObj.AddComponent<Image>();
            fillImage.color = new Color(0.25f, 0.45f, 0.75f, 0.9f);
            RectTransform fillRect = fillObj.GetComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0, 0);
            fillRect.anchorMax = new Vector2(0, 1);
            fillRect.sizeDelta = Vector2.zero;

            GameObject handleAreaObj = new GameObject("Handle Slide Area");
            handleAreaObj.transform.SetParent(sliderObj.transform, false);
            RectTransform handleAreaRect = handleAreaObj.AddComponent<RectTransform>();
            handleAreaRect.anchorMin = Vector2.zero;
            handleAreaRect.anchorMax = Vector2.one;
            handleAreaRect.offsetMin = new Vector2(15, 0);
            handleAreaRect.offsetMax = new Vector2(-15, 0);

            GameObject handleObj = new GameObject("Handle");
            handleObj.transform.SetParent(handleAreaObj.transform, false);
            Image handleImage = handleObj.AddComponent<Image>();
            handleImage.color = new Color(0.85f, 0.88f, 0.95f, 1f);
            RectTransform handleRect = handleObj.GetComponent<RectTransform>();
            handleRect.anchorMin = new Vector2(0.5f, 0f);
            handleRect.anchorMax = new Vector2(0.5f, 1f);
            handleRect.pivot = new Vector2(0.5f, 0.5f);
            handleRect.sizeDelta = new Vector2(30, 0);
            handleRect.anchoredPosition = Vector2.zero;

            slider.targetGraphic = handleImage;
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;

            return sliderObj;
        }

        private GameObject CreateSlider(string name, Transform parent, float min, float max, float defaultValue, out Slider slider)
        {
            GameObject sliderObj = new GameObject(name);
            sliderObj.transform.SetParent(parent, false);

            RectTransform sliderRect = sliderObj.AddComponent<RectTransform>();
            sliderRect.sizeDelta = new Vector2(280, 36);

            Image sliderBg = sliderObj.AddComponent<Image>();
            sliderBg.color = new Color(0.1f, 0.1f, 0.15f, 0.5f);

            slider = sliderObj.AddComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = defaultValue;

            GameObject bgObj = new GameObject("Background");
            bgObj.transform.SetParent(sliderObj.transform, false);
            Image bgImage = bgObj.AddComponent<Image>();
            bgImage.color = new Color(0.1f, 0.1f, 0.15f, 0.6f);
            RectTransform bgRect = bgObj.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0, 0.25f);
            bgRect.anchorMax = new Vector2(1, 0.75f);
            bgRect.sizeDelta = Vector2.zero;

            CreateSliderTicks(bgObj.transform, min, max);

            GameObject fillAreaObj = new GameObject("Fill Area");
            fillAreaObj.transform.SetParent(sliderObj.transform, false);
            RectTransform fillAreaRect = fillAreaObj.AddComponent<RectTransform>();
            fillAreaRect.anchorMin = new Vector2(0, 0.25f);
            fillAreaRect.anchorMax = new Vector2(1, 0.75f);
            fillAreaRect.offsetMin = new Vector2(2, 0);
            fillAreaRect.offsetMax = new Vector2(-2, 0);

            GameObject fillObj = new GameObject("Fill");
            fillObj.transform.SetParent(fillAreaObj.transform, false);
            Image fillImage = fillObj.AddComponent<Image>();
            fillImage.color = new Color(0.25f, 0.45f, 0.75f, 0.9f);
            RectTransform fillRect = fillObj.GetComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0, 0);
            fillRect.anchorMax = new Vector2(0, 1);
            fillRect.sizeDelta = Vector2.zero;

            GameObject handleAreaObj = new GameObject("Handle Slide Area");
            handleAreaObj.transform.SetParent(sliderObj.transform, false);
            RectTransform handleAreaRect = handleAreaObj.AddComponent<RectTransform>();
            handleAreaRect.anchorMin = Vector2.zero;
            handleAreaRect.anchorMax = Vector2.one;
            handleAreaRect.offsetMin = new Vector2(15, 0);
            handleAreaRect.offsetMax = new Vector2(-15, 0);

            GameObject handleObj = new GameObject("Handle");
            handleObj.transform.SetParent(handleAreaObj.transform, false);
            Image handleImage = handleObj.AddComponent<Image>();
            handleImage.color = new Color(0.85f, 0.88f, 0.95f, 1f);
            RectTransform handleRect = handleObj.GetComponent<RectTransform>();
            handleRect.anchorMin = new Vector2(0.5f, 0f);
            handleRect.anchorMax = new Vector2(0.5f, 1f);
            handleRect.pivot = new Vector2(0.5f, 0.5f);
            handleRect.sizeDelta = new Vector2(30, 0);
            handleRect.anchoredPosition = Vector2.zero;

            slider.targetGraphic = handleImage;
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;

            return sliderObj;
        }

        private void CreateSliderTicks(Transform parent, float min, float max)
        {
            GameObject ticksContainer = new GameObject("Ticks");
            ticksContainer.transform.SetParent(parent, false);
            RectTransform ticksRect = ticksContainer.AddComponent<RectTransform>();
            ticksRect.anchorMin = Vector2.zero;
            ticksRect.anchorMax = Vector2.one;
            ticksRect.sizeDelta = Vector2.zero;

            int tickCount = 5;
            for (int i = 0; i < tickCount; i++)
            {
                GameObject tick = new GameObject($"Tick_{i}");
                tick.transform.SetParent(ticksContainer.transform, false);
                Image tickImage = tick.AddComponent<Image>();
                tickImage.color = new Color(0.4f, 0.4f, 0.5f, 0.5f);
                RectTransform tickRect = tick.GetComponent<RectTransform>();
                tickRect.anchorMin = new Vector2((float)i / (tickCount - 1), 0);
                tickRect.anchorMax = new Vector2((float)i / (tickCount - 1), 1);
                tickRect.pivot = new Vector2(0.5f, 0.5f);
                tickRect.sizeDelta = new Vector2(2, 8);
                tickRect.anchoredPosition = Vector2.zero;
            }
        }

        private void CreateToggleRow(Transform parent, string label, string localizationKey, bool defaultValue, out Toggle toggle)
        {
            GameObject row = new GameObject(label + "Row");
            row.transform.SetParent(parent, false);

            RectTransform rect = row.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 0.5f);
            rect.anchorMax = new Vector2(1, 0.5f);
            rect.sizeDelta = new Vector2(0, 32);
            rect.anchoredPosition = Vector2.zero;

            HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.padding = new RectOffset(0, 0, 0, 0);

            GameObject labelObj = CreateText("Label", row.transform, label, Vector2.zero, new Vector2(0, 32), 14, TextAlignmentOptions.Left);
            LayoutElement labelLayout = labelObj.AddComponent<LayoutElement>();
            labelLayout.flexibleWidth = 1;
            
            if (!string.IsNullOrEmpty(localizationKey))
            {
                AddLocalizedText(labelObj, localizationKey);
            }

            GameObject toggleObj = CreateToggle("Toggle", row.transform, defaultValue, out toggle);
            LayoutElement toggleLayout = toggleObj.AddComponent<LayoutElement>();
            toggleLayout.preferredWidth = 32;
            toggleLayout.preferredHeight = 32;
            toggleLayout.flexibleWidth = 0;
        }

        private GameObject CreateToggle(string name, Transform parent, bool defaultValue, out Toggle toggle)
        {
            GameObject toggleObj = new GameObject(name);
            toggleObj.transform.SetParent(parent, false);

            RectTransform toggleRect = toggleObj.AddComponent<RectTransform>();
            toggleRect.sizeDelta = new Vector2(32, 32);

            toggle = toggleObj.AddComponent<Toggle>();
            toggle.isOn = defaultValue;

            GameObject bgObj = new GameObject("Background");
            bgObj.transform.SetParent(toggleObj.transform, false);
            Image bgImage = bgObj.AddComponent<Image>();
            bgImage.color = new Color(0.3f, 0.3f, 0.35f, 0.95f);
            RectTransform bgRect = bgObj.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0.5f, 0.5f);
            bgRect.anchorMax = new Vector2(0.5f, 0.5f);
            bgRect.pivot = new Vector2(0.5f, 0.5f);
            bgRect.sizeDelta = new Vector2(26, 26);
            bgRect.anchoredPosition = Vector2.zero;

            GameObject fillObj = new GameObject("Fill");
            fillObj.transform.SetParent(bgObj.transform, false);
            Image fillImage = fillObj.AddComponent<Image>();
            fillImage.color = new Color(0.25f, 0.55f, 0.85f, 1f);
            RectTransform fillRect = fillObj.GetComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0.5f, 0.5f);
            fillRect.anchorMax = new Vector2(0.5f, 0.5f);
            fillRect.pivot = new Vector2(0.5f, 0.5f);
            fillRect.sizeDelta = new Vector2(20, 20);
            fillRect.anchoredPosition = Vector2.zero;

            toggle.targetGraphic = bgImage;
            toggle.graphic = null;

            ToggleCheckboxAnimation checkboxAnim = toggleObj.AddComponent<ToggleCheckboxAnimation>();
            checkboxAnim.fillSizeOn = 20f;

            return toggleObj;
        }

        private void CreateDropdownRow(Transform parent, string label, string localizationKey, string[] options, string[] optionKeys, int defaultValue, out TMP_Dropdown dropdown)
        {
            GameObject row = new GameObject(label + "Row");
            row.transform.SetParent(parent, false);

            RectTransform rect = row.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 0.5f);
            rect.anchorMax = new Vector2(1, 0.5f);
            rect.sizeDelta = new Vector2(0, 36);
            rect.anchoredPosition = Vector2.zero;

            HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childAlignment = TextAnchor.MiddleLeft;

            GameObject labelObj = CreateText("Label", row.transform, label, Vector2.zero, new Vector2(160, 36), 14, TextAlignmentOptions.Left);
            LayoutElement labelLayout = labelObj.AddComponent<LayoutElement>();
            labelLayout.preferredWidth = 160;
            labelLayout.flexibleWidth = 0;
            
            if (!string.IsNullOrEmpty(localizationKey))
            {
                AddLocalizedText(labelObj, localizationKey);
            }

            GameObject dropdownObj = CreateDropdown("Dropdown", row.transform, options, defaultValue, out dropdown);
            LayoutElement dropdownLayout = dropdownObj.AddComponent<LayoutElement>();
            dropdownLayout.flexibleWidth = 1;
            dropdownLayout.preferredWidth = 180;
        }

        private GameObject CreateDropdown(string name, Transform parent, string[] options, int defaultValue, out TMP_Dropdown dropdown)
        {
            GameObject dropdownObj = new GameObject(name);
            dropdownObj.transform.SetParent(parent, false);

            RectTransform dropdownRect = dropdownObj.AddComponent<RectTransform>();
            dropdownRect.sizeDelta = new Vector2(160, 34);

            Image bgImage = dropdownObj.AddComponent<Image>();
            bgImage.color = new Color(0.1f, 0.1f, 0.15f, 0.6f);

            dropdown = dropdownObj.AddComponent<TMP_Dropdown>();

            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(dropdownObj.transform, false);
            TextMeshProUGUI labelText = labelObj.AddComponent<TextMeshProUGUI>();
            labelText.fontSize = 14;
            labelText.alignment = TextAlignmentOptions.Left;
            labelText.color = Color.white;
            RectTransform labelRect = labelObj.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0, 0);
            labelRect.anchorMax = new Vector2(1, 1);
            labelRect.offsetMin = new Vector2(10, 0);
            labelRect.offsetMax = new Vector2(-25, 0);

            GameObject arrowObj = new GameObject("Arrow");
            arrowObj.transform.SetParent(dropdownObj.transform, false);
            Image arrowImage = arrowObj.AddComponent<Image>();
            arrowImage.color = new Color(0.7f, 0.7f, 0.75f, 1f);
            RectTransform arrowRect = arrowObj.GetComponent<RectTransform>();
            arrowRect.anchorMin = new Vector2(1, 0.5f);
            arrowRect.anchorMax = new Vector2(1, 0.5f);
            arrowRect.sizeDelta = new Vector2(16, 16);
            arrowRect.anchoredPosition = new Vector2(-12, 0);

            GameObject templateObj = new GameObject("Template");
            templateObj.transform.SetParent(dropdownObj.transform, false);
            templateObj.SetActive(false);
            RectTransform templateRect = templateObj.AddComponent<RectTransform>();
            templateRect.anchorMin = new Vector2(0, 0);
            templateRect.anchorMax = new Vector2(1, 0);
            templateRect.pivot = new Vector2(0.5f, 1);
            templateRect.anchoredPosition = new Vector2(0, 2);
            
            float itemHeight = 28f;
            float maxHeight = 180f;
            float calculatedHeight = Mathf.Min(options.Length * itemHeight + 10f, maxHeight);
            templateRect.sizeDelta = new Vector2(0, calculatedHeight);

            Image templateBg = templateObj.AddComponent<Image>();
            templateBg.color = new Color(0.1f, 0.1f, 0.15f, 0.95f);

            ScrollRect scrollRect = templateObj.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;

            GameObject viewportObj = new GameObject("Viewport");
            viewportObj.transform.SetParent(templateObj.transform, false);
            Image viewportMask = viewportObj.AddComponent<Image>();
            viewportMask.color = new Color(1, 1, 1, 0.01f);
            Mask mask = viewportObj.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            RectTransform viewportRect = viewportObj.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.sizeDelta = Vector2.zero;

            GameObject contentObj = new GameObject("Content");
            contentObj.transform.SetParent(viewportObj.transform, false);
            RectTransform contentRect = contentObj.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.sizeDelta = new Vector2(0, options.Length * itemHeight);

            VerticalLayoutGroup contentLayout = contentObj.AddComponent<VerticalLayoutGroup>();
            contentLayout.spacing = 2;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = false;
            contentLayout.childAlignment = TextAnchor.UpperCenter;
            contentLayout.padding = new RectOffset(5, 5, 5, 5);

            ContentSizeFitter contentFitter = contentObj.AddComponent<ContentSizeFitter>();
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            GameObject itemObj = new GameObject("Item");
            itemObj.transform.SetParent(contentObj.transform, false);
            RectTransform itemRect = itemObj.AddComponent<RectTransform>();
            itemRect.anchorMin = new Vector2(0, 0.5f);
            itemRect.anchorMax = new Vector2(1, 0.5f);
            itemRect.sizeDelta = new Vector2(0, itemHeight);

            LayoutElement itemLayout = itemObj.AddComponent<LayoutElement>();
            itemLayout.preferredHeight = itemHeight;

            Image itemBg = itemObj.AddComponent<Image>();
            itemBg.color = new Color(0.15f, 0.15f, 0.2f, 0.8f);

            Toggle itemToggle = itemObj.AddComponent<Toggle>();
            itemToggle.targetGraphic = itemBg;

            ColorBlock toggleColors = new ColorBlock
            {
                normalColor = new Color(0.15f, 0.15f, 0.2f, 0.8f),
                highlightedColor = new Color(0.25f, 0.35f, 0.5f, 0.9f),
                pressedColor = new Color(0.2f, 0.25f, 0.35f, 0.9f),
                selectedColor = new Color(0.2f, 0.3f, 0.45f, 0.9f),
                disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f),
                colorMultiplier = 1f,
                fadeDuration = 0.1f
            };
            itemToggle.colors = toggleColors;

            GameObject itemCheckObj = new GameObject("Item Check");
            itemCheckObj.transform.SetParent(itemObj.transform, false);
            Image checkImage = itemCheckObj.AddComponent<Image>();
            checkImage.color = new Color(0.25f, 0.5f, 0.8f, 0.9f);
            RectTransform checkRect = itemCheckObj.GetComponent<RectTransform>();
            checkRect.anchorMin = new Vector2(0, 0.5f);
            checkRect.anchorMax = new Vector2(0, 0.5f);
            checkRect.sizeDelta = new Vector2(16, 16);
            checkRect.anchoredPosition = new Vector2(8, 0);
            itemToggle.graphic = checkImage;

            GameObject itemLabelObj = new GameObject("Item Label");
            itemLabelObj.transform.SetParent(itemObj.transform, false);
            TextMeshProUGUI itemLabelText = itemLabelObj.AddComponent<TextMeshProUGUI>();
            itemLabelText.fontSize = 15;
            itemLabelText.alignment = TextAlignmentOptions.Left;
            itemLabelText.color = Color.white;
            itemLabelText.fontStyle = FontStyles.Bold;
            RectTransform itemLabelRect = itemLabelObj.GetComponent<RectTransform>();
            itemLabelRect.anchorMin = Vector2.zero;
            itemLabelRect.anchorMax = Vector2.one;
            itemLabelRect.offsetMin = new Vector2(32, 2);
            itemLabelRect.offsetMax = new Vector2(-8, -2);

            scrollRect.content = contentRect;
            scrollRect.viewport = viewportRect;

            dropdown.template = templateRect;
            dropdown.captionText = labelText;
            dropdown.itemText = itemLabelText;
            dropdown.ClearOptions();
            dropdown.AddOptions(new System.Collections.Generic.List<string>(options));
            dropdown.value = defaultValue;

            DropdownScrollHandler scrollHandler = dropdownObj.AddComponent<DropdownScrollHandler>();

            return dropdownObj;
        }

        private void CreateBottomButtons(Transform parent, VRSettingsMenu settingsMenu)
        {
            GameObject buttonContainer = new GameObject("BottomButtons");
            buttonContainer.transform.SetParent(parent, false);

            RectTransform buttonRect = buttonContainer.AddComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0.5f, 0);
            buttonRect.anchorMax = new Vector2(0.5f, 0);
            buttonRect.pivot = new Vector2(0.5f, 0);
            buttonRect.sizeDelta = new Vector2(400, 50);
            buttonRect.anchoredPosition = new Vector2(0, 20);

            HorizontalLayoutGroup layout = buttonContainer.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 40;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;

            GameObject backBtn = CreateButton("BackButton", buttonContainer.transform, "Back", out Button back);
            settingsMenu.backButton = back;

            GameObject resetBtn = CreateButton("ResetButton", buttonContainer.transform, "Reset", out Button reset);
            settingsMenu.resetButton = reset;
        }

        private GameObject CreateButton(string name, Transform parent, string text, out Button button)
        {
            GameObject btn = new GameObject(name);
            btn.transform.SetParent(parent, false);

            RectTransform rect = btn.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(120, 40);

            Image image = btn.AddComponent<Image>();
            image.color = new Color(0.12f, 0.15f, 0.22f, 0.6f);

            button = btn.AddComponent<Button>();
            button.targetGraphic = image;

            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.12f, 0.15f, 0.22f, 0.6f);
            colors.highlightedColor = new Color(0.2f, 0.3f, 0.45f, 0.8f);
            colors.pressedColor = new Color(0.08f, 0.1f, 0.15f, 0.7f);
            colors.selectedColor = new Color(0.18f, 0.25f, 0.38f, 0.75f);
            button.colors = colors;

            GameObject textObj = CreateText("Text", btn.transform, text, Vector2.zero, Vector2.zero, 15, TextAlignmentOptions.Center);
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            return btn;
        }

        private GameObject CreateText(string name, Transform parent, string text, Vector2 position, Vector2 size, int fontSize, TextAlignmentOptions alignment)
        {
            GameObject textObj = new GameObject(name);
            textObj.transform.SetParent(parent, false);

            RectTransform rect = textObj.AddComponent<RectTransform>();
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = alignment;
            tmp.color = new Color(0.9f, 0.9f, 0.95f, 1f);

            return textObj;
        }

        private GameObject CreateContainer(string name, Transform parent)
        {
            GameObject container = new GameObject(name);
            container.transform.SetParent(parent, false);

            RectTransform rect = container.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;

            return container;
        }

        private void CreateSettingsScene(GameObject prefabInstance)
        {
            UnityEngine.SceneManagement.Scene scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.DefaultGameObjects);

            GameObject eventSystemObj = GameObject.Find("EventSystem");
            if (eventSystemObj == null)
            {
                eventSystemObj = new GameObject("EventSystem");
            }
            
            var eventSystem = eventSystemObj.GetComponent<UnityEngine.EventSystems.EventSystem>();
            if (eventSystem == null)
            {
                eventSystemObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
            }
            
            var existingInputModule = eventSystemObj.GetComponent<UnityEngine.EventSystems.BaseInputModule>();
            if (existingInputModule != null)
            {
                DestroyImmediate(existingInputModule);
            }
            eventSystemObj.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();

            if (GameContext.Instance == null)
            {
                new GameObject("GameContext").AddComponent<GameContext>();
            }

            if (SettingsManager.Instance == null)
            {
                GameObject settingsManagerObj = new GameObject("SettingsManager");
                settingsManagerObj.AddComponent<SettingsManager>();
            }

            GameObject xrOrigin = GameObject.Find("XR Origin (XR Rig)");
            if (xrOrigin == null)
            {
                string[] xrOrigins = AssetDatabase.FindAssets("XR Origin");
                if (xrOrigins.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(xrOrigins[0]);
                    GameObject xrPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (xrPrefab != null)
                    {
                        xrOrigin = (GameObject)PrefabUtility.InstantiatePrefab(xrPrefab);
                    }
                }
            }

            if (!AssetDatabase.IsValidFolder(scenePath))
            {
                System.IO.Directory.CreateDirectory(scenePath);
            }

            string sceneFullPath = System.IO.Path.Combine(scenePath, "SettingsScene.unity");
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, sceneFullPath);
            Debug.Log($"[VRSettingsMenu] 场景已保存到: {sceneFullPath}");
        }
    }
}
#endif
