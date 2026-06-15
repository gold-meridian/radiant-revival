#include "../../common.h"

sampler2D GradientTexture : register(s0);
sampler2D NoiseTexture : register(s1);

GLOBAL_TIME(Time);

float4 NebulaShaderFragment(float2 uv : TEXCOORD0, float4 baseColor : COLOR0) : COLOR0
{
    float gradient = tex2D(GradientTexture, uv).a;
    
    float2 noiseUv = float2(uv.x + (Time * 0.02), uv.y + (Time * 0.01));
    noiseUv *= 0.7;
    
    float2 noiseUv2 = float2(uv.x - (Time * 0.011), uv.y + (Time * 0.005));
    noiseUv2 *= 0.5;
    
    float noise = tex2D(NoiseTexture, noiseUv).r * tex2D(NoiseTexture, noiseUv2).r;
    
    noise = 1 - pow(1 - noise, 3);
    
    gradient += noise * gradient;
    
    float alpha = step(0.6, gradient);
    
    alpha *= 1 - pow(1 - saturate(uv * 3), 4);
    
    return baseColor * alpha;
}

BEGIN_TECHNIQUE(Technique1)
    BEGIN_PASS(NebulaShader) 
        PIXEL_SHADER(compile ps_3_0 NebulaShaderFragment())  
    END_PASS
END_TECHNIQUE