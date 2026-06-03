#include "../common.h"

sampler2D TileTexture : register(s0);

TEXTURE_SIZE(TileTextureSize, 0);

int SampleCount;
float2 BlurSize;

float4 RainBlurShaderFragment(float2 textureUv : TEXCOORD0) : COLOR0
{
    float2 dtc = BlurSize / TileTextureSize;
    
    float2 uv = textureUv;
    
    float color = tex2D(TileTexture, uv).a;
    
    [unroll(16)]
    for (int i = 0; i < SampleCount; i++)
    {
        uv += dtc;
        
        color += tex2D(TileTexture, uv).a;
    }
    
    return color;
}

BEGIN_TECHNIQUE(Technique1)
    BEGIN_PASS(RainBlurShader) 
        PIXEL_SHADER(compile ps_3_0 RainBlurShaderFragment())
    END_PASS
END_TECHNIQUE