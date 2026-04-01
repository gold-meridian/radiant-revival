#include "../tmlbuild.h"

sampler2D tex : register(s0);
sampler2D light_map : register(s1);

float screen_size_x SCREEN_SIZE_X;
float screen_size_y SCREEN_SIZE_Y;

float4 main(float2 pos : SV_POSITION, float2 uv : TEXCOORD0, float4 color : COLOR0) : COLOR0
{
    float4 albedo = tex2D(tex, uv) * color;
    
    float2 screen_uv = pos.xy / float2(screen_size_x, screen_size_y);
    float3 light = tex2D(light_map, screen_uv).rgb;
    
    return float4(albedo.rgb * light, albedo.a);
}

#ifdef FX
technique Technique1
{
    pass SmoothLightingShader
    {
        PixelShader = compile ps_3_0 main();
    }
}
#endif // FX