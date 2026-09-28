// RE:PLANET – Wasser (Built-in, Forward, transparent). Gesteuert von Runtime/Render/TerrainLook.cs.
// Fresnel, Himmelsreflexion (Reflexionssonde unity_SpecCube0), Tiefenfarbe über die Tiefentextur (flach türkis → tief blau),
// Uferschaum über die Tiefendifferenz, zwei animierte Normal-Ebenen + Detailrauschen, Sonnenglanz, weicher Uferrand, Nebel.
// Ohne Tiefentextur (_RP_DepthOn = 0, niedrigste Qualitätsstufe) wird eine feste Wassertiefe angenommen.
Shader "RePlanet/Water"
{
    Properties
    {
        _ShallowColor ("Flachwasser", Color) = (0.20, 0.85, 0.80, 1)
        _DeepColor ("Tiefwasser", Color) = (0.02, 0.18, 0.38, 1)
        _FoamColor ("Schaum", Color) = (0.95, 0.98, 1.0, 1)
        _BumpMap ("Wellen-Normalen", 2D) = "bump" {}
        _NoiseTex ("Rauschen", 2D) = "gray" {}
        _WaveParams ("Wellen (Maßstab 1, Maßstab 2, Tempo, Stärke)", Vector) = (0.045, 0.11, 1.0, 0.55)
        _Clarity ("Sichttiefe (m)", Float) = 3.5
        _AlphaRange ("Deckkraft (flach, tief)", Vector) = (0.35, 0.94, 0, 0)
        _FoamParams ("Schaum (Breite m, Stärke, Maßstab, –)", Vector) = (1.2, 0.9, 0.35, 0)
        _Reflect ("Reflexion", Float) = 1.0
        _SunSpec ("Sonnenglanz", Float) = 3.0
        _Gloss ("Glanzschärfe", Float) = 600
    }

    SubShader
    {
        Tags { "Queue" = "Transparent-10" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        LOD 200

        Pass
        {
            Tags { "LightMode" = "ForwardBase" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "UnityLightingCommon.cginc"

            float4 _ShallowColor, _DeepColor, _FoamColor;
            sampler2D _BumpMap;
            sampler2D _NoiseTex;
            float4 _WaveParams, _AlphaRange, _FoamParams;
            float _Clarity, _Reflect, _SunSpec, _Gloss;
            float _RP_DepthOn;
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                UNITY_FOG_COORDS(2)
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1.0)).xyz;
                o.screenPos = ComputeScreenPos(o.pos);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            float2 SampleN(float2 uv)
            {
                return tex2D(_BumpMap, uv).rg * 2.0 - 1.0;
            }

            float4 frag(v2f i) : SV_Target
            {
                float3 toCam = _WorldSpaceCameraPos - i.worldPos;
                float dist = length(toCam);
                float3 V = toCam / max(dist, 0.0001);
                float t = _Time.y * _WaveParams.z;

                // Zwei gegenläufige Normal-Ebenen + feine dritte Ebene; in der Ferne flacher (kein Flimmern)
                float2 p = i.worldPos.xz;
                float2 n1 = SampleN(p * _WaveParams.x + float2(t * 0.021, t * 0.013));
                float2 n2 = SampleN(p * _WaveParams.y + float2(-t * 0.017, t * 0.029));
                float2 n3 = SampleN(p * _WaveParams.y * 3.1 + float2(t * 0.05, -t * 0.04));
                float strength = _WaveParams.w * lerp(1.0, 0.25, saturate(dist / 180.0));
                float2 nxy = (n1 + n2 * 0.8 + n3 * 0.35) * strength;
                float3 N = normalize(float3(nxy.x, 1.0, nxy.y));

                // Wassertiefe entlang des Sichtstrahls aus der Tiefentextur
                float surfEye = i.screenPos.w;
                float thick = 3.0;
                if (_RP_DepthOn > 0.5)
                {
                    float2 suv = i.screenPos.xy / i.screenPos.w;
                    float sceneEye = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, suv));
                    thick = max(sceneEye - surfEye, 0.0);
                }
                // senkrechte Tiefe annähern (flacher Blick → langer Weg durchs Wasser)
                float vDepth = thick * saturate(V.y + 0.15);
                float depthT = 1.0 - exp(-vDepth / max(_Clarity, 0.05));

                // Beleuchtung
                float3 L = normalize(_WorldSpaceLightPos0.xyz);
                float3 sunCol = _LightColor0.rgb;
                float3 amb = unity_AmbientSky.rgb * 0.7 + unity_AmbientEquator.rgb * 0.3;
                float ndl = saturate(dot(N, L));
                float3 body = lerp(_ShallowColor.rgb, _DeepColor.rgb, depthT);
                // Streulicht im Wasser: Wellenkämme gegen die Sonne leuchten türkis auf
                float sss = pow(saturate(dot(-V, L) * 0.5 + 0.5), 3.0) * saturate(nxy.x * 0.5 + nxy.y * 0.5 + 0.3);
                float3 lit = body * (amb + sunCol * (0.35 + ndl * 0.4)) + _ShallowColor.rgb * sunCol * sss * 0.35;

                // Fresnel + Himmelsreflexion
                float ndv = saturate(dot(N, V));
                float fres = 0.02 + 0.98 * pow(1.0 - ndv, 5.0);
                float3 R = reflect(-V, N);
                R.y = abs(R.y);
                float3 refl = DecodeHDR(UNITY_SAMPLE_TEXCUBE_LOD(unity_SpecCube0, R, 1.0), unity_SpecCube0_HDR);
                float3 col = lerp(lit, refl, saturate(fres * _Reflect));

                // Sonnenglanz: scharfer Kern + breiter Schimmer
                float3 H = normalize(L + V);
                float nh = saturate(dot(N, H));
                float spec = pow(nh, _Gloss) * _SunSpec + pow(nh, _Gloss * 0.08) * 0.12 * _SunSpec;
                col += sunCol * spec;

                // Uferschaum über die Tiefendifferenz, mit wandernden Schaumbändern
                float edge = saturate(1.0 - thick / max(_FoamParams.x, 0.01));
                float fn = tex2D(_NoiseTex, p * _FoamParams.z + float2(t * 0.03, t * 0.02)).b;
                float fn2 = tex2D(_NoiseTex, p * _FoamParams.z * 2.3 - float2(t * 0.02, t * 0.035)).a;
                float bands = sin(thick * 7.0 - _Time.y * 1.6 + fn * 6.0) * 0.5 + 0.5;
                float foam = saturate(smoothstep(0.45, 0.75, edge * (0.7 + bands * 0.5) + (fn + fn2) * 0.35 - 0.2) + pow(edge, 6.0) * 0.6);
                foam *= _FoamParams.y * _RP_DepthOn;
                float3 foamLit = _FoamColor.rgb * (amb + sunCol * (0.4 + ndl * 0.6));
                col = lerp(col, foamLit, foam);

                // Deckkraft: flach durchsichtig, tief deckend; Reflexion/Glanz/Schaum decken immer; weicher Uferrand
                float alpha = lerp(_AlphaRange.x, _AlphaRange.y, depthT);
                alpha = max(alpha, max(saturate(fres * _Reflect), foam));
                alpha = saturate(alpha + spec * 0.5);
                alpha *= saturate(thick * 4.0 + (1.0 - _RP_DepthOn));

                float4 outc = float4(col, alpha);
                UNITY_APPLY_FOG(i.fogCoord, outc);
                return outc;
            }
            ENDCG
        }
    }
    Fallback Off
}
