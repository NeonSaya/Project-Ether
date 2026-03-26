using UnityEditor;
using UnityEngine;
using TMPro;

public static class FontAtlasFixer
{
    [MenuItem("Tools/Fix TMP Font Atlas Textures")]
    public static void FixFonts()
    {
        string[] guids = AssetDatabase.FindAssets("t:TMP_FontAsset");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            
            if (fontAsset != null)
            {
                // Check if atlas is missing
                bool needsFix = false;
                try
                {
                    if (fontAsset.atlasTexture == null || fontAsset.atlasTextures == null || fontAsset.atlasTextures.Length == 0)
                    {
                        needsFix = true;
                    }
                }
                catch
                {
                    needsFix = true;
                }
                
                if (needsFix)
                {
                    Debug.Log($"Fixing font asset: {path}");
                    
                    // Clear the font data to recreate internal arrays and textures if it's a dynamic font
                    fontAsset.ClearFontAssetData(true);
                    
                    EditorUtility.SetDirty(fontAsset);
                }
            }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Font Atlas fix complete!");
    }
}