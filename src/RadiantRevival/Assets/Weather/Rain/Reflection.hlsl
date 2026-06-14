#include "../../common.h"

// blur
#define ENABLE_REFLECTION_BLUR 0
#define REFLECTION_BLUR_RADIUS (2.0)

// reflection strength
#define REFLECTION_STRENGTH (1.0)

// distortion
#define ENABLE_DISTORTION 1
#define DISTORTION_X_AMPLITUDE (0.0015)
#define DISTORTION_Y_AMPLITUDE (0.0015)
#define DISTORTION_X_FREQUENCY (120.0)
#define DISTORTION_Y_FREQUENCY (80.0)
#define DISTORTION_SPEED (1.5)

// try to preserve local contrast
#define ENABLE_EDGE_PRESERVATION 0
#define EDGE_PRESERVATION_STRENGTH (1.0)

sampler2D ScreenTexture : register(s0);
sampler2D DistanceMap : register(s1);

SCREEN_SIZE(ScreenSize);
TEXTURE_SIZE(DistanceTextureSize, 1);
GLOBAL_TIME(Time);

float Intensity;
float DrawZoom;

float4 ReflectionShaderFragment(float2 uv : TEXCOORD0, float2 svPos : SV_POSITION, float4 baseColor : COLOR0) : COLOR0
{
    float2 pixel = 2.0 / ScreenSize;
    {
        pixel *= 1. / DrawZoom;
    }
    
    float2 mapUv = uv;
    {
        mapUv -= 0.5;
        mapUv *= DrawZoom;
        mapUv += 0.5;
    }

    float2 map = tex2D(DistanceMap, mapUv);
    
    float reflectionLine = map.x;
    {
        reflectionLine -= 0.5;
        reflectionLine /= DrawZoom;
        reflectionLine += 0.5;
    }
    
    float alpha = map.y * (1 - pow(1 - Intensity, 5.4));
    
    float2 reflectedUv = uv;
    reflectedUv.y = (reflectionLine * 2) - reflectedUv.y;
    
#if ENABLE_DISTORTION
    float distortionAmount = pow(1. - alpha, 2.5);
    float wave1 = sin(reflectedUv.x * DISTORTION_X_FREQUENCY + Time * DISTORTION_SPEED) * DISTORTION_X_AMPLITUDE;
    float wave2 = sin(reflectedUv.y * DISTORTION_Y_FREQUENCY + Time * DISTORTION_SPEED) * DISTORTION_Y_AMPLITUDE;
    reflectedUv.xy += float2(wave1, wave2) * distortionAmount;
#endif
    
    float4 screen = tex2D(ScreenTexture, uv);
    
#if ENABLE_REFLECTION_BLUR
    float2 blurPixel = float2(REFLECTION_BLUR_RADIUS / ScreenSize.x, 0.);
    float4 reflectedScreen =
    (
        tex2D(ScreenTexture, reflectedUv - blurPixel) +
        tex2D(ScreenTexture, reflectedUv) +
        tex2D(ScreenTexture, reflectedUv + blurPixel)
    ) / 3.0;
#else
    float4 reflectedScreen = tex2D(ScreenTexture, reflectedUv);
#endif

    reflectedScreen = 1 - pow(1 - reflectedScreen, 1.2);
 
    alpha *= REFLECTION_STRENGTH;
    
#if ENABLE_EDGE_PRESERVATION
    float luminance = dot(screen.rgb, float3(0.299, 0.587, 0.114));
    float outlineFactor = pow(saturate(luminance), EDGE_PRESERVATION_STRENGTH);
    alpha *= outlineFactor;
#endif
    
    float4 color = lerp(screen, reflectedScreen, alpha);
    
    return color;
}

BEGIN_TECHNIQUE(Technique1)
    BEGIN_PASS(ReflectionShader)   
        PIXEL_SHADER(compile ps_3_0 ReflectionShaderFragment())  
    END_PASS
END_TECHNIQUE