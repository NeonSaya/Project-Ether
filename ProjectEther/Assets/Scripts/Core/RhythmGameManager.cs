using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using OsuVR;

namespace OsuVR
{
    /// <summary>
    /// 节奏游戏管理器：控制游戏循环和音符生成（支持HitCircle、Slider、Spinner）
    /// </summary>
    public class RhythmGameManager : MonoBehaviour
    {

        [Header("全局材质补丁")]
        [Tooltip("拖入你做好的 Mat_Note_Glow，所有 Note 都会强制换成这个")]
        public Material globalGlowMaterial;

        [Header("谱面配置")]
        [Tooltip("osu谱面文件名（.osu文件）")]
        public string osuFileName = "map.osu";

        [Header("音乐配置")]
        [Tooltip("用于播放音乐的AudioSource")]
        public AudioSource musicSource;

        [Tooltip("音乐文件（会赋值给musicSource的clip）")]
        public AudioClip musicClip;

        [Header("音符预制体")]
        [Tooltip("打击圈预制体")]
        public GameObject hitCirclePrefab;

        [Tooltip("滑条预制体")]
        public GameObject sliderPrefab;

        [Tooltip("转盘预制体")]
        public GameObject spinnerPrefab;

        [Header("音符配置")]
        [Tooltip("音符飞行速度（米/秒）")]
        public float noteSpeed = 5.0f;

        [Tooltip("音符提前生成时间（毫秒）")] //自动计算
        public float spawnOffsetMs = 1000f;

        [Tooltip("音符生命周期（秒），超过此时间未击打则自动销毁")]
        public float noteLifetime = 3.0f;

        [Header("游戏设置")]

        [Header("AutoPlay 设置")]
        [Tooltip("开启后，游戏将由 AI 自动游玩")]
        public bool useAutoPlay = false;

        [Tooltip("指向场景中的 AutoPlayManager 脚本")]
        public AutoPlayManager autoPlayManager;

        [Tooltip("准备时间缓冲（秒），音乐开始前给玩家的准备时间")]
        public float preparationTime = 2.0f;

        [Tooltip("是否自动开始游戏")]
        public bool autoStart = true;

        [Header("系统引用")]
        [Tooltip("拖入挂载了 ScoreManager 脚本的物体")]
        public ScoreManager scoreManager; // <--- 新增这个

        [Header("暂停菜单设置")]
        [Tooltip("VR暂停菜单Prefab（推荐使用）")]
        public GameObject pauseMenuPrefab;
        
        [Tooltip("如果不想用Prefab，可以拖入场景中的VRPauseMenu实例")]
        public VRPauseMenu pauseMenuInstance;

        // 内部引用
        private InputAction cancelInputAction;
        private VRPauseMenu currentPauseMenu; // 当前激活的暂停菜单实例
        private bool isUsingPrefab = false;   // 是否使用Prefab模式

        [Header("游戏状态")]
        [Tooltip("当前音乐时间（毫秒）")]
        public double currentMusicTimeMs = 0;

        [Tooltip("游戏是否正在进行")]
        public bool isPlaying = false;

        [Tooltip("音乐是否正在播放")]
        public bool isMusicPlaying = false;

        [Tooltip("倒计时时间（秒），负数表示游戏未开始")]
        public double countdownTime = 0;

        [Tooltip("游戏是否已结束")]
        public bool isGameEnded = false;

        [Header("调试信息")]
        [SerializeField]
        private int totalNotes = 0;

        [SerializeField]
        private int spawnedNotes = 0;

        [SerializeField]
        private int activeNotes = 0;

        [SerializeField]
        private double dspStartTime = 0;

        [SerializeField]
        private double gameStartDspTime = 0;

        [Header("校准设置")]
        [Tooltip("全局偏移 (毫秒)：调整音画同步。正数表示Note出现得更晚，负数表示Note出现得更早")]
        public float universalOffsetMs = 0f;

        private double audioSystemLatency = 0;

        private List<HitObject> hitObjects = new List<HitObject>();
        private int nextNoteIndex = 0;
        private Beatmap currentBeatmap;
        private string currentBeatmapPath;
        private Dictionary<HitObject, GameObject> activeNoteObjects = new Dictionary<HitObject, GameObject>();

        private double bufferStartDspTime = 0;
        private int currentRenderBaseline = 0;

        private ModEffectsApplier modEffects;
        private float speedMultiplier = 1f;

        // [新增] 1. 静态计算公式
        public static double CalculateTimePreempt(float ar)
        {
            // 限制 AR 在 0 到 10 之间
            ar = Mathf.Clamp(ar, 0f, 10f);

            if (ar < 5)
            {
                // AR 0 = 1800ms, AR 5 = 1200ms
                return 1200 + 120 * (5 - ar);
            }
            else
            {
                // AR 5 = 1200ms, AR 10 = 450ms
                return 1200 - 150 * (ar - 5);
            }
        }

        /// <summary>
        /// 初始化游戏
        /// </summary>
        void Start()
        {
            Debug.Log($"开始初始化节奏游戏管理器...");

            int bufferLength;
            int numBuffers;
            AudioSettings.GetDSPBufferSize(out bufferLength, out numBuffers);
            // 单次缓冲区的长度 / 采样率 = 延迟秒数
            // 通常 Unity 默认是 Best Latency，但也可能有 20-50ms
            audioSystemLatency = (double)bufferLength / AudioSettings.outputSampleRate;

            Debug.Log($"[Sync] 音频系统硬件延迟: {audioSystemLatency * 1000:F2} ms");

            // 检查必要组件
            if (hitCirclePrefab == null)
            {
                Debug.LogError("打击圈预制体未分配！");
            }

            if (sliderPrefab == null)
            {
                Debug.LogWarning("滑条预制体未分配，滑条将不会被生成。");
            }

            if (spinnerPrefab == null)
            {
                Debug.LogWarning("转盘预制体未分配，转盘将不会被生成。");
            }

            // 初始化AudioSource
            InitializeAudioSource();

            // 从SettingsManager加载音频偏移设置
            if (SettingsManager.Instance != null && SettingsManager.Instance.Settings != null)
            {
                universalOffsetMs = SettingsManager.Instance.Settings.audioOffsetMs;
                Debug.Log($"[Settings] Loaded audio offset: {universalOffsetMs}ms");
            }

            /// 获取选歌数据（如果有）
            string selectedPath = null;
            if (GameContext.Instance != null)
            {
                selectedPath = GameContext.Instance.SelectedBeatmapPath;
                Debug.Log($"[Context] GameContext 存在，存储路径: '{selectedPath}'");
            }
            else
            {
                Debug.LogWarning("[Context] GameContext 为空！(你是直接运行的 GameScene 吗？请从 MenuScene 开始)");
            }

            // 根据选歌数据加载谱面
            if (!string.IsNullOrEmpty(selectedPath))
            {
                Debug.Log("[RhythmGameManager] 检测到选歌数据，尝试加载选定谱面...");
                LoadBeatmapFromPath(selectedPath);
            }
            else
            {
                Debug.Log("[RhythmGameManager] 无选歌数据，加载默认/测试谱面...");
                LoadBeatmap(); // 加载 Inspector 里的 osuFileName
                if (autoStart) Invoke("StartGame", 1.0f);
            }

            // 初始化暂停菜单
            InitializePauseMenu();
            
            // 初始化暂停菜单输入
            InitializePauseInput();
        }

