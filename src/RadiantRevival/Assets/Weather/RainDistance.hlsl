#include "../common.h"

sampler2D Texture : register(s0);

int SampleCount;
float SampleDistance;
float DrawZoom;

float4 RainDistanceShaderFragment(float2 textureUv : TEXCOORD0) : COLOR0
{
    float dtc = SampleDistance;
    
    float2 uv = textureUv;
    
    float4 tiles = tex2D(Texture, uv);
    
    float color = tiles.a;
    
    clip(color - 0.001);
    
    color *= (tiles.r + tiles.g + tiles.b) * 0.333;
    color = 1 - pow(1 - color, 5);
    
    [unroll(32)]
    for (int i = 0; i < SampleCount; i++)
    {
        uv.y += dtc;
        
        if (tex2D(Texture, uv).a == 0)
        {
            float dist = distance(textureUv.y, uv.y) * SampleCount / DrawZoom * 2;
            float4 output = float4(uv.y, color * pow(1 - dist, 2), 0, 0);
            
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