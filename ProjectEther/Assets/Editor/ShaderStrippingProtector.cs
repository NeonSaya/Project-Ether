using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// 资源偷渡法 (Dummy Resources Anti-Stripping):
/// 在 Assets/Resources/AntiStrippingDummies/ 下自动生成 Dummy 材质球。
/// Unity 构建时会扫描 Resources 目录，发现这些材质后精准提取且仅编译
/// 材质当前激活的关键词变体，既防剔除又避免百万级变体爆炸。
/// </summary>
public static class ShaderStrippingProtector
{
    const string OutputDir = "Assets/Resources/AntiStrippingDummies";

    /// <summary>
    /// 每个条目: (Shader名称, 启用的Keyword列表, 材质属性设置)
    /// </summary>
    static readonly (string shader, string[] keywords, string name)[] DummyDefs =
    {
        // === URP 核心 (最容易被剔除) ===
        ("Universal Render Pipeline/Lit",          new[]{"_EMISSION","_METALLICSPECGLOSSMAP"}, "Dummy_URP_Lit"),
        ("Universal Render Pipeline/Unlit",        new[]{"_SURFACE_TYPE_TRANSPARENT"},          "Dummy_URP_Unlit"),
        ("Universal Render Pipeline/Particles/Unlit", new string[0],                            "Dummy_URP_ParticlesUnlit"),

        // === 移动端粒子 ===
        ("Mobile/Particles/Additive",              new string[0],                               "Dummy_MobileParticlesAdditive"),
        ("Legacy Shaders/Particles/Additive",      new string[0],                               "Dummy_LegacyParticlesAdditive"),
        ("Mobile/Particles/Alpha Blended",         new string[0],                               "Dummy_MobileParticlesAlphaBlended"),

        // === 标准粒子 ===
        ("Particles/Standard Unlit",               new string[0],                               "Dummy_ParticlesStandardUnlit"),
        ("Universal Render Pipeline/Particles/Simple Lit", new string[0],                       "Dummy_URP_ParticlesSimpleLit"),

        // === 内置 Unlit ===
        ("Unlit/Texture",                          new string[0],                               "Dummy_UnlitTexture"),
        ("Unlit/Transparent",                      new string[0],                               "Dummy_UnlitTransparent"),

        // === UI ===
        ("UI/Default",                             new string[0],                               "Dummy_UI_Default"),

        // === Sprite ===
        ("Sprites/Default",                        new string[0],                               "Dummy_SpritesDefault"),

        // === Fallback ===
        ("Standard",                               new[]{"_EMISSION"},                          "Dummy_Standard"),
    };

    [MenuItem("Tools/Project Ether/Generate Anti-Stripping Materials")]
    static void GenerateDummyMaterials()
    {
        // 确保输出目录存在
        if (!AssetDatabase.IsValidFolder(OutputDir))
        {
            string parent = "Assets/Resources";
            if (!AssetDatabase.IsValidFolder(parent))
                AssetDatabase.CreateFolder("Assets", "Resources");
            AssetDatabase.CreateFolder(parent, "AntiStrippingDummies");
        }

        int created = 0, skipped = 0, failed = 0;

        foreach (var (shaderName, keywords, matName) in DummyDefs)
        {
            string assetPath = $"{OutputDir}/{matName}.mat";

            // 跳过已存在且 Shader 未变的材质
            var existing = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (existing != null && existing.shader != null && existing.shader.name == shaderName)
            {
                // 仍然更新 keywords
                foreach (var kw in keywords)
                    existing.EnableKeyword(kw);
                EditorUtility.SetDirty(existing);
                skipped++;
                continue;
            }

            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogWarning($"[AntiStrip] Shader 未找到: {shaderName}，跳过");
                failed++;
                continue;
            }

            // 创建材质
            var mat = new Material(shader);
            mat.name = matName;

            // 设置合理的默认属性 (避免粉色)
            SetDefaultProperties(mat, shaderName);

            // 启用代码中使用的 keywords
            foreach (var kw in keywords)
                mat.EnableKeyword(kw);

            // 保存为资产
            AssetDatabase.CreateAsset(mat, assetPath);
            created++;

            Debug.Log($"[AntiStrip] 已创建: {matName} (Shader: {shaderName}, Keywords: {keywords.Length})");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[AntiStrip] 完成! 创建={created}, 跳过={skipped}, 失败={failed}");
        Debug.Log($"[AntiStrip] 材质位于: {OutputDir}/");
        Debug.Log("[AntiStrip] Unity 构建时会自动扫描 Resources 目录，精准编译这些材质引用的变体。");
    }

    static void SetDefaultProperties(Material mat, string shaderName)
    {
        // URP Lit: 设为白色基础色
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", Texture2D.whiteTexture);
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", Texture2D.whiteTexture);

        // 透明模式 (用于 URP Unlit)
        if (mat.HasProperty("_Surface"))
        {
            mat.SetFloat("_Surface", 1); // Transparent
            mat.SetFloat("_Blend", 0);   // Alpha
        }
        if (mat.HasProperty("_SrcBlend"))
        {
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
        }

        // Particle Additive: 设为白色
        if (mat.HasProperty("_TintColor")) mat.SetColor("_TintColor", Color.white);

        // Emission: 设为黑色 (不发光，但编译 _EMISSION 变体)
        if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", Color.black);
    }

    /// <summary>
    /// 清理所有 Dummy 材质 (用于重置)
    /// </summary>
    [MenuItem("Tools/Project Ether/Clear Anti-Stripping Materials")]
    static void ClearDummyMaterials()
    {
        if (!AssetDatabase.IsValidFolder(OutputDir))
        {
            Debug.Log("[AntiStrip] 目录不存在，无需清理");
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:Material", new[] { OutputDir });
        int deleted = 0;
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            AssetDatabase.DeleteAsset(path);
            deleted++;
        }

        AssetDatabase.Refresh();
        Debug.Log($"[AntiStrip] 已清理 {deleted} 个 Dummy 材质");
    }
}
