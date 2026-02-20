#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;
using UnityEngine.EventSystems;

namespace OsuVR.Editor
{
    public class SimpleSceneSetup : EditorWindow
    {
        const float CANVAS_SCALE = 0.003f;
        const float CANVAS_POS_Y = 2.5f;
        const float CANVAS_POS_Z = 2f;

        [MenuItem("Project Ether/简单配置/主菜单场景", false, 1)]
        public static void SetupMainMenu()
        {
            GameObject root = new GameObject("[MainMenu]");
            SimpleMainMenu menu = root.AddComponent<SimpleMainMenu>();

            Canvas canvas = CreateSimpleCanvas("MenuCanvas", root.transform);

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
            GameObject settingsBtn = CreateSimpleButton("Settings", buttonContainer.transform, new Color(0.5f, 0.5f, 0.5f));
            GameObject creditsBtn = CreateSimpleButton("Credits", buttonContainer.transform, new Color(0.4f, 0.5f, 0.6f));
            GameObject quitBtn = CreateSimpleButton("Quit", buttonContainer.transform, new Color(0.8f, 0.3f, 0.3f));

            AddButtonCollider(playBtn);
            AddButtonCollider(settingsBtn);
            AddButtonCollider(creditsBtn);
            AddButtonCollider(quitBtn);

            GameObject versionObj = CreateText("Version", canvas.transform, "Demo v0.1", 
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

            Canvas canvas = CreateSimpleCanvas("SongSelectCanvas", root.transform);

            GameObject titleObj = CreateText("Title", canvas.transform, "Select Song", 
                new Vector2(0, 200), new Vector2(400, 50), 28, TextAlignmentOptions.Center);

            GameObject scrollView = CreateScrollView("SongScrollView", canvas.transform, new Vector2(0, 30), new Vector2(500, 280));
            Transform listContent = scrollView.transform.Find("Viewport/Content");

            GameObject modBtn = CreateSimpleButton("Mods", canvas.transform, new Color(0.3f, 0.5f, 0.8f), new Vector2(200, -150));
            GameObject backBtn = CreateSimpleButton("Back", canvas.transform, new Color(0.5f, 0.3f, 0.3f), new Vector2(-200, -150));

            AddButtonCollider(modBtn);
            AddButtonCollider(backBtn);

            GameObject modPanel = CreateModPanel(canvas.transform);
            Transform modButtonContainer = modPanel.transform.Find("ModContainer");

            GameObject multiplierObj = CreateText("Multiplier", modPanel.transform, "1.00x", 
                new Vector2(0, 120), new Vector2(150, 40), 20, TextAlignmentOptions.Center);
            GameObject activeModsObj = CreateText("ActiveMods", modPanel.transform, "No Mod", 
                new Vector2(0, 80), new Vector2(200, 30), 16, TextAlignmentOptions.Center);

            AudioSource audioSource = root.AddComponent<AudioSource>();

            GameObject songItemPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/SongItem.prefab");
            GameObject modButtonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/ModButton.prefab");

            SerializedObject so = new SerializedObject(selection);
            so.FindProperty("listContent").objectReferenceValue = listContent;
            so.FindProperty("songItemPrefab").objectReferenceValue = songItemPrefab;
            so.FindProperty("modPanel").objectReferenceValue = modPanel;
            so.FindProperty("modButtonContainer").objectReferenceValue = modButtonContainer;
            so.FindProperty("modButtonPrefab").objectReferenceValue = modButtonPrefab;
            so.FindProperty("multiplierText").objectReferenceValue = multiplierObj.GetComponent<TextMeshProUGUI>();
            so.FindProperty("activeModsText").objectReferenceValue = activeModsObj.GetComponent<TextMeshProUGUI>();
            so.FindProperty("sfxSource").objectReferenceValue = audioSource;

            so.FindProperty("openModButton").objectReferenceValue = modBtn.GetComponent<Button>();
            so.FindProperty("backMenuButton").objectReferenceValue = backBtn.GetComponent<Button>();

            Button closeBtnComponent = modPanel.transform.Find("CloseButton").GetComponent<Button>();
            so.FindProperty("closeModButton").objectReferenceValue = closeBtnComponent;

            so.ApplyModifiedProperties();

            modPanel.SetActive(false);

            Selection.activeGameObject = root;
            Undo.RegisterCreatedObjectUndo(root, "Create SongSelection");

            if (songItemPrefab == null)
                Debug.LogWarning("[SimpleSceneSetup] 未找到 SongItem 预制体，请先运行 '创建歌曲项预制体'");
            if (modButtonPrefab == null)
                Debug.LogWarning("[SimpleSceneSetup] 未找到 ModButton 预制体，请先运行 '创建 Mod 按钮预制体'");

            Debug.Log("[SimpleSceneSetup] 选歌场景配置完成！");
        }

        [MenuItem("Project Ether/简单配置/创建歌曲项预制体", false, 20)]
        public static void CreateSongItemPrefab()
        {
            GameObject prefab = new GameObject("SongItem");

            RectTransform rect = prefab.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(450, 80);

            Image bg = prefab.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.1f, 0.15f, 0.9f);

            Button btn = prefab.AddComponent<Button>();

            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(prefab.transform);
            RectTransform titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 0.5f);
            titleRect.anchorMax = new Vector2(1, 1);
            titleRect.offsetMin = new Vector2(15, 0);
            titleRect.offsetMax = new Vector2(-100, -5);
            TextMeshProUGUI titleTmp = titleObj.AddComponent<TextMeshProUGUI>();
            titleTmp.text = "Song Title";
            titleTmp.fontSize = 20;
            titleTmp.alignment = TextAlignmentOptions.BottomLeft;
            titleTmp.color = Color.white;

