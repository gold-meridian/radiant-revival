#include "../common.h"

sampler2D TileTexture : register(s0);
sampler2D UnpaintedTexture : register(s1);

#define EPSILON (1e-10)

#define POSTERIZATION_STEPS (4)

float MinSat;
float MaxSat;

float MinHue;
float MaxHue;

float4 LightColor;
float2 LightPosition;

float4 Source;

float DrawZoom;

SCREEN_SIZE(ScreenSize)

float3 RGBtoHCV(float3 color)
{
    float4 p = color.g < color.b
        ? float4(color.bg, -1, 0.6666)
        : float4(color.gb, 0, -0.3333);
    
    float4 q = color.r < p.x
        ? float4(p.xyw, color.r)
        : float4(color.r, p.yzx);
    
    float C = q.x - min(q.w, q.y);
    
    float hue = abs((q.w - q.y) / (6 * C + EPSILON) + q.z);
    
    return float3(hue, C, q.x);
}

float3 RGBtoHSL(float3 color)
{
    float3 hcv = RGBtoHCV(color);
    
    float l = hcv.z - hcv.y * 0.5;
    float s = hcv.y / (1 - abs((l * 2) - 1) + EPSILON);
    
    return float3(hcv.x, s, l);
}

float Map(float value, float start1, float stop1, float start2, float stop2)
{
    value = clamp(value, start1, stop1);
    return start2 + (stop2 - start2) * ((value - start1) / (stop1 - start1));
}

float4 NatureLightingShaderFragment(float2 svPos : SV_POSITION, float2 textureUv : TEXCOORD0, float4 baseColor : COLOR0) : COLOR0
{
    float4 base = tex2D(TileTexture, textureUv) * baseColor;
    
    float4 unpainted = tex2D(UnpaintedTexture, textureUv);
    
    float3 hsl = RGBtoHSL(unpainted.rgb);
    
    bool inRange =
        hsl.x > MinHue && hsl.x < MaxHue
     && hsl.y > MinSat && hsl.y < MaxSat;
    
    float lightness = (hsl.z - 0.2) * 1.5;
    
    lightness = 1 - lightness;
    
    float2 lightPos = LightPosition / ScreenSize;
    {
        lightPos -= 0.5;
        lightPos *= DrawZoom;
        lightPos += 0.5;
    }
    
    float2 lightUv = svPos / ScreenSize;
    {
        lightUv -= 0.5;
        lightUv *= DrawZoom;
        lightUv += 0.5;
    }
    
    float2 lightDirection = normalize(lightUv - lightPos);
    
    float2 topLeft = Source.zw / ScreenSize;
    
    lightUv = (lightUv - topLeft) / (Source.xy / ScreenSize);
    lightUv -= 0.5 - (lightDirection * 0.2);
    lightUv *= 3;
    
    float lightFactor = 1 - pow(saturate(dot(lightDirection, lightUv) + (0.5 * length(lightUv))), 2);
    
    lightness = pow(lightness, 7);
    
    lightness *= lightFactor;
    
    lightness = floor(lightness * POSTERIZATION_STEPS) / POSTERIZATION_STEPS;
    
    float4 light = LightColor * saturate(lightness) * inRange * unpainted.a * LightColor.a;
    
    return base + light;
}

BEGIN_TECHNIQUE(Technique1)
    BEGIN_PASS(NatureLightingShader) 
        PIXEL_SHADER(compile ps_3_0 NatureLightingShaderFragment()) 
    END_PASS
END_TECHNIQUE