#include "../common.h"

sampler2D TileTexture : register(s0);
sampler2D SourceTexture : register(s1);

#define EPSILON (1e-10)

float MinSat;
float MaxSat;

float MinHue;
float MaxHue;

float4 LightColor;

float3 RGBtoHCV(float3 color)
{
    float4 p = color.g < color.b
        ? float4(color.bg, -1, 0.6666)
        : float4(color.gb, 0, -0.3333);
    
    float4 q = color.r < p.x
        ? float4(p.xyw, color.r)
        : float4(color.r, p.yzx);
    
    float C = q.x - min(q.w, q.y);
    
    float hue = abs((q.w - q.y) / (6 * C + EPSILON) + q.z);
    
    return float3(hue, C, q.x);
}

float3 RGBtoHSL(float3 color)
{
    float3 hcv = RGBtoHCV(color);
    
    float l = hcv.z - hcv.y * 0.5;
    float s = hcv.y / (1 - abs((l * 2) - 1) + EPSILON);
    
    return float3(hcv.x, s, l);
}

float4 NatureLightingShaderFragment(float2 textureUv : TEXCOORD0, float4 baseColor : COLOR0) : COLOR0
{
    const float hue_leniance = 0.0;
    const float saturation_leniance = 0.0;

    float4 base = tex2D(TileTexture, textureUv) * baseColor;
    
    float4 source = tex2D(TileTexture, textureUv);
    
    float3 hsl = RGBtoHSL(source.rgb);
    
    bool inRange =
        hsl.x > max(MinHue - hue_leniance, 0) && hsl.x < min(MaxHue + hue_leniance, 1)
     && hsl.y > max(MinSat - saturation_leniance, 0) && hsl.y < min(MaxSat + saturation_leniance, 1);
    
    float lightness = (hsl.z - 0.2) * 2.3;
    
    lightness = pow(1 - lightness, 6) * 0.7;
    
    float4 light = LightColor * lightness * inRange * base.a;
    
    return base + light;
}

BEGIN_TECHNIQUE(Technique1)
    BEGIN_PASS(NatureLightingShader) 
        PIXEL_SHADER(compile ps_3_0 NatureLightingShaderFragment()) 
    END_PASS
END_TECHNIQUE