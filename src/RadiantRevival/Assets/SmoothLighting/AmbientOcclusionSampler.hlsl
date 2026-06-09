#include "../common.h"

sampler2D WallTexture : register(s0);
sampler2D TileTexture : register(s1);

// float2 wall_tex_size TEXTURE_SIZE(0);
TEXTURE_SIZE(TileTextureSize, 1);

float4 OcclusionColor;
float2 TilePixelOffset;

float4 MaskShaderFragment(float2 wallTextureUv : TEXCOORD0) : COLOR0
{
    float4 mask = tex2D(WallTexture, wallTextureUv);
    
    float2 tileUv = wallTextureUv * TileTextureSize;
    tileUv += TilePixelOffset;
    tileUv /= TileTextureSize;
    float blur = tex2D(TileTexture, tileUv);
    
    float4 occColor = float4(OcclusionColor.rgb, 1.0);
    float4 color = lerp(mask, occColor, pow(blur, 2.4) * mask.a * OcclusionColor.a);
    return color;
}

BEGIN_TECHNIQUE(Technique1)
    BEGIN_PASS(MaskShader)
        PIXEL_SHADER(compile ps_3_0 MaskShaderFragment())
    END_PASS
END_TECHNIQUE