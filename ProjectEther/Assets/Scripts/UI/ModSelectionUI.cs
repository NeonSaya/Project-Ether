using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace OsuVR
{
    public class ModSelectionUI : MonoBehaviour
    {
        [Header("UI 引用")]
        [Tooltip("Mod 按钮的父容器")]
        public Transform modButtonContainer;

        [Tooltip("Mod 按钮预制体")]
        public GameObject modButtonPrefab;

        [Tooltip("显示当前分数倍率的文本")]
        public TextMeshProUGUI multiplierText;

        [Tooltip("显示当前选中 Mod 的文本")]
        public TextMeshProUGUI activeModsText;

        [Tooltip("清除所有 Mod 的按钮")]
        public Button clearAllButton;

        [Header("设置")]
        [Tooltip("是否在 Start 时自动生成按钮")]
        public bool autoGenerateButtons = true;

        private ModSelection modSelection;
        private Dictionary<ModType, ModButtonController> buttonControllers = new Dictionary<ModType, ModButtonController>();

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
            if (autoGenerateButtons)
            {
                GenerateModButtons();
            }

            if (clearAllButton != null)
            {
                clearAllButton.onClick.AddListener(OnClearAllClicked);
            }

            UpdateUI();
        }

        void OnDestroy()
        {
            if (modSelection != null)
            {
                modSelection.OnModChanged -= OnModChanged;
            }

            if (clearAllButton != null)
            {
                clearAllButton.onClick.RemoveListener(OnClearAllClicked);
            }
        }

        private void GenerateModButtons()
        {
            if (modButtonContainer == null || modButtonPrefab == null)
            {
                Debug.LogWarning("[ModSelectionUI] 缺少必要的引用，无法生成按钮");
                return;
            }

            foreach (Transform child in modButtonContainer)
            {
                Destroy(child.gameObject);
            }
            buttonControllers.Clear();

            var categories = new[] {
                ModCategory.Difficulty,
                ModCategory.Automation,
                ModCategory.Challenge,
                ModCategory.Speed,
                ModCategory.Visual
            };

            foreach (var category in categories)
            {
                var modsInCategory = ModDatabase.GetModsByCategory(category);
                foreach (var modInfo in modsInCategory)
                {
                    CreateModButton(modInfo);
                }
            }
        }

        private void CreateModButton(ModInfo modInfo)
        {
            GameObject buttonObj = Instantiate(modButtonPrefab, modButtonContainer);
            buttonObj.transform.localScale = Vector3.one;
            buttonObj.transform.localPosition = Vector3.zero;
            buttonObj.transform.localRotation = Quaternion.identity;

            var controller = buttonObj.GetComponent<ModButtonController>();
            if (controller == null)
            {
                controller = buttonObj.AddComponent<ModButtonController>();
            }

            controller.Initialize(modInfo, modSelection.HasMod(modInfo.type));
            controller.OnModClicked += OnModButtonClicked;

            buttonControllers[modInfo.type] = controller;
        }

        private void OnModButtonClicked(ModType modType)
        {
            modSelection.ToggleMod(modType);
        }

        private void OnModChanged(ModType mod, bool enabled)
        {
            if (buttonControllers.TryGetValue(mod, out var controller))
            {
                controller.SetSelected(enabled);
            }

            var incompatible = ModDatabase.GetIncompatibleMods(mod);
            foreach (var incompMod in incompatible)
            {
                if (buttonControllers.TryGetValue(incompMod, out var incompController))
                {
                    incompController.SetSelected(false);
                }
            }

            UpdateUI();
        }

        private void OnClearAllClicked()
        {
            modSelection.Clear();
            UpdateUI();
        }

        private void UpdateUI()
        {
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
                    multiplierText.color = multiplier >= 1f ? new Color(0.3f, 1f, 0.5f) : new Color(1f, 0.5f, 0.3f);
                }
            }

            if (activeModsText != null)
            {
                string modString = modSelection.GetModString();
                if (string.IsNullOrEmpty(modString))
                {
                    activeModsText.text = "No Mod";
                    activeModsText.color = Color.white;
                }
                else
                {
                    activeModsText.text = modString;
                    activeModsText.color = new Color(1f, 0.8f, 0.3f);
                }
            }
        }

        public void SetModSelection(ModSelection selection)
        {
            modSelection = selection ?? new ModSelection();
            modSelection.OnModChanged += OnModChanged;

            foreach (var kvp in buttonControllers)
            {
                kvp.Value.SetSelected(modSelection.HasMod(kvp.Key));
            }

            UpdateUI();
        }

        public ModSelection GetModSelection()
        {
            return modSelection;
        }
    }

    public class ModButtonController : MonoBehaviour
    {
        [Header("UI 组件")]
        public Image backgroundImage;
        public TextMeshProUGUI shortNameText;
        public TextMeshProUGUI fullNameText;
        public Button button;

        private ModInfo modInfo;
        private bool isSelected = false;
        private Color normalColor = new Color(0.2f, 0.2f, 0.25f);
        private Color selectedColor = new Color(0.3f, 0.6f, 0.9f);

        public event System.Action<ModType> OnModClicked;

        void Awake()
        {
            if (button == null)
                button = GetComponent<Button>();

            if (backgroundImage == null)
                backgroundImage = GetComponent<Image>();

            if (button != null)
                button.onClick.AddListener(OnButtonClicked);
        }

        void OnDestroy()
        {
            if (button != null)
                button.onClick.RemoveListener(OnButtonClicked);
        }

        public void Initialize(ModInfo info, bool selected)
        {
            modInfo = info;
            isSelected = selected;

            if (shortNameText != null)
                shortNameText.text = info.shortName;

            if (fullNameText != null)
                fullNameText.text = info.fullName;

            UpdateVisual();
        }

        public void SetSelected(bool selected)
        {
            isSelected = selected;
            UpdateVisual();
        }

        private void OnButtonClicked()
        {
            OnModClicked?.Invoke(modInfo.type);
        }

        private void UpdateVisual()
        {
            if (backgroundImage != null)
            {
                backgroundImage.color = isSelected ? modInfo.displayColor : normalColor;
            }

            if (shortNameText != null)
            {
                shortNameText.color = isSelected ? Color.white : Color.gray;
            }
        }
    }
}
