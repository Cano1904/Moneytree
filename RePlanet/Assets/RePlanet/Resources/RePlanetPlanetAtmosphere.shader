// RE:PLANET – Atmosphärensaum der Planeten in der Planetenwahl (Runtime/Render/PlanetSelectScene.cs).
// Auf einer etwas größeren Kugel um den Planeten (Rückseiten, additiv, ohne Tiefenschreiben): Für jeden Bildpunkt
// wird der kleinste Abstand des Sichtstrahls zum Planetenmittelpunkt (in Planetenradien) bestimmt. Außerhalb der
// Scheibe nimmt die Dichte exponentiell ab (weicher Saum), zur Sonne hin ist der Saum hell, gegen die Sonne leuchtet
// er durch Vorwärtsstreuung auf (Gegenlicht-Sichel), auf der Nachtseite bleibt ein dünner, kühler Rand.
Shader "RePlanet/PlanetAtmosphere"
{
    Properties
    {
        _AtmoColor ("Atmosphäre", Color) = (0.4, 0.7, 1.0, 1)
        _SunDirW ("Richtung zur Sonne (Welt)", Vector) = (0, 0.3, -1, 0)
        _SunColor ("Sonnenlicht", Color) = (1.4, 1.3, 1.15, 1)
        _Center ("Mittelpunkt (xyz), Radius (w)", Vector) = (0, 0, 0, 1)
        _Params ("x Stärke, y Höhe (Anteil des Radius), z Vorwärtsstreuung, w Nachtsaum", Vector) = (1, 0.045, 1, 0.25)
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        LOD 100
        Blend One One
        ZWrite Off
        Cull Front

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            float4 _AtmoColor, _SunDirW, _SunColor, _Center, _Params;

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; float3 wp : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.wp = mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1.0)).xyz;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float R = max(_Center.w, 1e-3);
                float3 cam = _WorldSpaceCameraPos;
                float3 d = i.wp - cam;
                d *= rsqrt(max(dot(d, d), 1e-8));
                float3 toC = _Center.xyz - cam;
                float t = dot(toC, d);                       // nächster Punkt des Strahls zum Mittelpunkt
                float3 closest = cam + d * max(t, 0.0);
                float3 off = closest - _Center.xyz;
                float b = sqrt(max(dot(off, off), 0.0)) / R;  // Abstand in Planetenradien
                float H = max(_Params.y, 1e-3);
                // außerhalb der Scheibe: exponentieller Abfall; innen (Strahl trifft den Planeten) übernimmt der Planet
                float dens = b >= 1.0 ? exp(-(b - 1.0) / H) : 0.0;
                dens *= smoothstep(1.0, 1.0 + H * 0.35, b) * 0.6 + 0.4 * step(1.0, b);
                float3 L = _SunDirW.xyz * rsqrt(max(dot(_SunDirW.xyz, _SunDirW.xyz), 1e-8));
                float3 up = off * rsqrt(max(dot(off, off), 1e-8));
                float lit = saturate(dot(up, L) * 0.8 + 0.35);
                float forward = pow(saturate(dot(d, L)), 6.0) * _Params.z;  // Gegenlicht
                float night = _Params.w * (1.0 - lit);
                float3 col = _AtmoColor.rgb * dens * (lit * 1.6 + night) * _Params.x;
                col += _SunColor.rgb * _AtmoColor.rgb * dens * forward * 1.5 * _Params.x;
                return float4(clamp(col, 0.0, 16.0), 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
