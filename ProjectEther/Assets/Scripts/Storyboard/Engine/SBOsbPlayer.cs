using UnityEngine;
using OsuVR.Storyboard.Data;

namespace OsuVR.Storyboard.Engine
{
    /// <summary>
    /// Storyboard 播放器顶层入口 (对应 osu!droid OsbPlayer)
    /// 持有 5 个 SBPlayingLayer (Background/Fail/Pass/Foreground/Overlay)
    ///
    /// 用法:
    ///   var player = new SBOsbPlayer();
    ///   player.LoadStoryboard(storyboard);
    ///   // 每帧:
    ///   player.Update(currentTimeMs);
    ///   // 渲染层遍历:
    ///   for (int layer = 0; layer < 5; layer++)
    ///       for (var s = player.GetLayerActiveHead(layer); s != player.GetLayerActiveTail(layer); s = s.Next)
    ///           // 读取 s.State, s.Element
    /// </summary>
    public class SBOsbPlayer
    {
        private SBPlayingLayer[] _layers = new SBPlayingLayer[5];

        /// <summary>
        /// 加载 Storyboard: 将 SBStoryboard 转换为运行时引擎数据
        /// 调用一次即可，之后每帧调用 Update(time)
        /// </summary>
        public void LoadStoryboard(SBStoryboard storyboard)
        {
            for (int i = 0; i < 5; i++)
            {
                var elements = storyboard.Layers[i];
                if (elements == null || elements.Count == 0)
                {
                    _layers[i] = null;
                    continue;
                }

                var layer = new SBPlayingLayer();
                layer.LoadSprites(elements, SBCommandGroupBuilder.Build);
                _layers[i] = layer;

                Debug.Log($"[OsbPlayer] Layer {i} loaded: {elements.Count} sprites");
            }
        }

        /// <summary>
        /// 每帧调用: 推进所有图层到指定时间
        /// </summary>
        public void Update(double time)
        {
            for (int i = 0; i < 5; i++)
                _layers[i]?.Update(time);
        }

        /// <summary>
        /// 获取指定图层的活跃链表头 (用于渲染层遍历)
        /// </summary>
        public SBPlayingSprite GetLayerActiveHead(int layer)
        {
            return _layers[layer]?.ActiveHead;
        }

        /// <summary>
        /// 获取指定图层的活跃链表尾哨兵 (遍历终止条件)
        /// </summary>
        public SBPlayingSprite GetLayerActiveTail(int layer)
        {
            return _layers[layer]?.ActiveTail;
        }

        public void Reset()
        {
            for (int i = 0; i < 5; i++)
                _layers[i]?.Reset();
        }

        /// <summary>
        /// 遍历所有图层的所有 Sprite (用于渲染器缓存纹理索引)
        /// </summary>
        public void ForEachSprite(System.Action<int, SBPlayingSprite> action)
        {
            for (int i = 0; i < 5; i++)
                _layers[i]?.ForEachSprite(s => action(i, s));
        }

        public void Unload()
        {
            for (int i = 0; i < 5; i++)
                _layers[i] = null;
        }
    }
}
