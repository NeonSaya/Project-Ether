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
        public TextMeshProUGUI difficultyCountText;
        public Button myButton;
        public Image backgroundImage;

        private BeatmapMetadata _metadata;
        private bool _isSelected = false;

        private static readonly Color NormalColor = new Color(0.05f, 0.05f, 0.1f, 0.6f);
        private static readonly Color SelectedColor = new Color(0.15f, 0.25f, 0.4f, 0.85f);
        private static readonly Color NormalTextColor = Color.white;
        private static readonly Color SelectedTextColor = new Color(0.4f, 0.8f, 1f);

        public BeatmapMetadata Metadata => _metadata;

        public void Setup(BeatmapMetadata metadata, int difficultyCount, UnityAction<BeatmapMetadata> onClickAction)
        {
            _metadata = metadata;

            bool useOriginalLanguage = false;
            if (SettingsManager.Instance != null && SettingsManager.Instance.Settings != null)
            {
                useOriginalLanguage = SettingsManager.Instance.Settings.displayOriginalLanguage;
            }

            if (titleText) titleText.text = metadata.GetDisplayTitle(useOriginalLanguage);
            if (artistText) artistText.text = metadata.GetDisplayArtist(useOriginalLanguage);
            if (difficultyCountText) difficultyCountText.text = $"{difficultyCount}D";

            if (backgroundImage == null)
            {
                backgroundImage = GetComponent<Image>();
            }

            SetSelected(false);

            myButton.onClick.RemoveAllListeners();
            myButton.onClick.AddListener(() =>
            {
                onClickAction.Invoke(_metadata);
            });
        }

        public void SetSelected(bool selected)
        {
            _isSelected = selected;

            if (backgroundImage != null)
            {
                backgroundImage.color = selected ? SelectedColor : NormalColor;
            }

            Color textColor = selected ? SelectedTextColor : NormalTextColor;
            if (titleText) titleText.color = textColor;
            if (artistText) artistText.color = selected ? SelectedTextColor : new Color(0.7f, 0.7f, 0.7f);
        }

        public bool IsSelected => _isSelected;

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
