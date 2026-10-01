// RE:PLANET – Gelände (Built-in, Surface Shader mit Standard-Beleuchtung, Schattenempfang und Nebel).
// Grundfarbe/Maske bleibt die zur Laufzeit gemalte Geländetextur (Verschmutzung, Begrünung, Straßen – WorldView).
// Darüber: großflächige Farbvariation je Planet, Hangneigung → Fels mit Gesteinsschichtung (triplanar),
// Detailrauschen in der Nähe, nasser Uferstreifen. Straßen analytisch aus den Straßensegmenten des Layouts:
// Asphalt mit Körnung, Rissen, Flickstellen und Pfützen, scharfe Markierungen (Mittel-/Randlinien, Zebrastreifen an
// Kreuzungen, abgenutzt), Gehwegplatten mit Bordstein. Parameter setzt Runtime/Render/TerrainLook.cs.
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
        _RoadColor ("Asphalt (a = Markierungen)", Color) = (0.2, 0.2, 0.22, 1)
        _RoadStyle ("Markierung gelb, Gehweg, Risse, Schnee", Vector) = (0, 1, 1, 0)
        _RoadCount ("Anzahl Straßen", Float) = 0
        _BaseRect ("Stützpunkt (ohne Markierungen)", Vector) = (0, 0, 0, 0)
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
        float4 _RoadA[16];      // xy Anfang, zw Ende (Welt-xz)
        float4 _RoadB[16];      // x Breite
        float _RoadCount;
        float4 _RoadColor, _RoadStyle, _BaseRect;
        // Nässe 0..1 (global aus Runtime/Render/GroundMarks.cs; nicht gesetzt = 0 = trocken)
        float _RP_Wetness;

        float LineMask(float d, float halfW, float aa) { return 1.0 - smoothstep(halfW - aa, halfW + aa, d); }

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

            // Wiese: wo die Grundfarbe grün ist (Gras, Begrünung), Farbwechsel zwischen gelb- und blaugrünen Flächen,
            // dunklere Grasbüschel-Flecken, helle trockene Halmspitzen und kahle Erdstellen an mäßigen Hängen (Trittspuren)
            float slope0 = 1.0 - saturate(n.y);
            float grassM = saturate((base.g - max(base.r, base.b)) * 7.0);
            if (grassM > 0.001)
            {
                float hue = tex2D(_NoiseTex, wp.xz * 0.011 + 0.21).r;
                float3 gWarm = base * float3(1.12, 1.04, 0.72);
                float3 gCool = base * float3(0.8, 0.98, 1.08);
                float3 grass = lerp(gWarm, gCool, smoothstep(0.3, 0.7, hue));
                float clump = tex2D(_NoiseTex, wp.xz * 0.115 + 0.53).g;
                grass *= lerp(0.8, 1.12, clump);
                float aaG = fwidth(wp.x) * 2.0 + 0.02;
                float speck = tex2D(_NoiseTex, wp.xz * 1.35 + 0.77).a;
                float tuftD = smoothstep(0.74 - aaG, 0.8 + aaG, speck) * nearF;
                float tuftL = smoothstep(0.24 + aaG, 0.18 - aaG, speck) * nearF;
                grass = lerp(grass, grass * float3(0.58, 0.7, 0.55), tuftD * 0.8);
                grass = lerp(grass, grass * float3(1.25, 1.18, 0.75), tuftL * 0.6);
                // kahle Stellen: an Hängen und in Senken der Erosionsmaske
                float ero = tex2D(_NoiseTex, wp.xz * 0.042 + 0.11).r + slope0 * 0.9;
                float bare = smoothstep(0.78, 0.86, ero) * (1.0 - smoothstep(0.32, 0.45, slope0));
                float3 soil = lerp(_RockA.rgb, float3(0.47, 0.36, 0.24), 0.6) * lerp(0.82, 1.08, tex2D(_NoiseTex, wp.xz * 0.6).b);
                grass = lerp(grass, soil, bare * 0.85);
                base = lerp(base, grass, grassM);
            }

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
            float smoothness = saturate(lerp(_Glossiness, 0.12, rockMask) + wet * 0.45);

            // ---------------- Straßen (analytisch, scharf auch aus der Nähe)
            float roadMask = 0.0, walkMask = 0.0, curbMask = 0.0, inCount = 0.0;
            float bestS = 0.0, bestT = 0.0, bestHW = 1.0;
            int best = -1;
            [loop] for (int i = 0; i < 16; i++)
            {
                if (i >= (int)_RoadCount) break;
                float2 ra = _RoadA[i].xy, rb = _RoadA[i].zw;
                float hw = _RoadB[i].x * 0.5;
                float2 ab = rb - ra;
                float len = max(length(ab), 1e-3);
                float2 dir = ab / len;
                float2 ap = wp.xz - ra;
                float t = dot(ap, dir);
                float sd = dot(ap, float2(-dir.y, dir.x));
                float along = step(-0.5, t) * step(t, len + 0.5);
                float d = abs(sd);
                float inside = along * (1.0 - smoothstep(hw - 0.04, hw + 0.04, d));
                if (inside > 0.5)
                {
                    inCount += 1.0;
                    if (best < 0) { best = i; bestS = sd; bestT = dot(wp.xz, dir); bestHW = hw; }
                }
                roadMask = max(roadMask, inside);
                curbMask = max(curbMask, along * step(hw, d) * step(d, hw + 0.28));
                walkMask = max(walkMask, along * step(hw + 0.28, d) * step(d, hw + 2.8));
            }
            // Abstand zur Kante der nächsten kreuzenden Straße (für Zebrastreifen/Aussetzen der Linien)
            float minEdge = 99.0;
            [loop] for (int j = 0; j < 16; j++)
            {
                if (j >= (int)_RoadCount) break;
                if (j == best) continue;
                float2 ra = _RoadA[j].xy, rb = _RoadA[j].zw;
                float2 ab = rb - ra;
                float len = max(length(ab), 1e-3);
                float2 dir = ab / len;
                float2 ap = wp.xz - ra;
                float t = dot(ap, dir);
                if (t < -0.5 || t > len + 0.5) continue;
                minEdge = min(minEdge, abs(dot(ap, float2(-dir.y, dir.x))) - _RoadB[j].x * 0.5);
            }
            float inBase = step(_BaseRect.x, wp.x) * step(wp.x, _BaseRect.z) * step(_BaseRect.y, wp.z) * step(wp.z, _BaseRect.w);
            walkMask *= (1.0 - roadMask) * _RoadStyle.y * (1.0 - inBase);
            curbMask *= (1.0 - roadMask) * _RoadStyle.y * (1.0 - inBase);
            float flat = saturate(n.y * 4.0 - 3.0);
            roadMask *= flat;
            if (roadMask > 0.001 || walkMask > 0.001 || curbMask > 0.001)
            {
                float aa = max(fwidth(wp.x), fwidth(wp.z)) * 1.2 + 0.004;
                // Asphalt: Körnung, Flicken, Risse, Pfützen, Rinne am Rand
                float agg = tex2D(_NoiseTex, wp.xz * 1.9).a;
                float fine = tex2D(_NoiseTex, wp.xz * 0.61 + 0.3).b;
                float patchN = tex2D(_NoiseTex, wp.xz * 0.045 + 0.7).r;
                float crackN = tex2D(_NoiseTex, wp.xz * 0.13 + 0.2).g;
                float crackP = tex2D(_NoiseTex, wp.xz * 0.031 + 0.5).r;
                float crack = LineMask(abs(crackN - 0.5), 0.012, fwidth(crackN) + 0.004) * smoothstep(0.45, 0.6, crackP) * _RoadStyle.z;
                // Senken im Belag: trocken nur dunkle Schmutz-/Ölflecken (matt), erst bei Nässe spiegelnde Pfützen.
                // Vorher spiegelten sie immer mit Glätte 0,88 den hellen Abendhimmel → helle „Papier-/Schneeflecken“.
                float sink = tex2D(_NoiseTex, wp.xz * 0.055 + 0.13).b;
                float puddle = smoothstep(0.7, 0.75, sink) * (1.0 - _RoadStyle.w);
                float wetNow = saturate(_RP_Wetness);
                float gutter = saturate(1.0 - (bestHW - abs(bestS)) / 0.6);
                float3 asph = _RoadColor.rgb * lerp(0.78, 1.15, agg) * lerp(0.9, 1.08, fine);
                asph = lerp(asph, asph * 0.78, smoothstep(0.66, 0.7, patchN));
                asph *= 1.0 - crack * 0.6;
                asph = lerp(asph, asph * 0.7 + float3(0.02, 0.018, 0.015), gutter * 0.6);
                asph = lerp(asph, base, 0.18); // Schmutz/Verschmutzungsgrad aus der Geländetextur
                // Markierungen (abgenutzt), an Kreuzungen ausgesetzt, dort Zebrastreifen
                float wear = smoothstep(0.25, 0.55, tex2D(_NoiseTex, wp.xz * 0.37 + 0.9).g);
                float s = abs(bestS);
                float nearCross = step(minEdge, 5.5);
                float mark = 0.0;
                if (inCount < 1.5 && nearCross < 0.5)
                {
                    if (bestHW > 6.5)   // breite Hauptstraße: doppelte Mittellinie, gestrichelte Fahrstreifen
                    {
                        mark = max(LineMask(abs(s - 0.2), 0.07, aa), 0.0);
                        mark = max(mark, LineMask(abs(s - bestHW * 0.5), 0.07, aa) * step(frac(bestT / 6.0), 0.5));
                    }
                    else mark = LineMask(s, 0.08, aa) * step(frac(bestT / 6.0), 0.5);
                    mark = max(mark, LineMask(abs(s - (bestHW - 0.45)), 0.07, aa));
                }
                float zebra = 0.0;
                if (inCount < 1.5 && minEdge > 0.8 && minEdge < 4.3 && s < bestHW - 0.7)
                    zebra = step(frac(bestS / 1.1), 0.5) * LineMask(abs(minEdge - 2.55), 1.6, aa);
                mark = max(mark, zebra) * _RoadColor.a * (1.0 - inBase) * wear;
                float3 paint = lerp(float3(0.82, 0.82, 0.78), float3(0.95, 0.75, 0.2), _RoadStyle.x);
                float3 roadAlb = lerp(asph, paint, mark);
                float roadSmooth = lerp(0.12, 0.35, mark) * (1.0 - crack);
                // trockener Fleck: dunkler, leicht bräunlich, mit Rand (Schmutzkante); nass: Wasserfilm
                float rim = smoothstep(0.66, 0.7, sink) - puddle;
                float3 stain = roadAlb * float3(0.66, 0.63, 0.6);
                roadAlb = lerp(roadAlb, roadAlb * 0.82 + float3(0.012, 0.01, 0.006), saturate(rim) * 0.6 * (1.0 - wetNow));
                roadAlb = lerp(roadAlb, lerp(stain, roadAlb * 0.5, wetNow), puddle);
                roadSmooth = lerp(roadSmooth, lerp(0.22, 0.86, wetNow), puddle);
                // Abrieb in den Fahrspuren und Laub/Splitt am Rinnstein (gibt dem Asphalt Struktur statt heller Flecken)
                float lane = smoothstep(0.35, 0.0, abs(abs(bestS) - bestHW * 0.5)) * (1.0 - _RoadStyle.w);
                roadAlb *= lerp(1.0, lerp(0.93, 1.04, agg), lane * 0.5);
                float grit = step(0.83, tex2D(_NoiseTex, wp.xz * 2.7 + 0.41).a) * gutter;
                roadAlb = lerp(roadAlb, base * 0.7, grit * 0.5 * (1.0 - _RoadStyle.w));
                roadAlb = lerp(roadAlb, float3(0.92, 0.95, 1.0), _RoadStyle.w * smoothstep(0.35, 0.7, fine) * 0.6);
                // Gehwegplatten (0,6 m) und Bordstein
                float2 wuv = wp.xz / 0.6;
                float2 wf = abs(frac(wuv) - 0.5);
                float joint = 1.0 - smoothstep(0.46, 0.49, max(wf.x, wf.y));
                float slab = lerp(0.88, 1.08, frac(sin(dot(floor(wuv), float2(12.9898, 78.233))) * 43758.5453));
                float3 walkAlb = lerp(base, float3(0.55, 0.53, 0.5), 0.6) * slab * lerp(0.75, 1.0, joint) * lerp(0.9, 1.05, fine);
                float3 curbAlb = float3(0.66, 0.64, 0.6) * lerp(0.85, 1.05, agg);
                albedo = lerp(albedo, roadAlb, roadMask);
                smoothness = lerp(smoothness, roadSmooth, roadMask);
                albedo = lerp(albedo, walkAlb, walkMask * flat);
                albedo = lerp(albedo, curbAlb, curbMask * flat);
            }

            o.Albedo = albedo;
            o.Metallic = 0.0;
            o.Smoothness = smoothness;
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
