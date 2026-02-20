using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace OsuVR
{
    /// <summary>
    /// Mod 选择界面 UI 控制器
    /// 管理 Mod 按钮的生成、交互和显示
    /// </summary>
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

        [Tooltip("关闭 Mod 面板的按钮")]
        public Button closeButton;

        [Header("面板引用")]
        [Tooltip("Mod 选择面板的整体 GameObject")]
        public GameObject modPanel;

        [Header("设置")]
        [Tooltip("是否在 Start 时自动生成按钮")]
        public bool autoGenerateButtons = true;

        private ModSelection modSelection;
        private Dictionary<ModType, ModButtonController> buttonControllers = new Dictionary<ModType, ModButtonController>();

        // =========================================================
        // 生命周期
        // =========================================================

        void Awake()
        {
            // 获取全局 Mod 选择实例
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

            if (closeButton != null)
            {
                closeButton.onClick.AddListener(OnCloseClicked);
            }

            UpdateUI();
            
            // 确保按钮状态与当前 Mod 选择状态同步
            ForceSyncButtonStates();
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

            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(OnCloseClicked);
            }
        }

        // =========================================================
        // 按钮生成
        // =========================================================

        /// <summary>
        /// 生成所有 Mod 按钮
        /// 按分类顺序生成，清除现有按钮
        /// </summary>
        private void GenerateModButtons()
        {
            if (modButtonContainer == null || modButtonPrefab == null)
            {
                Debug.LogWarning("[ModSelectionUI] 缺少必要的引用，无法生成按钮");
                return;
            }

            // 清除现有按钮
            foreach (Transform child in modButtonContainer)
            {
                Destroy(child.gameObject);
            }
            buttonControllers.Clear();

            // 按分类顺序生成按钮
            var categories = new[] {
                ModCategory.Difficulty,
                ModCategory.Automation,
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

        /// <summary>
        /// 创建单个 Mod 按钮
        /// </summary>
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

        // =========================================================
        // 事件处理
        // =========================================================

        /// <summary>
        /// Mod 按钮点击回调
        /// </summary>
        private void OnModButtonClicked(ModType modType)
        {
            modSelection.ToggleMod(modType);
        }

        /// <summary>
        /// Mod 状态变化回调
        /// 更新按钮视觉状态，处理互斥 Mod
        /// </summary>
        private void OnModChanged(ModType mod, bool enabled)
        {
            // 更新当前 Mod 按钮状态
            if (buttonControllers.TryGetValue(mod, out var controller))
            {
                controller.SetSelected(enabled);
            }

            // 更新互斥 Mod 按钮状态 (强制取消选中)
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

        /// <summary>
        /// 清除所有按钮点击回调
        /// </summary>
        private void OnClearAllClicked()
        {
            modSelection.Clear();
            UpdateUI();
        }

        /// <summary>
        /// 关闭按钮点击回调
        /// </summary>
        private void OnCloseClicked()
        {
            if (modPanel != null)
            {
                modPanel.SetActive(false);
            }
            else
            {
                // 如果没有设置面板，则隐藏整个组件
                gameObject.SetActive(false);
            }
        }

        // =========================================================
        // UI 更新
        // =========================================================

        /// <summary>
        /// 更新分数倍率和 Mod 显示文本
        /// </summary>
        private void UpdateUI()
        {
            float multiplier = modSelection.GetTotalScoreMultiplier();

            // 更新分数倍率显示
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
                    // 倍率 >=1 显示绿色，<1 显示红色
                    multiplierText.color = multiplier >= 1f ? new Color(0.3f, 1f, 0.5f) : new Color(1f, 0.5f, 0.3f);
                }
            }

            // 更新已选 Mod 显示
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

        // =========================================================
        // 公开接口
        // =========================================================

        /// <summary>
        /// 设置 Mod 选择实例
        /// </summary>
        public void SetModSelection(ModSelection selection)
        {
            modSelection = selection ?? new ModSelection();
            modSelection.OnModChanged += OnModChanged;

            // 同步所有按钮状态
            foreach (var kvp in buttonControllers)
            {
                kvp.Value.SetSelected(modSelection.HasMod(kvp.Key));
            }

            UpdateUI();
        }

        /// <summary>
        /// 获取当前 Mod 选择
        /// </summary>
        public ModSelection GetModSelection()
        {
            return modSelection;
        }

        /// <summary>
        /// 强制同步所有按钮状态与当前 Mod 选择状态
        /// </summary>
        public void ForceSyncButtonStates()
        {
            foreach (var kvp in buttonControllers)
            {
                bool hasMod = modSelection.HasMod(kvp.Key);
                kvp.Value.SetSelected(hasMod);
            }
            UpdateUI();
        }
    }

    /// <summary>
    /// 单个 Mod 按钮控制器
    /// 处理按钮的视觉状态和点击事件
    /// </summary>
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

        /// <summary>
        /// 按钮点击事件
        /// </summary>
        public event System.Action<ModType> OnModClicked;

        // =========================================================
        // 生命周期
        // =========================================================

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

        // =========================================================
        // 初始化与状态更新
        // =========================================================

        /// <summary>
        /// 初始化按钮
        /// </summary>
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

        /// <summary>
        /// 设置选中状态
        /// </summary>
        public void SetSelected(bool selected)
        {
            isSelected = selected;
            UpdateVisual();
        }

        /// <summary>
        /// 按钮点击回调
        /// </summary>
        private void OnButtonClicked()
        {
            OnModClicked?.Invoke(modInfo.type);
        }

        /// <summary>
        /// 更新视觉状态
        /// 选中时显示 Mod 主题色，未选中时显示默认灰色
        /// </summary>
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
