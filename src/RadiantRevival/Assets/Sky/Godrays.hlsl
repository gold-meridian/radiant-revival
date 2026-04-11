#include "../common.h"

sampler2D OccludersTexture : register(s0);
sampler2D LightsTexture : register(s1);

SCREEN_SIZE(ScreenSize);

float2 LightPosition;
float DecayMult;
int SampleCount;

static const float4x4 bayer =
    float4x4(0, 8, 2, 10,
             12, 4, 14, 6,
             3, 11, 1, 9,
             15, 7, 13, 5) / 16;

float godrays(float2 uv)
{
    float2 light = LightPosition / ScreenSize;
    float2 dir = light - uv;
    int samples = max(SampleCount, 4);
    float2 dtc = dir * (1.0 / samples);
    float occ = 0;
    float2 bayerUv = frac(uv * ScreenSize / 16) * 4;
    float dither = bayer[bayerUv.x][bayerUv.y];
    float decay = 1;
    
    [unroll(64)]
    for (int i = 0; i < samples; i++)
    {
        uv += dtc;
        
        float l = tex2D(LightsTexture, uv + (dtc * dither)).a;
        l *= l;
        l = 1 - l;
    
        occ += (l - pow(tex2D(OccludersTexture, uv + (dtc * dither)).a, 2)) * decay;
        decay *= DecayMult;
    }
    
    occ /= samples;
    return occ;
}

float4 GodraysShaderFragment(float2 uv : TEXCOORD0, float4 color : COLOR0) : COLOR0
{
    float gr = godrays(uv);
    gr = 1 - pow(1 - gr, 2);
    color *= gr;
    color.a = (color.r + color.g + color.b) * 0.333;
    return color;
}

BEGIN_TECHNIQUE(Technique1)
    BEGIN_PASS(GodraysShader)
        PIXEL_SHADER(compile ps_3_0 GodraysShaderFragment())
    END_PASS
END_TECHNIQUE