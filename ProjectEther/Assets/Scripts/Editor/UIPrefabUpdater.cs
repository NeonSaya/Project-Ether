#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using OsuVR;
using System.Collections.Generic;

namespace OsuVR.Editor
{
    public class UIPrefabUpdater : EditorWindow
    {
        private static readonly Dictionary<string, string> SceneToPrefabMap = new Dictionary<string, string>
        {
            { "MainMenuScene", "Assets/Prefabs/UI/MainMenu.prefab" },
            { "SongSelectScene", "Assets/Prefabs/UI/SongSelection.prefab" },
            { "SettingsScene", "Assets/Prefabs/UI/VRSettingsMenu.prefab" },
            { "ResultScene", "Assets/Prefabs/UI/ResultScreen.prefab" }
        };

        [MenuItem("Project Ether/工具/更新所有UI预制体", false, 100)]
        public static void UpdateAllPrefabs()
        {
            int updated = 0;
            
            if (UpdatePrefabFromScene("MainMenuScene")) updated++;
            if (UpdatePrefabFromScene("SongSelectScene")) updated++;
            if (UpdatePrefabFromScene("SettingsScene")) updated++;
            if (UpdatePrefabFromScene("ResultScene")) updated++;
            
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            Debug.Log($"[UIPrefabUpdater] 完成！共更新 {updated} 个预制体");
        }

        [MenuItem("Project Ether/工具/更新主菜单预制体", false, 101)]
        public static bool UpdateMainMenuPrefab()
        {
            return UpdatePrefabFromScene("MainMenuScene");
        }

        [MenuItem("Project Ether/工具/更新选歌界面预制体", false, 102)]
        public static bool UpdateSongSelectionPrefab()
        {
            return UpdatePrefabFromScene("SongSelectScene");
        }

        [MenuItem("Project Ether/工具/更新设置界面预制体", false, 103)]
        public static bool UpdateSettingsMenuPrefab()
        {
            return UpdatePrefabFromScene("SettingsScene");
        }

        [MenuItem("Project Ether/工具/更新结算界面预制体", false, 104)]
        public static bool UpdateResultScreenPrefab()
        {
            return UpdatePrefabFromScene("ResultScene");
        }

