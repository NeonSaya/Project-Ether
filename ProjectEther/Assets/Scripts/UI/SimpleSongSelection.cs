using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using UnityEngine.XR; // ✅ 必须加上这个引用，用于读取 VR 摇杆


namespace OsuVR
{
    /// <summary>
    /// 简化版选歌菜单控制器
    /// 直接继承原有 SongSelectionMenu 的逻辑
    /// </summary>
    public class SimpleSongSelection : MonoBehaviour
    {
        [Header("UI 引用")]
        public Transform listContent;
        public GameObject songItemPrefab;

        [Header("Mod 面板")]
        public GameObject modPanel;
        public Transform modButtonContainer;
        public GameObject modButtonPrefab;
        public TextMeshProUGUI multiplierText;
        public TextMeshProUGUI activeModsText;

        [Header("场景配置")]
        public string gameSceneName = "GameScene";

        [Header("按钮引用")]
        public Button openModButton;
        public Button backMenuButton;
        public Button closeModButton;

        [Header("音效")]
        public AudioSource sfxSource;
        public AudioClip selectSound;

        private List<BeatmapMetadata> songList = new List<BeatmapMetadata>();
        private BeatmapMetadata selectedSong;

        void Start()
        {
            Debug.Log($"[SimpleSongSelection] 歌曲文件夹: {BeatmapImporter.SongsDirectory}");
            BeatmapImporter.ImportNewOszFiles();
            RefreshSongList();
            GenerateModButtons();

            if (modPanel != null) modPanel.SetActive(false);

            // ✅ 终极防呆：如果你忘了重新生成场景或者拖拽引用，代码会自己去找按钮并强行绑定！
            if (openModButton == null)
            {
                GameObject btnObj = GameObject.Find("Btn_Mods");
                if (btnObj != null) openModButton = btnObj.GetComponent<Button>();
            }
            if (backMenuButton == null)
            {
                GameObject btnObj = GameObject.Find("Btn_Back");
                if (btnObj != null) backMenuButton = btnObj.GetComponent<Button>();
            }
            if (closeModButton == null)
            {
                GameObject btnObj = GameObject.Find("CloseButton");
                if (btnObj != null) closeModButton = btnObj.GetComponent<Button>();
            }

            // 清除旧事件并重新绑定
            if (openModButton != null) { openModButton.onClick.RemoveAllListeners(); openModButton.onClick.AddListener(ToggleModPanel); }
            if (backMenuButton != null) { backMenuButton.onClick.RemoveAllListeners(); backMenuButton.onClick.AddListener(GoBack); }
            if (closeModButton != null) { closeModButton.onClick.RemoveAllListeners(); closeModButton.onClick.AddListener(ToggleModPanel); }
        }
        void Update()
        {
            // 获取所有属于"右手"和"控制器"的设备
            var rightHandDevices = new List<InputDevice>();
            InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller, rightHandDevices);

