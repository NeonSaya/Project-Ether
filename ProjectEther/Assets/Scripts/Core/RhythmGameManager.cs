using System.Collections.Generic;
using UnityEngine;

namespace OsuVR
{
    /// <summary>
    /// 节奏游戏管理器：控制游戏循环和音符生成
    /// </summary>
    public class RhythmGameManager : MonoBehaviour
    {
        [Header("谱面配置")]
        [Tooltip("osu谱面文件名（.osu文件）")]
        public string osuFileName = "map.osu";

        [Header("音符配置")]
        [Tooltip("音符预制体")]
        public GameObject notePrefab;

        [Tooltip("音符飞行速度（米/秒）")]
        public float noteSpeed = 5.0f;

        [Tooltip("音符提前生成时间（毫秒）")]
        public float spawnOffsetMs = 1000f;

        [Tooltip("音符生命周期（秒），超过此时间未击打则自动销毁")]
        public float noteLifetime = 3.0f;

        [Header("游戏状态")]
        [Tooltip("是否自动开始游戏")]
        public bool autoStart = true;

        [Tooltip("当前游戏时间（毫秒）")]
        public double currentMusicTimeMs = 0;

        [Tooltip("游戏是否正在进行")]
        public bool isPlaying = false;

        [Header("调试信息")]
        [SerializeField]
        private int totalNotes = 0;

        [SerializeField]
        private int spawnedNotes = 0;

        [SerializeField]
        private int activeNotes = 0;

        // 私有变量
        private List<HitObject> hitObjects = new List<HitObject>();
        private int nextNoteIndex = 0;
        private double gameStartTime = 0;

        // 已生成音符的缓存
        private Dictionary<HitObject, GameObject> activeNoteObjects = new Dictionary<HitObject, GameObject>();

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

            // 加载谱面
            LoadBeatmap();

            // 如果设置为自动开始，则启动游戏
            if (autoStart)
            {
                StartGame();
            }
        }

        /// <summary>
        /// 每帧更新游戏状态
        /// </summary>
        void Update()
        {
            // 如果游戏未进行，不执行更新
            if (!isPlaying) return;

            // 更新当前音乐时间（基于Unity游戏时间）
            // 注意：这里假设音乐与游戏同时开始，且没有暂停
            currentMusicTimeMs = (Time.time - gameStartTime) * 1000;

            // 生成应该出现的音符
            SpawnNotes();

            // 更新调试信息
            UpdateDebugInfo();
        }

