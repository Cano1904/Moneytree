// Vertical gradient skybox with a subtle star field (no texture assets).
Shader "Glasscore/Sky"
{
    Properties
    {
        _Top ("Top", Color) = (0.01, 0.02, 0.06, 1)
        _Horizon ("Horizon", Color) = (0.05, 0.18, 0.28, 1)
        _Bottom ("Bottom", Color) = (0.0, 0.0, 0.01, 1)
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _Top, _Horizon, _Bottom;
            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };
            v2f vert (appdata v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.dir = v.vertex.xyz; return o; }
            float hash(float3 p) { return frac(sin(dot(p, float3(12.9898, 78.233, 45.164))) * 43758.5453); }
            fixed4 frag (v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float3 col = d.y > 0 ? lerp(_Horizon.rgb, _Top.rgb, pow(saturate(d.y), 0.6)) : lerp(_Horizon.rgb, _Bottom.rgb, pow(saturate(-d.y), 0.5));
                float3 cell = floor(d * 220.0);
                float star = step(0.9975, hash(cell)) * saturate(d.y * 3.0);
                return fixed4(col + star * 0.8, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
