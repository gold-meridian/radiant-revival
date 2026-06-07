#include "../../common.h"

sampler2D ScreenTexture : register(s0);
sampler2D MaskTexture : register(s1);
sampler2D LightMap : register(s2);
sampler2D RainTexture : register(s3);
sampler2D NoiseTexture : register(s4);

#define TILE_SIZE (16.0)

SCREEN_SIZE(ScreenSize);
TEXTURE_SIZE(MaskTextureSize, 1);
TEXTURE_SIZE(LightingBufferSize, 2);

float Time;

float DrawZoom;
float2 MaskOffset;
float2 TilePixelOffset;

float2 LightOffset;

float OffscreenTiles;
float GlobalBrightness;

float2 Direction;

float Intensity;

float2 RainPosition;

float4 DistortionShaderFragment(float2 svPos : SV_POSITION, float4 baseColor : COLOR0) : COLOR0
{
    float2 uv = svPos;
    
    float2 maskUv = uv;
    
    maskUv -= MaskOffset;
    maskUv += TilePixelOffset;
    
    maskUv /= MaskTextureSize * 2;
    {
        maskUv -= 0.5;
        maskUv *= DrawZoom;
        maskUv += 0.5;
    }
    
    float2 screenPosTiles = (uv + LightOffset / DrawZoom) / TILE_SIZE;
    screenPosTiles += OffscreenTiles;
    float2 lightUv = screenPosTiles / LightingBufferSize;
    {
        lightUv -= 0.5;
        lightUv *= DrawZoom;
        lightUv += 0.5;
    }
    
    float mask = tex2D(MaskTexture, maskUv);
    
    float3 light = tex2D(LightMap, lightUv);
    
    uv /= ScreenSize;
    
    float2 rainUv = uv;
    {
        rainUv -= 0.5;
        rainUv *= DrawZoom;
        rainUv += 0.5;
    }
    
    float4 rain = tex2D(RainTexture, rainUv);
    
    float2 noiseUv = rainUv;
    noiseUv.y *= 0.3f;
    noiseUv += float2(Time * 0.1, Time * 0.3);
    
    float noise = pow(tex2D(NoiseTexture, noiseUv).r, 3) * 4;
    
    float offsetSize = 8 * (Intensity + noise) / max(ScreenSize.x, ScreenSize.y);
    float2 offset = -Direction * offsetSize * (1 - pow(1 - rain.a, 5)) * (1 - mask);
    
    float4 color = tex2D(ScreenTexture, (uv) + offset);
    
    color.rgb += rain.rgb * rain.a * (1 - mask) * 0.24 * max(1 - pow(Intensity, 2), 0.4) * light * GlobalBrightness;
    
    return color;
}

BEGIN_TECHNIQUE(Technique1)
    BEGIN_PASS(DistortionShader)  
        PIXEL_SHADER(compile ps_3_0 DistortionShaderFragment()) 
    END_PASS
END_TECHNIQUE