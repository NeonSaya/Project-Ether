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
            
            // 添加必要组件
            Canvas canvas = pauseMenuObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 100;
            
            pauseMenuObj.AddComponent<CanvasScaler>();
            pauseMenuObj.AddComponent<GraphicRaycaster>();
            pauseMenuObj.AddComponent<CanvasGroup>();
            
            VRPauseMenu vrPauseMenu = pauseMenuObj.AddComponent<VRPauseMenu>();
            
            // 创建UI结构
            CreateUIStructure(pauseMenuObj, vrPauseMenu);
            
            // 创建Prefab
            string assetPath = $"{savePath}/{prefabName}.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(pauseMenuObj, assetPath);
            
            if (prefab != null)
            {
                Debug.Log($"[VRPauseMenu] 成功创建Prefab: {assetPath}");
                Selection.activeObject = prefab;
                
                // 清理场景中的临时对象
                DestroyImmediate(pauseMenuObj);
            }
            else
            {
                Debug.LogError("[VRPauseMenu] 创建Prefab失败");
            }
        }

        void CreateUIStructure(GameObject root, VRPauseMenu pauseMenu)
        {
            // 创建主容器
            GameObject mainContainer = new GameObject("PauseMenu_Container");
            mainContainer.transform.SetParent(root.transform, false);
            RectTransform containerRect = mainContainer.AddComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0.5f, 0.5f);
            containerRect.anchorMax = new Vector2(0.5f, 0.5f);
            containerRect.pivot = new Vector2(0.5f, 0.5f);
            containerRect.sizeDelta = new Vector2(600, 450);
            containerRect.anchoredPosition = Vector2.zero;

            // 创建标题
            GameObject titleObj = CreateText("Title", mainContainer.transform, "PAUSED", 
                new Vector2(0, 120), new Vector2(500, 70), 32, TextAlignmentOptions.Center);
            
            // 创建按钮容器
            GameObject buttonContainer = new GameObject("Buttons");
            buttonContainer.transform.SetParent(mainContainer.transform, false);
            RectTransform buttonContainerRect = buttonContainer.AddComponent<RectTransform>();
            buttonContainerRect.anchorMin = new Vector2(0.5f, 0.5f);
            buttonContainerRect.anchorMax = new Vector2(0.5f, 0.5f);
            buttonContainerRect.pivot = new Vector2(0.5f, 1f);
            buttonContainerRect.sizeDelta = new Vector2(220, 200);
            buttonContainerRect.anchoredPosition = new Vector2(0, 20);

            VerticalLayoutGroup layout = buttonContainer.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 12;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

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
                Vector2.zero, new Vector2(200, 200), 120, TextAlignmentOptions.Center);
            pauseMenu.countdownText = countdownTextObj.GetComponent<TextMeshProUGUI>();
            pauseMenu.countdownText.fontSize = 120;
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
            
            // 设置按钮视觉
            Image image = buttonObj.AddComponent<Image>();
            image.color = new Color(0.2f, 0.6f, 1f, 1f); // 蓝色主题
            
            // 添加文本
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(buttonObj.transform, false);
            
            TextMeshProUGUI tmpText = textObj.AddComponent<TextMeshProUGUI>();
            tmpText.text = buttonText;
            tmpText.fontSize = 20;
            tmpText.alignment = TextAlignmentOptions.Center;
            tmpText.color = Color.white;
            
            RectTransform textRect = tmpText.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.sizeDelta = Vector2.zero;
            textRect.anchoredPosition = Vector2.zero;

            // 设置按钮RectTransform
            RectTransform buttonRect = buttonObj.GetComponent<RectTransform>();
            buttonRect.sizeDelta = new Vector2(200, 50);

            return button;
        }
    }
}
#endif