using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OsuVR
{
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
        public TextMeshProUGUI descriptionText;
        public Button button;

        private ModInfo modInfo;
        private bool isSelected = false;
        private Color normalColor = new Color(0.1f, 0.1f, 0.14f, 0.95f);
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
            if (modInfo.type != ModType.None)
            {
                UpdateLocalizedText();
            }
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

            if (shortNameText == null)
            {
                Transform t = transform.Find("ShortName");
                if (t != null)
                    shortNameText = t.GetComponent<TextMeshProUGUI>();
            }
            if (shortNameText != null)
                shortNameText.text = info.shortName;

            UpdateLocalizedText();
            UpdateVisual();
        }

        private void UpdateLocalizedText()
        {
            if (modInfo.type == ModType.None)
                return;

            if (fullNameText == null)
            {
                Transform t = transform.Find("FullName");
                if (t != null)
                    fullNameText = t.GetComponent<TextMeshProUGUI>();
            }
            if (fullNameText != null)
            {
                string nameKey = $"mod_{modInfo.type.ToString().ToLower()}_name";
                string localizedName = LocalizationManager.GetText(nameKey);
                fullNameText.text = LocalizationManager.HasKey(nameKey)
                    ? localizedName
                    : modInfo.fullName;
            }

            if (descriptionText == null)
            {
                Transform t = transform.Find("Description");
                if (t != null)
                    descriptionText = t.GetComponent<TextMeshProUGUI>();
            }
            if (descriptionText != null)
            {
                string descKey = $"mod_{modInfo.type.ToString().ToLower()}_desc";
                string localizedDesc = LocalizationManager.GetText(descKey);
                descriptionText.text = LocalizationManager.HasKey(descKey)
                    ? localizedDesc
                    : modInfo.description;
            }
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
            if (backgroundImage == null)
            {
                backgroundImage = GetComponent<Image>();
            }

            if (backgroundImage != null)
            {
                backgroundImage.color =
                    isSelected && modInfo != null ? modInfo.displayColor : normalColor;
            }

            if (shortNameText != null)
            {
                shortNameText.color = isSelected ? Color.white : new Color(0.7f, 0.7f, 0.7f);
            }

            if (fullNameText != null)
            {
                fullNameText.color = isSelected ? Color.white : new Color(0.8f, 0.8f, 0.8f);
            }

            if (descriptionText != null)
            {
                descriptionText.color = isSelected
                    ? new Color(0.8f, 0.8f, 0.8f)
                    : new Color(0.5f, 0.5f, 0.5f);
            }
        }
    }
}
