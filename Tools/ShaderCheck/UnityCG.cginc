// Minimaler Ersatz für Unitys UnityCG.cginc – NUR für die Syntax-/Typprüfung mit glslang (HLSL → SPIR-V).
// Enthält die Makros und Variablen, die die RE:PLANET-Shader verwenden. Nicht in Unity verwenden.
#ifndef RP_UNITYCG_STUB
#define RP_UNITYCG_STUB
#define fixed float
#define fixed2 float2
#define fixed3 float3
#define fixed4 float4
#define half float
#define half2 float2
#define half3 float3
#define half4 float4
float4 _Time;
float4 _SinTime;
float3 _WorldSpaceCameraPos;
float4 _WorldSpaceLightPos0;
float4 _LightColor0;
float4 unity_FogColor;
float4x4 unity_ObjectToWorld;
float4x4 unity_WorldToObject;
float4x4 unity_MatrixVP;
#define UNITY_MATRIX_VP unity_MatrixVP
#define UNITY_PI 3.14159265359
float4 UnityObjectToClipPos(float4 v) { return mul(unity_MatrixVP, mul(unity_ObjectToWorld, float4(v.xyz, 1.0))); }
float4 UnityObjectToClipPos(float3 v) { return mul(unity_MatrixVP, mul(unity_ObjectToWorld, float4(v, 1.0))); }
float3 UnityObjectToWorldNormal(float3 n) { return normalize(mul(n, (float3x3)unity_WorldToObject)); }
#define TRANSFORM_TEX(tex, name) (tex.xy * name##_ST.xy + name##_ST.zw)
#define UNITY_FOG_COORDS(idx) float fogCoord : TEXCOORD##idx;
#define UNITY_TRANSFER_FOG(o, outpos) o.fogCoord = (outpos).z
#define UNITY_APPLY_FOG(coord, col) col.rgb = lerp(unity_FogColor.rgb, col.rgb, saturate(coord))
struct appdata_base { float4 vertex : POSITION; float3 normal : NORMAL; float4 texcoord : TEXCOORD0; };
struct appdata_full { float4 vertex : POSITION; float4 tangent : TANGENT; float3 normal : NORMAL; float4 texcoord : TEXCOORD0; float4 texcoord1 : TEXCOORD1; float4 color : COLOR; };
#endif
