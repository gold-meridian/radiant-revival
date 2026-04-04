#include "../../tmlbuild.h"
#include "../../expressions.h"


sampler2D tex : register(s0);

float2 tex_size TEXTURE_SIZE(0);
float time GLOBAL_TIME;

float4 source;
float hover_intensity;

#define PIXEL_SIZE (2.0)

// Main background, inverted stars in the highlight BG.
static float4 main_bg_color = float4(6. / 255., 10. / 255., 145. / 255., 1.);

// Highlight BG at the bottom, stars in the regular BG.
static float4 highlight_bg_color = float4(9. / 255., 12. / 255., 171. / 255., 1.);

// Used exclusively for darker stars at the top of the regular BG.
static float4 dark_bg_color = float4(5. / 255., 9. / 255., 129. / 255., 1.);

float2 normalize_with_pixelation(float2 coords, float pixel_size, float2 resolution)
{
    return floor(coords / pixel_size) / (resolution / pixel_size);
}

float4 main(float2 tex_coords : TEXCOORD0, float4 base_color : COLOR0) : COLOR0
{
    float2 resolution = source.xy;
    float2 position = source.zw;
    float2 coords = tex_coords * resolution;
    float2 uv = normalize_with_pixelation(coords, PIXEL_SIZE, resolution);
    coords /= PIXEL_SIZE;
    
    return float4(uv, 0., 1.);
}

#ifdef FX
technique Technique1
{
    pass PanelShader
    {
        PixelShader = compile ps_3_0 main();
    }
}
#endif // FX