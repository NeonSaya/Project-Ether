using UnityEngine;
using TMPro;

namespace OsuVR
{
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class LocalizedText : MonoBehaviour
    {
        [SerializeField]
        public string localizationKey;

        private TextMeshProUGUI textComponent;

        private TMP_FontAsset originalFont;

        private void Awake()
        {
            textComponent = GetComponent<TextMeshProUGUI>();
            if (textComponent != null)
            {
                originalFont = textComponent.font;
            }
        }

        private void Start()
        {
            UpdateText();
            LocalizationManager.OnLanguageChanged += OnLanguageChanged;
        }

        private void OnDestroy()
        {
            LocalizationManager.OnLanguageChanged -= OnLanguageChanged;
        }

        private void OnLanguageChanged()
        {
            UpdateText();
        }

        public void UpdateText()
        {
            if (textComponent == null || string.IsNullOrEmpty(localizationKey))
                return;

            textComponent.text = LocalizationManager.GetText(localizationKey);

            // Handle Font assignment for CJK Support
            Language currentLang = LocalizationManager.GetCurrentLanguage();
            if (currentLang == Language.Chinese || currentLang == Language.Japanese)
            {
                string fontName = currentLang == Language.Chinese 
                    ? "Fonts & Materials/SourceHanSansSC-Regular SDF" 
                    : "Fonts & Materials/SourceHanSans-Regular SDF";
                    
                TMP_FontAsset cjkFont = Resources.Load<TMP_FontAsset>(fontName);
                if (cjkFont != null && textComponent.font != cjkFont)
                {
                    textComponent.font = cjkFont;
                }
            }
            else
            {
                if (originalFont != null && textComponent.font != originalFont)
                {
                    textComponent.font = originalFont;
                }
            }

            // Improve layout wrapping and overflow handling
            // Enable auto sizing to prevent text from overflowing visually in constrained areas
            if (!textComponent.enableAutoSizing)
            {
                textComponent.enableAutoSizing = true;
                textComponent.fontSizeMin = 10;
                textComponent.fontSizeMax = textComponent.fontSize;
                textComponent.overflowMode = TextOverflowModes.Ellipsis;
            }
            
            // Fix RTL issue (e.g. Arabic, or reversed characters accidentally). Ensure it's Left-to-Right.
            textComponent.isRightToLeftText = false;
        }

        public void SetKey(string key)
        {
            localizationKey = key;
            UpdateText();
        }
    }
}
