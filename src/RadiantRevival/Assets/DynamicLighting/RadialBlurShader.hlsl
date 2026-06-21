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

float2 UvToScreenSpace(float2 svPos, float2 textureUv, float2 uv)
{
    float2 topLeft = svPos - (textureUv * LightTextureSize);

    return topLeft + (uv * LightTextureSize);
}

float4 RadialBlurShaderFragment(float2 svPos : SV_POSITION0, float2 textureUv : TEXCOORD0, float4 baseColor : COLOR0) : COLOR0
{
    const float2 light_position = 0.5;

    float2 diff = (light_position - textureUv);
    
    float size = baseColor.a;
    
    float2 dtc = (normalize(diff) / SampleCount) / SampleCount;
    
    float occ = 0;
    
    float2 offset = 0;
    
    [unroll(16)]
    for (int i = 0; i < SampleCount; i++)
    {
        /*if (length(offset) > length(diff))
        {
            occ += SampleCount - i;
            
            break;
        }*/
        
        float light = tex2D(LightTexture, textureUv + offset).r;
        
        float2 tileUv = textureUv + (offset / (ViewportSize / max(ViewportSize.y, ViewportSize.x)));
        
        tileUv = UvToScreenSpace(svPos, textureUv, tileUv) / ViewportSize;
    
        occ += (1 - (tex2D(TileTexture, tileUv).a * TileOcclusionStrength));
        
        offset += dtc;
    }
    
    occ /= SampleCount;
    
    return abs(occ);  // (1 - length(diff));
    
    float4 color = 0;
    
    color.rgb = baseColor.rgb * occ;
    color.a = 0;
    
    return color;
}

BEGIN_TECHNIQUE(Technique1)
    BEGIN_PASS(RadialBlurShader)  
        PIXEL_SHADER(compile ps_3_0 RadialBlurShaderFragment())   
    END_PASS
END_TECHNIQUE