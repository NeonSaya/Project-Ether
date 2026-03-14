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

        // --- 新增：一键配置变量 ---
        private TMP_FontAsset mainFontAsset;
        private TMP_FontAsset fallbackFontAsset;

        // --- 原有：系统字体生成变量 ---
        private bool showSystemGenerator = false;
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
            // ==========================================
            // 模块 1：全新的思源黑体一键配置区
            // ==========================================
            GUILayout.Label("⭐ 一键全局字体与后备配置", EditorStyles.boldLabel);
            GUILayout.Space(5);

            EditorGUILayout.HelpBox(
                "最佳工作流：\n" +
                "1. 将下载的思源黑体 .otf 拖入 Unity\n" +
                "2. 右键字体文件 -> Create -> TextMeshPro -> Font Asset\n" +
                "3. 选中生成的资产，把 Atlas Population Mode 改为 Dynamic\n" +
                "4. 将它们拖入下方槽位，点击一键配置即可！",
                MessageType.Info
            );

            GUILayout.Space(10);
            mainFontAsset = (TMP_FontAsset)EditorGUILayout.ObjectField("主字体 (如简中)", mainFontAsset, typeof(TMP_FontAsset), false);
            fallbackFontAsset = (TMP_FontAsset)EditorGUILayout.ObjectField("后备字体 (如日文)", fallbackFontAsset, typeof(TMP_FontAsset), false);

            GUILayout.Space(10);
            EditorGUI.BeginDisabledGroup(mainFontAsset == null);
            {
                if (GUILayout.Button("一键设为全局默认并绑定后备", GUILayout.Height(40)))
                {
                    ApplyOneClickSetup();
                }
            }
            EditorGUI.EndDisabledGroup();

            GUILayout.Space(10);

            if (GUILayout.Button("检查当前 TMP 全局配置", GUILayout.Height(30)))
            {
                CheckCurrentConfig();
            }

            GUILayout.Space(20);

            // ==========================================
            // 模块 2：原有的本地系统字体生成区 (收纳折叠)
            // ==========================================
            GUIStyle foldoutStyle = new GUIStyle(EditorStyles.foldout);
            foldoutStyle.fontStyle = FontStyle.Bold;
            showSystemGenerator = EditorGUILayout.Foldout(showSystemGenerator, "高级：从 Windows 系统生成字体 (旧版功能)", foldoutStyle);

            if (showSystemGenerator)
            {
                GUILayout.Space(10);
                GUILayout.Label("系统字体检测:");
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
                GUILayout.Label("自定义字体路径:");
                systemFontPath = EditorGUILayout.TextField(systemFontPath);
                if (GUILayout.Button("浏览...", GUILayout.Width(80)))
                {
                    string path = EditorUtility.OpenFilePanel("选择字体文件", "C:\\Windows\\Fonts", "ttf,ttc,otf");
                    if (!string.IsNullOrEmpty(path)) systemFontPath = path;
                }

                GUILayout.Space(10);
                atlasSize = EditorGUILayout.IntPopup("图集大小", atlasSize,
                    new string[] { "1024", "2048", "4096" },
                    new int[] { 1024, 2048, 4096 });
                useDynamicMode = EditorGUILayout.Toggle("使用动态模式 (推荐)", useDynamicMode);

                GUILayout.Space(10);
                EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(systemFontPath) || !File.Exists(systemFontPath));
                {
                    if (GUILayout.Button("生成字体资产", GUILayout.Height(30)))
                    {
                        GenerateFontAsset();
                    }
                }
                EditorGUI.EndDisabledGroup();
            }
        }

        // ==========================================
        // 核心逻辑：一键配置
        // ==========================================
        private void ApplyOneClickSetup()
        {
            TMP_Settings settings = TMP_Settings.instance;
            if (settings == null)
            {
                settings = Resources.Load<TMP_Settings>("TMP Settings");
                if (settings == null)
                {
                    EditorUtility.DisplayDialog("错误", "找不到 TMP Settings。\n请先在顶部菜单栏选择 Window -> TextMeshPro -> Import TMP Essential Resources。", "确定");
                    return;
                }
            }

            SerializedObject so = new SerializedObject(settings);

            // 1. 设置默认全局字体
            SerializedProperty defaultFontProp = so.FindProperty("m_defaultFontAsset");
            defaultFontProp.objectReferenceValue = mainFontAsset;

            // 2. 设置全局后备字体 (Fallback)
            if (fallbackFontAsset != null)
            {
                SerializedProperty fallbackProp = so.FindProperty("m_fallbackFontAssets");

                // 检查是否已经在这个列表里了，防止重复添加
                bool exists = false;
                for (int i = 0; i < fallbackProp.arraySize; i++)
                {
                    if (fallbackProp.GetArrayElementAtIndex(i).objectReferenceValue == fallbackFontAsset)
                    {
                        exists = true;
                        break;
                    }
                }

                // 如果没在里面，就加到列表末尾
                if (!exists)
                {
                    fallbackProp.arraySize++;
                    fallbackProp.GetArrayElementAtIndex(fallbackProp.arraySize - 1).objectReferenceValue = fallbackFontAsset;
                }
            }

            // 3. 应用并保存
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog("太棒了！配置成功",
                $"【全局默认字体】已设为: {mainFontAsset.name}\n" +
                (fallbackFontAsset != null ? $"【全局后备字体】已添加: {fallbackFontAsset.name}\n\n" : "\n\n") +
                "您的游戏现在可以完美显示 CJK (中日韩) 字符了！",
                "确定");
        }

        // ==========================================
        // 原有逻辑保留区
        // ==========================================
        private void CheckCurrentConfig()
        {
            TMP_Settings settings = TMP_Settings.instance;
            if (settings == null) settings = Resources.Load<TMP_Settings>("TMP Settings");

            if (settings == null)
            {
                EditorUtility.DisplayDialog("配置检查", "无法找到 TMP Settings，请先导入 Essential Resources。", "确定");
                return;
            }

            string info = "当前 TMP Settings 全局配置:\n\n";
            info += $"【默认主字体】: {(TMP_Settings.defaultFontAsset != null ? TMP_Settings.defaultFontAsset.name : "无")}\n\n";
            info += "【Fallback 后备字体列表】:\n";

            var fallbacks = TMP_Settings.fallbackFontAssets;
            if (fallbacks == null || fallbacks.Count == 0)
            {
                info += "  (空)\n";
            }
            else
            {
                foreach (var font in fallbacks)
                {
                    info += $"  - {(font != null ? font.name : "丢失的引用")}\n";
                }
            }

            EditorUtility.DisplayDialog("配置检查", info, "确定");
        }

        private void GenerateFontAsset()
        {
            // 你原有的 Windows 系统字体生成逻辑 (完全保留未动)
            if (!File.Exists(systemFontPath))
            {
                EditorUtility.DisplayDialog("错误", "字体文件不存在", "确定");
                return;
            }

            string fontName = Path.GetFileNameWithoutExtension(systemFontPath);
            if (fontName.Contains(".")) fontName = fontName.Substring(0, fontName.IndexOf('.'));

            string destFolder = "Assets/TextMesh Pro/Resources/Fonts & Materials";
            if (!Directory.Exists(destFolder)) Directory.CreateDirectory(destFolder);

            string destFontPath = $"{destFolder}/{fontName}{Path.GetExtension(systemFontPath)}";
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

            string assetPath = $"{destFolder}/{fontName} SDF.asset";

            AtlasPopulationMode mode = useDynamicMode ? AtlasPopulationMode.Dynamic : AtlasPopulationMode.Static;

            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(sourceFont, 90, 9,
                GlyphRenderMode.SDFAA, atlasSize, atlasSize, mode);

            if (fontAsset == null)
            {
                EditorUtility.DisplayDialog("错误", "创建字体资产失败", "确定");
                return;
            }

            AssetDatabase.CreateAsset(fontAsset, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("成功", $"字体资产创建成功!\n\n路径: {assetPath}", "确定");
            Debug.Log($"[UnicodeFontSetup] 字体资产创建成功: {assetPath}");
        }
    }
}
#endif