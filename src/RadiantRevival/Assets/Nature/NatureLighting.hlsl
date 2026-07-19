#include "../common.h"

sampler2D NatureTexture : register(s0);
sampler2D ProcessedTexture : register(s1);

#define EPSILON (1e-10)

#define POSTERIZATION_STEPS (4)

#define PIXEL_SIZE (2)

float4 LightColor;
float2 LightPosition;

float DrawZoom;

float2 Contrast;

SCREEN_SIZE(ScreenSize)

TEXTURE_SIZE(TextureSize, 1)

float Map(float value, float start1, float stop1, float start2, float stop2)
{
    value = clamp(value, start1, stop1);
    return start2 + (stop2 - start2) * ((value - start1) / (stop1 - start1));
}

float4 NatureLightingShaderFragment(float2 textureUv : TEXCOORD0, float4 baseColor : COLOR0) : COLOR0
{
    float4 base = tex2D(NatureTexture, textureUv) * baseColor;
    
    float4 processed = tex2D(ProcessedTexture, textureUv);
    
    // return processed.xyxy;
    
    /*
    float3 pixel = float3(PIXEL_SIZE / TextureSize, 0);
    
    float2 averageField = 0;
    {
        float4 left = tex2D(ProcessedTexture, textureUv - pixel.xz);
        float4 right = tex2D(ProcessedTexture, textureUv + pixel.xz);
        float4 up = tex2D(ProcessedTexture, textureUv - pixel.zy);
        float4 down = tex2D(ProcessedTexture, textureUv + pixel.zy);
        
        float total = left.a + right.a + up.a + down.a;
        
        left *= left.a;
        right *= right.a;
        up *= up.a;
        down *= down.a;
        
        averageField = processed.xy + left.xy + right.xy + up.xy + down.xy;
        averageField /= total + processed.a;
    }
    */
    
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
    
    float lightness = processed.z;
    
    float lightFactor = pow(saturate(dot(lightDirection, normal) + 1.3 + (0.3 * dist)), 1.3);
    lightFactor *= pow(dist, 1);
    
    lightness *= lightFactor;
    
    lightness = pow(saturate(lightness), 7);
    
    lightness = floor(lightness * POSTERIZATION_STEPS) / POSTERIZATION_STEPS;
    
    lightness = 1 - pow(1 - lightness, 2.4);
    
    return base + (LightColor * lightness * LightColor.a);
}

BEGIN_TECHNIQUE(Technique1)
    BEGIN_PASS(NatureLightingShader) 
        PIXEL_SHADER(compile ps_3_0 NatureLightingShaderFragment()) 
    END_PASS
END_TECHNIQUE