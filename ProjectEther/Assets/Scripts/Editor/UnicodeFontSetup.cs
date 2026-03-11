#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using TMPro;
using UnityEngine.TextCore.LowLevel;
using System.IO;

namespace OsuVR.Editor
{
    public class UnicodeFontSetup : EditorWindow
    {
        [MenuItem("Project Ether/工具/Unicode字体设置")]
        public static void ShowWindow()
        {
            GetWindow<UnicodeFontSetup>("Unicode字体设置");
        }

        private string systemFontPath = "";
        private int atlasSize = 2048;
        private bool useDynamicMode = true;

        private static readonly string[] CommonChineseFonts = new string[]
        {
            "C:\\Windows\\Fonts\\msyh.ttc",
            "C:\\Windows\\Fonts\\msyhbd.ttc",
            "C:\\Windows\\Fonts\\simhei.ttf",
            "C:\\Windows\\Fonts\\simsun.ttc",
            "C:\\Windows\\Fonts\\simkai.ttf",
            "C:\\Windows\\Fonts\\STZHONGS.TTF",
            "C:\\Windows\\Fonts\\STFANGSO.TTF",
        };

        void OnGUI()
        {
            GUILayout.Label("Unicode字体设置", EditorStyles.boldLabel);
            GUILayout.Space(10);

            EditorGUILayout.HelpBox(
                "此工具帮助您设置支持中文/日文等Unicode字符的TMP字体。\n\n" +
                "步骤:\n" +
                "1. 选择一个支持Unicode的字体文件\n" +
                "2. 点击'生成字体资产'\n" +
                "3. 点击'配置为Fallback字体'\n\n" +
                "推荐字体:\n" +
                "- 微软雅黑 (msyh.ttc) - Windows系统自带\n" +
                "- 思源黑体 - 从Google Fonts下载\n" +
                "- Noto Sans CJK - 从Google Fonts下载",
                MessageType.Info
            );

            GUILayout.Space(10);

            GUILayout.Label("系统字体检测:", EditorStyles.boldLabel);
            foreach (string fontPath in CommonChineseFonts)
            {
                bool exists = File.Exists(fontPath);
                string fontName = Path.GetFileName(fontPath);
                if (exists)
                {
                    GUILayout.BeginHorizontal();
                    GUI.color = Color.green;
                    GUILayout.Label("✓", GUILayout.Width(20));
                    GUI.color = Color.white;
                    GUILayout.Label(fontName);
                    if (GUILayout.Button("选择", GUILayout.Width(50)))
                    {
                        systemFontPath = fontPath;
                    }
                    GUILayout.EndHorizontal();
                }
            }

            GUILayout.Space(10);

            GUILayout.Label("自定义字体路径:", EditorStyles.boldLabel);
            systemFontPath = EditorGUILayout.TextField(systemFontPath);
            if (GUILayout.Button("浏览...", GUILayout.Width(80)))
            {
                string path = EditorUtility.OpenFilePanel("选择字体文件", "C:\\Windows\\Fonts", "ttf,ttc,otf");
                if (!string.IsNullOrEmpty(path))
                {
                    systemFontPath = path;
                }
            }

            GUILayout.Space(10);

            GUILayout.Label("设置:", EditorStyles.boldLabel);
            atlasSize = EditorGUILayout.IntPopup("图集大小", atlasSize,
                new string[] { "1024", "2048", "4096" },
                new int[] { 1024, 2048, 4096 });
            useDynamicMode = EditorGUILayout.Toggle("使用动态模式 (推荐)", useDynamicMode);

            GUILayout.Space(20);

            EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(systemFontPath) || !File.Exists(systemFontPath));
            {
                if (GUILayout.Button("生成字体资产", GUILayout.Height(40)))
                {
                    GenerateFontAsset();
                }
            }
            EditorGUI.EndDisabledGroup();

            GUILayout.Space(10);

            if (GUILayout.Button("配置为Fallback字体", GUILayout.Height(30)))
            {
                ConfigureFallback();
            }

            GUILayout.Space(10);

            if (GUILayout.Button("检查当前配置", GUILayout.Height(30)))
            {
                CheckCurrentConfig();
            }
        }

