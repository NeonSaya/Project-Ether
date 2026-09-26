Shader "OsuVR/SBVideoOverlay"
{
    // 视频 Overlay shader: 视频纹理 × 边缘羽化
    // 用于独立视频层，与 SB Overlay 层完全解耦

    Properties
    {
        _MainTex ("Texture", 2D) = "black" {}
        _EdgeFadeTex ("Edge Fade", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent-99"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "VideoOverlay"
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

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
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.uv = input.uv;
                return o;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 video = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half4 fade = SAMPLE_TEXTURE2D(_EdgeFadeTex, sampler_EdgeFadeTex, input.uv);

                // 预乘输出: 亮度 = α², 透明度 = α² (Blend One OneMinusSrcAlpha)
                float alpha = _Color.a;
                float finalAlpha = fade.a * alpha * alpha;
                return half4(video.rgb * alpha * alpha * fade.a, finalAlpha);
            }
            ENDHLSL
        }
    }
}
