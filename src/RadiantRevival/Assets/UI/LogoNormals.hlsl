#include "../common.h"
#include "../spheres.h"

sampler2D tex : register(s0);

float screen_size_x SCREEN_SIZE_X;
float screen_size_y SCREEN_SIZE_Y;

float rotation;

float2 light_position;

float4 main(float2 pos : SV_POSITION, float2 uv : TEXCOORD0, float4 color : COLOR0) : COLOR0
{
    float2 screenSize = float2(screen_size_x, screen_size_y);
    
    const float depth = 0.0005;
    
    float3 dir = normalize(float3((light_position - pos) / screenSize, depth));
    
    float4 normal = tex2D(tex, uv);
    
    float3 sp = normal.rgb - .5;
    sp *= 2;
    
    sp.rgb = mul(sp.rgb, rotateZ(rotation));
    
    float4 light = color * dot(sp.rgb, dir);
    
    light *= normal.a;
    
    light.a = 0;
    
    return light;
}

BEGIN_TECHNIQUE(Technique1)
    BEGIN_PASS(LogoNormalsShader)
        PIXEL_SHADER(compile ps_3_0 main())
    END_PASS
END_TECHNIQUE