        private void GenerateFontAsset()
        {
            if (!File.Exists(systemFontPath))
            {
                EditorUtility.DisplayDialog("错误", "字体文件不存在", "确定");
                return;
            }

            string fontName = Path.GetFileNameWithoutExtension(systemFontPath);
            if (fontName.Contains("."))
            {
                fontName = fontName.Substring(0, fontName.IndexOf('.'));
            }

            string destFolder = "Assets/TextMesh Pro/Resources/Fonts & Materials";
            if (!Directory.Exists(destFolder))
            {
                Directory.CreateDirectory(destFolder);
            }

            string destFontPath = Path.Combine(destFolder, fontName + ".ttf");
            if (!File.Exists(destFontPath))
            {
                File.Copy(systemFontPath, destFontPath, true);
                AssetDatabase.ImportAsset(destFontPath);
            }

            Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(destFontPath);
            if (sourceFont == null)
            {
                EditorUtility.DisplayDialog("错误", "无法加载字体文件", "确定");
                return;
            }

            string assetPath = Path.Combine(destFolder, fontName + " SDF.asset");

            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(sourceFont, 90, 9, 
                GlyphRenderMode.SDFAA, 
                atlasSize, atlasSize, 
                AtlasPopulationMode.Dynamic);
            
            if (fontAsset == null)
            {
                EditorUtility.DisplayDialog("错误", "创建字体资产失败", "确定");
                return;
            }

            AssetDatabase.CreateAsset(fontAsset, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("成功",
                $"字体资产创建成功!\n\n路径: {assetPath}\n\n" +
                "请点击'配置为Fallback字体'按钮将其添加到TMP Settings中。",
                "确定");

            Debug.Log($"[UnicodeFontSetup] 字体资产创建成功: {assetPath}");
        }

        private void ConfigureFallback()
        {
            TMP_Settings settings = TMP_Settings.instance;
            if (settings == null)
            {
                EditorUtility.DisplayDialog("错误", "无法找到TMP Settings", "确定");
                return;
            }

            string fontsFolder = "Assets/TextMesh Pro/Resources/Fonts & Materials";
            string[] guids = AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { fontsFolder });

            var unicodeFonts = new System.Collections.Generic.List<TMP_FontAsset>();
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                if (font != null && font.name != "LiberationSans SDF")
                {
                    unicodeFonts.Add(font);
                }
            }

            if (unicodeFonts.Count == 0)
            {
                EditorUtility.DisplayDialog("提示",
                    "未找到Unicode字体资产。\n请先使用'生成字体资产'按钮创建一个。",
                    "确定");
                return;
            }

            SerializedObject so = new SerializedObject(settings);
            SerializedProperty fallbackProp = so.FindProperty("m_fallbackFontAssets");

            fallbackProp.arraySize = unicodeFonts.Count;
            for (int i = 0; i < unicodeFonts.Count; i++)
            {
                fallbackProp.GetArrayElementAtIndex(i).objectReferenceValue = unicodeFonts[i];
                Debug.Log($"[UnicodeFontSetup] 添加Fallback字体: {unicodeFonts[i].name}");
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog("成功",
                $"已将 {unicodeFonts.Count} 个字体添加到TMP Settings的Fallback列表中。\n\n" +
                "现在游戏应该可以正确显示中文、日文等Unicode字符了！",
                "确定");
        }

        private void CheckCurrentConfig()
        {
            TMP_Settings settings = TMP_Settings.instance;
            if (settings == null)
            {
                EditorUtility.DisplayDialog("配置检查", "无法找到TMP Settings", "确定");
                return;
            }

            string info = "当前TMP Settings配置:\n\n";
            info += $"默认字体: {(TMP_Settings.defaultFontAsset != null ? TMP_Settings.defaultFontAsset.name : "无")}\n\n";

            info += "Fallback字体列表:\n";
            var fallbacks = TMP_Settings.fallbackFontAssets;
            if (fallbacks == null || fallbacks.Count == 0)
            {
                info += "  (空)\n";
            }
            else
            {
                foreach (var font in fallbacks)
                {
                    info += $"  - {(font != null ? font.name : "null")}\n";
                }
            }

            EditorUtility.DisplayDialog("配置检查", info, "确定");
        }
    }
}
#endif
