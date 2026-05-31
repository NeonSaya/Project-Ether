using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

/// <summary>
/// 防止 Unity 构建时剔除通过 Shader.Find() 动态加载的 Shader。
/// 所有在代码中通过 Shader.Find() 使用的 Shader 必须在此注册。
/// </summary>
public class ShaderStripGuard : IPreprocessShaders
{
    // 项目中所有通过 Shader.Find() 动态引用的自定义 Shader
    static readonly string[] CriticalShaderNames =
    {
        // === 自定义 Shader (最关键，剔除即黑屏) ===
        "OsuVR/SBInstanced",
        "OsuVR/FlashlightMask",
        "OsuVR/HolographicScreen",
        "OsuVR/SBOverlay",
        "Osu/ApproachCircle_SmartDepth",
        "Osu/SliderVR_Flat_Stencil_VR_Fixed",

        // === 运行时动态加载的 Unity 内置 Shader ===
        "Mobile/Particles/Additive",
        "Legacy Shaders/Particles/Additive",
        "Mobile/Particles/Alpha Blended",
        "Particles/Standard Unlit",
        "Universal Render Pipeline/Particles/Unlit",
        "Universal Render Pipeline/Particles/Simple Lit",
        "Universal Render Pipeline/Unlit",
        "Universal Render Pipeline/Lit",
        "Unlit/Texture",
        "Unlit/Transparent",
        "UI/Default",
        "Sprites/Default",
    };

    public int callbackOrder => 0;

    public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
    {
        // 不做任何剔除，仅保留此接口的存在以确保脚本被加载
        // 实际的防剔除通过 AlwaysIncludedShaders 列表实现
    }

    /// <summary>
    /// 菜单工具：一键将所有关键 Shader 加入 Always Included Shaders
    /// </summary>
    [MenuItem("Tools/Project Ether/Force Include Critical Shaders")]
    static void ForceIncludeShaders()
    {
        var graphicsSettings = AssetDatabase.LoadAssetAtPath<GraphicsSettings>("ProjectSettings/GraphicsSettings.asset");
        var serializedObject = new SerializedObject(graphicsSettings);
        var alwaysIncludedShaders = serializedObject.FindProperty("m_AlwaysIncludedShaders");

        int addedCount = 0;

        foreach (string shaderName in CriticalShaderNames)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogWarning($"[ShaderStripGuard] Shader 未找到: {shaderName}");
                continue;
            }

            // 检查是否已在列表中
            bool alreadyIncluded = false;
            for (int i = 0; i < alwaysIncludedShaders.arraySize; i++)
            {
                var element = alwaysIncludedShaders.GetArrayElementAtIndex(i);
                if (element.objectReferenceValue == shader)
                {
                    alreadyIncluded = true;
                    break;
                }
            }

            if (!alreadyIncluded)
            {
                alwaysIncludedShaders.InsertArrayElementAtIndex(alwaysIncludedShaders.arraySize);
                var newElement = alwaysIncludedShaders.GetArrayElementAtIndex(alwaysIncludedShaders.arraySize - 1);
                newElement.objectReferenceValue = shader;
                addedCount++;
                Debug.Log($"[ShaderStripGuard] 已添加: {shaderName}");
            }
        }

        serializedObject.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
        Debug.Log($"[ShaderStripGuard] 完成！新增 {addedCount} 个 Shader 到 Always Included 列表");
    }
}
