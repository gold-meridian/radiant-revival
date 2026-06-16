#include "../../../common.h"
#include "../../../spheres.h"

sampler2D SurfaceTexture : register(s0);

matrix Projection;

float4 DrawColor;

struct VSInput
{
    float4 Position : POSITION0;
    float3 Normal : NORMAL0;
};

struct PSInput
{
    float4 Position : SV_POSITION0;
    float3 Normal : NORMAL0;
};

PSInput EarthShaderVertex(in VSInput input)
{
    PSInput output = (PSInput)0;
    
    float4 pos = mul(input.Position, Projection);
    output.Position = pos;
    
    output.Normal = normalize(input.Normal);

    return output;
}

float4 EarthShaderFragment(in PSInput input) : COLOR0
{
    float3 normal = input.Normal;

    float theta = (atan2(normal.x, normal.z) / PI) * 0.5 + 0.5;
    float phi = (asin(-normal.y) / (PI * 0.5)) * 0.5 + 0.5;
    
    float2 uv = float2(-theta, phi);
    
    return tex2D(SurfaceTexture, uv) * DrawColor;
}

BEGIN_TECHNIQUE(Technique1)
    BEGIN_PASS(EarthShader)
        VERTEX_SHADER(compile vs_3_0 EarthShaderVertex())
        PIXEL_SHADER(compile ps_3_0 EarthShaderFragment())
    END_PASS
END_TECHNIQUE