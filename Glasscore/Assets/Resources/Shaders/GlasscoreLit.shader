// Simple lit opaque surface with rim light and emission (avatars, weapons, stones).
Shader "Glasscore/Lit"
{
    Properties
    {
        _Color ("Albedo", Color) = (0.8, 0.8, 0.8, 1)
        _Emission ("Emission", Color) = (0, 0, 0, 1)
        _Rim ("Rim", Range(0,2)) = 0.6
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Emission)
            UNITY_INSTANCING_BUFFER_END(Props)
            float _Rim;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float3 n : TEXCOORD0; float3 v : TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.n = UnityObjectToWorldNormal(v.normal);
                o.v = normalize(WorldSpaceViewDir(v.vertex));
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float4 albedo = UNITY_ACCESS_INSTANCED_PROP(Props, _Color);
                float4 emission = UNITY_ACCESS_INSTANCED_PROP(Props, _Emission);
                float3 n = normalize(i.n);
                float ndl = saturate(dot(n, normalize(_WorldSpaceLightPos0.xyz)));
                float3 ambient = ShadeSH9(float4(n, 1));
                float rim = pow(1.0 - saturate(dot(n, normalize(i.v))), 3.0) * _Rim;
                float3 col = albedo.rgb * (ambient + _LightColor0.rgb * ndl) + emission.rgb + rim * emission.rgb * 0.5 + rim * 0.15;
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
