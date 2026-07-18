Shader "OsuVR/SBOverlay"
{
    // Overlay shader: 将 SB RenderTexture (预乘 alpha) 合成到屏幕
    // RT 中 rgb 已是预乘色, a 为覆盖率 (由 SBInstanced 预乘管线产出)
    // 输出: rgb = sb.rgb × scale, a = sb.a × scale (亮度跟随透明度)
    // Blend One OneMinusSrcAlpha: dst = src.rgb + dst.rgb × (1 - src.a)

    Properties
    {
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

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

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
                half4 sb = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half4 fade = SAMPLE_TEXTURE2D(_EdgeFadeTex, sampler_EdgeFadeTex, input.uv);

                // 预乘管线: scale 同时作用于 rgb 和 a (亮度跟随透明度)
                float scale = fade.a * _ScreenAlpha;
                half a = sb.a * scale;

                // 覆盖率接近 1 时 clamp, 消除浮点精度导致的背景穿透
                a = a > 0.99 ? 1.0 : a;

                return half4(sb.rgb * scale, a);
            }
            ENDHLSL
        }
    }
}
