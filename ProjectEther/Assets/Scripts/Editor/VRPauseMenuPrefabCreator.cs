#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using OsuVR;

namespace OsuVR.Editor
{
    public class VRPauseMenuPrefabCreator : EditorWindow
    {
        [MenuItem("Project Ether/工具/创建VR暂停菜单Prefab")]
        public static void ShowWindow()
        {
            GetWindow<VRPauseMenuPrefabCreator>("VR暂停菜单创建器");
        }

        private string prefabName = "VRPauseMenu";
        private string savePath = "Assets/Prefabs/UI";

        void OnGUI()
        {
            GUILayout.Label("VR暂停菜单Prefab创建器", EditorStyles.boldLabel);
            
            EditorGUILayout.Space();
            
            prefabName = EditorGUILayout.TextField("Prefab名称:", prefabName);
            savePath = EditorGUILayout.TextField("保存路径:", savePath);
            
            EditorGUILayout.Space();
            
            if (GUILayout.Button("创建VR暂停菜单Prefab"))
            {
                CreateVRPauseMenuPrefab();
            }
        }

        void CreateVRPauseMenuPrefab()
        {
            // 确保路径存在
            string fullPath = System.IO.Path.Combine(Application.dataPath, savePath.Replace("Assets/", ""));
            if (!System.IO.Directory.Exists(fullPath))
            {
                System.IO.Directory.CreateDirectory(fullPath);
            }

            // 创建根对象
            GameObject pauseMenuObj = new GameObject(prefabName);
            
            // 设置Layer为UI
            pauseMenuObj.layer = LayerMask.NameToLayer("UI");
            
            // 添加必要组件
            Canvas canvas = pauseMenuObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 100;
            canvas.gameObject.layer = LayerMask.NameToLayer("UI");
            
            CanvasScaler scaler = pauseMenuObj.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 10;
            
            pauseMenuObj.AddComponent<GraphicRaycaster>();
            
            CanvasGroup canvasGroup = pauseMenuObj.AddComponent<CanvasGroup>();
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
            
            // 设置Canvas的scale和位置
            RectTransform rootRect = pauseMenuObj.GetComponent<RectTransform>();
            rootRect.localScale = Vector3.one * 0.002f;
            rootRect.position = new Vector3(0f, 2.2f, 1.5f);
            
            VRPauseMenu vrPauseMenu = pauseMenuObj.AddComponent<VRPauseMenu>();
            vrPauseMenu.fixedPosition = new Vector3(0f, 2.2f, 1.5f);
            
            // 创建UI结构
            CreateUIStructure(pauseMenuObj, vrPauseMenu);
            
            // 设置所有子对象的Layer
            SetLayerRecursively(pauseMenuObj, LayerMask.NameToLayer("UI"));
            
            // 创建Prefab
            string assetPath = $"{savePath}/{prefabName}.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(pauseMenuObj, assetPath);
            
            if (prefab != null)
            {
                Debug.Log($"[VRPauseMenu] 成功创建Prefab: {assetPath}");
                Selection.activeObject = prefab;
                
                // 清理场景中的临时对象 - 使用延迟销毁避免TMP字体资源问题
                DestroyTemporaryObject(pauseMenuObj);
            }
            else
            {
                Debug.LogError("[VRPauseMenu] 创建Prefab失败");
            }
        }

        void DestroyTemporaryObject(GameObject obj)
        {
            // 先禁用所有TMP组件，避免字体资源引用问题
            var tmpComponents = obj.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var tmp in tmpComponents)
            {
                tmp.text = string.Empty;
            }
            
            // 使用Destroy而不是DestroyImmediate，让Unity在下一帧安全清理
            Object.DestroyImmediate(obj);
        }

        void CreateUIStructure(GameObject root, VRPauseMenu pauseMenu)
        {
            // 创建主容器 - 尺寸适配0.002 scale
            GameObject mainContainer = new GameObject("PauseMenu_Container");
            mainContainer.transform.SetParent(root.transform, false);
            RectTransform containerRect = mainContainer.AddComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0.5f, 0.5f);
            containerRect.anchorMax = new Vector2(0.5f, 0.5f);
            containerRect.pivot = new Vector2(0.5f, 0.5f);
            containerRect.sizeDelta = new Vector2(400, 300);
            containerRect.anchoredPosition = Vector2.zero;

            // 添加背景面板以支持射线检测
            Image bgImage = mainContainer.AddComponent<Image>();
            bgImage.color = new Color(0.05f, 0.05f, 0.1f, 0.9f);
            bgImage.raycastTarget = true;

            // 创建标题
            GameObject titleObj = CreateText("Title", mainContainer.transform, "PAUSED", 
                new Vector2(0, 100), new Vector2(300, 60), 36, TextAlignmentOptions.Center);
            
