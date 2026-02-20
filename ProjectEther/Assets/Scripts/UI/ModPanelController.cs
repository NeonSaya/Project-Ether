using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace OsuVR
{
    /// <summary>
    /// Mod 选择面板控制器
    /// 整合到选歌界面，VR 世界空间 UI
    /// </summary>
    public class ModPanelController : MonoBehaviour
    {
        [Header("UI 引用")]
        public Transform modButtonContainer;
        public GameObject modButtonPrefab;
        public TextMeshProUGUI multiplierText;
        public TextMeshProUGUI activeModsText;
        public Button clearAllButton;
        public Button closeButton;

        [Header("布局设置")]
        public int buttonsPerRow = 4;
        public float buttonSpacing = 0.12f;
        public float rowSpacing = 0.15f;

        [Header("颜色设置")]
        public Color normalColor = new Color(0.15f, 0.15f, 0.2f, 0.9f);
        public Color selectedColor = new Color(0.3f, 0.6f, 0.9f, 0.95f);

        private ModSelection modSelection;
        private Dictionary<ModType, ModButtonUI> buttons = new Dictionary<ModType, ModButtonUI>();

        private class ModButtonUI
        {
            public GameObject gameObject;
            public Image background;
            public TextMeshProUGUI shortNameText;
            public TextMeshProUGUI fullNameText;
            public Button button;
            public ModInfo modInfo;
            public bool isSelected;
        }

        void Awake()
        {
            if (GameContext.Instance != null)
            {
                modSelection = GameContext.Instance.SelectedMods;
            }
            else
            {
                modSelection = new ModSelection();
            }

            modSelection.OnModChanged += OnModChanged;
        }

        void Start()
        {
            GenerateModButtons();
            SetupButtons();
            UpdateDisplay();
        }

        void OnDestroy()
        {
            if (modSelection != null)
            {
                modSelection.OnModChanged -= OnModChanged;
            }
        }

        void GenerateModButtons()
        {
            if (modButtonContainer == null) return;

            foreach (Transform child in modButtonContainer)
            {
                Destroy(child.gameObject);
            }
            buttons.Clear();

            var categories = new[] {
                ModCategory.Difficulty,
                ModCategory.Automation,
                ModCategory.Speed,
                ModCategory.Visual
            };

            int index = 0;
            foreach (var category in categories)
            {
                var modsInCategory = ModDatabase.GetModsByCategory(category);
                foreach (var modInfo in modsInCategory)
                {
                    CreateModButton(modInfo, index);
                    index++;
                }
            }
        }

        void CreateModButton(ModInfo modInfo, int index)
        {
            GameObject btnObj;

            if (modButtonPrefab != null)
            {
                btnObj = Instantiate(modButtonPrefab, modButtonContainer);
            }
            else
            {
                btnObj = CreateDefaultModButton(modInfo);
            }

            int row = index / buttonsPerRow;
            int col = index % buttonsPerRow;

            btnObj.transform.localPosition = new Vector3(
                col * buttonSpacing - (buttonsPerRow - 1) * buttonSpacing * 0.5f,
                -row * rowSpacing,
                0f
            );
            btnObj.transform.localRotation = Quaternion.identity;
            btnObj.name = $"Mod_{modInfo.shortName}";

            var buttonUI = new ModButtonUI
            {
                gameObject = btnObj,
                modInfo = modInfo,
                isSelected = modSelection.HasMod(modInfo.type)
            };

            var images = btnObj.GetComponentsInChildren<Image>();
            if (images.Length > 0) buttonUI.background = images[0];

            var texts = btnObj.GetComponentsInChildren<TextMeshProUGUI>();
            if (texts.Length > 0)
            {
                buttonUI.shortNameText = texts[0];
                buttonUI.shortNameText.text = modInfo.shortName;
            }
            if (texts.Length > 1)
            {
                buttonUI.fullNameText = texts[1];
                buttonUI.fullNameText.text = modInfo.fullName;
            }

            buttonUI.button = btnObj.GetComponent<Button>();
            if (buttonUI.button == null)
                buttonUI.button = btnObj.AddComponent<Button>();

            buttonUI.button.onClick.AddListener(() => ToggleMod(modInfo.type));

            UpdateButtonVisual(buttonUI);
            buttons[modInfo.type] = buttonUI;
        }

        GameObject CreateDefaultModButton(ModInfo modInfo)
        {
            GameObject obj = new GameObject($"Mod_{modInfo.shortName}");
            obj.transform.SetParent(modButtonContainer);
            obj.transform.localScale = new Vector3(0.1f, 0.1f, 0.01f);

            var bg = obj.AddComponent<Image>();
            bg.color = normalColor;

            var btn = obj.AddComponent<Button>();

            var textObj = new GameObject("Text");
            textObj.transform.SetParent(obj.transform);
            textObj.transform.localPosition = Vector3.zero;
            textObj.transform.localScale = Vector3.one;

            var tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = modInfo.shortName;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 0.05f;
            tmp.color = Color.white;

            return obj;
        }

        void SetupButtons()
        {
            if (clearAllButton != null)
            {
                clearAllButton.onClick.AddListener(OnClearAllClicked);
            }

            if (closeButton != null)
            {
                closeButton.onClick.AddListener(OnCloseClicked);
            }
        }

        void ToggleMod(ModType modType)
        {
            if (modSelection != null)
            {
                modSelection.ToggleMod(modType);
            }
        }

        void OnModChanged(ModType mod, bool enabled)
        {
            if (buttons.TryGetValue(mod, out var buttonUI))
            {
                buttonUI.isSelected = enabled;
                UpdateButtonVisual(buttonUI);
            }

            var incompatible = ModDatabase.GetIncompatibleMods(mod);
            foreach (var incompMod in incompatible)
            {
                if (buttons.TryGetValue(incompMod, out var incompButton))
                {
                    incompButton.isSelected = false;
                    UpdateButtonVisual(incompButton);
                }
            }

            UpdateDisplay();
        }

        void UpdateButtonVisual(ModButtonUI buttonUI)
        {
            if (buttonUI.background != null)
            {
                buttonUI.background.color = buttonUI.isSelected
                    ? buttonUI.modInfo.displayColor
                    : normalColor;
            }

            if (buttonUI.shortNameText != null)
            {
                buttonUI.shortNameText.color = buttonUI.isSelected
                    ? Color.white
                    : new Color(0.7f, 0.7f, 0.7f);
            }
        }

        void UpdateDisplay()
        {
            if (modSelection == null) return;

            float multiplier = modSelection.GetTotalScoreMultiplier();

            if (multiplierText != null)
            {
                if (multiplier == 0f)
                {
                    multiplierText.text = "0.00x";
                    multiplierText.color = Color.gray;
                }
                else
                {
                    multiplierText.text = $"{multiplier:F2}x";
                    multiplierText.color = multiplier >= 1f
                        ? new Color(0.3f, 1f, 0.5f)
                        : new Color(1f, 0.5f, 0.3f);
                }
            }

            if (activeModsText != null)
            {
                string modString = modSelection.GetModString();
                activeModsText.text = string.IsNullOrEmpty(modString) ? "No Mod" : modString;
                activeModsText.color = string.IsNullOrEmpty(modString)
                    ? new Color(0.6f, 0.6f, 0.6f)
                    : new Color(1f, 0.8f, 0.3f);
            }
        }

        void OnClearAllClicked()
        {
            if (modSelection != null)
            {
                modSelection.Clear();
            }
        }

        void OnCloseClicked()
        {
            gameObject.SetActive(false);
        }

        public void Show()
        {
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        public void Toggle()
        {
            gameObject.SetActive(!gameObject.activeSelf);
        }
    }
}
