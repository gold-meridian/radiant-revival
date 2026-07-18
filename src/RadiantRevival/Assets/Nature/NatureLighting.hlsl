#include "../common.h"

sampler2D NatureTexture : register(s0);
sampler2D ProcessedTexture : register(s1);

#define EPSILON (1e-10)

#define POSTERIZATION_STEPS (3)

float4 LightColor;
float2 LightPosition;

float DrawZoom;

float2 Contrast;

TEXTURE_SIZE(TextureSize, 1)

SCREEN_SIZE(ScreenSize)

float Map(float value, float start1, float stop1, float start2, float stop2)
{
    value = clamp(value, start1, stop1);
    return start2 + (stop2 - start2) * ((value - start1) / (stop1 - start1));
}

float4 NatureLightingShaderFragment(float2 textureUv : TEXCOORD0, float4 baseColor : COLOR0) : COLOR0
{
    float4 base = tex2D(NatureTexture, textureUv) * baseColor;
    
    float4 processed = tex2D(ProcessedTexture, textureUv);
    
    float2 lightPos = LightPosition / ScreenSize;
    {
        lightPos -= 0.5;
        lightPos *= DrawZoom;
        lightPos += 0.5;
    }
    
    float2 lightDirection = normalize(lightPos - 0.5);
    
    float2 normal = (processed.xy - 0.5) * 2;
    
    float dist = length(normal);
    
    float lightness = processed.z;
    
    float lightFactor = pow(saturate(dot(lightDirection, normal) + 0.4 + (0.5 * dist)), 1.3);
    lightFactor += pow(dist, 3) * 0.06;
    
    // float fade = saturate(1 - pow(lightUv.y + 0.0, 3));
    
    lightness *= lightFactor;
    
    lightness = floor(lightness * POSTERIZATION_STEPS) / POSTERIZATION_STEPS;
    
    lightness = saturate(lightness);
    
    return base + LightColor * lightness;
}

BEGIN_TECHNIQUE(Technique1)
    BEGIN_PASS(NatureLightingShader) 
        PIXEL_SHADER(compile ps_3_0 NatureLightingShaderFragment()) 
    END_PASS
END_TECHNIQUE