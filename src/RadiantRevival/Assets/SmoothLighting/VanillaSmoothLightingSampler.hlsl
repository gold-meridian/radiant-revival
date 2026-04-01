#include "../tmlbuild.h"
#include "../expressions.h"

sampler2D tex : register(s0);
sampler2D light_map : register(s1);

#define OFFSCREEN_TILES (1.0)
#define TILE_SIZE (16.0)
#define OFFSCREEN_PADDING (OFFSCREEN_TILES * TILE_SIZE)

float2 lighting_buffer_size TEXTURE_SIZE(1);
float2 screen_position SCREEN_POSITION;
float screen_size_x SCREEN_SIZE_X;
float screen_size_y SCREEN_SIZE_Y;

float4 main(float2 pos : SV_POSITION, float2 uv : TEXCOORD0, float4 color : COLOR0) : COLOR0
{
    float4 albedo = tex2D(tex, uv) * color;
    
    /*
    float2 world_pos_pixels = pos + screen_position;
    float2 world_pos_tiles = world_pos_pixels / TILE_SIZE;
    float2 buffer_tile_offset = screen_position / TILE_SIZE - OFFSCREEN_PADDING;
    
    float2 light_uv = (tile_pos - buffer_tile_offset) / (lighting_buffer_size * TILE_SIZE);
    */
    
    float2 screen_pos_tiles = pos / TILE_SIZE;
    screen_pos_tiles += OFFSCREEN_TILES;
    float2 light_uv = screen_pos_tiles / lighting_buffer_size;
    
    float3 light = tex2D(light_map, light_uv).rgb;
    
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