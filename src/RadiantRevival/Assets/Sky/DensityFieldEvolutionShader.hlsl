#include "../common.h"

sampler2D previousData : register(s0);
sampler2D noiseTexture : register(s1);

float depth;
float sqrtDepth;

float horizontalScrollSpeed;
float time;

float densityDampeningDecayFactor;
float advectionBlendInterpolant;
float densityGrowthCoefficient;
float densityDecayCoefficient;
float condensationCoefficient;
float humidityBase;
float humidityHeightFalloff;
float surfaceTemperature;
float spaceTemperature;
float buoyancyReferenceTemperature;
float buoyancyIntensity;

float2 targetSize2D;

float3 ConvertTo3D(float2 uv)
{
    float2 pixel = uv * targetSize2D * sqrtDepth;
    float2 xy = fmod(pixel, targetSize2D);
    float z = pixel.x / targetSize2D.x + (sqrtDepth - 1) * pixel.y / targetSize2D.y;    
    
    return float3(xy, z);
}

float2 ConvertToUv(float3 p, float yTile)
{
    float x = p.x;
    float y = p.y;
    float z = p.z;

    float width = targetSize2D.x;
    float height = targetSize2D.y;
    float s = sqrtDepth;
    
    float xTile = round(z - x / width - (s - 1) * (y / height) - (s - 1) * yTile);

    float pixelX = x + xTile * width;
    float pixelY = y + yTile * height;

    return float2(pixelX / (width * s), pixelY / (height * s));
}

float Hash13(float3 p)
{
    p = frac(p * 0.3183099 + 0.1) * 17;
    return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
}

float CalculateNoise(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    f = smoothstep(0, 1, f);
    
    return lerp(lerp(lerp(Hash13(i + float3(0, 0, 0)),
                        Hash13(i + float3(1, 0, 0)), f.x),
                   lerp(Hash13(i + float3(0, 1, 0)),
                        Hash13(i + float3(1, 1, 0)), f.x), f.y),
                lerp(lerp(Hash13(i +float3(0, 0, 1)),
                        Hash13(i + float3(1, 0, 1)), f.x),
                   lerp(Hash13(i + float3(0, 1, 1)),
                        Hash13(i + float3(1, 1, 1)), f.x), f.y), f.z);
}

float3 CalculateCurl(float3 p)
{
    float epsilon = 0.004;
    float dx = CalculateNoise(p + float3(epsilon, 0, 0)) - CalculateNoise(p - float3(epsilon, 0, 0));
    float dy = CalculateNoise(p + float3(0, epsilon, 0)) - CalculateNoise(p - float3(0, epsilon, 0));
    float dz = CalculateNoise(p + float3(0, 0, epsilon)) - CalculateNoise(p - float3(0, 0, epsilon));

    float3 noiseGradientA = float3(dx, dy, dz) / (epsilon * 2);

    // Offset position by a random value for
    // a second, uncorrelated noise read.
    p += 774.23;

    dx = CalculateNoise(p + float3(epsilon, 0, 0)) - CalculateNoise(p - float3(epsilon, 0, 0));
    dy = CalculateNoise(p + float3(0, epsilon, 0)) - CalculateNoise(p - float3(0, epsilon, 0));
    dz = CalculateNoise(p + float3(0, 0, epsilon)) - CalculateNoise(p - float3(0, 0, epsilon));

    float3 noiseGradientB = float3(dx, dy, dz) / (epsilon * 2);

    float3 curl = cross(noiseGradientA, noiseGradientB);

    return normalize(curl);
}

float CalculateDivergence(float3 p)
{
    float epsilon = 0.01;
    float dx = CalculateNoise(p + float3(epsilon, 0, 0)) - CalculateNoise(p - float3(epsilon, 0, 0));
    float dy = CalculateNoise(p + float3(0, epsilon, 0)) - CalculateNoise(p - float3(0, epsilon, 0));
    float dz = CalculateNoise(p + float3(0, 0, epsilon)) - CalculateNoise(p - float3(0, 0, epsilon));
    return (dx + dy + dz) / (epsilon * 2);
}

float GaussianDistribution(float x, float sigma, float mu)
{
    float normalizationCoefficient = 1 / (sigma * sqrt(6.283));
    float exponent = -(pow(x - mu, 2) / (sigma * sigma * 2));
    return exp(exponent) * normalizationCoefficient;
}

