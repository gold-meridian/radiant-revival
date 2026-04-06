#include "../tmlbuild.h"
#include "../expressions.h"

sampler2D wall_tex : register(s0);
sampler2D tile_tex : register(s1);

float4 occlusion_color;

float2 tile_tex_size TEXTURE_SIZE(1);

float2 draw_offset;

float4 main_mask(float2 uv : TEXCOORD0) : COLOR0
{
    float4 mask = tex2D(wall_tex, uv);
    
    float2 bluruv = ceil((uv - draw_offset) * tile_tex_size) / tile_tex_size;
    
    float blur = tex2D(tile_tex, bluruv + (draw_offset));
    
    float4 occColor = occlusion_color;
    occColor.a = 1;
    
    float4 color = lerp(mask, occColor, pow(blur, 2.4) * mask.a * occlusion_color.a);
    
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