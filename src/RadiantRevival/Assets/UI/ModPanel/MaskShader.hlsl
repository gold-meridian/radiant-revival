#include "../../common.h"

sampler2D PanelTexture : register(s0);
sampler2D TargetTexture : register(s1);

float4 PanelSource;

float4 MaskShaderFragment(float2 svPos : SV_POSITION, float2 panelUv : TEXCOORD0) : COLOR0
{
    float2 panelPosition = PanelSource.zw;
    float2 panelSize = PanelSource.xy;
    
    float2 targetUv = svPos;
    
    targetUv -= panelPosition;
    targetUv /= panelSize;
    
    float4 target = tex2D(TargetTexture, targetUv);
    
    float mask = tex2D(PanelTexture, panelUv);
    
    return target * mask;
}

BEGIN_TECHNIQUE(Technique1)
    BEGIN_PASS(MaskShader)
        PIXEL_SHADER(compile ps_3_0 MaskShaderFragment()) 
    END_PASS
END_TECHNIQUE