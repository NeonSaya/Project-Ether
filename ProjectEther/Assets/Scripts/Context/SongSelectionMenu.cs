using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OsuVR
{
    public class SongSelectionMenu : MonoBehaviour
    {
        [Header("配置")]
        public Transform listContent; // ScrollView 的 Content
        public GameObject songItemPrefab; // 刚才做的卡片预制体
        public string gameSceneName = "GameScene"; // 你的游戏场景名字

        void Start()
        {
            // 1. 打印路径，方便你在电脑上找到它
            Debug.Log($"🔥 歌曲文件夹路径: {BeatmapImporter.SongsDirectory}");

            // 2. 先执行解压逻辑 (如果有新 .osz)
            BeatmapImporter.ImportNewOszFiles();

            // 3. 刷新列表显示
            RefreshSongList();
        }

        public void RefreshSongList()
        {
            // 1. 清理旧列表
            foreach (Transform child in listContent)
            {
                Destroy(child.gameObject);
            }

            // 2. 扫描歌曲 (确保你有 BeatmapImporter 和 SongMetaLoader)
            List<BeatmapMetadata> maps = SongMetaLoader.ScanSongFolder();

            if (maps.Count == 0)
            {
                Debug.LogWarning("未找到歌曲，请在 PersistentDataPath/Songs 下放入文件夹");
            }

            // 3. 生成新按钮
            foreach (var map in maps)
            {
                GameObject obj = Instantiate(songItemPrefab, listContent);
                obj.transform.localScale = Vector3.one;           // 强行设为 1,1,1
                obj.transform.localPosition = Vector3.zero;       // 位置归零(Layout会自动排版)
                obj.transform.localRotation = Quaternion.identity;// 旋转归零
                var view = obj.GetComponent<SongItemView>();
                if (view != null)
                {
                    // 把 OnSongSelected 方法传给按钮
                    view.Setup(map, OnSongSelected);
                }
            }

        }

        // 当玩家点击某个按钮时触发
        private void OnSongSelected(BeatmapMetadata mapData)
        {
            Debug.Log($"选中: {mapData.Title}");

            // 存入 GameContext
            if (GameContext.Instance == null)
            {
                new GameObject("GameContext").AddComponent<GameContext>();
            }
            GameContext.Instance.SelectedBeatmapPath = mapData.OsuFilePath;

            // 跳转场景
            SceneManager.LoadScene(gameSceneName);
        }
    }
}