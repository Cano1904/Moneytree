// RE:PLANET – Gelände (Built-in, Surface Shader mit Standard-Beleuchtung, Schattenempfang und Nebel).
// Grundfarbe/Maske bleibt die zur Laufzeit gemalte Geländetextur (Verschmutzung, Begrünung, Straßen – WorldView).
// Darüber: großflächige Farbvariation je Planet, Hangneigung → Fels mit Gesteinsschichtung (triplanar),
// Detailrauschen in der Nähe, nasser Uferstreifen. Parameter setzt Runtime/Render/TerrainLook.cs.
Shader "RePlanet/Terrain"
{
    Properties
    {
        _MainTex ("Geländetextur", 2D) = "white" {}
        _NoiseTex ("Rauschen (RGBA, kachelbar)", 2D) = "gray" {}
        _RockA ("Fels hell", Color) = (0.55, 0.45, 0.38, 1)
        _RockB ("Fels dunkel", Color) = (0.32, 0.26, 0.22, 1)
        _TintA ("Farbvariation A", Color) = (1.0, 0.8, 0.5, 1)
        _TintB ("Farbvariation B", Color) = (0.5, 0.8, 0.75, 1)
        _Look ("Variation, Sättigung, Detail, Schichtdichte", Vector) = (0.25, 1.2, 0.6, 0.35)
        _Slope ("Hang (Beginn, Ende), Uferhöhe, Ufer an", Vector) = (0.22, 0.42, 0, 0)
        _Glossiness ("Glätte", Range(0, 1)) = 0.08
        _RimColor ("Streiflicht (Himmel)", Color) = (0.5, 0.6, 0.7, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        LOD 300

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert addshadow
        #pragma target 3.0

        sampler2D _MainTex;
        sampler2D _NoiseTex;
        float4 _RockA, _RockB, _TintA, _TintB, _Look, _Slope, _RimColor;
        half _Glossiness;

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
            float3 wNormal;
        };

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.wNormal = UnityObjectToWorldNormal(v.normal);
        }

        float LumT(float3 c) { return dot(c, float3(0.2126, 0.7152, 0.0722)); }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float3 wp = IN.worldPos;
            float3 n = normalize(IN.wNormal);
            float3 base = tex2D(_MainTex, IN.uv_MainTex).rgb;

            // kräftigere Grundfarben
            float l = LumT(base);
            base = max(lerp(l.xxx, base, _Look.y), 0.0);

            // großflächige Farbvariation (Farbton wandert über die Landschaft)
            float big = tex2D(_NoiseTex, wp.xz * 0.0045).r;
            float mid = tex2D(_NoiseTex, wp.xz * 0.021 + 0.37).g;
            float3 tint = lerp(_TintA.rgb, _TintB.rgb, smoothstep(0.3, 0.7, big));
            base *= lerp(float3(1, 1, 1), tint / max(LumT(tint), 0.01), _Look.x);
            base *= 0.88 + mid * 0.24;

            // Detailrauschen nur in der Nähe
            float dist = distance(wp, _WorldSpaceCameraPos);
            float nearF = saturate(1.0 - dist / 55.0);
            float det = tex2D(_NoiseTex, wp.xz * 0.23).b * 0.6 + tex2D(_NoiseTex, wp.xz * 0.97).a * 0.4;
            base *= lerp(1.0, 0.8 + det * 0.4, nearF * _Look.z);

            // Fels an steilen Hängen: Gesteinsschichtung (Höhenbänder, verwirbelt) + triplanares Detail
            float slope = 1.0 - saturate(n.y);
            float rockMask = smoothstep(_Slope.x, _Slope.y, slope + (mid - 0.5) * 0.18);
            float3 bw = pow(abs(n), 4.0);
            bw /= max(bw.x + bw.y + bw.z, 0.0001);
            float tri = tex2D(_NoiseTex, wp.zy * 0.31).b * bw.x + tex2D(_NoiseTex, wp.xz * 0.31).b * bw.y + tex2D(_NoiseTex, wp.xy * 0.31).b * bw.z;
            float warp = tex2D(_NoiseTex, wp.xz * 0.03 + float2(0.0, wp.y * 0.01)).r;
            float band1 = sin((wp.y + warp * 4.0) * _Look.w * 6.2832) * 0.5 + 0.5;
            float band2 = sin((wp.y * 2.7 + warp * 7.0) * _Look.w * 6.2832) * 0.5 + 0.5;
            float3 rock = lerp(_RockB.rgb, _RockA.rgb, saturate(band1 * 0.65 + band2 * 0.35));
            rock *= 0.72 + tri * 0.56;
            rock = lerp(rock, rock * base / max(LumT(base), 0.02) * 0.5 + rock * 0.5, 0.3);
            float3 albedo = lerp(base, rock, rockMask);

            // nasser Uferstreifen (dunkler, glänzender)
            float wet = _Slope.w * saturate(1.0 - abs(wp.y - _Slope.z - 0.15) / 0.55);
            albedo *= 1.0 - wet * 0.35;

            o.Albedo = albedo;
            o.Metallic = 0.0;
            o.Smoothness = saturate(lerp(_Glossiness, 0.12, rockMask) + wet * 0.45);
            o.Occlusion = lerp(1.0, 0.7 + tri * 0.3, rockMask);
            // leichtes Streiflicht in Himmelsfarbe an Silhouetten (Tiefe, NMS-typisch)
            float3 V = (_WorldSpaceCameraPos - wp) / max(dist, 0.0001);
            float rim = pow(1.0 - saturate(dot(n, V)), 4.0);
            o.Emission = _RimColor.rgb * rim * 0.12 * (1.0 - nearF * 0.5);
            o.Alpha = 1.0;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
