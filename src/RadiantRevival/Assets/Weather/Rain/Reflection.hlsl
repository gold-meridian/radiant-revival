#include "../../common.h"

sampler2D ScreenTexture : register(s0);
sampler2D DistanceMap : register(s1);

SCREEN_SIZE(ScreenSize);
TEXTURE_SIZE(DistanceTextureSize, 1);

float Intensity;

float4 ReflectionShaderFragment(float2 uv : TEXCOORD0, float2 svPos : SV_POSITION, float4 baseColor : COLOR0) : COLOR0
{
    float2 map = tex2D(DistanceMap, uv);
    
    float reflectionLine = map.x;
    
    float alpha = map.y * (1 - pow(1 - Intensity, 5.4));
    
    float2 reflectedUv = uv;
    reflectedUv.y = (reflectionLine * 2) - reflectedUv.y;
    
    float4 screen = tex2D(ScreenTexture, uv);
    
    float4 reflectedScreen = tex2D(ScreenTexture, reflectedUv);
    reflectedScreen = 1 - pow(1 - reflectedScreen, 1.2);
    
    float4 color = lerp(screen, reflectedScreen, alpha);
    
    return color;
}

BEGIN_TECHNIQUE(Technique1)
    BEGIN_PASS(ReflectionShader)   
        PIXEL_SHADER(compile ps_3_0 ReflectionShaderFragment())  
    END_PASS
END_TECHNIQUE