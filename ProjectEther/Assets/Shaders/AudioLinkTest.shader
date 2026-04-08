Shader "Custom/AudioLinkTest"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.2, 0.3, 0.5, 1)
        _EmissionIntensity ("Emission Intensity", Range(0, 5)) = 2
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float _EmissionIntensity;
        CBUFFER_END

        // 全局音频变量（来自AudioVisualizationManager）
        float _Global_Audio_Bass;
        float _Global_Audio_Mid;
        float _Global_Audio_Treble;

        // AudioLink纹理（可选）
        TEXTURE2D(_AudioTexture);
        SAMPLER(sampler_AudioTexture);

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
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                // 基础颜色
                float3 baseColor = _BaseColor.rgb;

                // 方式1：使用全局音频变量
                float3 audioColor = float3(
                    _Global_Audio_Bass,  // R: 低频（红色）
                    _Global_Audio_Mid,   // G: 中频（绿色）
                    _Global_Audio_Treble // B: 高频（蓝色）
                );

                // 方式2：采样AudioLink纹理（高级用法）
                // float4 audioLinkData = SAMPLE_TEXTURE2D(_AudioTexture, sampler_AudioTexture, float2(0.5, 0.5));

                // 发光效果
                float3 emission = audioColor * _EmissionIntensity;

                // 最终颜色
                float3 finalColor = baseColor + emission;

                return float4(finalColor, 1);
            }
            ENDHLSL
        }
    }
}