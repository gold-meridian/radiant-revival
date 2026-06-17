#include "../common.h"

sampler2D baseTexture : register(s0);

float globalTime;
float2 zoom;
float2 screenPosition;
float2 screenSize;
float3 worldSize;
float3 radii;
float3 sunPosition;
float3 sunlightFactor;
float3 scatterCoefficients;
float3x3 saturationBoostMatrix;

float2 CalculateRayEllipsoidIntersectionOffsets(float3 rayOrigin, float3 rayDirection, float3 ellipsoidOrigin, float3 ellipsoidRadii)
{
    float3 ellipsoidOffset = (rayOrigin - ellipsoidOrigin) / ellipsoidRadii;
    float3 modulatedDirection = rayDirection / ellipsoidRadii;

    float a = dot(modulatedDirection, modulatedDirection);
    float b = dot(modulatedDirection, ellipsoidOffset) * 2;
    float c = dot(ellipsoidOffset, ellipsoidOffset) - 1;
    float discriminant = pow(b, 2) - a * c * 4;

    float t0 = (-b - sqrt(discriminant)) / (a * 2);
    float t1 = (-b + sqrt(discriminant)) / (a * 2);
    
    return float2((t0 > 0) ? t1 : t0, (t0 > 0) ? t0 : t1);
}

float CalculateDensity(float3 p)
{
    float3 offsetFromWorldOrigin = p - worldSize * 0.5;
    float normalizedHeight = length(offsetFromWorldOrigin / radii);
    
    return exp(-normalizedHeight * 0.65) * pow(smoothstep(1, 0.93, normalizedHeight), 3);
}

float3 CalculateOpticalDepth(float3 start, float3 end, float3 rayDirection, float3 modulatedScatterCoefficients)
{
    float rayLength = distance(start, end);
    float ds = rayLength / 9;
    float3 opticalDepth = 0;
    for (int i = 0; i < 9; i++)
    {
        float stepInterpolant = (i + 0.5) / 9.0;
        float3 p = start + rayDirection * (stepInterpolant * rayLength);
        
        float density = CalculateDensity(p);
        opticalDepth += modulatedScatterCoefficients * density * ds;
    }
    
    return opticalDepth;
}

float4 PixelShaderFunction(float4 sampleColor : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    float2 worldStableUv = (uv - 0.5) / zoom + 0.5 + screenPosition / screenSize;
    float2 worldPosition = worldStableUv * screenSize;
    
    float2 screenWorldCenter = screenPosition + screenSize * 0.5;
    
    float3 rayOrigin = float3(worldPosition, worldSize.z * 0.5);
    float3 rayDirection = normalize(float3(uv * 2 - 1, 0.9));
    float3 color = 0;
    
    float2 offsets = CalculateRayEllipsoidIntersectionOffsets(rayOrigin, rayDirection, worldSize * 0.5, radii);
    
    float rayLength = offsets.y;
    float ds = rayLength / 12;
    float3 start = rayOrigin;
    float3 sunDirection = normalize(sunPosition - float3(rayOrigin.xy, 0));
    float2 sunDirection2D = normalize(sunPosition.xy - screenWorldCenter);
    
    // Apply a weird hack by increasing the scattering as the sun
    // goes down.
    // This is mostly here due to the dumb, highly elliptical 
    // atmosphere shape.
    
    // Having to cover the entire world space makes the requirement of
    // crossing a greater quantity of the atmospheric medium to
    // induce natural scattering of blue and green light a lot trickier.
    // So... I cheated a bit.
    float cosZenith = saturate(dot(sunDirection2D, float2(0, -1)));
    float scatterAccentuation = 1 / max(pow(cosZenith, 1.75), 0.05);
    float3 modulatedScatterCoefficients = scatterCoefficients * scatterAccentuation;
    
    float densitySum = 0;
    
    for (int i = 0; i < 12; i++)
    {
        float stepInterpolant = (i + 0.5) / 12.0;
        float3 p = start + rayDirection * (stepInterpolant * rayLength);
        
        // Sample a ray towards the sun from the ray sample's
        // current position to determine how much light reaches
        // this point from the sun.
        // Note that transmittance in the contexts below all adhere to
        // the Beer-Lambert Law, hence the exponential decay.
        float2 sunOffsets = CalculateRayEllipsoidIntersectionOffsets(p, sunDirection, worldSize * 0.5, radii);
        float3 sunEnd = p + sunDirection * sunOffsets.y;
        float3 sunTransmittance = exp(-CalculateOpticalDepth(p, sunEnd, sunDirection, modulatedScatterCoefficients));
        float3 sunLight = sunlightFactor * sunTransmittance;
        
        // Determine how much light has scattered away thus far
        // along the view ray.
        float3 viewTransmittance = exp(-CalculateOpticalDepth(start, p, rayDirection, modulatedScatterCoefficients));
        
        // Accumulate light in accordance with the scattering integral.
        // This integral is the reason behind the inclusion of
        // the ds term.
        float density = CalculateDensity(p);
        color += modulatedScatterCoefficients * sunLight * viewTransmittance * density * ds;
        
        densitySum += density;
    }
    
    // Apply the Rayleigh phase function.
    float threeOverSixteenPi = 0.0596831037;
    float cosTheta = dot(sunDirection, rayDirection);
    float rayleighPhase = threeOverSixteenPi * (pow(cosTheta, 2) + 1);
    color *= rayleighPhase;
    
    // Modulate and tonemap the color back to sanity.
    float3 tonemappedColor = tanh(color / pow(scatterAccentuation, 0.7) * 100);
    
    // Apply a slight saturation boost to the color.
    // This isn't really in line with the physical
    // scattering equations, I just think it's neat
    // and gives artistic control over the results.
    tonemappedColor = mul(tonemappedColor, saturationBoostMatrix);
    
    // Make the color more translucent the less density
    // was found along the view ray.
    // This is done primarily to account for the
    // upper atmosphere, where you'd expect to see some
    // stars peek through.
    float densityAverage = densitySum / 12;
    float opacity = smoothstep(0, 0.45, densityAverage);
    return float4(tonemappedColor, 1) * sampleColor * opacity;
}

BEGIN_TECHNIQUE(Technique1)
    BEGIN_PASS(AutoloadPass)
        PIXEL_SHADER(compile ps_3_0 PixelShaderFunction())
    END_PASS
END_TECHNIQUE