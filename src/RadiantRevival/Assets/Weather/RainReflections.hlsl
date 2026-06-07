#include "../common.h"

sampler2D ScreenTexture : register(s0);
sampler2D DistanceMap : register(s1);

SCREEN_SIZE(ScreenSize);
TEXTURE_SIZE(DistanceTextureSize, 1);

float Intensity;

float4 RainReflectionsShaderFragment(float2 uv : TEXCOORD0, float2 svPos : SV_POSITION, float4 baseColor : COLOR0) : COLOR0
{
    float2 map = tex2D(DistanceMap, uv);
    
    float reflectionLine = map.x;
    
    float alpha = map.y * (1 - pow(1 - Intensity, 5.4));
    
    float2 reflectedUv = uv;
    reflectedUv.y = (reflectionLine * 2) - reflectedUv.y;
    
    // Arbitrary offset, looks nicer
    reflectedUv.y += 5 / ScreenSize.y;
    
    float4 color = lerp(tex2D(ScreenTexture, uv), tex2D(ScreenTexture, reflectedUv), alpha);
    
    return color;
}

BEGIN_TECHNIQUE(Technique1)
    BEGIN_PASS(RainReflectionsShader)   
        PIXEL_SHADER(compile ps_3_0 RainReflectionsShaderFragment())  
    END_PASS
END_TECHNIQUE