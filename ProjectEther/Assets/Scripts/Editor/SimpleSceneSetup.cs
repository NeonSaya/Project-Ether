#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;
using UnityEngine.EventSystems;
using OsuVR;

namespace OsuVR.Editor
{
    public class SimpleSceneSetup : EditorWindow
    {
        const float CANVAS_SCALE = 0.0025f;
        const float CANVAS_POS_Y = 2.5f;
        const float CANVAS_POS_Z = 1.5f;
        const float CANVAS_WIDTH = 1200f;
        const float CANVAS_HEIGHT = 800f;

        [MenuItem("Project Ether/简单配置/主菜单场景", false, 1)]
        public static void SetupMainMenu()
        {
            GameObject root = new GameObject("[MainMenu]");
            SimpleMainMenu menu = root.AddComponent<SimpleMainMenu>();

            Canvas canvas = CreateSimpleCanvas("MenuCanvas", root.transform, 600, 450);

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

            GameObject versionObj = CreateText("Version", canvas.transform, "v0.6.0 Beta",
                new Vector2(0, -150), new Vector2(200, 30), 14, TextAlignmentOptions.Center);

            AudioSource audioSource = root.AddComponent<AudioSource>();

            SerializedObject so = new SerializedObject(menu);
            so.FindProperty("playButton").objectReferenceValue = playBtn.GetComponent<Button>();
            so.FindProperty("settingsButton").objectReferenceValue = settingsBtn.GetComponent<Button>();
            so.FindProperty("creditsButton").objectReferenceValue = creditsBtn.GetComponent<Button>();
            so.FindProperty("quitButton").objectReferenceValue = quitBtn.GetComponent<Button>();
            so.FindProperty("titleText").objectReferenceValue = titleObj.GetComponent<TextMeshProUGUI>();
            so.FindProperty("versionText").objectReferenceValue = versionObj.GetComponent<TextMeshProUGUI>();
            so.FindProperty("audioSource").objectReferenceValue = audioSource;
            so.ApplyModifiedProperties();

            Selection.activeGameObject = root;
            Undo.RegisterCreatedObjectUndo(root, "Create MainMenu");

            Debug.Log("[SimpleSceneSetup] 主菜单场景配置完成！");
        }

