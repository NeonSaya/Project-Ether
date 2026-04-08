using UnityEngine;
using UnityEditor;
using UnityEditor.PackageManager;

namespace OsuVR.Editor
{
    /// <summary>
    /// AudioLink安装助手：提供一键安装和配置功能
    /// </summary>
    public class AudioLinkInstaller : EditorWindow
    {
        private bool isInstalling = false;
        private string installStatus = "";
        private Vector2 scrollPosition;

        [MenuItem("Tools/Project Ether/AudioLink安装助手")]
        public static void ShowWindow()
        {
            var window = GetWindow<AudioLinkInstaller>("AudioLink安装助手");
            window.minSize = new Vector2(500, 600);
            window.Show();
        }

        void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("AudioLink 集成指南", EditorStyles.boldLabel);
            EditorGUILayout.Space(10);

            DrawInstallationSection();
            EditorGUILayout.Space(20);
            DrawConfigurationSection();
            EditorGUILayout.Space(20);
            DrawShaderUsageSection();
            EditorGUILayout.Space(20);
            DrawTroubleshootingSection();

            EditorGUILayout.EndScrollView();
        }

        void DrawInstallationSection()
        {
            EditorGUILayout.LabelField("1. 安装方法", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "AudioLink支持URP渲染管线（版本3.1.0+）\n" +
                "推荐使用以下方法之一进行安装：",
                MessageType.Info
            );

            EditorGUILayout.Space(10);

            // 方法1：Git Package
            EditorGUILayout.LabelField("方法A：Git Package（推荐）", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "在 Packages/manifest.json 中添加依赖：\n\n" +
                "\"com.llealloo.audiolink\": \"https://github.com/llealloo/vrc-udon-audio-link.git\"\n\n" +
                "或使用 Package Manager 的 'Add package from git URL' 功能",
                MessageType.None
            );

            if (GUILayout.Button("复制Git URL到剪贴板", GUILayout.Height(30)))
            {
                EditorGUIUtility.systemCopyBuffer = "https://github.com/llealloo/vrc-udon-audio-link.git";
                Debug.Log("[AudioLinkInstaller] Git URL已复制到剪贴板");
            }

            EditorGUILayout.Space(10);

            // 方法2：UnityPackage
            EditorGUILayout.LabelField("方法B：UnityPackage下载", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "从GitHub Releases页面下载最新的UnityPackage：\n" +
                "https://github.com/llealloo/vrc-udon-audio-link/releases\n\n" +
                "推荐版本：AudioLink 3.1.2（支持URP）",
                MessageType.None
            );

            if (GUILayout.Button("打开GitHub Releases页面", GUILayout.Height(30)))
            {
                Application.OpenURL("https://github.com/llealloo/vrc-udon-audio-link/releases");
            }

            EditorGUILayout.Space(10);

            // 方法3：VCC
            EditorGUILayout.LabelField("方法C：VRChat Creator Companion（仅VRChat项目）", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "如果您的项目已配置VCC，可以直接在VCC中添加AudioLink包。\n" +
                "注意：此方法需要VRChat SDK。",
                MessageType.Warning
            );
        }

        void DrawConfigurationSection()
        {
            EditorGUILayout.LabelField("2. 场景配置", EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "安装完成后，按以下步骤配置场景：",
                MessageType.Info
            );

            EditorGUILayout.Space(5);

            EditorGUILayout.LabelField("步骤1：添加AudioLink预制体", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "在Project窗口中找到：\n" +
                "Packages/com.llealloo.audiolink/Runtime/Prefabs/AudioLink.prefab\n\n" +
                "拖入场景Hierarchy中。",
                MessageType.None
            );

            EditorGUILayout.Space(5);

            EditorGUILayout.LabelField("步骤2：添加AudioLinkAdapter", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "将 AudioLinkAdapter.cs 脚本挂载到AudioLink物体上。\n" +
                "适配器会自动同步音频源。",
                MessageType.None
            );

            EditorGUILayout.Space(5);

            EditorGUILayout.LabelField("步骤3：配置AudioSource", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "AudioLinkAdapter会自动从以下位置查找AudioSource：\n" +
                "1. AudioVisualizationManager.targetAudioSource\n" +
                "2. MusicManager.Instance.GetAudioSource()\n" +
                "3. 场景中的第一个AudioSource\n\n" +
                "也可以手动调用 SetAudioSource() 方法。",
                MessageType.None
            );

            EditorGUILayout.Space(10);

            if (GUILayout.Button("自动配置场景（实验性）", GUILayout.Height(35)))
            {
                AutoConfigureScene();
            }
        }

