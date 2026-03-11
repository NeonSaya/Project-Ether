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
        public Button myButton;

        private BeatmapMetadata _metadata;

        public void Setup(BeatmapMetadata metadata, UnityAction<BeatmapMetadata> onClickAction)
        {
            _metadata = metadata;

            bool useOriginalLanguage = false;
            if (SettingsManager.Instance != null && SettingsManager.Instance.Settings != null)
            {
                useOriginalLanguage = SettingsManager.Instance.Settings.displayOriginalLanguage;
            }

            if (titleText) titleText.text = metadata.GetDisplayTitle(useOriginalLanguage);
            if (artistText) artistText.text = metadata.GetDisplayArtist(useOriginalLanguage);
            if (versionText) versionText.text = metadata.Version;

            myButton.onClick.RemoveAllListeners();
            myButton.onClick.AddListener(() =>
            {
                onClickAction.Invoke(_metadata);
            });
        }

        public void RefreshDisplay()
        {
            if (_metadata == null) return;

            bool useOriginalLanguage = false;
            if (SettingsManager.Instance != null && SettingsManager.Instance.Settings != null)
            {
                useOriginalLanguage = SettingsManager.Instance.Settings.displayOriginalLanguage;
            }

            if (titleText) titleText.text = _metadata.GetDisplayTitle(useOriginalLanguage);
            if (artistText) artistText.text = _metadata.GetDisplayArtist(useOriginalLanguage);
        }
    }
}