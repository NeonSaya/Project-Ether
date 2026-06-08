Shader "OsuVR/SBVideoOverlay"
{
    // 视频 Overlay shader: 视频纹理 × 边缘羽化
    // 用于独立视频层，与 SB Overlay 层完全解耦

    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "VideoOverlay"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            TEXTURE2D(_EdgeFadeTex);
            SAMPLER(sampler_EdgeFadeTex);

            half4 _Color;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.uv = input.uv;
                return o;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 video = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half4 fade = SAMPLE_TEXTURE2D(_EdgeFadeTex, sampler_EdgeFadeTex, input.uv);

                // 视频颜色 × _Color(默认白色), Alpha = 边缘羽化 × _Color.a
                return half4(video.rgb, fade.a) * _Color;
            }
            ENDHLSL
        }
    }
}