            GameObject artistObj = new GameObject("Artist");
            artistObj.transform.SetParent(prefab.transform);
            RectTransform artistRect = artistObj.AddComponent<RectTransform>();
            artistRect.anchorMin = new Vector2(0, 0);
            artistRect.anchorMax = new Vector2(1, 0.5f);
            artistRect.offsetMin = new Vector2(15, 5);
            artistRect.offsetMax = new Vector2(-100, -5);
            TextMeshProUGUI artistTmp = artistObj.AddComponent<TextMeshProUGUI>();
            artistTmp.text = "Artist";
            artistTmp.fontSize = 14;
            artistTmp.alignment = TextAlignmentOptions.TopLeft;
            artistTmp.color = new Color(0.7f, 0.7f, 0.7f);

            GameObject versionObj = new GameObject("Version");
            versionObj.transform.SetParent(prefab.transform);
            RectTransform versionRect = versionObj.AddComponent<RectTransform>();
            versionRect.anchorMin = new Vector2(1, 0);
            versionRect.anchorMax = new Vector2(1, 1);
            versionRect.offsetMin = new Vector2(-90, 5);
            versionRect.offsetMax = new Vector2(-10, -5);
            TextMeshProUGUI versionTmp = versionObj.AddComponent<TextMeshProUGUI>();
            versionTmp.text = "[Hard]";
            versionTmp.fontSize = 14;
            versionTmp.alignment = TextAlignmentOptions.Right;
            versionTmp.color = new Color(1f, 0.7f, 0.2f);

            SongItemView view = prefab.AddComponent<SongItemView>();
            SerializedObject so = new SerializedObject(view);
            so.FindProperty("titleText").objectReferenceValue = titleTmp;
            so.FindProperty("artistText").objectReferenceValue = artistTmp;
            so.FindProperty("versionText").objectReferenceValue = versionTmp;
            so.FindProperty("myButton").objectReferenceValue = btn;
            so.ApplyModifiedProperties();

            BoxCollider collider = prefab.AddComponent<BoxCollider>();
            collider.size = new Vector3(450, 80, 10);
            collider.center = Vector3.zero;
            collider.isTrigger = true;

            string path = "Assets/Prefabs/SongItem.prefab";
            System.IO.Directory.CreateDirectory("Assets/Prefabs");
            PrefabUtility.SaveAsPrefabAsset(prefab, path);
            DestroyImmediate(prefab);

            AssetDatabase.Refresh();
            Debug.Log($"[SimpleSceneSetup] 歌曲项预制体已创建: {path}");
        }

