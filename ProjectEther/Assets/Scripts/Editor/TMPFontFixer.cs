#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using TMPro;
using System.Collections.Generic;
using System.IO;

namespace OsuVR.Editor
{
    public class TMPFontFixer : EditorWindow
    {
        [MenuItem("Project Ether/工具/修复TMP字体问题")]
        public static void ShowWindow()
        {
            GetWindow<TMPFontFixer>("TMP字体修复工具");
        }

        private TMP_FontAsset defaultFont;
        private bool scanAllScenes = true;
        private bool fixMissingAtlas = true;
        private bool reassignDefaultFont = true;

        void OnGUI()
        {
            GUILayout.Label("TextMesh Pro 字体修复工具", EditorStyles.boldLabel);
            
            EditorGUILayout.Space();
            
            EditorGUILayout.HelpBox(
                "此工具将修复以下问题：\n" +
                "1. 字体Atlas纹理丢失\n" +
                "2. TMP组件引用损坏的字体\n" +
                "3. 重新分配默认字体资源",
                MessageType.Info);
            
            EditorGUILayout.Space();
            
            defaultFont = (TMP_FontAsset)EditorGUILayout.ObjectField(
                "默认字体 (可选)", defaultFont, typeof(TMP_FontAsset), false);
            
            EditorGUILayout.Space();
            
            scanAllScenes = EditorGUILayout.Toggle("扫描所有Prefab和场景", scanAllScenes);
            fixMissingAtlas = EditorGUILayout.Toggle("修复丢失的Atlas纹理", fixMissingAtlas);
            reassignDefaultFont = EditorGUILayout.Toggle("重新分配默认字体", reassignDefaultFont);
            
            EditorGUILayout.Space();
            
            if (GUILayout.Button("查找并修复所有TMP问题", GUILayout.Height(40)))
            {
                FixAllTMPIssues();
            }
            
            EditorGUILayout.Space();
            
            if (GUILayout.Button("重新导入TMP默认资源", GUILayout.Height(30)))
            {
                ReimportTMPDefaultResources();
            }
            
            EditorGUILayout.Space();
            
            if (GUILayout.Button("清理TMP缓存", GUILayout.Height(30)))
            {
                ClearTMPCache();
            }
        }

        void FixAllTMPIssues()
        {
            int fixedCount = 0;
            int errorCount = 0;
            
            // 获取默认字体
            if (defaultFont == null)
            {
                defaultFont = GetDefaultTMPFont();
            }
            
            if (defaultFont == null && reassignDefaultFont)
            {
                Debug.LogError("[TMPFontFixer] 未找到默认字体，请手动指定或确保TMP默认资源已导入");
                return;
            }
            
            // 查找所有TMP字体资源
            var fontAssets = AssetDatabase.FindAssets("t:TMP_FontAsset");
            Debug.Log($"[TMPFontFixer] 找到 {fontAssets.Length} 个字体资源");
            
            // 检查并修复字体资源
            foreach (var guid in fontAssets)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                
                if (font != null && fixMissingAtlas)
                {
                    if (IsFontAssetCorrupted(font))
                    {
                        Debug.LogWarning($"[TMPFontFixer] 发现损坏的字体资源: {path}");
                        
                        if (reassignDefaultFont && defaultFont != null && font != defaultFont)
                        {
                            // 标记为需要替换
                            Debug.Log($"[TMPFontFixer] 将替换使用此字体的组件: {font.name}");
                        }
                    }
                }
            }
            
            // 查找所有Prefab中的TMP组件
            var prefabGuids = AssetDatabase.FindAssets("t:Prefab");
            foreach (var guid in prefabGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                
                if (prefab != null)
                {
                    var tmpComponents = prefab.GetComponentsInChildren<TextMeshProUGUI>(true);
                    var tmp3dComponents = prefab.GetComponentsInChildren<TextMeshPro>(true);
                    
                    foreach (var tmp in tmpComponents)
                    {
                        if (FixTMPComponent(tmp))
                            fixedCount++;
                        else
                            errorCount++;
                    }
                    
                    foreach (var tmp in tmp3dComponents)
                    {
                        if (FixTMPComponent(tmp))
                            fixedCount++;
                        else
                            errorCount++;
                    }
                }
            }
            
