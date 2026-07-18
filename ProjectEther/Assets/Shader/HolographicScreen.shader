Shader "OsuVR/HolographicScreen"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,0.8)
        _EdgeFade ("Edge Fade Width", Range(0.01, 0.4)) = 0.15
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent-100"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }
        Blend One OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
                UNITY_DEFINE_INSTANCED_PROP(float, _EdgeFade)
                UNITY_DEFINE_INSTANCED_PROP(float4, _MainTex_ST)
            UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

            Varyings vert(Attributes i)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_TRANSFER_INSTANCE_ID(i, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.uv = i.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float4 color = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Color);
                float edgeFade = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _EdgeFade);

                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);

                // 边缘羽化：UV 距离中心越远，alpha 越低
                float2 centered = i.uv - 0.5;   // [-0.5, 0.5]
                float2 d = abs(centered) * 2.0;  // [0, 1] 从中心到边缘
                float fadeX = smoothstep(1.0, 1.0 - edgeFade, d.x);
                float fadeY = smoothstep(1.0, 1.0 - edgeFade, d.y);
                float edgeAlpha = fadeX * fadeY;

                // 预乘输出: 亮度 = α², 透明度 = α² (Blend One OneMinusSrcAlpha)
                float alpha = color.a;
                float finalAlpha = alpha * alpha * edgeAlpha;
                return half4(tex.rgb * alpha * alpha * edgeAlpha, finalAlpha);
            }
            ENDHLSL
        }
    }
}
