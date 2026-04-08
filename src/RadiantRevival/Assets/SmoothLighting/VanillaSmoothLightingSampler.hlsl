#include "../tmlbuild.h"
#include "../expressions.h"

sampler2D tex : register(s0);
sampler2D light_map : register(s1);

#define TILE_SIZE (16.0)
#define OFFSCREEN_PADDING (offscreen_tiles * TILE_SIZE)

#define DOGSHIT_SLOP false

float2 lighting_buffer_size TEXTURE_SIZE(1);
float2 screen_position SCREEN_POSITION;
float screen_size_x SCREEN_SIZE_X;
float screen_size_y SCREEN_SIZE_Y;

float offscreen_tiles;
float global_brightness;

// For cases where manual adjustment is needed.
float2 draw_offset;
float draw_zoom;

#if DOGSHIT_SLOP
float luminance(float3 c)
{
    return dot(c, float3(0.299, 0.587, 0.114));
}

float height(float4 texel)
{
    float h = texel.a;
    h = lerp(h, luminance(texel.rgb), 0.35);
    h = pow(h, 1.5);
    return h;
}

float3 compute_normal(float2 uv)
{
    float2 texel = 1.0 / float2(screen_size_x, screen_size_y);

    float h_c = height(tex2D(tex, uv));
    float h_l = height(tex2D(tex, uv - float2(texel.x, 0)));
    float h_r = height(tex2D(tex, uv + float2(texel.x, 0)));
    float h_u = height(tex2D(tex, uv - float2(0, texel.y)));
    float h_d = height(tex2D(tex, uv + float2(0, texel.y)));

    float dx = (h_r - h_l) * 0.5;
    float dy = (h_d - h_u) * 0.5;

    float edge = abs(h_c - h_l) + abs(h_c - h_r) + abs(h_c - h_u) + abs(h_c - h_d);
    float strength = 3.0 + edge * 6.0;

    float3 n = normalize(float3(-dx * strength, -dy * strength, 1.0));
    return n;
}
#endif

float4 main(float2 pos : SV_POSITION, float2 uv : TEXCOORD0, float4 color : COLOR0) : COLOR0
{
    float4 albedo = tex2D(tex, uv) * color;
    
    float2 screen_pos_tiles = (pos + draw_offset / draw_zoom) / TILE_SIZE;
    screen_pos_tiles += offscreen_tiles;
    float2 light_uv = screen_pos_tiles / lighting_buffer_size;

    // Center and apply zoom.
    light_uv -= 0.5;
    light_uv *= draw_zoom;
    light_uv += 0.5;
    
    float3 light = tex2D(light_map, light_uv).rgb;
    light = saturate(light * global_brightness);
    
#if DOGSHIT_SLOP
    float3 normal = compute_normal(uv);
    float3 light_dir = normalize(float3(0.4, -0.6, 1.0));
    float n_dot_l = saturate(dot(normal, light_dir));
    float3 diffuse = saturate(n_dot_l * 0.7 + 0.3);
    float ambient = 0.0;
    light *= ambient + diffuse;
    
    float rim = pow(1.0 - saturate(dot(normal, float3(0,0,1))), 2.0);
    light += rim * 0.25;
    
    float3 viewDir = float3(0, 0, 1);
    float3 halfDir = normalize(light_dir + viewDir);
    float spec = pow(saturate(dot(normal, halfDir)), 32.0);
    light += spec * 0.3;
#endif
    
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