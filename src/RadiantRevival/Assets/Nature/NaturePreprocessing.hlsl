#include "../common.h"

sampler2D NatureTexture : register(s0);
sampler2D MaskTexture : register(s1);

#define EPSILON (1e-10)

#define TAU (6.28318531)

float MinHue;
float MaxHue;
float HueOffset;

float MinSat;
float MaxSat;
bool InvertMask;

float2 Contrast;

float4 Source;

float2 FrameSize;

TEXTURE_SIZE(TextureSize, 1)

float2 GetVanillaHueSat(float3 color)
{
    float maximum = max(max(color.r, color.g), color.b);
    float minimum = min(min(color.r, color.g), color.b);
    float delta = maximum - minimum;

    if (delta <= 0 || maximum <= 0)
    {
        return float2(0, 0);
    }
        
    float redHue = ((color.g - color.b) / delta) + 6;
    float greenHue = ((color.b - color.r) / delta) + 2;
    float blueHue = ((color.r - color.g) / delta) + 4;
    
    float hueSector = max(max(
        redHue * (color.r >= maximum),
        greenHue * (color.g >= maximum)),
        blueHue * (color.b >= maximum)
    );

    float hue = frac(hueSector * (1 / 6));
    float saturation = delta / maximum;
    
    return float2(hue, saturation);
}

float SignedFraction(float value)
{
    float fraction = frac(abs(value));
    return fraction * sign(value);
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
        return 0;
    }
    
    float2 hueSat = GetVanillaHueSat(base.rgb);
    float hue = SignedFraction(hueSat.x + HueOffset);
    float saturation = hueSat.y;

    float inside =
        saturation >= MinSat &&
        saturation <= MaxSat &&
        hue >= MinHue &&
        hue <= MaxHue
            ? 1
            : 0;

    if (InvertMask > 0.5)
    {
        inside = 1 - inside;
    }
    
    return float4(inside, Contrast.x, Contrast.y, 0);
}

float AngleLerp(float aAngle, float bAngle, float amount)
{
    float angle;
    
    if (bAngle < aAngle)
    {
        float num = bAngle + TAU;
        angle = ((num - aAngle > aAngle - bAngle) ? lerp(aAngle, bAngle, amount) : lerp(aAngle, num, amount));
    }
    else
    {
        float num = bAngle - TAU;
        angle = ((bAngle - aAngle > aAngle - num) ? lerp(aAngle, num, amount) : lerp(aAngle, bAngle, amount));
    }
    
    return angle;
}

float4 NatureDistanceFieldShaderFragment(float2 textureUv : TEXCOORD0) : COLOR0
{
    float4 base = tex2D(NatureTexture, textureUv);
    
    float4 mask = tex2D(MaskTexture, textureUv);
    
    if (!Sample(textureUv))
    {
        return 0;
    }
    
    float maximum = max(max(base.r, base.g), base.b);
    float minimum = min(min(base.r, base.g), base.b);

    float lightness = (minimum + maximum) / 2;
    
    float2 dist = BruteForceDistance(textureUv);
    
    float2 localUv = textureUv * TextureSize;
    localUv -= floor(localUv / FrameSize) * FrameSize;
    localUv /= FrameSize;
    
    float fade = saturate(1 - pow(localUv.y - 0.02, 4.3));
    
    localUv -= 0.5;
    localUv *= 2;
    
    float len = length(dist);
    
    const float angle_interpolant = 0.2;
    
    float localAng = atan2(localUv.y, localUv.x);
    float origAng = atan2(dist.y, dist.x);
    
    float between = AngleLerp(localAng, origAng, angle_interpolant);
    
    dist = float2(cos(between), sin(between)) * len;
    
    dist *= 0.5;
    dist += 0.5;
    
    lightness = (lightness - mask.y) * mask.z;
    
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