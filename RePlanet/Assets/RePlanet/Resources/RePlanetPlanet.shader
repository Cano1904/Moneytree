// RE:PLANET – Planetenkugeln der Planetenwahl (Weltall-Szene, Runtime/Render/PlanetSelectScene.cs).
// Eigenes, unbeleuchtetes Rechnen (unabhängig von Unitys Lichtern und Umgebung): Tag-/Nachtseite mit weichem
// Übergang (umwickeltes Lambert), warmes Streulicht an der Tag-Nacht-Grenze, Glanz auf Ozeanen, Atmosphärenschimmer am
// Scheibenrand (Fresnel, zur Sonne hin heller), Nachtseite in kühlem Streulicht statt Schwarz.
// Pass „Oberfläche“ auf der Planetenkugel; Shader „RePlanet/PlanetAtmosphere“ (unten) auf einer etwas größeren Kugel
// zeichnet den Lichtsaum außerhalb der Scheibe (Dichte aus dem Abstand des Sichtstrahls zum Mittelpunkt).
// Alle Ausgaben sind begrenzt (keine NaN/Unendlich-Werte).
Shader "RePlanet/Planet"
{
    Properties
    {
        _MainTex ("Oberfläche", 2D) = "gray" {}
        _Color ("Tönung", Color) = (1, 1, 1, 1)
        _SunDirW ("Richtung zur Sonne (Welt)", Vector) = (0, 0.3, -1, 0)
        _SunColor ("Sonnenlicht", Color) = (1.4, 1.3, 1.15, 1)
        _NightColor ("Nachtseite (Streulicht)", Color) = (0.05, 0.06, 0.1, 1)
        _AtmoColor ("Atmosphäre", Color) = (0.4, 0.7, 1.0, 1)
        _Params ("x Atmosphäre, y Glanz (Ozean), z Terminator-Wärme, w Sättigung", Vector) = (1, 0.4, 1, 1.15)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color, _SunDirW, _SunColor, _NightColor, _AtmoColor, _Params;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 wn : TEXCOORD1; float3 wp : TEXCOORD2; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.wn = UnityObjectToWorldNormal(v.normal);
                o.wp = mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1.0)).xyz;
                return o;
            }

            float Lum(float3 c) { return dot(c, float3(0.2126, 0.7152, 0.0722)); }

            float4 frag(v2f i) : SV_Target
            {
                float3 N = i.wn * rsqrt(max(dot(i.wn, i.wn), 1e-8));
                float3 V = _WorldSpaceCameraPos - i.wp;
                V *= rsqrt(max(dot(V, V), 1e-8));
                float3 L = _SunDirW.xyz * rsqrt(max(dot(_SunDirW.xyz, _SunDirW.xyz), 1e-8));

                float3 alb = tex2D(_MainTex, i.uv).rgb * _Color.rgb;
                // Sättigung leicht anheben (kräftige Farben aus dem All)
                alb = max(lerp(Lum(alb).xxx, alb, _Params.w), 0.0);

                float ndl = dot(N, L);
                // weicher Übergang: umwickeltes Lambert + Tagesmaske
                float wrap = saturate((ndl + 0.18) / 1.18);
                float day = smoothstep(-0.12, 0.25, ndl);
                float3 col = alb * _SunColor.rgb * wrap * (0.3 + 0.7 * day);
                // Nachtseite: kühles Streulicht (nie ganz schwarz)
                col += alb * _NightColor.rgb * (1.0 - day * 0.7) + _NightColor.rgb * 0.15;
                // Terminator: warmes, rötliches Streulicht im Übergang
                float term = saturate(1.0 - abs(ndl + 0.02) / 0.22);
                col += alb * float3(1.0, 0.45, 0.2) * term * term * 0.9 * _Params.z;
                // Glanz auf dunklen, blauen Flächen (Ozeane)
                float water = saturate((alb.b - max(alb.r, alb.g * 0.9)) * 6.0) * _Params.y;
                float3 H = normalize(L + V);
                float spec = pow(saturate(dot(N, H)), 60.0) * day;
                col += _SunColor.rgb * spec * water * 0.8;
                // Atmosphärenschimmer am Rand der Scheibe, zur Sonne hin hell, auf der Nachtseite als dünner Saum
                float fres = 1.0 - saturate(dot(N, V));
                float rim = fres * fres * fres;
                float litRim = saturate(ndl * 0.6 + 0.45);
                col += _AtmoColor.rgb * rim * (0.15 + 1.4 * litRim) * _Params.x;
                // leichter Dunst über der ganzen Tagseite (Blau-/Planetenfarbe)
                col = lerp(col, col + _AtmoColor.rgb * 0.12 * day, 0.6 * _Params.x);
                return float4(clamp(col, 0.0, 32.0), 1.0);
            }
            ENDCG
        }
    }
    Fallback "Unlit/Texture"
}
