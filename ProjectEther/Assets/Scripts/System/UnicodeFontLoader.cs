using UnityEngine;
using TMPro;
using UnityEngine.TextCore.LowLevel;

namespace OsuVR
{
    [ExecuteAlways]
    public class UnicodeFontLoader : MonoBehaviour
    {
        private static UnicodeFontLoader _instance;
        public static UnicodeFontLoader Instance => _instance;

        [Header("Unicode Font Settings")]
        [SerializeField]
        private TMP_FontAsset unicodeFont;

        [SerializeField]
        private bool autoLoadSystemFont = false;

        [SerializeField]
        [Tooltip("System font names to try loading (in order) - Japanese fonts first for proper Japanese support")]
        private readonly string[] systemFontNames = new string[]
        {
            "Yu Gothic",
            "Meiryo",
            "MS Gothic",
            "Microsoft YaHei",
            "SimHei",
            "SimSun",
            "Arial Unicode MS"
        };

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeUnicodeFont();
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            if (_instance == this)
            {
                InitializeUnicodeFont();
            }
        }

        private void InitializeUnicodeFont()
        {
            if (unicodeFont != null)
            {
                SetFallbackFont(unicodeFont);
                return;
            }

            if (autoLoadSystemFont)
            {
                TryLoadSystemFont();
            }
        }

        private void TryLoadSystemFont()
        {
            foreach (string fontName in systemFontNames)
            {
                Font systemFont = Font.CreateDynamicFontFromOSFont(fontName, 64);
                if (systemFont != null)
                {
                    Debug.Log($"[UnicodeFontLoader] Found system font: {fontName}");
                    
                    TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(systemFont, 90, 9, 
                        GlyphRenderMode.SDFAA, 
                        2048, 2048, 
                        AtlasPopulationMode.Dynamic);
                    
                    // TMP_FontAsset.CreateFontAsset might return an empty/corrupted font asset if it fails
                    // to load the OS font. We must check if atlasTextures is actually assigned.
                    if (fontAsset != null && fontAsset.atlasTextures != null && fontAsset.atlasTextures.Length > 0 && fontAsset.atlasTextures[0] != null)
                    {
                        unicodeFont = fontAsset;
                        SetFallbackFont(fontAsset);
                        
                        Debug.Log($"[UnicodeFontLoader] Created dynamic font asset for: {fontName}");
                        return;
                    }
                    else if (fontAsset != null)
                    {
                        // Clean up the broken asset
                        Destroy(fontAsset);
                    }
                }
            }

            Debug.LogWarning("[UnicodeFontLoader] Could not find any suitable system font for Unicode support");
        }

        private void SetFallbackFont(TMP_FontAsset font)
        {
            TMP_Settings settings = TMP_Settings.instance;
            if (settings == null)
            {
                Debug.LogWarning("[UnicodeFontLoader] TMP_Settings not found");
                return;
            }

            bool alreadyAdded = false;
            foreach (var fallback in TMP_Settings.fallbackFontAssets)
            {
                if (fallback == font)
                {
                    alreadyAdded = true;
                    break;
                }
            }

            if (!alreadyAdded)
            {
                TMP_Settings.fallbackFontAssets.Add(font);
                Debug.Log($"[UnicodeFontLoader] Added {font.name} to TMP fallback fonts");
            }
        }

        public static void RefreshAllText()
        {
            var allText = FindObjectsOfType<TextMeshProUGUI>(true);
            foreach (var text in allText)
            {
                text.ForceMeshUpdate();
            }
        }

#if UNITY_EDITOR
        [UnityEditor.MenuItem("Project Ether/工具/刷新所有文本")]
        public static void RefreshAllTextMenu()
        {
            RefreshAllText();
            Debug.Log("[UnicodeFontLoader] Refreshed all text components");
        }
#endif
    }
}
