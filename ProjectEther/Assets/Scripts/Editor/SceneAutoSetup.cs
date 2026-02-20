#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

namespace OsuVR.Editor
{
    public class SceneAutoSetup : EditorWindow
    {
        [MenuItem("Project Ether/场景配置/主菜单场景", false, 1)]
        public static void SetupMainMenuScene()
        {
            GameObject root = new GameObject("[MainMenu]");
            MainMenuController controller = root.AddComponent<MainMenuController>();

            Transform menuPanel = CreateWorldSpaceCanvas("MenuPanel", root.transform);
            menuPanel.localPosition = new Vector3(0, 1.5f, 3f);

            Transform buttonContainer = CreateButtonContainer(menuPanel);
            CreateMenuButtons(buttonContainer);

            Transform titleObj = CreateTitle(menuPanel);

            SerializedObject so = new SerializedObject(controller);
            so.FindProperty("menuPanel").objectReferenceValue = menuPanel;
            so.FindProperty("buttonContainer").objectReferenceValue = buttonContainer;
            so.FindProperty("titleText").objectReferenceValue = titleObj.GetComponentInChildren<TextMeshProUGUI>();
            so.ApplyModifiedProperties();

            AudioSource audioSource = root.AddComponent<AudioSource>();
            so = new SerializedObject(controller);
            so.FindProperty("audioSource").objectReferenceValue = audioSource;
            so.ApplyModifiedProperties();

            Selection.activeGameObject = root;
            Undo.RegisterCreatedObjectUndo(root, "Create MainMenu");

            Debug.Log("[SceneAutoSetup] 主菜单场景配置完成！");
        }

        [MenuItem("Project Ether/场景配置/选歌场景", false, 2)]
        public static void SetupSongSelectScene()
        {
            GameObject root = new GameObject("[SongSelection]");
            SongSelectionController controller = root.AddComponent<SongSelectionController>();

            Transform mainPanel = CreateWorldSpaceCanvas("MainPanel", root.transform);
            mainPanel.localPosition = new Vector3(0, 1.2f, 4f);

            Transform songListPanel = CreatePanel("SongListPanel", mainPanel, new Vector2(-0.8f, 0), new Vector2(0.8f, 1.2f));
            Transform detailPanel = CreatePanel("DetailPanel", mainPanel, new Vector2(0.5f, 0), new Vector2(0.7f, 1.2f));

            Transform songListContainer = CreateScrollView(songListPanel, out ScrollRect scrollRect);

            Transform difficultyContainer = CreateDifficultyButtons(detailPanel);

            Transform modPanel = CreateModPanel(mainPanel);
            modPanel.gameObject.SetActive(false);

            Transform bottomPanel = CreateBottomButtons(mainPanel);

            CreateDetailTexts(detailPanel, controller);

            AudioSource previewSource = root.AddComponent<AudioSource>();
            AudioSource sfxSource = root.AddComponent<AudioSource>();

            SerializedObject so = new SerializedObject(controller);
            so.FindProperty("mainPanel").objectReferenceValue = mainPanel;
            so.FindProperty("songListPanel").objectReferenceValue = songListPanel;
            so.FindProperty("detailPanel").objectReferenceValue = detailPanel;
            so.FindProperty("modPanel").objectReferenceValue = modPanel;
            so.FindProperty("songListContainer").objectReferenceValue = songListContainer;
            so.FindProperty("songScrollRect").objectReferenceValue = scrollRect;
            so.FindProperty("difficultyContainer").objectReferenceValue = difficultyContainer;
            so.FindProperty("playButton").objectReferenceValue = bottomPanel.Find("PlayButton").GetComponent<Button>();
            so.FindProperty("backButton").objectReferenceValue = bottomPanel.Find("BackButton").GetComponent<Button>();
            so.FindProperty("modToggleButton").objectReferenceValue = detailPanel.Find("ModButton").GetComponent<Button>();
            so.FindProperty("previewAudioSource").objectReferenceValue = previewSource;
            so.FindProperty("audioSource").objectReferenceValue = sfxSource;
            so.ApplyModifiedProperties();

            ModPanelController modController = modPanel.gameObject.AddComponent<ModPanelController>();
            SetupModPanel(modController, modPanel);

            Selection.activeGameObject = root;
            Undo.RegisterCreatedObjectUndo(root, "Create SongSelection");

            Debug.Log("[SceneAutoSetup] 选歌场景配置完成！");
        }

