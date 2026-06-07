Shader "OsuVR/SBOverlay"
{
    // Overlay shader: 将 SB RenderTexture 与边缘羽化纹理相乘
    // Alpha = SB.spriteAlpha × EdgeFade.alpha × _ScreenAlpha
    // _ScreenAlpha: 屏幕整体透明度 (来自设置面板), 不污染 sprite 自身的 Fade 逻辑

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

                // 合成 alpha: sprite 自身 × 边缘羽化 × 屏幕透明度
                half a = sb.a * fade.a * _ScreenAlpha;

                // 当 alpha 接近 1.0 时 clamp, 消除浮点精度导致的背景穿透
                // sprite 完全覆盖背景时, 不会透出下面的内容
                a = a > 0.99 ? 1.0 : a;

                return half4(sb.rgb * _ScreenAlpha, a);
            }
            ENDHLSL
        }
    }
}
