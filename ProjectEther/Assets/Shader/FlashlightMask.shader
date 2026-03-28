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
        Tags { "Queue"="Overlay+100" "RenderType"="Transparent" "IgnoreProjector"="True" }
        LOD 100

        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
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
                float3 camPos = _WorldSpaceCameraPos;
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