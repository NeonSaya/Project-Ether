using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using UnityEngine.XR;

namespace OsuVR
{
    public class SimpleSongSelection : MonoBehaviour
    {
        [Header("UI 引用 - 列表区")]
        public Transform listContent;
        public GameObject songItemPrefab;

        [Header("UI 引用 - Info 面板")]
        public GameObject infoPanel;
        public RawImage backgroundImage;
        public TextMeshProUGUI titleText;
        public TextMeshProUGUI artistText;
        public TextMeshProUGUI csText;
        public TextMeshProUGUI arText;
        public TextMeshProUGUI odText;
        public TextMeshProUGUI hpText;
        public TextMeshProUGUI lengthText;
        public TextMeshProUGUI difficultyText;
        public Transform difficultyDropdownContainer;

        [Header("UI 引用 - Mod 面板")]
        public GameObject modPanel;
        public Transform modButtonContainer;
        public GameObject modButtonPrefab;
        public TextMeshProUGUI multiplierText;
        public TextMeshProUGUI activeModsText;

        [Header("UI 引用 - 选歌界面Mod状态显示")]
        public TextMeshProUGUI modStatusText;

        [Header("UI 引用 - 控制台按钮")]
        public Button openModButton;
        public Button backMenuButton;
        public Button playButton;

        [Header("场景配置")]
        public string gameSceneName = "GameScene";

        [Header("音效")]
        public AudioSource sfxSource;
        public AudioClip selectSound;

        private List<BeatmapSet> beatmapSets = new List<BeatmapSet>();
        private BeatmapSet selectedSet;
        private BeatmapMetadata selectedDifficulty;
        
        private bool isModPanelActive = false;
        private readonly Dictionary<ModType, Image> generatedModImages = new Dictionary<ModType, Image>();

        private TextMeshProUGUI toggleModsButtonText;
        private ScrollRect cachedScrollRect;
        private List<GameObject> difficultyButtons = new List<GameObject>();
        private readonly List<SongItemView> songItemViews = new List<SongItemView>();
        private SongItemView currentSelectedView = null;

        [Header("滚动按钮")]
        public Button scrollUpButton;
        public Button scrollDownButton;
        public float scrollAmount = 100f;

        void Start()
        {
            Debug.Log($"[SimpleSongSelection] 歌曲文件夹: {BeatmapImporter.SongsDirectory}");
            BeatmapImporter.ImportNewOszFiles();
            
            if (listContent != null)
            {
                cachedScrollRect = listContent.GetComponentInParent<ScrollRect>();
            }
            
            RefreshSongList();
            GenerateModButtons();

            SetupButtonReferences();
            SetupButtonListeners();

            if (modPanel != null) modPanel.SetActive(false);
            if (infoPanel != null) infoPanel.SetActive(true);
            isModPanelActive = false;

            UpdateToggleModsButtonText();
        }

        void SetupButtonReferences()
        {
            if (openModButton == null)
            {
                GameObject btnObj = GameObject.Find("Btn_ToggleMods");
                if (btnObj != null) openModButton = btnObj.GetComponent<Button>();
            }
            if (backMenuButton == null)
            {
                GameObject btnObj = GameObject.Find("Btn_Back");
                if (btnObj != null) backMenuButton = btnObj.GetComponent<Button>();
            }
            if (playButton == null)
            {
                GameObject btnObj = GameObject.Find("Btn_Play");
                if (btnObj != null) playButton = btnObj.GetComponent<Button>();
            }

            if (openModButton != null)
            {
                toggleModsButtonText = openModButton.GetComponentInChildren<TextMeshProUGUI>();
            }
        }

        void SetupButtonListeners()
        {
            if (openModButton != null)
            {
                openModButton.onClick.RemoveAllListeners();
                openModButton.onClick.AddListener(ToggleModPanel);
            }
            if (backMenuButton != null)
            {
                backMenuButton.onClick.RemoveAllListeners();
                backMenuButton.onClick.AddListener(GoBack);
            }
            if (playButton != null)
            {
                playButton.onClick.RemoveAllListeners();
                playButton.onClick.AddListener(OnPlayButtonClicked);
            }
            if (scrollUpButton != null)
            {
                scrollUpButton.onClick.RemoveAllListeners();
                scrollUpButton.onClick.AddListener(ScrollUp);
            }
            if (scrollDownButton != null)
            {
                scrollDownButton.onClick.RemoveAllListeners();
                scrollDownButton.onClick.AddListener(ScrollDown);
            }
        }

