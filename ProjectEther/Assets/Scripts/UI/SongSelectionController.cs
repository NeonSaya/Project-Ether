using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;

namespace OsuVR
{
    /// <summary>
    /// 选歌界面控制器
    /// osu 原版选歌风格 + 跳舞机选歌逻辑
    /// VR 世界空间 UI
    /// </summary>
    public class SongSelectionController : MonoBehaviour
    {
        [Header("UI 面板")]
        public Transform mainPanel;
        public Transform songListPanel;
        public Transform detailPanel;
        public Transform modPanel;

        [Header("歌曲列表")]
        public Transform songListContainer;
        public GameObject songItemPrefab;
        public ScrollRect songScrollRect;

        [Header("歌曲详情")]
        public TextMeshProUGUI titleText;
        public TextMeshProUGUI artistText;
        public TextMeshProUGUI mapperText;
        public TextMeshProUGUI difficultyText;
        public TextMeshProUGUI bpmText;
        public TextMeshProUGUI lengthText;
        public RawImage backgroundPreview;

        [Header("难度选择")]
        public Transform difficultyContainer;
        public GameObject difficultyButtonPrefab;

        [Header("Mod 显示")]
        public TextMeshProUGUI activeModsText;
        public TextMeshProUGUI scoreMultiplierText;
        public Button modToggleButton;

        [Header("操作按钮")]
        public Button playButton;
        public Button backButton;

        [Header("布局设置")]
        public float panelDistance = 4f;
        public float panelHeight = 1.2f;
        public float songItemHeight = 0.12f;

        [Header("动画设置")]
        public float scrollAnimSpeed = 10f;
        public float fadeSpeed = 5f;
        public float detailAnimDelay = 0.2f;

        [Header("音效")]
        public AudioClip selectSound;
        public AudioClip confirmSound;
        public AudioClip scrollSound;
        public AudioSource audioSource;

        [Header("预览音乐")]
        public AudioSource previewAudioSource;
        public float previewFadeInDuration = 1f;
        public float previewDelay = 0.5f;

        private List<BeatmapMetadata> songList = new List<BeatmapMetadata>();
        private List<SongItemUI> songItems = new List<SongItemUI>();
        private int currentIndex = 0;
        private BeatmapMetadata selectedSong;
        private string selectedDifficulty;
        private bool modPanelVisible = false;
        private Coroutine previewCoroutine;

        private class SongItemUI
        {
            public GameObject gameObject;
            public Transform transform;
            public TextMeshProUGUI titleText;
            public TextMeshProUGUI artistText;
            public Image background;
            public CanvasGroup canvasGroup;
            public Button button;
        }

        void Start()
        {
            InitializeUI();
            LoadSongList();
            SetupButtons();
        }

        void InitializeUI()
        {
            PositionPanel();

            if (previewAudioSource == null)
            {
                previewAudioSource = gameObject.AddComponent<AudioSource>();
            }
            previewAudioSource.loop = true;
            previewAudioSource.volume = 0f;
        }

        void PositionPanel()
        {
            if (mainPanel == null) return;

            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                Vector3 forward = mainCam.transform.forward;
                forward.y = 0;
                forward.Normalize();

                mainPanel.position = mainCam.transform.position + forward * panelDistance + Vector3.up * panelHeight;
                mainPanel.rotation = Quaternion.LookRotation(forward, Vector3.up);
            }
        }

        void LoadSongList()
        {
            BeatmapImporter.ImportNewOszFiles();
            songList = SongMetaLoader.ScanSongFolder();

            if (songList.Count == 0)
            {
                Debug.LogWarning("[SongSelection] 未找到歌曲");
                ShowEmptyState();
                return;
            }

            CreateSongItems();
            SelectSong(0);
        }

        void ShowEmptyState()
        {
            if (titleText != null)
                titleText.text = "未找到歌曲";
            if (artistText != null)
                artistText.text = "请将谱面放入 Songs 文件夹";
            if (playButton != null)
                playButton.interactable = false;
        }

