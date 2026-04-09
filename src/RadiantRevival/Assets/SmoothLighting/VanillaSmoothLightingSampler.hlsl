#include "../common.h"

sampler2D Texture : register(s0);
sampler2D LightMap : register(s1);

#define TILE_SIZE (16.0)
#define OFFSCREEN_PADDING (offscreen_tiles * TILE_SIZE)

#define DOGSHIT_SLOP 0

TEXTURE_SIZE(LightingBufferSize, 1);
SCREEN_SIZE(ScreenSize);
SCREEN_POSITION(ScreenPosition);

float OffscreenTiles;
float GlobalBrightness;

// For cases where manual adjustment is needed.
float2 DrawOffset;
float DrawZoom;

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

float4 SmoothLightingShaderFragment(float2 svPos : SV_POSITION, float2 textureUv : TEXCOORD0, float4 color : COLOR0) : COLOR0
{
    float4 albedo = tex2D(Texture, textureUv) * color;
    
    float2 screenPosTiles = (svPos + DrawOffset / DrawZoom) / TILE_SIZE;
    screenPosTiles += OffscreenTiles;
    float2 lightUv = screenPosTiles / LightingBufferSize;
    {
        // Center and apply zoom.
        lightUv -= 0.5;
        lightUv *= DrawZoom;
        lightUv += 0.5;
    }
    
    float3 light = tex2D(LightMap, lightUv).rgb;
    light = saturate(light * GlobalBrightness);
    
#if DOGSHIT_SLOP
    float3 normal = compute_normal(textureUv);
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

BEGIN_TECHNIQUE(Technique1)
    BEGIN_PASS(SmoothLightingShader)
        PIXEL_SHADER(compile ps_3_0 SmoothLightingShaderFragment())
    END_PASS
END_TECHNIQUE