        void DrawShaderUsageSection()
        {
            EditorGUILayout.LabelField("3. Shader使用指南", EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "AudioLink提供两种数据访问方式：",
                MessageType.Info
            );

            EditorGUILayout.Space(5);

            // 方式1：全局Shader变量
            EditorGUILayout.LabelField("方式A：全局Shader变量（推荐）", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "AudioVisualizationManager提供的全局变量：\n\n" +
                "• _Global_Audio_Bass (Float): 低频能量 0-1\n" +
                "• _Global_Audio_Mid (Float): 中频能量 0-1\n" +
                "• _Global_Audio_Treble (Float): 高频能量 0-1\n\n" +
                "在Shader Graph中：\n" +
                "1. 创建Property节点\n" +
                "2. 设置Mode为Global\n" +
                "3. 输入变量名（如 _Global_Audio_Bass）",
                MessageType.None
            );

            EditorGUILayout.Space(5);

            // 方式2：AudioLink纹理
            EditorGUILayout.LabelField("方式B：AudioLink纹理（高级）", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "AudioLink全局纹理：_AudioTexture\n\n" +
                "纹理包含的数据区域：\n" +
                "• ALPASS_GENERALVU: 通用VU数据\n" +
                "• ALPASS_DFT: DFT频谱数据\n" +
                "• ALPASS_WAVEFORM: 波形数据\n" +
                "• ALPASS_AUTOCORRELATOR: 自相关数据\n" +
                "• ALPASS_COLORCHORD: 色彩和弦\n\n" +
                "详细文档：\n" +
                "https://github.com/llealloo/vrc-udon-audio-link/blob/master/Documentation~/ShaderCreation.md",
                MessageType.None
            );

            if (GUILayout.Button("打开Shader创建文档", GUILayout.Height(30)))
            {
                Application.OpenURL("https://github.com/llealloo/vrc-udon-audio-link/blob/master/Documentation~/ShaderCreation.md");
            }
        }

        void DrawTroubleshootingSection()
        {
            EditorGUILayout.LabelField("4. 常见问题", EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "Q: AudioLink不响应音频？\n" +
                "A: 检查AudioSource是否正确绑定，查看AudioLinkAdapter的调试日志。\n\n" +
                "Q: Shader中读取不到全局变量？\n" +
                "A: 确保AudioVisualizationManager正在运行，检查变量名拼写。\n\n" +
                "Q: URP渲染问题？\n" +
                "A: 确保使用AudioLink 3.1.0+版本，该版本支持URP。\n\n" +
                "Q: 性能问题？\n" +
                "A: AudioLink使用GPU计算，对CPU影响小。如需优化，可降低纹理分辨率。",
                MessageType.Info
            );

            EditorGUILayout.Space(10);

            EditorGUILayout.HelpBox(
                "推荐工作流：\n\n" +
                "1. 使用全局Shader变量（_Global_Audio_Bass/Mid/Treble）进行基础音频响应\n" +
                "2. 使用AudioLink纹理（_AudioTexture）进行高级频谱分析\n" +
                "3. 通过AudioLinkAdapter自动同步音频源\n" +
                "4. 在RhythmGameManager中注入音频源",
                MessageType.Info
            );
        }

        void AutoConfigureScene()
        {
            Debug.Log("[AudioLinkInstaller] 开始自动配置场景...");

            // 检查AudioVisualizationManager
            var audioVisManager = FindObjectOfType<AudioVisualizationManager>();
            if (audioVisManager == null)
            {
                Debug.LogWarning("[AudioLinkInstaller] 场景中未找到AudioVisualizationManager，请先创建AudioVisualizationSystem物体");
                return;
            }

            // 检查AudioLinkAdapter
            var adapter = FindObjectOfType<AudioLinkAdapter>();
            if (adapter == null)
            {
                // 尝试查找AudioLink组件
                var audioLinkType = System.Type.GetType("AudioLink, AudioLink");
                if (audioLinkType != null)
                {
                    var audioLink = FindObjectOfType(audioLinkType) as MonoBehaviour;
                    if (audioLink != null)
                    {
                        adapter = audioLink.gameObject.AddComponent<AudioLinkAdapter>();
                        Debug.Log($"[AudioLinkInstaller] 已添加AudioLinkAdapter到: {audioLink.gameObject.name}");
                    }
                    else
                    {
                        Debug.LogWarning("[AudioLinkInstaller] 场景中未找到AudioLink组件，请先添加AudioLink预制体");
                        return;
                    }
                }
                else
                {
                    Debug.LogWarning("[AudioLinkInstaller] AudioLink未安装，请先安装AudioLink包");
                    return;
                }
            }

            Debug.Log("[AudioLinkInstaller] ✅ 场景配置完成！");
            Debug.Log("[AudioLinkInstaller] 提示：运行游戏后，AudioLinkAdapter会自动同步音频源");
        }
    }
}