        /// <summary>
        /// 初始化AudioSource (修复了 VR 音频设置)
        /// </summary>
        private void InitializeAudioSource()
        {
            // 如果没有分配musicSource，尝试获取组件
            if (musicSource == null)
            {
                musicSource = GetComponent<AudioSource>();
                if (musicSource == null)
                {
                    musicSource = gameObject.AddComponent<AudioSource>();
                }
            }

            // 设置AudioSource属性
            musicSource.playOnAwake = false;
            musicSource.loop = false;

            // 修复: VR游戏中必须关闭多普勒效应，否则当你摇头或快速移动时，音乐音调会变怪
            musicSource.dopplerLevel = 0;
            musicSource.reverbZoneMix = 0; // 通常也不需要混响

            // 如果有分配音乐文件，设置clip
            if (musicClip != null)
            {
                musicSource.clip = musicClip;
            }

            Debug.Log($"AudioSource初始化完成，当前clip: {musicSource.clip?.name ?? "null"}");
        }

        /// <summary>
        /// 每帧更新游戏状态
        /// </summary>
        void Update()
        {
            // 检查空格键开始游戏（调试功能）
            if (Input.GetKeyDown(KeyCode.Space) && !isPlaying)
            {
                Debug.Log("空格键按下，开始游戏！");
                StartGame();
            }

            // 更新当前时间
            UpdateCurrentTime();

            // 如果游戏未开始但是处于缓冲期，更新倒计时
            if (!isPlaying && bufferStartDspTime > 0)
            {
                UpdateCountdown();
            }

            // 如果游戏进行中，生成音符
            if (isPlaying)
            {
                SpawnNotes();
                CheckGameEnd();
            }

            // 更新调试信息
            UpdateDebugInfo();
        }

        /// <summary>
        /// 检查游戏是否结束
        /// 条件：所有音符已判定完成（与原版 osu! 一致）
        /// </summary>
        private void CheckGameEnd()
        {
            if (isGameEnded) return;

            // 所有音符已生成且已判定 = 游戏结束
            // 不需要等歌曲播放完毕，打完最后一个note就结算
            bool allNotesProcessed = nextNoteIndex >= hitObjects.Count;
            bool allNotesCleared = activeNotes == 0;

            if (allNotesProcessed && allNotesCleared)
            {
                StartCoroutine(EndGameCoroutine());
            }
        }

        /// <summary>
        /// 结束游戏协程：等待2秒后跳转结算场景
        /// 音乐继续播放，保持余韵感
        /// </summary>
        private IEnumerator EndGameCoroutine()
        {
            if (isGameEnded) yield break;

            isGameEnded = true;
            // 注意：不设置 isPlaying = false，让音乐继续播放

            Debug.Log("[RhythmGame] 游戏结束！等待2秒后显示结算...");

            // 等待2秒，让玩家有余韵感
            yield return new WaitForSeconds(2f);

            // 让音乐跨场景继续播放
            if (musicSource != null && musicSource.isPlaying)
            {
                if (MusicManager.Instance != null)
                {
                    MusicManager.Instance.SetAudioSource(musicSource);
                    MusicManager.Instance.SetPersist(true);
                }
            }

            if (scoreManager != null && currentBeatmap != null)
            {
                scoreManager.SetBeatmapInfo(
                    currentBeatmap.Metadata?.Title,
                    currentBeatmap.Metadata?.TitleUnicode,
                    currentBeatmap.Metadata?.Artist,
                    currentBeatmap.Metadata?.ArtistUnicode,
                    currentBeatmap.Metadata?.Version,
                    currentBeatmap.Metadata?.Creator
                );

                ResultData result = scoreManager.GetResultData();

                // 保存到全局上下文 (用于结算场景显示和重试)
                if (GameContext.Instance != null)
                {
                    GameContext.Instance.LastResult = result;
                    GameContext.Instance.CurrentBeatmapPath = currentBeatmapPath;

                    // 跳转到结算场景
                    UnityEngine.SceneManagement.SceneManager.LoadScene(GameContext.Instance.ResultSceneName);
                }
                else
                {
                    Debug.LogWarning("[RhythmGame] GameContext 未找到，无法跳转到结算场景");
                    Debug.Log($"[Result] 分数: {result.finalScore}, 准确率: {result.accuracy * 100:F2}%, 最大连击: {result.maxCombo}, 评级: {result.rank}");
                }
            }
        }

        /// <summary>
        /// 更新当前时间
        /// </summary>
        private void UpdateCurrentTime()
        {
            if (!isPlaying)
            {
                // 游戏未开始，计算倒计时时间
                if (bufferStartDspTime > 0)
                {
                    double currentDspTime = AudioSettings.dspTime;
                    double elapsedBufferTime = currentDspTime - bufferStartDspTime;

                    // 修复: 倒计时等待阶段，spawnOffset的真实等待时间也受变速影响
                    countdownTime = preparationTime + ((spawnOffsetMs / 1000.0) / speedMultiplier) - elapsedBufferTime;

                    // 修复: 乘上 speedMultiplier 让游戏时间与加速的音乐匹配
                    currentMusicTimeMs = (((currentDspTime - dspStartTime) * 1000.0) * speedMultiplier) - universalOffsetMs;
                }
                else
                {
                    countdownTime = 0;
                    currentMusicTimeMs = -spawnOffsetMs;
                }
            }
            else
            {
                // 游戏已开始，使用DSP时间计算精确的音乐时间
                if (dspStartTime > 0)
                {
                    double currentDspTime = AudioSettings.dspTime;
                    
                    // 修复: 乘上 speedMultiplier 让游戏时间与加速的音乐匹配
                    currentMusicTimeMs = (((currentDspTime - dspStartTime) * 1000.0) * speedMultiplier) - universalOffsetMs;

                    // 检查音乐是否应该开始但还未开始
                    if (!isMusicPlaying && currentMusicTimeMs >= 0)
                    {
                        isMusicPlaying = true;
                        Debug.Log($"音乐开始播放！当前时间: {currentMusicTimeMs:F2}ms");
                    }
                }
            }
        }

        /// <summary>
        /// 更新倒计时显示
        /// </summary>
        private void UpdateCountdown()
        {
            // 这里可以添加倒计时UI更新逻辑
            // 例如：if (countdownTime > 0 && countdownTime <= 3) 显示"3, 2, 1..."

            // 检查是否需要开始游戏（缓冲期结束）
            if (countdownTime <= 0 && !isPlaying)
            {
                StartGamePlay();
            }
        }

