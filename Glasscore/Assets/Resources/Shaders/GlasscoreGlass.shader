// Transparent glass tile / shard: fresnel rim, neon edge glow and procedural damage overlay.
// _Crack      0 = pristine, 1 = heavily cracked (cellular crack network)
// _Spiderweb  0/1 tempered glass below 30 HP: radial + concentric fracture web
Shader "Glasscore/Glass"
{
    Properties
    {
        _Color ("Tint", Color) = (0.55, 0.85, 1.0, 0.22)
        _EdgeColor ("Edge Glow", Color) = (0.0, 1.0, 1.0, 1.0)
        _Crack ("Crack Amount", Range(0,1)) = 0
        _Spiderweb ("Spiderweb", Range(0,1)) = 0
        _Seed ("Pattern Seed", Float) = 0
        _Warning ("Destabilized Pulse", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
                UNITY_DEFINE_INSTANCED_PROP(float, _Crack)
                UNITY_DEFINE_INSTANCED_PROP(float, _Spiderweb)
                UNITY_DEFINE_INSTANCED_PROP(float, _Seed)
                UNITY_DEFINE_INSTANCED_PROP(float, _Warning)
            UNITY_INSTANCING_BUFFER_END(Props)
            float4 _EdgeColor;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 viewDir : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.viewDir = normalize(WorldSpaceViewDir(v.vertex));
                return o;
            }

            float2 hash2(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453);
            }

            // Distance to the nearest Voronoi cell border → thin bright crack lines.
            float cellEdge(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float d1 = 8.0, d2 = 8.0;
                for (int y = -1; y <= 1; y++)
                for (int x = -1; x <= 1; x++)
                {
                    float2 g = float2(x, y);
                    float2 r = g + hash2(i + g) - f;
                    float d = dot(r, r);
                    if (d < d1) { d2 = d1; d1 = d; } else if (d < d2) { d2 = d; }
                }
                return sqrt(d2) - sqrt(d1);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float4 tint = UNITY_ACCESS_INSTANCED_PROP(Props, _Color);
                float crack = UNITY_ACCESS_INSTANCED_PROP(Props, _Crack);
                float web = UNITY_ACCESS_INSTANCED_PROP(Props, _Spiderweb);
                float seed = UNITY_ACCESS_INSTANCED_PROP(Props, _Seed);
                float warn = UNITY_ACCESS_INSTANCED_PROP(Props, _Warning);

                float fresnel = pow(1.0 - saturate(abs(dot(normalize(i.worldNormal), normalize(i.viewDir)))), 3.0);
                float2 uv = i.uv;
                float border = 1.0 - smoothstep(0.0, 0.035, min(min(uv.x, 1.0 - uv.x), min(uv.y, 1.0 - uv.y)));

                float lines = 0;
                if (crack > 0.001)
                {
                    float e = cellEdge(uv * lerp(3.0, 7.0, crack) + seed * 17.13);
                    lines = (1.0 - smoothstep(0.0, 0.03 + 0.03 * crack, e)) * crack;
                }
                if (web > 0.5)
                {
                    float2 c = uv - 0.5 - (hash2(seed.xx) - 0.5) * 0.3;
                    float ang = atan2(c.y, c.x);
                    float rad = length(c);
                    float spokes = 1.0 - smoothstep(0.0, 0.06, abs(sin(ang * 9.0 + seed)));
                    float rings = 1.0 - smoothstep(0.0, 0.04, abs(sin(rad * 38.0)));
                    lines = max(lines, max(spokes, rings * 0.8) * saturate(1.2 - rad * 1.4));
                }

                float pulse = warn * (0.5 + 0.5 * sin(_Time.y * 12.0));
                float3 col = tint.rgb + fresnel * 0.6 + _EdgeColor.rgb * border * 0.9 + lines * float3(0.95, 1.0, 1.0) + pulse * float3(1.0, 0.25, 0.1);
                float alpha = saturate(tint.a + fresnel * 0.45 + border * 0.6 + lines * 0.8 + pulse * 0.4);
                return fixed4(col, alpha);
            }
            ENDCG
        }
    }
    Fallback Off
}
