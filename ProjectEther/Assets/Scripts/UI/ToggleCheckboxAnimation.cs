using UnityEngine;
using UnityEngine.UI;

namespace OsuVR
{
    [RequireComponent(typeof(Toggle))]
    public class ToggleCheckboxAnimation : MonoBehaviour
    {
        [Header("Animation Settings")]
        public float animationSpeed = 10f;
        
        [Header("Colors")]
        public Color offBgColor = new Color(0.3f, 0.3f, 0.35f, 0.95f);
        public Color onBgColor = new Color(0.2f, 0.5f, 0.8f, 0.95f);
        public Color fillColor = new Color(0.25f, 0.55f, 0.85f, 1f);
        
        [Header("Fill Size")]
        public float fillSizeOn = 20f;
        public float fillSizeOff = 0f;

        private Toggle toggle;
        private RectTransform fillRect;
        private Image backgroundImage;
        private Image fillImage;
        private bool targetState;

        void Awake()
        {
            toggle = GetComponent<Toggle>();
            
            Transform bgTransform = transform.Find("Background");
            if (bgTransform != null)
            {
                backgroundImage = bgTransform.GetComponent<Image>();
                
                Transform fillTransform = bgTransform.Find("Fill");
                if (fillTransform != null)
                {
                    fillRect = fillTransform.GetComponent<RectTransform>();
                    fillImage = fillTransform.GetComponent<Image>();
                }
            }
        }

        void Start()
        {
            if (toggle != null)
            {
                targetState = toggle.isOn;
                SetStateImmediate(toggle.isOn);
                toggle.onValueChanged.AddListener(OnToggleChanged);
            }
        }

        void OnToggleChanged(bool isOn)
        {
            targetState = isOn;
        }

        void Update()
        {
            if (fillRect == null || backgroundImage == null) return;

            float targetSize = targetState ? fillSizeOn : fillSizeOff;
            float currentSize = fillRect.sizeDelta.x;
            
            if (Mathf.Abs(currentSize - targetSize) > 0.5f)
            {
                float newSize = Mathf.Lerp(currentSize, targetSize, Time.deltaTime * animationSpeed);
                fillRect.sizeDelta = new Vector2(newSize, newSize);
                
                Color targetBgColor = targetState ? onBgColor : offBgColor;
                backgroundImage.color = Color.Lerp(backgroundImage.color, targetBgColor, Time.deltaTime * animationSpeed);
                
                if (fillImage != null)
                {
                    float targetAlpha = targetState ? 1f : 0f;
                    Color currentColor = fillImage.color;
                    float newAlpha = Mathf.Lerp(currentColor.a, targetAlpha, Time.deltaTime * animationSpeed);
                    fillImage.color = new Color(fillColor.r, fillColor.g, fillColor.b, newAlpha);
                }
            }
            else
            {
                fillRect.sizeDelta = new Vector2(targetSize, targetSize);
                backgroundImage.color = targetState ? onBgColor : offBgColor;
                
                if (fillImage != null)
                {
                    float alpha = targetState ? 1f : 0f;
                    fillImage.color = new Color(fillColor.r, fillColor.g, fillColor.b, alpha);
                }
            }
        }

        private void SetStateImmediate(bool isOn)
        {
            if (fillRect != null)
            {
                float size = isOn ? fillSizeOn : fillSizeOff;
                fillRect.sizeDelta = new Vector2(size, size);
            }
            
            if (backgroundImage != null)
            {
                backgroundImage.color = isOn ? onBgColor : offBgColor;
            }
            
            if (fillImage != null)
            {
                float alpha = isOn ? 1f : 0f;
                fillImage.color = new Color(fillColor.r, fillColor.g, fillColor.b, alpha);
            }
        }

        void OnDestroy()
        {
            if (toggle != null)
            {
                toggle.onValueChanged.RemoveListener(OnToggleChanged);
            }
        }
    }
}
