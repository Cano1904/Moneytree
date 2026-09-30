// RE:PLANET – Oberflächen (Built-in, Surface Shader mit Standard-Beleuchtung, Schatten und Nebel).
// Grundfarbe aus der Farbpalette (UV0, Alpha = Glätte), dazu je Ecke (UV1): x = Oberflächenklasse + Zufallswert je Bauteil,
// y = Bodennähe (1 am Wandfuß). Daraus entstehen im Objektraum projiziert: Putz mit Wasserflecken, Regenschlieren und
// Farbklecksen, Beton mit Schalungsfugen, Ziegel im Verband, Wellblech mit Rostläufern, Lack mit Kratzern, Rost und
// Kantenabrieb, Asphalt mit Körnung und Rissen, Gummi, Holz, Fliesen, Stein. Detail blendet mit der Entfernung aus.
// Parameter: Runtime/Render/SurfaceLook.cs.
// Nur aktiv, wenn die Einstellung „Detail-Shader“ an ist (Standard: aus – dann Standard-Shader mit Palette).
// Absichtlich OHNE Normalen-Ausgabe (o.Normal/INTERNAL_DATA/Ersatz-Tangenten): Die frühere Relief-Berechnung über
// Bildschirmableitungen war der riskanteste Teil (in Unity erschienen die Teile reinweiß). Fugen und Relief wirken jetzt
// über Grundfarbe und Umgebungsverdeckung; alle Ausgaben sind begrenzt (keine NaN/Unendlich-Werte in den HDR-Puffer).
Shader "RePlanet/Surface"
{
    Properties
    {
        _Color ("Farbe", Color) = (1, 1, 1, 1)
        _MainTex ("Grundfarbe / Palette", 2D) = "white" {}
        _DetailTex ("Oberflächendetail (RGBA, kachelbar)", 2D) = "gray" {}
        _Glossiness ("Glätte", Range(0, 1)) = 0.2
        _Metallic ("Metall", Range(0, 1)) = 0
        _EmissionColor ("Leuchten", Color) = (0, 0, 0, 1)
        _Look ("Detail, Schmutz, Fugentiefe, Nahbereich (m)", Vector) = (1, 1, 1, 70)
        _DirtColor ("Schmutzfarbe", Color) = (0.3, 0.25, 0.2, 1)
        _SurfClass ("Klasse erzwingen (-1 = je Ecke)", Float) = -1
        _GlowTex ("Leuchten je Palettenfarbe", 2D) = "black" {}
        _GlowScale ("Leuchtfaktor", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        LOD 300

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert addshadow
        #pragma target 3.0
        #pragma multi_compile_instancing

        sampler2D _MainTex;
        sampler2D _DetailTex;
        sampler2D _GlowTex;
        float _GlowScale;
        half4 _Color, _EmissionColor, _DirtColor;
        half _Glossiness, _Metallic;
        float4 _Look;
        float _SurfClass;

        struct Input
        {
            float2 uv_MainTex;
            float4 sdata;
            float3 oPos;
            float3 oNrm;
            float3 worldPos;
        };

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.sdata = float4(v.texcoord1.xy, 0, 0);
            o.oPos = v.vertex.xyz;
            o.oNrm = v.normal;
        }

        float Hash21(float2 p)
        {
            p = frac(p * float2(123.34, 456.21));
            p += dot(p, p + 45.32);
            return frac(p.x * p.y);
        }

        float Lum(float3 c) { return dot(c, float3(0.2126, 0.7152, 0.0722)); }

        // Abstand zur nächsten Gitterlinie (Periode per), in Metern
        float LineDist(float x, float per) { return abs(frac(x / per + 0.5) - 0.5) * per; }

        float3 Hue(float h)
        {
            float3 k = saturate(abs(frac(h + float3(0.0, 0.6667, 0.3333)) * 6.0 - 3.0) - 1.0);
            return k;
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            half4 pal = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            float cls = _SurfClass >= 0 ? _SurfClass : floor(IN.sdata.x + 0.002);
            float rnd = frac(IN.sdata.x);
            float low = saturate(IN.sdata.y);
            float3 p = IN.oPos;
            // Normale ohne normalize(): eine Null-Normale (entartetes Dreieck) ergäbe sonst NaN
            float3 n = IN.oNrm * rsqrt(max(dot(IN.oNrm, IN.oNrm), 1e-8));
            float3 an = abs(n);
            // Projektion auf die dominante Achse: Wände (x entlang, y = Höhe), Böden/Dächer (xz)
            float isTop = step(max(an.x, an.z), an.y);
            float isX = (1.0 - isTop) * step(an.z, an.x);
            float2 uv = lerp(lerp(p.xy, p.zy, isX), p.xz, isTop);
            float2 uvn = uv + rnd * float2(13.7, 5.3); // Rauschversatz je Bauteil

            float dist = distance(IN.worldPos, _WorldSpaceCameraPos);
            float nearF = saturate(1.0 - dist / _Look.w);
            float px = max(max(fwidth(uv.x), fwidth(uv.y)), 1e-5); // Meter je Pixel
            float wall = 1.0 - isTop;

            // Detailtextur in vier Maßstäben (alle Abfragen vor den Verzweigungen)
            half4 dA = tex2D(_DetailTex, uvn * 0.21);
            half4 dB = tex2D(_DetailTex, uvn * 0.83 + 0.31);
            half4 dC = tex2D(_DetailTex, uvn * 3.1 + 0.67);
            half4 dS = tex2D(_DetailTex, float2(uvn.x * 0.55, uv.y * 0.045 + rnd * 3.0));

            // Kantenabrieb: starke Normalenkrümmung (Fasen) im Verhältnis zur Pixelgröße
            float curv = min(length(fwidth(IN.oNrm)) / max(length(fwidth(IN.oPos)), 1e-4), 100.0);
            float edge = saturate((curv - 6.0) / 18.0) * nearF;

            float3 alb = pal.rgb * (1.0 + (rnd - 0.5) * 0.1);
            float smooth = _Glossiness * pal.a; // Palette: Alpha = Glätte je Farbe
            float metal = _Metallic;
            float h = 0.0;          // Relief in Metern (negativ = Fuge/Riss) – wirkt als Verdeckung
            float occ = 1.0;
            float3 rustCol = float3(0.42, 0.2, 0.09) * lerp(0.75, 1.15, dC.g);
            float dirtK = 1.0;

            if (cls < 0.5)                  // allgemein
            {
                alb *= lerp(0.9, 1.07, dA.r) * lerp(0.95, 1.04, dC.g);
                h = dC.g * 0.0015;
            }
            else if (cls < 1.5)             // Putz
            {
                alb *= lerp(0.8, 1.08, dA.r) * lerp(0.9, 1.06, dC.g);
                float stain = smoothstep(0.55, 0.85, dA.a) * 0.22;
                float streak = smoothstep(0.45, 0.9, dS.r) * wall * (0.12 + 0.2 * (1.0 - low));
                alb *= (1.0 - stain) * (1.0 - streak);
                // Farbkleckse/Sprühflecken in Griffhöhe (nur einzelne Bauteile)
                float2 cell = floor(uv / 3.0);
                float spray = smoothstep(0.7, 0.78, dB.a) * wall * step(0.55, frac(rnd * 3.7)) * saturate(low * 2.5);
                float3 sc = lerp(Hue(Hash21(cell + rnd * 7.0)), float3(0.95, 0.95, 0.92), 0.25) * 0.72;
                alb = lerp(alb, sc, spray * 0.8);
                h = dC.g * 0.004 + dB.r * 0.003;
                smooth *= 0.7;
            }
            else if (cls < 2.5)             // Beton
            {
                float seam = 1.0 - smoothstep(0.008, 0.008 + px * 1.5, min(LineDist(uv.x, 2.4), LineDist(uv.y, 1.2)));
                float2 tie = frac(uv / float2(0.6, 0.6)) - 0.5;
                float hole = (1.0 - smoothstep(0.012, 0.012 + px * 1.5, length(tie * 0.6))) * wall;
                float fade = saturate(1.0 - px / 0.04);
                alb *= lerp(0.8, 1.08, dA.r) * lerp(0.88, 1.07, dC.g);
                alb *= 1.0 - (seam * 0.3 + hole * 0.45) * fade;
                alb *= 1.0 - smoothstep(0.6, 0.9, dA.a) * 0.18;
                h = dC.g * 0.004 - (seam + hole) * 0.006 * fade;
                smooth *= 0.6;
            }
            else if (cls < 3.5)             // Ziegel (Läuferverband)
            {
                float bw = 0.25, bh = 0.085;
                float row = floor(uv.y / bh);
                float bx = uv.x / bw + 0.5 * fmod(abs(row), 2.0);
                float2 cell = float2(floor(bx), row);
                float fx = frac(bx), fy = frac(uv.y / bh);
                float md = min(min(fx, 1.0 - fx) * bw, min(fy, 1.0 - fy) * bh);
                float mortar = 1.0 - smoothstep(0.006, 0.006 + px, md);
                float fade = saturate(1.0 - px / 0.025);
                float v1 = Hash21(cell + rnd * 11.0), v2 = Hash21(cell + 7.1);
                float3 brick = pal.rgb * lerp(0.72, 1.18, v1) * lerp(float3(1, 1, 1), float3(1.1, 0.92, 0.85), v2);
                brick *= lerp(0.88, 1.06, dC.g);
                float3 mortarC = lerp(float3(0.62, 0.6, 0.56), pal.rgb, 0.25) * lerp(0.85, 1.05, dC.r);
                float3 near = lerp(brick, mortarC, mortar);
                float3 far = pal.rgb * lerp(0.86, 1.06, dA.r) * 0.95;
                alb = lerp(far, near, fade) * (1.0 - smoothstep(0.55, 0.9, dA.a) * 0.2);
                h = ((1.0 - mortar) * 0.008 + dC.g * 0.002) * fade;
                smooth *= 0.55;
            }
            else if (cls < 4.5)             // Wellblech / Metallverkleidung
            {
                float fade = saturate(1.0 - px / 0.05) * wall;
                float rib = sin(uv.x * 6.2832 / 0.2);
                float joint = 1.0 - smoothstep(0.01, 0.01 + px * 1.5, LineDist(uv.y, 2.6));
                float rust = smoothstep(0.5, 0.95, dS.r) * smoothstep(0.35, 0.75, dA.a + low * 0.3);
                alb *= lerp(0.84, 1.08, dA.r) * (1.0 + rib * 0.06 * fade) * (1.0 - joint * 0.35 * fade);
                alb = lerp(alb, rustCol, rust * 0.75);
                h = rib * 0.012 * fade - joint * 0.004;
                smooth = lerp(smooth, 0.12, rust);
                metal *= 1.0 - rust;
            }
            else if (cls < 5.5)             // Lack auf Metall: Kratzer, Rostflecken, Kantenabrieb
            {
                float scratch = (1.0 - dB.b) * nearF;
                float rust = smoothstep(0.72, 0.9, dA.a + low * 0.35 + edge * 0.15);
                float3 bare = float3(0.6, 0.6, 0.6);
                alb *= lerp(0.9, 1.06, dA.r) * lerp(0.95, 1.03, dC.g);
                alb = lerp(alb, bare, saturate(scratch * 0.55 + edge * 0.55));
                alb = lerp(alb, rustCol, rust);
                smooth = lerp(smooth, 0.7, saturate(scratch + edge) * 0.5);
                smooth = lerp(smooth, 0.15, rust);
                metal = lerp(metal, 0.85, saturate(edge + scratch * 0.5)) * (1.0 - rust);
                h = rust * dC.g * 0.004 - scratch * 0.0015;
            }
            else if (cls < 6.5)             // Asphalt / Pflaster
            {
                float crack = (1.0 - dB.b) * saturate(1.0 - px / 0.03);
                float patch = smoothstep(0.62, 0.66, dA.a);
                alb *= lerp(0.78, 1.12, dC.g) * lerp(0.86, 1.05, dA.r);
                alb *= 1.0 - crack * 0.55;
                alb = lerp(alb, alb * 0.78, patch);
                h = dC.g * 0.003 - crack * 0.01;
                smooth = lerp(0.1, 0.22, patch) * (1.0 - crack);
                dirtK = 0.5;
            }
            else if (cls < 7.5)             // Gummi / Kunststoff
            {
                alb *= lerp(0.9, 1.06, dC.g) * lerp(0.94, 1.04, dA.r);
                h = dC.g * 0.002;
                smooth *= 0.6;
            }
            else if (cls < 8.5)             // Holz: Maserung und Bretterfugen
            {
                float grain = sin((uv.y + dB.r * 0.12) * 260.0) * 0.5 + 0.5;
                float plank = 1.0 - smoothstep(0.004, 0.004 + px * 1.5, LineDist(uv.y, 0.22));
                float fade = saturate(1.0 - px / 0.02);
                alb *= lerp(0.8, 1.1, dA.r) * (1.0 - grain * 0.12 * fade) * (1.0 - plank * 0.45);
                alb = lerp(alb, Lum(alb) * float3(0.8, 0.8, 0.82), smoothstep(0.55, 0.85, dA.a) * 0.5); // vergraut
                h = grain * 0.0015 * fade - plank * 0.004;
                smooth *= 0.5;
            }
            else if (cls < 9.5)             // Fliesen / Platten
            {
                float grout = 1.0 - smoothstep(0.004, 0.004 + px * 1.5, min(LineDist(uv.x, 0.3), LineDist(uv.y, 0.3)));
                float2 cell = floor(uv / 0.3);
                alb *= lerp(0.9, 1.08, Hash21(cell)) * lerp(0.94, 1.04, dC.g);
                alb = lerp(alb, float3(0.35, 0.34, 0.32), grout * 0.7);
                h = -grout * 0.003;
                smooth = lerp(smooth + 0.2, 0.1, grout);
            }
            else                            // Stein / Quader
            {
                alb *= lerp(0.78, 1.12, dA.r) * lerp(0.86, 1.08, dC.g) * lerp(0.9, 1.05, dB.r);
                alb *= 1.0 - (1.0 - dB.b) * 0.35;
                h = dC.g * 0.006 + dA.r * 0.004;
                smooth *= 0.6;
            }

            // Schmutz am Fuß (Spritzwasser, Staub) und leichte allgemeine Verschmutzung
            float grime = low * sqrt(low) * lerp(0.55, 1.0, dA.r) * _Look.y * dirtK;
            grime = saturate(grime + smoothstep(0.7, 1.0, dA.a) * 0.12 * _Look.y);
            alb = lerp(alb, _DirtColor.rgb * lerp(0.7, 1.1, dC.g), grime * 0.6);
            smooth *= 1.0 - grime * 0.6;
            occ = 1.0 - low * low * low * 0.35;

            // Relief ohne Normalen: Fugen, Risse und Mörtel als Verdeckung (dunkler in der Tiefe), im Nahbereich
            float groove = saturate(-h * 90.0) * nearF * _Look.z;
            occ *= 1.0 - groove * 0.45;

            float3 albedo = lerp(pal.rgb, saturate(alb), saturate(_Look.x));
            o.Albedo = saturate(albedo);
            o.Metallic = saturate(metal);
            o.Smoothness = saturate(smooth) * 0.95;
            o.Occlusion = saturate(occ);
            float3 emi = _EmissionColor.rgb + tex2D(_GlowTex, IN.uv_MainTex).rgb * _GlowScale;
            o.Emission = clamp(emi, 0.0, 16.0);
            o.Alpha = 1.0;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
