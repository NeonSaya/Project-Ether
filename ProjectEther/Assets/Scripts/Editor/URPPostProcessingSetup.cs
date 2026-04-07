using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEditor;

namespace OsuVR.Editor
{
    /// <summary>
    /// URP后期处理配置工具：自动配置适合VR音游的Post-Processing效果
    /// </summary>
    public class URPPostProcessingSetup
    {
        [MenuItem("Tools/Project Ether/配置URP后期处理效果")]
        public static void ConfigurePostProcessing()
        {
            string profilePath = "Assets/Settings/ProjectEther-PostProcessing.asset";
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);

            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, profilePath);
                Debug.Log($"[PostProcessing] 创建新的Post-Processing Profile: {profilePath}");
            }

            ConfigureBloom(profile);
            ConfigureColorAdjustments(profile);
            ConfigureVignette(profile);
            ConfigureTonemapping(profile);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[PostProcessing] ✅ Post-Processing配置完成！路径: {profilePath}");
            Debug.Log($"[PostProcessing] 请将此Profile拖入URP Renderer Data的Post Process Data槽位");
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = profile;
        }

        private static void ConfigureBloom(VolumeProfile profile)
        {
            Bloom bloom;
            if (!profile.TryGet(out bloom))
            {
                bloom = profile.Add<Bloom>();
            }

            bloom.active = true;
            bloom.threshold.Override(1.2f);
            bloom.intensity.Override(1.5f);
            bloom.scatter.Override(0.7f);
            bloom.tint.Override(new Color(0.9f, 0.95f, 1f));
            bloom.highQualityFiltering.Override(true);
            bloom.dirtTexture.Override(null);
            bloom.dirtIntensity.Override(0f);

            Debug.Log($"[PostProcessing] ✅ Bloom配置完成 - Threshold: 1.2, Intensity: 1.5, Scatter: 0.7");
        }

        private static void ConfigureColorAdjustments(VolumeProfile profile)
        {
            ColorAdjustments colorAdjustments;
            if (!profile.TryGet(out colorAdjustments))
            {
                colorAdjustments = profile.Add<ColorAdjustments>();
            }

            colorAdjustments.active = true;
            colorAdjustments.postExposure.Override(0.2f);
            colorAdjustments.contrast.Override(15f);
            colorAdjustments.colorFilter.Override(Color.white);
            colorAdjustments.hueShift.Override(0f);
            colorAdjustments.saturation.Override(10f);

            Debug.Log($"[PostProcessing] ✅ Color Adjustments配置完成 - Contrast: 15, Saturation: 10");
        }

        private static void ConfigureVignette(VolumeProfile profile)
        {
            Vignette vignette;
            if (!profile.TryGet(out vignette))
            {
                vignette = profile.Add<Vignette>();
            }

            vignette.active = true;
            vignette.color.Override(new Color(0f, 0f, 0.1f, 1f));
            vignette.center.Override(new Vector2(0.5f, 0.5f));
            vignette.intensity.Override(0.25f);
            vignette.smoothness.Override(0.4f);
            vignette.rounded.Override(true);

            Debug.Log($"[PostProcessing] ✅ Vignette配置完成 - Intensity: 0.25, Smoothness: 0.4");
        }

        private static void ConfigureTonemapping(VolumeProfile profile)
        {
            Tonemapping tonemapping;
            if (!profile.TryGet(out tonemapping))
            {
                tonemapping = profile.Add<Tonemapping>();
            }

            tonemapping.active = true;
            tonemapping.mode.Override(TonemappingMode.ACES);

            Debug.Log($"[PostProcessing] ✅ Tonemapping配置完成 - Mode: ACES");
        }

        [MenuItem("Tools/Project Ether/创建音频响应测试Shader")]
        public static void CreateAudioReactiveTestShader()
        {
            string shaderPath = "Assets/Shaders/AudioReactiveTest.shader";
            string shaderCode = @"Shader ""Custom/AudioReactiveTest""
{
    Properties
    {
        _BaseColor (""Base Color"", Color) = (1, 1, 1, 1)
        _EmissionIntensity (""Emission Intensity"", Range(0, 10)) = 1
    }

    SubShader
    {
        Tags { ""RenderType""=""Opaque"" ""RenderPipeline""=""UniversalPipeline"" }
        LOD 100

        HLSLINCLUDE
        #include ""Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl""

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float _EmissionIntensity;
        CBUFFER_END

        // 全局音频变量
        float _Global_Audio_Bass;
        float _Global_Audio_Mid;
        float _Global_Audio_Treble;

        ENDHLSL

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                // 基础颜色
                float3 baseColor = _BaseColor.rgb;

                // 音频响应发光
                float3 emission = float3(_Global_Audio_Bass, _Global_Audio_Mid, _Global_Audio_Treble) * _EmissionIntensity;

                // 最终颜色
                float3 finalColor = baseColor + emission;

                return float4(finalColor, 1);
            }
            ENDHLSL
        }
    }
}";

            System.IO.Directory.CreateDirectory("Assets/Shaders");
            System.IO.File.WriteAllText(shaderPath, shaderCode);
            AssetDatabase.Refresh();

            Debug.Log($"[Shader] ✅ 音频响应测试Shader已创建: {shaderPath}");
            Debug.Log($"[Shader] 该Shader演示了如何读取全局音频变量 _Global_Audio_Bass/Mid/Treble");

            Selection.activeObject = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
        }
    }
}
