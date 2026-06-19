#include "../../common.h"

sampler2D baseTexture : register(s1);
sampler2D normalTexture : register(s2);

float4x4 viewProjectionMatrix;

struct VertexShaderInput
{
    float4 Position : POSITION0;
    float4 Color : COLOR0;
    float4 Rotation : TEXCOORD0;
    float2 TextureCoordinates : TEXCOORD1;
};

struct VertexShaderOutput
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR0;
    float4 Rotation : TEXCOORD0;
    float2 TextureCoordinates : TEXCOORD1;
    float3 PositionCopy : TEXCOORD2;
};

struct PixelShaderOutput
{
    float4 Color : SV_Target0;
    float4 Normal : SV_Target1;
    float4 Position : SV_Target2;
};

VertexShaderOutput VertexShaderFunction(in VertexShaderInput input)
{
    VertexShaderOutput output = (VertexShaderOutput)0;
    output.Position = mul(input.Position, viewProjectionMatrix);
    output.PositionCopy = output.Position;
    output.Color = input.Color;
    output.Rotation = input.Rotation;
    output.TextureCoordinates = input.TextureCoordinates;

    return output;
}

float3 QuaternionRotate(float3 v, float4 rotation)
{
    float a = (rotation.y * v.z - rotation.z * v.y) * 2;
    float b = (rotation.z * v.x - rotation.x * v.z) * 2;
    float c = (rotation.x * v.y - rotation.y * v.x) * 2;
    float3 result;
    
    result.x = v.x + a * rotation.w + (rotation.y * c - rotation.z * b);
    result.y = v.y + b * rotation.w + (rotation.z * a - rotation.x * c);
    result.z = v.z + c * rotation.w + (rotation.x * b - rotation.y * a);
    
    return result;
}

PixelShaderOutput PixelShaderFunction(VertexShaderOutput input)
{
    float2 uv = input.TextureCoordinates;
    
    float4 baseResult = tex2D(baseTexture, uv);
    float4 coloredResult = baseResult * input.Color;
    float4 normalResult = float4(normalize(tex2D(normalTexture, uv).xyz * 2 - 1), 1);
    normalResult.xyz = QuaternionRotate(normalResult.xyz, input.Rotation);
    normalResult *= coloredResult.a;
    
    PixelShaderOutput result = (PixelShaderOutput)0;
    result.Color = tex2D(baseTexture, uv) * input.Color;
    result.Normal = normalResult;
    result.Position = float4(input.PositionCopy, 0);
    
    return result;
}

BEGIN_TECHNIQUE(Technique1)
    BEGIN_PASS(AutoloadPass)
        VERTEX_SHADER(compile vs_3_0 VertexShaderFunction())
        PIXEL_SHADER(compile ps_3_0 PixelShaderFunction())
    END_PASS
END_TECHNIQUE