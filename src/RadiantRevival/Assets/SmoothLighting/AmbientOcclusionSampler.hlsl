#include "../common.h"

sampler2D wall_tex : register(s0);
sampler2D tile_tex : register(s1);

// float2 wall_tex_size TEXTURE_SIZE(0);
float2 tile_tex_size TEXTURE_SIZE(1);

float4 occlusion_color;
float2 tex_pixel_offset;

float4 main_mask(float2 uv : TEXCOORD0) : COLOR0
{
    float4 mask = tex2D(wall_tex, uv);
    
    float2 tile_uv = uv * tile_tex_size;
    tile_uv += tex_pixel_offset;
    tile_uv /= tile_tex_size;
    float blur = tex2D(tile_tex, tile_uv);
    
    float4 occ_color = float4(occlusion_color.rgb, 1.0);
    float4 color = lerp(mask, occ_color, pow(blur, 2.4) * mask.a * occlusion_color.a);
    return color;
}

BEGIN_TECHNIQUE(Technique1)
    BEGIN_PASS(MaskShader)
        PIXEL_SHADER(compile ps_3_0 main())
    END_PASS
END_TECHNIQUE