#include "../spheres.h"

sampler uImage0 : register(s0);

float shadowRotation;

float4 shadowColor;

float4 atmoColor;
float4 atmoShadowColor;

float radius;

float map(float value, float start1, float stop1, float start2, float stop2)
{
    value = clamp(value, start1, stop1);
    return start2 + (stop2 - start2) * ((value - start1) / (stop1 - start1));
}

float4 planet(float dist, float3 sp)
{
    float2 pt = lonlat(sp);
    
    float falloff = step(dist, radius);
    
    float light = dot(sp, mul(float3(0, 1, 0), rotateZ(TAU - PIOVER2 + shadowRotation)));
    
    light = 1 - pow(1 - map(light, -0.03, 0.4, 0, 1), 3.2);
    
    return lerp(shadowColor, tex2D(uImage0, pt), light) * falloff;
}

float4 atmo(float dist, float3 sp)
{
    float atmo = map(dist, radius, 1, 1, 0) * step(radius, dist);
	
    float light = dot(sp, mul(float3(0, 1, 0), rotateZ(TAU - PIOVER2 + shadowRotation)));
    
    light = 1 - pow(1 - map(light, -0.08, 0.34, 0, 1), 2);
    
    float4 col = lerp(atmoShadowColor, atmoColor * tex2D(uImage0, 0.5), light);
	
    return col * atmo;
}

float4 main(float2 uv : TEXCOORD0, float4 baseColor : COLOR0) : COLOR0
{
    uv -= 0.5;
    uv *= 2;
    
    float dist = length(uv);
    
    clip(1 - dist);
    
    if (dist > radius)
    {
        float3 sp = sphere(uv, dist, 1);
		
        float4 color = atmo(dist, sp);
        
        color *= color.a;
        
        color.a = 0;
        
        return color * baseColor;
    }
    else
    {
        float3 sp = sphere(uv, dist, radius);
		
        float4 color = planet(dist, sp);
		
        return color * color.a * baseColor;
    }
}

#ifdef FX
technique Technique1
{
    pass MoonShader
    {
        PixelShader = compile ps_3_0 main();
    }
}
#endif // FX