        private static bool UpdatePrefabFromScene(string sceneName)
        {
            string scenePath = $"Assets/Scenes/{sceneName}.unity";
            string prefabPath = SceneToPrefabMap.TryGetValue(sceneName, out var path) ? path : null;
            
            if (string.IsNullOrEmpty(prefabPath))
            {
                Debug.LogWarning($"[UIPrefabUpdater] 未找到场景 {sceneName} 对应的预制体路径");
                return false;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            
            if (prefab == null)
            {
                Debug.LogWarning($"[UIPrefabUpdater] 未找到预制体: {prefabPath}，将尝试创建新预制体");
                return CreateNewPrefab(sceneName, prefabPath);
            }

            GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            
            if (instance == null)
            {
                Debug.LogWarning($"[UIPrefabUpdater] 无法实例化预制体: {prefabPath}");
                return false;
            }

            try
            {
                Vector3 originalPos = instance.transform.position;
                Vector3 originalScale = instance.transform.localScale;
                Quaternion originalRot = instance.transform.rotation;
                
                switch (sceneName)
                {
                    case "MainMenuScene":
                        UpdateMainMenuInstance(instance);
                        break;
                    case "SongSelectScene":
                        UpdateSongSelectionInstance(instance);
                        break;
                    case "SettingsScene":
                        UpdateSettingsMenuInstance(instance);
                        break;
                    case "ResultScene":
                        UpdateResultScreenInstance(instance);
                        break;
                }
                
                instance.transform.position = originalPos;
                instance.transform.localScale = originalScale;
                instance.transform.rotation = originalRot;
                
                PrefabUtility.ApplyPrefabInstance(instance, InteractionMode.AutomatedAction);
                Debug.Log($"[UIPrefabUpdater] 预制体已更新: {prefabPath}");
                return true;
            }
            finally
            {
                DestroyImmediate(instance);
            }
        }

        private static bool CreateNewPrefab(string sceneName, string prefabPath)
        {
            GameObject root = null;
            
            switch (sceneName)
            {
                case "MainMenuScene":
                    root = CreateMainMenuRoot();
                    break;
                case "SongSelectScene":
                    root = CreateSongSelectionRoot();
                    break;
                case "SettingsScene":
                    root = CreateSettingsMenuRoot();
                    break;
                case "ResultScene":
                    root = CreateResultScreenRoot();
                    break;
                default:
                    return false;
            }

            if (root == null) return false;

            try
            {
                string directory = System.IO.Path.GetDirectoryName(prefabPath);
                if (!AssetDatabase.IsValidFolder(directory))
                {
                    System.IO.Directory.CreateDirectory(directory);
                }

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Debug.Log($"[UIPrefabUpdater] 新预制体已创建: {prefabPath}");
                return true;
            }
            finally
            {
                DestroyImmediate(root);
            }
        }

        #region Create Roots

        private static GameObject CreateMainMenuRoot()
        {
            GameObject root = new GameObject("[MainMenu]");
            root.AddComponent<SimpleMainMenu>();

            Canvas canvas = CreateWorldCanvas("MenuCanvas", root.transform, 600, 450);

            GameObject titleObj = CreateText("Title", canvas.transform, "Project Ether\n<size=60%>以太计划</size>", 
                new Vector2(0, 120), new Vector2(500, 70), 32, TextAlignmentOptions.Center);

            GameObject buttonContainer = new GameObject("Buttons");
            buttonContainer.transform.SetParent(canvas.transform, false);
            RectTransform containerRect = buttonContainer.AddComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0.5f, 0.5f);
            containerRect.anchorMax = new Vector2(0.5f, 0.5f);
            containerRect.pivot = new Vector2(0.5f, 1f);
            containerRect.sizeDelta = new Vector2(220, 200);
            containerRect.anchoredPosition = new Vector2(0, 20);

            VerticalLayoutGroup layout = buttonContainer.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 12;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = buttonContainer.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            GameObject playBtn = CreateSimpleButton("Play", buttonContainer.transform, new Color(0.2f, 0.6f, 1f));
            AddLocalizedTextToButton(playBtn, "ui_play");
            GameObject settingsBtn = CreateSimpleButton("Settings", buttonContainer.transform, new Color(0.5f, 0.5f, 0.5f));
            AddLocalizedTextToButton(settingsBtn, "ui_settings");
            GameObject creditsBtn = CreateSimpleButton("Credits", buttonContainer.transform, new Color(0.4f, 0.5f, 0.6f));
            AddLocalizedTextToButton(creditsBtn, "ui_credits");
            GameObject quitBtn = CreateSimpleButton("Quit", buttonContainer.transform, new Color(0.8f, 0.3f, 0.3f));
            AddLocalizedTextToButton(quitBtn, "ui_quit");

            AddButtonCollider(playBtn);
            AddButtonCollider(settingsBtn);
            AddButtonCollider(creditsBtn);
            AddButtonCollider(quitBtn);

            GameObject versionObj = CreateText("Version", canvas.transform, "Demo v0.1", 
                new Vector2(0, -150), new Vector2(200, 30), 14, TextAlignmentOptions.Center);

            AudioSource audioSource = root.AddComponent<AudioSource>();

            SerializedObject so = new SerializedObject(root.GetComponent<SimpleMainMenu>());
            so.FindProperty("playButton").objectReferenceValue = playBtn.GetComponent<Button>();
            so.FindProperty("settingsButton").objectReferenceValue = settingsBtn.GetComponent<Button>();
            so.FindProperty("creditsButton").objectReferenceValue = creditsBtn.GetComponent<Button>();
            so.FindProperty("quitButton").objectReferenceValue = quitBtn.GetComponent<Button>();
            so.FindProperty("titleText").objectReferenceValue = titleObj.GetComponent<TextMeshProUGUI>();
            so.FindProperty("versionText").objectReferenceValue = versionObj.GetComponent<TextMeshProUGUI>();
            so.FindProperty("audioSource").objectReferenceValue = audioSource;
            so.ApplyModifiedProperties();

            return root;
        }

