Shader "OsuVR/SBInstanced"
{
    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        // =============================================
        //  Pass 0: Opaque (覆盖背景, 不透明 sprite)
        //  Blend One Zero: 直接写入, 不与目标混合
        //  Alpha test: 纹理透明像素被丢弃
        // =============================================
        Pass
        {
            Name "SB_Opaque"

            Cull Off
            ZWrite Off
            ZTest Always
            Blend One Zero

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct SpriteInstanceData
            {
                float4x4 objectToWorld;
                float4   color;
                float4   params0;
            };

            StructuredBuffer<SpriteInstanceData> _InstanceData;

            TEXTURE2D_ARRAY(_MainTexArray);
            SAMPLER(sampler_MainTexArray);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                uint   instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float4 color       : COLOR;
                nointerpolation int textureIndex : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                SpriteInstanceData inst = _InstanceData[input.instanceID];

                float2 uv = input.uv;
                if (inst.params0.z > 0.5) uv.x = 1.0 - uv.x;
                if (inst.params0.w > 0.5) uv.y = 1.0 - uv.y;

                Varyings o;
                float3 worldPos = mul(inst.objectToWorld, input.positionOS).xyz;
                o.positionCS    = TransformWorldToHClip(worldPos);
                o.uv            = uv;
                o.color         = inst.color;
                o.textureIndex  = (int)inst.params0.x;
                return o;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D_ARRAY(
                    _MainTexArray, sampler_MainTexArray,
                    input.uv, input.textureIndex);

                // Alpha test: 丢弃纹理中的透明像素 (如 PNG 背景)
                clip(tex.a - 0.1);

                // 直接输出, Blend One Zero 覆盖目标
                return tex * input.color;
            }
            ENDHLSL
        }

        // =============================================
        //  Pass 1: Alpha Blend (透明 sprite, 标准混合)
        // =============================================
        Pass
        {
            Name "SB_AlphaBlend"

            Cull Off
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct SpriteInstanceData
            {
                float4x4 objectToWorld;
                float4   color;
                float4   params0;
            };

            StructuredBuffer<SpriteInstanceData> _InstanceData;

            TEXTURE2D_ARRAY(_MainTexArray);
            SAMPLER(sampler_MainTexArray);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                uint   instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float4 color       : COLOR;
                nointerpolation int textureIndex : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                SpriteInstanceData inst = _InstanceData[input.instanceID];

                float2 uv = input.uv;
                if (inst.params0.z > 0.5) uv.x = 1.0 - uv.x;
                if (inst.params0.w > 0.5) uv.y = 1.0 - uv.y;

                Varyings o;
                float3 worldPos = mul(inst.objectToWorld, input.positionOS).xyz;
                o.positionCS    = TransformWorldToHClip(worldPos);
                o.uv            = uv;
                o.color         = inst.color;
                o.textureIndex  = (int)inst.params0.x;
                return o;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D_ARRAY(
                    _MainTexArray, sampler_MainTexArray,
                    input.uv, input.textureIndex);
                return tex * input.color;
            }
            ENDHLSL
        }

        // =============================================
        //  Pass 1: Additive
        // =============================================
        Pass
        {
            Name "SB_Additive"

            Cull Off
            ZWrite Off
            ZTest Always
            Blend One One

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct SpriteInstanceData
            {
                float4x4 objectToWorld;
                float4   color;
                float4   params0;
            };

            StructuredBuffer<SpriteInstanceData> _InstanceData;

            TEXTURE2D_ARRAY(_MainTexArray);
            SAMPLER(sampler_MainTexArray);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                uint   instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float4 color       : COLOR;
                nointerpolation int textureIndex : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                SpriteInstanceData inst = _InstanceData[input.instanceID];

                float2 uv = input.uv;
                if (inst.params0.z > 0.5) uv.x = 1.0 - uv.x;
                if (inst.params0.w > 0.5) uv.y = 1.0 - uv.y;

                Varyings o;
                float3 worldPos = mul(inst.objectToWorld, input.positionOS).xyz;
                o.positionCS    = TransformWorldToHClip(worldPos);
                o.uv            = uv;
                o.color         = inst.color;
                o.textureIndex  = (int)inst.params0.x;
                return o;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D_ARRAY(
                    _MainTexArray, sampler_MainTexArray,
                    input.uv, input.textureIndex);
                half4 c = tex * input.color;
                return half4(c.rgb * c.a, 1);
            }
            ENDHLSL
        }
    }
}
