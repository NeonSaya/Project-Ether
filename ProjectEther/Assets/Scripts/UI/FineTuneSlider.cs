using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace OsuVR
{
    public class FineTuneSlider : MonoBehaviour
    {
        [Header("References")]
        public Slider targetSlider;
        public TextMeshProUGUI valueText;

        [Header("Settings")]
        public float minValue = -200f;
        public float maxValue = 200f;
        public string format = "ms";

        private Button[] fineTuneButtons;

        void Start()
        {
            SetupButtons();
            UpdateValueText();
            
            if (targetSlider != null)
            {
                targetSlider.onValueChanged.AddListener(OnSliderValueChanged);
            }
        }

        void OnEnable()
        {
            SetupButtons();
        }

        private void SetupButtons()
        {
            if (fineTuneButtons != null && fineTuneButtons.Length > 0) return;
            
            fineTuneButtons = GetComponentsInChildren<Button>();
            
            foreach (var btn in fineTuneButtons)
            {
                if (btn == null) continue;
                
                string btnName = btn.name.ToLower();
                
                if (btnName.Contains("decreasebtn") && !btnName.Contains("1") && !btnName.Contains("5"))
                {
                    btn.onClick.AddListener(() => AdjustValue(-10));
                }
                else if (btnName.Contains("decreasebtn5"))
                {
                    btn.onClick.AddListener(() => AdjustValue(-5));
                }
                else if (btnName.Contains("decreasebtn1"))
                {
                    btn.onClick.AddListener(() => AdjustValue(-1));
                }
                else if (btnName.Contains("increasebtn1"))
                {
                    btn.onClick.AddListener(() => AdjustValue(1));
                }
                else if (btnName.Contains("increasebtn5"))
                {
                    btn.onClick.AddListener(() => AdjustValue(5));
                }
                else if (btnName.Contains("increasebtn") && !btnName.Contains("1") && !btnName.Contains("5"))
                {
                    btn.onClick.AddListener(() => AdjustValue(10));
                }
            }
        }

        public void AdjustValue(float delta)
        {
            if (targetSlider == null) return;
            
            float newValue = Mathf.Clamp(targetSlider.value + delta, minValue, maxValue);
            targetSlider.value = newValue;
            UpdateValueText();
        }

        private void OnSliderValueChanged(float value)
        {
            UpdateValueText();
        }

        private void UpdateValueText()
        {
            if (valueText == null || targetSlider == null) return;
            
            string displayValue = FormatValue(targetSlider.value);
            valueText.text = displayValue;
        }

        private string FormatValue(float value)
        {
            switch (format)
            {
                case "percent":
                    return Mathf.RoundToInt(value * 100) + "%";
                case "ms":
                    return Mathf.RoundToInt(value) + " ms";
                case "m":
                    return value.ToString("F2") + " m";
                case "deg":
                    return Mathf.RoundToInt(value) + "°";
                default:
                    return Mathf.RoundToInt(value).ToString();
            }
        }
    }
}
