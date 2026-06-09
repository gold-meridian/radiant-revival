#include "../common.h"
#include "../spheres.h"

sampler2D BaseTexture : register(s0);

int SampleCount;
float2 BlurSize;
float4 OcclusionColor;

float4 HorizontalShaderFragment(float2 baseTextureUv : TEXCOORD0) : COLOR0
{
    float color = 0;
    int samples = max(SampleCount, 4);
    int sampleHalf = samples / 2;
    
    float2 dtc = BlurSize / samples;
    dtc.y = 0;
    
    [unroll(16)]
    for (int i = -sampleHalf; i <= sampleHalf; i++)
    {
        color += tex2D(BaseTexture, baseTextureUv + dtc * i).a;
    }
    
    color /= samples;
    return color;
}

float4 VerticalShaderFragment(float2 baseTextureUv : TEXCOORD0) : COLOR0
{
    float color = 0;
    int samples = max(SampleCount, 4);
    int sampleHalf = samples / 2;
    
    float2 dtc = BlurSize / samples;
    dtc.x = 0;
    
    [unroll(16)]
    for (int i = -sampleHalf; i <= sampleHalf; i++)
    {
        color += tex2D(BaseTexture, baseTextureUv + dtc * i).a;
    }
    
    color /= samples;
    return color;
}

BEGIN_TECHNIQUE(Technique1)
    BEGIN_PASS(HorizontalShader)
        PIXEL_SHADER(compile ps_3_0 HorizontalShaderFragment())
    END_PASS
    BEGIN_PASS(VerticalShader)
        PIXEL_SHADER(compile ps_3_0 VerticalShaderFragment())
    END_PASS
END_TECHNIQUE
