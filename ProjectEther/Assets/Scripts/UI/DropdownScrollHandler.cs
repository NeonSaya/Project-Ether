using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

namespace OsuVR
{
    [RequireComponent(typeof(TMP_Dropdown))]
    public class DropdownScrollHandler : MonoBehaviour
    {
        [Header("Input Settings")]
        [Tooltip("右手摇杆动作 (用于滚动下拉菜单)")]
        public InputActionProperty scrollAction;
        
        [Header("Scroll Settings")]
        public float scrollSpeed = 0.5f;
        public float deadzone = 0.3f;

        private TMP_Dropdown dropdown;
        private ScrollRect scrollRect;
        private bool isDropdownOpen = false;
        private float targetScrollPosition;

        void Awake()
        {
            dropdown = GetComponent<TMP_Dropdown>();
        }

        void OnEnable()
        {
            if (scrollAction.action != null && scrollAction.action.bindings.Count > 0)
            {
                scrollAction.action.Enable();
            }
        }

        void OnDisable()
        {
            if (scrollAction.action != null && scrollAction.action.bindings.Count > 0)
            {
                scrollAction.action.Disable();
            }
        }

        void Start()
        {
            if (dropdown != null && dropdown.template != null)
            {
                scrollRect = dropdown.template.GetComponentInChildren<ScrollRect>();
            }
        }

        void Update()
        {
            CheckDropdownState();
            
            if (isDropdownOpen && scrollRect != null)
            {
                HandleScrollInput();
            }
        }

        private void CheckDropdownState()
        {
            if (dropdown == null || dropdown.template == null) return;
            
            bool wasOpen = isDropdownOpen;
            isDropdownOpen = dropdown.IsActive() && dropdown.template.gameObject.activeInHierarchy;
            
            if (isDropdownOpen && !wasOpen)
            {
                if (scrollRect == null)
                {
                    scrollRect = dropdown.template.GetComponentInChildren<ScrollRect>();
                }
                targetScrollPosition = scrollRect != null ? scrollRect.verticalNormalizedPosition : 1f;
            }
        }

        private void HandleScrollInput()
        {
            if (scrollRect == null || scrollRect.content == null) return;

            float scrollValue = 0f;

            if (scrollAction.action != null && scrollAction.action.bindings.Count > 0)
            {
                Vector2 stickInput = scrollAction.action.ReadValue<Vector2>();
                if (Mathf.Abs(stickInput.y) > deadzone)
                {
                    scrollValue = stickInput.y;
                }
            }

            if (Mouse.current != null)
            {
                Vector2 mouseScroll = Mouse.current.scroll.ReadValue();
                if (Mathf.Abs(mouseScroll.y) > 0.1f)
                {
                    scrollValue = mouseScroll.y > 0 ? 1f : -1f;
                }
            }

            if (Mathf.Abs(scrollValue) > 0.01f)
            {
                float contentHeight = scrollRect.content.rect.height;
                float viewportHeight = scrollRect.viewport.rect.height;
                
                if (contentHeight > viewportHeight)
                {
                    float scrollAmount = scrollValue * scrollSpeed * Time.deltaTime;
                    float normalizedScrollAmount = scrollAmount / (contentHeight - viewportHeight);
                    
                    targetScrollPosition = Mathf.Clamp01(targetScrollPosition + normalizedScrollAmount);
                    
                    scrollRect.verticalNormalizedPosition = Mathf.Lerp(
                        scrollRect.verticalNormalizedPosition,
                        targetScrollPosition,
                        Time.deltaTime * 15f
                    );
                }
            }
        }
    }
}
