Shader "Osu/Slider"
{
    Properties
    {
        // 可以在材质面板调节整体透明度
        _MainAlpha ("Master Alpha", Range(0,1)) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100
        
        // 混合模式：标准透明混合
        Blend SrcAlpha OneMinusSrcAlpha
        // 关闭深度写入，防止半透明物体遮挡问题
        ZWrite Off
        // 关闭剔除，这样滑条正反面都能看到（VR里很重要）
        Cull Off 

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR; // 接收 C# 传进来的顶点颜色
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
            };

            float _MainAlpha;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color; // 直接传递颜色
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = i.color;
                col.a *= _MainAlpha; // 应用整体透明度
                return col;
            }
            ENDCG
        }
    }
}