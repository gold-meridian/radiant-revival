#include "../common.h"

sampler2D NatureTexture : register(s0);
sampler2D MaskTexture : register(s1);

#define EPSILON (1e-10)

#define TAU (6.28318531)

float MinHue;
float MaxHue;
float HueOffset;
bool InvertHue;

float MinSat;
float MaxSat;
bool InvertSat;

float2 Contrast;

float4 Source;

float2 FrameSize;

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
    return tex2D(MaskTexture, uv).r > 0;
}

float2 BruteForceDistance(float2 origUv)
{
    const int max_radius = 40;

    float2 bounds = FrameSize / TextureSize.xy;
    
    float2 pixel = 2 / TextureSize;

    float2 nearest = 999999;
    
    [loop]
    for (int x = -max_radius; x < max_radius; x++)
    {
        [loop]
        for (int y = -max_radius; y < max_radius; y++)
        {
            float2 offset = float2(x, y) * pixel;
        
            if (!Sample(origUv + offset) && length(offset) < length(nearest))
            {
                nearest = offset;
            }
        }
    }
    
    nearest /= bounds;
    
    return nearest;
}

float4 NatureMaskShaderFragment(float2 textureUv : TEXCOORD0) : COLOR0
{
    float4 base = tex2D(NatureTexture, textureUv);
    
    if (base.a < 1)
    {
        return false;
    }
    
    float3 hsl = RGBtoHSL(base.rgb);
    
    float hue = (hsl.x + HueOffset) % 1;
    
    bool inRange =
        InvertHue != (hue >= MinHue && hue <= MaxHue)
     && InvertSat != (hsl.y >= MinSat && hsl.y <= MaxSat);
     
    return float4(inRange, Contrast.x, Contrast.y, 0);
}

float4 NatureDistanceFieldShaderFragment(float2 textureUv : TEXCOORD0) : COLOR0
{
    float4 base = tex2D(NatureTexture, textureUv);
    
    float2 fieldUv = floor(textureUv * (TextureSize / 2)) / (TextureSize / 2);
    
    float4 mask = tex2D(MaskTexture, fieldUv);
    
    if (!Sample(fieldUv))
    {
        return 0;
    }
    
    float3 hsl = RGBtoHSL(base.rgb);
    
    float2 dist = BruteForceDistance(fieldUv);
    
    float2 localUv = fieldUv * TextureSize;
    localUv -= floor(localUv / FrameSize) * FrameSize;
    localUv /= FrameSize;
    
    float fade = saturate(1 - pow(localUv.y, 4));
    
    /*
    localUv -= 0.5;
    localUv *= 2;
    
    float len = length(dist);
    
    const float angle_interpolant = 0.7;
    
    float localAng = atan2(localUv.y, localUv.x);
    float origAng = atan2(dist.y, dist.x);
    
    float between = (TAU + localAng - origAng) % TAU;
    between = localAng + (between * angle_interpolant);
    
    dist = float2(cos(between), sin(between)) * len;
    */
    
    dist *= 0.5;
    dist += 0.5;
    
    float lightness = (hsl.z - mask.y) * mask.z;
    
    lightness = 1 - lightness;
    
    lightness = saturate(lightness) * fade * base.a;
    
    return float4(dist, lightness, 1);
}

BEGIN_TECHNIQUE(Technique1)
    BEGIN_PASS(NatureMaskShader)   
        PIXEL_SHADER(compile ps_3_0 NatureMaskShaderFragment())    
    END_PASS
    BEGIN_PASS(NatureDistanceFieldShader)  
        PIXEL_SHADER(compile ps_3_0 NatureDistanceFieldShaderFragment())   
    END_PASS
END_TECHNIQUE