        private static GameObject CreateSongSelectionRoot()
        {
            GameObject root = new GameObject("[SongSelection]");
            root.AddComponent<SimpleSongSelection>();

            Canvas canvas = CreateWorldCanvas("SongSelectCanvas", root.transform, 1200, 800);

            return root;
        }

        private static GameObject CreateSettingsMenuRoot()
        {
            GameObject root = new GameObject("VRSettingsMenu");

            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 10;

            root.AddComponent<GraphicRaycaster>();

            CanvasGroup canvasGroup = root.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;

            root.AddComponent<VRSettingsMenu>();

            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(600, 450);
            rootRect.localScale = new Vector3(0.0025f, 0.0025f, 0.0025f);

            CreateSettingsUIStructure(root);

            return root;
        }

        private static GameObject CreateResultScreenRoot()
        {
            return ResultScreenCreator.CreateResultScreenRoot();
        }

        #endregion

        #region MainMenu Update

        private static void UpdateMainMenuInstance(GameObject instance)
        {
            Transform canvas = instance.transform.Find("MenuCanvas");
            if (canvas == null) return;

            Transform buttons = canvas.Find("Buttons");
            if (buttons != null)
            {
                foreach (Transform btn in buttons)
                {
                    UpdateButtonLocalization(btn);
                }
            }
        }

        #endregion

        #region SongSelection Update

        private static void UpdateSongSelectionInstance(GameObject instance)
        {
            Transform canvas = instance.transform.Find("SongSelectCanvas");
            if (canvas == null) return;

            Transform bgPanel = canvas.Find("BackgroundPanel");
            if (bgPanel == null) return;

            Transform leftArea = bgPanel.Find("LeftArea");
            if (leftArea != null)
            {
                Transform titleText = leftArea.Find("Title");
                if (titleText != null)
                {
                    AddLocalizedText(titleText.gameObject, "ui_beatmaps");
                }
            }

            Transform rightTop = bgPanel.Find("RightTopArea");
            if (rightTop != null)
            {
                Transform infoPanel = rightTop.Find("InfoPanel");
                if (infoPanel != null)
                {
                    UpdateInfoPanelLocalization(infoPanel);
                }

                Transform modPanel = rightTop.Find("ModPanel");
                if (modPanel != null)
                {
                    UpdateModPanelLocalization(modPanel);
                }
            }

            Transform rightBottom = bgPanel.Find("RightBottomArea");
            if (rightBottom != null)
            {
                Transform btnContainer = rightBottom.Find("ButtonContainer");
                if (btnContainer != null)
                {
                    Transform backBtn = btnContainer.Find("Btn_Back");
                    if (backBtn != null) UpdateButtonLocalization(backBtn);
                    
                    Transform playBtn = btnContainer.Find("Btn_Play");
                    if (playBtn != null) UpdateButtonLocalization(playBtn);
                }
            }
        }

        private static void UpdateInfoPanelLocalization(Transform infoPanel)
        {
            Transform statsContainer = infoPanel.Find("StatsContainer");
            if (statsContainer != null)
            {
                string[] statKeys = { "ui_cs", "ui_ar", "ui_od", "ui_hp" };
                string[] statNames = { "CS", "AR", "OD", "HP" };
                
                for (int i = 0; i < statNames.Length; i++)
                {
                    Transform stat = statsContainer.Find(statNames[i]);
                    if (stat != null)
                    {
                        Transform label = stat.Find("Label");
                        if (label != null)
                        {
                            AddLocalizedText(label.gameObject, statKeys[i]);
                        }
                    }
                }
            }
        }