            if (rightHandDevices.Count > 0)
            {
                // 获取右摇杆的二维坐标值
                if (rightHandDevices[0].TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 stick))
                {
                    // 摇杆推度大于 0.1 才响应（防误触）
                    if (Mathf.Abs(stick.y) > 0.1f)
                    {
                        ScrollRect scroll = listContent.GetComponentInParent<ScrollRect>();
                        if (scroll != null)
                        {
                            // 向上推增加位置，向下推减少位置 (1.5f 为滚动速度，可自行调整)
                            scroll.verticalNormalizedPosition += stick.y * Time.deltaTime * 1.5f;
                            // 钳制在 0~1 之间防止滚出边界
                            scroll.verticalNormalizedPosition = Mathf.Clamp01(scroll.verticalNormalizedPosition);
                        }
                    }
                }
            }
        }


        public void RefreshSongList()
        {
            if (listContent == null)
            {
                Debug.LogError("[SimpleSongSelection] listContent 未设置！");
                return;
            }

            if (songItemPrefab == null)
            {
                Debug.LogError("[SimpleSongSelection] songItemPrefab 未设置！请先创建预制体。");
                return;
            }

            foreach (Transform child in listContent)
            {
                Destroy(child.gameObject);
            }

            songList = SongMetaLoader.ScanSongFolder();

            if (songList.Count == 0)
            {
                Debug.LogWarning("[SimpleSongSelection] 未找到歌曲");
                return;
            }

            Debug.Log($"[SimpleSongSelection] 找到 {songList.Count} 首歌曲");

            foreach (var map in songList)
            {
                GameObject obj = Instantiate(songItemPrefab, listContent);
                obj.transform.localScale = Vector3.one;
                obj.transform.localPosition = Vector3.zero;
                obj.transform.localRotation = Quaternion.identity;

                var view = obj.GetComponent<SongItemView>();
                if (view != null)
                {
                    view.Setup(map, OnSongSelected);
                }
                else
                {
                    Debug.LogWarning($"[SimpleSongSelection] 预制体缺少 SongItemView 组件");
                }
            }
        }

        void OnSongSelected(BeatmapMetadata mapData)
        {
            selectedSong = mapData;
            Debug.Log($"[SimpleSongSelection] 选中: {mapData.Title}");

            if (sfxSource != null && selectSound != null)
                sfxSource.PlayOneShot(selectSound);

            if (GameContext.Instance == null)
            {
                new GameObject("GameContext").AddComponent<GameContext>();
            }
            GameContext.Instance.SelectedBeatmapPath = mapData.OsuFilePath;
            GameContext.Instance.CurrentBeatmapPath = mapData.OsuFilePath;

            // ✅ 检查场景是否添加到了 Build Settings，防止静默失败选不了歌！
            if (Application.CanStreamedLevelBeLoaded(gameSceneName))
            {
                SceneManager.LoadScene(gameSceneName);
            }
            else
            {
                Debug.LogError($"[SimpleSongSelection] 无法加载场景！请点击顶部菜单 File -> Build Settings，点击 'Add Open Scenes' 或把 {gameSceneName} 拖进上面的大框框里！");
            }
        }

        void GenerateModButtons()
        {
            if (modButtonContainer == null || modButtonPrefab == null) return;

            foreach (Transform child in modButtonContainer)
            {
                Destroy(child.gameObject);
            }

            var allMods = ModDatabase.GetAllMods();
            foreach (var modInfo in allMods)
            {
                GameObject btnObj = Instantiate(modButtonPrefab, modButtonContainer);
                btnObj.transform.localScale = Vector3.one;
                btnObj.transform.localPosition = Vector3.zero;

                var tmp = btnObj.GetComponentInChildren<TextMeshProUGUI>();
                if (tmp != null)
                    tmp.text = modInfo.shortName;

                var img = btnObj.GetComponent<Image>();
                Color defaultColor = new Color(0.15f, 0.15f, 0.2f);
                if (img != null)
                    img.color = defaultColor;

                var btn = btnObj.GetComponent<Button>();
                if (btn == null) btn = btnObj.AddComponent<Button>();

                ModType capturedType = modInfo.type;
                btn.onClick.AddListener(() => ToggleMod(capturedType, img, modInfo.displayColor, defaultColor));
            }

            UpdateModDisplay();
        }

        void ToggleMod(ModType modType, Image buttonImage, Color activeColor, Color defaultColor)
        {
            if (GameContext.Instance == null)
            {
                new GameObject("GameContext").AddComponent<GameContext>();
            }

            var mods = GameContext.Instance.SelectedMods;
            mods.ToggleMod(modType);

            bool isActive = mods.HasMod(modType);
            if (buttonImage != null)
                buttonImage.color = isActive ? activeColor : defaultColor;

            UpdateModDisplay();
        }

        void UpdateModDisplay()
        {
            if (GameContext.Instance == null) return;

            var mods = GameContext.Instance.SelectedMods;

            if (multiplierText != null)
            {
                float mult = mods.GetTotalScoreMultiplier();
                multiplierText.text = $"{mult:F2}x";
                multiplierText.color = mult >= 1f ? new Color(0.3f, 1f, 0.5f) : new Color(1f, 0.5f, 0.3f);
            }

            if (activeModsText != null)
            {
                string modStr = mods.GetModString();
                activeModsText.text = string.IsNullOrEmpty(modStr) ? "No Mod" : modStr;
            }
        }

        public void ToggleModPanel()
        {
            if (modPanel != null)
                modPanel.SetActive(!modPanel.activeSelf);
        }

        public void GoBack()
        {
            SceneManager.LoadScene("MainMenuScene");
        }
    }
}
