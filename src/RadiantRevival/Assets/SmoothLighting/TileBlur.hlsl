#include "../tmlbuild.h"
#include "../expressions.h"
#include "../spheres.h"

sampler2D uImage0 : register(s0);

int sample_count;

float2 blur_size;

float4 occlusion_color;

float gaussian(float x)
{
    return exp(-x * x * PI);
}

float4 main_horizontal(float2 uv : TEXCOORD0) : COLOR0
{
    float4 color = 0;
    
    int samples = max(sample_count, 4);
    
    int sampleHalf = samples / 2;
    
    uv -= blur_size * 0.5;
    
    float2 dtc = blur_size / samples;
    dtc.y = 0;
    
    [unroll(16)]
    for (int i = -sampleHalf; i <= sampleHalf; i++)
    {
        color += gaussian(i / samples) * tex2D(uImage0, uv * dtc * i);
    }
    
    color /= samples;

    return color;
}

float4 main_vertical(float2 uv : TEXCOORD0) : COLOR0
{
    float4 color = 0;
    
    int samples = max(sample_count, 4);
    
    int sampleHalf = samples / 2;
    
    uv -= blur_size * 0.5;
    
    float2 dtc = blur_size / samples;
    dtc.x = 0;
    
    [unroll(16)]
    for (int i = -sampleHalf; i <= sampleHalf; i++)
    {
        uv += dtc;
    
        color += gaussian(i / samples) * tex2D(uImage0, uv);
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