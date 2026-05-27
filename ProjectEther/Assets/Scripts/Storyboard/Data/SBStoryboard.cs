using System.Collections.Generic;

namespace OsuVR.Storyboard.Data
{
    /// <summary>
    /// 顶层 Storyboard 数据容器：解析后的完整内存对象树
    /// </summary>
    public class SBStoryboard
    {
        /// <summary>
        /// 按图层分组的元素列表 (Background, Fail, Pass, Foreground, Overlay)
        /// </summary>
        public List<SBElement>[] Layers = new List<SBElement>[5];

        public SBStoryboard()
        {
            for (int i = 0; i < 5; i++)
                Layers[i] = new List<SBElement>();
        }

        /// <summary>
        /// 添加元素到对应图层
        /// </summary>
        public void AddElement(SBElement element)
        {
            Layers[(int)element.Layer].Add(element);
        }

        /// <summary>
        /// 评估所有元素在指定时间点的状态
        /// </summary>
        public void Evaluate(double currentTime)
        {
            for (int i = 0; i < 5; i++)
            {
                foreach (var element in Layers[i])
                    element.Evaluate(currentTime);
            }
        }

        /// <summary>
        /// 按渲染顺序 (Background → Foreground → Overlay) 获取所有元素
        /// </summary>
        public IEnumerable<SBElement> GetAllElementsInRenderOrder()
        {
            for (int i = 0; i < 5; i++)
            {
                foreach (var element in Layers[i])
                    yield return element;
            }
        }

        /// <summary>
        /// 元素总数
        /// </summary>
        public int TotalElementCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < 5; i++)
                    count += Layers[i].Count;
                return count;
            }
        }
    }
}