        [MenuItem("Project Ether/简单配置/选歌场景", false, 2)]
        public static void SetupSongSelect()
        {
            GameObject root = new GameObject("[SongSelection]");
            SimpleSongSelection selection = root.AddComponent<SimpleSongSelection>();

            Canvas canvas = CreateAllInOneCanvas("SongSelectCanvas", root.transform);

            GameObject backgroundPanel = CreateBackgroundPanel(canvas.transform);

            GameObject leftArea = CreateLeftArea(backgroundPanel.transform);

            GameObject rightTopArea = CreateRightTopArea(backgroundPanel.transform);

            GameObject rightBottomArea = CreateRightBottomArea(backgroundPanel.transform);

            GameObject infoPanel = CreateInfoPanel(rightTopArea.transform);
            GameObject modPanel = CreateModPanel(rightTopArea.transform);

            Transform listContent = leftArea.transform.Find("SongScrollView/Viewport/Content");
            Transform modButtonContainer = modPanel.transform.Find("ModContainer");

            GameObject multiplierObj = modPanel.transform.Find("Multiplier").gameObject;
            GameObject activeModsObj = modPanel.transform.Find("ActiveMods").gameObject;

            GameObject titleTextObj = infoPanel.transform.Find("Title").gameObject;
            GameObject artistTextObj = infoPanel.transform.Find("Artist").gameObject;
            GameObject csTextObj = infoPanel.transform.Find("StatsContainer/CS/Value").gameObject;
            GameObject arTextObj = infoPanel.transform.Find("StatsContainer/AR/Value").gameObject;
            GameObject odTextObj = infoPanel.transform.Find("StatsContainer/OD/Value").gameObject;
            GameObject hpTextObj = infoPanel.transform.Find("StatsContainer/HP/Value").gameObject;
            GameObject lengthTextObj = infoPanel.transform.Find("StatsContainer/Length/Value").gameObject;
            GameObject difficultyTextObj = infoPanel.transform.Find("DifficultySelector/Text").gameObject;
            GameObject backgroundImageObj = infoPanel.transform.Find("BackgroundImage").gameObject;
            Transform difficultyDropdownContainer = infoPanel.transform.Find("DifficultyDropdownContainer");

            GameObject backBtn = rightBottomArea.transform.Find("ButtonContainer/Btn_Back").gameObject;
            GameObject toggleModsBtn = rightBottomArea.transform.Find("ButtonContainer/Btn_ToggleMods").gameObject;
            GameObject playBtn = rightBottomArea.transform.Find("ButtonContainer/Btn_Play").gameObject;
            GameObject modStatusObj = rightBottomArea.transform.Find("ModStatusText").gameObject;

            GameObject scrollUpBtn = leftArea.transform.Find("ScrollButtons/Btn_ScrollUp").gameObject;
            GameObject scrollDownBtn = leftArea.transform.Find("ScrollButtons/Btn_ScrollDown").gameObject;

            AddButtonCollider(backBtn);
            AddButtonCollider(toggleModsBtn);
            AddButtonCollider(playBtn);
            AddButtonCollider(scrollUpBtn);
            AddButtonCollider(scrollDownBtn);

            AudioSource audioSource = root.AddComponent<AudioSource>();

            GameObject songItemPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/SongItem.prefab");
            GameObject modButtonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/ModButton.prefab");

            SerializedObject so = new SerializedObject(selection);
            so.FindProperty("listContent").objectReferenceValue = listContent;
            so.FindProperty("songItemPrefab").objectReferenceValue = songItemPrefab;
            so.FindProperty("modButtonPrefab").objectReferenceValue = modButtonPrefab;
            
            so.FindProperty("infoPanel").objectReferenceValue = infoPanel;
            so.FindProperty("modPanel").objectReferenceValue = modPanel;
            so.FindProperty("modButtonContainer").objectReferenceValue = modButtonContainer;
            
            so.FindProperty("multiplierText").objectReferenceValue = multiplierObj.GetComponent<TextMeshProUGUI>();
            so.FindProperty("activeModsText").objectReferenceValue = activeModsObj.GetComponent<TextMeshProUGUI>();
            
            so.FindProperty("titleText").objectReferenceValue = titleTextObj.GetComponent<TextMeshProUGUI>();
            so.FindProperty("artistText").objectReferenceValue = artistTextObj.GetComponent<TextMeshProUGUI>();
            so.FindProperty("csText").objectReferenceValue = csTextObj.GetComponent<TextMeshProUGUI>();
            so.FindProperty("arText").objectReferenceValue = arTextObj.GetComponent<TextMeshProUGUI>();
            so.FindProperty("odText").objectReferenceValue = odTextObj.GetComponent<TextMeshProUGUI>();
            so.FindProperty("hpText").objectReferenceValue = hpTextObj.GetComponent<TextMeshProUGUI>();
            so.FindProperty("lengthText").objectReferenceValue = lengthTextObj.GetComponent<TextMeshProUGUI>();
            so.FindProperty("difficultyText").objectReferenceValue = difficultyTextObj.GetComponent<TextMeshProUGUI>();
            so.FindProperty("backgroundImage").objectReferenceValue = backgroundImageObj.GetComponent<RawImage>();
            so.FindProperty("difficultyDropdownContainer").objectReferenceValue = difficultyDropdownContainer;
            
            so.FindProperty("openModButton").objectReferenceValue = toggleModsBtn.GetComponent<Button>();
            so.FindProperty("backMenuButton").objectReferenceValue = backBtn.GetComponent<Button>();
            so.FindProperty("playButton").objectReferenceValue = playBtn.GetComponent<Button>();
            
            so.FindProperty("modStatusText").objectReferenceValue = modStatusObj.GetComponent<TextMeshProUGUI>();
            
            so.FindProperty("scrollUpButton").objectReferenceValue = scrollUpBtn.GetComponent<Button>();
            so.FindProperty("scrollDownButton").objectReferenceValue = scrollDownBtn.GetComponent<Button>();
            
            so.FindProperty("sfxSource").objectReferenceValue = audioSource;
            
            so.ApplyModifiedProperties();

            modPanel.SetActive(false);

            Selection.activeGameObject = root;
            Undo.RegisterCreatedObjectUndo(root, "Create SongSelection");

            if (songItemPrefab == null)
                Debug.LogWarning("[SimpleSceneSetup] 未找到 SongItem 预制体，请先运行 '创建歌曲项预制体'");
            if (modButtonPrefab == null)
                Debug.LogWarning("[SimpleSceneSetup] 未找到 ModButton 预制体，请先运行 '创建 Mod 按钮预制体'");

            Debug.Log("[SimpleSceneSetup] All-in-One 选歌场景配置完成！");
        }

        static Canvas CreateAllInOneCanvas(string name, Transform parent)
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

            canvasObj.AddComponent<GraphicRaycaster>();

            RectTransform rect = canvasObj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(CANVAS_WIDTH, CANVAS_HEIGHT);
            rect.localScale = Vector3.one * CANVAS_SCALE;
            rect.localPosition = new Vector3(0, CANVAS_POS_Y, CANVAS_POS_Z);
            rect.localRotation = Quaternion.identity;

            return canvas;
        }

        static GameObject CreateBackgroundPanel(Transform parent)
        {
            GameObject panel = new GameObject("BackgroundPanel");
            panel.transform.SetParent(parent, false);

            RectTransform rect = panel.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;

            Image bg = panel.AddComponent<Image>();
            bg.color = new Color(0.02f, 0.02f, 0.04f, 0.75f);

            return panel;
        }

        static GameObject CreateLeftArea(Transform parent)
        {
            GameObject leftArea = new GameObject("LeftArea_SongList");
            leftArea.transform.SetParent(parent, false);

            RectTransform rect = leftArea.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(0.35f, 1);
            rect.pivot = new Vector2(0, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            rect.offsetMin = new Vector2(20, 20);
            rect.offsetMax = new Vector2(-10, -20);

            GameObject titleObj = CreateAnchoredText("Title", leftArea.transform, "BEATMAPS", 
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1),
                new Vector2(10, -50), new Vector2(-10, -10), 28, TextAlignmentOptions.Left);
            
            AddLocalizedText(titleObj, "ui_beatmaps");

            GameObject scrollView = CreateScrollViewAnchored("SongScrollView", leftArea.transform);

            return leftArea;
        }

