using UnityEngine;
using UnityEngine.UI;
using TMPro;

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
            if (string.IsNullOrEmpty(message)) return;
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
            private float _fadeStart;
            private float _duration;
            private bool _active;

            void Awake()
            {
                EnsureCreated();
            }

            private void EnsureCreated()
            {
                if (_tmp != null) return;

                transform.localScale = Vector3.one * 0.002f;

                var canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                // 根 Canvas 的 sortingOrder 直接生效（overrideSorting 只对嵌套 Canvas 有意义）
                canvas.sortingOrder = 500;
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
            }

            public void Show(string message, Color color, float duration)
            {
                EnsureCreated();
                _tmp.text = message;
                _color = color;
                _tmp.color = color;
                _fadeStart = Time.unscaledTime;
                _duration = duration;
                _active = true;
            }

            void LateUpdate()
            {
                // 跟随相机：放在玩家视线前方 1.5m、略低于视线，任何朝向都能看到
                var cam = Camera.main;
                if (cam == null) cam = FindFirstObjectByType<Camera>();
                if (cam != null)
                {
                    Vector3 forward = cam.transform.forward;
                    forward.y = 0f;
                    if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
                    forward.Normalize();
                    transform.position = cam.transform.position + forward * 1.5f + Vector3.down * 0.2f;
                    transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
                }

                if (!_active) return;

                float elapsed = Time.unscaledTime - _fadeStart;
                if (elapsed >= _duration)
                {
                    _active = false;
                    _tmp.text = string.Empty;
                    return;
                }

                float fadeStart = _duration * 0.6f;
                if (elapsed > fadeStart)
                {
                    float alpha = 1f - (elapsed - fadeStart) / (_duration - fadeStart);
                    _tmp.color = new Color(_color.r, _color.g, _color.b, alpha);
                }
            }
        }
    }
}