        void CreateSongItems()
        {
            if (songListContainer == null) return;

            foreach (Transform child in songListContainer)
            {
                Destroy(child.gameObject);
            }
            songItems.Clear();

            for (int i = 0; i < songList.Count; i++)
            {
                var metadata = songList[i];
                var item = CreateSongItem(metadata, i);
                songItems.Add(item);
            }
        }

        SongItemUI CreateSongItem(BeatmapMetadata metadata, int index)
        {
            SongItemUI item;

            if (songItemPrefab != null)
            {
                GameObject obj = Instantiate(songItemPrefab, songListContainer);
                obj.transform.localPosition = Vector3.down * index * songItemHeight;
                obj.transform.localRotation = Quaternion.identity;
                obj.name = $"SongItem_{metadata.Title}";

                item = new SongItemUI
                {
                    gameObject = obj,
                    transform = obj.transform,
                    titleText = obj.GetComponentInChildren<TextMeshProUGUI>(),
                    background = obj.GetComponent<Image>(),
                    button = obj.GetComponent<Button>()
                };

                var cg = obj.GetComponent<CanvasGroup>();
                if (cg == null) cg = obj.AddComponent<CanvasGroup>();
                item.canvasGroup = cg;
            }
            else
            {
                GameObject obj = new GameObject($"SongItem_{metadata.Title}");
                obj.transform.SetParent(songListContainer);
                obj.transform.localPosition = Vector3.down * index * songItemHeight;
                obj.transform.localRotation = Quaternion.identity;
                obj.transform.localScale = new Vector3(1f, songItemHeight * 0.8f, 0.01f);

                var bg = obj.AddComponent<Image>();
                bg.color = new Color(0.1f, 0.1f, 0.15f, 0.8f);

                var btn = obj.AddComponent<Button>();

                var textObj = new GameObject("Text");
                textObj.transform.SetParent(obj.transform);
                textObj.transform.localPosition = Vector3.zero;
                textObj.transform.localScale = Vector3.one;

                var tmp = textObj.AddComponent<TextMeshProUGUI>();
                tmp.text = $"{metadata.Title}\n<size=60%>{metadata.Artist}</size>";
                tmp.alignment = TextAlignmentOptions.Left;
                tmp.fontSize = 0.06f;
                tmp.color = Color.white;

                var cg = obj.AddComponent<CanvasGroup>();

                item = new SongItemUI
                {
                    gameObject = obj,
                    transform = obj.transform,
                    titleText = tmp,
                    background = bg,
                    button = btn,
                    canvasGroup = cg
                };
            }

            if (item.titleText != null)
            {
                item.titleText.text = $"{metadata.Title}\n<size=60%>{metadata.Artist}</size>";
            }

            int capturedIndex = index;
            item.button.onClick.AddListener(() =>
            {
                SelectSong(capturedIndex);
                PlaySound(selectSound);
            });

            return item;
        }

        void SelectSong(int index)
        {
            if (index < 0 || index >= songList.Count) return;

            currentIndex = index;
            selectedSong = songList[index];

            UpdateSongListVisuals();
            UpdateDetailPanel();
            UpdateDifficultyButtons();
            StartPreviewMusic();
        }

        void UpdateSongListVisuals()
        {
            for (int i = 0; i < songItems.Count; i++)
            {
                var item = songItems[i];
                bool isSelected = i == currentIndex;

                if (item.background != null)
                {
                    item.background.color = isSelected
                        ? new Color(0.2f, 0.4f, 0.8f, 0.95f)
                        : new Color(0.1f, 0.1f, 0.15f, 0.8f);
                }

                if (item.canvasGroup != null)
                {
                    float distance = Mathf.Abs(i - currentIndex);
                    float alpha = Mathf.Lerp(1f, 0.3f, distance / 5f);
                    item.canvasGroup.alpha = alpha;
                }
            }

            ScrollToSelected();
        }

        void ScrollToSelected()
        {
            if (songScrollRect != null)
            {
                float targetPos = 1f - (float)currentIndex / Mathf.Max(1, songList.Count - 1);
                songScrollRect.verticalNormalizedPosition = Mathf.Lerp(
                    songScrollRect.verticalNormalizedPosition,
                    targetPos,
                    Time.deltaTime * scrollAnimSpeed
                );
            }
        }