        [MenuItem("Project Ether/简单配置/创建 Mod 按钮预制体", false, 21)]
        public static void CreateModButtonPrefab()
        {
            GameObject prefab = new GameObject("ModButton");

            RectTransform rect = prefab.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(60, 60);

            Image bg = prefab.AddComponent<Image>();
            bg.color = new Color(0.15f, 0.15f, 0.2f);

            Button btn = prefab.AddComponent<Button>();

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(prefab.transform);
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = "HR";
            tmp.fontSize = 18;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;

            BoxCollider collider = prefab.AddComponent<BoxCollider>();
            collider.size = new Vector3(60, 60, 10);
            collider.center = Vector3.zero;
            collider.isTrigger = true;

            string path = "Assets/Prefabs/ModButton.prefab";
            System.IO.Directory.CreateDirectory("Assets/Prefabs");
            PrefabUtility.SaveAsPrefabAsset(prefab, path);
            DestroyImmediate(prefab);

            AssetDatabase.Refresh();
            Debug.Log($"[SimpleSceneSetup] Mod 按钮预制体已创建: {path}");
        }

        static Canvas CreateSimpleCanvas(string name, Transform parent)
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
            rect.sizeDelta = new Vector2(600, 450);
            rect.localScale = Vector3.one * CANVAS_SCALE;
            rect.localPosition = new Vector3(0, CANVAS_POS_Y, CANVAS_POS_Z);

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

        static void AddButtonCollider(GameObject btnObj)
        {
            RectTransform rect = btnObj.GetComponent<RectTransform>();
            if (rect == null) return;

            BoxCollider collider = btnObj.AddComponent<BoxCollider>();
            
            float width = 200;
            float height = 50;
            
            if (rect.sizeDelta.x > 0) width = rect.sizeDelta.x;
            if (rect.sizeDelta.y > 0) height = rect.sizeDelta.y;
            
            collider.size = new Vector3(width, height, 10);
            collider.center = Vector3.zero;
            collider.isTrigger = true;
        }

        static GameObject CreateScrollView(string name, Transform parent, Vector2 position, Vector2 size)
        {
            GameObject scrollObj = new GameObject(name);
            scrollObj.transform.SetParent(parent, false);

            RectTransform scrollRectTrans = scrollObj.AddComponent<RectTransform>();
            scrollRectTrans.anchorMin = new Vector2(0.5f, 0.5f);
            scrollRectTrans.anchorMax = new Vector2(0.5f, 0.5f);
            scrollRectTrans.pivot = new Vector2(0.5f, 0.5f);
            scrollRectTrans.sizeDelta = size;
            scrollRectTrans.anchoredPosition = position;

            Image scrollBg = scrollObj.AddComponent<Image>();
            scrollBg.color = new Color(0.05f, 0.05f, 0.1f, 0.9f);

            ScrollRect scroll = scrollObj.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;

            GameObject viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollObj.transform, false);
            RectTransform viewportRect = viewport.AddComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.sizeDelta = Vector2.zero;

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
            contentRect.sizeDelta = new Vector2(0, 0);

            VerticalLayoutGroup layout = content.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 5;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.padding = new RectOffset(10, 10, 10, 10);

            ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewportRect;
            scroll.content = contentRect;

            return scrollObj;
        }

        static GameObject CreateModPanel(Transform parent)
        {
            GameObject panel = new GameObject("ModPanel");
            panel.transform.SetParent(parent, false);

            RectTransform rect = panel.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(400, 300);
            rect.anchoredPosition = new Vector2(150, 0);

            Image bg = panel.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.08f, 0.12f, 0.95f);

            GameObject titleObj = CreateText("Title", panel.transform, "Mod Selection", 
                new Vector2(0, 120), new Vector2(300, 40), 20, TextAlignmentOptions.Center);

            GameObject container = new GameObject("ModContainer");
            container.transform.SetParent(panel.transform, false);
            RectTransform containerRect = container.AddComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0, 0);
            containerRect.anchorMax = new Vector2(1, 1);
            containerRect.offsetMin = new Vector2(20, 70);
            containerRect.offsetMax = new Vector2(-20, -80);

            GridLayoutGroup grid = container.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(60, 60);
            grid.spacing = new Vector2(10, 10);
            grid.childAlignment = TextAnchor.MiddleCenter;

            GameObject closeBtn = CreateSimpleButton("Close", panel.transform, new Color(0.5f, 0.3f, 0.3f), new Vector2(0, -120));
            closeBtn.name = "CloseButton";
            closeBtn.GetComponentInChildren<TextMeshProUGUI>().text = "Close";
            AddButtonCollider(closeBtn);

            return panel;
        }
    }
}
#endif
