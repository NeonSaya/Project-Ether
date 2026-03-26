using UnityEditor;
using UnityEngine;
using TMPro;

[InitializeOnLoad]
public class ForceReimport
{
    static ForceReimport()
    {
        EditorApplication.delayCall += Run;
    }

    static void Run()
    {
        if (SessionState.GetBool("ForceReimportRun4", false))
            return;
        
        SessionState.SetBool("ForceReimportRun4", true);
        
        Debug.Log("[ForceReimport] Reimporting TMP assets...");
        var tmpSettingsPath = "Assets/TextMesh Pro/Resources";
        if (AssetDatabase.IsValidFolder(tmpSettingsPath))
        {
            AssetDatabase.ImportAsset(tmpSettingsPath, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
        }
        
        var fontGuids = AssetDatabase.FindAssets("t:TMP_FontAsset");
        foreach (var guid in fontGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }
        
        FontAtlasFixer.FixFonts();
        
        Debug.Log("[ForceReimport] Done!");
    }
}