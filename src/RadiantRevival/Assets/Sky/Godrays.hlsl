#include "../tmlbuild.h"

sampler2D tex : register(s0);
sampler2D lights : register(s1);

float screen_size_x SCREEN_SIZE_X;
float screen_size_y SCREEN_SIZE_Y;

float2 light_position;

float light_size;

float decay_mult;

int sample_count;

static const float4x4 bayer =
    float4x4(0, 8, 2, 10,
             12, 4, 14, 6,
             3, 11, 1, 9,
             15, 7, 13, 5) / 16;

float godrays(float2 uv)
{
    float2 screenSize = float2(screen_size_x, screen_size_y);
    
    float2 light = light_position / screenSize;
    
    float2 screen = screenSize / min(screenSize.x, screenSize.y);
    
    float2 dir = (light - uv) * screen;
    
    int samples = max(sample_count, 4);
    
    float2 dtc = dir * (1.0 / samples);
    
    float size = 3.9 / max(light_size, 0.001);
    
    float occ = 0;
    
    float2 bayeruv = frac(uv * screenSize);
    float dither = bayer[bayeruv.x][bayeruv.y];
    
    float decay = 1;
    
    [unroll(64)]
    for (int i = 0; i < samples; i++)
    {
        uv += dtc;
        
        float l = tex2D(lights, uv + dither).a;
    
        l *= l;
        l = 1 - l;
    
        occ += (l - pow(tex2D(tex, uv).a, 2)) * decay;
        
        decay *= decay_mult;
    }
    
    occ /= samples;
    
    return occ;
}

float4 main(float2 uv : TEXCOORD0, float4 color : COLOR0) : COLOR0
{
    float gr = godrays(uv);
    
    gr = 1 - pow(1 - gr, 2);

    color *= gr;
    
    color.a = (color.r + color.g + color.b) * 0.333;
    
    return color;
}

#ifdef FX
technique Technique1
{
    pass GodraysShader
    {
        PixelShader = compile ps_3_0 main();
    }
}
#endif // FX