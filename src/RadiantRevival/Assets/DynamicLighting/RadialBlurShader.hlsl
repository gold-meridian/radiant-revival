#include "../common.h"

sampler2D LightTexture : register(s0);
sampler2D TileTexture : register(s1);

int SampleCount;
float DecayMult;

float TileOcclusionStrength;

TEXTURE_SIZE(LightTextureSize, 0)

VIEWPORT_SIZE(ViewportSize)

float Map(float value, float start1, float stop1, float start2, float stop2)
{
    value = clamp(value, start1, stop1);
    return start2 + (stop2 - start2) * ((value - start1) / (stop1 - start1));
}

float2 LightUvToTileUv(float2 svPos, float2 textureUv, float2 uv)
{
    float2 topLeft = svPos - (textureUv * LightTextureSize);

    return (topLeft + (uv * LightTextureSize)) / ViewportSize;
}

float4 RadialBlurShaderFragment(float2 svPos : SV_POSITION0, float2 textureUv : TEXCOORD0, float4 baseColor : COLOR0) : COLOR0
{
    const float magic_number = 0.1;
    
    const float2 light_position = 0.5;

    float2 diff = light_position - textureUv;
    
    int samples = max(SampleCount, 4);
    
    float2 dtc = normalize(diff) / samples;
    dtc *= magic_number * baseColor.a;
    
    float accumulated = 0;
    
    float2 offset = 0;
    
    [unroll(32)]
    for (int i = 0; i < samples; i++)
    {
        bool pastCenter = length(offset) > (length(diff) * magic_number * baseColor.a);
    
        float light = pastCenter
          ? 1
          : tex2D(LightTexture, textureUv + offset).r;
          
        light = 1;
          
        float2 tileUv = textureUv + offset;
        tileUv = LightUvToTileUv(svPos, textureUv, pastCenter ? 0.5 : tileUv);
        
        float occ = tex2D(TileTexture, tileUv).a;
        
        if (!pastCenter)
        {
            light -= occ;
        }
    
        accumulated += light;
    
        if (!pastCenter)
        {
            offset += dtc * (1 - (occ * TileOcclusionStrength));
        }
    }
    
    accumulated /= samples;
    
    float4 color = baseColor;
    color.rgb *= accumulated * color.a * (1 - length(diff * 2));
    
    return color;
}

BEGIN_TECHNIQUE(Technique1)
    BEGIN_PASS(RadialBlurShader)  
        PIXEL_SHADER(compile ps_3_0 RadialBlurShaderFragment())   
    END_PASS
END_TECHNIQUE