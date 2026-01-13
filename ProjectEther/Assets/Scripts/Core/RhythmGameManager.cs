using System.Collections.Generic;
using UnityEngine;

namespace OsuVR
{
    /// <summary>
    /// 节奏游戏管理器：控制游戏循环和音符生成（使用AudioSource精准同步）
    /// </summary>
    public class RhythmGameManager : MonoBehaviour
    {
        [Header("谱面配置")]
        [Tooltip("osu谱面文件名（.osu文件）")]
        public string osuFileName = "map.osu";

        [Header("音乐配置")]
        [Tooltip("用于播放音乐的AudioSource")]
        public AudioSource musicSource;

        [Tooltip("音乐文件（会赋值给musicSource的clip）")]
        public AudioClip musicClip;

        [Header("音符配置")]
        [Tooltip("音符预制体")]
        public GameObject notePrefab;

        [Tooltip("音符飞行速度（米/秒）")]
        public float noteSpeed = 5.0f;

        [Tooltip("音符提前生成时间（毫秒）")]
        public float spawnOffsetMs = 1000f;

        [Tooltip("音符生命周期（秒），超过此时间未击打则自动销毁")]
        public float noteLifetime = 3.0f;

        [Header("游戏设置")]
        [Tooltip("准备时间缓冲（秒），音乐开始前给玩家的准备时间")]
        public float preparationTime = 2.0f;

        [Tooltip("是否自动开始游戏")]
        public bool autoStart = true;

        [Header("游戏状态")]
        [Tooltip("当前音乐时间（毫秒）")]
        public double currentMusicTimeMs = 0;

        [Tooltip("游戏是否正在进行")]
        public bool isPlaying = false;

        [Tooltip("音乐是否正在播放")]
        public bool isMusicPlaying = false;

        [Tooltip("倒计时时间（秒），负数表示游戏未开始")]
        public double countdownTime = 0;

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

        // 私有变量
        private List<HitObject> hitObjects = new List<HitObject>();
        private int nextNoteIndex = 0;

        // 已生成音符的缓存
        private Dictionary<HitObject, GameObject> activeNoteObjects = new Dictionary<HitObject, GameObject>();

        // 缓冲期开始的时间
        private double bufferStartDspTime = 0;

        /// <summary>
        /// 初始化游戏
        /// </summary>
        void Start()
        {
            Debug.Log($"开始初始化节奏游戏管理器...");

            // 检查必要组件
            if (notePrefab == null)
            {
                Debug.LogError("音符预制体未分配！请分配一个音符预制体。");
                return;
            }

            // 初始化AudioSource
            InitializeAudioSource();

            // 加载谱面
            LoadBeatmap();

            // 如果设置为自动开始，则启动游戏
            if (autoStart)
            {
                // 添加一个短暂的延迟，确保所有组件都初始化完成
                Invoke("StartGame", 1.0f);
            }
        }

        /// <summary>
        /// 初始化AudioSource
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
            }

            // 更新调试信息
            UpdateDebugInfo();
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

                    // 倒计时时间 = 总缓冲时间 - 已过的缓冲时间
                    countdownTime = preparationTime + (spawnOffsetMs / 1000.0) - elapsedBufferTime;

                    // 当前音乐时间为负的spawnOffset（倒计时）
                    currentMusicTimeMs = -(spawnOffsetMs - (elapsedBufferTime * 1000));
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
                    currentMusicTimeMs = (currentDspTime - dspStartTime) * 1000.0;

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

            Debug.Log("开始游戏设置...");

            // 重置游戏状态
            ResetGameState();

            // 计算DSP开始时间
            // dspStartTime = 当前DSP时间 + 准备时间 + spawnOffset（转换为秒）
            // 这样音乐会在准备时间 + spawnOffset秒后开始播放
            double currentDspTime = AudioSettings.dspTime;
            dspStartTime = currentDspTime + preparationTime + (spawnOffsetMs / 1000.0);

