using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OsuVR
{
    public class SongItemView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("UI 组件")]
        public TextMeshProUGUI titleText;
        public TextMeshProUGUI artistText;
        public TextMeshProUGUI difficultyCountText;
        public Button myButton;
        public Image backgroundImage;

        private BeatmapMetadata _metadata;
        private bool _isSelected = false;
        private bool hovered;
        private int difficultyCount;
        private TextMeshProUGUI metadataText;
        private Button favoriteButton;
        private TextMeshProUGUI favoriteText;

        private static readonly Color NormalColor = SongSelectionStyle.Panel;
        private static readonly Color SelectedColor = SongSelectionStyle.Selected;
        private static readonly Color NormalTextColor = SongSelectionStyle.Text;
        private static readonly Color SelectedTextColor = SongSelectionStyle.Accent;

        public BeatmapMetadata Metadata => _metadata;

        void OnEnable()
        {
            LocalizationManager.OnLanguageChanged += OnLanguageChanged;
        }

        void OnDisable()
        {
            LocalizationManager.OnLanguageChanged -= OnLanguageChanged;
        }

        private void OnLanguageChanged()
        {
            RefreshDisplay();
        }

        public void Setup(
            BeatmapMetadata metadata,
            int difficultyCount,
            UnityAction<BeatmapMetadata> onClickAction
        )
        {
            _metadata = metadata;
            this.difficultyCount = difficultyCount;

            bool useOriginalLanguage = false;
            if (SettingsManager.Instance != null && SettingsManager.Instance.Settings != null)
            {
                useOriginalLanguage = SettingsManager.Instance.Settings.displayOriginalLanguage;
            }

            if (titleText)
                titleText.text = metadata.GetDisplayTitle(useOriginalLanguage);
            if (artistText)
                artistText.text = metadata.GetDisplayArtist(useOriginalLanguage);
            if (difficultyCountText)
                difficultyCountText.text = $"{difficultyCount}D";

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
                backgroundImage.color =
                    selected ? SelectedColor
                    : hovered ? SongSelectionStyle.Control
                    : NormalColor;
            }

            Color textColor = selected ? SelectedTextColor : NormalTextColor;
            if (titleText)
                titleText.color = textColor;
            if (artistText)
                artistText.color = selected ? SelectedTextColor : SongSelectionStyle.Muted;
        }

        public bool IsSelected => _isSelected;

        public void OnPointerEnter(PointerEventData eventData)
        {
            hovered = true;
            SetSelected(_isSelected);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovered = false;
            SetSelected(_isSelected);
        }

        public void ConfigureLibraryRow(
            bool favorite,
            System.Action toggleFavorite,
            TMP_FontAsset font
        )
        {
            var layout = GetComponent<LayoutElement>() ?? gameObject.AddComponent<LayoutElement>();
            layout.minHeight = layout.preferredHeight = 92;
            layout.flexibleHeight = 0;
            titleText =
                titleText != null
                    ? titleText
                    : SongSelectionStyle.Label(transform, "Title", font, 17);
            artistText =
                artistText != null
                    ? artistText
                    : SongSelectionStyle.Label(transform, "Artist", font, 13);
            StyleRowText(titleText, 4, 34, 17);
            StyleRowText(artistText, 36, 26, 13);
            if (difficultyCountText != null)
                difficultyCountText.gameObject.SetActive(false);
            metadataText = SongSelectionStyle.Label(transform, "LibraryMetadata", font, 12);
            StyleRowText(metadataText, 64, 24, 12);
            metadataText.color = SongSelectionStyle.Muted;
            favoriteButton = SongSelectionStyle.Button(
                transform,
                "Favorite",
                "",
                font,
                () => toggleFavorite?.Invoke(),
                0,
                24,
                40,
                42,
                22
            );
            var favoriteRect = (RectTransform)favoriteButton.transform;
            favoriteRect.anchorMin = favoriteRect.anchorMax = new Vector2(1, 1);
            favoriteRect.anchoredPosition = new Vector2(-48, -24);
            favoriteText = favoriteButton.GetComponentInChildren<TextMeshProUGUI>();
            var divider = SongSelectionStyle.Rect(transform, "Divider", 0, 91, 342, 1);
            divider.anchorMax = new Vector2(1, 1);
            divider.sizeDelta = new Vector2(0, 1);
            SongSelectionStyle.Fill(divider, SongSelectionStyle.Line);
            if (myButton != null)
            {
                var colors = myButton.colors;
                colors.normalColor = colors.highlightedColor = colors.selectedColor = Color.white;
                colors.pressedColor = new Color(0.9f, 0.95f, 1f);
                myButton.colors = colors;
            }
            SetFavorite(favorite);
            RefreshDisplay();
        }

        private static void StyleRowText(TextMeshProUGUI text, float y, float height, float size)
        {
            SongSelectionStyle.Place(text.rectTransform, 12, y, 270, height);
            text.rectTransform.anchorMax = new Vector2(1, 1);
            text.rectTransform.sizeDelta = new Vector2(-68, height);
            SongSelectionStyle.ConfigureText(text, size);
        }

        public void SetFavorite(bool value)
        {
            if (favoriteText == null)
                return;
            favoriteText.text = value ? "★" : "☆";
            favoriteText.color = value ? SongSelectionStyle.Accent : SongSelectionStyle.Muted;
            SongSelectionStyle.ConfigureButton(
                favoriteButton,
                value ? SongSelectionStyle.Selected : SongSelectionStyle.Panel
            );
        }

        public void RefreshDisplay()
        {
            if (_metadata == null)
                return;

            bool useOriginalLanguage = false;
            if (SettingsManager.Instance != null && SettingsManager.Instance.Settings != null)
            {
                useOriginalLanguage = SettingsManager.Instance.Settings.displayOriginalLanguage;
            }

            if (titleText)
                titleText.text = _metadata.GetDisplayTitle(useOriginalLanguage);
            if (artistText)
                artistText.text = _metadata.GetDisplayArtist(useOriginalLanguage);
            if (metadataText != null)
                metadataText.text = string.Format(
                    LocalizationManager.GetText("ui_library_row_metadata"),
                    _metadata.BPM.ToString(
                        "0.#",
                        System.Globalization.CultureInfo.InvariantCulture
                    ),
                    _metadata.GetDisplayLength(),
                    difficultyCount
                );
        }
    }
}
