using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

namespace OsuVR
{
    /// <summary>
    /// 全局 Toast 提示：世界空间 Canvas，跟随相机，始终压在普通 UI 之上
    /// （sortingOrder=500，介于主 UI=100 与场景切换遮罩=9999 之间）。
    /// 跨场景存活，适合"提示后切场景"的报错路径。
    /// </summary>
    public static class VRToast
    {
        private const float DefaultDuration = 3f;

        private static ToastBehaviour _behaviour;

        public static void Show(string message, Color color, float duration = DefaultDuration)
        {
            if (string.IsNullOrEmpty(message))
                return;
            if (_behaviour == null)
            {
                var go = new GameObject("[VRToast]");
                if (Application.isPlaying)
                    Object.DontDestroyOnLoad(go);
                _behaviour = go.AddComponent<ToastBehaviour>();
            }
            _behaviour.Show(message, color, duration);
        }

        private class ToastBehaviour : MonoBehaviour
        {
            private TextMeshProUGUI _tmp;
            private Color _color;
            private Canvas _canvas;
            private readonly List<XRDisplaySubsystem> _displays = new();
            private float _elapsed;
            private float _duration;
            private bool _active;
            private bool _hasFocus;
            private bool _paused;
            private bool _readyLastFrame;

            void Awake()
            {
                _hasFocus = Application.isFocused;
                EnsureCreated();
            }

            private void EnsureCreated()
            {
                if (_tmp != null)
                    return;

                transform.localScale = Vector3.one * 0.002f;

                _canvas = gameObject.AddComponent<Canvas>();
                _canvas.renderMode = RenderMode.WorldSpace;
                // 根 Canvas 的 sortingOrder 直接生效（overrideSorting 只对嵌套 Canvas 有意义）
                _canvas.sortingOrder = 500;
                _canvas.enabled = false;
                var scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                scaler.dynamicPixelsPerUnit = 10f;

                var textGo = new GameObject("Text");
                textGo.transform.SetParent(transform, false);
                var rt = textGo.AddComponent<RectTransform>();
                rt.sizeDelta = new Vector2(800f, 120f);

                _tmp = textGo.AddComponent<TextMeshProUGUI>();
                _tmp.fontSize = 22f;
                _tmp.fontStyle = FontStyles.Bold;
                _tmp.alignment = TextAlignmentOptions.Center;
                _tmp.enableWordWrapping = true;
                _tmp.overflowMode = TextOverflowModes.Ellipsis;
                _tmp.raycastTarget = false;
            }

            public void Show(string message, Color color, float duration)
            {
                EnsureCreated();
                _tmp.text = message;
                _color = color;
                _tmp.color = color;
                _elapsed = 0f;
                _duration = Mathf.Max(0.1f, duration);
                _active = true;
                _readyLastFrame = false;
                _canvas.enabled = false;
            }

            private bool CanPresent(Camera camera)
            {
                if (!_hasFocus || _paused || camera == null)
                    return false;
                if (
                    VRSceneTransitionManager.Instance != null
                    && VRSceneTransitionManager.Instance.IsTransitioning
                )
                    return false;

                if (Application.platform == RuntimePlatform.Android || XRSettings.isDeviceActive)
                {
                    SubsystemManager.GetInstances(_displays);
                    foreach (var display in _displays)
                        if (display.running)
                            return true;
                    return false;
                }
                return true;
            }

            void OnApplicationFocus(bool hasFocus)
            {
                _hasFocus = hasFocus;
                _readyLastFrame = false;
            }

            void OnApplicationPause(bool paused)
            {
                _paused = paused;
                _readyLastFrame = false;
            }

            void LateUpdate()
            {
                if (!_active)
                    return;

                // 跟随相机：放在玩家视线前方 1.5m、略低于视线，任何朝向都能看到
                var cam = Camera.main;
                if (cam == null)
                    cam = FindFirstObjectByType<Camera>();
                if (!CanPresent(cam))
                {
                    _canvas.enabled = false;
                    _readyLastFrame = false;
                    return;
                }

                if (cam != null)
                {
                    Vector3 forward = cam.transform.forward;
                    forward.y = 0f;
                    if (forward.sqrMagnitude < 0.001f)
                        forward = Vector3.forward;
                    forward.Normalize();
                    transform.position =
                        cam.transform.position + forward * 1.5f + Vector3.down * 0.2f;
                    transform.rotation = Quaternion.LookRotation(
                        transform.position - cam.transform.position
                    );
                }

                _canvas.enabled = true;
                // 返回 VR 的首帧可能携带整个暂停间隔，先显示，再从后续可见帧计时。
                if (_readyLastFrame)
                    _elapsed += Time.unscaledDeltaTime;
                _readyLastFrame = true;
                if (_elapsed >= _duration)
                {
                    _active = false;
                    _tmp.text = string.Empty;
                    _canvas.enabled = false;
                    return;
                }

                float fadeStart = _duration * 0.6f;
                float alpha =
                    _elapsed > fadeStart
                        ? 1f - (_elapsed - fadeStart) / (_duration - fadeStart)
                        : 1f;
                _tmp.color = new Color(_color.r, _color.g, _color.b, _color.a * alpha);
            }
        }
    }
}
