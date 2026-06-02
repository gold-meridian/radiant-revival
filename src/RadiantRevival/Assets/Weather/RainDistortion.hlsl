#include "../common.h"

sampler2D ScreenTexture : register(s0);
sampler2D MaskTexture : register(s1);
sampler2D LightMap : register(s2);
sampler2D Noise : register(s3);
sampler2D RainTexture : register(s4);

#define TILE_SIZE (16.0)
#define TIME_SPEED (0.001)

TEXTURE_SIZE(ScreenTextureSize, 0);
TEXTURE_SIZE(MaskTextureSize, 1);
TEXTURE_SIZE(LightingBufferSize, 2);
TEXTURE_SIZE(RainTextureSize, 4);

SCREEN_POSITION(ScreenPosition)

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

float Map(float value, float start1, float stop1, float start2, float stop2)
{
    value = clamp(value, start1, stop1);
    return start2 + (stop2 - start2) * ((value - start1) / (stop1 - start1));
}

float4 RainDistortionShaderFragment(float2 svPos : SV_POSITION, float4 baseColor : COLOR0) : COLOR0
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
    
    float2 rainUv = uv;
    rainUv += ScreenPosition;
    rainUv /= max(ScreenTextureSize.x, ScreenTextureSize.y);
    
    rainUv.x += rainUv.y * -Direction.x;
    rainUv.x *= 2.3;
    rainUv.y *= 0.015;
    
    rainUv.y -= Time * TIME_SPEED * max(Intensity, 0.4);
    
    float noise = tex2D(Noise, rainUv).x;
    
    float strength = 3.4 * (1 - pow(1 - Intensity, 12));
    
    float rain = saturate(pow(abs(noise * strength) - (1 - pow(Intensity, 2)), 5));
    
    float offsetSize = 32 * Intensity / max(ScreenTextureSize.x, ScreenTextureSize.y);
    float2 offset = -Direction * offsetSize * (1 - pow(1 - rain, 5)) * (1 - mask);
    
    float4 color = tex2D(ScreenTexture, (uv / ScreenTextureSize) + offset);
    
    float3 rainColor = 0.24 * (1 - Intensity) * light * GlobalBrightness * tex2D(RainTexture, RainPosition / RainTextureSize).rgb;
    
    color.rgb += rain * (1 - mask) * rainColor;
    
    return color;
}

BEGIN_TECHNIQUE(Technique1)
    BEGIN_PASS(RainDistortionShader)  
        PIXEL_SHADER(compile ps_3_0 RainDistortionShaderFragment()) 
    END_PASS
END_TECHNIQUE