        [MenuItem("Project Ether/场景配置/设置场景", false, 3)]
        public static void SetupSettingsScene()
        {
            GameObject root = new GameObject("[Settings]");
            SettingsController controller = root.AddComponent<SettingsController>();

            Transform settingsPanel = CreateWorldSpaceCanvas("SettingsPanel", root.transform);
            settingsPanel.localPosition = new Vector3(0, 1.2f, 3f);

            CreateSettingsUI(settingsPanel, controller);

            Selection.activeGameObject = root;
            Undo.RegisterCreatedObjectUndo(root, "Create Settings");

            Debug.Log("[SceneAutoSetup] 设置场景配置完成！");
        }

        [MenuItem("Project Ether/场景配置/添加XR Origin", false, 10)]
        public static void AddXROrigin()
        {
            GameObject xrOrigin = new GameObject("XR Origin (XR Rig)");

            GameObject cameraOffset = new GameObject("Camera Offset");
            cameraOffset.transform.SetParent(xrOrigin.transform);
            cameraOffset.transform.localPosition = new Vector3(0, 0, 0);

            GameObject mainCamera = new GameObject("Main Camera");
            mainCamera.transform.SetParent(cameraOffset.transform);
            mainCamera.transform.localPosition = new Vector3(0, 1.7f, 0);

            Camera cam = mainCamera.AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.02f, 0.04f);

            AudioListener listener = mainCamera.AddComponent<AudioListener>();

            Selection.activeGameObject = xrOrigin;
            Undo.RegisterCreatedObjectUndo(xrOrigin, "Create XR Origin");

            Debug.Log("[SceneAutoSetup] XR Origin 已创建！请手动添加 XR 相关组件。");
        }

        [MenuItem("Project Ether/场景配置/添加GameContext", false, 11)]
        public static void AddGameContext()
        {
            if (GameContext.Instance != null)
            {
                Debug.LogWarning("[SceneAutoSetup] GameContext 已存在！");
                return;
            }

            GameObject go = new GameObject("[GameContext]");
            go.AddComponent<GameContext>();

            Selection.activeGameObject = go;
            Undo.RegisterCreatedObjectUndo(go, "Create GameContext");

            Debug.Log("[SceneAutoSetup] GameContext 已创建！");
        }

        static Transform CreateWorldSpaceCanvas(string name, Transform parent)
        {
            GameObject canvasObj = new GameObject(name);
            canvasObj.transform.SetParent(parent);

            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 10;

            CanvasGroup group = canvasObj.AddComponent<CanvasGroup>();
            group.interactable = true;
            group.blocksRaycasts = true;

            RectTransform rect = canvasObj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(2f, 1.5f);
            rect.localScale = Vector3.one;

            return canvasObj.transform;
        }