        void UpdateDetailPanel()
        {
            if (selectedSong == null) return;

            if (titleText != null)
                titleText.text = selectedSong.Title;
            if (artistText != null)
                artistText.text = selectedSong.Artist;
            if (mapperText != null)
                mapperText.text = $"Mapped by {selectedSong.Creator}";
            if (difficultyText != null)
                difficultyText.text = $"[{selectedSong.Version}]";
            if (bpmText != null)
                bpmText.text = $"BPM: {selectedSong.BPM:F0}";
            if (lengthText != null)
                lengthText.text = FormatLength(selectedSong.Length);
        }

        string FormatLength(float seconds)
        {
            int mins = (int)(seconds / 60);
            int secs = (int)(seconds % 60);
            return $"{mins}:{secs:D2}";
        }

        void UpdateDifficultyButtons()
        {
            if (difficultyContainer == null) return;

            foreach (Transform child in difficultyContainer)
            {
                Destroy(child.gameObject);
            }

            var diffs = GetAvailableDifficulties(selectedSong);
            foreach (var diff in diffs)
            {
                CreateDifficultyButton(diff);
            }
        }

        List<string> GetAvailableDifficulties(BeatmapMetadata metadata)
        {
            var diffs = new List<string>();
            if (!string.IsNullOrEmpty(metadata.Version))
            {
                diffs.Add(metadata.Version);
            }
            if (diffs.Count == 0)
            {
                diffs.Add("Normal");
            }
            return diffs;
        }

        void CreateDifficultyButton(string difficulty)
        {
            GameObject btnObj;

            if (difficultyButtonPrefab != null)
            {
                btnObj = Instantiate(difficultyButtonPrefab, difficultyContainer);
            }
            else
            {
                btnObj = new GameObject($"Diff_{difficulty}");
                btnObj.transform.SetParent(difficultyContainer);
                btnObj.transform.localScale = new Vector3(0.3f, 0.08f, 0.01f);

                var bg = btnObj.AddComponent<Image>();
                bg.color = GetDifficultyColor(difficulty);

                var tmp = btnObj.AddComponent<TextMeshProUGUI>();
                tmp.text = difficulty;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.fontSize = 0.05f;
            }

            var btn = btnObj.GetComponent<Button>();
            if (btn == null) btn = btnObj.AddComponent<Button>();

            btn.onClick.AddListener(() =>
            {
                selectedDifficulty = difficulty;
                PlaySound(selectSound);
            });
        }

        Color GetDifficultyColor(string difficulty)
        {
            string d = difficulty.ToLower();
            if (d.Contains("easy")) return new Color(0.3f, 0.8f, 0.3f);
            if (d.Contains("normal")) return new Color(0.3f, 0.7f, 1f);
            if (d.Contains("hard")) return new Color(1f, 0.7f, 0.2f);
            if (d.Contains("insane") || d.Contains("expert")) return new Color(1f, 0.3f, 0.3f);
            if (d.Contains("extra") || d.Contains("master")) return new Color(0.8f, 0.2f, 0.8f);
            return new Color(0.5f, 0.5f, 0.5f);
        }

        void StartPreviewMusic()
        {
            if (previewCoroutine != null)
                StopCoroutine(previewCoroutine);

            previewCoroutine = StartCoroutine(PlayPreviewMusicCoroutine());
        }

        IEnumerator PlayPreviewMusicCoroutine()
        {
            if (previewAudioSource == null || selectedSong == null)
                yield break;

            previewAudioSource.Stop();
            previewAudioSource.volume = 0f;

            string audioPath = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(selectedSong.OsuFilePath),
                selectedSong.AudioFilename
            );

            if (!System.IO.File.Exists(audioPath))
            {
                Debug.LogWarning($"[SongSelection] 音频文件不存在: {audioPath}");
                yield break;
            }

            yield return new WaitForSeconds(previewDelay);

