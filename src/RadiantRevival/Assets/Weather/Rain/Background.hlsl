#include "../../common.h"

#define TILE_SIZE (16.0)

#define RAIN_ALPHA (0.2)
#define RAIN_INTENSITY_MIN (0.45)

sampler2D RainTexture : register(s0);
sampler2D LightMap : register(s1);

SCREEN_SIZE(ScreenSize);
TEXTURE_SIZE(LightingBufferSize, 1);

float2 LightOffset;

float OffscreenTiles;
float GlobalBrightness;

float Intensity;

float4 BackgroundShaderFragment(float2 uv : TEXCOORD0, float2 svPos : SV_POSITION, float4 baseColor : COLOR0) : COLOR0
{
    float2 screenPosTiles = (svPos + LightOffset) / TILE_SIZE;
    screenPosTiles += OffscreenTiles;
    
    float2 lightUv = screenPosTiles / LightingBufferSize;
    float3 light = tex2D(LightMap, lightUv);
    
    float2 rainUv = uv;
    float4 rain = tex2D(RainTexture, rainUv);
    
    rain *= RAIN_ALPHA * max(1 - pow(Intensity, 2), RAIN_INTENSITY_MIN) * GlobalBrightness;
    rain.rgb *= light * baseColor.rgb;
    
    rain *= rain.a;
    
    return rain;
}

BEGIN_TECHNIQUE(Technique1)
    BEGIN_PASS(BackgroundShader)   
        PIXEL_SHADER(compile ps_3_0 BackgroundShaderFragment())  
    END_PASS
END_TECHNIQUE