            // 记录缓冲期开始时间（用于倒计时）
            bufferStartDspTime = currentDspTime;

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
            countdownTime = preparationTime + (spawnOffsetMs / 1000.0);
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
                CreateTestBeatmap(new Beatmap()); // 这里的逻辑视情况保留或删除
                return;
            }

            try
            {
                // 2. 修正解析逻辑：使用静态解析器
                Beatmap beatmap = new Beatmap();
                string[] lines = System.IO.File.ReadAllLines(filePath);
                bool inHitObjects = false;

                foreach (string line in lines)
                {
                    string trim = line.Trim();
                    if (trim == "[HitObjects]")
                    {
                        inHitObjects = true;
                        continue;
                    }

                    if (inHitObjects && !string.IsNullOrEmpty(trim))
                    {
                        if (trim.StartsWith("[") && trim.EndsWith("]")) break;
                        // 调用静态方法
                        OsuParser.ParseHitObject(trim, beatmap);
                    }
                }

                hitObjects = new List<HitObject>(beatmap.HitObjects);
                totalNotes = hitObjects.Count;
                Debug.Log($"✅ 谱面加载完成，共 {totalNotes} 个音符");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"解析失败: {e.Message}");
            }
        }

        /// <summary>
        /// 创建测试谱面数据
        /// </summary>
        private void CreateTestBeatmap(Beatmap beatmap)
        {
            // 创建一些测试点击圆圈
            // 位置分布在osu游戏区域的不同位置

            // 中心点
            beatmap.HitObjects.Add(new HitCircle(1000, new Vector2(256, 192), true, 0));

            // 四个角落
            beatmap.HitObjects.Add(new HitCircle(2000, new Vector2(50, 50), false, 0));
            beatmap.HitObjects.Add(new HitCircle(3000, new Vector2(462, 50), false, 0));
            beatmap.HitObjects.Add(new HitCircle(4000, new Vector2(50, 334), false, 0));
            beatmap.HitObjects.Add(new HitCircle(5000, new Vector2(462, 334), false, 0));

            // 中间位置
            beatmap.HitObjects.Add(new HitCircle(6000, new Vector2(128, 96), true, 1));
            beatmap.HitObjects.Add(new HitCircle(7000, new Vector2(384, 96), false, 0));
            beatmap.HitObjects.Add(new HitCircle(8000, new Vector2(128, 288), false, 0));
            beatmap.HitObjects.Add(new HitCircle(9000, new Vector2(384, 288), false, 0));

            // 添加一些连击
            for (int i = 0; i < 5; i++)
            {
                float x = 100 + i * 70;
                float y = 150 + i * 20;
                double time = 10000 + i * 500;
                bool isNewCombo = (i == 0);

                beatmap.HitObjects.Add(new HitCircle(time, new Vector2(x, y), isNewCombo, 0));
            }
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

            // 遍历尚未生成的音符
            while (nextNoteIndex < hitObjects.Count)
            {
                HitObject hitObject = hitObjects[nextNoteIndex];

                // 计算生成时间
                double spawnTime = hitObject.StartTime - spawnOffsetMs;

                // 比较当前音乐时间（注意：在缓冲期 currentMusicTimeMs 是负数，这正好能对应上）
                if (currentMusicTimeMs >= spawnTime)
                {
                    SpawnNote(hitObject);
                    nextNoteIndex++;
                    spawnedNotes++;
                }
                else
                {
                    break;
                }
            }
        }

        /// <summary>
        /// 生成单个音符
        /// </summary>
        /// <param name="hitObject">击打对象</param>
        private void SpawnNote(HitObject hitObject)
        {
            // 使用CoordinateMapper将osu坐标转换为世界坐标
            Vector3 targetPosition = CoordinateMapper.MapToWorld(hitObject.Position);

            // 计算生成位置（在目标位置前方一定距离）
            // 让音符从远处飞向目标位置
            Vector3 spawnPosition = targetPosition + new Vector3(0, 0, 3.0f); // 从前方3米处开始

            // 实例化音符预制体
            GameObject noteObject = Instantiate(notePrefab, spawnPosition, Quaternion.identity);

            // 设置音符名称（便于调试）
            noteObject.name = $"Note_{hitObject.StartTime}ms";

            // 将音符添加到活动列表
            activeNoteObjects[hitObject] = noteObject;
            activeNotes = activeNoteObjects.Count;

            // 为音符添加NoteController组件（如果还没有的话）
            NoteController noteController = noteObject.GetComponent<NoteController>();
            if (noteController == null)
            {
                noteController = noteObject.AddComponent<NoteController>();
            }

            // 配置音符控制器
            noteController.Initialize(hitObject, targetPosition, noteSpeed, this);

            // 设置自动销毁时间
            // 音符应该在击打时间后一段时间自动销毁
            double timeUntilHit = hitObject.StartTime - currentMusicTimeMs;
            double destroyDelay = (timeUntilHit / 1000.0) + noteLifetime;

            if (destroyDelay > 0)
            {
                Destroy(noteObject, (float)destroyDelay);
            }
            else
            {
                // 如果已经过了击打时间，立即销毁
                Destroy(noteObject);
            }

            Debug.Log($"生成音符: 时间={hitObject.StartTime}ms, 位置={hitObject.Position}, 世界位置={targetPosition}, 距离击打还有{timeUntilHit:F0}ms");
        }

        /// <summary>
        /// 清理所有活动音符
        /// </summary>
        private void ClearAllNotes()
        {
            foreach (var kvp in activeNoteObjects)
            {
                if (kvp.Value != null)
                {
                    Destroy(kvp.Value);
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
        /// <returns>进度百分比 (0-1)</returns>
        public float GetProgress()
        {
            if (hitObjects.Count == 0) return 0f;

            return (float)nextNoteIndex / hitObjects.Count;
        }

        /// <summary>
        /// 当音符被击打时调用
        /// </summary>
        /// <param name="hitObject">被击打的音符对象</param>
        /// <param name="accuracy">击打准确度（毫秒偏差）</param>
        public void OnNoteHit(HitObject hitObject, double accuracy)
        {
            if (activeNoteObjects.ContainsKey(hitObject))
            {
                // 获取音符对象
                GameObject noteObject = activeNoteObjects[hitObject];

                // 从活动列表中移除
                activeNoteObjects.Remove(hitObject);
                activeNotes = activeNoteObjects.Count;

                // 播放击打效果（例如粒子效果）
                // 这里可以添加击打特效

                // 销毁音符
                Destroy(noteObject);

                Debug.Log($"击打音符: 时间={hitObject.StartTime}ms, 准确度={accuracy:F1}ms");
            }
        }

        /// <summary>
        /// 当音符错过时调用
        /// </summary>
        /// <param name="hitObject">错过的音符对象</param>
        public void OnNoteMiss(HitObject hitObject)
        {
            Debug.Log($"错过音符: 时间={hitObject.StartTime}ms");

            // 从活动列表中移除（即使已经自动销毁）
            if (activeNoteObjects.ContainsKey(hitObject))
            {
                activeNoteObjects.Remove(hitObject);
                activeNotes = activeNoteObjects.Count;
            }
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
        void OnGUI()
        {
            if (!Application.isPlaying) return;

            GUILayout.BeginArea(new Rect(10, 10, 350, 300));

            GUILayout.Label("=== 节奏游戏调试 (DSP时间同步) ===");
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

            ClearAllNotes();

            Debug.Log("游戏已停止");
        }

        /// <summary>
        /// 当组件被销毁时清理资源
        /// </summary>
        void OnDestroy()
        {
            ClearAllNotes();

            if (musicSource != null && musicSource.isPlaying)
            {
                musicSource.Stop();
            }
        }

        /// <summary>
        /// 获取当前音乐时间（毫秒） - 供NoteController使用
        /// </summary>
        public double GetCurrentMusicTimeMs()
        {
            if (!isPlaying)
            {
                return -spawnOffsetMs;
            }

            if (dspStartTime > 0)
            {
                double currentDspTime = AudioSettings.dspTime;
                return (currentDspTime - dspStartTime) * 1000.0;
            }

            return 0;
        }
    }
}