        /// <summary>
        /// 加载osu谱面文件
        /// </summary>
        private void LoadBeatmap()
        {
            Debug.Log($"开始加载谱面文件: {osuFileName}");
            Beatmap beatmap = new Beatmap();

            // 1. 拼凑路径 (使用 DataPath)
            string filePath = System.IO.Path.Combine(Application.dataPath, "Songs", osuFileName);

            if (!System.IO.File.Exists(filePath))
            {
                Debug.LogError($"找不到文件: {filePath}，请检查 Assets/Songs 文件夹");
                return;
            }

            // 2. 读取所有行并解析 (这是真正读取数据的逻辑)
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
                    // 检测到下一个段落就停止
                    if (trim.StartsWith("[") && trim.EndsWith("]")) break;

                    // 调用解析器
                    OsuParser.ParseHitObject(trim, beatmap);
                }
            }

            // 3. 将解析出的数据赋值给列表
            hitObjects = new List<HitObject>(beatmap.HitObjects);
            totalNotes = hitObjects.Count;

            Debug.Log($"✅ 谱面加载完成，共读取到 {totalNotes} 个音符！");
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
        /// 开始游戏
        /// </summary>
        public void StartGame()
        {
            if (hitObjects == null || hitObjects.Count == 0)
            {
                Debug.LogError("无法开始游戏：没有加载到音符数据！");
                return;
            }

            Debug.Log("开始游戏！");

            // 重置游戏状态
            nextNoteIndex = 0;
            currentMusicTimeMs = 0;
            isPlaying = true;
            gameStartTime = Time.time;

            // 清理现有的音符
            ClearAllNotes();

            Debug.Log($"游戏开始，总音符数: {totalNotes}");
        }

        /// <summary>
        /// 暂停游戏
        /// </summary>
        public void PauseGame()
        {
            isPlaying = false;
            Debug.Log("游戏已暂停");
        }

        /// <summary>
        /// 恢复游戏
        /// </summary>
        public void ResumeGame()
        {
            isPlaying = true;
            Debug.Log("游戏已恢复");
        }

        /// <summary>
        /// 生成音符
        /// </summary>
        private void SpawnNotes()
        {
            // 遍历尚未生成的音符
            while (nextNoteIndex < hitObjects.Count)
            {
                HitObject hitObject = hitObjects[nextNoteIndex];

                // 计算这个音符应该生成的时间（提前spawnOffsetMs毫秒）
                double spawnTime = hitObject.StartTime - spawnOffsetMs;

                // 如果当前时间已经达到或超过了生成时间
                if (currentMusicTimeMs >= spawnTime)
                {
                    // 生成音符
                    SpawnNote(hitObject);

                    // 移动到下一个音符
                    nextNoteIndex++;
                    spawnedNotes++;
                }
                else
                {
                    // 如果当前音符还没到生成时间，后面的音符也肯定没到
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
            noteController.Initialize(hitObject, targetPosition, noteSpeed);

            // 设置自动销毁时间
            // 音符应该在击打时间后一段时间自动销毁
            double destroyTime = (hitObject.StartTime - currentMusicTimeMs) / 1000.0 + noteLifetime;
            Destroy(noteObject, (float)destroyTime);

            Debug.Log($"生成音符: 时间={hitObject.StartTime}ms, 位置={hitObject.Position}, 世界位置={targetPosition}");
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

                Debug.Log($"击打音符: 时间={hitObject.StartTime}ms, 准确度={accuracy}ms");
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
        /// 在编辑器中显示调试按钮
        /// </summary>
        void OnGUI()
        {
            if (!Application.isPlaying) return;

            GUILayout.BeginArea(new Rect(10, 10, 300, 200));
            GUILayout.Label("=== 节奏游戏调试 ===");
            GUILayout.Label($"游戏时间: {currentMusicTimeMs:F0}ms");
            GUILayout.Label($"音符: {spawnedNotes}/{totalNotes} 已生成");
            GUILayout.Label($"活动音符: {activeNotes}");
            GUILayout.Label($"进度: {GetProgress():P0}");

            GUILayout.Space(10);

            if (!isPlaying && GUILayout.Button("开始游戏"))
            {
                StartGame();
            }

            if (isPlaying && GUILayout.Button("暂停游戏"))
            {
                PauseGame();
            }

            if (!isPlaying && nextNoteIndex > 0 && GUILayout.Button("恢复游戏"))
            {
                ResumeGame();
            }

            GUILayout.EndArea();
        }

        /// <summary>
        /// 当组件被销毁时清理资源
        /// </summary>
        void OnDestroy()
        {
            ClearAllNotes();
        }
    }

    /// <summary>
    /// 音符控制器：控制音符的移动和碰撞检测
    /// </summary>
    public class NoteController : MonoBehaviour
    {
        [Header("音符配置")]
        public HitObject hitObject;
        public Vector3 targetPosition;
        public float moveSpeed = 5.0f;

        [Header("状态")]
        public bool isActive = true;
        public bool hasBeenHit = false;

        // 碰撞检测范围
        private const float hitRadius = 0.1f;

        /// <summary>
        /// 初始化音符
        /// </summary>
        public void Initialize(HitObject hitObj, Vector3 targetPos, float speed)
        {
            hitObject = hitObj;
            targetPosition = targetPos;
            moveSpeed = speed;
            isActive = true;
            hasBeenHit = false;
        }

        /// <summary>
        /// 每帧更新
        /// </summary>
        void Update()
        {
            if (!isActive) return;

            // 计算朝向目标位置的方向
            Vector3 direction = (targetPosition - transform.position).normalized;

            // 移动音符
            float distanceThisFrame = moveSpeed * Time.deltaTime;
            transform.position += direction * distanceThisFrame;

            // 如果接近目标位置，减慢速度或停止
            float distanceToTarget = Vector3.Distance(transform.position, targetPosition);

            if (distanceToTarget < 0.1f)
            {
                // 到达目标位置
                transform.position = targetPosition;

                // 可以在这里触发"错过"事件
                if (!hasBeenHit)
                {
                    OnMiss();
                }
            }

            // 始终面向玩家（假设玩家在原点）
            transform.LookAt(Vector3.zero);
        }

        /// <summary>
        /// 当被控制器击中时调用
        /// </summary>
        public void OnHit()
        {
            if (hasBeenHit) return;

            hasBeenHit = true;
            isActive = false;

            // 计算击打准确度
            double currentTime = Time.time * 1000; // 转换为毫秒
            double hitTime = hitObject.StartTime;
            double accuracy = currentTime - hitTime;

            // 触发击打事件
            Debug.Log($"音符被击打! 偏差: {accuracy}ms");

            // 播放击打效果
            // 这里可以添加粒子效果、声音等

            // 通知游戏管理器
            RhythmGameManager manager = FindObjectOfType<RhythmGameManager>();
            if (manager != null)
            {
                manager.OnNoteHit(hitObject, accuracy);
            }

            // 销毁自己（稍后一点，让效果播放完）
            Destroy(gameObject, 0.5f);
        }

        /// <summary>
        /// 当错过音符时调用
        /// </summary>
        private void OnMiss()
        {
            if (hasBeenHit) return;

            isActive = false;

            // 通知游戏管理器
            RhythmGameManager manager = FindObjectOfType<RhythmGameManager>();
            if (manager != null)
            {
                manager.OnNoteMiss(hitObject);
            }

            // 播放错过效果（例如淡出）
            // 这里可以添加淡出动画
        }

        /// <summary>
        /// 碰撞检测（用于VR控制器）
        /// </summary>
        void OnTriggerEnter(Collider other)
        {
            if (hasBeenHit || !isActive) return;

            // 检查是否是控制器
            if (other.CompareTag("VRController"))
            {
                OnHit();
            }
        }

        /// <summary>
        /// 在编辑器中显示Gizmos（调试用）
        /// </summary>
        void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying) return;

            // 绘制目标位置
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(targetPosition, 0.05f);

            // 绘制移动方向
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(transform.position, targetPosition);

            // 绘制击打范围
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, hitRadius);
        }
    }
}