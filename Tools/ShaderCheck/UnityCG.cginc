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
float4 _ScreenParams;
float4 _ProjectionParams;
float4 _ZBufferParams;
float4x4 unity_MatrixV;
float4x4 unity_CameraProjection;
#define UNITY_MATRIX_V unity_MatrixV
float4 unity_AmbientSky;
float4 unity_AmbientEquator;
float4 unity_AmbientGround;
float4 unity_SHAr; float4 unity_SHAg; float4 unity_SHAb;
float3 ShadeSH9(float4 n) { return float3(dot(unity_SHAr, n), dot(unity_SHAg, n), dot(unity_SHAb, n)); }
#define sampler2D_float sampler2D
#define UNITY_DECLARE_DEPTH_TEXTURE(tex) sampler2D tex
#define SAMPLE_DEPTH_TEXTURE(tex, uv) (tex2D(tex, uv).r)
#define SAMPLE_DEPTH_TEXTURE_PROJ(tex, uv) (tex2Dproj(tex, uv).r)
#define UNITY_PROJ_COORD(a) a
float Linear01Depth(float z) { return 1.0 / (_ZBufferParams.x * z + _ZBufferParams.y); }
float LinearEyeDepth(float z) { return 1.0 / (_ZBufferParams.z * z + _ZBufferParams.w); }
float4 ComputeScreenPos(float4 p) { float4 o = p * 0.5; o.xy = float2(o.x, o.y * _ProjectionParams.x) + o.w; o.zw = p.zw; return o; }
float4 ComputeGrabScreenPos(float4 p) { return ComputeScreenPos(p); }
#define COMPUTE_EYEDEPTH(o) o = -mul(UNITY_MATRIX_V, mul(unity_ObjectToWorld, v.vertex)).z
float3 UnityWorldSpaceViewDir(float3 worldPos) { return _WorldSpaceCameraPos.xyz - worldPos; }
float3 UnityWorldSpaceLightDir(float3 worldPos) { return _WorldSpaceLightPos0.xyz - worldPos * _WorldSpaceLightPos0.w; }
float3 UnityObjectToWorldDir(float3 d) { return normalize(mul((float3x3)unity_ObjectToWorld, d)); }
TextureCube unity_SpecCube0;
SamplerState samplerunity_SpecCube0;
float4 unity_SpecCube0_HDR;
#define UNITY_DECLARE_TEXCUBE(tex) TextureCube tex; SamplerState sampler##tex
#define UNITY_SAMPLE_TEXCUBE(tex, coord) tex.Sample(sampler##tex, coord)
#define UNITY_SAMPLE_TEXCUBE_LOD(tex, coord, lod) tex.SampleLevel(sampler##tex, coord, lod)
float3 DecodeHDR(float4 data, float4 decodeInstructions) { return decodeInstructions.x * data.rgb; }
struct appdata_img { float4 vertex : POSITION; float2 texcoord : TEXCOORD0; };
struct v2f_img { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
v2f_img vert_img(appdata_img v) { v2f_img o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.texcoord; return o; }
float4 unity_ColorSpaceDielectricSpec;
float DecodeFloatRG(float2 enc) { return dot(enc, float2(1.0, 1.0 / 255.0)); }
float3 DecodeViewNormalStereo(float4 enc4)
{
    float kScale = 1.7777;
    float3 nn = enc4.xyz * float3(2.0 * kScale, 2.0 * kScale, 0) + float3(-kScale, -kScale, 1);
    float g = 2.0 / dot(nn.xyz, nn.xyz);
    float3 n; n.xy = g * nn.xy; n.z = g - 1.0;
    return n;
}
void DecodeDepthNormal(float4 enc, out float depth, out float3 normal) { depth = DecodeFloatRG(enc.zw); normal = DecodeViewNormalStereo(enc); }
float3 GammaToLinearSpace(float3 c) { return c * (c * (c * 0.305306011 + 0.682171111) + 0.012522878); }
#endif
