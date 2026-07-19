#include "../common.h"

sampler2D NatureTexture : register(s0);
sampler2D ProcessedTexture : register(s1);

#define EPSILON (1e-10)

#define POSTERIZATION_STEPS (3)

#define PIXEL_SIZE (2)

float4 LightColor;
float2 LightPosition;

float DrawZoom;

float2 Contrast;

SCREEN_SIZE(ScreenSize)

TEXTURE_SIZE(MaskSize, 1)

float Map(float value, float start1, float stop1, float start2, float stop2)
{
    value = clamp(value, start1, stop1);
    return start2 + (stop2 - start2) * ((value - start1) / (stop1 - start1));
}

float4 NatureLightingShaderFragment(float2 textureUv : TEXCOORD0, float4 baseColor : COLOR0) : COLOR0
{
    float4 base = tex2D(NatureTexture, textureUv) * baseColor;
    
    float4 processed = tex2D(ProcessedTexture, textureUv);
    
    float3 pixel = float3(1 / MaskSize, 0);
    
    float mult = 1;
    {
        float4 left = tex2D(ProcessedTexture, textureUv + float2(-pixel.x, 0));
        float4 right = tex2D(ProcessedTexture, textureUv + float2(pixel.x, 0));
        float4 up = tex2D(ProcessedTexture, textureUv + float2(0, -pixel.y));
        float4 down = tex2D(ProcessedTexture, textureUv + float2(0, pixel.y));
        
        float total = processed.a + left.a + right.a + up.a + down.a;
        
        if (total < 5)
        {
            mult = 0.93;
        }
    }
    
    float2 lightPos = LightPosition / ScreenSize;
    {
        lightPos -= 0.5;
        lightPos *= DrawZoom;
        lightPos += 0.5;
    }
    
    float2 lightDirection = normalize(lightPos - 0.5);
    
    float2 normal = (processed.xy - 0.5) * 2;
    
    float dist = saturate(1 - length(normal));
    
    normal = normalize(normal);
    
    float lightness = processed.z * mult;
    
    float lightFactor = pow(saturate(dot(lightDirection, normal) + 0.6 + (0.3 * dist)), 1.3) + 0.09;
    lightFactor *= 1-pow( 1-dist, 1.1);
    
    lightness *= saturate(lightFactor);
    
    lightness = pow(saturate(lightness + 0.06), 16);
    
    lightness = floor(lightness * POSTERIZATION_STEPS) / POSTERIZATION_STEPS;
    
    return base + (LightColor * lightness * LightColor.a);
}

BEGIN_TECHNIQUE(Technique1)
    BEGIN_PASS(NatureLightingShader) 
        PIXEL_SHADER(compile ps_3_0 NatureLightingShaderFragment()) 
    END_PASS
END_TECHNIQUE