// It is assumed that everything here operates in Kelvin.
float CalculateTemperature(float3 p, float spaceInterpolant)
{
    float baseFalloff = lerp(surfaceTemperature, spaceTemperature, pow(spaceInterpolant, 2));
    
    // Apply temperature variance with noise.
    // This applies most strongly in the middle atmosphere, but
    // permeates universally.
    float varianceIntensity = GaussianDistribution(spaceInterpolant, 0.1, 0.54) * 0.6 + 0.25;
    float noiseVariance = 0;
    
    float lowFrequencyNoise = CalculateNoise(p * 0.01 + time * 0.3);
    float midFrequencyNoise = CalculateNoise(p * 0.04 + lowFrequencyNoise * 0.04 - time * 0.9);
    float highFrequencyNoise = CalculateNoise(p * 0.09 + midFrequencyNoise * 0.03 + time * 2);
    
    noiseVariance += lowFrequencyNoise * 4.7;
    noiseVariance += midFrequencyNoise * 2.3;
    noiseVariance += highFrequencyNoise * 1.1;
    
    baseFalloff -= smoothstep(0.45, 0.56, spaceInterpolant) * 25;
    
    return baseFalloff + noiseVariance * varianceIntensity;
}

float CalculateHumidity(float3 p, float spaceInterpolant)
{
    // Exponentially taper off humidity the more in
    // space the current point is.
    float humidity = humidityBase * exp(-humidityHeightFalloff * spaceInterpolant);
    
    // Apply a low and high frequency noise pass
    // to the humidity field.
    // This guides the emergent formation of clouds
    // considerably.
    float noise = CalculateNoise(p * 0.043 + time * 0.07) * 2 - 1;
    humidity += noise * 0.33;
    
    noise = CalculateNoise(p * 0.015 - time * 0.02 + noise * 0.1) * 2 - 1;
    humidity += noise * humidity * 0.9;

    return saturate(humidity);
}

float3 CalculateVelocity(float3 p, float temperature, float previousDensity, float condensationPotential, float spawnPuffyLobes)
{
    float3 velocity = 0;
    
    // Make clouds vertically adhere to buoyancy in
    // accordance with the temperature.
    float buoyancy = (temperature - buoyancyReferenceTemperature) * (1 + previousDensity * 2.6) * buoyancyIntensity;
    velocity.y += buoyancy;
    
    // Also adhere to the condensation potential to try
    // and incentivize the creation of larger-scale
    // cumulonimbus clouds as needed.
    velocity.y += condensationPotential * -10 + spawnPuffyLobes * 5;
    
    // Adhere to wind and stuff.
    velocity.x += horizontalScrollSpeed;
    
    // Apply curl noise to the clouds because it results
    // in some really cool local swirliness.
    float3 curl = CalculateCurl(p * 0.01 + time * float3(0.05, 0.1, -0.4) + previousDensity * 0.07);
    curl.y *= 2.5;
    curl.xz *= 0.5;
    velocity += curl * (1 + spawnPuffyLobes * 2.6);
    
    return velocity * 0.26;
}

float4 PixelShaderFunction(float4 sampleColor : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    float previousDensity = tex2D(previousData, uv);
    float nextDensity = previousDensity;
    
    float3 p = ConvertTo3D(uv);
    float yTile = floor(uv.y * sqrtDepth);
    uv = frac(uv * sqrtDepth);
    
    float topOfWorldInterpolant = smoothstep(0.75, 0.98, 1 - uv.y);
    float spaceInterpolant = uv.y;
    
    // Determine how much the clouds should condensate
    // based on proximity from a saturation humidity value.
    float temperature = CalculateTemperature(p, spaceInterpolant);
    float humidity = CalculateHumidity(p, spaceInterpolant) + previousDensity * 0.2;
    float saturationHumidity = exp(-condensationCoefficient * temperature);
    float condensationPotential = pow(saturate(humidity - saturationHumidity), 1.5);
    
    float puffyLobeSpawnProbability = 1 - condensationPotential * 0.15;
    float spawnPuffyLobes = smoothstep(0, 0.08, CalculateNoise(p) - puffyLobeSpawnProbability);
    nextDensity += spawnPuffyLobes * 0.1;
    
    // Determine how much the clouds should evaporate
    // based on distance from an equilibrium temperature.
    // In the general sense, this influences overall cloud
    // size and permeation.
    float equilibriumTemperature = lerp(surfaceTemperature, spaceTemperature, 0.3);
    float evaporation = saturate(temperature - equilibriumTemperature) * 0.15;
    
    float growthIncrement = densityGrowthCoefficient * condensationPotential;
    float decayIncrement = densityDecayCoefficient * evaporation;
    nextDensity += growthIncrement - decayIncrement;
    
    float3 velocity = CalculateVelocity(p, temperature, previousDensity, condensationPotential, spawnPuffyLobes);
    float densityAdvected = tex2D(previousData, ConvertToUv(p - velocity, yTile));
    nextDensity = lerp(nextDensity, densityAdvected, advectionBlendInterpolant);
    nextDensity += (CalculateNoise(p * 0.5 + time * 1.7) - 0.5) * 0.005;
    nextDensity *= densityDampeningDecayFactor;
    
    return nextDensity * (1 - topOfWorldInterpolant * 0.15);
}

BEGIN_TECHNIQUE(Technique1)
    BEGIN_PASS(AutoloadPass)
        PIXEL_SHADER(compile ps_3_0 PixelShaderFunction())
    END_PASS
END_TECHNIQUE