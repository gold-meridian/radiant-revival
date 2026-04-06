#include "../tmlbuild.h"
#include "../expressions.h"
#include "../spheres.h"

sampler2D uImage0 : register(s0);

int sample_count;

float2 blur_size;

float4 occlusion_color;

float4 main_horizontal(float2 uv : TEXCOORD0) : COLOR0
{
    float color = 0;
    
    int samples = max(sample_count, 4);
    
    int sampleHalf = samples / 2;
    
    float2 dtc = blur_size / samples;
    dtc.y = 0;
    
    [unroll(16)]
    for (int i = -sampleHalf; i <= sampleHalf; i++)
    {
        color += tex2D(uImage0, uv + dtc * i).a;
    }
    
    color /= samples;

    return color;
}

float4 main_vertical(float2 uv : TEXCOORD0) : COLOR0
{
    float color = 0;
    
    int samples = max(sample_count, 4);
    
    int sampleHalf = samples / 2;
    
    float2 dtc = blur_size / samples;
    dtc.x = 0;
    
    [unroll(16)]
    for (int i = -sampleHalf; i <= sampleHalf; i++)
    {
        color += tex2D(uImage0, uv + dtc * i).a;
    }
    
    color /= samples;
    
    return color;
}

#ifdef FX
technique Technique1
{
    pass HorizontalShader
    {
        PixelShader = compile ps_3_0 main_horizontal();
    }
    pass VerticalShader
    {
        PixelShader = compile ps_3_0 main_vertical();
    }
}
#endif // FX