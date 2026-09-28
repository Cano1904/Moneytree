// RE:PLANET – Fenster (Built-in, Surface Shader mit Standard-Beleuchtung).
// Jede Scheibe ist ein flacher Quader mit Flächen-UVs 0..1 (UV0); UV1.x trägt einen Zufallswert je Fenster.
// Daraus: Rahmen und Sprossen (je Fenster unterschiedlich), dunkles, stark spiegelndes Glas mit leichten Schlieren,
// Laibungsschatten und – sobald _EmissionColor (Lichtstärke des Bereichs, von WorldView gesetzt) > 0 ist – teils
// beleuchtete Innenräume: warmes Deckenlicht mit Verlauf, Vorhänge, Jalousien, selten ein bläulicher Bildschirm und
// sehr selten eine flackernde Lampe.
Shader "RePlanet/Window"
{
    Properties
    {
        _Color ("Glas", Color) = (0.08, 0.1, 0.12, 1)
        _FrameColor ("Rahmen", Color) = (0.82, 0.8, 0.76, 1)
        _DetailTex ("Oberflächendetail (RGBA, kachelbar)", 2D) = "gray" {}
        _Glossiness ("Glätte Glas", Range(0, 1)) = 0.93
        _EmissionColor ("Licht (Stärke = Helligkeit)", Color) = (0, 0, 0, 1)
        _LitShare ("Anteil beleuchteter Fenster", Range(0, 1)) = 0.62
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        LOD 300

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert addshadow
        #pragma target 3.0
        #pragma multi_compile_instancing

        sampler2D _DetailTex;
        half4 _Color, _FrameColor, _EmissionColor;
        half _Glossiness, _LitShare;

        struct Input
        {
            float4 wdata;
            float3 worldPos;
        };

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.wdata = float4(v.texcoord.xy, v.texcoord1.xy);
        }

        float Hash11(float x) { return frac(sin(x * 91.3458) * 47453.5453); }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float2 uv = IN.wdata.xy;
            float id = frac(IN.wdata.z) + floor(IN.wdata.z) * 0.137;
            float2 fw = max(fwidth(uv), 1e-5);
            float2 e = min(uv, 1.0 - uv);

            // Rahmen, Sprossen, Kämpfer (je Fenster anders)
            float fr = 0.075;
            float inner = smoothstep(fr - fw.x, fr + fw.x, e.x) * smoothstep(fr * 1.2 - fw.y, fr * 1.2 + fw.y, e.y);
            float style = Hash11(id * 7.31 + 1.7);
            float mull = style < 0.55 ? 1.0 - smoothstep(0.02 - fw.x, 0.02 + fw.x, abs(uv.x - 0.5)) : 0.0;
            float tran = style < 0.3 || style > 0.8 ? 1.0 - smoothstep(0.022 - fw.y, 0.022 + fw.y, abs(uv.y - 0.68)) : 0.0;
            float frame = saturate(1.0 - inner + mull + tran);
            float reveal = smoothstep(0.0, 0.2, min(e.x, e.y)); // Laibungsschatten

            // Glas: dunkel, spiegelnd, mit Schlieren
            half4 d = tex2D(_DetailTex, IN.worldPos.xz * 0.37 + IN.worldPos.y * 0.21 + id * 3.1);
            float smudge = smoothstep(0.55, 0.9, d.a) * 0.35;
            float3 glass = _Color.rgb * lerp(0.8, 1.15, d.r);

            // Innenraum (nur wenn der Bereich Licht hat)
            float lit = max(_EmissionColor.r, max(_EmissionColor.g, _EmissionColor.b));
            float on = step(Hash11(id * 13.7 + 0.3), _LitShare);
            float hueR = Hash11(id * 5.13 + 2.1);
            float3 warm = lerp(float3(1.0, 0.58, 0.28), float3(1.0, 0.83, 0.58), hueR);
            float tv = step(0.93, Hash11(id * 3.77 + 4.4));
            warm = lerp(warm, float3(0.42, 0.62, 1.0), tv);
            float room = 0.35 + 0.65 * smoothstep(0.0, 1.0, uv.y);                   // Deckenlicht
            room *= 0.75 + 0.25 * sin(uv.x * 3.14159);                              // zur Mitte heller
            float curtain = Hash11(id * 9.1 + 0.7) > 0.45 ? smoothstep(0.3, 0.18, min(uv.x, 1.0 - uv.x)) : 0.0;
            room *= lerp(1.0, 0.45 + 0.12 * sin(uv.x * 95.0), curtain);
            float blinds = Hash11(id * 4.3 + 8.8) > 0.8 ? step(0.55, frac(uv.y * 13.0)) : 0.0;
            room *= 1.0 - blinds * 0.55;
            // Silhouette eines Möbelstücks im unteren Drittel
            float furn = Hash11(id * 2.9) > 0.6 ? (1.0 - step(0.28, uv.y)) * step(0.25, uv.x) * step(uv.x, 0.6) : 0.0;
            room *= 1.0 - furn * 0.6;
            // sehr selten: flackernde Lampe
            float flick = 1.0;
            if (Hash11(id * 21.3 + 5.0) > 0.965)
                flick = 0.45 + 0.55 * step(0.3, Hash11(floor(_Time.y * 11.0) + id * 40.0));
            float3 emit = warm * room * on * flick * lit * reveal * (1.0 - frame);
            // unbeleuchtete Fenster: kaum sichtbarer Restschein
            emit += warm * 0.02 * lit * (1.0 - on) * (1.0 - frame);

            // Rahmenfarbe je Fenster: weiß, dunkelgrau oder Holz
            float fsel = Hash11(id * 6.6 + 3.3);
            float3 frameC = fsel < 0.55 ? _FrameColor.rgb : fsel < 0.85 ? float3(0.2, 0.21, 0.23) : float3(0.4, 0.28, 0.18);

            o.Albedo = lerp(glass * (1.0 - on * lit * 0.2), frameC, frame);
            o.Metallic = 0.0;
            o.Smoothness = lerp(_Glossiness * (1.0 - smudge), 0.35, frame);
            o.Occlusion = lerp(0.6, 1.0, reveal);
            o.Emission = emit;
            o.Alpha = 1.0;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