            // 查找场景中的TMP组件
            if (scanAllScenes)
            {
                var sceneTmpUGUI = FindObjectsOfType<TextMeshProUGUI>(true);
                var sceneTmp3D = FindObjectsOfType<TextMeshPro>(true);
                
                foreach (var tmp in sceneTmpUGUI)
                {
                    if (FixTMPComponent(tmp))
                        fixedCount++;
                }
                
                foreach (var tmp in sceneTmp3D)
                {
                    if (FixTMPComponent(tmp))
                        fixedCount++;
                }
            }
            
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            Debug.Log($"[TMPFontFixer] 修复完成！修复: {fixedCount}, 错误: {errorCount}");
            
            EditorUtility.DisplayDialog(
                "TMP字体修复完成",
                $"修复完成！\n修复组件数: {fixedCount}\n错误数: {errorCount}",
                "确定");
        }

        bool FixTMPComponent(TMP_Text tmp)
        {
            if (tmp == null) return false;
            
            bool needsFix = false;
            
            // 检查字体是否为空或损坏
            if (tmp.font == null || IsFontAssetCorrupted(tmp.font))
            {
                needsFix = true;
            }
            
            // 检查字体是否在默认资源列表中但损坏
            if (tmp.font != null && tmp.font.atlasTexture == null)
            {
                needsFix = true;
            }
            
            if (needsFix && defaultFont != null)
            {
                Undo.RecordObject(tmp, "Fix TMP Font");
                tmp.font = defaultFont;
                EditorUtility.SetDirty(tmp);
                Debug.Log($"[TMPFontFixer] 修复组件: {GetGameObjectPath(tmp.gameObject)}");
                return true;
            }
            
            return false;
        }

        bool IsFontAssetCorrupted(TMP_FontAsset font)
        {
            if (font == null) return true;
            
            // 检查atlas纹理
            try
            {
                var atlas = font.atlasTexture;
                if (atlas == null)
                {
                    return true;
                }
            }
            catch
            {
                return true;
            }
            
            return false;
        }

        TMP_FontAsset GetDefaultTMPFont()
        {
            // 尝试获取TMP默认字体
            var defaultFonts = AssetDatabase.FindAssets("LiberationSans t:TMP_FontAsset");
            
            foreach (var guid in defaultFonts)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                
                if (font != null && !IsFontAssetCorrupted(font))
                {
                    Debug.Log($"[TMPFontFixer] 找到默认字体: {path}");
                    return font;
                }
            }
            
            // 尝试查找任何可用的字体
            var allFonts = AssetDatabase.FindAssets("t:TMP_FontAsset");
            foreach (var guid in allFonts)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                
                if (font != null && !IsFontAssetCorrupted(font))
                {
                    Debug.Log($"[TMPFontFixer] 使用备用字体: {path}");
                    return font;
                }
            }
            
            return null;
        }

        string GetGameObjectPath(GameObject obj)
        {
            string path = obj.name;
            Transform current = obj.transform.parent;
            
            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }
            
            return path;
        }

        void ReimportTMPDefaultResources()
        {
            Debug.Log("[TMPFontFixer] 重新导入TMP默认资源...");
            
            // 强制重新导入TMP设置
            var tmpSettingsPath = "Assets/TextMesh Pro/Resources";
            
            if (AssetDatabase.IsValidFolder(tmpSettingsPath))
            {
                AssetDatabase.ImportAsset(tmpSettingsPath, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
            }
            
            // 重新导入所有TMP字体资源
            var fontGuids = AssetDatabase.FindAssets("t:TMP_FontAsset");
            foreach (var guid in fontGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
            
            AssetDatabase.Refresh();
            
            Debug.Log("[TMPFontFixer] TMP默认资源重新导入完成");
            
            EditorUtility.DisplayDialog("TMP资源导入", "TMP默认资源重新导入完成", "确定");
        }

        void ClearTMPCache()
        {
            Debug.Log("[TMPFontFixer] 清理TMP缓存...");
            
            // 清理TMP设置缓存
            if (TMP_Settings.instance != null)
            {
                Resources.UnloadAsset(TMP_Settings.instance);
            }
            
            // 强制垃圾回收
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
            
            // 刷新资源数据库
            AssetDatabase.Refresh();
            
            Debug.Log("[TMPFontFixer] TMP缓存清理完成");
            
            EditorUtility.DisplayDialog("TMP缓存清理", "TMP缓存清理完成", "确定");
        }
    }
}
#endif
