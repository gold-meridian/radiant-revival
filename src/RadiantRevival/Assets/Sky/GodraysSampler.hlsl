#include "../common.h"

sampler2D Texture : register(s0);

SCREEN_SIZE(ScreenSize);

float2 LightPosition;
float BlurStrength;
int SampleCount;

float4 RadialBlurShaderFragment(float2 textureUv : TEXCOORD0) : COLOR0
{
    float2 light = LightPosition / ScreenSize;
    int samples = max(SampleCount, 4);
    float2 dir = light - textureUv;
    float2 dtc = dir * (1.0 / samples) * BlurStrength;
    float4 color = 0;
    
    textureUv -= dtc * samples * 0.5;
    
    [unroll(32)]
    for (int i = 0; i < samples; i++)
    {
        textureUv += dtc;
        color += tex2D(Texture, textureUv);
    }
    
    color /= samples;
    return color;
}

BEGIN_TECHNIQUE(Technique1)
    BEGIN_PASS(RadialBlurShader)
        PIXEL_SHADER(compile ps_3_0 RadialBlurShaderFragment())
    END_PASS
END_TECHNIQUE