        public void ScrollUp()
        {
            if (cachedScrollRect == null) return;
            cachedScrollRect.verticalNormalizedPosition += scrollAmount / 1000f;
            cachedScrollRect.verticalNormalizedPosition = Mathf.Clamp01(cachedScrollRect.verticalNormalizedPosition);
        }

        public void ScrollDown()
        {
            if (cachedScrollRect == null) return;
            cachedScrollRect.verticalNormalizedPosition -= scrollAmount / 1000f;
            cachedScrollRect.verticalNormalizedPosition = Mathf.Clamp01(cachedScrollRect.verticalNormalizedPosition);
        }

        void Update()
        {
            HandleVRStickScroll();
        }

        void HandleVRStickScroll()
        {
            if (cachedScrollRect == null) return;

            var rightHandDevices = new List<InputDevice>();
            InputDevices.GetDevicesWithCharacteristics(
                InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller, 
                rightHandDevices);

            if (rightHandDevices.Count > 0)
            {
                if (rightHandDevices[0].TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 stick))
                {
                    if (Mathf.Abs(stick.y) > 0.1f)
                    {
                        cachedScrollRect.verticalNormalizedPosition += stick.y * Time.deltaTime * 1.5f;
                        cachedScrollRect.verticalNormalizedPosition = Mathf.Clamp01(cachedScrollRect.verticalNormalizedPosition);
                    }
                }
            }
        }

