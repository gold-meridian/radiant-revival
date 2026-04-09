#include "../common.h"
#include "../spheres.h"

sampler2D Logo : register(s0);

SCREEN_SIZE(ScreenSize);

float Rotation;
float2 LightPosition;

float4 LogoNormalsShaderFragment(float2 svPos : SV_POSITION, float2 logoUv : TEXCOORD0, float4 color : COLOR0) : COLOR0
{
    const float depth = 0.0005;
    
    float3 dir = normalize(float3((LightPosition - svPos) / ScreenSize, depth));
    float4 normal = tex2D(Logo, logoUv);
    float3 sp = normal.rgb - .5;
    sp *= 2;
    sp.rgb = mul(sp.rgb, rotateZ(Rotation));
    
    float4 light = color * dot(sp.rgb, dir);
    light *= normal.a;
    light.a = 0;
    return light;
}

BEGIN_TECHNIQUE(Technique1)
    BEGIN_PASS(LogoNormalsShader)
        PIXEL_SHADER(compile ps_3_0 LogoNormalsShaderFragment())
    END_PASS
END_TECHNIQUE