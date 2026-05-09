#include "../common.h"

sampler2D ScreenTexture : register(s0);
sampler2D MaskTexture : register(s1);

TEXTURE_SIZE(ScreenTextureSize, 0);
TEXTURE_SIZE(MaskTextureSize, 1);

float Time;

float DrawZoom;
float2 DrawOffset;
float2 TilePixelOffset;

float4 RainDistortionShaderFragment(float2 textureUv : TEXCOORD0, float4 baseColor : COLOR0) : COLOR0
{
    float2 uv = textureUv;
    
    float2 maskUv = uv;
    {
        maskUv -= 0.5;
        maskUv *= DrawZoom;
        maskUv += 0.5;
    }
    maskUv *= ScreenTextureSize;
    
    maskUv -= DrawOffset;
    maskUv += TilePixelOffset;
    
    maskUv /= MaskTextureSize * 2;
    
    float4 color = lerp(tex2D(ScreenTexture, uv), float4(1, 0, 0, 1), tex2D(MaskTexture, maskUv));
    
    return color * baseColor;
}

BEGIN_TECHNIQUE(Technique1)
    BEGIN_PASS(RainDistortionShader)  
        PIXEL_SHADER(compile ps_3_0 RainDistortionShaderFragment()) 
    END_PASS
END_TECHNIQUE