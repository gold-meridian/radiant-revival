#include "../../tmlbuild.h"
#include "../../expressions.h"

/*
sampler2D tex : register(s0);

texture uTexture;
sampler tex0 = sampler_state
{
    texture = <uTexture>;
    magfilter = POINT;
    minfilter = POINT;
    mipfilter = POINT;
    AddressU = wrap;
    AddressV = wrap;
};

float2 u_tex_size TEXTURE_SIZE(0);
float u_time GLOBAL_TIME;

float4 u_source;
float u_hover_intensity;

#define PIXEL_SIZE (2.0)
*/

sampler uImage0 : register(s0);

float uTime GLOBAL_TIME;
float4 uSource;
float uHoverIntensity;
float uPixel;
float uColorResolution;
float uGrayness;
float4 uColor;
float4 uSecondaryColor;
float uSpeed;
bool uSmallPanel;

// Main background, inverted stars in the highlight BG.
// static float4 main_bg_color = float4(6. / 255., 10. / 255., 145. / 255., 1.);

// Highlight BG at the bottom, stars in the regular BG.
// static float4 highlight_bg_color = float4(9. / 255., 12. / 255., 171. / 255., 1.);

// Used exclusively for darker stars at the top of the regular BG.
// static float4 dark_bg_color = float4(5. / 255., 9. / 255., 129. / 255., 1.);

/*
float4 main(float2 coords : SV_POSITION, float2 tex_coords : TEXCOORD0, float4 baseColor : COLOR0) : COLOR0
{
    return float4(coords.xy / 1000.0, 0.0, 1.0);
}
*/

float4 main(float2 coords : SV_POSITION, float2 tex_coords : TEXCOORD0, float4 baseColor : COLOR0) : COLOR0
{
    float2 resolution = uSource.xy;
    float2 position = uSource.zw;
    coords -= position;
    float2 uv = floor(coords / uPixel) / (float2(resolution.x, resolution.y) / uPixel);
    
    float redWobble = sin(uTime * uSpeed * 3.1415);
    float4 redFade = lerp(uColor * (1 + uHoverIntensity * 0.2), uSecondaryColor, uv.x * 2 - redWobble * 0.1);
    float4 panelColor = lerp(redFade, (uSmallPanel ? uColor : baseColor) - 0.05 + uHoverIntensity * 0.1, pow(uv.x, 1.2 + redWobble * 0.3));
    panelColor.rgb *= 1.2 + sin(uv.x * (uSmallPanel ? 1 : 11) + uv.y * 2 - uTime * uSpeed * 3.1415) * 0.3 * (1 - uv.x * 0.1);
    // float4 acolor = float4(uv.xy, 0., 1.);
    panelColor.rg = coords.xy;
    return tex2D(uImage0, tex_coords) * panelColor;
}

// float4 main(float4 coords : SV_POSITION, float2 tex_coords : TEXCOORD0) : COLOR0
// {
//     return float4(1., 0., 0., 1.);
//     
//     float4 map = tex2D(tex, tex_coords);
//     bool bevel = map.r > 0.;
//     float hover = bevel ? 1. : hover_intensity;
//     
//     float2 resolution = source.xy;
//     float2 position = source.zw;
//     {
//         coords -= position;
//     }
//     
//     // float2 pixel = floor(coords / PIXEL_SIZE.xx) / (resolution / PIXEL_SIZE.xx);
//     float2 pixel = resolution / PIXEL_SIZE.xx;
//     
//     float highlight_height = 30.0;
//     float base_line = source.y - highlight_height;
//     
//     float wave_width = 10.0;
//     float wave_gap = 2.0;
//     
//     float t = pixel.x / (wave_width + wave_gap);
//     float local = frac(t);
//     
//     float wave = 0.0;
//     /*
//     if (local < wave_width / (wave_width + wave_gap))
//     {
//         float x = local * (wave_width + wave_gap);
//         float centered = (x - wave_width * 0.5) / (wave_width * 0.5);
//         
//         // semicircle: sqrt(1 - x^2)
//         wave = sqrt(saturate(1.0 - centered * centered)) * 8.0;
//         
//         wave *= 0.8 + 0.2 * sin(pixel.x * 0.05 + time);
//     }
//     */
//     
//     float boundary = base_line - wave;
//     bool highlight = pixel.y > boundary || bevel;
//     return float4(1., 0., 0., 1.);
//     
//     float4 base_color = highlight ? highlight_bg_color : dark_bg_color;
//     
//     /*
//     float star_spacing = 6.0;
//     float2 star_cell = frac(pixel / star_spacing);
//     float2 star_local = abs(star_cell - 0.5);
//     bool star = (star_local.x < 0.08 && star_local.y < 0.25)
//              || (star_local.y < 0.08 && star_local.x < 0.25);
//     
//     bool top_band = pixel.y < 24.0;
//     
//     if (star)
//     {
//         if (top_band)
//         {
//             base_color = dark_bg_color;
//         }
//         else
//         {
//             base_color = highlight ? main_bg_color : highlight_bg_color;
//         }
//     }
//     */
//     
//     return base_color * map.a;
// }

#ifdef FX
technique Technique1
{
    pass PanelShader
    {
        PixelShader = compile ps_3_0 main();
    }
}
#endif // FX