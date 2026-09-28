// RE:PLANET – prozeduraler Himmel (Built-in Render Pipeline)
// Farbverläufe, Dunstband am Horizont, Sonne mit Glühen, animierte Wolkenfelder mit Domain-Warping,
// Nebelschleier, Sterne, zwei Himmelskörper (Planeten/Monde) mit Tag-/Nachtseite und Atmosphärensaum,
// Polarlicht. Alle Parameter setzt Runtime/Render/Atmosphere.cs je Planet, Tageszeit und Wetter.
Shader "RePlanet/Sky"
{
    Properties
    {
        _ZenithColor ("Zenit", Color) = (0.30, 0.50, 0.80, 1)
        _HorizonColor ("Horizont", Color) = (1.00, 0.75, 0.50, 1)
        _HazeColor ("Dunstband", Color) = (0.90, 0.70, 0.55, 1)
        _GroundColor ("Boden", Color) = (0.30, 0.25, 0.20, 1)
        _HazeStrength ("Dunst", Range(0, 1)) = 0.6
        _SunDir ("Sonnenrichtung", Vector) = (0.3, 0.4, 0.8, 0)
        _SunColor ("Sonnenfarbe", Color) = (1.0, 0.85, 0.6, 1)
        _SunSize ("Sonnengröße (rad)", Float) = 0.035
        _SunIntensity ("Sonnenintensität", Float) = 2.0
        _CloudLight ("Wolken hell", Color) = (1.0, 0.85, 0.7, 1)
        _CloudShadow ("Wolken dunkel", Color) = (0.45, 0.35, 0.40, 1)
        _CloudCover ("Bedeckung", Range(0, 1)) = 0.5
        _CloudDensity ("Dichte", Float) = 2.5
        _CloudScale ("Wolkenmaßstab", Float) = 0.9
        _CloudSpeed ("Wolkentempo", Float) = 0.02
        _CloudWind ("Windrichtung", Vector) = (1, 0, 0.3, 0)
        _NebulaA ("Nebel A", Color) = (0.9, 0.4, 0.2, 1)
        _NebulaB ("Nebel B", Color) = (0.3, 0.1, 0.4, 1)
        _NebulaStrength ("Nebelstärke", Float) = 0.2
        _StarStrength ("Sterne", Float) = 0.0
        _P1Dir ("Himmelskörper 1 (Richtung, Radius)", Vector) = (0.4, 0.35, 0.85, 0.15)
        _P1ColorA ("Körper 1 Farbe A", Color) = (0.7, 0.75, 0.85, 1)
        _P1ColorB ("Körper 1 Farbe B", Color) = (0.45, 0.5, 0.62, 1)
        _P1Rim ("Körper 1 Saum", Color) = (0.6, 0.8, 1.0, 1)
        _P1Params ("Körper 1 (Bänder, Seed, Sichtbarkeit, Saumstärke)", Vector) = (0.3, 1.7, 1, 1)
        _P2Dir ("Himmelskörper 2 (Richtung, Radius)", Vector) = (-0.5, 0.25, 0.6, 0.06)
        _P2ColorA ("Körper 2 Farbe A", Color) = (0.8, 0.6, 0.5, 1)
        _P2ColorB ("Körper 2 Farbe B", Color) = (0.5, 0.35, 0.3, 1)
        _P2Rim ("Körper 2 Saum", Color) = (1.0, 0.8, 0.6, 1)
        _P2Params ("Körper 2 (Bänder, Seed, Sichtbarkeit, Saumstärke)", Vector) = (0.0, 5.3, 1, 0.6)
        _AuroraA ("Polarlicht A", Color) = (0.2, 1.0, 0.6, 1)
        _AuroraB ("Polarlicht B", Color) = (0.6, 0.3, 1.0, 1)
        _AuroraStrength ("Polarlicht", Float) = 0.0
        _Exposure ("Belichtung", Float) = 1.0
        _SkyTime ("Zeit", Float) = 0.0
        _Detail ("Detailstufe (0 niedrig, 1 hoch)", Float) = 1.0
        _BankStrength ("Wolkenbank am Horizont", Float) = 0.8
        _BankHeight ("Höhe der Wolkentürme", Float) = 1.0
        _SunGlow ("Sonnenhof (Mie)", Float) = 1.0
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            float4 _ZenithColor, _HorizonColor, _HazeColor, _GroundColor;
            float _HazeStrength;
            float4 _SunDir, _SunColor;
            float _SunSize, _SunIntensity;
            float4 _CloudLight, _CloudShadow;
            float _CloudCover, _CloudDensity, _CloudScale, _CloudSpeed;
            float4 _CloudWind;
            float4 _NebulaA, _NebulaB;
            float _NebulaStrength, _StarStrength;
            float4 _P1Dir, _P1ColorA, _P1ColorB, _P1Rim, _P1Params;
            float4 _P2Dir, _P2ColorA, _P2ColorB, _P2Rim, _P2Params;
            float4 _AuroraA, _AuroraB;
            float _AuroraStrength, _Exposure, _SkyTime, _Detail;
            float _BankStrength, _BankHeight, _SunGlow;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 dir : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            float hash13(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.zyx + 31.32);
                return frac((p.x + p.y) * p.z);
            }

            float noise3(float3 x)
            {
                float3 i = floor(x);
                float3 f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                float n000 = hash13(i);
                float n100 = hash13(i + float3(1, 0, 0));
                float n010 = hash13(i + float3(0, 1, 0));
                float n110 = hash13(i + float3(1, 1, 0));
                float n001 = hash13(i + float3(0, 0, 1));
                float n101 = hash13(i + float3(1, 0, 1));
                float n011 = hash13(i + float3(0, 1, 1));
                float n111 = hash13(i + float3(1, 1, 1));
                float a = lerp(lerp(n000, n100, f.x), lerp(n010, n110, f.x), f.y);
                float b = lerp(lerp(n001, n101, f.x), lerp(n011, n111, f.x), f.y);
                return lerp(a, b, f.z);
            }

            float fbm5(float3 p)
            {
                float v = 0.0;
                float a = 0.5;
                for (int k = 0; k < 5; k++)
                {
                    v += a * noise3(p);
                    p = p * 2.03 + float3(1.7, 9.2, 3.1);
                    a *= 0.5;
                }
                return v;
            }

            float fbm3(float3 p)
            {
                float v = 0.0;
                float a = 0.5;
                for (int k = 0; k < 3; k++)
                {
                    v += a * noise3(p);
                    p = p * 2.07 + float3(5.1, 1.3, 7.7);
                    a *= 0.5;
                }
                return v;
            }

            // Bauschige Wolkenmassen: großräumige Verteilung + verwirbeltes Detail + „Billow“-Ränder
            float cloudField(float3 p)
            {
                float q = fbm3(p * 0.45);
                float c = fbm5(p * 1.1 + float3(q * 2.2, q * 1.4, q));
                float billow = 1.0 - abs(noise3(p * 2.6) * 2.0 - 1.0);
                return c * 0.8 + billow * 0.25 + q * 0.2;
            }

            float cloudFieldLow(float3 p)
            {
                float q = noise3(p * 0.45);
                return fbm3(p * 1.1 + q) * 0.9 + q * 0.3;
            }

            // Wolkenbank am Horizont: aufgetürmte Kumulusmassen rund um den Horizont (auch bei flachem Blick sichtbar).
            // Rückgabe: x Dichte, y Licht (0 Unterseite/abgewandt … 1 beleuchtete Kuppe)
            float2 cloudBank(float3 d, float t)
            {
                float h = d.y;
                float2 az = normalize(d.xz + 0.0001);
                float2 drift = _CloudWind.xz * t * _CloudSpeed * 0.15;
                float top = 0.05 + 0.2 * _BankHeight * saturate(fbm3(float3(az.x * 2.3 + drift.x, 0.7, az.y * 2.3 + drift.y)) * 2.0 - 0.45);
                float hn = saturate(h / top);
                float3 p = float3(az.x * 5.0 + drift.x, h * 16.0, az.y * 5.0 + drift.y) * _CloudScale;
                float n = _Detail > 0.5 ? fbm5(p) : fbm3(p);
                float shape = n + (1.0 - hn) * 0.42 - hn * hn * 0.25;
                float dens = saturate((shape - 0.62) * 5.0) * saturate(1.0 - hn * 0.98) * saturate(h * 60.0 + 1.0);
                float n2 = _Detail > 0.5 ? fbm3(p + float3(0.0, 0.9, 0.0)) : n * 0.9;
                float lightAmt = saturate(0.35 + (n - n2) * 2.5 + hn * 0.55);
                return float2(dens * _BankStrength, lightAmt);
            }

            // Himmelskörper mit Tag-/Nachtseite, Oberflächenmuster und Atmosphärensaum
            void body(float3 d, float4 dirRad, float4 colA, float4 colB, float4 rim, float4 prm, float3 sunDir, inout float3 col)
            {
                float3 pdir = normalize(dirRad.xyz);
                float radius = max(dirRad.w, 0.001);
                float vis = prm.z;
                if (vis <= 0.001) return;
                float dd = clamp(dot(d, pdir), -1.0, 1.0);
                float ang = acos(dd);
                // Leuchtender Saum außerhalb der Scheibe
                float outer = saturate(1.0 - (ang - radius) / (radius * 0.35));
                if (ang > radius) col += rim.rgb * outer * outer * 0.45 * prm.w * vis;
                if (ang > radius) return;

                float3 upv = abs(pdir.y) < 0.98 ? float3(0, 1, 0) : float3(1, 0, 0);
                float3 right = normalize(cross(upv, pdir));
                float3 up2 = cross(pdir, right);
                float s = sin(radius);
                float2 uv = float2(dot(d, right), dot(d, up2)) / s;
                float r2 = saturate(dot(uv, uv));
                float z = sqrt(1.0 - r2);
                float3 n = normalize(right * uv.x + up2 * uv.y - pdir * z);

                float pat = fbm5(n * 3.2 + prm.y);
                float bands = sin(n.y * 18.0 + pat * 5.0) * 0.5 + 0.5;
                float3 surf = lerp(colA.rgb, colB.rgb, saturate(pat * 1.3 - 0.15));
                surf = lerp(surf, colB.rgb * 0.8, bands * prm.x);
                float crater = smoothstep(0.62, 0.7, fbm3(n * 9.0 + prm.y * 3.0));
                surf *= 1.0 - crater * 0.25;

                float light = saturate(dot(n, sunDir) * 1.1 + 0.05);
                float3 lit = surf * (light * 1.05 + 0.04);
                // Atmosphäre am Rand der Scheibe
                float limb = pow(1.0 - z, 2.5);
                lit += rim.rgb * limb * prm.w * (0.35 + light * 0.9);
                // Sichtbarkeit am Tag durch den Himmel hindurch (Streulicht)
                float edge = smoothstep(1.0, 0.96, sqrt(r2));
                float3 mixed = lit + col * (1.0 - vis) * 0.9;
                col = lerp(col, mixed, edge * saturate(vis + 0.25));
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float h = d.y;
                float t = _SkyTime;
                float3 sunDir = normalize(_SunDir.xyz);

                // Grundverlauf
                float up = saturate(h);
                float3 col = lerp(_HorizonColor.rgb, _ZenithColor.rgb, pow(up, 0.5));
                if (h < 0.0) col = lerp(_HorizonColor.rgb, _GroundColor.rgb, saturate(-h * 5.0));
                // Streulicht: der Himmel wird zur Sonne hin heller und wärmer (breiter Mie-Hof, am Horizont stärker)
                float sdot = dot(d, sunDir);
                float mie = pow(saturate(sdot), 5.0) * (0.35 + 0.65 * (1.0 - up)) + pow(saturate(sdot), 40.0) * 0.6;
                col += _SunColor.rgb * mie * 0.35 * _SunGlow;

                // Nebelschleier (weiträumig, farbig)
                float neb = fbm5(d * 2.3 + float3(0.0, t * 0.003, 0.0));
                float neb2 = fbm3(d * 5.1 + neb * 1.8);
                float3 nebCol = lerp(_NebulaB.rgb, _NebulaA.rgb, saturate(neb2 * 1.6 - 0.3));
                col += nebCol * saturate(neb * 1.8 - 0.55) * _NebulaStrength * saturate(h * 3.0 + 0.2);

                // Sterne
                if (_StarStrength > 0.001 && h > -0.05)
                {
                    float3 sp = d * 220.0;
                    float3 cell = floor(sp);
                    float rnd = hash13(cell);
                    float3 f = frac(sp) - 0.5;
                    float star = step(0.9965, rnd) * saturate(1.0 - length(f) * 3.2);
                    float tw = 0.6 + 0.4 * sin(t * 3.0 + rnd * 80.0);
                    float3 sc = lerp(float3(0.75, 0.85, 1.0), float3(1.0, 0.85, 0.7), hash13(cell + 7.0));
                    col += sc * star * tw * 2.2 * _StarStrength * saturate(h * 6.0 + 0.3);
                    // feine Sternenstaub-Schicht
                    float dust = pow(saturate(fbm3(d * 14.0) * 1.35 - 0.4), 3.0);
                    col += float3(0.6, 0.65, 0.9) * dust * 0.25 * _StarStrength;
                }

                // Polarlicht
                if (_AuroraStrength > 0.001 && h > 0.0)
                {
                    float wave = fbm3(float3(d.x * 2.2, d.z * 2.2, t * 0.04)) * 3.0;
                    float center = 0.28 + 0.08 * sin(d.x * 3.0 + wave + t * 0.15);
                    float below = exp(-pow((h - center) * 16.0, 2.0));
                    float above = exp(-pow((h - center) * 3.5, 2.0));
                    float curtain = h < center ? below : above;
                    float rays = pow(0.5 + 0.5 * sin(d.x * 70.0 + d.z * 31.0 + wave * 7.0 + t * 0.6), 3.0);
                    rays = rays * 0.75 + 0.25 * fbm3(float3(d.x * 20.0, h * 3.0, t * 0.1));
                    float3 ac = lerp(_AuroraA.rgb, _AuroraB.rgb, saturate((h - center) * 4.0));
                    col += ac * curtain * rays * _AuroraStrength * 0.9;
                }

                // Himmelskörper (hinter den Wolken)
                body(d, _P1Dir, _P1ColorA, _P1ColorB, _P1Rim, _P1Params, sunDir, col);
                body(d, _P2Dir, _P2ColorA, _P2ColorB, _P2Rim, _P2Params, sunDir, col);

                // Sonne mit Glühen
                float sd = dot(d, sunDir);
                float cosSun = cos(_SunSize);
                float disc = smoothstep(cosSun, cosSun + (1.0 - cosSun) * 0.25, sd);
                float glow = pow(saturate(sd), 10.0) * 0.45 + pow(saturate(sd), 90.0) * 0.8;
                col += _SunColor.rgb * (glow + disc * _SunIntensity) * step(-0.02, h + 0.02);

                // Wolkenschicht auf einer gekrümmten Schale (kein Verschmieren am Horizont), windgetrieben,
                // mit einfacher Selbstverschattung zur Sonne hin (volumetrischer Eindruck)
                if (h > -0.02)
                {
                    const float R = 8.0;
                    const float Hc = 1.0;
                    float hh = max(h, 0.0);
                    float tt = -R * hh + sqrt(R * R * hh * hh + 2.0 * R * Hc + Hc * Hc);
                    float3 p = d * tt * _CloudScale;
                    float2 wdir = normalize(_CloudWind.xz + 0.0001);
                    p.xz += wdir * t * _CloudSpeed;
                    p.y += t * 0.004;
                    float c;
                    if (_Detail > 0.5) c = cloudField(p); else c = cloudFieldLow(p);
                    float cover = saturate(_CloudCover);
                    float dens = saturate((c - (1.0 - cover) * 0.85) * _CloudDensity);
                    dens *= saturate(h * 14.0 + 0.15);
                    float3 toSun = normalize(float3(sunDir.x, 0.0, sunDir.z) + 0.0001);
                    float cs = c;
                    if (_Detail > 0.5) cs = cloudField(p + toSun * 0.35);
                    float lightAmt = saturate(0.5 + (c - cs) * 4.0);
                    float towardSun = pow(saturate(dot(d, sunDir) * 0.5 + 0.5), 3.0);
                    float shade = saturate(c * 1.3 - 0.2);
                    // dicke Wolkenkerne dunkler (Unterseite), dünne Ränder hell – kräftiger Hell-Dunkel-Kontrast wie gemalt
                    float thick = saturate((c - (1.0 - cover) * 0.85) * _CloudDensity * 0.5);
                    float3 cc = lerp(_CloudShadow.rgb, _CloudLight.rgb, saturate(lightAmt * 0.8 + shade * 0.25 - thick * 0.35 + 0.15));
                    cc += _SunColor.rgb * towardSun * 0.3 * lightAmt;
                    // Silberrand: dünne Ränder leuchten gegen die Sonne
                    cc += _SunColor.rgb * pow(saturate(1.0 - dens), 2.0) * towardSun * 0.45;
                    col = lerp(col, cc, dens * 0.96);
                }

                // Wolkenbank am Horizont (vor der Wolkenschicht, hinter dem Dunst)
                if (_BankStrength > 0.001 && h > -0.03 && h < 0.3)
                {
                    float2 bank = cloudBank(d, t);
                    float towardSunB = pow(saturate(dot(d, sunDir) * 0.5 + 0.5), 4.0);
                    float3 bc = lerp(_CloudShadow.rgb, _CloudLight.rgb, bank.y);
                    bc += _SunColor.rgb * towardSunB * (0.25 + bank.y * 0.45);
                    bc += _SunColor.rgb * pow(saturate(1.0 - bank.x), 3.0) * towardSunB * 0.6;
                    // ferne Türme verschwimmen im Horizontdunst
                    bc = lerp(bc, _HazeColor.rgb, saturate(0.45 - h * 2.0) * _HazeStrength);
                    col = lerp(col, bc, saturate(bank.x) * 0.95);
                }

                // Dunstband über dem Horizont
                float haze = exp(-abs(h) * 14.0) * _HazeStrength;
                col = lerp(col, _HazeColor.rgb, saturate(haze));

                col *= _Exposure;
                return fixed4(col, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