        private void EnsureGameContext()
        {
            if (GameContext.Instance == null)
            {
                GameObject gcObj = new GameObject("GameContext");
                gcObj.AddComponent<GameContext>();
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

            songItemViews.Clear();
            currentSelectedView = null;

            beatmapSets = SongMetaLoader.ScanSongFolderGrouped();

            if (beatmapSets.Count == 0)
            {
                Debug.LogWarning("[SimpleSongSelection] 未找到歌曲");
                return;
            }

            Debug.Log($"[SimpleSongSelection] 找到 {beatmapSets.Count} 个 BeatmapSet");

            foreach (var set in beatmapSets)
            {
                GameObject obj = Instantiate(songItemPrefab, listContent);
                obj.transform.localScale = Vector3.one;
                obj.transform.localPosition = Vector3.zero;
                obj.transform.localRotation = Quaternion.identity;

                var view = obj.GetComponent<SongItemView>();
                if (view != null)
                {
                    BeatmapMetadata defaultDiff = set.GetDefaultDifficulty();
                    view.Setup(defaultDiff, set.Difficulties.Count, (meta) => OnBeatmapSetSelected(set));
                    songItemViews.Add(view);
                }
                else
                {
                    Debug.LogWarning($"[SimpleSongSelection] 预制体缺少 SongItemView 组件");
                }
            }
        }

        void OnBeatmapSetSelected(BeatmapSet set)
        {
            selectedSet = set;
            selectedDifficulty = set.GetDefaultDifficulty();

            UpdateSelectionHighlight(set);

            Debug.Log($"[SimpleSongSelection] 选中 BeatmapSet: {set.Title} ({set.Difficulties.Count} 个难度)");

            if (sfxSource != null && selectSound != null)
                sfxSource.PlayOneShot(selectSound);

            LoadBackgroundImage(set.BackgroundPath);
            UpdateInfoPanel(selectedDifficulty);
            GenerateDifficultyButtons(set);

            if (isModPanelActive)
            {
                ToggleModPanel();
            }
        }

        void UpdateSelectionHighlight(BeatmapSet set)
        {
            if (currentSelectedView != null)
            {
                currentSelectedView.SetSelected(false);
            }

            foreach (var view in songItemViews)
            {
                if (view.Metadata != null && view.Metadata.Title == set.Title && view.Metadata.Artist == set.Artist)
                {
                    view.SetSelected(true);
                    currentSelectedView = view;
                    return;
                }
            }
        }

        void LoadBackgroundImage(string backgroundPath)
        {
            if (backgroundImage == null) return;

            Texture2D bgTex = SongMetaLoader.LoadBackground(backgroundPath);
            if (bgTex != null)
            {
                backgroundImage.texture = bgTex;
                backgroundImage.color = new Color(1f, 1f, 1f, 0.12f);
                backgroundImage.gameObject.SetActive(true);
            }
            else
            {
                backgroundImage.gameObject.SetActive(false);
            }
        }

        void GenerateDifficultyButtons(BeatmapSet set)
        {
            if (difficultyDropdownContainer == null) return;

            foreach (var btn in difficultyButtons)
            {
                if (btn != null) Destroy(btn);
            }
            difficultyButtons.Clear();

            foreach (var diff in set.Difficulties)
            {
                GameObject btnObj = new GameObject($"Diff_{diff.Version}");
                btnObj.transform.SetParent(difficultyDropdownContainer, false);

                RectTransform rt = btnObj.AddComponent<RectTransform>();
                rt.sizeDelta = new Vector2(90, 30);

                Image img = btnObj.AddComponent<Image>();
                img.color = GetDifficultyColor(diff.OverallDifficulty);

                Button btn = btnObj.AddComponent<Button>();
                
                GameObject textObj = new GameObject("Text");
                textObj.transform.SetParent(btnObj.transform, false);
                RectTransform textRt = textObj.AddComponent<RectTransform>();
                textRt.anchorMin = Vector2.zero;
                textRt.anchorMax = Vector2.one;
                textRt.sizeDelta = Vector2.zero;
                textRt.offsetMin = new Vector2(4, 2);
                textRt.offsetMax = new Vector2(-4, -2);
                TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
                tmp.text = string.IsNullOrEmpty(diff.Version) ? "Normal" : diff.Version;
                tmp.fontSize = 12;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = Color.white;

                BeatmapMetadata capturedDiff = diff;
                btn.onClick.AddListener(() => OnDifficultySelected(capturedDiff));

                difficultyButtons.Add(btnObj);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(difficultyDropdownContainer as RectTransform);
        }

        void OnDifficultySelected(BeatmapMetadata diff)
        {
            selectedDifficulty = diff;
            Debug.Log($"[SimpleSongSelection] 切换难度: {diff.Version}");
            UpdateInfoPanel(diff);
            UpdateStatsWithMods();

            if (sfxSource != null && selectSound != null)
                sfxSource.PlayOneShot(selectSound);
        }

        Color GetDifficultyColor(float od)
        {
            if (od < 3) return new Color(0.4f, 0.8f, 0.4f);
            if (od < 5) return new Color(0.6f, 0.8f, 0.3f);
            if (od < 6.5f) return new Color(0.9f, 0.7f, 0.2f);
            if (od < 8) return new Color(1f, 0.5f, 0.3f);
            return new Color(1f, 0.3f, 0.4f);
        }

        void UpdateInfoPanel(BeatmapMetadata mapData)
        {
            bool useOriginalLanguage = false;
            if (SettingsManager.Instance != null && SettingsManager.Instance.Settings != null)
            {
                useOriginalLanguage = SettingsManager.Instance.Settings.displayOriginalLanguage;
            }

            if (titleText != null)
            {
                string displayTitle = mapData.GetDisplayTitle(useOriginalLanguage);
                titleText.text = string.IsNullOrEmpty(displayTitle) ? "Unknown Title" : displayTitle;
            }

            if (artistText != null)
            {
                string displayArtist = mapData.GetDisplayArtist(useOriginalLanguage);
                artistText.text = string.IsNullOrEmpty(displayArtist) ? "-" : displayArtist;
            }

            try
            {
                if (csText != null)
                {
                    csText.text = mapData.CircleSize.ToString("F1");
                }

                if (arText != null)
                {
                    arText.text = mapData.ApproachRate.ToString("F1");
                }

                if (odText != null)
                {
                    odText.text = mapData.OverallDifficulty.ToString("F1");
                }

                if (hpText != null)
                {
                    hpText.text = mapData.HPDrainRate.ToString("F1");
                }

                if (lengthText != null)
                {
                    lengthText.text = mapData.GetDisplayLength();
                }

                if (difficultyText != null)
                {
                    string diffName = string.IsNullOrEmpty(mapData.Version) ? "Normal" : mapData.Version;
                    difficultyText.text = $"{diffName} ▼";
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[SimpleSongSelection] 更新四维数据失败: {e.Message}");
                
                if (csText != null) csText.text = "-";
                if (arText != null) arText.text = "-";
                if (odText != null) odText.text = "-";
                if (hpText != null) hpText.text = "-";
                if (lengthText != null) lengthText.text = "-";
            }
        }

        void UpdateStatsWithMods()
        {
            if (selectedDifficulty == null) return;

            EnsureGameContext();
            var mods = GameContext.Instance.SelectedMods;

            float origCs = selectedDifficulty.CircleSize;
            float origAr = selectedDifficulty.ApproachRate;
            float origOd = selectedDifficulty.OverallDifficulty;
            float origHp = selectedDifficulty.HPDrainRate;

            float cs = origCs;
            float ar = origAr;
            float od = origOd;
            float hp = origHp;

            bool hasHR = mods.HasMod(ModType.HardRock);
            bool hasEZ = mods.HasMod(ModType.Easy);

            if (hasHR)
            {
                cs = Mathf.Min(cs * 1.3f, 10f);
                ar = Mathf.Min(ar * 1.4f, 10f);
                od = Mathf.Min(od * 1.4f, 10f);
                hp = Mathf.Min(hp * 1.4f, 10f);
            }
            else if (hasEZ)
            {
                cs = cs * 0.5f;
                ar = ar * 0.5f;
                od = od * 0.5f;
                hp = hp * 0.5f;
            }

            Color normalColor = Color.white;
            Color increasedColor = new Color(1f, 0.5f, 0.5f);
            Color decreasedColor = new Color(0.5f, 1f, 0.5f);

            try
            {
                if (csText != null)
                {
                    csText.text = cs.ToString("F1");
                    csText.color = hasHR ? increasedColor : (hasEZ ? decreasedColor : normalColor);
                }
                if (arText != null)
                {
                    arText.text = ar.ToString("F1");
                    arText.color = hasHR ? increasedColor : (hasEZ ? decreasedColor : normalColor);
                }
                if (odText != null)
                {
                    odText.text = od.ToString("F1");
                    odText.color = hasHR ? increasedColor : (hasEZ ? decreasedColor : normalColor);
                }
                if (hpText != null)
                {
                    hpText.text = hp.ToString("F1");
                    hpText.color = hasHR ? increasedColor : (hasEZ ? decreasedColor : normalColor);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[SimpleSongSelection] 更新Mod后四维数据失败: {e.Message}");
            }
        }

        void GenerateModButtons()
        {
            if (modButtonContainer == null || modButtonPrefab == null) return;

            foreach (Transform child in modButtonContainer)
            {
                Destroy(child.gameObject);
            }
            
            generatedModImages.Clear();

            var allMods = ModDatabase.GetAllMods();
            foreach (var modInfo in allMods)
            {
                GameObject btnObj = Instantiate(modButtonPrefab, modButtonContainer);
                
                RectTransform rt = btnObj.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchoredPosition3D = Vector3.zero;
                    rt.localPosition = new Vector3(rt.localPosition.x, rt.localPosition.y, 0);
                    rt.localRotation = Quaternion.identity;
                    rt.localScale = Vector3.one;
                }

                var controller = btnObj.GetComponent<ModButtonController>();
                if (controller == null)
                {
                    controller = btnObj.AddComponent<ModButtonController>();
                }

                bool isSelected = false;
                if (GameContext.Instance != null && GameContext.Instance.SelectedMods != null)
                {
                    isSelected = GameContext.Instance.SelectedMods.HasMod(modInfo.type);
                }

                controller.Initialize(modInfo, isSelected);
                controller.OnModClicked += ToggleMod;

                var img = btnObj.GetComponent<Image>();
                if (img != null) generatedModImages[modInfo.type] = img;
            }

            UpdateModDisplay();
        }

        void ToggleMod(ModType modType)
        {
            EnsureGameContext();
            GameContext.Instance.SelectedMods.ToggleMod(modType);
            UpdateModDisplay();
        }

        void UpdateModDisplay()
        {
            EnsureGameContext();

            var mods = GameContext.Instance.SelectedMods;

            if (multiplierText != null)
            {
                float mult = mods.GetTotalScoreMultiplier();
                multiplierText.text = $"{mult:F2}x";
                multiplierText.color = mult >= 1f ? new Color(0.4f, 1f, 0.6f) : new Color(1f, 0.5f, 0.4f);
            }

            if (activeModsText != null)
            {
                string modStr = mods.GetModString();
                activeModsText.text = string.IsNullOrEmpty(modStr) ? LocalizationManager.GetText("ui_no_mod") : modStr;
            }

            string statusModStr = mods.GetModString();
            if (modStatusText != null)
            {
                if (string.IsNullOrEmpty(statusModStr))
                {
                    modStatusText.text = "";
                    modStatusText.gameObject.SetActive(false);
                }
                else
                {
                    float mult = mods.GetTotalScoreMultiplier();
                    modStatusText.text = $"{statusModStr} ({mult:F2}x)";
                    modStatusText.gameObject.SetActive(true);
                }
            }

            Color inactiveColor = new Color(0.1f, 0.1f, 0.15f, 0.9f);
            foreach (var kvp in generatedModImages)
            {
                ModType type = kvp.Key;
                Image img = kvp.Value;
                if (img != null)
                {
                    ModInfo info = ModDatabase.GetModInfo(type);
                    bool isActive = mods.HasMod(type);
                    if (isActive)
                    {
                        Color modColor = info.displayColor;
                        img.color = new Color(modColor.r, modColor.g, modColor.b, 0.95f);
                    }
                    else
                    {
                        img.color = inactiveColor;
                    }
                }
            }

            UpdateStatsWithMods();
        }

        public void ToggleModPanel()
        {
            if (infoPanel == null || modPanel == null) return;

            isModPanelActive = !isModPanelActive;

            if (isModPanelActive)
            {
                infoPanel.SetActive(false);
                modPanel.SetActive(true);
                
                if (backMenuButton != null) backMenuButton.gameObject.SetActive(false);
            }
            else
            {
                modPanel.SetActive(false);
                infoPanel.SetActive(true);
                
                if (backMenuButton != null) backMenuButton.gameObject.SetActive(true);
            }

            UpdateToggleModsButtonText();
        }

        void UpdateToggleModsButtonText()
        {
            if (toggleModsButtonText != null)
            {
                if (isModPanelActive)
                {
                    toggleModsButtonText.text = LocalizationManager.GetText("ui_back");
                    toggleModsButtonText.color = new Color(1f, 0.3f, 0.3f);
                }
                else
                {
                    toggleModsButtonText.text = LocalizationManager.GetText("ui_mods");
                    toggleModsButtonText.color = Color.white;
                }
            }
        }

        public void OnPlayButtonClicked()
        {
            if (selectedDifficulty == null)
            {
                Debug.LogWarning("[SimpleSongSelection] 未选中任何歌曲，无法开始游戏！");
                return;
            }

            Debug.Log($"[SimpleSongSelection] 开始游玩: {selectedDifficulty.Title} [{selectedDifficulty.Version}]");

            EnsureGameContext();
            
            GameContext.Instance.SelectedBeatmapPath = selectedDifficulty.OsuFilePath;
            GameContext.Instance.CurrentBeatmapPath = selectedDifficulty.OsuFilePath;

            if (Application.CanStreamedLevelBeLoaded(gameSceneName))
            {
                SceneManager.LoadScene(gameSceneName);
            }
            else
            {
                Debug.LogError($"[SimpleSongSelection] 无法加载场景！请确保 '{gameSceneName}' 已添加到 Build Settings！");
            }
        }

        public void GoBack()
        {
            SceneManager.LoadScene("MainMenuScene");
        }
    }
}
