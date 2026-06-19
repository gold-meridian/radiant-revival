#include "../../common.h"

sampler2D snowTexture : register(s0);
sampler2D snowNormalTexture : register(s1);
sampler2D reflectionColorTexture : register(s2);
sampler2D reflectionDepthTexture : register(s3);

float reflectivityInterpolant;

float4 CalculateScreenSpaceReflections(float2 start, float3 stepDirection)
{
    float2 uv = start;
    float4 result = 0;

    for (int i = 0; i < 64; i++)
    {
        float depth = tex2D(reflectionDepthTexture, uv).r;
        float4 sampleColor = float4(tex2D(reflectionColorTexture, uv).rgb, 1);
        bool validForSetting = depth >= 0.05 && result.a <= 0;
        
        result = lerp(result, sampleColor, validForSetting);
        
        uv += stepDirection.xy * 0.02;
    }

    return result;
}

float4 PixelShaderFunction(float2 uv : TEXCOORD0, float4 sampleColor : COLOR0) : COLOR0
{
    float4 baseColor = tex2D(snowTexture, uv);
    
    float3 normal = normalize(tex2D(snowNormalTexture, uv).xyz);
    float3 viewDirection = float3(0, 0, 1);
    float3 reflectionDirection = reflect(viewDirection, normal);
    float4 reflectedColor = CalculateScreenSpaceReflections(uv, reflectionDirection);
    
    float3 color = lerp(baseColor.rgb, reflectedColor.rgb, reflectivityInterpolant * baseColor.a);
    color += reflectedColor * reflectivityInterpolant * 0.45;
    
    return float4(color, 0) * sampleColor * baseColor.a * 1.2;
}

BEGIN_TECHNIQUE(Technique1)
    BEGIN_PASS(AutoloadPass) 
        PIXEL_SHADER(compile ps_3_0 PixelShaderFunction()) 
    END_PASS
END_TECHNIQUE
