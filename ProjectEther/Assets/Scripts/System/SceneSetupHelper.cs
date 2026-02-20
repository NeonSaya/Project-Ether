using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace OsuVR
{
    /// <summary>
    /// 场景设置辅助类
    /// 提供场景初始化和快速配置功能
    /// </summary>
    public static class SceneSetupHelper
    {
        public static void CreateMainMenuScene()
        {
            GameObject root = new GameObject("[MainMenu]");
            root.AddComponent<MainMenuController>();

            GameObject canvas = CreateWorldSpaceCanvas("[MenuCanvas]", 4f);
            canvas.transform.SetParent(root.transform);

            Debug.Log("[SceneSetup] 主菜单场景已创建，请保存为 MainMenuScene");
        }

        public static void CreateSongSelectScene()
        {
            GameObject root = new GameObject("[SongSelection]");
            root.AddComponent<SongSelectionController>();

            GameObject canvas = CreateWorldSpaceCanvas("[SongSelectCanvas]", 4f);
            canvas.transform.SetParent(root.transform);

            GameObject modPanel = new GameObject("ModPanel");
            modPanel.AddComponent<ModPanelController>();
            modPanel.transform.SetParent(canvas.transform);
            modPanel.SetActive(false);

            Debug.Log("[SceneSetup] 选歌场景已创建，请保存为 SongSelectScene");
        }

        public static void CreateSettingsScene()
        {
            GameObject root = new GameObject("[Settings]");
            root.AddComponent<SettingsController>();

            GameObject canvas = CreateWorldSpaceCanvas("[SettingsCanvas]", 3f);
            canvas.transform.SetParent(root.transform);

            Debug.Log("[SceneSetup] 设置场景已创建");
        }

        public static GameObject CreateWorldSpaceCanvas(string name, float distance)
        {
            GameObject canvasObj = new GameObject(name);

            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 100;

            CanvasGroup group = canvasObj.AddComponent<CanvasGroup>();
            group.interactable = true;
            group.blocksRaycasts = true;

            RectTransform rect = canvasObj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(2f, 1.5f);
            rect.localScale = Vector3.one;

            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                rect.position = mainCam.transform.position + mainCam.transform.forward * distance;
                rect.rotation = Quaternion.LookRotation(mainCam.transform.forward, Vector3.up);
            }

            return canvasObj;
        }

        public static GameObject CreateVRButton(string name, string text, Transform parent)
        {
            GameObject btnObj = new GameObject(name);
            btnObj.transform.SetParent(parent);
            btnObj.transform.localScale = new Vector3(0.3f, 0.1f, 0.01f);

            var bg = btnObj.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.1f, 0.15f, 0.9f);

            var btn = btnObj.AddComponent<Button>();

            var textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform);
            textObj.transform.localPosition = Vector3.zero;
            textObj.transform.localScale = Vector3.one;

            var tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 0.05f;
            tmp.color = Color.white;

            return btnObj;
        }
    }
}
