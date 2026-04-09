#include "../../common.h"

sampler2D PanelTexture : register(s0);

TEXTURE_SIZE(PanelTextureSize, 0);
GLOBAL_TIME(Time);

float4 PanelSource;
float HoverIntensity;

#define PIXEL_SIZE (2.0)

// Main background, inverted stars in the highlight BG.
static float4 main_bg_color = float4(6. / 255., 10. / 255., 145. / 255., 1.);

// Highlight BG at the bottom, stars in the regular BG.
static float4 highlight_bg_color = float4(9. / 255., 12. / 255., 171. / 255., 1.);

// Used exclusively for darker stars at the top of the regular BG.
static float4 dark_bg_color = float4(5. / 255., 9. / 255., 129. / 255., 1.);

float4 main(float2 svPos : SV_POSITION, float2 panelUv : TEXCOORD0) : COLOR0
{
    float4 map = tex2D(PanelTexture, panelUv);
    bool bevel = map.r > 0.;
    float hover = bevel ? 1. : HoverIntensity;
    
    float2 resolution = PanelSource.xy;
    float2 position = PanelSource.zw;
    {
        svPos -= position;
    }
    
    float2 pixel_fuck_you = floor(svPos / PIXEL_SIZE) / (resolution / PIXEL_SIZE);
    float2 pixel = 1. * pixel_fuck_you;
    
    float highlight_height = 30.0;
    float base_line = PanelSource.y - highlight_height;
    
    float wave_width = 10.0;
    float wave_gap = 2.0;
    
    float t = pixel.x / (wave_width + wave_gap);
    float local = frac(t);
    
    float wave = 0.0;
    if (local < wave_width / (wave_width + wave_gap))
    {
        float x = local * (wave_width + wave_gap);
        float centered = (x - wave_width * 0.5) / (wave_width * 0.5);
        
        // semicircle: sqrt(1 - x^2)
        wave = sqrt(saturate(1.0 - centered * centered)) * 8.0;
        
        wave *= 0.8 + 0.2 * sin(pixel.x * 0.05 + Time);
    }
    
    float boundary = base_line - wave;
    bool highlight = pixel.y > boundary || bevel;
    
    float4 baseColor = highlight ? highlight_bg_color : dark_bg_color;
    
    float star_spacing = 6.0;
    float2 star_cell = frac(pixel / star_spacing);
    float2 star_local = abs(star_cell - 0.5);
    bool star = (star_local.x < 0.08 && star_local.y < 0.25)
             || (star_local.y < 0.08 && star_local.x < 0.25);
    
    bool top_band = pixel.y < 24.0;
    
    if (star)
    {
        if (top_band)
        {
            baseColor = dark_bg_color;
        }
        else
        {
            baseColor = highlight ? main_bg_color : highlight_bg_color;
        }
    }
    
    return baseColor * map.a;
}

BEGIN_TECHNIQUE(Technique1)
    BEGIN_PASS(PanelShader)
        PIXEL_SHADER(compile ps_3_0 main())
    END_PASS
END_TECHNIQUE