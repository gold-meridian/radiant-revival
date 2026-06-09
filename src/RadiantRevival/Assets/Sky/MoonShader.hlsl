#include "../common.h"
#include "../spheres.h"
#include "../colors.h"

sampler MoonTexture : register(s0);

float ShadowRotation;
float4 ShadowColor;
float4 AtmoColor;
float4 AtmoShadowColor;
float Radius;

float Map(float value, float start1, float stop1, float start2, float stop2)
{
    value = clamp(value, start1, stop1);
    return start2 + (stop2 - start2) * ((value - start1) / (stop1 - start1));
}

float4 Planet(float dist, float3 sp)
{
    float2 pt = lonlat(sp);
    float falloff = step(dist, Radius);
    float light = dot(sp, mul(float3(0, 1, 0), rotateZ(TAU - PIOVER2 + ShadowRotation)));
    light = 1 - pow(1 - Map(light, -0.03, 0.4, 0, 1), 3.2);
    return lerp(ShadowColor, tex2D(MoonTexture, pt), light) * falloff;
}

float4 Atmo(float dist, float3 sp)
{
    float atmo = pow(Map(dist, Radius, 1, 1, 0), 2.5) * step(Radius, dist);
    float light = dot(sp, mul(float3(0, 1, 0), rotateZ(TAU - PIOVER2 + ShadowRotation)));
    light = 1 - pow(1 - Map(light, -0.09, 0.34, 0, 1), 2);
    float4 col = oklabLerp(AtmoShadowColor, AtmoColor * tex2D(MoonTexture, 0.5), light);
    return col * atmo;
}

float4 MoonShaderFragment(float2 moonUv : TEXCOORD0, float4 baseColor : COLOR0) : COLOR0
{
    moonUv -= 0.5;
    moonUv *= 2;
    
    float dist = length(moonUv);
    
    clip(1 - dist);
    
    if (dist > Radius)
    {
        float3 sp = sphere(moonUv, dist, Radius);
        float4 color = Atmo(dist, sp);
        color *= color.a;
        color.a = 0;
        return color * baseColor;
    }
    
    float3 sp = sphere(moonUv, dist, Radius);
    float4 color = Planet(dist, sp);
    return color * color.a * baseColor;
}

BEGIN_TECHNIQUE(Technique1)
    BEGIN_PASS(MoonShader)
        PIXEL_SHADER(compile ps_3_0 MoonShaderFragment())
    END_PASS
END_TECHNIQUE
