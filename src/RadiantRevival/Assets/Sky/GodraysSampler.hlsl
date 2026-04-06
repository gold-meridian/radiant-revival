#include "../tmlbuild.h"

sampler2D tex : register(s0);

float screen_size_x SCREEN_SIZE_X;
float screen_size_y SCREEN_SIZE_Y;

float2 light_position;

float blur_strength;

int sample_count;

float4 main(float2 uv : TEXCOORD0) : COLOR0
{
    float2 screenSize = float2(screen_size_x, screen_size_y);
    
    float2 light = light_position / screenSize;
    
    int samples = max(sample_count, 4);
    
    float2 dir = (light - uv);
    
    float2 dtc = dir * (1.0 / samples) * blur_strength;
    
    float4 color = 0;
    
    uv -= dtc * samples * 0.5;
    
    [unroll(32)]
    for (int i = 0; i < samples; i++)
    {
        uv += dtc;
        
        color += tex2D(tex, uv);
    }
    
    color /= samples;
    
    return color;
}

#ifdef FX
technique Technique1
{
    pass RadialBlurShader
    {
        PixelShader = compile ps_3_0 main();
    }
}
#endif // FX