        /// <summary>
        /// 开始游戏（设置音乐播放计划）
        /// </summary>
        public void StartGame()
        {
            if (hitObjects == null || hitObjects.Count == 0)
            {
                Debug.LogError("无法开始游戏：没有加载到音符数据！");
                return;
            }

            if (musicSource == null || musicSource.clip == null)
            {
                Debug.LogError("无法开始游戏：AudioSource或音乐文件未设置！");
                return;
            }
            if (scoreManager != null)
            {
                scoreManager.Initialize(hitObjects);
                Debug.Log($"[RhythmGame] 分数系统已初始化");
            }
            else
            {
                Debug.LogWarning("[RhythmGame] ⚠️ ScoreManager 未赋值！分数将不会计算。");
            }

            if (JudgementVisualizer.Instance != null)
            {
                Debug.Log("[RhythmGame] 正在预热判定特效...");
                JudgementVisualizer.Instance.Prewarm();
            }
            Debug.Log("开始游戏设置...");

            // 重置游戏状态
            ResetGameState();

            // 计算DSP开始时间
            // dspStartTime = 当前DSP时间 + 准备时间 + spawnOffset（转换为秒）
            // 这样音乐会在准备时间 + spawnOffset秒后开始播放
            double currentDspTime = AudioSettings.dspTime;

            double startTimeBuffer = System.Math.Max(preparationTime, 0.5);
            dspStartTime = currentDspTime + startTimeBuffer;

            // 记录缓冲期开始时间（用于倒计时）
            bufferStartDspTime = currentDspTime;

            // 设置音乐变速 (DT/HT)
            musicSource.pitch = speedMultiplier;
            // 设置音乐在未来某个精确时刻播放
            musicSource.PlayScheduled(dspStartTime);

            // 计算游戏开始DSP时间（音乐开始前spawnOffset毫秒）
            gameStartDspTime = dspStartTime - (spawnOffsetMs / 1000.0);

            // 设置缓冲期状态（还未正式开始游戏）
            isPlaying = false;  // 注意：这里先设置为false，等缓冲期结束再设为true
            isMusicPlaying = false;

            Debug.Log($"游戏计划设置完成:");
            Debug.Log($"当前DSP时间: {currentDspTime:F3}s");
            Debug.Log($"音乐开始时间: {dspStartTime:F3}s (当前时间 + {preparationTime + spawnOffsetMs / 1000.0:F3}s)");
            Debug.Log($"游戏开始时间: {gameStartDspTime:F3}s (音乐开始前 {spawnOffsetMs}ms)");
            Debug.Log($"准备时间: {preparationTime}s, 音符提前时间: {spawnOffsetMs}ms");

            // 清理现有的音符（确保开始前没有残留音符）
            ClearAllNotes();
        }

        /// <summary>
        /// 从指定绝对路径加载谱面（供 UI 选歌使用）
        /// </summary>
        /// <param name="absoluteOsuFilePath">.osu 文件的完整路径</param>
        public void LoadBeatmapFromPath(string absoluteOsuFilePath)
        {
            Debug.Log($"正在从路径加载谱面: {absoluteOsuFilePath}");

            // 0. 停止之前的游戏状态
            CancelInvoke("StartGame");
            StopAllCoroutines();
            if (isPlaying)
            {
                isPlaying = false;
                if (musicSource.isPlaying) musicSource.Stop();
                ClearAllNotes();
            }

            // 1. 解析谱面
            if (!File.Exists(absoluteOsuFilePath))
            {
                Debug.LogError($"[Load] 致命错误：文件不存在 -> {absoluteOsuFilePath}");
                LoadBeatmap();
                StartGame();
                return;
            }

            try
            {
                currentBeatmap = OsuParser.Parse(absoluteOsuFilePath);
                currentBeatmapPath = absoluteOsuFilePath;
                StackingProcessor.ApplyStacking(currentBeatmap);

                hitObjects = currentBeatmap.HitObjects;
                totalNotes = hitObjects.Count;

                ApplyModEffects();

                if (currentBeatmap.ComboColors == null || currentBeatmap.ComboColors.Count == 0)
                {
                    currentBeatmap.ComboColors = new List<Color> {
                        new Color(1f, 0.4f, 0.4f), new Color(0.4f, 0.6f, 1f),
                        new Color(0.4f, 1f, 0.4f), new Color(1f, 0.8f, 0.4f)
                     };
                }

                if (currentBeatmap != null && currentBeatmap.Difficulty != null)
                {
                    float effectiveAR = modEffects != null
                        ? modEffects.GetModifiedAR(currentBeatmap.Difficulty.ApproachRate)
                        : currentBeatmap.Difficulty.ApproachRate;
                    spawnOffsetMs = (float)CalculateTimePreempt(effectiveAR);
                    Debug.Log($"[AR System] Loaded AR: {currentBeatmap.Difficulty.ApproachRate}, Modified AR: {effectiveAR}, TimePreempt: {spawnOffsetMs}ms");
                }

                foreach (var obj in hitObjects)
                {
                    obj.TimePreempt = spawnOffsetMs;
                }

                // HR Mod: Y轴镜像翻转 (修复选歌界面进入时未应用HR镜像的问题)
                if (modEffects != null && modEffects.IsHardRock)
                {
                    ApplyHardRockMirror();
                }

                Debug.Log($"✅ 谱面数据解析成功: {currentBeatmap.Metadata.Title}");

                string folderPath = Path.GetDirectoryName(absoluteOsuFilePath);
                string audioFileName = currentBeatmap.General.AudioFilename.Trim();
                string audioPath = Path.Combine(folderPath, audioFileName);

                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.LoadBeatmapSamples(folderPath);
                }

                StartCoroutine(LoadAudioClip(audioPath));
            }
            catch (System.Exception e)
            {
                Debug.LogError($"加载失败: {e.Message}\n{e.StackTrace}");
                // 出错时回退到默认加载
                LoadBeatmap();
                StartGame();
            }
        }

