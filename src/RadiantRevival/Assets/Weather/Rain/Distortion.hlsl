#include "../../common.h"

#define TILE_SIZE (16.0)
#define DISTORTION_SIZE (8.0)

#define RAIN_COLOR_MULTIPLIER (0.25)
#define RAIN_COLOR_ADD (0.15)
#define RAIN_INTENSITY_MIN (0.45)

sampler2D ScreenTexture : register(s0);
sampler2D MaskTexture : register(s1);
sampler2D LightMap : register(s2);
sampler2D RainTexture : register(s3);
sampler2D NoiseTexture : register(s4);

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
    
    float mask = 1 - tex2D(MaskTexture, maskUv);
    
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
    
    float offsetSize = DISTORTION_SIZE * (1 - pow(1 - Intensity, 4) + noise) / max(ScreenSize.x, ScreenSize.y);
    float2 offset = -Direction * offsetSize * (1 - pow(1 - rain.a, 5)) * mask;
    
    float2 distortedUv = uv + offset;
    
    float4 color = tex2D(ScreenTexture, distortedUv);
    
    float rainAlpha = rain.a * mask * max(1 - pow(Intensity, 2), RAIN_INTENSITY_MIN) * GlobalBrightness;
    
    float3 rainColor = rain.rgb;
    
    color.rgb = lerp(color.rgb, color.rgb * rainColor, rainAlpha * RAIN_COLOR_MULTIPLIER);
    color.rgb += rainColor * light * rainAlpha * RAIN_COLOR_ADD;
    
    return color;
}

BEGIN_TECHNIQUE(Technique1)
    BEGIN_PASS(DistortionShader)  
        PIXEL_SHADER(compile ps_3_0 DistortionShaderFragment()) 
    END_PASS
END_TECHNIQUE