        private static void UpdateModPanelLocalization(Transform modPanel)
        {
            Transform activeMods = modPanel.Find("ActiveMods");
            if (activeMods != null)
            {
                AddLocalizedText(activeMods.gameObject, "ui_no_mod");
            }
        }

        #endregion

        #region SettingsMenu Update

        private static void CreateSettingsUIStructure(GameObject root)
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

            CreateSettingsTabBar(mainContainer.transform, settingsMenu);

            GameObject contentArea = CreateContainer("ContentArea", mainContainer.transform);
            RectTransform contentRect = contentArea.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 0);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.offsetMin = new Vector2(25, 75);
            contentRect.offsetMax = new Vector2(-25, -55);

            CreateSettingsAudioPanel(contentArea.transform, settingsMenu);
            CreateSettingsGraphicsPanel(contentArea.transform, settingsMenu);
            CreateSettingsGamePanel(contentArea.transform, settingsMenu);
            CreateSettingsControllerPanel(contentArea.transform, settingsMenu);

            CreateSettingsBottomButtons(mainContainer.transform, settingsMenu);

            AudioSource audioSource = root.AddComponent<AudioSource>();
            settingsMenu.audioSource = audioSource;
        }

        private static void CreateSettingsTabBar(Transform parent, VRSettingsMenu settingsMenu)
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
                GameObject tabBtn = CreateSettingsTabButton($"Tab_{tabNames[i]}", tabBar.transform, tabNames[i], tabKeys[i], out Button btn);
                settingsMenu.tabButtons[i] = btn;
            }
        }

        private static GameObject CreateSettingsTabButton(string name, Transform parent, string text, string localizationKey, out Button button)
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

        private static void CreateSettingsAudioPanel(Transform parent, VRSettingsMenu settingsMenu)
        {
            GameObject panel = CreateContainer("AudioPanel", parent, 0);
            settingsMenu.tabPanels[0] = panel;

            CreateSliderRow(panel.transform, "Audio Offset", "ui_audio_offset", -200, 200, 0, "ms", out Slider audioOffsetSlider, out TextMeshProUGUI audioOffsetText);
            settingsMenu.audioOffsetSlider = audioOffsetSlider;
            settingsMenu.audioOffsetValueText = audioOffsetText;

            CreateSliderRow(panel.transform, "Master Volume", "ui_master_volume", 0, 1, 0.8f, "percent", out Slider masterSlider, out TextMeshProUGUI masterText);
            settingsMenu.masterVolumeSlider = masterSlider;
            settingsMenu.masterVolumeValueText = masterText;

            CreateSliderRow(panel.transform, "Music Volume", "ui_music_volume", 0, 1, 0.6f, "percent", out Slider musicSlider, out TextMeshProUGUI musicText);
            settingsMenu.musicVolumeSlider = musicSlider;
            settingsMenu.musicVolumeValueText = musicText;

            CreateSliderRow(panel.transform, "SFX Volume", "ui_sfx_volume", 0, 1, 0.8f, "percent", out Slider sfxSlider, out TextMeshProUGUI sfxText);
            settingsMenu.sfxVolumeSlider = sfxSlider;
            settingsMenu.sfxVolumeValueText = sfxText;
        }

        private static void CreateSettingsGraphicsPanel(Transform parent, VRSettingsMenu settingsMenu)
        {
            GameObject panel = CreateContainer("GraphicsPanel", parent, 1);
            settingsMenu.tabPanels[1] = panel;

            CreateDropdownRow(panel.transform, "Quality", "ui_quality", new[] { "Low", "Medium", "High", "Ultra" }, 2, out TMP_Dropdown qualityDropdown);
            settingsMenu.qualityDropdown = qualityDropdown;

            CreateDropdownRow(panel.transform, "Anti-Aliasing", "ui_anti_aliasing", new[] { "Off", "2x", "4x", "8x" }, 2, out TMP_Dropdown aaDropdown);
            settingsMenu.antiAliasingDropdown = aaDropdown;

            CreateSliderRow(panel.transform, "Particle Density", "ui_particle_density", 0, 1, 0.7f, "percent", out Slider particleSlider, out TextMeshProUGUI particleText);
            settingsMenu.particleDensitySlider = particleSlider;
            settingsMenu.particleDensityValueText = particleText;
        }

        private static void CreateSettingsGamePanel(Transform parent, VRSettingsMenu settingsMenu)
        {
            GameObject panel = CreateContainer("GamePanel", parent, 2);
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

        private static void CreateSettingsControllerPanel(Transform parent, VRSettingsMenu settingsMenu)
        {
            GameObject panel = CreateContainer("ControllerOffsetPanel", parent, 3);
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
        }

        private static void CreateLanguageRow(Transform parent, out TMP_Dropdown dropdown)
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

        private static GameObject CreateLanguageDropdown(string name, Transform parent, string[] options, int defaultValue, out TMP_Dropdown dropdown)
        {
            GameObject dropdownObj = CreateDropdown(name, parent, options, defaultValue, out dropdown);
            
            dropdown.onValueChanged.AddListener((index) =>
            {
                LocalizationManager.SetLanguageByIndex(index);
            });

            return dropdownObj;
        }

        private static void CreateSettingsBottomButtons(Transform parent, VRSettingsMenu settingsMenu)
        {
            GameObject buttonRow = new GameObject("ButtonRow");
            buttonRow.transform.SetParent(parent, false);

            RectTransform rect = buttonRow.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(1, 0);
            rect.pivot = new Vector2(0.5f, 0);
            rect.sizeDelta = new Vector2(0, 45);
            rect.anchoredPosition = Vector2.zero;

            HorizontalLayoutGroup layout = buttonRow.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 15;
            layout.padding = new RectOffset(25, 25, 8, 8);
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childAlignment = TextAnchor.MiddleCenter;

            GameObject backBtn = CreateSimpleButton("Back", buttonRow.transform, new Color(0.5f, 0.3f, 0.3f, 0.8f));
            AddLocalizedTextToButton(backBtn, "ui_back");
            settingsMenu.backButton = backBtn.GetComponent<Button>();

            GameObject resetBtn = CreateSimpleButton("Reset", buttonRow.transform, new Color(0.3f, 0.5f, 0.3f, 0.8f));
            settingsMenu.resetButton = resetBtn.GetComponent<Button>();
        }

        private static void UpdateSettingsMenuInstance(GameObject instance)
        {
            Transform container = instance.transform.Find("SettingsContainer");
            if (container == null) return;

            Transform tabBar = container.Find("TabBar");
            if (tabBar != null)
            {
                string[] tabKeys = { "ui_tab_audio", "ui_tab_graphics", "ui_tab_game", null };
                string[] tabNames = { "Audio", "Graphics", "Game", "Controller" };
                
                for (int i = 0; i < tabNames.Length; i++)
                {
                    Transform tab = tabBar.Find($"Tab_{tabNames[i]}");
                    if (tab != null && !string.IsNullOrEmpty(tabKeys[i]))
                    {
                        Transform text = tab.Find("Text");
                        if (text != null)
                        {
                            AddLocalizedText(text.gameObject, tabKeys[i]);
                        }
                    }
                }
            }

            Transform contentArea = container.Find("ContentArea");
            if (contentArea != null)
            {
                Transform gamePanel = contentArea.Find("GamePanel");
                if (gamePanel != null)
                {
                    UpdateGamePanelLocalization(gamePanel);
                }
            }

            Transform buttonRow = container.Find("ButtonRow");
            if (buttonRow != null)
            {
                foreach (Transform btn in buttonRow)
                {
                    UpdateButtonLocalization(btn);
                }
            }
        }

        private static void UpdateGamePanelLocalization(Transform gamePanel)
        {
            Transform languageRow = gamePanel.Find("LanguageRow");
            if (languageRow != null)
            {
                Transform label = languageRow.Find("Label");
                if (label != null)
                {
                    AddLocalizedText(label.gameObject, "ui_language");
                }
            }
        }

        #endregion

        #region ResultScreen Update

        private static void UpdateResultScreenInstance(GameObject instance)
        {
            Transform canvas = instance.transform.Find("ResultCanvas");
            if (canvas == null) return;

            Transform resultPanel = canvas.Find("ResultPanel");
            if (resultPanel == null) return;

            Transform scoreSection = resultPanel.Find("ScoreSection");
            if (scoreSection != null)
            {
                UpdateScoreSectionLocalization(scoreSection);
            }

            Transform judgmentSection = resultPanel.Find("JudgmentSection");
            if (judgmentSection != null)
            {
                UpdateJudgmentSectionLocalization(judgmentSection);
            }

            Transform buttonSection = resultPanel.Find("ButtonSection");
            if (buttonSection != null)
            {
                foreach (Transform btn in buttonSection)
                {
                    UpdateButtonLocalization(btn);
                }
            }
        }

        private static void UpdateScoreSectionLocalization(Transform scoreSection)
        {
            string[][] labelMappings = {
                new[] { "ScoreLabel", "ui_score" },
                new[] { "AccuracyLabel", "ui_accuracy" },
                new[] { "ComboLabel", "ui_max_combo" }
            };

            foreach (var mapping in labelMappings)
            {
                Transform label = scoreSection.Find(mapping[0]);
                if (label != null)
                {
                    AddLocalizedText(label.gameObject, mapping[1]);
                }
            }
        }

        private static void UpdateJudgmentSectionLocalization(Transform judgmentSection)
        {
            string[][] labelMappings = {
                new[] { "Hit300Label", "ui_hit300" },
                new[] { "Hit100Label", "ui_hit100" },
                new[] { "Hit50Label", "ui_hit50" },
                new[] { "MissLabel", "ui_miss" }
            };

            foreach (var mapping in labelMappings)
            {
                Transform label = judgmentSection.Find(mapping[0]);
                if (label != null)
                {
                    AddLocalizedText(label.gameObject, mapping[1]);
                }
            }
        }

        #endregion

        #region Helper Methods

        private static Canvas CreateWorldCanvas(string name, Transform parent, float width, float height)
        {
            GameObject canvasObj = new GameObject(name);
            canvasObj.transform.SetParent(parent);

            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 10;

            canvasObj.AddComponent<GraphicRaycaster>();

            RectTransform rect = canvasObj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(width, height);
            rect.localScale = new Vector3(0.0025f, 0.0025f, 0.0025f);
            rect.anchoredPosition3D = new Vector3(0, 2.5f, 1.5f);

            return canvas;
        }

        private static GameObject CreateContainer(string name, Transform parent, int tabIndex = -1)
        {
            GameObject container = new GameObject(name);
            container.transform.SetParent(parent, false);

            RectTransform rect = container.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;

            if (tabIndex >= 0)
            {
                container.SetActive(tabIndex == 0);
            }

            return container;
        }

        private static GameObject CreateText(string name, Transform parent, string text, Vector2 anchoredPos, Vector2 size, int fontSize, TextAlignmentOptions alignment)
        {
            GameObject textObj = new GameObject(name);
            textObj.transform.SetParent(parent, false);

            RectTransform rect = textObj.AddComponent<RectTransform>();
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;

            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = alignment;
            tmp.color = Color.white;
            tmp.overflowMode = TextOverflowModes.Overflow;

            return textObj;
        }

        private static GameObject CreateSimpleButton(string name, Transform parent, Color color)
        {
            GameObject btnObj = new GameObject($"Btn_{name}");
            btnObj.transform.SetParent(parent, false);

            RectTransform rect = btnObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 0.5f);
            rect.anchorMax = new Vector2(1, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0, 50);

            Image img = btnObj.AddComponent<Image>();
            img.color = color;

            Button btn = btnObj.AddComponent<Button>();

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = name;
            tmp.fontSize = 20;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;

            return btnObj;
        }

        private static void AddButtonCollider(GameObject btnObj)
        {
            BoxCollider collider = btnObj.AddComponent<BoxCollider>();
            RectTransform rect = btnObj.GetComponent<RectTransform>();
            collider.size = new Vector3(rect.sizeDelta.x, rect.sizeDelta.y, 10);
            collider.center = Vector3.zero;
            collider.isTrigger = true;
        }

        private static void CreateSliderRow(Transform parent, string label, string localizationKey, float min, float max, float defaultValue, string format, out Slider slider, out TextMeshProUGUI valueText)
        {
            GameObject row = new GameObject(label.Replace(" ", "") + "Row");
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

            GameObject sliderObj = new GameObject("Slider");
            sliderObj.transform.SetParent(row.transform, false);
            slider = sliderObj.AddComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = defaultValue;

            RectTransform sliderRect = sliderObj.GetComponent<RectTransform>();
            LayoutElement sliderLayout = sliderObj.AddComponent<LayoutElement>();
            sliderLayout.flexibleWidth = 1;
            sliderLayout.preferredWidth = 120;

            GameObject valueObj = CreateText("Value", row.transform, "", Vector2.zero, new Vector2(60, 36), 14, TextAlignmentOptions.Right);
            LayoutElement valueLayout = valueObj.AddComponent<LayoutElement>();
            valueLayout.preferredWidth = 60;
            valueLayout.flexibleWidth = 0;
            valueText = valueObj.GetComponent<TextMeshProUGUI>();

            if (format == "percent")
                valueText.text = $"{Mathf.RoundToInt(defaultValue * 100)}%";
            else
                valueText.text = $"{defaultValue}{format}";
        }

        private static void CreateToggleRow(Transform parent, string label, string localizationKey, bool defaultValue, out Toggle toggle)
        {
            GameObject row = new GameObject(label.Replace(" ", "") + "Row");
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

            GameObject toggleObj = new GameObject("Toggle");
            toggleObj.transform.SetParent(row.transform, false);
            toggle = toggleObj.AddComponent<Toggle>();
            toggle.isOn = defaultValue;

            RectTransform toggleRect = toggleObj.GetComponent<RectTransform>();
            toggleRect.sizeDelta = new Vector2(24, 24);

            Image bg = toggleObj.AddComponent<Image>();
            bg.color = new Color(0.15f, 0.15f, 0.2f, 0.9f);
            toggle.targetGraphic = bg;

            GameObject checkmark = new GameObject("Checkmark");
            checkmark.transform.SetParent(toggleObj.transform, false);
            Image checkImg = checkmark.AddComponent<Image>();
            checkImg.color = new Color(0.25f, 0.55f, 0.85f, 1f);
            toggle.graphic = checkImg;

            RectTransform checkRect = checkmark.GetComponent<RectTransform>();
            checkRect.anchorMin = Vector2.zero;
            checkRect.anchorMax = Vector2.one;
            checkRect.sizeDelta = Vector2.zero;
        }

        private static void CreateDropdownRow(Transform parent, string label, string localizationKey, string[] options, int defaultValue, out TMP_Dropdown dropdown)
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

        private static GameObject CreateDropdown(string name, Transform parent, string[] options, int defaultValue, out TMP_Dropdown dropdown)
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
            RectTransform contentRect = contentObj.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0, options.Length * itemHeight);

            GameObject itemObj = new GameObject("Item");
            itemObj.transform.SetParent(contentObj.transform, false);
            RectTransform itemRect = itemObj.GetComponent<RectTransform>();
            itemRect.anchorMin = new Vector2(0, 0.5f);
            itemRect.anchorMax = new Vector2(1, 0.5f);
            itemRect.sizeDelta = new Vector2(0, itemHeight);

            Image itemBg = itemObj.AddComponent<Image>();
            itemBg.color = new Color(0.15f, 0.15f, 0.2f, 0.9f);

            GameObject itemCheckObj = new GameObject("Item Check");
            itemCheckObj.transform.SetParent(itemObj.transform, false);
            Image itemCheckImg = itemCheckObj.AddComponent<Image>();
            itemCheckImg.color = new Color(0.25f, 0.55f, 0.85f, 1f);
            RectTransform itemCheckRect = itemCheckObj.GetComponent<RectTransform>();
            itemCheckRect.anchorMin = new Vector2(0, 0.5f);
            itemCheckRect.anchorMax = new Vector2(0, 0.5f);
            itemCheckRect.sizeDelta = new Vector2(16, 16);
            itemCheckRect.anchoredPosition = new Vector2(12, 0);

            GameObject itemLabelObj = new GameObject("Item Label");
            itemLabelObj.transform.SetParent(itemObj.transform, false);
            TextMeshProUGUI itemLabel = itemLabelObj.AddComponent<TextMeshProUGUI>();
            itemLabel.fontSize = 14;
            itemLabel.alignment = TextAlignmentOptions.Left;
            itemLabel.color = Color.white;
            RectTransform itemLabelRect = itemLabelObj.GetComponent<RectTransform>();
            itemLabelRect.anchorMin = new Vector2(0, 0);
            itemLabelRect.anchorMax = new Vector2(1, 1);
            itemLabelRect.offsetMin = new Vector2(30, 0);
            itemLabelRect.offsetMax = new Vector2(-5, 0);

            dropdown.template = templateRect;
            dropdown.captionText = labelText;
            dropdown.itemText = itemLabel;
            dropdown.value = defaultValue;

            var optionsList = new List<string>(options);
            dropdown.ClearOptions();
            dropdown.AddOptions(optionsList);

            scrollRect.content = contentRect;
            scrollRect.viewport = viewportRect;

            return dropdownObj;
        }

        private static void UpdateButtonLocalization(Transform buttonTransform)
        {
            Transform text = buttonTransform.Find("Text");
            if (text != null)
            {
                string btnName = buttonTransform.name;
                string key = GetButtonLocalizationKey(btnName);
                if (!string.IsNullOrEmpty(key))
                {
                    AddLocalizedText(text.gameObject, key);
                }
            }
        }

        private static string GetButtonLocalizationKey(string buttonName)
        {
            if (buttonName.Contains("Play")) return "ui_play";
            if (buttonName.Contains("Settings")) return "ui_settings";
            if (buttonName.Contains("Credits")) return "ui_credits";
            if (buttonName.Contains("Quit")) return "ui_quit";
            if (buttonName.Contains("Back")) return "ui_back";
            if (buttonName.Contains("Retry")) return "ui_retry";
            if (buttonName.Contains("Mods")) return "ui_mods";
            return null;
        }

        private static void AddLocalizedText(GameObject textObj, string key)
        {
            if (textObj == null || string.IsNullOrEmpty(key)) return;
            
            var existing = textObj.GetComponent<LocalizedText>();
            if (existing != null)
            {
                SerializedObject so = new SerializedObject(existing);
                so.FindProperty("localizationKey").stringValue = key;
                so.ApplyModifiedProperties();
                return;
            }
            
            var localizedText = textObj.AddComponent<LocalizedText>();
            SerializedObject so2 = new SerializedObject(localizedText);
            so2.FindProperty("localizationKey").stringValue = key;
            so2.ApplyModifiedProperties();
        }

        private static void AddLocalizedTextToButton(GameObject buttonObj, string key)
        {
            Transform text = buttonObj.transform.Find("Text");
            if (text != null)
            {
                AddLocalizedText(text.gameObject, key);
            }
        }

        #endregion
    }
}
#endif
