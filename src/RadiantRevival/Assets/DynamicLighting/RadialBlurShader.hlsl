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
    const float2 light_position = 0.5;

    float2 diff = light_position - textureUv;
    
    int samples = max(SampleCount, 4);
    
    float2 dtc = normalize(diff) / samples;
    dtc *= 0.1;
    
    float4 accumulated = 0;
    
    float2 offset = 0;
    
    [unroll(32)]
    for (int i = 0; i < samples; i++)
    {
        bool pastCenter = length(offset) > length(diff * 0.5f);
    
        float light = pastCenter
          ? 1
          : tex2D(LightTexture, textureUv + offset).r;
          
        float2 tileUv = textureUv + (offset / (ViewportSize / max(ViewportSize.y, ViewportSize.x)));
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
    
    return accumulated;
    
    float4 color = 0;
    color.rgb = baseColor.rgb * accumulated * baseColor.a;
    color.a = 0;
    
    return color;
}

BEGIN_TECHNIQUE(Technique1)
    BEGIN_PASS(RadialBlurShader)  
        PIXEL_SHADER(compile ps_3_0 RadialBlurShaderFragment())   
    END_PASS
END_TECHNIQUE