        static GameObject CreateRightTopArea(Transform parent)
        {
            GameObject rightTopArea = new GameObject("RightTopArea_DynamicHub");
            rightTopArea.transform.SetParent(parent, false);

            RectTransform rect = rightTopArea.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.35f, 0.2f);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            rect.offsetMin = new Vector2(10, 10);
            rect.offsetMax = new Vector2(-20, -10);

            return rightTopArea;
        }

        static GameObject CreateRightBottomArea(Transform parent)
        {
            GameObject rightBottomArea = new GameObject("RightBottomArea_ActionBar");
            rightBottomArea.transform.SetParent(parent, false);

            RectTransform rect = rightBottomArea.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.35f, 0);
            rect.anchorMax = new Vector2(1, 0.2f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            rect.offsetMin = new Vector2(10, 20);
            rect.offsetMax = new Vector2(-20, -10);

            GameObject modStatusObj = new GameObject("ModStatusText");
            modStatusObj.transform.SetParent(rightBottomArea.transform, false);
            RectTransform modStatusRect = modStatusObj.AddComponent<RectTransform>();
            modStatusRect.anchorMin = new Vector2(1, 0.5f);
            modStatusRect.anchorMax = new Vector2(1, 0.5f);
            modStatusRect.pivot = new Vector2(1, 0);
            modStatusRect.sizeDelta = new Vector2(400, 30);
            modStatusRect.anchoredPosition = new Vector2(-20, 50);
            TextMeshProUGUI modStatusText = modStatusObj.AddComponent<TextMeshProUGUI>();
            modStatusText.text = "";
            modStatusText.fontSize = 14;
            modStatusText.alignment = TextAlignmentOptions.Right;
            modStatusText.color = new Color(1f, 0.8f, 0.3f);
            modStatusText.fontStyle = FontStyles.Bold;

            GameObject layoutContainer = new GameObject("ButtonContainer");
            layoutContainer.transform.SetParent(rightBottomArea.transform, false);
            RectTransform containerRect = layoutContainer.AddComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(1, 0.5f);
            containerRect.anchorMax = new Vector2(1, 0.5f);
            containerRect.pivot = new Vector2(1, 0.5f);
            containerRect.sizeDelta = new Vector2(700, 80);
            containerRect.anchoredPosition = new Vector2(-20, 0);

            HorizontalLayoutGroup layout = layoutContainer.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 30;
            layout.childAlignment = TextAnchor.MiddleRight;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            GameObject backBtn = CreateActionButton("Back", layoutContainer.transform, 
                new Vector2(180, 60), new Color(1f, 0.267f, 0.267f, 0.8f), 20);
            backBtn.name = "Btn_Back";
            AddLocalizedTextToButton(backBtn, "ui_back");

            GameObject toggleModsBtn = CreateActionButton("MODS", layoutContainer.transform, 
                new Vector2(180, 60), new Color(0.267f, 0.667f, 1f, 0.8f), 20);
            toggleModsBtn.name = "Btn_ToggleMods";

            GameObject playBtn = CreateActionButton("PLAY", layoutContainer.transform, 
                new Vector2(250, 80), new Color(1f, 0.4f, 0.667f, 1f), 28);
            playBtn.name = "Btn_Play";
            AddLocalizedTextToButton(playBtn, "ui_play_button");

            return rightBottomArea;
        }

        static GameObject CreateInfoPanel(Transform parent)
        {
            GameObject infoPanel = new GameObject("InfoPanel");
            infoPanel.transform.SetParent(parent, false);

            RectTransform rect = infoPanel.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;

            Image bg = infoPanel.AddComponent<Image>();
            bg.color = new Color(0.03f, 0.03f, 0.06f, 0.4f);

            GameObject bgImageObj = new GameObject("BackgroundImage");
            bgImageObj.transform.SetParent(infoPanel.transform, false);
            RectTransform bgImageRect = bgImageObj.AddComponent<RectTransform>();
            bgImageRect.anchorMin = new Vector2(0.5f, 0.5f);
            bgImageRect.anchorMax = new Vector2(0.5f, 0.5f);
            bgImageRect.pivot = new Vector2(0.5f, 0.5f);
            bgImageRect.sizeDelta = new Vector2(CANVAS_WIDTH * 0.65f, CANVAS_HEIGHT);
            RawImage bgRawImage = bgImageObj.AddComponent<RawImage>();
            bgRawImage.color = new Color(1f, 1f, 1f, 0.15f);
            AspectRatioFitter aspectFitter = bgImageObj.AddComponent<AspectRatioFitter>();
            aspectFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            aspectFitter.aspectRatio = 16f / 9f;
            bgRawImage.gameObject.SetActive(false);

            GameObject headerBar = new GameObject("HeaderBar");
            headerBar.transform.SetParent(infoPanel.transform, false);
            RectTransform headerRect = headerBar.AddComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0, 1);
            headerRect.anchorMax = new Vector2(1, 1);
            headerRect.pivot = new Vector2(0.5f, 1);
            headerRect.sizeDelta = Vector2.zero;
            headerRect.offsetMin = new Vector2(0, -80);
            headerRect.offsetMax = new Vector2(0, 0);
            Image headerBg = headerBar.AddComponent<Image>();
            headerBg.color = new Color(0.05f, 0.05f, 0.1f, 0.6f);

            GameObject titleObj = CreateAnchoredText("Title", infoPanel.transform, "Select a Beatmap",
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1),
                new Vector2(20, -40), new Vector2(-20, -8), 28, TextAlignmentOptions.Left);

            GameObject artistObj = CreateAnchoredText("Artist", infoPanel.transform, "-",
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1),
                new Vector2(20, -68), new Vector2(-20, -48), 16, TextAlignmentOptions.Left);

            GameObject divider1 = new GameObject("Divider1");
            divider1.transform.SetParent(infoPanel.transform, false);
            RectTransform div1Rect = divider1.AddComponent<RectTransform>();
            div1Rect.anchorMin = new Vector2(0, 1);
            div1Rect.anchorMax = new Vector2(1, 1);
            div1Rect.pivot = new Vector2(0.5f, 1);
            div1Rect.sizeDelta = new Vector2(0, 1);
            div1Rect.anchoredPosition = new Vector2(0, -85);
            Image div1Img = divider1.AddComponent<Image>();
            div1Img.color = new Color(0.2f, 0.2f, 0.3f, 0.5f);

            GameObject statsContainer = new GameObject("StatsContainer");
            statsContainer.transform.SetParent(infoPanel.transform, false);
            RectTransform statsRect = statsContainer.AddComponent<RectTransform>();
            statsRect.anchorMin = new Vector2(0, 1);
            statsRect.anchorMax = new Vector2(1, 1);
            statsRect.pivot = new Vector2(0, 1);
            statsRect.anchoredPosition = Vector2.zero;
            statsRect.sizeDelta = Vector2.zero;
            statsRect.offsetMin = new Vector2(15, -130);
            statsRect.offsetMax = new Vector2(-15, -95);

            HorizontalLayoutGroup statsLayout = statsContainer.AddComponent<HorizontalLayoutGroup>();
            statsLayout.spacing = 6;
            statsLayout.childAlignment = TextAnchor.MiddleCenter;
            statsLayout.childControlWidth = false;
            statsLayout.childControlHeight = false;
            statsLayout.childForceExpandWidth = false;
            statsLayout.childForceExpandHeight = false;

            CreateStatBlock(statsContainer.transform, "CS", "ui_cs");
            CreateStatBlock(statsContainer.transform, "AR", "ui_ar");
            CreateStatBlock(statsContainer.transform, "OD", "ui_od");
            CreateStatBlock(statsContainer.transform, "HP", "ui_hp");
            CreateStatBlock(statsContainer.transform, "Length", null);

            GameObject divider2 = new GameObject("Divider2");
            divider2.transform.SetParent(infoPanel.transform, false);
            RectTransform div2Rect = divider2.AddComponent<RectTransform>();
            div2Rect.anchorMin = new Vector2(0, 1);
            div2Rect.anchorMax = new Vector2(1, 1);
            div2Rect.pivot = new Vector2(0.5f, 1);
            div2Rect.sizeDelta = new Vector2(0, 1);
            div2Rect.anchoredPosition = new Vector2(0, -140);
            Image div2Img = divider2.AddComponent<Image>();
            div2Img.color = new Color(0.2f, 0.2f, 0.3f, 0.5f);

            GameObject difficultySelector = new GameObject("DifficultySelector");
            difficultySelector.transform.SetParent(infoPanel.transform, false);
            RectTransform diffRect = difficultySelector.AddComponent<RectTransform>();
            diffRect.anchorMin = new Vector2(0, 1);
            diffRect.anchorMax = new Vector2(1, 1);
            diffRect.pivot = new Vector2(0.5f, 1);
            diffRect.anchoredPosition = Vector2.zero;
            diffRect.sizeDelta = Vector2.zero;
            diffRect.offsetMin = new Vector2(15, -180);
            diffRect.offsetMax = new Vector2(-15, -150);

            Image diffBg = difficultySelector.AddComponent<Image>();
            diffBg.color = new Color(0.08f, 0.08f, 0.15f, 0.6f);

            Button diffBtn = difficultySelector.AddComponent<Button>();

            GameObject diffTextObj = new GameObject("Text");
            diffTextObj.transform.SetParent(difficultySelector.transform, false);
            RectTransform diffTextRect = diffTextObj.AddComponent<RectTransform>();
            diffTextRect.anchorMin = Vector2.zero;
            diffTextRect.anchorMax = Vector2.one;
            diffTextRect.pivot = new Vector2(0.5f, 0.5f);
            diffTextRect.sizeDelta = Vector2.zero;
            diffTextRect.offsetMin = new Vector2(10, 0);
            diffTextRect.offsetMax = new Vector2(-10, 0);
            TextMeshProUGUI diffText = diffTextObj.AddComponent<TextMeshProUGUI>();
            diffText.text = "[Difficulty Version] ▼";
            diffText.fontSize = 16;
            diffText.alignment = TextAlignmentOptions.MidlineLeft;
            diffText.color = Color.white;

            GameObject diffDropdownContainer = new GameObject("DifficultyDropdownContainer");
            diffDropdownContainer.transform.SetParent(infoPanel.transform, false);
            RectTransform dropdownRect = diffDropdownContainer.AddComponent<RectTransform>();
            dropdownRect.anchorMin = new Vector2(0, 1);
            dropdownRect.anchorMax = new Vector2(1, 1);
            dropdownRect.pivot = new Vector2(0, 1);
            dropdownRect.anchoredPosition = Vector2.zero;
            dropdownRect.sizeDelta = Vector2.zero;
            dropdownRect.offsetMin = new Vector2(15, -220);
            dropdownRect.offsetMax = new Vector2(-15, -185);

            HorizontalLayoutGroup dropdownLayout = diffDropdownContainer.AddComponent<HorizontalLayoutGroup>();
            dropdownLayout.spacing = 6;
            dropdownLayout.childAlignment = TextAnchor.MiddleLeft;
            dropdownLayout.childControlWidth = false;
            dropdownLayout.childControlHeight = false;
            dropdownLayout.childForceExpandWidth = false;
            dropdownLayout.childForceExpandHeight = false;

            return infoPanel;
        }

        static void CreateStatBlock(Transform parent, string label, string localizationKey)
        {
            GameObject statBlock = new GameObject(label);
            statBlock.transform.SetParent(parent, false);

            RectTransform rect = statBlock.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(65, 50);

            VerticalLayoutGroup layout = statBlock.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 2;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(statBlock.transform, false);
            RectTransform labelRect = labelObj.AddComponent<RectTransform>();
            labelRect.sizeDelta = new Vector2(65, 20);
            TextMeshProUGUI labelText = labelObj.AddComponent<TextMeshProUGUI>();
            labelText.text = label;
            labelText.fontSize = 12;
            labelText.alignment = TextAlignmentOptions.Center;
            labelText.color = new Color(0.6f, 0.6f, 0.6f);
            
            if (!string.IsNullOrEmpty(localizationKey))
            {
                AddLocalizedText(labelObj, localizationKey);
            }

            GameObject valueObj = new GameObject("Value");
            valueObj.transform.SetParent(statBlock.transform, false);
            RectTransform valueRect = valueObj.AddComponent<RectTransform>();
            valueRect.sizeDelta = new Vector2(65, 30);
            TextMeshProUGUI valueText = valueObj.AddComponent<TextMeshProUGUI>();
            valueText.text = "-";
            valueText.fontSize = 20;
            valueText.alignment = TextAlignmentOptions.Center;
            valueText.color = Color.white;
        }

        static GameObject CreateModPanel(Transform parent)
        {
            GameObject panel = new GameObject("ModPanel");
            panel.transform.SetParent(parent, false);

            RectTransform rect = panel.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;

            Image bg = panel.AddComponent<Image>();
            bg.color = new Color(0.02f, 0.02f, 0.04f, 0.4f);

            GameObject headerBar = new GameObject("HeaderBar");
            headerBar.transform.SetParent(panel.transform, false);
            RectTransform headerRect = headerBar.AddComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0, 1);
            headerRect.anchorMax = new Vector2(1, 1);
            headerRect.pivot = new Vector2(0.5f, 1);
            headerRect.sizeDelta = Vector2.zero;
            headerRect.offsetMin = new Vector2(0, -70);
            headerRect.offsetMax = new Vector2(0, 0);
            Image headerBg = headerBar.AddComponent<Image>();
            headerBg.color = new Color(0.05f, 0.05f, 0.1f, 0.6f);

            GameObject multiplierObj = CreateAnchoredText("Multiplier", panel.transform, "1.00x",
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(-60, -45), new Vector2(60, -12), 24, TextAlignmentOptions.Center);

            GameObject activeModsObj = CreateAnchoredText("ActiveMods", panel.transform, "No Mod",
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(-100, -62), new Vector2(100, -48), 12, TextAlignmentOptions.Center);
            AddLocalizedText(activeModsObj, "ui_no_mod");

            GameObject divider = new GameObject("Divider");
            divider.transform.SetParent(panel.transform, false);
            RectTransform divRect = divider.AddComponent<RectTransform>();
            divRect.anchorMin = new Vector2(0, 1);
            divRect.anchorMax = new Vector2(1, 1);
            divRect.pivot = new Vector2(0.5f, 1);
            divRect.sizeDelta = new Vector2(0, 1);
            divRect.anchoredPosition = new Vector2(0, -75);
            Image divImg = divider.AddComponent<Image>();
            divImg.color = new Color(0.2f, 0.2f, 0.3f, 0.5f);

            GameObject modContainer = new GameObject("ModContainer");
            modContainer.transform.SetParent(panel.transform, false);
            RectTransform containerRect = modContainer.AddComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0, 0);
            containerRect.anchorMax = new Vector2(1, 1);
            containerRect.pivot = new Vector2(0.5f, 0.5f);
            containerRect.anchoredPosition = Vector2.zero;
            containerRect.sizeDelta = Vector2.zero;
            containerRect.offsetMin = new Vector2(15, 15);
            containerRect.offsetMax = new Vector2(-15, -85);

            GridLayoutGroup grid = modContainer.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(240, 75);
            grid.spacing = new Vector2(8, 6);
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;

            return panel;
        }

        [MenuItem("Project Ether/简单配置/创建歌曲项预制体", false, 20)]
        public static void CreateSongItemPrefab()
        {
            GameObject prefab = new GameObject("SongItem");

            RectTransform rect = prefab.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(450, 70);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;

            Image bg = prefab.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.05f, 0.1f, 0.6f);

            Button btn = prefab.AddComponent<Button>();

            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(prefab.transform, false);
            RectTransform titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 0.55f);
            titleRect.anchorMax = new Vector2(1, 1);
            titleRect.pivot = new Vector2(0, 0.5f);
            titleRect.offsetMin = new Vector2(15, 2);
            titleRect.offsetMax = new Vector2(-60, -2);
            TextMeshProUGUI titleTmp = titleObj.AddComponent<TextMeshProUGUI>();
            titleTmp.text = "Song Title";
            titleTmp.fontSize = 18;
            titleTmp.alignment = TextAlignmentOptions.MidlineLeft;
            titleTmp.color = Color.white;
            titleTmp.overflowMode = TextOverflowModes.Ellipsis;

            GameObject artistObj = new GameObject("Artist");
            artistObj.transform.SetParent(prefab.transform, false);
            RectTransform artistRect = artistObj.AddComponent<RectTransform>();
            artistRect.anchorMin = new Vector2(0, 0);
            artistRect.anchorMax = new Vector2(1, 0.45f);
            artistRect.pivot = new Vector2(0, 0.5f);
            artistRect.offsetMin = new Vector2(15, 2);
            artistRect.offsetMax = new Vector2(-60, -2);
            TextMeshProUGUI artistTmp = artistObj.AddComponent<TextMeshProUGUI>();
            artistTmp.text = "Artist";
            artistTmp.fontSize = 12;
            artistTmp.alignment = TextAlignmentOptions.MidlineLeft;
            artistTmp.color = new Color(0.7f, 0.7f, 0.7f);
            artistTmp.overflowMode = TextOverflowModes.Ellipsis;

            GameObject diffCountObj = new GameObject("DifficultyCount");
            diffCountObj.transform.SetParent(prefab.transform, false);
            RectTransform diffCountRect = diffCountObj.AddComponent<RectTransform>();
            diffCountRect.anchorMin = new Vector2(1, 0);
            diffCountRect.anchorMax = new Vector2(1, 1);
            diffCountRect.pivot = new Vector2(1, 0.5f);
            diffCountRect.offsetMin = new Vector2(-55, 15);
            diffCountRect.offsetMax = new Vector2(-10, -15);
            diffCountRect.sizeDelta = new Vector2(45, 40);
            TextMeshProUGUI diffCountTmp = diffCountObj.AddComponent<TextMeshProUGUI>();
            diffCountTmp.text = "4D";
            diffCountTmp.fontSize = 20;
            diffCountTmp.fontStyle = FontStyles.Bold;
            diffCountTmp.alignment = TextAlignmentOptions.Center;
            diffCountTmp.color = new Color(0.4f, 0.8f, 1f);

            SongItemView view = prefab.AddComponent<SongItemView>();
            SerializedObject so = new SerializedObject(view);
            so.FindProperty("titleText").objectReferenceValue = titleTmp;
            so.FindProperty("artistText").objectReferenceValue = artistTmp;
            so.FindProperty("difficultyCountText").objectReferenceValue = diffCountTmp;
            so.FindProperty("myButton").objectReferenceValue = btn;
            so.FindProperty("backgroundImage").objectReferenceValue = bg;
            so.ApplyModifiedProperties();

            BoxCollider collider = prefab.AddComponent<BoxCollider>();
            collider.size = new Vector3(450, 70, 10);
            collider.center = Vector3.zero;
            collider.isTrigger = true;

            string path = "Assets/Prefabs/SongItem.prefab";
            System.IO.Directory.CreateDirectory("Assets/Prefabs");
            PrefabUtility.SaveAsPrefabAsset(prefab, path);
            DestroyImmediate(prefab);

            AssetDatabase.Refresh();
            Debug.Log($"[SimpleSceneSetup] 歌曲项预制体已创建: {path} (尺寸: 450x70)");
        }

        [MenuItem("Project Ether/简单配置/创建 Mod 按钮预制体", false, 21)]
        public static void CreateModButtonPrefab()
        {
            GameObject prefab = new GameObject("ModButton");

            RectTransform rect = prefab.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(240, 75);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;

            Image bg = prefab.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.08f, 0.12f, 0.9f);

            Button btn = prefab.AddComponent<Button>();

            GameObject shortNameObj = new GameObject("ShortName");
            shortNameObj.transform.SetParent(prefab.transform, false);
            RectTransform shortNameRect = shortNameObj.AddComponent<RectTransform>();
            shortNameRect.anchorMin = new Vector2(0, 0);
            shortNameRect.anchorMax = new Vector2(0, 1);
            shortNameRect.pivot = new Vector2(0, 0.5f);
            shortNameRect.offsetMin = new Vector2(10, 5);
            shortNameRect.offsetMax = new Vector2(55, -5);
            shortNameRect.sizeDelta = new Vector2(45, 65);
            TextMeshProUGUI shortNameText = shortNameObj.AddComponent<TextMeshProUGUI>();
            shortNameText.text = "HR";
            shortNameText.fontSize = 22;
            shortNameText.fontStyle = FontStyles.Bold;
            shortNameText.alignment = TextAlignmentOptions.Center;
            shortNameText.color = Color.white;

            GameObject fullNameObj = new GameObject("FullName");
            fullNameObj.transform.SetParent(prefab.transform, false);
            RectTransform fullNameRect = fullNameObj.AddComponent<RectTransform>();
            fullNameRect.anchorMin = new Vector2(0, 0.6f);
            fullNameRect.anchorMax = new Vector2(1, 1);
            fullNameRect.pivot = new Vector2(0, 0.5f);
            fullNameRect.offsetMin = new Vector2(60, 0);
            fullNameRect.offsetMax = new Vector2(-10, -2);
            TextMeshProUGUI fullNameText = fullNameObj.AddComponent<TextMeshProUGUI>();
            fullNameText.text = "Hard Rock";
            fullNameText.fontSize = 15;
            fullNameText.alignment = TextAlignmentOptions.MidlineLeft;
            fullNameText.color = Color.white;
            fullNameText.overflowMode = TextOverflowModes.Ellipsis;

            GameObject descObj = new GameObject("Description");
            descObj.transform.SetParent(prefab.transform, false);
            RectTransform descRect = descObj.AddComponent<RectTransform>();
            descRect.anchorMin = new Vector2(0, 0);
            descRect.anchorMax = new Vector2(1, 0.55f);
            descRect.pivot = new Vector2(0, 0.5f);
            descRect.offsetMin = new Vector2(60, 3);
            descRect.offsetMax = new Vector2(-10, 0);
            TextMeshProUGUI descriptionText = descObj.AddComponent<TextMeshProUGUI>();
            descriptionText.text = "Everything becomes harder...";
            descriptionText.fontSize = 10;
            descriptionText.alignment = TextAlignmentOptions.TopLeft;
            descriptionText.color = new Color(0.55f, 0.55f, 0.55f);
            descriptionText.overflowMode = TextOverflowModes.Ellipsis;
            descriptionText.enableWordWrapping = true;

            BoxCollider collider = prefab.AddComponent<BoxCollider>();
            collider.size = new Vector3(240, 75, 10);
            collider.center = Vector3.zero;
            collider.isTrigger = true;

            ModButtonController controller = prefab.AddComponent<ModButtonController>();
            SerializedObject so = new SerializedObject(controller);
            so.FindProperty("button").objectReferenceValue = btn;
            so.FindProperty("backgroundImage").objectReferenceValue = bg;
            so.FindProperty("fullNameText").objectReferenceValue = fullNameText;
            so.FindProperty("shortNameText").objectReferenceValue = shortNameText;
            so.FindProperty("descriptionText").objectReferenceValue = descriptionText;
            so.ApplyModifiedProperties();

            string path = "Assets/Prefabs/ModButton.prefab";
            System.IO.Directory.CreateDirectory("Assets/Prefabs");
            PrefabUtility.SaveAsPrefabAsset(prefab, path);
            DestroyImmediate(prefab);

            AssetDatabase.Refresh();
            Debug.Log($"[SimpleSceneSetup] Mod 按钮预制体已创建: {path} (尺寸: 240x75, 两行描述)");
        }

        static Canvas CreateSimpleCanvas(string name, Transform parent, float width, float height)
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

            canvasObj.AddComponent<GraphicRaycaster>();

            RectTransform rect = canvasObj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(width, height);
            rect.localScale = Vector3.one * 0.003f;
            rect.localPosition = new Vector3(0, 2.5f, 2f);

            return canvas;
        }

        static GameObject CreateText(string name, Transform parent, string text, Vector2 position, Vector2 size, float fontSize, TextAlignmentOptions alignment)
        {
            GameObject textObj = new GameObject(name);
            textObj.transform.SetParent(parent, false);

            RectTransform rect = textObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;

            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = alignment;
            tmp.color = Color.white;
            tmp.overflowMode = TextOverflowModes.Overflow;

            return textObj;
        }

        static GameObject CreateAnchoredText(string name, Transform parent, string text,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 offsetMin, Vector2 offsetMax, float fontSize, TextAlignmentOptions alignment)
        {
            GameObject textObj = new GameObject(name);
            textObj.transform.SetParent(parent, false);

            RectTransform rect = textObj.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;

            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = alignment;
            tmp.color = Color.white;
            tmp.overflowMode = TextOverflowModes.Overflow;

            return textObj;
        }

        static GameObject CreateSimpleButton(string name, Transform parent, Color color, Vector2? position = null)
        {
            GameObject btnObj = new GameObject($"Btn_{name}");
            btnObj.transform.SetParent(parent, false);

            RectTransform rect = btnObj.AddComponent<RectTransform>();
            
            if (position.HasValue)
            {
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = position.Value;
                rect.sizeDelta = new Vector2(120, 50);
            }
            else
            {
                rect.anchorMin = new Vector2(0, 0.5f);
                rect.anchorMax = new Vector2(1, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(0, 50);
            }

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

        static GameObject CreateActionButton(string name, Transform parent, Vector2 size, Color color, float fontSize)
        {
            GameObject btnObj = new GameObject();
            btnObj.transform.SetParent(parent, false);

            RectTransform rect = btnObj.AddComponent<RectTransform>();
            rect.sizeDelta = size;

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
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.fontStyle = FontStyles.Bold;

            return btnObj;
        }

        static void AddButtonCollider(GameObject btnObj)
        {
            RectTransform rect = btnObj.GetComponent<RectTransform>();
            if (rect == null) return;

            BoxCollider collider = btnObj.AddComponent<BoxCollider>();
            
            float width = rect.sizeDelta.x > 0 ? rect.sizeDelta.x : 120;
            float height = rect.sizeDelta.y > 0 ? rect.sizeDelta.y : 50;
            
            collider.size = new Vector3(width, height, 10);
            collider.center = Vector3.zero;
            collider.isTrigger = true;
        }

        static GameObject CreateScrollViewAnchored(string name, Transform parent)
        {
            GameObject scrollObj = new GameObject(name);
            scrollObj.transform.SetParent(parent, false);

            RectTransform scrollRectTrans = scrollObj.AddComponent<RectTransform>();
            scrollRectTrans.anchorMin = new Vector2(0, 0);
            scrollRectTrans.anchorMax = new Vector2(1, 1);
            scrollRectTrans.pivot = new Vector2(0.5f, 0.5f);
            scrollRectTrans.anchoredPosition = Vector2.zero;
            scrollRectTrans.sizeDelta = Vector2.zero;
            scrollRectTrans.offsetMin = new Vector2(0, 60);
            scrollRectTrans.offsetMax = new Vector2(0, 0);

            Image scrollBg = scrollObj.AddComponent<Image>();
            scrollBg.color = new Color(0.03f, 0.03f, 0.06f, 0.9f);

            ScrollRect scroll = scrollObj.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;

            GameObject viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollObj.transform, false);
            RectTransform viewportRect = viewport.AddComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.pivot = new Vector2(0.5f, 0.5f);
            viewportRect.sizeDelta = Vector2.zero;
            viewportRect.anchoredPosition = Vector2.zero;

            Image viewportMask = viewport.AddComponent<Image>();
            viewportMask.color = Color.white;
            Mask mask = viewport.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            GameObject content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            RectTransform contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0, 0);

            VerticalLayoutGroup layout = content.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 4;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.padding = new RectOffset(10, 10, 10, 10);

            ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewportRect;
            scroll.content = contentRect;

            GameObject scrollButtonContainer = new GameObject("ScrollButtons");
            scrollButtonContainer.transform.SetParent(parent, false);
            RectTransform btnContainerRect = scrollButtonContainer.AddComponent<RectTransform>();
            btnContainerRect.anchorMin = new Vector2(0, 0);
            btnContainerRect.anchorMax = new Vector2(1, 0);
            btnContainerRect.pivot = new Vector2(0.5f, 0);
            btnContainerRect.sizeDelta = new Vector2(0, 55);
            btnContainerRect.anchoredPosition = Vector2.zero;

            Image btnContainerBg = scrollButtonContainer.AddComponent<Image>();
            btnContainerBg.color = new Color(0.05f, 0.05f, 0.1f, 0.8f);

            HorizontalLayoutGroup btnLayout = scrollButtonContainer.AddComponent<HorizontalLayoutGroup>();
            btnLayout.spacing = 20;
            btnLayout.childAlignment = TextAnchor.MiddleCenter;
            btnLayout.childControlWidth = false;
            btnLayout.childControlHeight = false;
            btnLayout.childForceExpandWidth = false;
            btnLayout.childForceExpandHeight = false;

            GameObject scrollUpBtn = CreateScrollButton("Btn_ScrollUp", scrollButtonContainer.transform, "▲ UP");
            GameObject scrollDownBtn = CreateScrollButton("Btn_ScrollDown", scrollButtonContainer.transform, "▼ DOWN");

            return scrollObj;
        }

        static GameObject CreateScrollButton(string name, Transform parent, string label)
        {
            GameObject btn = new GameObject(name);
            btn.transform.SetParent(parent, false);

            RectTransform rect = btn.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(120, 40);

            Image bg = btn.AddComponent<Image>();
            bg.color = new Color(0.15f, 0.15f, 0.25f, 0.9f);

            Button button = btn.AddComponent<Button>();

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(btn.transform, false);
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 16;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(0.6f, 0.8f, 1f);

            BoxCollider collider = btn.AddComponent<BoxCollider>();
            collider.size = new Vector3(120, 40, 10);
            collider.center = Vector3.zero;
            collider.isTrigger = true;

            return btn;
        }

        static void AddLocalizedText(GameObject textObj, string key)
        {
            if (textObj == null || string.IsNullOrEmpty(key)) return;
            
            var localizedText = textObj.AddComponent<LocalizedText>();
            SerializedObject so = new SerializedObject(localizedText);
            so.FindProperty("localizationKey").stringValue = key;
            so.ApplyModifiedProperties();
        }

        static void AddLocalizedTextToButton(GameObject btnObj, string key)
        {
            if (btnObj == null || string.IsNullOrEmpty(key)) return;
            
            Transform textTransform = btnObj.transform.Find("Text");
            if (textTransform != null)
            {
                AddLocalizedText(textTransform.gameObject, key);
            }
        }
    }
}
#endif
