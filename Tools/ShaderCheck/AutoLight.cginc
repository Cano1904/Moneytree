// Minimaler Ersatz für AutoLight.cginc – NUR für die Syntax-/Typprüfung.
#ifndef RP_AUTOLIGHT_STUB
#define RP_AUTOLIGHT_STUB
#define SHADOW_COORDS(idx) float4 _ShadowCoord : TEXCOORD##idx;
#define TRANSFER_SHADOW(a) a._ShadowCoord = 0;
#define SHADOW_ATTENUATION(a) 1.0
#define UNITY_LIGHT_ATTENUATION(destName, input, worldPos) float destName = 1.0;
#endif
