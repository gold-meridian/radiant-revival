#include "../common.h"

sampler2D TileTexture : register(s0);
sampler2D SourceTexture : register(s1);

#define EPSILON (1e-10)

float MinSat;
float MaxSat;

float MinHue;
float MaxHue;

float3 RGBtoHSL(float3 color)
{
    float highest = max(max(color.r, color.g), color.b);
    float lowest = min(min(color.r, color.g), color.b);

    float range = highest - lowest;

    float r2 = (highest - color.r) / range;
    float g2 = (highest - color.g) / range;
    float b2 = (highest - color.b) / range;

    float hue = 0;
    
    if (color.r == highest)
    {
        hue = (color.g == lowest ? 5 + b2 : 1 - g2) * 0.1666;
    }
    else if (color.g == highest)
    {
        hue = (color.b == lowest ? 1 + r2 : 3 - b2) * 0.1666;
    }
    else
    {
        hue = (color.r == lowest ? 3 + g2 : 5 - r2) * 0.1666;
    }

    float lightness = (lowest + highest) * 0.5;

    float saturation = range / (lightness <= 0.5 ? (highest + lowest) : (2 - highest - lowest));

    return float3(hue, saturation, lightness);
}

float4 NatureLightingShaderFragment(float2 textureUv : TEXCOORD0, float4 baseColor : COLOR0) : COLOR0
{
    float4 base = tex2D(TileTexture, textureUv) * baseColor;
    
    clip(base.a - EPSILON);
    
    float4 source = tex2D(TileTexture, textureUv);
    
    float3 hsl = RGBtoHSL(source.rgb);
    
    bool inRange =
        hsl.x > MinHue && hsl.x < MaxHue
     && hsl.y > MinSat && hsl.y < MaxSat;
    
    return base + inRange;
}

BEGIN_TECHNIQUE(Technique1)
    BEGIN_PASS(NatureLightingShader) 
        PIXEL_SHADER(compile ps_3_0 NatureLightingShaderFragment()) 
    END_PASS
END_TECHNIQUE