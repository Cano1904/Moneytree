// RE:PLANET – eigene Nachbearbeitung für die Built-in Render Pipeline (ohne Pakete), gesteuert von Runtime/Render/PostFX.cs.
// Pässe: 0 Bloom-Vorfilter (mit Luftperspektive), 1 Bloom-Verkleinern (Dual-Filter), 2 Bloom-Vergrößern (Zelt + Stufe),
//        3 Sonnenstrahlen-Maske, 4 Sonnenstrahlen-Radialunschärfe, 5 Zusammensetzen (Nebel, Bloom, Strahlen, Belichtung,
//        Kontrast, ACES-Tonemapping, Split-Toning, Sättigung/Vibrance, Vignette, Korn, chromatische Aberration).
// Koordinaten: „uv“ = Quelltextur, „uvd“ = Bildschirm/Tiefe (auf D3D mit Kantenglättung ist die Quelle gespiegelt).
Shader "Hidden/RePlanet/PostFX"
{
    Properties
    {
        _MainTex ("Quelle", 2D) = "white" {}
        _BloomTex ("Bloom", 2D) = "black" {}
        _ShaftTex ("Sonnenstrahlen", 2D) = "black" {}
        _SkyCube ("Himmel (Würfel)", Cube) = "" {}
    }

    CGINCLUDE
    #include "UnityCG.cginc"

    sampler2D _MainTex;
    float4 _MainTex_TexelSize;
    sampler2D _BloomTex;
    sampler2D _ShaftTex;
    UNITY_DECLARE_TEXCUBE(_SkyCube);
    UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);

    float4 _RayBL, _RayBR, _RayTL, _RayTR;   // Sichtstrahlen der Bildecken (Weltraum, Sichttiefe 1)
    float4 _FogColor;                         // rgb Nebelfarbe, a = maximale Deckkraft
    float4 _FogParams;                        // x Dichte, y Höhenabfall, z Bezugshöhe, w an/aus
    float4 _FogParams2;                       // x Himmelsanteil, y Würfel-Mip, z linearer Anteil, w Tiefe verfügbar
    float4 _SunDirW;                          // Richtung zur Sonne (Welt)
    float4 _SunColor;                         // rgb Farbe, a Streulicht im Nebel
    float4 _SunScreen;                        // xy Bildschirmpunkt der Sonne, z Stärke, w Seitenverhältnis
    float4 _ShaftParams;                      // x Schrittweite, y Schwelle, z Abklingen, w Radius
    float4 _ShaftColor;
    float4 _Bloom;                            // x Schwelle, y weiches Knie, z Stärke, w Obergrenze
    float4 _Grade;                            // x Belichtung, y Kontrast, z Sättigung, w Vibrance
    float4 _ShadowTint, _HighlightTint;       // rgb Tönung (a = Stärke)
    float4 _Fx;                               // x Vignette, y Korn, z chromatische Aberration, w Zeit
    float4 _VignetteColor;

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

    float Hash12(float2 p)
    {
        float3 p3 = frac(float3(p.xyx) * 0.1031);
        p3 += dot(p3, p3.yzx + 33.33);
        return frac((p3.x + p3.y) * p3.z);
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
        float3 skyCol = UNITY_SAMPLE_TEXCUBE_LOD(_SkyCube, sd, _FogParams2.y).rgb;
        float3 fogCol = lerp(_FogColor.rgb, skyCol, _FogParams2.x);
        float sun = pow(saturate(dot(dir, _SunDirW.xyz)), 6.0);
        fogCol += _SunColor.rgb * sun * _SunColor.a;
        return lerp(col, fogCol, f);
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
        float3 a = tex2D(_MainTex, i.uv + float2(-d.x, -d.y)).rgb;
        float3 b = tex2D(_MainTex, i.uv + float2( d.x, -d.y)).rgb;
        float3 c = tex2D(_MainTex, i.uv + float2(-d.x,  d.y)).rgb;
        float3 e = tex2D(_MainTex, i.uv + float2( d.x,  d.y)).rgb;
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
        float3 src = min(tex2D(_MainTex, i.uvd).rgb, 4.0);
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

        col = ApplyFog(col, i.uvd, i.ray);
        col *= _Grade.x;
        col += tex2D(_BloomTex, i.uv).rgb * _Bloom.z;
        col += tex2D(_ShaftTex, i.uvd).rgb * _ShaftColor.rgb * _SunScreen.z;

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
    }
    Fallback Off
}
