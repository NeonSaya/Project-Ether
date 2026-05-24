using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace OsuVR
{
    /// <summary>
    /// 设置页面抽象基类
    ///
    /// 每个设置页面负责：
    /// 1. BuildContent: 在给定的父容器中创建控件
    /// 2. RefreshUI: 从 tempSettings 刷新所有控件值（不触发回调）
    /// 3. TabLocalizationKey: 提供标签页本地化 Key
    /// </summary>
    public abstract class SettingsPageBase
    {
        protected GameObject atomicSliderPrefab;
        protected GameObject atomicTogglePrefab;
        protected GameObject atomicDropdownPrefab;
        protected AudioSource audioSource;
        protected AudioClip hoverSound;
        protected AudioClip clickSound;

        /// <summary>
        /// 标签页本地化 Key
        /// </summary>
        public abstract string TabLocalizationKey { get; }

        /// <summary>
        /// 初始化页面，注入预制体引用和音频资源
        /// </summary>
        public void Initialize(GameObject sliderPrefab, GameObject togglePrefab,
            GameObject dropdownPrefab, AudioSource source, AudioClip hover, AudioClip click)
        {
            atomicSliderPrefab = sliderPrefab;
            atomicTogglePrefab = togglePrefab;
            atomicDropdownPrefab = dropdownPrefab;
            audioSource = source;
            hoverSound = hover;
            clickSound = click;
        }

        /// <summary>
        /// 在父容器中构建页面内容
        /// </summary>
        /// <param name="parent">内容区域的 RectTransform</param>
        /// <param name="tempSettings">当前工作设置副本</param>
        /// <param name="contentWidth">内容区域宽度</param>
        public abstract void BuildContent(RectTransform parent, GameSettings tempSettings, float contentWidth);

        /// <summary>
        /// 从 tempSettings 刷新所有 UI 控件值（不触发 onValueChanged 回调）
        /// </summary>
        public abstract void RefreshUI(GameSettings tempSettings);

        // ============================================================
        //  Helper: 创建带本地化的 Slider 控件
        // ============================================================

        /// <summary>
        /// 创建一个原子化 Slider 控件
        /// </summary>
        /// <param name="parent">父容器</param>
        /// <param name="label">标签文本（本地化前）</param>
        /// <param name="localizationKey">本地化 Key</param>
        /// <param name="minVal">最小值</param>
        /// <param name="maxVal">最大值</param>
        /// <param name="currentVal">当前值</param>
        /// <param name="valueFormat">值显示格式（如 "{0:F0} ms"）</param>
        /// <param name="onValueChanged">值变化回调</param>
        /// <returns>Slider 组件引用</returns>
        protected Slider CreateSlider(Transform parent, string label, string localizationKey,
            float minVal, float maxVal, float currentVal, string valueFormat,
            UnityEngine.Events.UnityAction<float> onValueChanged, float valueScale = 1f)
        {
            var slider = UILayoutHelper.InstantiateSliderPrefab(atomicSliderPrefab, parent,
                label, localizationKey, minVal, maxVal, currentVal, valueFormat, onValueChanged, valueScale);

            if (slider != null)
                AddControlSounds(slider.gameObject, true);

            return slider;
        }

        // ============================================================
        //  Helper: 创建 AudioOffsetRow（Prefab 精确结构）
        // ============================================================

        /// <summary>
        /// 创建 AudioOffsetRow（Header + Slider + FineTune 按钮行，高度 95px）
        /// 精确复刻 VRSettingsMenu.prefab 中的 AudioOffsetRow 结构
        /// </summary>
        protected Slider CreateAudioOffsetRow(Transform parent, string label, string localizationKey,
            float minVal, float maxVal, float currentVal, string valueFormat,
            UnityEngine.Events.UnityAction<float> onValueChanged,
            UnityEngine.Events.UnityAction<int> onFineTune)
        {
            // Row root: VLG 垂直排列 Header / Slider / FineTune
            var rowGo = new GameObject("Audio OffsetRow");
            rowGo.transform.SetParent(parent, false);

            var rowRt = rowGo.AddComponent<RectTransform>();
            rowRt.anchorMin = new Vector2(0f, 0.5f);
            rowRt.anchorMax = new Vector2(1f, 0.5f);
            rowRt.pivot = new Vector2(0.5f, 0.5f);
            rowRt.sizeDelta = new Vector2(0f, 95f);

            var rowVlg = rowGo.AddComponent<VerticalLayoutGroup>();
            rowVlg.spacing = 2f;
            rowVlg.childAlignment = TextAnchor.UpperLeft;
            rowVlg.childControlWidth = true;
            rowVlg.childControlHeight = false;
            rowVlg.childForceExpandWidth = true;
            rowVlg.childForceExpandHeight = false;

            // ---- Header (Label + Value) ----
            var headerGo = new GameObject("Header");
            headerGo.transform.SetParent(rowGo.transform, false);
            var headerRt = headerGo.AddComponent<RectTransform>();
            headerRt.anchorMin = new Vector2(0.5f, 0.5f);
            headerRt.anchorMax = new Vector2(0.5f, 0.5f);
            headerRt.sizeDelta = new Vector2(0f, 24f);

            var headerHlg = headerGo.AddComponent<HorizontalLayoutGroup>();
            headerHlg.spacing = 10f;
            headerHlg.childAlignment = TextAnchor.LowerLeft;
            headerHlg.childControlWidth = true;
            headerHlg.childControlHeight = false;
            headerHlg.childForceExpandWidth = true;
            headerHlg.childForceExpandHeight = true;

            // Header/Label
            var headerLabelGo = new GameObject("Label");
            headerLabelGo.transform.SetParent(headerGo.transform, false);
            var headerLabelRt = headerLabelGo.AddComponent<RectTransform>();
            headerLabelRt.anchorMin = new Vector2(0.5f, 0.5f);
            headerLabelRt.anchorMax = new Vector2(0.5f, 0.5f);
            headerLabelRt.sizeDelta = new Vector2(0f, 24f);
            var headerLabelTmp = headerLabelGo.AddComponent<TextMeshProUGUI>();
            headerLabelTmp.text = label;
            headerLabelTmp.fontSize = 14f;
            headerLabelTmp.color = new Color(0.9f, 0.9f, 0.95f, 1f);
            headerLabelTmp.alignment = TextAlignmentOptions.Left;
            headerLabelTmp.enableAutoSizing = false;
            headerLabelTmp.raycastTarget = false;
            if (!string.IsNullOrEmpty(localizationKey))
            {
                var lt = headerLabelGo.AddComponent<LocalizedText>();
                lt.localizationKey = localizationKey;
            }
            var headerLabelLe = headerLabelGo.AddComponent<LayoutElement>();
            headerLabelLe.flexibleWidth = 1f;

            // Header/Value
            var headerValueGo = new GameObject("Value");
            headerValueGo.transform.SetParent(headerGo.transform, false);
            var headerValueRt = headerValueGo.AddComponent<RectTransform>();
            headerValueRt.anchorMin = new Vector2(0.5f, 0.5f);
            headerValueRt.anchorMax = new Vector2(0.5f, 0.5f);
            headerValueRt.sizeDelta = new Vector2(0f, 24f);
            var headerValueTmp = headerValueGo.AddComponent<TextMeshProUGUI>();
            headerValueTmp.text = string.Format(valueFormat, currentVal);
            headerValueTmp.fontSize = 16f;
            headerValueTmp.fontStyle = FontStyles.Bold;
            headerValueTmp.color = Color.white;
            headerValueTmp.alignment = TextAlignmentOptions.Center;
            headerValueTmp.enableAutoSizing = false;
            headerValueTmp.raycastTarget = false;
            var headerValueLe = headerValueGo.AddComponent<LayoutElement>();
            headerValueLe.preferredWidth = 100f;

            // ---- Slider ----
            var sliderGo = new GameObject("Slider");
            sliderGo.transform.SetParent(rowGo.transform, false);
            var sliderRt = sliderGo.AddComponent<RectTransform>();
            sliderRt.anchorMin = new Vector2(0.5f, 0.5f);
            sliderRt.anchorMax = new Vector2(0.5f, 0.5f);
            sliderRt.sizeDelta = new Vector2(0f, 36f);
            var sliderLe = sliderGo.AddComponent<LayoutElement>();
            sliderLe.preferredHeight = 32f;
            sliderLe.flexibleWidth = 1f;

            // 创建标准 Slider 控件
            var bgGo = new GameObject("Background");
            bgGo.transform.SetParent(sliderGo.transform, false);
            var bgRt = bgGo.AddComponent<RectTransform>();
            bgRt.anchorMin = new Vector2(0f, 0.25f);
            bgRt.anchorMax = new Vector2(1f, 0.75f);
            bgRt.sizeDelta = Vector2.zero;
            var bgImg = bgGo.AddComponent<Image>();
            bgImg.color = new Color(0.15f, 0.15f, 0.2f, 0.6f);

            var fillAreaGo = new GameObject("Fill Area");
            fillAreaGo.transform.SetParent(sliderGo.transform, false);
            var fillAreaRt = fillAreaGo.AddComponent<RectTransform>();
            fillAreaRt.anchorMin = new Vector2(0f, 0.25f);
            fillAreaRt.anchorMax = new Vector2(1f, 0.75f);
            fillAreaRt.sizeDelta = Vector2.zero;
            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(fillAreaGo.transform, false);
            var fillRt = fillGo.AddComponent<RectTransform>();
            fillRt.sizeDelta = Vector2.zero;
            var fillImg = fillGo.AddComponent<Image>();
            fillImg.color = new Color(0.3f, 0.6f, 1f, 0.8f);

            var handleAreaGo = new GameObject("Handle Slide Area");
            handleAreaGo.transform.SetParent(sliderGo.transform, false);
            var handleAreaRt = handleAreaGo.AddComponent<RectTransform>();
            handleAreaRt.anchorMin = Vector2.zero;
            handleAreaRt.anchorMax = Vector2.one;
            handleAreaRt.sizeDelta = Vector2.zero;
            var handleGo = new GameObject("Handle");
            handleGo.transform.SetParent(handleAreaGo.transform, false);
            var handleRt = handleGo.AddComponent<RectTransform>();
            handleRt.sizeDelta = new Vector2(20f, 0f);
            var handleImg = handleGo.AddComponent<Image>();
            handleImg.color = new Color(0.8f, 0.85f, 0.95f, 1f);

            var slider = sliderGo.AddComponent<Slider>();
            slider.fillRect = fillRt;
            slider.handleRect = handleRt;
            slider.targetGraphic = handleImg;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = minVal;
            slider.maxValue = maxVal;
            slider.value = currentVal;
            slider.wholeNumbers = false;
            if (onValueChanged != null)
                slider.onValueChanged.AddListener(onValueChanged);

            // 值变化时更新 Header/Value 文本
            var capturedFormat = valueFormat;
            var capturedTmp = headerValueTmp;
            slider.onValueChanged.AddListener(v =>
            {
                capturedTmp.text = string.Format(capturedFormat, v);
            });

            AddControlSounds(sliderGo, true);

            // ---- FineTune 按钮行 ----
            var fineTuneGo = new GameObject("FineTune");
            fineTuneGo.transform.SetParent(rowGo.transform, false);
            var fineTuneRt = fineTuneGo.AddComponent<RectTransform>();
            fineTuneRt.anchorMin = new Vector2(0.5f, 0.5f);
            fineTuneRt.anchorMax = new Vector2(0.5f, 0.5f);
            fineTuneRt.sizeDelta = new Vector2(0f, 28f);

            var fineTuneHlg = fineTuneGo.AddComponent<HorizontalLayoutGroup>();
            fineTuneHlg.spacing = 6f;
            fineTuneHlg.childAlignment = TextAnchor.LowerCenter;
            fineTuneHlg.childControlWidth = false;
            fineTuneHlg.childControlHeight = false;
            fineTuneHlg.childForceExpandWidth = false;
            fineTuneHlg.childForceExpandHeight = false;

            // SpacerLeft (flexible)
            CreateFineTuneSpacer(fineTuneGo.transform);

            // 按钮: -10, -5, -1, +1, +5, +10
            int[] fineTuneValues = { -10, -5, -1, 1, 5, 10 };
            foreach (int val in fineTuneValues)
                CreateFineTuneButton(fineTuneGo.transform, val, onFineTune);

            // SpacerRight (flexible)
            CreateFineTuneSpacer(fineTuneGo.transform);

            return slider;
        }

        private static void CreateFineTuneSpacer(Transform parent)
        {
            var go = new GameObject("Spacer");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(100f, 100f);
            var le = go.AddComponent<LayoutElement>();
            le.flexibleWidth = 1f;
        }

        private void CreateFineTuneButton(Transform parent, int value,
            UnityEngine.Events.UnityAction<int> onFineTune)
        {
            string text = value > 0 ? $"+{value}" : $"{value}";
            var go = new GameObject(text);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(42f, 24f);

            var img = go.AddComponent<Image>();
            img.color = UILayoutHelper.ButtonNormalColor;

            var button = go.AddComponent<Button>();
            var colors = button.colors;
            colors.normalColor = new Color(1f, 1f, 1f, 1f);
            colors.highlightedColor = new Color(0.96f, 0.96f, 0.96f, 1f);
            colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
            button.colors = colors;
            button.targetGraphic = img;

            int capturedValue = value;
            button.onClick.AddListener(() =>
            {
                PlayClickSound();
                onFineTune?.Invoke(capturedValue);
            });

            // 按钮文字
            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            var textRt = textGo.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.sizeDelta = Vector2.zero;
            textRt.anchoredPosition = Vector2.zero;
            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 12f;
            tmp.color = new Color(0.9f, 0.9f, 0.95f, 1f);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableAutoSizing = false;
            tmp.raycastTarget = false;

            UILayoutHelper.AddColorHoverEffect(go, img,
                UILayoutHelper.ButtonNormalColor, UILayoutHelper.ButtonHoverColor);
            UILayoutHelper.AddHoverSoundEffect(go, audioSource, hoverSound, 0.5f);
        }

        // ============================================================
        //  Helper: 创建带本地化的 Toggle 控件
        // ============================================================

        /// <summary>
        /// 创建一个原子化 Toggle 控件
        /// </summary>
        protected Toggle CreateToggle(Transform parent, string label, string localizationKey,
            bool isOn, UnityEngine.Events.UnityAction<bool> onValueChanged)
        {
            var toggle = UILayoutHelper.InstantiateTogglePrefab(atomicTogglePrefab, parent,
                label, localizationKey, isOn, onValueChanged);

            if (toggle != null)
                AddControlSounds(toggle.gameObject, false);

            return toggle;
        }

        // ============================================================
        //  Helper: 创建带本地化的 Dropdown 控件
        // ============================================================

        /// <summary>
        /// 创建一个原子化 Dropdown 控件
        /// </summary>
        protected TMP_Dropdown CreateDropdown(Transform parent, string label, string localizationKey,
            System.Collections.Generic.List<string> options, int currentIndex,
            UnityEngine.Events.UnityAction<int> onValueChanged)
        {
            var dropdown = UILayoutHelper.InstantiateDropdownPrefab(atomicDropdownPrefab, parent,
                label, localizationKey, options, currentIndex, onValueChanged);

            if (dropdown != null)
                AddControlSounds(dropdown.gameObject, false);

            return dropdown;
        }

        // ============================================================
        //  Helper: 音频反馈
        // ============================================================

        /// <summary>
        /// 为控件添加悬停音效和点击音效
        /// </summary>
        /// <param name="go">控件 GameObject</param>
        /// <param name="playSoundOnValueChanged">是否在值变化时播放音效（Slider 用 true）</param>
        protected void AddControlSounds(GameObject go, bool playSoundOnValueChanged)
        {
            UILayoutHelper.AddHoverSoundEffect(go, audioSource, hoverSound, 0.5f);
        }

        /// <summary>
        /// 播放点击音效
        /// </summary>
        protected void PlayClickSound()
        {
            if (audioSource != null && clickSound != null)
                audioSource.PlayOneShot(clickSound, 0.8f);
        }

        /// <summary>
        /// 播放悬停音效
        /// </summary>
        protected void PlayHoverSound()
        {
            if (audioSource != null && hoverSound != null)
                audioSource.PlayOneShot(hoverSound, 0.5f);
        }

        // ============================================================
        //  Helper: 刷新 Slider 值（不触发回调）
        // ============================================================

        /// <summary>
        /// 安全设置 Slider 值（临时移除监听器避免触发回调）
        /// </summary>
        protected void SetSliderValueWithoutNotify(Slider slider, float value)
        {
            if (slider == null) return;
            slider.SetValueWithoutNotify(value);
            UILayoutHelper.UpdateSliderValueText(slider, GetFormatForSlider(slider));
        }

        /// <summary>
        /// 安全设置 Slider 值并使用指定格式更新文本
        /// </summary>
        protected void SetSliderValueWithoutNotify(Slider slider, float value, string format, float valueScale = 1f)
        {
            if (slider == null) return;
            slider.SetValueWithoutNotify(value);
            UILayoutHelper.UpdateSliderValueText(slider, format, valueScale);
        }

        /// <summary>
        /// 安全设置 Toggle 值（不触发回调）
        /// </summary>
        protected void SetToggleValueWithoutNotify(Toggle toggle, bool value)
        {
            if (toggle == null) return;
            toggle.SetIsOnWithoutNotify(value);
        }

        /// <summary>
        /// 安全设置 Dropdown 值（不触发回调）
        /// </summary>
        protected void SetDropdownValueWithoutNotify(TMP_Dropdown dropdown, int value)
        {
            if (dropdown == null) return;
            dropdown.SetValueWithoutNotify(value);
            dropdown.RefreshShownValue();
        }

        /// <summary>
        /// 获取 Slider 对应的格式字符串（子类可重写）
        /// </summary>
        protected virtual string GetFormatForSlider(Slider slider)
        {
            return "{0:F2}";
        }
    }
}
