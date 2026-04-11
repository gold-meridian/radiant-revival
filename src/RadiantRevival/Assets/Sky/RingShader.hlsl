#include "../common.h"

sampler RingTexture : register(s0);

float ShadowRotation;
float4 ShadowColor;

float2 Rotate(float2 coords, float2 center, float angle)
{
    float2 translatedCoords = coords - center;
    float c = cos(angle);
    float s = sin(angle);
    float2x2 mat = float2x2(float2(c, s), float2(-s, c));
    
    float2 rotatedCoords;
    rotatedCoords.x = dot(translatedCoords, mat[0].xy);
    rotatedCoords.y = dot(translatedCoords, mat[1].xy);
    return rotatedCoords;
}

float4 RingShaderFragment(float2 ringUv : TEXCOORD0, float4 baseColor : COLOR0) : COLOR0
{
    ringUv = Rotate(ringUv, 0.5, ShadowRotation);
    
    float4 rings = tex2D(RingTexture, saturate(ringUv + 0.5)) * baseColor;
    float light = saturate(abs(ringUv.x * 3.4f));
    light = 1 - pow(2, 15 * (light - 1));
    light *= step(0, ringUv.y);
    return lerp(rings, ShadowColor * rings, light);
}

BEGIN_TECHNIQUE(Technique1)
    BEGIN_PASS(RingShader)
        PIXEL_SHADER(compile ps_3_0 RingShaderFragment())
    END_PASS
END_TECHNIQUE
