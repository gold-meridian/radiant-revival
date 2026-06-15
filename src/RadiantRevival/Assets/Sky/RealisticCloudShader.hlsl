#include "../common.h"

sampler2D densityFieldTexture : register(s0);
sampler2D noiseTexture : register(s1);

float horizontalScroll;
float phaseAnisotropy;
float densityPosterizationLevel;
float pixelationLevel;
float2 screenPosition;
float2 screenSize;
float3 cloudSize;
float3 sunPosition;
float3 sunlightFactor;
float3 scatterCoefficients;
float3 extinctionCoefficients;

float fieldSqrtDepth;
float2 fieldTargetSize2D;

float2 CalculateRayBoxIntersectionOffsets(float3 rayOrigin, float3 rayDirection, float3 boxMin, float3 boxMax)
{
    float3 invDir = 1 / rayDirection;
    float3 tBottom = invDir * (boxMin - rayOrigin);
    float3 tTop = invDir * (boxMax - rayOrigin);
    float3 tMin = min(tBottom, tTop);
    float3 tMax = max(tBottom, tTop);
    
    float tNear = max(max(tMin.x, tMin.y), tMin.z);
    float tFar = min(min(tMax.x, tMax.y), tMax.z);
    
    return float2(tNear, tFar);
}

float2 ConvertToUv(float3 p, float yTile)
{
    float x = p.x;
    float y = p.y;
    float z = p.z;

    float width = fieldTargetSize2D.x;
    float height = fieldTargetSize2D.y;
    float s = fieldSqrtDepth;
    
    float xTile = round(z - x / width - (s - 1) * (y / height) - (s - 1) * yTile);

    float pixelX = x + xTile * width;
    float pixelY = y + yTile * height;

    return float2(pixelX / (width * s), pixelY / (height * s));
}

float CalculateDensity(float3 p)
{
    float3 normalizedP = p / cloudSize;
    normalizedP.x = sin(normalizedP.x * 2.09439 + horizontalScroll);
    normalizedP.z *= 0.8;
    
    float3 pixel = normalizedP * float3(fieldTargetSize2D, fieldSqrtDepth * fieldSqrtDepth);
    
    float width = fieldTargetSize2D.x;
    float height = fieldTargetSize2D.y;
    float s = fieldSqrtDepth;

    float xModulo = fmod(pixel.x, width);
    float yModulo = fmod(pixel.y, height);
    float A = pixel.z - (xModulo / width) - (s - 1) * (yModulo / height);
    float yTile = floor(A / (s - 1) + 1e-5);
    
    float2 uv = ConvertToUv(pixel, yTile);
    float density = tex2D(densityFieldTexture, uv).x;
    
    return round(density * densityPosterizationLevel) / densityPosterizationLevel;
}

float3 CalculateOpticalDepth(float3 start, float3 rayDirection, float travelDistance)
{
    float ds = travelDistance / 8;
    float3 opticalDepth = 0;

    for (int i = 0; i < 8; i++)
    {
        float t = (i + 0.5) * ds;
        float3 p = start + rayDirection * t;

        float d = CalculateDensity(p);
        opticalDepth += extinctionCoefficients * d * ds;
    }

    return opticalDepth;
}

float PhaseHG(float cosTheta, float g)
{
    float fourPi = 12.566371;
    float gSquared = g * g;
    float denominator = pow(1 + gSquared - g * cosTheta * 2, 1.5) * fourPi;
    return (1 - gSquared) / denominator;
}

float4 PixelShaderFunction(float4 sampleColor : COLOR0, float2 uv : TEXCOORD0, float2 position : SV_Position) : COLOR0
{
    float2 worldRelativeUv = position / screenSize;
    float2 worldStableUv = (worldRelativeUv - 0.5) + 0.5 + screenPosition / screenSize;
    float2 worldPosition = worldStableUv * screenSize;
    float3 sunDirection = normalize(sunPosition - float3(worldPosition, 0));
    
    float3 boxMin = 0;
    float3 boxMax = cloudSize;
    
    float2 pixelationFactor = pixelationLevel / cloudSize.xy;
    uv = round(uv / pixelationFactor) * pixelationFactor;
    
    float3 rayOrigin = float3(cloudSize.xy * uv, 0);
    float3 rayDirection = float3(0, 0, 1);
    
    float ds = cloudSize.z / 16;

    float densitySum = 0;
    float3 color = 0;
    float3 viewTransmittance = 1;

    for (int i = 0; i < 12; i++)
    {
        float t = (i + 0.5) * ds;
        float3 p = rayOrigin + rayDirection * t;

        float density = CalculateDensity(p);
        float travelDistance = CalculateRayBoxIntersectionOffsets(p, sunDirection, 0, cloudSize).y;
        float3 sunTransmittance = exp(-CalculateOpticalDepth(p, sunDirection, travelDistance));
        
        float cosTheta = dot(sunDirection, rayDirection);
        float phase = PhaseHG(cosTheta, phaseAnisotropy);
        
        // Determine the contribution of scattering at the current
        // point based on the sun's transmittance, density, and
        // the Henyey–Greenstein phase function.
        float3 scatterContribution = sunlightFactor * sunTransmittance * scatterCoefficients * density * phase;
        
        // Multiple scattering is not reasonable for real-time rendering
        // contexts. To vaguely approximate it, simply apply a boost to the
        // overall scattering term.
        float multipleScatteringEnergyBoost = 1.25;
        scatterContribution *= multipleScatteringEnergyBoost;
        
        color += viewTransmittance * scatterContribution * ds;
        viewTransmittance *= exp(-extinctionCoefficients * density * ds);
        densitySum += density;
    }
    
    // Compose everything together and make less dense
    // regions fade away into invisibility.
    // Tonemapping is skipped, I think it muted the overall colors
    // a bit too much.
    float densityAverage = densitySum / 16;
    float opacity = pow(smoothstep(0.03, 0.5, densityAverage), 2);
    float3 result = color * 20;
    
    return float4(result, 1) * sampleColor * opacity;
}

BEGIN_TECHNIQUE(Technique1) 
    BEGIN_PASS(AutoloadPass) 
        PIXEL_SHADER(compile ps_3_0 PixelShaderFunction()) 
    END_PASS
END_TECHNIQUE