        static Transform CreateButtonContainer(Transform parent)
        {
            GameObject container = new GameObject("ButtonContainer");
            container.transform.SetParent(parent);
            container.transform.localPosition = Vector3.zero;
            container.transform.localScale = Vector3.one;

            RectTransform rect = container.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0.8f, 0.8f);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);

            VerticalLayoutGroup layout = container.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 15f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            return container.transform;
        }

        static void CreateMenuButtons(Transform container)
        {
            string[] buttonNames = { "Play", "Settings", "Credits", "Quit" };
            Color[] buttonColors = {
                new Color(0.2f, 0.6f, 1f),
                new Color(0.5f, 0.5f, 0.5f),
                new Color(0.5f, 0.5f, 0.5f),
                new Color(0.8f, 0.2f, 0.2f)
            };

            for (int i = 0; i < buttonNames.Length; i++)
            {
                CreateVRButton(buttonNames[i], container, buttonColors[i]);
            }
        }

        static GameObject CreateVRButton(string name, Transform parent, Color? bgColor = null)
        {
            GameObject btnObj = new GameObject($"Btn_{name}");
            btnObj.transform.SetParent(parent);
            btnObj.transform.localScale = new Vector3(0.5f, 0.12f, 0.01f);

            RectTransform rect = btnObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(200f, 50f);

            Image bg = btnObj.AddComponent<Image>();
            bg.color = bgColor ?? new Color(0.15f, 0.15f, 0.2f, 0.95f);
            bg.raycastTarget = true;

            Button btn = btnObj.AddComponent<Button>();

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform);
            textObj.transform.localPosition = Vector3.zero;
            textObj.transform.localScale = Vector3.one;

            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = name;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 24f;
            tmp.color = Color.white;

            return btnObj;
        }

        static Transform CreateTitle(Transform parent)
        {
            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(parent);
            titleObj.transform.localPosition = new Vector3(0, 0.8f, 0);
            titleObj.transform.localScale = Vector3.one;

            RectTransform rect = titleObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(400f, 100f);

            TextMeshProUGUI tmp = titleObj.AddComponent<TextMeshProUGUI>();
            tmp.text = "Project Ether\n<size=50%>以太计划</size>";
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 36f;
            tmp.color = new Color(0.5f, 0.8f, 1f);

            return titleObj.transform;
        }

        static Transform CreatePanel(string name, Transform parent, Vector2 position, Vector2 size)
        {
            GameObject panel = new GameObject(name);
            panel.transform.SetParent(parent);
            panel.transform.localPosition = new Vector3(position.x, position.y, 0);
            panel.transform.localScale = Vector3.one;

            RectTransform rect = panel.AddComponent<RectTransform>();
            rect.sizeDelta = size;

            Image bg = panel.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.05f, 0.1f, 0.9f);

            return panel.transform;
        }

        static Transform CreateScrollView(Transform parent, out ScrollRect scrollRect)
        {
            GameObject scrollObj = new GameObject("ScrollView");
            scrollObj.transform.SetParent(parent);
            scrollObj.transform.localPosition = Vector3.zero;
            scrollObj.transform.localScale = Vector3.one;

            RectTransform rect = scrollObj.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;

            Image bg = scrollObj.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.1f, 0.15f, 0.8f);

            scrollRect = scrollObj.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Elastic;

            GameObject viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollObj.transform);
            viewport.transform.localPosition = Vector3.zero;
            viewport.transform.localScale = Vector3.one;

            RectTransform viewportRect = viewport.AddComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.sizeDelta = Vector2.zero;

            Image viewportMask = viewport.AddComponent<Image>();
            viewportMask.color = Color.white;

            Mask mask = viewport.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            GameObject content = new GameObject("Content");
            content.transform.SetParent(viewport.transform);
            content.transform.localPosition = Vector3.zero;
            content.transform.localScale = Vector3.one;

            RectTransform contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.sizeDelta = new Vector2(0, 300f);

            VerticalLayoutGroup layout = content.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 5f;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.viewport = viewportRect;
            scrollRect.content = contentRect;

            return content.transform;
        }

        static Transform CreateDifficultyButtons(Transform parent)
        {
            GameObject container = new GameObject("DifficultyContainer");
            container.transform.SetParent(parent);
            container.transform.localPosition = new Vector3(0, -0.3f, 0);
            container.transform.localScale = Vector3.one;

            RectTransform rect = container.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(300f, 40f);

            HorizontalLayoutGroup layout = container.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.MiddleCenter;

            return container.transform;
        }

        static Transform CreateModPanel(Transform parent)
        {
            GameObject panel = new GameObject("ModPanel");
            panel.transform.SetParent(parent);
            panel.transform.localPosition = new Vector3(0, 0, -0.1f);
            panel.transform.localScale = Vector3.one;

            RectTransform rect = panel.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(1.5f, 1f);

            Image bg = panel.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.1f, 0.15f, 0.95f);

            GameObject container = new GameObject("ModButtonContainer");
            container.transform.SetParent(panel.transform);
            container.transform.localPosition = Vector3.zero;

            RectTransform containerRect = container.AddComponent<RectTransform>();
            containerRect.anchorMin = Vector2.zero;
            containerRect.anchorMax = Vector2.one;
            containerRect.sizeDelta = Vector2.zero;

            GridLayoutGroup grid = container.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(60f, 60f);
            grid.spacing = new Vector2(10f, 10f);
            grid.childAlignment = TextAnchor.MiddleCenter;

            return panel.transform;
        }

        static void SetupModPanel(ModPanelController controller, Transform modPanel)
        {
            Transform container = modPanel.Find("ModButtonContainer");

            SerializedObject so = new SerializedObject(controller);
            so.FindProperty("modButtonContainer").objectReferenceValue = container;

            GameObject multiplierObj = CreateText("MultiplierText", modPanel, "1.00x", new Vector2(0, 0.4f));
            GameObject activeModsObj = CreateText("ActiveModsText", modPanel, "No Mod", new Vector2(0, 0.35f));

            so.FindProperty("multiplierText").objectReferenceValue = multiplierObj.GetComponent<TextMeshProUGUI>();
            so.FindProperty("activeModsText").objectReferenceValue = activeModsObj.GetComponent<TextMeshProUGUI>();

            GameObject closeBtn = CreateVRButton("Close", modPanel, new Color(0.5f, 0.2f, 0.2f));
            closeBtn.transform.localPosition = new Vector3(0, -0.4f, 0);
            closeBtn.transform.localScale = new Vector3(0.3f, 0.08f, 0.01f);
            so.FindProperty("closeButton").objectReferenceValue = closeBtn.GetComponent<Button>();

            so.ApplyModifiedProperties();
        }

        static Transform CreateBottomButtons(Transform parent)
        {
            GameObject container = new GameObject("BottomPanel");
            container.transform.SetParent(parent);
            container.transform.localPosition = new Vector3(0, -0.6f, 0);
            container.transform.localScale = Vector3.one;

            RectTransform rect = container.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(400f, 50f);

            HorizontalLayoutGroup layout = container.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 20f;
            layout.childAlignment = TextAnchor.MiddleCenter;

            GameObject playBtn = CreateVRButton("PlayButton", container.transform, new Color(0.2f, 0.7f, 0.3f));
            playBtn.name = "PlayButton";
            playBtn.transform.localScale = new Vector3(0.4f, 0.1f, 0.01f);
            playBtn.GetComponentInChildren<TextMeshProUGUI>().text = "▶ Play";

            GameObject backBtn = CreateVRButton("BackButton", container.transform, new Color(0.5f, 0.3f, 0.3f));
            backBtn.name = "BackButton";
            backBtn.transform.localScale = new Vector3(0.3f, 0.1f, 0.01f);
            backBtn.GetComponentInChildren<TextMeshProUGUI>().text = "← Back";

            return container.transform;
        }

        static void CreateDetailTexts(Transform parent, SongSelectionController controller)
        {
            GameObject titleObj = CreateText("TitleText", parent, "Song Title", new Vector2(0, 0.4f), 28f);
            GameObject artistObj = CreateText("ArtistText", parent, "Artist", new Vector2(0, 0.32f), 20f);
            GameObject mapperObj = CreateText("MapperText", parent, "Mapped by ...", new Vector2(0, 0.26f), 16f);
            GameObject diffObj = CreateText("DifficultyText", parent, "[Difficulty]", new Vector2(0, 0.18f), 18f);
            GameObject bpmObj = CreateText("BPMText", parent, "BPM: 120", new Vector2(0, 0.1f), 16f);
            GameObject lengthObj = CreateText("LengthText", parent, "Length: 3:00", new Vector2(0, 0.04f), 16f);

            GameObject modBtn = CreateVRButton("ModButton", parent, new Color(0.3f, 0.5f, 0.8f));
            modBtn.transform.localPosition = new Vector3(0, -0.15f, 0);
            modBtn.transform.localScale = new Vector3(0.35f, 0.08f, 0.01f);
            modBtn.GetComponentInChildren<TextMeshProUGUI>().text = "Mods";

            SerializedObject so = new SerializedObject(controller);
            so.FindProperty("titleText").objectReferenceValue = titleObj.GetComponent<TextMeshProUGUI>();
            so.FindProperty("artistText").objectReferenceValue = artistObj.GetComponent<TextMeshProUGUI>();
            so.FindProperty("mapperText").objectReferenceValue = mapperObj.GetComponent<TextMeshProUGUI>();
            so.FindProperty("difficultyText").objectReferenceValue = diffObj.GetComponent<TextMeshProUGUI>();
            so.FindProperty("bpmText").objectReferenceValue = bpmObj.GetComponent<TextMeshProUGUI>();
            so.FindProperty("lengthText").objectReferenceValue = lengthObj.GetComponent<TextMeshProUGUI>();
            so.ApplyModifiedProperties();
        }

        static GameObject CreateText(string name, Transform parent, string text, Vector2 position, float fontSize = 20f)
        {
            GameObject textObj = new GameObject(name);
            textObj.transform.SetParent(parent);
            textObj.transform.localPosition = new Vector3(position.x, position.y, 0);
            textObj.transform.localScale = Vector3.one;

            RectTransform rect = textObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(250f, 30f);

            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = fontSize;
            tmp.color = Color.white;

            return textObj;
        }

        static void CreateSettingsUI(Transform parent, SettingsController controller)
        {
            GameObject titleObj = CreateText("Title", parent, "Settings", new Vector2(0, 0.6f), 32f);

            GameObject volumeSection = CreateSettingsSection(parent, "Audio", new Vector2(0, 0.35f));
            Slider masterSlider = CreateSlider("MasterVolume", volumeSection.transform, "Master", 0);
            Slider musicSlider = CreateSlider("MusicVolume", volumeSection.transform, "Music", 1);
            Slider sfxSlider = CreateSlider("SFXVolume", volumeSection.transform, "SFX", 2);

            GameObject gameSection = CreateSettingsSection(parent, "Gameplay", new Vector2(0, 0f));
            Slider noteSpeedSlider = CreateSlider("NoteSpeed", gameSection.transform, "Note Speed", 0, 1f, 15f);
            Toggle autoToggle = CreateToggle("AutoPlay", gameSection.transform, "Auto Play", 1);

            GameObject visualSection = CreateSettingsSection(parent, "Visual", new Vector2(0, -0.35f));
            Slider brightnessSlider = CreateSlider("Brightness", visualSection.transform, "Brightness", 0, 0.5f, 1.5f);
            Toggle fpsToggle = CreateToggle("ShowFPS", visualSection.transform, "Show FPS", 1);

            GameObject buttonSection = new GameObject("Buttons");
            buttonSection.transform.SetParent(parent);
            buttonSection.transform.localPosition = new Vector2(0, -0.55f);
            buttonSection.transform.localScale = Vector3.one;

            RectTransform btnRect = buttonSection.AddComponent<RectTransform>();
            btnRect.sizeDelta = new Vector2(300f, 40f);

            HorizontalLayoutGroup btnLayout = buttonSection.AddComponent<HorizontalLayoutGroup>();
            btnLayout.spacing = 20f;
            btnLayout.childAlignment = TextAnchor.MiddleCenter;

            GameObject backBtn = CreateVRButton("BackButton", buttonSection.transform, new Color(0.5f, 0.3f, 0.3f));
            backBtn.transform.localScale = new Vector3(0.3f, 0.08f, 0.01f);
            backBtn.GetComponentInChildren<TextMeshProUGUI>().text = "Back";

            GameObject resetBtn = CreateVRButton("ResetButton", buttonSection.transform, new Color(0.6f, 0.4f, 0.2f));
            resetBtn.transform.localScale = new Vector3(0.3f, 0.08f, 0.01f);
            resetBtn.GetComponentInChildren<TextMeshProUGUI>().text = "Reset";

            SerializedObject so = new SerializedObject(controller);
            so.FindProperty("settingsPanel").objectReferenceValue = parent;
            so.FindProperty("masterVolumeSlider").objectReferenceValue = masterSlider;
            so.FindProperty("musicVolumeSlider").objectReferenceValue = musicSlider;
            so.FindProperty("sfxVolumeSlider").objectReferenceValue = sfxSlider;
            so.FindProperty("noteSpeedSlider").objectReferenceValue = noteSpeedSlider;
            so.FindProperty("useAutoPlayToggle").objectReferenceValue = autoToggle;
            so.FindProperty("brightnessSlider").objectReferenceValue = brightnessSlider;
            so.FindProperty("showFPSCounterToggle").objectReferenceValue = fpsToggle;
            so.FindProperty("backButton").objectReferenceValue = backBtn.GetComponent<Button>();
            so.FindProperty("resetButton").objectReferenceValue = resetBtn.GetComponent<Button>();
            so.ApplyModifiedProperties();
        }

        static GameObject CreateSettingsSection(Transform parent, string title, Vector2 position)
        {
            GameObject section = new GameObject($"Section_{title}");
            section.transform.SetParent(parent);
            section.transform.localPosition = new Vector3(position.x, position.y, 0);
            section.transform.localScale = Vector3.one;

            RectTransform rect = section.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(350f, 80f);

            VerticalLayoutGroup layout = section.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 5f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;

            GameObject titleObj = CreateText("Title", section.transform, title, Vector2.zero, 18f);
            titleObj.GetComponent<TextMeshProUGUI>().color = new Color(0.6f, 0.8f, 1f);

            return section;
        }

        static Slider CreateSlider(string name, Transform parent, string label, int index, float minVal = 0f, float maxVal = 1f)
        {
            GameObject sliderObj = new GameObject($"Slider_{name}");
            sliderObj.transform.SetParent(parent);
            sliderObj.transform.localPosition = new Vector3(0, -15f - index * 25f, 0);
            sliderObj.transform.localScale = Vector3.one;

            RectTransform rect = sliderObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(280f, 20f);

            HorizontalLayoutGroup layout = sliderObj.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10f;
            layout.childControlWidth = false;
            layout.childControlHeight = false;

            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(sliderObj.transform);
            labelObj.transform.localScale = Vector3.one;

            RectTransform labelRect = labelObj.AddComponent<RectTransform>();
            labelRect.sizeDelta = new Vector2(80f, 20f);

            TextMeshProUGUI labelTmp = labelObj.AddComponent<TextMeshProUGUI>();
            labelTmp.text = label;
            labelTmp.fontSize = 14f;
            labelTmp.alignment = TextAlignmentOptions.MidlineLeft;

            GameObject sliderGO = new GameObject("Slider");
            sliderGO.transform.SetParent(sliderObj.transform);
            sliderGO.transform.localScale = Vector3.one;

            RectTransform sliderRect = sliderGO.AddComponent<RectTransform>();
            sliderRect.sizeDelta = new Vector2(150f, 20f);

            Slider slider = sliderGO.AddComponent<Slider>();
            slider.minValue = minVal;
            slider.maxValue = maxVal;
            slider.value = maxVal;

            GameObject valueObj = new GameObject("Value");
            valueObj.transform.SetParent(sliderObj.transform);
            valueObj.transform.localScale = Vector3.one;

            RectTransform valueRect = valueObj.AddComponent<RectTransform>();
            valueRect.sizeDelta = new Vector2(40f, 20f);

            TextMeshProUGUI valueTmp = valueObj.AddComponent<TextMeshProUGUI>();
            valueTmp.text = "100%";
            valueTmp.fontSize = 12f;
            valueTmp.alignment = TextAlignmentOptions.MidlineRight;

            return slider;
        }

        static Toggle CreateToggle(string name, Transform parent, string label, int index)
        {
            GameObject toggleObj = new GameObject($"Toggle_{name}");
            toggleObj.transform.SetParent(parent);
            toggleObj.transform.localPosition = new Vector3(0, -15f - index * 25f, 0);
            toggleObj.transform.localScale = Vector3.one;

            RectTransform rect = toggleObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(200f, 20f);

            HorizontalLayoutGroup layout = toggleObj.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10f;
            layout.childControlWidth = false;
            layout.childControlHeight = false;

            Toggle toggle = toggleObj.AddComponent<Toggle>();

            GameObject checkObj = new GameObject("Background");
            checkObj.transform.SetParent(toggleObj.transform);
            checkObj.transform.localScale = Vector3.one;

            RectTransform checkRect = checkObj.AddComponent<RectTransform>();
            checkRect.sizeDelta = new Vector2(20f, 20f);

            Image checkBg = checkObj.AddComponent<Image>();
            checkBg.color = new Color(0.2f, 0.2f, 0.25f);

            GameObject markObj = new GameObject("Checkmark");
            markObj.transform.SetParent(checkObj.transform);
            markObj.transform.localScale = Vector3.one;

            RectTransform markRect = markObj.AddComponent<RectTransform>();
            markRect.sizeDelta = new Vector2(15f, 15f);

            Image markImg = markObj.AddComponent<Image>();
            markImg.color = new Color(0.3f, 0.7f, 1f);

            toggle.targetGraphic = checkBg;
            toggle.graphic = markImg;

            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(toggleObj.transform);
            labelObj.transform.localScale = Vector3.one;

            RectTransform labelRect = labelObj.AddComponent<RectTransform>();
            labelRect.sizeDelta = new Vector2(150f, 20f);

            TextMeshProUGUI labelTmp = labelObj.AddComponent<TextMeshProUGUI>();
            labelTmp.text = label;
            labelTmp.fontSize = 14f;
            labelTmp.alignment = TextAlignmentOptions.MidlineLeft;

            return toggle;
        }
    }
}
#endif
