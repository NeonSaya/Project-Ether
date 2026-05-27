using UnityEditor;
using UnityEngine;
using OsuVR;

/// <summary>
/// 编辑器工具：一键在当前场景添加 PostProcessManager
/// 菜单: Tools > PostProcessing > Setup in Scene
/// </summary>
public static class PostProcessSetup
{
    [MenuItem("Tools/PostProcessing/Setup in Scene")]
    public static void SetupInScene()
    {
        if (PostProcessManager.Instance != null)
        {
            Debug.Log("[PostProcessSetup] PostProcessManager 已存在于场景中");
            Selection.activeGameObject = PostProcessManager.Instance.gameObject;
            return;
        }

        var go = new GameObject("PostProcessManager");
        go.AddComponent<PostProcessManager>();
        go.AddComponent<PostProcessDefaults>();

        Undo.RegisterCreatedObjectUndo(go, "Create PostProcessManager");
        Selection.activeGameObject = go;

        Debug.Log("[PostProcessSetup] PostProcessManager 已添加到场景");
    }

    [MenuItem("Tools/PostProcessing/Create Profile Asset")]
    public static void CreateProfileAsset()
    {
        var profile = ScriptableObject.CreateInstance<UnityEngine.Rendering.PostProcessing.PostProcessProfile>();
        string path = "Assets/Settings/X-PostProcessProfile.asset";

        if (!AssetDatabase.IsValidFolder("Assets/Settings"))
        {
            AssetDatabase.CreateFolder("Assets", "Settings");
        }

        AssetDatabase.CreateAsset(profile, path);
        AssetDatabase.SaveAssets();
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = profile;
        Debug.Log($"[PostProcessSetup] Profile 已创建: {path}");
    }
}
