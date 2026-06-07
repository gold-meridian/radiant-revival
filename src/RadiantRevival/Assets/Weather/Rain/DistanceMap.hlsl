#include "../../common.h"

sampler2D TileTexture : register(s0);
sampler2D RainMask : register(s1);
sampler2D DistanceMap : register(s2);

SCREEN_SIZE(ScreenSize);
TEXTURE_SIZE(TileTextureSize, 0);

int SampleCount;
float SampleDistance;
float DrawZoom;

float2 RainMaskOffset;
float ZoomDifference;

float2 ScreenPositionDifference;

float4 DistanceMapProcessingShaderFragment(float2 textureUv : TEXCOORD0, float2 svPos : SV_POSITION) : COLOR0
{
    float2 maskUv = textureUv - (RainMaskOffset / (TileTextureSize * 0.5));
    
    float4 tiles = tex2D(TileTexture, textureUv);
    
    float alpha = tiles.a - tex2D(RainMask, maskUv);
    
    alpha *= (tiles.r + tiles.g + tiles.b) * 0.333;
    alpha = 1 - pow(1 - alpha, 5);
    
    float2 distUv = svPos / ScreenSize;
    distUv -= (ScreenPositionDifference / ScreenSize) / DrawZoom;
    {
        distUv -= 0.5;
        distUv *= ZoomDifference;
        distUv += 0.5;
    }
    
    float prior = tex2D(DistanceMap, distUv).y;
    
    float4 color = float4(tiles.a, alpha, prior, 0);
    
    return color;
}

float4 DistanceMapShaderFragment(float2 textureUv : TEXCOORD0) : COLOR0
{
    float dtc = SampleDistance;
    
    float2 uv = textureUv;
    
    float4 tiles = tex2D(TileTexture, uv);
    
    clip(tiles.r - 0.001);
    
    [unroll(32)]
    for (int i = 0; i < SampleCount; i++)
    {
        uv.y += dtc;
        
        if (tex2D(TileTexture, uv).r == 0)
        {
            float dist = distance(textureUv.y, uv.y) * SampleCount * DrawZoom * 4;
            float alpha = lerp(tiles.b, tiles.g * pow(1 - dist, 2), 0.03) * tiles.r;
            float4 output = float4(uv.y, alpha, 0, 0);
            
            return output;
        }
    }
    
    return 0;
}

BEGIN_TECHNIQUE(Technique1) 
    BEGIN_PASS(DistanceMapProcessingShader)       
        PIXEL_SHADER(compile ps_3_0 DistanceMapProcessingShaderFragment())      
    END_PASS
    BEGIN_PASS(DistanceMapShader)     
        PIXEL_SHADER(compile ps_3_0 DistanceMapShaderFragment())    
    END_PASS
END_TECHNIQUE