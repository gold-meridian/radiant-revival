#include "../common.h"

sampler2D RainTexture : register(s0);
sampler2D LightMap : register(s1);

#define TILE_SIZE (16.0)

SCREEN_SIZE(ScreenSize);
TEXTURE_SIZE(LightingBufferSize, 1);

float2 LightOffset;

float OffscreenTiles;
float GlobalBrightness;

float Intensity;

float4 RainBackgroundShaderFragment(float2 uv : TEXCOORD0, float2 svPos : SV_POSITION, float4 baseColor : COLOR0) : COLOR0
{
    float2 screenPosTiles = (svPos + LightOffset) / TILE_SIZE;
    screenPosTiles += OffscreenTiles;
    
    float2 lightUv = screenPosTiles / LightingBufferSize;
    float3 light = tex2D(LightMap, lightUv);
    
    float2 rainUv = uv;
    float4 rain = tex2D(RainTexture, rainUv);
    
    rain *= 0.4 * max(1 - pow(Intensity, 2), 0.4) * GlobalBrightness;
    rain.rgb *= light * baseColor.rgb;
    
    rain *= rain.a;
    
    return rain;
}

BEGIN_TECHNIQUE(Technique1)
    BEGIN_PASS(RainBackgroundShader)   
        PIXEL_SHADER(compile ps_3_0 RainBackgroundShaderFragment())  
    END_PASS
END_TECHNIQUE