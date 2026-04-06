#include "../tmlbuild.h"

sampler2D wall_tex : register(s0);
sampler2D tile_tex : register(s1);

float screen_size_x SCREEN_SIZE_X;
float screen_size_y SCREEN_SIZE_Y;

float4 occlusion_color;

float4 main_mask(float2 uv : TEXCOORD0) : COLOR0
{
    float4 mask = tex2D(wall_tex, uv);
    float4 blur = tex2D(tile_tex, uv);
    
    float4 occColor = occlusion_color;
    occColor.a = 1;
    
    float4 color = lerp(mask, occColor, pow(blur.a, 2.4) * mask.a * occlusion_color.a);
    
    return color;
}

#ifdef FX
technique Technique1
{
    pass MaskShader
    {
        PixelShader = compile ps_2_0 main_mask();
    }
}
#endif // FX