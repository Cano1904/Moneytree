// Minimaler Ersatz für Lighting.cginc/UnityPBSLighting.cginc – NUR für die Syntax-/Typprüfung (Surface-Shader-Rahmen).
#ifndef RP_LIGHTING_STUB
#define RP_LIGHTING_STUB
#include "UnityCG.cginc"
struct SurfaceOutput { float3 Albedo; float3 Normal; float3 Emission; float Specular; float Gloss; float Alpha; };
struct SurfaceOutputStandard { float3 Albedo; float3 Normal; float3 Emission; float Metallic; float Smoothness; float Occlusion; float Alpha; };
struct SurfaceOutputStandardSpecular { float3 Albedo; float3 Specular; float3 Normal; float3 Emission; float Smoothness; float Occlusion; float Alpha; };
#define UNITY_INITIALIZE_OUTPUT(type, name) name = (type)0;
#define INTERNAL_DATA
#define WorldNormalVector(data, normal) normal
#endif