            // 创建按钮容器
            GameObject buttonContainer = new GameObject("Buttons");
            buttonContainer.transform.SetParent(mainContainer.transform, false);
            RectTransform buttonContainerRect = buttonContainer.AddComponent<RectTransform>();
            buttonContainerRect.anchorMin = new Vector2(0.5f, 0.5f);
            buttonContainerRect.anchorMax = new Vector2(0.5f, 0.5f);
            buttonContainerRect.pivot = new Vector2(0.5f, 0.5f);
            buttonContainerRect.sizeDelta = new Vector2(280, 200);
            buttonContainerRect.anchoredPosition = new Vector2(0, -20);

            VerticalLayoutGroup layout = buttonContainer.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 15;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            // 添加Content Size Fitter确保按钮容器大小正确
            ContentSizeFitter fitter = buttonContainer.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // 创建按钮
            pauseMenu.continueButton = CreateButton("Continue", buttonContainer.transform);
            pauseMenu.retryButton = CreateButton("Retry", buttonContainer.transform);
            pauseMenu.backToMenuButton = CreateButton("Back to Menu", buttonContainer.transform);

            // 创建倒计时面板
            pauseMenu.countdownPanel = new GameObject("CountdownPanel");
            pauseMenu.countdownPanel.transform.SetParent(mainContainer.transform, false);
            pauseMenu.countdownPanel.SetActive(false);
            
            RectTransform countdownRect = pauseMenu.countdownPanel.AddComponent<RectTransform>();
            countdownRect.anchorMin = new Vector2(0.5f, 0.5f);
            countdownRect.anchorMax = new Vector2(0.5f, 0.5f);
            countdownRect.pivot = new Vector2(0.5f, 0.5f);
            countdownRect.sizeDelta = new Vector2(200, 200);
            countdownRect.anchoredPosition = Vector2.zero;

            GameObject countdownTextObj = CreateText("CountdownText", pauseMenu.countdownPanel.transform, "3", 
                Vector2.zero, new Vector2(200, 200), 100, TextAlignmentOptions.Center);
            pauseMenu.countdownText = countdownTextObj.GetComponent<TextMeshProUGUI>();
            pauseMenu.countdownText.fontSize = 100;
            pauseMenu.countdownText.alignment = TextAlignmentOptions.Center;
        }

        GameObject CreateText(string name, Transform parent, string text, Vector2 anchoredPosition, Vector2 sizeDelta, float fontSize, TextAlignmentOptions alignment)
        {
            GameObject textObj = new GameObject(name);
            textObj.transform.SetParent(parent, false);

            TextMeshProUGUI tmpText = textObj.AddComponent<TextMeshProUGUI>();
            tmpText.text = text;
            tmpText.fontSize = fontSize;
            tmpText.alignment = alignment;
            tmpText.color = Color.white;
            tmpText.enableWordWrapping = false;
            tmpText.raycastTarget = false;

            RectTransform rect = tmpText.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = sizeDelta;
            rect.anchoredPosition = anchoredPosition;

            return textObj;
        }

        Button CreateButton(string buttonText, Transform parent)
        {
            GameObject buttonObj = new GameObject(buttonText.Replace(" ", "_") + "_Button");
            buttonObj.transform.SetParent(parent, false);

            Button button = buttonObj.AddComponent<Button>();
            Navigation nav = new Navigation { mode = Navigation.Mode.None };
            button.navigation = nav;
            
            // 设置按钮视觉 - 确保raycastTarget为true
            Image image = buttonObj.AddComponent<Image>();
            image.color = new Color(0.2f, 0.6f, 1f, 1f);
            image.raycastTarget = true;
            
            // 添加布局元素控制按钮大小
            LayoutElement layoutElement = buttonObj.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = 220;
            layoutElement.preferredHeight = 50;
            
            // 添加文本
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(buttonObj.transform, false);
            
            TextMeshProUGUI tmpText = textObj.AddComponent<TextMeshProUGUI>();
            tmpText.text = buttonText;
            tmpText.fontSize = 24;
            tmpText.alignment = TextAlignmentOptions.Center;
            tmpText.color = Color.white;
            tmpText.raycastTarget = false;
            
            RectTransform textRect = tmpText.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.sizeDelta = Vector2.zero;
            textRect.anchoredPosition = Vector2.zero;

            // 设置按钮RectTransform
            RectTransform buttonRect = buttonObj.GetComponent<RectTransform>();
            buttonRect.sizeDelta = new Vector2(220, 50);

            return button;
        }

        void SetLayerRecursively(GameObject obj, int layer)
        {
            obj.layer = layer;
            foreach (Transform child in obj.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }
    }
}
#endif