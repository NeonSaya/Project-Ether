using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OsuVR
{
    public class GraphyTextBridge : MonoBehaviour
    {
        [Serializable]
        private struct TextBinding
        {
            public Text source;
            public TextMeshProUGUI target;
        }

        [SerializeField]
        private TextBinding[] bindings;

        private void LateUpdate()
        {
            // 保留 Graphy 的统计与颜色更新，只将显示交给项目的 TMP 字体。
            foreach (var binding in bindings)
            {
                if (binding.target.text != binding.source.text)
                    binding.target.text = binding.source.text;
                if (binding.target.color != binding.source.color)
                    binding.target.color = binding.source.color;
            }
        }
    }
}
