#include "../common.h"

sampler2D Texture : register(s0);

TEXTURE_SIZE(TextureSize, 0);

int SampleCount;

float4 RainDistanceShaderFragment(float2 textureUv : TEXCOORD0) : COLOR0
{
    float dtc = -2 / TextureSize.y;
    
    float2 uv = textureUv;
    
    float color = tex2D(Texture, uv).a;
    
    clip(color - 0.001);
    
    [unroll(32)]
    for (int i = 0; i < SampleCount; i++)
    {
        uv.y += dtc;
        
        if (tex2D(Texture, uv).a == 0)
        {
            float dist = distance(textureUv.y, uv.y) * SampleCount * 2;
            float4 output = float4(uv.y, color * (1 - dist), 0, 0);
            
            // Last two components are ignored
            return output;
        }
    }
    
    return 0;
}

BEGIN_TECHNIQUE(Technique1)
    BEGIN_PASS(RainDistanceShader)    
        PIXEL_SHADER(compile ps_3_0 RainDistanceShaderFragment())   
    END_PASS
END_TECHNIQUE