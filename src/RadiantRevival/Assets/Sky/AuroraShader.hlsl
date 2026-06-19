#include "../common.h"

sampler2D noiseTexture : register(s1);

float time;
float bandClumping;
float baseHeight;
float heightSuppressionExponent;
float raymarchStepDecay;
float purpleBias;
float redExcitementHeightKilometers;
float greenExcitementHeightKilometers;
float blueExcitementHeightKilometers;

float3 redContributionCoefficients;
float3 greenContributionCoefficients;
float3 blueContributionCoefficients;
float3 colorBandWidths;

struct PixelShaderOutput
{
    float4 Color : SV_Target0;
    float4 Depth : SV_Target1;
};

float3 Hash33(float3 p3)
{
    float3 uv = frac(p3 * float3(.1031, .11369, .13787));
    uv += dot(uv, uv.yxz + 19.19);
    return -1.0 + 2.0 * frac(float3((uv.x + uv.y) * uv.z, (uv.x + uv.z) * uv.y, (uv.y + uv.z) * uv.x));
}

float CalculateDensity(float3 p)
{
    float noise = 0;
    float2 uv = p.xz;
    
    // Undulate slightly along the forward direction.
    // This isn't super important and is deliberately
    // subtle, but it helps break up the shape monotony
    // a little bit.
    uv.y += sin(time * 30 + p.x * 20) * 0.02;
    
    for (int i = 0; i < 3; i++)
    {
        float theta = 6.283 * i / 3 + time * 0.08;
        float2 scrollDirection = float2(cos(theta), sin(theta));
        float2 scrollOffset = float2(noise * 0.25, time * -1.9) + scrollDirection * time * 0.45;
        
        float decay = pow(1.25, i);
        noise += tex2D(noiseTexture, uv * decay + scrollOffset) / decay;
    }
    
    // Calculate the global dissipation value
    // based on a sheet of scrolling noise.
    // This is used to break up the sameness in density
    // across the sky to create peaks and troughs.
    float globalDissipation = tex2D(noiseTexture, uv * 0.8 + float2(0, time * 0.5));
    
    // The inverse of the globation dissipation is
    // taken as the basis of an ambient glow, however, to
    // keep the density a little bit glowy in a universal
    // sense.
    float ambientGlow = (1 - globalDissipation) * 0.4;
    
    return noise * pow(globalDissipation, 2) * 6 + ambientGlow;
}

float4 CalculateColor(float3 p, float heightKilometers)
{    
    // Each component excitement value gets its own
    // corresponding height band where it peaks.
    float redExcitement = exp(-pow((heightKilometers - redExcitementHeightKilometers) / colorBandWidths.r, 2));
    float greenExcitement = exp(-pow((heightKilometers - greenExcitementHeightKilometers) / colorBandWidths.g, 2));
    float blueExcitement = exp(-pow((heightKilometers - blueExcitementHeightKilometers) / colorBandWidths.b, 2));
    
    float3 termwiseExcitement = float3(redExcitement, greenExcitement, blueExcitement);
    
    // Apply termwise summations with dot products.
    // In their simplest forms, they'd simply map RGB bands
    // to exact RGB colors, but allowing them to mix
    // together like this is extremely important for
    // variation in the aurora.
    // I think the standard fare teals with a red overhead
    // is cool, but it'd be unfortunate if that were the
    // only pattern this shader could create.
    float red = dot(termwiseExcitement, redContributionCoefficients);
    float green = dot(termwiseExcitement, greenContributionCoefficients);
    float blue = dot(termwiseExcitement, blueContributionCoefficients);
    
    return float4(red, green, blue, 0);
}

PixelShaderOutput PixelShaderFunction(float2 uv : TEXCOORD0, float4 sampleColor : COLOR0) : COLOR0
{
    float3 boxSize = 1;
    float3 rayOrigin = 0;
    
    float3 rayDirection = normalize(float3(uv * 2 - 1, 0.5));
    float4 colorSum = 0;
    
    float depth = 0;
    
    for (float i = 0; i < 32; i++)
    {
        float raymarchProgress = i / 32;
        
        // Weird nonsense that extends the march
        // direction forward and upward in accordance
        // with what one might expect from an aurora.
        float extensionNumerator = (baseHeight + pow(raymarchProgress * 67.5, heightSuppressionExponent) * 0.0025 / bandClumping);
        float extensionDenominator = rayDirection.y * 2.2 + rayDirection.z * 0.3 + 0.4;        
        float extension = extensionNumerator / extensionDenominator;
        
        float3 p = rayDirection * extension;
        float heightKilometers = 370 - p.y * 300 + p.z * 60;
        
        // Please, evil banding, go away...
        p += Hash33(p * 80) * 0.002;
        
        // Ensure that colors further along taper
        // with an exponential decay term.
        float attenuation = exp(raymarchProgress * -raymarchStepDecay - 3.56);
        
        float density = CalculateDensity(p);
        float4 sampleColor = CalculateColor(p, heightKilometers) * density * attenuation;
        colorSum += sampleColor;
        
        float depthAtPoint = 1 - max(0, heightKilometers / 360);
        depth = lerp(depth, depthAtPoint, length(sampleColor.rgb) * 0.25);
    }

    // Feed the color sum into a smoothstep to separate
    // the RGB colors more strongly and make the differences
    // sharper.
    float4 sharpenedColor = smoothstep(0, 0.75, colorSum * 1.2);
    
    PixelShaderOutput output = (PixelShaderOutput)0;
    output.Color = sharpenedColor * sampleColor;
    output.Depth = float4(depth, 0, 0, 1);
    
    return output;
}

BEGIN_TECHNIQUE(Technique1)
    BEGIN_PASS(AutoloadPass) 
        PIXEL_SHADER(compile ps_3_0 PixelShaderFunction()) 
    END_PASS
END_TECHNIQUE
