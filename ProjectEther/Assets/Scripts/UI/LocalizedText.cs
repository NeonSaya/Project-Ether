using UnityEngine;
using TMPro;
using System.Collections.Generic;

namespace OsuVR
{
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class LocalizedText : MonoBehaviour
    {
        [SerializeField]
        public string localizationKey;

        private TextMeshProUGUI textComponent;

        private TMP_FontAsset originalFont;

        private static TMP_FontAsset cachedChineseFont;
        private static TMP_FontAsset cachedJapaneseFont;
        private static TMP_FontAsset cachedFallbackFont;
        private static bool fontsLoaded = false;

        private void Awake()
        {
            textComponent = GetComponent<TextMeshProUGUI>();
            if (textComponent != null)
            {
                originalFont = textComponent.font;
            }
            
            EnsureFontsLoaded();
        }

        private static void EnsureFontsLoaded()
        {
            if (fontsLoaded) return;
            
            cachedChineseFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/SourceHanSansSC-Regular SDF");
            cachedJapaneseFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/SourceHanSans-Regular SDF");
            cachedFallbackFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF - Fallback");
            
            Debug.Log($"[LocalizedText] Loading fonts - Chinese: {(cachedChineseFont != null ? cachedChineseFont.name : "NULL")}, Japanese: {(cachedJapaneseFont != null ? cachedJapaneseFont.name : "NULL")}, Fallback: {(cachedFallbackFont != null ? cachedFallbackFont.name : "NULL")}");
            
            if (cachedChineseFont != null)
            {
                cachedChineseFont.isMultiAtlasTexturesEnabled = true;
            }
            else
            {
                Debug.LogError("[LocalizedText] Failed to load Chinese font!");
            }
            
            if (cachedJapaneseFont != null)
            {
                cachedJapaneseFont.isMultiAtlasTexturesEnabled = true;
            }
            else
            {
                Debug.LogError("[LocalizedText] Failed to load Japanese font!");
            }
            
            fontsLoaded = true;
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

            Language currentLang = LocalizationManager.GetCurrentLanguage();
            TMP_FontAsset targetFont = null;

            switch (currentLang)
            {
                case Language.Chinese:
                    targetFont = cachedChineseFont;
                    break;
                case Language.Japanese:
                    targetFont = cachedJapaneseFont;
                    break;
                default:
                    targetFont = originalFont;
                    break;
            }

            if (targetFont == null)
            {
                Debug.LogWarning($"[LocalizedText] Target font is null for language: {currentLang}");
                return;
            }

            string text = LocalizationManager.GetText(localizationKey);
            
            Debug.Log($"[LocalizedText] UpdateText - Key: {localizationKey}, Lang: {currentLang}, Font: {targetFont.name}, Text: {text}");
            
            if (textComponent.font != targetFont)
            {
                textComponent.font = targetFont;
                textComponent.fontMaterial = targetFont.material;
            }
            
            textComponent.text = text;

            if (!textComponent.enableAutoSizing)
            {
                textComponent.enableAutoSizing = true;
                textComponent.fontSizeMin = 10;
                textComponent.fontSizeMax = textComponent.fontSize;
                textComponent.overflowMode = TextOverflowModes.Ellipsis;
            }
            
            textComponent.isRightToLeftText = false;
            
            textComponent.SetAllDirty();
            textComponent.ForceMeshUpdate(true, true);
            
            if (targetFont != null && !targetFont.isMultiAtlasTexturesEnabled)
            {
                targetFont.isMultiAtlasTexturesEnabled = true;
            }
        }

        public void SetKey(string key)
        {
            localizationKey = key;
            UpdateText();
        }

        public void SetTextDirectly(string text)
        {
            if (textComponent == null)
                textComponent = GetComponent<TextMeshProUGUI>();
                
            if (textComponent == null) return;

            Language currentLang = LocalizationManager.GetCurrentLanguage();
            TMP_FontAsset targetFont = null;

            switch (currentLang)
            {
                case Language.Chinese:
                    targetFont = cachedChineseFont;
                    break;
                case Language.Japanese:
                    targetFont = cachedJapaneseFont;
                    break;
                default:
                    targetFont = originalFont;
                    break;
            }

            if (targetFont != null && textComponent.font != targetFont)
            {
                textComponent.font = targetFont;
            }
            
            textComponent.text = text;
            textComponent.SetAllDirty();
            textComponent.ForceMeshUpdate(true);
        }

        public static void PreloadFonts()
        {
            EnsureFontsLoaded();
        }
    }
}
