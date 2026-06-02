#include "../common.h"

sampler2D ScreenTexture : register(s0);
sampler2D MaskTexture : register(s1);
sampler2D LightMap : register(s2);
sampler2D Noise : register(s3);

#define TILE_SIZE (16.0)
#define TIME_SPEED (0.2)

TEXTURE_SIZE(ScreenTextureSize, 0);
TEXTURE_SIZE(MaskTextureSize, 1);
TEXTURE_SIZE(LightingBufferSize, 2);
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
    rainUv.y *= 0.3;
    
    rainUv += Time * TIME_SPEED;
    rainUv.x += rainUv.y * Direction.x;
    
    float noise = tex2D(Noise, rainUv).x * tex2D(Noise, rainUv * 0.32f).y;
    float rain = saturate(pow(abs(noise) - (1 - Intensity), 5.) * 10.0);
    
    float2 offset = Direction * 0.03 * (1 - pow(1 - rain, 3));
    
    float4 color = tex2D(ScreenTexture, uv / ScreenTextureSize + offset);
    
    color += rain * (1 - mask);
    
    color.rgb *= light * GlobalBrightness;
    
    return color * baseColor;
}

BEGIN_TECHNIQUE(Technique1)
    BEGIN_PASS(RainDistortionShader)  
        PIXEL_SHADER(compile ps_3_0 RainDistortionShaderFragment()) 
    END_PASS
END_TECHNIQUE