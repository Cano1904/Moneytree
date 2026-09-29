// RE:PLANET – eigene Nachbearbeitung für die Built-in Render Pipeline (ohne Pakete), gesteuert von Runtime/Render/PostFX.cs.
// Pässe: 0 Bloom-Vorfilter (mit Luftperspektive), 1 Bloom-Verkleinern (Dual-Filter), 2 Bloom-Vergrößern (Zelt + Stufe),
//        3 Sonnenstrahlen-Maske, 4 Sonnenstrahlen-Radialunschärfe, 5 Zusammensetzen (Umgebungsverdeckung, Konturen, Nebel,
//        Bloom, Strahlen, Belichtung, Kontrast, ACES-Tonemapping, Split-Toning, Sättigung/Vibrance, Vignette, Korn,
//        chromatische Aberration), 6 Umgebungsverdeckung (SSAO-Näherung, halbe Auflösung).
// Koordinaten: „uv“ = Quelltextur, „uvd“ = Bildschirm/Tiefe (auf D3D mit Kantenglättung ist die Quelle gespiegelt).
// Alle HDR-Eingaben werden begrenzt (NaN → 0, Unendlich → 64), damit ein fehlerhaftes Material nie das ganze Bild
// über den Bloom überstrahlt.
Shader "Hidden/RePlanet/PostFX"
{
    Properties
    {
        _MainTex ("Quelle", 2D) = "white" {}
        _BloomTex ("Bloom", 2D) = "black" {}
        _ShaftTex ("Sonnenstrahlen", 2D) = "black" {}
        _AOTex ("Umgebungsverdeckung", 2D) = "white" {}
        _SkyCube ("Himmel (Würfel)", Cube) = "" {}
    }

    CGINCLUDE
    #include "UnityCG.cginc"

    sampler2D _MainTex;
    float4 _MainTex_TexelSize;
    sampler2D _BloomTex;
    sampler2D _ShaftTex;
    sampler2D _AOTex;
    float4 _AOTex_TexelSize;
    UNITY_DECLARE_TEXCUBE(_SkyCube);
    UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
    sampler2D _CameraDepthNormalsTexture;

    float4 _RayBL, _RayBR, _RayTL, _RayTR;   // Sichtstrahlen der Bildecken (Weltraum, Sichttiefe 1)
    float4 _FogColor;                         // rgb Nebelfarbe, a = maximale Deckkraft
    float4 _FogParams;                        // x Dichte, y Höhenabfall, z Bezugshöhe, w an/aus
    float4 _FogParams2;                       // x Himmelsanteil, y Würfel-Mip, z linearer Anteil, w Tiefe verfügbar
    float4 _SunDirW;                          // Richtung zur Sonne (Welt)
    float4 _SunColor;                         // rgb Farbe, a Streulicht im Nebel
    float4 _SunScreen;                        // xy Bildschirmpunkt der Sonne, z Stärke, w Seitenverhältnis
    float4 _ShaftParams;                      // x Schrittweite, y Schwelle (unbelichtet), z Abklingen, w Radius
    float4 _ShaftColor;
    float4 _Bloom;                            // x Schwelle, y weiches Knie, z Stärke, w Obergrenze
    float4 _Grade;                            // x Belichtung, y Kontrast, z Sättigung, w Vibrance
    float4 _ShadowTint, _HighlightTint;       // rgb Tönung (a = Stärke)
    float4 _Fx;                               // x Vignette, y Korn, z chromatische Aberration, w Zeit
    float4 _VignetteColor;
    float4 _ViewInfo;                         // x tan(halbes Sichtfeld) · Seitenverhältnis, y tan(halbes Sichtfeld), z Normalen-Textur da (0/1)
    float4 _EdgeParams;                       // x Stärke (0 = aus), y Pixelschritt, z Ausblenden ab (m), w ausgeblendet bei (m)
    float4 _EdgeColor;                        // rgb Tönung der Linien
    float4 _AOParams;                         // x Radius (m), y Stärke, z Abtastungen, w Reichweite (m)
    float4 _AOParams2;                        // x an (0/1)

    struct v2f
    {
        float4 pos : SV_POSITION;
        float2 uv : TEXCOORD0;
        float2 uvd : TEXCOORD1;
        float3 ray : TEXCOORD2;
    };

    float2 FlipUV(float2 uv)
    {
    #if UNITY_UV_STARTS_AT_TOP
        if (_MainTex_TexelSize.y < 0) uv.y = 1.0 - uv.y;
    #endif
        return uv;
    }

    v2f vert(appdata_img v)
    {
        v2f o;
        o.pos = UnityObjectToClipPos(v.vertex);
        o.uv = v.texcoord.xy;
        o.uvd = FlipUV(v.texcoord.xy);
        float3 b = lerp(_RayBL.xyz, _RayBR.xyz, o.uvd.x);
        float3 t = lerp(_RayTL.xyz, _RayTR.xyz, o.uvd.x);
        o.ray = lerp(b, t, o.uvd.y);
        return o;
    }

    float Lum(float3 c) { return dot(c, float3(0.2126, 0.7152, 0.0722)); }

    // HDR-Wert begrenzen: negative/NaN → 0 (max folgt IEEE maxNum), Unendlich → 64
    float3 SafeHDR(float3 c) { return min(max(c, 0.0), 64.0); }

    float Hash12(float2 p)
    {
        float3 p3 = frac(float3(p.xyx) * 0.1031);
        p3 += dot(p3, p3.yzx + 33.33);
        return frac((p3.x + p3.y) * p3.z);
    }

    // ------------------------------------------------------------ Tiefe / Sichtraum
    float EyeDepthAt(float2 uvd) { return LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uvd)); }

    // Sichtraum-Position: x rechts, y oben, z = Sichttiefe (vorwärts)
    float3 ViewPos(float2 uvd, float eye) { return float3((uvd * 2.0 - 1.0) * _ViewInfo.xy * eye, eye); }

    // Sichtraum-Normale aus der Tiefen-/Normalen-Textur (Unity: Kamera blickt nach −z) → vorwärts-z wie ViewPos
    float3 ViewNormal(float2 uvd)
    {
        float d; float3 n;
        DecodeDepthNormal(tex2D(_CameraDepthNormalsTexture, uvd), d, n);
        return normalize(float3(n.x, n.y, -n.z) + float3(0.0, 0.0, -1e-4));
    }

    // Luftperspektive: Höhennebel in Himmelsfarbe (Farbe aus der Himmels-Reflexionssonde in Blickrichtung),
    // mit Streulicht zur Sonne hin. Der Himmel selbst (Tiefe = fern) bleibt unberührt – er hat sein eigenes Dunstband.
    float3 ApplyFog(float3 col, float2 uvd, float3 ray)
    {
        if (_FogParams.w < 0.5 || _FogParams2.w < 0.5) return col;
        float raw = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uvd);
        float lin01 = Linear01Depth(raw);
        if (lin01 > 0.9999) return col;
        float eye = LinearEyeDepth(raw);
        float3 wv = ray * eye;
        float dist = length(wv);
        float3 dir = wv / max(dist, 0.0001);
        float k = _FogParams.y;
        float h0 = _WorldSpaceCameraPos.y - _FogParams.z;
        float kdy = k * wv.y;
        float along = abs(kdy) > 0.001 ? (1.0 - exp(-kdy)) / kdy : 1.0;
        float hf = clamp(exp(-k * h0) * along, 0.0, 3.0);
        float dd = _FogParams.x * dist;
        float optical = (dd * dd + dd * _FogParams2.z) * hf;
        float f = saturate(1.0 - exp(-optical)) * _FogColor.a;
        float3 sd = normalize(float3(dir.x, max(dir.y, 0.04), dir.z));
        float3 skyCol = SafeHDR(UNITY_SAMPLE_TEXCUBE_LOD(_SkyCube, sd, _FogParams2.y).rgb);
        // etwas dunkler als der Himmel dahinter: ferne Silhouetten bleiben erkennbar
        float3 fogCol = lerp(_FogColor.rgb, skyCol * 0.9, _FogParams2.x);
        float sun = pow(saturate(dot(dir, _SunDirW.xyz)), 6.0);
        fogCol += _SunColor.rgb * sun * _SunColor.a;
        return lerp(col, fogCol, f);
    }

    // ------------------------------------------------------------ Konturen
    // Feine, dunkel getönte Linien an Tiefensprüngen (nur auf der vorderen Seite, 1 Pixel) und an Knicken der
    // Oberfläche (Normalen, einseitig). Planare Flächen erzeugen keine Linien: verglichen wird 1/Tiefe, das auf
    // Ebenen im Bildschirm linear verläuft. Blendet mit der Entfernung aus, nie auf dem Himmel.
    float EdgeFactor(float2 uvd, float eyeC)
    {
        float2 t = _MainTex_TexelSize.xy * _EdgeParams.y;
        t = abs(t);
        float iC = 1.0 / eyeC;
        float iL = 1.0 / EyeDepthAt(uvd - float2(t.x, 0.0));
        float iR = 1.0 / EyeDepthAt(uvd + float2(t.x, 0.0));
        float iD = 1.0 / EyeDepthAt(uvd - float2(0.0, t.y));
        float iU = 1.0 / EyeDepthAt(uvd + float2(0.0, t.y));
        // vordere Seite: Mitte näher als der Durchschnitt der Nachbarn → 1/Tiefe größer
        float lap = max(0.0, (2.0 * iC - iL - iR)) + max(0.0, (2.0 * iC - iD - iU));
        float depthEdge = smoothstep(0.06, 0.22, lap / iC);
        float normalEdge = 0.0;
        if (_ViewInfo.z > 0.5)
        {
            float3 nC = ViewNormal(uvd);
            float3 nR = ViewNormal(uvd + float2(t.x, 0.0));
            float3 nU = ViewNormal(uvd + float2(0.0, t.y));
            float crease = 1.0 - min(dot(nC, nR), dot(nC, nU));
            normalEdge = smoothstep(0.28, 0.6, crease);
        }
        float fade = 1.0 - smoothstep(_EdgeParams.z, _EdgeParams.w, eyeC);
        return saturate(max(depthEdge, normalEdge * 0.8)) * fade;
    }

    // ------------------------------------------------------------ Bloom
    float3 SoftThreshold(float3 c)
    {
        float br = max(c.r, max(c.g, c.b));
        float knee = max(_Bloom.y, 0.0001);
        float soft = clamp(br - _Bloom.x + knee, 0.0, 2.0 * knee);
        soft = soft * soft / (4.0 * knee + 0.00001);
        float contrib = max(soft, br - _Bloom.x) / max(br, 0.00001);
        return min(c * contrib, _Bloom.w);
    }

    float4 fragPrefilter(v2f i) : SV_Target
    {
        float2 d = _MainTex_TexelSize.xy * 0.5;
        float3 a = SafeHDR(tex2D(_MainTex, i.uv + float2(-d.x, -d.y)).rgb);
        float3 b = SafeHDR(tex2D(_MainTex, i.uv + float2( d.x, -d.y)).rgb);
        float3 c = SafeHDR(tex2D(_MainTex, i.uv + float2(-d.x,  d.y)).rgb);
        float3 e = SafeHDR(tex2D(_MainTex, i.uv + float2( d.x,  d.y)).rgb);
        // Karis-Mittel: einzelne sehr helle Pixel flackern nicht
        float wa = 1.0 / (1.0 + Lum(a)), wb = 1.0 / (1.0 + Lum(b)), wc = 1.0 / (1.0 + Lum(c)), we = 1.0 / (1.0 + Lum(e));
        float3 col = (a * wa + b * wb + c * wc + e * we) / (wa + wb + wc + we);
        col = ApplyFog(col, i.uvd, i.ray);
        return float4(SoftThreshold(col * _Grade.x), 1.0);
    }

    float4 fragDown(v2f i) : SV_Target
    {
        float2 hp = _MainTex_TexelSize.xy * 0.5;
        float3 s = tex2D(_MainTex, i.uv).rgb * 4.0;
        s += tex2D(_MainTex, i.uv - hp).rgb;
        s += tex2D(_MainTex, i.uv + hp).rgb;
        s += tex2D(_MainTex, i.uv + float2(hp.x, -hp.y)).rgb;
        s += tex2D(_MainTex, i.uv - float2(hp.x, -hp.y)).rgb;
        return float4(s * 0.125, 1.0);
    }

    float4 fragUp(v2f i) : SV_Target
    {
        float2 hp = _MainTex_TexelSize.xy * 0.5;
        float3 s = tex2D(_MainTex, i.uv + float2(-hp.x * 2.0, 0.0)).rgb;
        s += tex2D(_MainTex, i.uv + float2(-hp.x, hp.y)).rgb * 2.0;
        s += tex2D(_MainTex, i.uv + float2(0.0, hp.y * 2.0)).rgb;
        s += tex2D(_MainTex, i.uv + float2(hp.x, hp.y)).rgb * 2.0;
        s += tex2D(_MainTex, i.uv + float2(hp.x * 2.0, 0.0)).rgb;
        s += tex2D(_MainTex, i.uv + float2(hp.x, -hp.y)).rgb * 2.0;
        s += tex2D(_MainTex, i.uv + float2(0.0, -hp.y * 2.0)).rgb;
        s += tex2D(_MainTex, i.uv + float2(-hp.x, -hp.y)).rgb * 2.0;
        float3 up = s / 12.0;
        return float4(up + tex2D(_BloomTex, i.uv).rgb, 1.0);
    }

    // ------------------------------------------------------------ Sonnenstrahlen
    // Maske im Bildschirmraum: nur Himmelspixel, hell und nahe am Sonnenpunkt
    float4 fragShaftMask(v2f i) : SV_Target
    {
        float raw = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv);
        float sky = step(0.9995, Linear01Depth(raw));
        if (_FogParams2.w < 0.5) sky = 1.0;
        float3 src = min(SafeHDR(tex2D(_MainTex, i.uvd).rgb), 4.0);
        float br = max(Lum(src) - _ShaftParams.y, 0.0);
        float2 dv = (i.uv - _SunScreen.xy) * float2(_SunScreen.w, 1.0);
        float fall = saturate(1.0 - length(dv) / _ShaftParams.w);
        fall *= fall;
        return float4(src * (br / max(Lum(src), 0.001)) * sky * fall, 1.0);
    }

    float4 fragShaftBlur(v2f i) : SV_Target
    {
        float2 uv = i.uv;
        float2 stepv = (_SunScreen.xy - uv) * _ShaftParams.x;
        float3 acc = 0;
        float w = 1.0, wsum = 0.0;
        for (int k = 0; k < 10; k++)
        {
            acc += tex2D(_MainTex, uv).rgb * w;
            wsum += w;
            w *= _ShaftParams.z;
            uv += stepv;
        }
        return float4(acc / wsum, 1.0);
    }

    // ------------------------------------------------------------ Umgebungsverdeckung (halbe Auflösung)
    // Alchemy-artige Näherung: Abtastpunkte in einer Scheibe um das Pixel (Radius in Metern, im Bild mit der Tiefe
    // skaliert), je Punkt die Verdeckung max(0, v·n − Versatz) / (v·v + ε). Gedreht je Pixel (goldener Winkel),
    // die leichte Körnung glättet das bilineare Hochskalieren im Zusammensetzen.
    float4 fragAO(v2f i) : SV_Target
    {
        float raw = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uvd);
        if (Linear01Depth(raw) > 0.999) return 1.0;
        float eye = LinearEyeDepth(raw);
        if (eye > _AOParams.w) return 1.0;
        float3 P = ViewPos(i.uvd, eye);
        float3 N = ViewNormal(i.uvd);
        float radius = _AOParams.x;
        float2 rUV = radius / (2.0 * eye * max(_ViewInfo.xy, 1e-4));
        // sehr nahe: Radius auf ein Achtel des Bildes begrenzen (Kosten, Ausreißer)
        rUV = min(rUV, float2(0.125, 0.125));
        float rot = Hash12(i.uvd * _ScreenParams.xy) * 6.2831853;
        float occ = 0.0;
        float n = max(_AOParams.z, 1.0);
        float r2 = radius * radius;
        [loop]
        for (float k = 0.0; k < n; k += 1.0)
        {
            float a = rot + k * 2.3999632;
            float s = sqrt((k + 0.5) / n);
            float2 uv2 = i.uvd + float2(cos(a), sin(a)) * rUV * s;
            float e2 = EyeDepthAt(uv2);
            float3 v = ViewPos(uv2, e2) - P;
            float vv = dot(v, v);
            float term = max(0.0, dot(v, N) - 0.015 * eye) / (vv + 0.01 * r2 + 1e-4);
            occ += term * saturate(1.0 - vv / (r2 * 4.0));
        }
        occ = occ / n * radius;
        float ao = saturate(1.0 - occ * _AOParams.y);
        // mit der Entfernung ausblenden
        ao = lerp(ao, 1.0, smoothstep(_AOParams.w * 0.6, _AOParams.w, eye));
        return float4(ao, ao, ao, 1.0);
    }

    // ------------------------------------------------------------ Zusammensetzen
    float3 ACESFilm(float3 x)
    {
        // Annäherung nach Narkowicz (ACES RRT+ODT), Eingabe linear, Ausgabe 0..1 linear
        return saturate((x * (2.51 * x + 0.03)) / (x * (2.43 * x + 0.59) + 0.14));
    }

    float4 fragComposite(v2f i) : SV_Target
    {
        float2 c = i.uvd - 0.5;
        float r2 = dot(c, c);
        float3 col;
        if (_Fx.z > 0.0001)
        {
            // Chromatische Aberration: nur zum Rand hin
            float2 off = (i.uv - 0.5) * r2 * _Fx.z;
            col.r = tex2D(_MainTex, i.uv - off).r;
            col.g = tex2D(_MainTex, i.uv).g;
            col.b = tex2D(_MainTex, i.uv + off).b;
        }
        else col = tex2D(_MainTex, i.uv).rgb;
        col = SafeHDR(col);

        // Umgebungsverdeckung und Konturen (vor dem Nebel: in der Ferne verschwinden sie im Dunst)
        if (_FogParams2.w > 0.5 && (_EdgeParams.x > 0.001 || _AOParams2.x > 0.5))
        {
            float raw = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uvd);
            if (Linear01Depth(raw) < 0.999)
            {
                float eyeC = LinearEyeDepth(raw);
                if (_AOParams2.x > 0.5)
                {
                    float2 h = _AOTex_TexelSize.xy * 0.5;
                    float ao = (tex2D(_AOTex, i.uv + float2(-h.x, -h.y)).r + tex2D(_AOTex, i.uv + float2(h.x, -h.y)).r +
                                tex2D(_AOTex, i.uv + float2(-h.x, h.y)).r + tex2D(_AOTex, i.uv + float2(h.x, h.y)).r) * 0.25;
                    col *= ao;
                }
                if (_EdgeParams.x > 0.001)
                {
                    float e = EdgeFactor(i.uvd, eyeC) * _EdgeParams.x;
                    col = lerp(col, col * _EdgeColor.rgb, e);
                }
            }
        }

        col = ApplyFog(col, i.uvd, i.ray);
        col *= _Grade.x;
        col += tex2D(_BloomTex, i.uv).rgb * _Bloom.z;
        col += tex2D(_ShaftTex, i.uvd).rgb * _ShaftColor.rgb * _SunScreen.z * _Grade.x;

        // Kontrast im logarithmischen Raum um Mittelgrau
        col = max(col, 0.0);
        col = 0.18 * pow(col / 0.18 + 0.00001, _Grade.y);

        col = ACESFilm(col);

        // Split-Toning: Schatten und Lichter getrennt tönen (Planetenpalette)
        float l = Lum(col);
        float hl = smoothstep(0.08, 0.75, l);
        float3 tS = lerp(float3(1, 1, 1), _ShadowTint.rgb / max(Lum(_ShadowTint.rgb), 0.01), _ShadowTint.a);
        float3 tH = lerp(float3(1, 1, 1), _HighlightTint.rgb / max(Lum(_HighlightTint.rgb), 0.01), _HighlightTint.a);
        col *= lerp(tS, tH, hl);

        // Sättigung + Vibrance (schwach gesättigte Farben stärker anheben)
        l = Lum(col);
        float mx = max(col.r, max(col.g, col.b));
        float mn = min(col.r, min(col.g, col.b));
        float satNow = (mx - mn) / max(mx, 0.0001);
        float s = _Grade.z * (1.0 + _Grade.w * (1.0 - satNow));
        col = max(lerp(l.xxx, col, s), 0.0);

        // Vignette (farbig abgedunkelt)
        float vig = saturate(pow(r2 * 2.0, 1.25) * _Fx.x);
        col = lerp(col, col * _VignetteColor.rgb, vig);

        // Filmkorn (luminanzabhängig) + Dithering gegen Farbstufen
        float n = Hash12(i.uvd * _ScreenParams.xy + frac(_Fx.w) * 173.0);
        col += (n - 0.5) * _Fx.y * (0.35 + sqrt(saturate(l)) * 0.65);
        col += (Hash12(i.uvd * _ScreenParams.xy + 71.3) - 0.5) / 255.0;
        return float4(saturate(col), 1.0);
    }
    ENDCG

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass // 0 Vorfilter
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragPrefilter
            #pragma target 3.0
            ENDCG
        }
        Pass // 1 Verkleinern
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragDown
            #pragma target 3.0
            ENDCG
        }
        Pass // 2 Vergrößern
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragUp
            #pragma target 3.0
            ENDCG
        }
        Pass // 3 Strahlen-Maske
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragShaftMask
            #pragma target 3.0
            ENDCG
        }
        Pass // 4 Strahlen-Unschärfe
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragShaftBlur
            #pragma target 3.0
            ENDCG
        }
        Pass // 5 Zusammensetzen
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragComposite
            #pragma target 3.0
            ENDCG
        }
        Pass // 6 Umgebungsverdeckung
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragAO
            #pragma target 3.0
            ENDCG
        }
    }
    Fallback Off
}
