Shader "OsuVR/SBOverlay"
{
    // Overlay shader: 将 SB RenderTexture (预乘 alpha) 合成到屏幕
    // RT is the complete encoded-RGB frame; alpha is 1 while loaded, 0 when cleared.
    // 输出: rgb = sb.rgb × scale, a = sb.a × scale (亮度跟随透明度)
    // Blend One OneMinusSrcAlpha: dst = src.rgb + dst.rgb × (1 - src.a)

    Properties
    {
        _MainTex ("Texture", 2D) = "black" {}
        _EdgeFadeTex ("Edge Fade", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _ScreenAlpha ("Screen Alpha", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+1"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "SBOverlay_Multiply"
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            TEXTURE2D(_EdgeFadeTex);
            SAMPLER(sampler_EdgeFadeTex);

            half4 _Color;
            half _ScreenAlpha;

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
                half4 sb = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                #ifndef UNITY_COLORSPACE_GAMMA
                sb.rgb = SRGBToLinear(sb.rgb);
                #endif
                half4 fade = SAMPLE_TEXTURE2D(_EdgeFadeTex, sampler_EdgeFadeTex, input.uv);

                // Match the existing video screen opacity curve, applied once to the
                // completed 2D frame. All loaded-frame pixels share the same opacity;
                // internal sprite fades and additive blending are already resolved.
                float opacity = _ScreenAlpha * _ScreenAlpha * fade.a * sb.a;
                return half4(sb.rgb * opacity, opacity);
            }
            ENDHLSL
        }
    }
}
