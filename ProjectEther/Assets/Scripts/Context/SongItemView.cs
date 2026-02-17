using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

namespace OsuVR
{
    public class SongItemView : MonoBehaviour
    {
        [Header("UI 组件")]
        public TextMeshProUGUI titleText;
        public TextMeshProUGUI artistText;
        public TextMeshProUGUI versionText;
        public Button myButton; // 按钮组件

        // 保存歌曲信息
        private BeatmapMetadata _metadata;

        // 初始化方法
        public void Setup(BeatmapMetadata metadata, UnityAction<BeatmapMetadata> onClickAction)
        {
            _metadata = metadata;

            // 显示信息
            if (titleText) titleText.text = metadata.Title;
            if (artistText) artistText.text = metadata.Artist;
            if (versionText) versionText.text = metadata.Version;

            // 绑定点击事件
            myButton.onClick.RemoveAllListeners();
            myButton.onClick.AddListener(() =>
            {
                // 当被点击时，执行传进来的动作，并把自己带过去
                onClickAction.Invoke(_metadata);
            });
        }
    }
}