        /// <summary>
        /// 协程：动态加载本地音频文件
        /// </summary>
        private IEnumerator LoadAudioClip(string audioPath)
        {
            // Windows/Android 路径需要加 file:// 前缀，且要注意转义
            string url = "file://" + audioPath;
            url = url.Replace("\\", "/");

            Debug.Log($"开始加载音频: {url}");

            using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.UNKNOWN))
            {
                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError)
                {
                    Debug.LogError($"音频加载错误: {www.error}");
                    Debug.LogError($"[Audio] 尝试加载的路径是: {audioPath}");

                    // 即使音频加载失败，也要强行开始游戏 (无声游玩)，否则会卡在黑屏
                    StartGame();
                }
                else
                {
                    AudioClip clip = DownloadHandlerAudioClip.GetContent(www);

                    if (clip != null)
                    {
                        // 必须设置 name，否则 Unity 有时会报错
                        clip.name = Path.GetFileName(audioPath);
                        clip.LoadAudioData();
                        musicSource.clip = clip;
                        musicClip = clip;

                        Debug.Log("[Audio] 正在预热音频引擎...");

                        musicSource.pitch = speedMultiplier;
                        Debug.Log($"[Mod] 音频速度设置为: {speedMultiplier}x");

                        musicSource.volume = 0;
                        musicSource.Play();

                        yield return null;
                        yield return null;

                        musicSource.Stop();
                        musicSource.time = 0;
                        musicSource.volume = 1;

                        Debug.Log($"[Audio] 预热完成: {clip.name} ({clip.length:F1}s)");

                        StartGame();
                    }
                    else
                    {
                        Debug.LogError("下载的 AudioClip 为空！");
                        StartGame();
                    }
                }
            }
        }

        private void ApplyModEffects()
        {
            if (GameContext.Instance != null && GameContext.Instance.SelectedMods != null)
            {
                modEffects = new ModEffectsApplier(GameContext.Instance.SelectedMods);
                speedMultiplier = modEffects.SpeedMultiplier;

                if (scoreManager != null)
                {
                    scoreManager.SetModsFromSelection(GameContext.Instance.SelectedMods);
                }

                useAutoPlay = modEffects.IsAutoPlay;

                Debug.Log($"[Mod] 已应用 Mod 效果: {modEffects.GetModString()}");
                Debug.Log($"[Mod] 速度倍率: {speedMultiplier}x, 分数倍率: {modEffects.ScoreMultiplier}x");
                Debug.Log($"[Mod] AutoPlay: {useAutoPlay}");
            }
            else
            {
                modEffects = null;
                speedMultiplier = 1f;
                if (scoreManager != null)
                {
                    scoreManager.SetModsFromSelection(null);
                }
            }
        }

        public ModEffectsApplier GetModEffects()
        {
            return modEffects;
        }

        /// <summary>
        /// 应用 Mod 效果到谱面难度参数
        /// </summary>
        private void ApplyModEffectsToBeatmap()
        {
            if (modEffects == null || currentBeatmap == null || currentBeatmap.Difficulty == null)
                return;

            var diff = currentBeatmap.Difficulty;

            // 应用 CS/AR 修改 (OD 固定为 250ms 判定窗口，不修改)
            diff.CircleSize = modEffects.GetModifiedCS(diff.CircleSize);
            diff.ApproachRate = modEffects.GetModifiedAR(diff.ApproachRate);

            Debug.Log($"[Mod] 已应用难度修改: CS={diff.CircleSize:F1}, AR={diff.ApproachRate:F1}");
        }

        /// <summary>
        /// HR Mod: Y轴镜像翻转所有物件位置
        /// </summary>
        private void ApplyHardRockMirror()
        {
            if (hitObjects == null) return;

            const float OSU_HEIGHT = 384f;
            const float CENTER_Y = OSU_HEIGHT / 2f;

            foreach (var obj in hitObjects)
            {
                // 1. 翻转头部坐标 (头部是绝对坐标，用 384 - y 是对的)
                Vector2 pos = obj.Position;
                pos.y = 2f * CENTER_Y - pos.y;
                obj.Position = pos;

                // 2. 翻转滑条控制点
                if (obj is SliderObject slider && slider.ControlPoints != null)
                {
                    for (int i = 0; i < slider.ControlPoints.Count; i++)
                    {
                        Vector2 cp = slider.ControlPoints[i];
                        
                        // 【核心修复】：因为控制点是相对坐标（偏移量）
                        // 往下偏移变成往上偏移，只需要对 Y 取反，不需要减去 384
                        cp.y = -cp.y;
                        
                        slider.ControlPoints[i] = cp;
                    }
                }
            }

            Debug.Log($"[Mod] HR Y轴镜像翻转完成，共处理 {hitObjects.Count} 个物件，已修复滑条相对坐标翻转");
        }

        /// <summary>
        /// 获取当前应用的 CS (考虑 Mod)
        /// </summary>
        public float GetCurrentCS()
        {
            if (currentBeatmap == null || currentBeatmap.Difficulty == null)
                return 5f;

            float baseCS = currentBeatmap.Difficulty.CircleSize;
            return modEffects != null ? modEffects.GetModifiedCS(baseCS) : baseCS;
        }

        /// <summary>
        /// 获取固定判定窗口 (250ms)
        /// </summary>
        public static double GetFixedHitWindow()
        {
            // OD 固定为 250ms 判定窗口
            return 250.0;
        }

        /// <summary>
        /// 缓冲期结束，正式开始游戏
        /// </summary>
        private void StartGamePlay()
        {
            if (isPlaying) return; // 防止重复调用

            Debug.Log("缓冲期结束，正式开始游戏！");

            isPlaying = true;
            bufferStartDspTime = 0; // 清除缓冲期开始时间

            // 重置下一个音符索引
            nextNoteIndex = 0;

            if (useAutoPlay && autoPlayManager != null)
            {
                autoPlayManager.enabled = true;
                Debug.Log("<color=cyan>[AutoPlay] AI 已接管控制权</color>");
            }

            Debug.Log($"游戏正式开始，总音符数: {totalNotes}");
        }

        /// <summary>
        /// 重置游戏状态
        /// </summary>
        private void ResetGameState()
        {
            nextNoteIndex = 0;
            currentMusicTimeMs = 0;
            isPlaying = false;
            isMusicPlaying = false;
            
            // 修复：除以 speedMultiplier
            countdownTime = preparationTime + ((spawnOffsetMs / 1000.0) / speedMultiplier);
            
            spawnedNotes = 0;
            activeNotes = 0;
        }

        /// <summary>
        /// 暂停游戏
        /// </summary>
        public void PauseGame()
        {
            if (!isPlaying) return;

            isPlaying = false;

            if (musicSource.isPlaying)
            {
                musicSource.Pause();
            }

            Debug.Log("游戏已暂停");
        }
        /// <summary>
        /// 恢复游戏
        /// </summary>
        public void ResumeGame()
        {
            if (isPlaying) return;

            isPlaying = true;

            if (!musicSource.isPlaying && musicSource.time > 0)
            {
                musicSource.UnPause();
            }

            Debug.Log("游戏已恢复");
        }

        /// <summary>
        /// 重新开始游戏
        /// </summary>
        public void RestartGame()
        {
            StopGame();
            StartGame();
            Debug.Log("游戏已重新开始");
        }

        /// <summary>
        /// 初始化暂停菜单UI
        /// </summary>
        private void InitializePauseMenu()
        {
            // 优先使用Prefab模式
            if (pauseMenuPrefab != null)
            {
                isUsingPrefab = true;
                Debug.Log("[RhythmGameManager] 使用VR暂停菜单Prefab模式");
                return;
            }

            // 其次使用实例模式
            if (pauseMenuInstance != null)
            {
                isUsingPrefab = false;
                currentPauseMenu = pauseMenuInstance;
                Debug.Log("[RhythmGameManager] 使用场景中的VR暂停菜单实例");
                return;
            }

            // 尝试查找场景中已有的暂停菜单
            var existingPauseMenu = FindObjectOfType<VRPauseMenu>();
            if (existingPauseMenu != null)
            {
                isUsingPrefab = false;
                currentPauseMenu = existingPauseMenu;
                Debug.Log("[RhythmGameManager] 找到场景中的VR暂停菜单");
                return;
            }

            Debug.LogWarning("[RhythmGameManager] 未设置暂停菜单Prefab或实例，暂停功能将降级为基本暂停/恢复");
        }



        /// <summary>
        /// 初始化暂停菜单输入
        /// </summary>
        private void InitializePauseInput()
        {
            // 创建Cancel输入动作
            cancelInputAction = new InputAction("Cancel", binding: "*/{Cancel}");
            cancelInputAction.Enable();
            
            if (cancelInputAction != null)
            {
                cancelInputAction.performed += OnCancelAction;
                Debug.Log("[RhythmGameManager] 已绑定菜单按钮输入");
            }
            else
            {
                Debug.LogWarning("[RhythmGameManager] 未找到Cancel输入动作，暂停功能可能不可用");
            }
        }

        /// <summary>
        /// 处理菜单按钮按下
        /// </summary>
        private void OnCancelAction(InputAction.CallbackContext context)
        {
            if (context.phase == InputActionPhase.Performed)
            {
                HandlePauseToggle();
            }
        }

        /// <summary>
        /// 切换暂停状态
        /// </summary>
        private void HandlePauseToggle()
        {
            // 检查是否可以暂停（游戏正在进行且未结束）
            if (!isPlaying || isGameEnded)
                return;

            // 使用Prefab模式
            if (isUsingPrefab)
            {
                if (currentPauseMenu != null && currentPauseMenu.IsPaused())
                {
                    // 如果已经在暂停状态，继续游戏
                    currentPauseMenu.HidePauseMenu();
                    // 回收到对象池（这里简单Destroy，实际项目建议用对象池）
                    Destroy(currentPauseMenu.gameObject);
                    currentPauseMenu = null;
                    ResumeGame();
                }
                else
                {
                    // 实例化暂停菜单Prefab
                    if (pauseMenuPrefab != null)
                    {
                        GameObject pauseMenuObj = Instantiate(pauseMenuPrefab);
                        currentPauseMenu = pauseMenuObj.GetComponent<VRPauseMenu>();
                        if (currentPauseMenu != null)
                        {
                            currentPauseMenu.ShowPauseMenu();
                        }
                        else
                        {
                            // 如果Prefab没有VRPauseMenu组件，直接暂停
                            PauseGame();
                        }
                    }
                    else
                    {
                        // 降级为基本暂停
                        PauseGame();
                    }
                }
            }
            // 使用实例模式
            else if (currentPauseMenu != null)
            {
                if (currentPauseMenu.IsPaused())
                {
                    // 如果已经在暂停状态，继续游戏
                    currentPauseMenu.HidePauseMenu();
                    ResumeGame();
                }
                else
                {
                    // 显示暂停菜单
                    currentPauseMenu.ShowPauseMenu();
                }
            }
            else
            {
                // 如果没有暂停菜单，直接暂停/恢复
                if (isMusicPlaying)
                {
                    PauseGame();
                }
                else
                {
                    ResumeGame();
                }
            }
        }

        /// <summary>
        /// 加载osu谱面文件
        /// </summary>
        /// <summary>
        /// 加载osu谱面文件
        /// </summary>
        private void LoadBeatmap()
        {
            Debug.Log($"开始加载谱面文件: {osuFileName}");

            // 1. 修正路径：指向 Assets/Songs
            string filePath = System.IO.Path.Combine(Application.dataPath, "Songs", osuFileName);

            // 检查文件是否存在
            if (!System.IO.File.Exists(filePath))
            {
                Debug.LogError($"谱面文件不存在: {filePath}");
                // 如果没有文件，创建测试数据
                CreateTestBeatmap(new Beatmap());
                return;
            }

            try
            {
                // 使用新的 OsuParser.Parse 静态方法解析整个文件
                currentBeatmap = OsuParser.Parse(filePath);
                StackingProcessor.ApplyStacking(currentBeatmap);

                // 获取 HitObjects 列表用于游戏逻辑
                hitObjects = currentBeatmap.HitObjects;
                totalNotes = hitObjects.Count;

                // 统计不同类型的音符数量
                int hitCircleCount = 0;
                int sliderCount = 0;
                int spinnerCount = 0;

                // 如果谱面没有定义 ComboColors，则使用默认颜色
                if (currentBeatmap.ComboColors == null || currentBeatmap.ComboColors.Count == 0)
                {
                    currentBeatmap.ComboColors = new List<Color> {
                        new Color(1f, 0.4f, 0.4f), // 红
                        new Color(0.4f, 0.6f, 1f), // 蓝
                        new Color(0.4f, 1f, 0.4f), // 绿
                        new Color(1f, 0.8f, 0.4f)  // 黄
                     };
                }
                // 统计音符类型
                foreach (var obj in hitObjects)
                {
                    if (obj is HitCircle) hitCircleCount++;
                    else if (obj is SliderObject) sliderCount++;
                    else if (obj is SpinnerObject) spinnerCount++;
                }
                // 根据谱面中的AR设置计算spawnOffsetMs
                if (currentBeatmap != null && currentBeatmap.Difficulty != null)
                {
                    // === 注释掉这一行！防止CS和AR被乘两次 ===
                    // ApplyModEffectsToBeatmap();

                    float baseAR = currentBeatmap.Difficulty.ApproachRate;
                    float modifiedAR = modEffects != null ? modEffects.GetModifiedAR(baseAR) : baseAR;
                    spawnOffsetMs = (float)CalculateTimePreempt(modifiedAR);

                    Debug.Log($"[AR System] Base AR: {baseAR}, Modified AR: {modifiedAR}, TimePreempt: {spawnOffsetMs}ms");
                }
                // [新增/核心修复] 2. 立即将 AR 时间应用到所有音符
                // 这样 SpawnNotes 里的 (StartTime - TimePreempt) 才能算出正确的生成时间
                foreach (var obj in hitObjects)
                {
                    obj.TimePreempt = spawnOffsetMs;
                }

                // HR Mod: Y轴镜像翻转
                if (modEffects != null && modEffects.IsHardRock)
                {
                    ApplyHardRockMirror();
                }

                Debug.Log($"✅ 谱面加载完成: {currentBeatmap.Metadata.Title} - {currentBeatmap.Metadata.Version}");
                Debug.Log($"  Audio: {currentBeatmap.General.AudioFilename}");
                Debug.Log($"  CS:{currentBeatmap.Difficulty.CircleSize} AR:{currentBeatmap.Difficulty.ApproachRate}");
                Debug.Log($"  总音符: {totalNotes} (圈:{hitCircleCount}, 滑:{sliderCount}, 转:{spinnerCount})");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"解析失败: {e.Message}\n{e.StackTrace}");
                // 出错时回退到测试谱面
                CreateTestBeatmap(new Beatmap());
            }
        }

        /// <summary>
        /// 创建测试谱面数据
        /// </summary>
        private void CreateTestBeatmap(Beatmap beatmap)
        {
            Debug.Log("创建测试谱面数据...");

            beatmap.Metadata.Title = "Test Beatmap";
            beatmap.Metadata.Artist = "Unity Developer";
            beatmap.Metadata.Version = "Debug Difficulty";
            beatmap.Difficulty.ApproachRate = 9;
            currentBeatmap = beatmap;

            // 创建打击圈
            beatmap.HitObjects.Add(new HitCircle(1000, new Vector2(256, 192), true, 0));
            beatmap.HitObjects.Add(new HitCircle(2000, new Vector2(50, 50), false, 0));
            beatmap.HitObjects.Add(new HitCircle(3000, new Vector2(462, 334), false, 0));

            // 创建滑条（简单直线）
            List<Vector2> sliderPoints = new List<Vector2> { Vector2.zero, new Vector2(256, 192) };

            beatmap.HitObjects.Add(new SliderObject(
                startTime: 4000,
                position: new Vector2(128, 96),
                curveType: CurveType.Linear,    // 补上缺失的曲线类型
                controlPoints: sliderPoints,    // 必须是 List<Vector2>
                repeatCount: 1,
                pixelLength: 100,
                isNewCombo: true,               // bool
                comboOffset: 0              // int
            ));

            // 创建转盘
            beatmap.HitObjects.Add(new SpinnerObject(7000, 10000, true));

            hitObjects = new List<HitObject>(beatmap.HitObjects);
            totalNotes = hitObjects.Count;

            Debug.Log($"测试谱面创建完成，共 {totalNotes} 个音符");
        }

        /// <summary>
        /// 生成音符
        /// </summary>
        private void SpawnNotes()
        {
            // 修改点：即使 isPlaying 为 false，只要处于缓冲期 (bufferStartDspTime > 0) 也要运行
            bool isBufferPhase = !isPlaying && bufferStartDspTime > 0;

            // 如果既不是进行中，也不是缓冲期，或者是数据为空，才返回
            if ((!isPlaying && !isBufferPhase) || hitObjects == null || nextNoteIndex >= hitObjects.Count)
                return;

            // ---------------------------------------------------------
            // 机制1: 全局基准线与防下穿机制
            // 目标：防止长歌曲的渲染队列掉入 2000 以下的天空盒层
            // ---------------------------------------------------------
            // 1. 当屏幕上没有存活音符时，重置基准线
            if (activeNotes == 0)
            {
                currentRenderBaseline = nextNoteIndex;
            }
            // 2. 长图防暴毙锁：连续 250 个音符不断档，强行重置
            else if (nextNoteIndex - currentRenderBaseline > 80)
            {
                currentRenderBaseline = nextNoteIndex;
            }

            // 遍历尚未生成的音符
            while (nextNoteIndex < hitObjects.Count)
            {
                HitObject hitObject = hitObjects[nextNoteIndex];

                // 计算生成时间（使用每个物件的TimePreempt作为提前量）
                double spawnTime = hitObject.StartTime - hitObject.TimePreempt;

                // 检查音符是否已经过期（超过250ms未生成则视为Miss）
                if (currentMusicTimeMs > hitObject.StartTime + 250f)
                {
                    Debug.LogWarning($"[Manager] 丢弃过期音符: {hitObject.StartTime}ms (当前: {currentMusicTimeMs:F0})");
                    if (scoreManager != null) scoreManager.RegisterMiss(300);
                    nextNoteIndex++;
                    continue;
                }

                // 比较当前音乐时间（注意：在缓冲期 currentMusicTimeMs 是负数，这正好能对应上）
                if (currentMusicTimeMs >= spawnTime)
                {
                    HitObject nextObject = null;
                    // 检查列表里是否还有下一个
                    if (nextNoteIndex + 1 < hitObjects.Count)
                    {
                        nextObject = hitObjects[nextNoteIndex + 1];
                    }

                    // 机制1: 计算安全渲染索引（相对绝对索引）
                    int safeRenderIndex = nextNoteIndex - currentRenderBaseline;
                    SpawnNoteByType(hitObject, safeRenderIndex, nextObject);
                    
                    nextNoteIndex++;
                    spawnedNotes++;
                }
                else
                {
                    break;
                }
            }
        }

        private void SpawnNoteByType(HitObject hitObject, int renderIndex, HitObject nextHitObject = null)
        {
            // 1. 获取对象池管理器
            var poolMgr = NotePoolManager.Instance;
            if (poolMgr == null) { Debug.LogError("PoolManager 没挂载！"); return; }

            GameObject noteObject = null;

            // 2. 颜色计算 (保持不变)
            Color comboColor = Color.white;
            if (currentBeatmap.ComboColors != null && currentBeatmap.ComboColors.Count > 0)
            {
                comboColor = currentBeatmap.ComboColors[hitObject.ComboIndex % currentBeatmap.ComboColors.Count];
            }

            // 3. CS 计算 (使用 Mod 修改后的 CS)
            float currentCS = GetCurrentCS();

            // 3.5 . 下一个音符位置 (保持不变)
            Vector3? nextNoteWorldPos = null;
            if (nextHitObject != null)
            {
                // 只需要 X/Y 平面的方向，Z轴(Stacking)的微小差异对粒子方向影响不大，直接用 MapToWorld 即可
                nextNoteWorldPos = CoordinateMapper.MapToWorld(nextHitObject.Position);
            }

            // 4. 从池中获取对象 & 初始化
            // ------------------------------------------------------
            if (hitObject is HitCircle)
            {
                noteObject = poolMgr.CirclePool.Get();
                // 🛑 【核心修复】如果池子给了个已销毁的坏对象，手动生成一个新的
                if (noteObject == null) noteObject = Instantiate(poolMgr.hitCirclePrefab);

                // 确保它是激活的
                if (!noteObject.activeSelf) noteObject.SetActive(true);

                PatchNoteVisuals(noteObject);
                var controller = noteObject.GetComponent<NoteController>();
                if (controller != null)
                {
                    Vector3 targetPosition = CoordinateMapper.MapToWorld(hitObject.Position);
                    controller.Initialize(hitObject, targetPosition, noteSpeed, currentCS, comboColor, this, poolMgr.CirclePool, renderIndex, nextNoteWorldPos);
                }
            }
            else if (hitObject is SliderObject)
            {
                noteObject = poolMgr.SliderPool.Get();
                // 🛑 【核心修复】
                if (noteObject == null) noteObject = Instantiate(poolMgr.sliderPrefab);

                if (!noteObject.activeSelf) noteObject.SetActive(true);
                var controller = noteObject.GetComponent<SliderController>();

                if (globalGlowMaterial != null)
                {
                    controller.sharedMaterial = globalGlowMaterial;

                    // 还要确保当前的 MeshRenderer 也换了 (如果是复用对象)
                    PatchMaterial(noteObject);
                }

                if (controller != null)
                {
                    controller.Initialize((SliderObject)hitObject, currentCS, comboColor, this, poolMgr.SliderPool, poolMgr.TickPool, renderIndex, nextNoteWorldPos);
                }
            }
            else if (hitObject is SpinnerObject)
            {
                noteObject = poolMgr.SpinnerPool.Get();
                // 🛑 【核心修复】
                if (noteObject == null) noteObject = Instantiate(poolMgr.spinnerPrefab);

                if (!noteObject.activeSelf) noteObject.SetActive(true);
                Vector2 osuCenter = new Vector2(256, 192);

                Vector3 worldCenter = CoordinateMapper.MapToWorld(osuCenter);

                noteObject.transform.position = worldCenter;
                float od = (currentBeatmap != null && currentBeatmap.Difficulty != null) ? currentBeatmap.Difficulty.OverallDifficulty: 5f;
                var controller = noteObject.GetComponent<SpinnerController>();
                if (controller != null)
                {
                    controller.Initialize((SpinnerObject)hitObject, this, poolMgr.SpinnerPool, worldCenter,od);
                }
            }
            else
            {
                return;
            }

            // 5. 设置名称 (方便调试，不再 Instantiate 所以要手动改名)
            if (noteObject != null)
            {
                noteObject.name = $"{hitObject.GetType().Name}_{hitObject.StartTime}ms";

                // 6. 添加到活动列表
                activeNoteObjects[hitObject] = noteObject;
                activeNotes = activeNoteObjects.Count;
            }
            // 物件的销毁现在完全由 Controller 在 Update/OnHit/OnMiss 中通过 Pool.Release() 接管
        }

        /// <summary>
        /// 清理所有活动音符 (对象池版)
        /// </summary>
        private void ClearAllNotes()
        {
            var poolMgr = NotePoolManager.Instance;

            // 遍历所有当前活动的音符
            foreach (var kvp in activeNoteObjects)
            {
                HitObject hitObject = kvp.Key;
                GameObject obj = kvp.Value;

                if (obj != null && obj.activeInHierarchy)
                {
                    // 根据 HitObject 的类型，归还到正确的池子
                    if (poolMgr != null)
                    {
                        if (hitObject is HitCircle)
                        {
                            poolMgr.CirclePool.Release(obj);
                        }
                        else if (hitObject is SliderObject)
                        {
                            poolMgr.SliderPool.Release(obj);
                        }
                        else if (hitObject is SpinnerObject)
                        {
                            poolMgr.SpinnerPool.Release(obj);
                        }
                        else
                        {
                            // 未知类型，直接 Disable (兜底)
                            obj.SetActive(false);
                        }
                    }
                    else
                    {
                        // 如果池管理器没了（比如退出游戏时），直接销毁
                        Destroy(obj);
                    }
                }
            }

            activeNoteObjects.Clear();
            activeNotes = 0;
        }

        /// <summary>
        /// 更新调试信息
        /// </summary>
        private void UpdateDebugInfo()
        {
            // 这里可以添加更多调试信息
            // 例如显示在UI上
        }

        /// <summary>
        /// 获取游戏进度百分比
        /// </summary>
        public float GetProgress()
        {
            if (hitObjects.Count == 0) return 0f;

            return (float)nextNoteIndex / hitObjects.Count;
        }

        /// <summary>
        /// 当音符被击打时调用
        /// </summary>
        public void OnNoteHit(HitObject hitObject, double timeDiff)
        {
            if (activeNoteObjects.ContainsKey(hitObject))
            {
                GameObject noteObject = activeNoteObjects[hitObject];
                activeNoteObjects.Remove(hitObject);
                activeNotes = activeNoteObjects.Count;
                // ✅ [新增] 视觉反馈
                // 1. 确定判定位置 (使用物体位置)
                Vector3 hitPos = noteObject.transform.position;
                // 2. 确定颜色
                Color comboColor = (currentBeatmap.ComboColors != null && currentBeatmap.ComboColors.Count > 0)
                    ? currentBeatmap.ComboColors[hitObject.ComboIndex % currentBeatmap.ComboColors.Count]
                    : Color.white;


                if (scoreManager != null)
                {
                    // ✅ [修复核心]：将毫秒误差转换为 0-1 的准确率
                    // 1.0 = 完美 (0ms误差), 0.0 = 极限 (250ms误差)
                    double maxWindow = 250.0; // 对应 NoteController 的 hitWindow
                    double absDiff = System.Math.Abs(timeDiff);

                    // 计算公式：1 - (误差 / 最大宽容度)
                    double accuracy01 = 1.0 - (absDiff / maxWindow);

                    // 钳制范围，防止负数
                    accuracy01 = System.Math.Clamp(accuracy01, 0.0, 1.0);

                    // 特殊情况：如果是滑条或转盘，它们传进来的本身就是百分比 (0~1 或 状态码)，直接信任
                    if (hitObject is SliderObject || hitObject is SpinnerObject)
                    {
                        accuracy01 = timeDiff;
                    }

                    // 现在传入的是正确的 0~1 值，可以正确计分了
                    int scoreValue = CalculateScoreFromAccuracy(accuracy01);
                    if (scoreManager != null)
                    {
                        scoreManager.RegisterHit(scoreValue);
                    }

                    if (JudgementVisualizer.Instance != null && !(hitObject is SliderObject))
                    {
                        if (currentBeatmap != null && currentBeatmap.ComboColors != null && currentBeatmap.ComboColors.Count > 0)
                        {
                            comboColor = currentBeatmap.ComboColors[hitObject.ComboIndex % currentBeatmap.ComboColors.Count];
                        }

                        // 显示判定！
                        JudgementVisualizer.Instance.ShowJudgement(hitPos, scoreValue, comboColor);
                    }



                }
                Debug.Log($"击打音符: 误差={timeDiff:F1}ms, 转换后Acc={timeDiff:F2}");
            }
        }

        /// <summary>
        /// 当音符错过时调用
        /// </summary>
        public void OnNoteMiss(HitObject hitObject)
        {
            if (activeNoteObjects.ContainsKey(hitObject))
            {
                // 1. 移除对象
                GameObject obj = activeNoteObjects[hitObject];
                activeNoteObjects.Remove(hitObject);
                activeNotes = activeNoteObjects.Count;
                Vector3 missPos = obj.transform.position;


                // 2. 只有在这里才通知 ScoreManager 记 Miss
                if (scoreManager != null && !(hitObject is SliderObject))
                {
                    scoreManager.RegisterMiss(300);
                }
                // 3. 显示 Miss 判定
                if (JudgementVisualizer.Instance != null && !(hitObject is SliderObject))
                {
                    JudgementVisualizer.Instance.ShowJudgement(missPos, 0, Color.red);
                }
            }
            else
            {
                // 如果列表里没有，说明已经被打中了(Hit)，或者是重复调用
                // 这种情况下，什么都不做！绝对不能判 Miss！
                Debug.LogWarning("拦截了一次无效的 Miss 判定 (对象已不在活动列表)");
            }
        }

        /// <summary>
        /// 当转盘完成时调用
        /// </summary>
        public void OnSpinnerCompleted(SpinnerObject spinnerObject)
        {
            Debug.Log($"转盘完成: 开始时间={spinnerObject.StartTime}ms");

            if (activeNoteObjects.ContainsKey(spinnerObject))
            {
                activeNoteObjects.Remove(spinnerObject);
                activeNotes = activeNoteObjects.Count;
            }

            if (scoreManager != null)
            {
                
            }

        }

        public static int CalculateScoreFromAccuracy(double accuracy01)
        {
            // accuracy01: 1.0 是完美重合，0.0 是判定边缘
            // 相当于：
            // > 0.8 (误差 < 50ms) -> 300
            // > 0.6 (误差 < 100ms) -> 100
            // > 0.2 (误差 < 200ms) -> 50

            if (accuracy01 >= 0.8f) return 300;
            if (accuracy01 >= 0.6f) return 100;
            if (accuracy01 >= 0.01f) return 50; // 只要不是 0，至少给 50 分
            return 0; // 只有真的是 0 (误差 > 250ms) 才判 Miss
        }

        /// <summary>
        /// 获取格式化时间字符串
        /// </summary>
        public string GetFormattedTime()
        {
            if (countdownTime > 0)
            {
                // 倒计时阶段
                if (countdownTime > 3)
                {
                    return $"准备中... {countdownTime:F1}";
                }
                else
                {
                    return $"{(int)Mathf.Ceil((float)countdownTime)}";
                }
            }
            else if (currentMusicTimeMs < 0)
            {
                // 游戏开始前的负时间
                return $"{currentMusicTimeMs:F0}";
            }
            else
            {
                // 音乐播放中
                int minutes = (int)(currentMusicTimeMs / 60000);
                int seconds = (int)((currentMusicTimeMs % 60000) / 1000);
                int milliseconds = (int)(currentMusicTimeMs % 1000);
                return $"{minutes:00}:{seconds:00}.{milliseconds:000}";
            }
        }

        /// <summary>
        /// 在编辑器中显示调试按钮和信息
        /// </summary>
        /// <summary>
        /// 在编辑器中显示调试按钮和信息
        /// </summary>
        void OnGUI()
        {
            if (!Application.isPlaying) return;

            // [修改] 增加高度到 500 以容纳更多信息
            GUILayout.BeginArea(new Rect(10, 10, 350, 500));

            GUILayout.Label("=== 节奏游戏调试 (多类型音符) ===");

            // [新增] 显示谱面元数据
            if (currentBeatmap != null && currentBeatmap.Metadata != null)
            {
                GUILayout.Space(5);
                GUILayout.Label($"曲名: {currentBeatmap.Metadata.Title}");
                GUILayout.Label($"艺术家: {currentBeatmap.Metadata.Artist}");
                GUILayout.Label($"难度: {currentBeatmap.Metadata.Version} (by {currentBeatmap.Metadata.Creator})");
                GUILayout.Label($"参数: CS:{currentBeatmap.Difficulty.CircleSize} AR:{currentBeatmap.Difficulty.ApproachRate} OD:{currentBeatmap.Difficulty.OverallDifficulty} HP:{currentBeatmap.Difficulty.HPDrainRate}");
                GUILayout.Label($"BPM点: {currentBeatmap.ControlPoints?.Timing?.Count ?? 0} | 滑条倍率: {currentBeatmap.Difficulty.SliderMultiplier}");
                GUILayout.Space(5);
            }

            GUILayout.Label($"游戏状态: {(isPlaying ? "进行中" : (bufferStartDspTime > 0 ? "准备中" : "未开始"))}");
            GUILayout.Label($"音乐状态: {(isMusicPlaying ? "播放中" : "未播放")}");
            GUILayout.Label($"当前时间: {GetFormattedTime()}");
            GUILayout.Label($"DSP开始时间: {dspStartTime:F3}s");
            GUILayout.Label($"音符: {spawnedNotes}/{totalNotes} 已生成");
            GUILayout.Label($"活动音符: {activeNotes}");
            GUILayout.Label($"进度: {GetProgress():P0}");

            if (countdownTime > 0)
            {
                GUILayout.Label($"倒计时: {countdownTime:F2}s");
            }

            GUILayout.Space(10);

            if (!isPlaying && bufferStartDspTime == 0 && GUILayout.Button("开始游戏 (空格键)"))
            {
                StartGame();
            }

            if (bufferStartDspTime > 0 && !isPlaying && GUILayout.Button("跳过准备"))
            {
                StartGamePlay();
            }

            if (isPlaying && GUILayout.Button("暂停游戏"))
            {
                PauseGame();
            }

            if (!isPlaying && nextNoteIndex > 0 && GUILayout.Button("恢复游戏"))
            {
                ResumeGame();
            }

            if (isPlaying && GUILayout.Button("停止游戏"))
            {
                StopGame();
            }

            GUILayout.Space(10);
            GUILayout.Label("控制说明:");
            GUILayout.Label("• 空格键: 开始游戏");
            GUILayout.Label("• P: 暂停/恢复 (需自行实现)");

            GUILayout.EndArea();
        }

        /// <summary>
        /// 停止游戏
        /// </summary>
        private void StopGame()
        {
            isPlaying = false;
            isMusicPlaying = false;

            if (musicSource.isPlaying)
            {
                musicSource.Stop();
            }

            if (autoPlayManager != null)
            {
                autoPlayManager.enabled = false;
            }

            ClearAllNotes();

            Debug.Log("游戏已停止");
        }

        /// <summary>
        /// 当组件被销毁时清理资源
        /// </summary>
        void OnDestroy()
        {
            // 清理Cancel输入动作
            if (cancelInputAction != null)
            {
                cancelInputAction.performed -= OnCancelAction;
                cancelInputAction.Disable();
                cancelInputAction.Dispose();
            }

            ClearAllNotes();

            if (musicSource != null && musicSource.isPlaying)
            {
                musicSource.Stop();
            }
        }

        /// <summary>
        /// 获取当前音乐时间（毫秒） - 供所有Controller使用
        /// </summary>
        public double GetCurrentMusicTimeMs()
        {
            // 如果没有在播放且没有处于缓冲期
            if (!isPlaying && bufferStartDspTime <= 0)
            {
                return -spawnOffsetMs;
            }

            if (dspStartTime > 0)
            {
                double currentDspTime = AudioSettings.dspTime;
                // 修复: 必须乘上 speedMultiplier，并减去全局偏移
                return (((currentDspTime - dspStartTime) * 1000.0) * speedMultiplier) - universalOffsetMs;
            }

            return 0;
        }

        public static float CalculateVROsuSize(float cs)
        {
            // 1. 调整 CS 范围：将 osu! 的 CS 范围 (2-7) 映射到 (0-5)
            float adjustedCS = cs - 2f;

            // 2. 标准 osu! 比例换算
            float rawScale = (1.0f - 0.7f * (adjustedCS - 5f) / 5f);

            // 3. VR 物理尺寸补正 (保留你觉得舒服的参数)
            // 基准值 0.11f (11cm) * 1.15 倍
            float baseWorldSize = 0.11f * 1.15f;

            float finalSize = rawScale * baseWorldSize;

            // 4. 照顾 VR 下限
            // 无论 CS 多高，物件直径不能低于 7cm
            return Mathf.Max(finalSize, 0.07f);
        }

        /// <summary>
        /// 材质补丁：强行把物体换成发光材质 (已修复暴力替换导致的白块Bug)
        /// </summary>
        private void PatchMaterial(GameObject obj)
        {
            if (globalGlowMaterial == null || obj == null) return;

            // 1. 尝试获取 MeshRenderer (替换滑条本体)
            MeshRenderer mr = obj.GetComponent<MeshRenderer>();
            if (mr != null && mr.sharedMaterial != globalGlowMaterial)
            {
                mr.sharedMaterial = globalGlowMaterial;
            }

            // 2. ✅ 双重保险：不要用 GetComponentsInChildren(true) 暴力替换！
            // 因为 SliderBorder 是由代码动态生成的子物体，我们精准查找到它并替换即可，放过 Tick。
            Transform border = obj.transform.Find("SliderBorder");
            if (border != null)
            {
                Renderer borderRenderer = border.GetComponent<Renderer>();
                if (borderRenderer != null) borderRenderer.sharedMaterial = globalGlowMaterial;
            }
        }

        /// <summary>
        /// 精确材质补丁：只替换 Body 和 Overlay，放过缩圈
        /// </summary>
        private void PatchNoteVisuals(GameObject noteRoot)
        {
            if (globalGlowMaterial == null || noteRoot == null) return;

            // 1. 尝试查找名为 "Body" 的子物体
            Transform bodyTrans = noteRoot.transform.Find("Body");
            if (bodyTrans != null)
            {
                MeshRenderer mr = bodyTrans.GetComponent<MeshRenderer>();
                if (mr != null) mr.sharedMaterial = globalGlowMaterial;
            }
            else
            {
                // 如果找不到 "Body" 子物体，可能根物体本身就是 Body
                // 检查根物体是否有 Renderer，且不是 Slider (Slider是动态生成的)
                MeshRenderer rootMr = noteRoot.GetComponent<MeshRenderer>();
                if (rootMr != null) rootMr.sharedMaterial = globalGlowMaterial;
            }

            // 2. 尝试查找名为 "Overlay" 的子物体
            Transform overlayTrans = noteRoot.transform.Find("Overlay");
            if (overlayTrans != null)
            {
                MeshRenderer mr = overlayTrans.GetComponent<MeshRenderer>();
                if (mr != null) mr.sharedMaterial = globalGlowMaterial;
            }

            // 3. 缩圈 (ApproachCircle) 和 文字 (Text) 
            // 因为我们没去查找它们，所以它们会保持原样！安全！
        }

        /// <summary>
        /// 根据文件扩展名获取 AudioType
        /// </summary>
        private AudioType GetAudioTypeFromPath(string path)
        {
            string extension = Path.GetExtension(path).ToLower();
            switch (extension)
            {
                case ".mp3": return AudioType.MPEG;
                case ".ogg": return AudioType.OGGVORBIS;
                case ".wav": return AudioType.WAV;
                case ".aiff": return AudioType.AIFF;
                default: return AudioType.UNKNOWN; // 让 Unity 自己猜
            }
        }


    }
}