            using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip("file:///" + audioPath, AudioType.UNKNOWN))
            {
                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError)
                {
                    Debug.LogWarning($"[SongSelection] 加载音频失败: {www.error}");
                    yield break;
                }

                previewAudioSource.clip = DownloadHandlerAudioClip.GetContent(www);
                previewAudioSource.time = selectedSong.PreviewTime;
                previewAudioSource.Play();

                float elapsed = 0f;
                while (elapsed < previewFadeInDuration)
                {
                    elapsed += Time.deltaTime;
                    previewAudioSource.volume = Mathf.Lerp(0f, 0.5f, elapsed / previewFadeInDuration);
                    yield return null;
                }
            }
        }

        void SetupButtons()
        {
            if (playButton != null)
            {
                playButton.onClick.AddListener(OnPlayClicked);
            }

            if (backButton != null)
            {
                backButton.onClick.AddListener(OnBackClicked);
            }

            if (modToggleButton != null)
            {
                modToggleButton.onClick.AddListener(ToggleModPanel);
            }
        }

        void ToggleModPanel()
        {
            modPanelVisible = !modPanelVisible;

            if (modPanel != null)
            {
                modPanel.gameObject.SetActive(modPanelVisible);
            }

            PlaySound(selectSound);
        }

        void OnPlayClicked()
        {
            if (selectedSong == null) return;

            PlaySound(confirmSound);

            if (previewAudioSource != null)
            {
                previewAudioSource.Stop();
            }

            if (GameContext.Instance != null)
            {
                GameContext.Instance.SelectedBeatmapPath = selectedSong.OsuFilePath;
                GameContext.Instance.CurrentBeatmapPath = selectedSong.OsuFilePath;
            }

            if (SceneFlowManager.Instance != null)
            {
                SceneFlowManager.Instance.GoToGame(selectedSong.OsuFilePath);
            }
            else
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene("GameScene");
            }
        }

        void OnBackClicked()
        {
            if (previewAudioSource != null)
            {
                previewAudioSource.Stop();
            }

            if (SceneFlowManager.Instance != null)
            {
                SceneFlowManager.Instance.GoToMainMenu();
            }
            else
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenuScene");
            }
        }

        void Update()
        {
            UpdatePanelPosition();
            UpdateModDisplay();
        }

        void UpdatePanelPosition()
        {
            if (mainPanel == null) return;

            Camera mainCam = Camera.main;
            if (mainCam == null) return;

            Vector3 targetPos = mainCam.transform.position + mainCam.transform.forward * panelDistance;
            targetPos.y = panelHeight;

            mainPanel.position = Vector3.Lerp(mainPanel.position, targetPos, Time.deltaTime * 3f);

            Vector3 lookDir = mainPanel.position - mainCam.transform.position;
            lookDir.y = 0;
            if (lookDir.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(lookDir, Vector3.up);
                mainPanel.rotation = Quaternion.Slerp(mainPanel.rotation, targetRot, Time.deltaTime * 3f);
            }
        }

        void UpdateModDisplay()
        {
            if (GameContext.Instance == null) return;

            var mods = GameContext.Instance.SelectedMods;

            if (activeModsText != null)
            {
                string modString = mods.GetModString();
                activeModsText.text = string.IsNullOrEmpty(modString) ? "No Mod" : modString;
            }

            if (scoreMultiplierText != null)
            {
                float multiplier = mods.GetTotalScoreMultiplier();
                scoreMultiplierText.text = $"{multiplier:F2}x";
                scoreMultiplierText.color = multiplier >= 1f
                    ? new Color(0.3f, 1f, 0.5f)
                    : new Color(1f, 0.5f, 0.3f);
            }
        }

        void PlaySound(AudioClip clip)
        {
            if (clip != null && audioSource != null)
            {
                audioSource.PlayOneShot(clip);
            }
        }

        void OnDestroy()
        {
            if (previewAudioSource != null)
            {
                previewAudioSource.Stop();
            }
        }

        public void SelectNextSong()
        {
            if (currentIndex < songList.Count - 1)
            {
                SelectSong(currentIndex + 1);
                PlaySound(scrollSound);
            }
        }

        public void SelectPreviousSong()
        {
            if (currentIndex > 0)
            {
                SelectSong(currentIndex - 1);
                PlaySound(scrollSound);
            }
        }
    }
}
