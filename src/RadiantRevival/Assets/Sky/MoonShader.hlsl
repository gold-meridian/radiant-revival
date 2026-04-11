#include "../common.h"
#include "../spheres.h"

sampler Texture : register(s0);
sampler NormalTexture : register(s1);

matrix Projection;
matrix ProjectionInverse;

float3 LightPosition;

struct VSInput
{
    float4 Position : POSITION0;
    float3 Normal : NORMAL0;
    float3 TextureCoordinates : TEXCOORD0;
};

struct PSInput
{
    float4 Position : SV_POSITION;
    float3 Normal : NORMAL0;
    float3 TextureCoordinates : TEXCOORD0;
};

PSInput MoonShaderVertex(in VSInput input)
{
    PSInput output = (PSInput)0;
    
    float4 pos = mul(input.Position, Projection);
    output.Position = pos;
    
    output.Normal = normalize(mul(input.Normal, ProjectionInverse));
    output.TextureCoordinates = input.TextureCoordinates;

    return output;
}

float4 MoonShaderFragment(in PSInput input) : COLOR0
{
    float4 color = tex2D(Texture, input.TextureCoordinates.xy);
    
    float2 lightDir = normalize(LightPosition - input.Position.xyz);
    
    color.rgb *= dot(input.Normal, LightPosition);

    return color;
}

BEGIN_TECHNIQUE(Technique1)
    BEGIN_PASS(MoonShader) 
        VERTEX_SHADER(compile vs_3_0 MoonShaderVertex()) 
        PIXEL_SHADER(compile ps_3_0 MoonShaderFragment())
    END_PASS
END_TECHNIQUE