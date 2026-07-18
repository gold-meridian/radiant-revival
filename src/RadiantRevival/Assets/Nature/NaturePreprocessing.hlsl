#include "../common.h"

sampler2D NatureTexture : register(s0);

#define EPSILON (1e-10)

float MinHue;
float MaxHue;
float HueOffset;
bool InvertHue;

float MinSat;
float MaxSat;
bool InvertSat;

float2 Contrast;

float4 Source;

TEXTURE_SIZE(TextureSize, 0)

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

float Map(float value, float start1, float stop1, float start2, float stop2)
{
    value = clamp(value, start1, stop1);
    return start2 + (stop2 - start2) * ((value - start1) / (stop1 - start1));
}

bool Sample(float2 uv)
{
    float4 base = tex2D(NatureTexture, uv);
    
    if (base.a < 1)
    {
        return false;
    }
    
    float3 hsl = RGBtoHSL(base.rgb);
    
    float hue = (hsl.x + HueOffset) % 1;
    
    bool inRange =
        InvertHue != (hue >= MinHue && hue <= MaxHue)
     && InvertSat != (hsl.y >= MinSat && hsl.y <= MaxSat);
     
    return inRange;
}

// Based on the spiral approach in https://shaderbits.com/blog/various-distance-field-generation-techniques
float2 BruteForceDistance(float2 origUv)
{
    const int max_radius = 80;

    float4 bounds = Source / TextureSize.xyxy;
    
    float2 pixel = 2 / TextureSize;

    for (int i = 0; i < max_radius; i++)
    {
        float totneighbors = i * 8;
        
        for (int j = 0; j < totneighbors; j++)
        {
            float progress = j / totneighbors;
            float2 offset = 0;
            
            offset.x = saturate(4 * progress) - saturate(4 * (progress - 0.5));
            offset.y = saturate(4 * (progress - 0.25)) - saturate(4 * (progress - 0.75));
            
            offset = (offset * totneighbors * 0.25) - i;
            
            float2 uv = origUv + (offset * pixel);

            if (!Sample(uv))
            {
                uv -= origUv;
                uv /= bounds.zw;
                uv *= 0.5;
                uv += 0.5;

                return uv;
            }
        }
    }
    
    return 0;
}

float4 NaturePreprocessingShaderFragment(float2 textureUv : TEXCOORD0) : COLOR0
{
    float4 base = tex2D(NatureTexture, textureUv);
    
    if (!Sample(textureUv))
    {
        return 0;
    }
    
    float3 hsl = RGBtoHSL(base.rgb);
    
    float2 dist = BruteForceDistance(textureUv);
    
    float lightness = (hsl.z - Contrast.x) * Contrast.y;
    
    lightness = 1 - lightness;
    
    lightness = pow(saturate(lightness), 7);
    
    lightness = saturate(lightness) * base.a;
    
    return float4(dist, lightness, 0);
}

BEGIN_TECHNIQUE(Technique1)
    BEGIN_PASS(NaturePreprocessingShader)  
        PIXEL_SHADER(compile ps_3_0 NaturePreprocessingShaderFragment())  
    END_PASS
END_TECHNIQUE