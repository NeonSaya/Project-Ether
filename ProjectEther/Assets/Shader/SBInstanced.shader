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

                // 预乘输出, Blend One Zero 直接写入覆盖目标
                half4 c = tex * input.color;
                return half4(c.rgb * c.a, c.a);
            }
            ENDHLSL
        }

        // =============================================
        //  Pass 1: Alpha Blend (透明 sprite, 预乘混合)
        //  输出预乘 alpha, Blend One OneMinusSrcAlpha
        //  剔除 additive sprite (由 Additive pass 绘制)
        // =============================================
        Pass
        {
            Name "SB_AlphaBlend"

            Cull Off
            ZWrite Off
            ZTest Always
            Blend One OneMinusSrcAlpha

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

                Varyings o;

                // blendMode 剔除: additive sprite (params0.y > 0.5) 不在本 pass 绘制
                // 输出退化三角形 (全顶点同位置, 零面积被光栅化剔除)
                if (inst.params0.y > 0.5)
                {
                    o.positionCS = float4(0, 0, 0, 1);
                    o.uv = input.uv;
                    o.color = 0;
                    o.textureIndex = 0;
                    return o;
                }

                float2 uv = input.uv;
                if (inst.params0.z > 0.5) uv.x = 1.0 - uv.x;
                if (inst.params0.w > 0.5) uv.y = 1.0 - uv.y;

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
                // 预乘输出: rgb 已乘 a, 配合 Blend One OneMinusSrcAlpha 实现正确的 over 合成
                // dst.rgb = src.rgb + dst.rgb × (1 - src.a)
                // dst.a   = src.a   + dst.a   × (1 - src.a)
                return half4(c.rgb * c.a, c.a);
            }
            ENDHLSL
        }

        // =============================================
        //  Pass 2: Additive (加法 sprite)
        //  只贡献预乘 rgb, alpha 输出 0 (不改变 RT 覆盖率)
        //  剔除普通 sprite (由 AlphaBlend pass 绘制)
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

                Varyings o;

                // blendMode 剔除: 普通 sprite (params0.y < 0.5) 不在本 pass 绘制
                if (inst.params0.y < 0.5)
                {
                    o.positionCS = float4(0, 0, 0, 1);
                    o.uv = input.uv;
                    o.color = 0;
                    o.textureIndex = 0;
                    return o;
                }

                float2 uv = input.uv;
                if (inst.params0.z > 0.5) uv.x = 1.0 - uv.x;
                if (inst.params0.w > 0.5) uv.y = 1.0 - uv.y;

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
                // 加法: 只加预乘 rgb, alpha 输出 0 → RT 覆盖率不被 additive 顶满
                // dst.rgb += c.rgb × c.a, dst.a += 0 → 下层内容在 additive 区域保持可见
                return half4(c.rgb * c.a, 0);
            }
            ENDHLSL
        }
    }
}
