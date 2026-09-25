Shader "OsuVR/SBInstanced"
{
    Properties { _MainTex ("Texture", 2D) = "white" {} }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        HLSLINCLUDE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            struct SpriteInstanceData
            {
                float4x4 objectToWorld;
                float4 color;
                float4 params0;
            };
            StructuredBuffer<SpriteInstanceData> _InstanceData;
            int _InstanceOffset;
            float4x4 _StoryboardVP;
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                uint instanceID : SV_InstanceID;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                nointerpolation float additive : TEXCOORD1;
            };
            Varyings vert(Attributes input)
            {
                SpriteInstanceData sprite = _InstanceData[input.instanceID + _InstanceOffset];
                Varyings o;
                o.positionCS = mul(_StoryboardVP, mul(sprite.objectToWorld, input.positionOS));
                o.uv = input.uv;
                o.color = sprite.color;
                o.additive = sprite.params0.y;
                return o;
            }
            float4 frag(Varyings input) : SV_Target
            {
                // 框架的 UNORM 采样: 编码后的 RGB, LOD 偏置 -0.9, 最大 LOD 3。
                float2 dx = ddx(input.uv) * _MainTex_TexelSize.zw;
                float2 dy = ddy(input.uv) * _MainTex_TexelSize.zw;
                float lod = clamp(0.5 * log2(max(max(dot(dx, dx), dot(dy, dy)), 1e-8)) - 0.9, 0.0, 3.0);
                float4 c = SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, input.uv, lod) * input.color;
                return c;
            }
            float _UnderlayEncoded;
            Varyings vertBackground(Attributes input)
            {
                Varyings o;
                o.positionCS = float4(input.positionOS.xy * 2.0, 0, 1);
                o.uv = input.uv;
                o.color = 1;
                o.additive = 0;
                return o;
            }
            float4 fragBackground(Varyings input) : SV_Target
            {
                float3 colour = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).rgb;
                #ifndef UNITY_COLORSPACE_GAMMA
                if (_UnderlayEncoded < 0.5) colour = LinearToSRGB(colour);
                #endif
                return float4(colour, 1);
            }
        ENDHLSL
        // 显式 pass 索引由渲染器按声明顺序提交。
        // RGB 使用与 lazer 相同的固定功能混合因子; alpha 存储覆盖率, 供 VR 使用。
        Pass
        {
            Name "SB_AlphaBlend"
            Cull Off
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #pragma multi_compile_instancing
            ENDHLSL
        }
        Pass
        {
            Name "SB_Additive"
            Cull Off
            ZWrite Off
            ZTest Always
            Blend SrcAlpha One, Zero One
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #pragma multi_compile_instancing
            ENDHLSL
        }
        Pass
        {
            Name "SB_Background"
            Cull Off
            ZWrite Off
            ZTest Always
            Blend One Zero
            HLSLPROGRAM
            #pragma vertex vertBackground
            #pragma fragment fragBackground
            #pragma target 4.5
            ENDHLSL
        }
    }
}
