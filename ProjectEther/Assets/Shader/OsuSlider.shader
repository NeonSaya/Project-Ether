Shader "Osu/SliderVR_Flat_Stencil_VR_Fixed"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _StencilID ("Stencil ID", Int) = 10
    }
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Transparent-2" 
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off 
        Cull Off

        Stencil {
            Ref [_StencilID]
            Comp NotEqual
            Pass Replace
        }

        Pass {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // ✅ 必须：启用实例化编译
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attr { 
                float4 posOS : POSITION;
                // ✅ 必须：实例化 ID
                UNITY_VERTEX_INPUT_INSTANCE_ID 
            };

            struct Vary { 
                float4 posCS : SV_POSITION;
                // ✅ 必须：立体输出宏
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO 
            };

            // ✅ 修复：将变量放入实例化缓冲区，以支持 multi_compile_instancing
            UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
            UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

            Vary vert(Attr i) {
                Vary o;
                // ✅ 必须：设置实例化和立体数据
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_TRANSFER_INSTANCE_ID(i, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.posCS = TransformObjectToHClip(i.posOS.xyz);
                return o;
            }

            half4 frag(Vary i) : SV_Target {
                // ✅ 必须：在片元着色器初始化立体数据
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(i);
                
                // ✅ 修复：使用宏获取当前实例的颜色
                return UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Color);
            }
            ENDHLSL
        }
    }
}