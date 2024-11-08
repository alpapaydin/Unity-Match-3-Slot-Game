Shader "Custom/PastelSparkle"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _RainbowSpeed ("Rainbow Speed", Range(0, 10)) = 1
    }
    SubShader
    {
        Tags 
        { 
            "RenderType"="Transparent" 
            "Queue"="Transparent" 
            "IgnoreProjector"="True"
        }
        
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask RGB
        Cull Off
        Lighting Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _RainbowSpeed;

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.texcoord = v.texcoord;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 texColor = tex2D(_MainTex, i.texcoord);
                static float3 colors[4] = {
                    float3(1.0, 0.7, 0.8),
                    float3(0.7, 0.8, 1.0),
                    float3(0.8, 1.0, 0.8),
                    float3(1.0, 1.0, 0.7)
                };
                float t = frac(_Time.y * _RainbowSpeed);
                float indexF = t * 4;
                uint i0 = (uint)indexF;
                uint i1 = (i0 + 1u) % 4u;
                float blend = frac(indexF);
                blend = (1 - cos(blend * 3.14159));
                float3 finalColor = lerp(colors[i0], colors[i1], blend);
                return fixed4(finalColor, texColor.a * i.color.a);
            }
            ENDCG
        }
    }
}