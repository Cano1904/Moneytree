// Separable 9-tap Gaussian blur for the live pause-menu overlay (match keeps running underneath).
Shader "Hidden/Glasscore/Blur"
{
    Properties { _MainTex ("Source", 2D) = "white" {} }
    CGINCLUDE
    #include "UnityCG.cginc"
    sampler2D _MainTex;
    float4 _MainTex_TexelSize;
    float2 _Direction;
    float _Spread;
    float _Darken;

    fixed4 blur (v2f_img i) : SV_Target
    {
        const float w0 = 0.2270270270, w1 = 0.1945945946, w2 = 0.1216216216, w3 = 0.0540540541, w4 = 0.0162162162;
        float2 o = _Direction * _MainTex_TexelSize.xy * _Spread;
        fixed4 c = tex2D(_MainTex, i.uv) * w0;
        c += (tex2D(_MainTex, i.uv + o) + tex2D(_MainTex, i.uv - o)) * w1;
        c += (tex2D(_MainTex, i.uv + o * 2) + tex2D(_MainTex, i.uv - o * 2)) * w2;
        c += (tex2D(_MainTex, i.uv + o * 3) + tex2D(_MainTex, i.uv - o * 3)) * w3;
        c += (tex2D(_MainTex, i.uv + o * 4) + tex2D(_MainTex, i.uv - o * 4)) * w4;
        c.rgb *= 1.0 - _Darken;
        return c;
    }
    ENDCG
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment blur
            ENDCG
        }
    }
    Fallback Off
}
