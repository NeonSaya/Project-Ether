Shader "OsuVR/FlashlightMask"
{
    Properties
    {
        _Color ("Color", Color) = (0,0,0,1)
        _Radius ("Radius", Float) = 0.5
        _Feather ("Feather", Float) = 0.15
        _PlaneZ ("Plane Z", Float) = 2.0
    }
    SubShader
    {
        // 将渲染队列调低。通常 osu!VR 的 Note 是 3000(Transparent) ~ 4000 左右
        // UI 的 Canvas 通常是在 4000+ 或 Overlay
        // 把遮罩调到 3950，让它盖住 Note，但不盖住 Overlay UI
        Tags { "Queue"="Transparent+950" "RenderType"="Transparent" "IgnoreProjector"="True" }
        LOD 100

        ZWrite Off
        // ZTest Always
        // 改为 LEqual 或者保持 Always 都行，只要渲染队列比 UI 低，Canvas UI 就能在它之后渲染并盖过它
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float4 _Color;
            float _Radius;
            float _Feather;
            float _PlaneZ;

            // set by script
            float3 _LeftRayOrigin;
            float3 _LeftRayDir;
            float3 _RightRayOrigin;
            float3 _RightRayDir;

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            float3 IntersectZPlane(float3 origin, float3 dir, float z)
            {
                if (abs(dir.z) < 0.0001) return origin; 
                float t = (z - origin.z) / dir.z;
                return origin + dir * t;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                // SPI(单通道实例化)下必须使用逐眼相机位置，否则右眼会复用左眼/中心相机位置导致光圈偏移
                #if defined(USING_STEREO_MATRICES)
                    float3 camPos = unity_StereoWorldSpaceCameraPos[unity_StereoEyeIndex];
                #else
                    float3 camPos = _WorldSpaceCameraPos;
                #endif
                float3 viewDir = normalize(i.worldPos - camPos);

                float distLeft = 99999.0;
                if (_LeftRayDir.z > 0.001) {
                    float3 leftPoint = IntersectZPlane(_LeftRayOrigin, _LeftRayDir, _PlaneZ);
                    float3 viewPoint = IntersectZPlane(camPos, viewDir, _PlaneZ);
                    distLeft = distance(viewPoint.xy, leftPoint.xy);
                }

                float distRight = 99999.0;
                if (_RightRayDir.z > 0.001) {
                    float3 rightPoint = IntersectZPlane(_RightRayOrigin, _RightRayDir, _PlaneZ);
                    float3 viewPoint = IntersectZPlane(camPos, viewDir, _PlaneZ);
                    distRight = distance(viewPoint.xy, rightPoint.xy);
                }

                float minDist = min(distLeft, distRight);

                float alpha = smoothstep(_Radius - _Feather, _Radius + _Feather, minDist);

                // If viewDir doesn't point towards the plane
                if (viewDir.z <= 0) alpha = 1.0;

                fixed4 col = _Color;
                col.a *= alpha;
                return col;
            }
            ENDCG
        }
    }
}