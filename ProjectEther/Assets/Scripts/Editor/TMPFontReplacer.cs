#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace OsuVR.Editor
{
    public class TMPFontReplacer : EditorWindow
    {
        private const string SOURCE_HAN_SANS_SC_GUID = "1acc6c82055414f468ce2751513dab16";

        [MenuItem("Project Ether/工具/批量替换TMP字体")]
        public static void ShowWindow()
        {
            GetWindow<TMPFontReplacer>("TMP字体批量替换");
        }

        private TMP_FontAsset targetFont;
        private bool includeExamples = false;
        private bool includeSamples = false;
        private bool removeMissingScripts = true;
        private Vector2 scrollPosition;
        private List<string> changedFiles = new List<string>();
        private List<string> errorFiles = new List<string>();
        private int totalChanged = 0;
        private int totalMissingScriptsRemoved = 0;

        void OnEnable()
        {
            targetFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                AssetDatabase.GUIDToAssetPath(SOURCE_HAN_SANS_SC_GUID));
        }

        void OnGUI()
        {
            GUILayout.Label("TextMesh Pro 字体批量替换工具", EditorStyles.boldLabel);

            EditorGUILayout.Space();

            EditorGUILayout.HelpBox(
                "此工具将扫描项目中所有场景和Prefab，将TextMeshPro组件的字体统一替换为指定字体。\n\n" +
                "默认目标字体: SourceHanSansSC-Regular SDF",
                MessageType.Info);

            EditorGUILayout.Space();

            targetFont = (TMP_FontAsset)EditorGUILayout.ObjectField(
                "目标字体", targetFont, typeof(TMP_FontAsset), false);

            if (targetFont == null)
            {
                EditorGUILayout.HelpBox("请选择目标字体！", MessageType.Error);
                return;
            }

            EditorGUILayout.Space();

            includeExamples = EditorGUILayout.Toggle("包含TMP Examples", includeExamples);
            includeSamples = EditorGUILayout.Toggle("包含XR Samples", includeSamples);
            removeMissingScripts = EditorGUILayout.Toggle("自动移除缺失脚本", removeMissingScripts);

            EditorGUILayout.Space();

            if (GUILayout.Button("预览将要修改的文件", GUILayout.Height(30)))
            {
                PreviewChanges();
            }

            EditorGUILayout.Space();

            if (GUILayout.Button("执行批量替换", GUILayout.Height(40)))
            {
                if (EditorUtility.DisplayDialog("确认替换",
                    $"确定要将所有TMP组件的字体替换为 {targetFont.name} 吗？\n\n此操作将修改场景和Prefab文件。",
                    "确定", "取消"))
                {
                    ExecuteReplace();
                }
            }

            EditorGUILayout.Space();

            if (changedFiles.Count > 0 || errorFiles.Count > 0)
            {
                GUILayout.Label($"修改记录 (共 {totalChanged} 个组件，移除 {totalMissingScriptsRemoved} 个缺失脚本):",
                    EditorStyles.boldLabel);

                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(200));
                foreach (var file in changedFiles)
                {
                    GUILayout.Label(file, EditorStyles.miniLabel);
                }
                foreach (var file in errorFiles)
                {
                    GUILayout.Label(file, EditorStyles.miniLabel);
                }
                EditorGUILayout.EndScrollView();

                if (GUILayout.Button("清除记录"))
                {
                    changedFiles.Clear();
                    errorFiles.Clear();
                    totalChanged = 0;
                    totalMissingScriptsRemoved = 0;
                }
            }
        }

        void PreviewChanges()
        {
            changedFiles.Clear();
            errorFiles.Clear();
            totalChanged = 0;
            totalMissingScriptsRemoved = 0;

            int prefabCount = 0;
            int sceneCount = 0;
            int componentCount = 0;
            int missingScriptCount = 0;

            var prefabGuids = AssetDatabase.FindAssets("t:Prefab");
            foreach (var guid in prefabGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                if (!ShouldProcessPath(path)) continue;

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                int missing = CountMissingScripts(prefab);
                if (missing > 0)
                {
                    missingScriptCount += missing;
                    errorFiles.Add($"[缺失脚本] {path} ({missing} 个)");
                }

                var tmpUGUI = prefab.GetComponentsInChildren<TextMeshProUGUI>(true);
                var tmp3D = prefab.GetComponentsInChildren<TextMeshPro>(true);

                int count = tmpUGUI.Length + tmp3D.Length;
                if (count > 0)
                {
                    prefabCount++;
                    componentCount += count;
                    changedFiles.Add($"[Prefab] {path} ({count} 个TMP组件)");
                }
            }

            var sceneGuids = AssetDatabase.FindAssets("t:Scene");
            foreach (var guid in sceneGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                if (!ShouldProcessPath(path)) continue;

                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var tmpUGUI = Object.FindObjectsOfType<TextMeshProUGUI>(true);
                var tmp3D = Object.FindObjectsOfType<TextMeshPro>(true);

                int count = tmpUGUI.Length + tmp3D.Length;
                if (count > 0)
                {
                    sceneCount++;
                    componentCount += count;
                    changedFiles.Add($"[Scene] {path} ({count} 个TMP组件)");
                }
            }

            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects);

            totalChanged = componentCount;
            Debug.Log($"[TMPFontReplacer] 预览完成: {prefabCount} 个Prefab, {sceneCount} 个场景, 共 {componentCount} 个TMP组件, {missingScriptCount} 个缺失脚本");

            EditorUtility.DisplayDialog("预览完成",
                $"找到:\n" +
                $"- {prefabCount} 个Prefab\n" +
                $"- {sceneCount} 个场景\n" +
                $"- 共 {componentCount} 个TMP组件\n" +
                $"- {missingScriptCount} 个缺失脚本",
                "确定");
        }

        void ExecuteReplace()
        {
            changedFiles.Clear();
            errorFiles.Clear();
            totalChanged = 0;
            totalMissingScriptsRemoved = 0;

            string currentScenePath = EditorSceneManager.GetActiveScene().path;
            int prefabChanged = 0;
            int sceneChanged = 0;

            var prefabGuids = AssetDatabase.FindAssets("t:Prefab");
            foreach (var guid in prefabGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                if (!ShouldProcessPath(path)) continue;

                var result = ProcessPrefab(path);
                if (result.fontChanged > 0)
                {
                    prefabChanged++;
                    totalChanged += result.fontChanged;
                    changedFiles.Add($"[Prefab] {path} (替换 {result.fontChanged} 个)");
                }
                if (result.missingScriptsRemoved > 0)
                {
                    totalMissingScriptsRemoved += result.missingScriptsRemoved;
                    errorFiles.Add($"[已清理] {path} (移除 {result.missingScriptsRemoved} 个缺失脚本)");
                }
                if (result.error)
                {
                    errorFiles.Add($"[错误] {path}");
                }
            }

            var sceneGuids = AssetDatabase.FindAssets("t:Scene");
            foreach (var guid in sceneGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                if (!ShouldProcessPath(path)) continue;

                int changed = ProcessScene(path);
                if (changed > 0)
                {
                    sceneChanged++;
                    totalChanged += changed;
                    changedFiles.Add($"[Scene] {path} (替换 {changed} 个)");
                }
            }

            if (!string.IsNullOrEmpty(currentScenePath))
            {
                EditorSceneManager.OpenScene(currentScenePath);
            }
            else
            {
                EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[TMPFontReplacer] 替换完成: {prefabChanged} 个Prefab, {sceneChanged} 个场景, 共 {totalChanged} 个组件, 移除 {totalMissingScriptsRemoved} 个缺失脚本");

            EditorUtility.DisplayDialog("替换完成",
                $"字体替换完成！\n\n" +
                $"- 修改Prefab: {prefabChanged}\n" +
                $"- 修改场景: {sceneChanged}\n" +
                $"- 替换组件: {totalChanged}\n" +
                $"- 移除缺失脚本: {totalMissingScriptsRemoved}\n\n" +
                $"目标字体: {targetFont.name}",
                "确定");
        }

        (int fontChanged, int missingScriptsRemoved, bool error) ProcessPrefab(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) return (0, 0, true);

            int fontChanged = 0;
            int missingScriptsRemoved = 0;
            bool modified = false;

            if (removeMissingScripts)
            {
                missingScriptsRemoved = RemoveMissingScriptsFromPrefab(prefab, path);
                if (missingScriptsRemoved > 0)
                {
                    modified = true;
                }
            }

            var tmpUGUI = prefab.GetComponentsInChildren<TextMeshProUGUI>(true);
            var tmp3D = prefab.GetComponentsInChildren<TextMeshPro>(true);

            foreach (var tmp in tmpUGUI)
            {
                if (tmp.font != targetFont)
                {
                    tmp.font = targetFont;
                    fontChanged++;
                    modified = true;
                }
            }

            foreach (var tmp in tmp3D)
            {
                if (tmp.font != targetFont)
                {
                    tmp.font = targetFont;
                    fontChanged++;
                    modified = true;
                }
            }

            if (modified)
            {
                EditorUtility.SetDirty(prefab);
                try
                {
                    PrefabUtility.SavePrefabAsset(prefab);
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[TMPFontReplacer] 保存Prefab失败: {path}\n{e.Message}");
                    return (fontChanged, missingScriptsRemoved, true);
                }
            }

            return (fontChanged, missingScriptsRemoved, false);
        }

        int RemoveMissingScriptsFromPrefab(GameObject prefab, string path)
        {
            int removed = 0;
            var allTransforms = prefab.GetComponentsInChildren<Transform>(true);

            foreach (var t in allTransforms)
            {
                var go = t.gameObject;
                var components = go.GetComponents<Component>();

                int missingCount = 0;
                for (int i = 0; i < components.Length; i++)
                {
                    if (components[i] == null)
                    {
                        missingCount++;
                    }
                }

                if (missingCount > 0)
                {
                    var serializedObject = new SerializedObject(go);
                    var prop = serializedObject.FindProperty("m_Component");

                    for (int j = prop.arraySize - 1; j >= 0; j--)
                    {
                        var element = prop.GetArrayElementAtIndex(j);
                        if (element.objectReferenceValue == null)
                        {
                            prop.DeleteArrayElementAtIndex(j);
                        }
                    }

                    serializedObject.ApplyModifiedProperties();
                    removed += missingCount;
                    Debug.LogWarning($"[TMPFontReplacer] 从 {go.name} 移除 {missingCount} 个缺失脚本 (Prefab: {path})");
                }
            }

            return removed;
        }

        int CountMissingScripts(GameObject prefab)
        {
            int count = 0;
            var allTransforms = prefab.GetComponentsInChildren<Transform>(true);

            foreach (var t in allTransforms)
            {
                var components = t.gameObject.GetComponents<Component>();
                foreach (var c in components)
                {
                    if (c == null)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        int ProcessScene(string path)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            int changed = 0;

            var tmpUGUI = Object.FindObjectsOfType<TextMeshProUGUI>(true);
            var tmp3D = Object.FindObjectsOfType<TextMeshPro>(true);

            bool modified = false;

            foreach (var tmp in tmpUGUI)
            {
                if (tmp.font != targetFont)
                {
                    tmp.font = targetFont;
                    changed++;
                    modified = true;
                }
            }

            foreach (var tmp in tmp3D)
            {
                if (tmp.font != targetFont)
                {
                    tmp.font = targetFont;
                    changed++;
                    modified = true;
                }
            }

            if (modified)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            return changed;
        }

        bool ShouldProcessPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;

            if (!includeExamples && path.Contains("TextMesh Pro/Examples & Extras"))
                return false;

            if (!includeSamples && path.Contains("/Samples/"))
                return false;

            return true;
        }
    }
}
#endif
