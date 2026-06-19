#include "../common.h"

sampler2D densityFieldTarget : register(s0);

float fieldSqrtDepth;
float2 fieldTargetSize2D;
float2 zoom;
float2 screenPosition;
float2 screenSize;
float3 sunPosition;

float CalculateRefractiveIndex(float wavelengthMicrometers)
{
    // Based on https://en.wikipedia.org/wiki/Cauchy%27s_equation.
    float a = 1.322;
    float b = 0.00304;
    return a + b / pow(wavelengthMicrometers, 2);
}

// https://gist.github.com/friendly/67a7df339aa999e2bcfcfec88311abfc
float3 CalculateColorFromWavelength(float wavelength)
{
    float R = 0;
    float G = 0;
    float B = 0;
    if (wavelength >= 380 && wavelength <= 440)
    {
        float attenuation = 0.3 + 0.7 * (wavelength - 380) / (440 - 380);

        R = ((-(wavelength - 440) / (440 - 380)) * attenuation);
        G = 0;
        B = attenuation;
    }
    else if (wavelength >= 440 && wavelength <= 490)
    {
        R = 0;
        G = ((wavelength - 440) / (490 - 440));
        B = 1;
    }
    else if (wavelength >= 490 && wavelength <= 510)
    {
        R = 0;
        G = 1;
        B = (-(wavelength - 510) / (510 - 490));
    }
    else if (wavelength >= 510 && wavelength <= 580)
    {
        R = ((wavelength - 510) / (580 - 510));
        G = 1;
        B = 0;
    }
    else if (wavelength >= 580 && wavelength <= 645)
    {
        R = 1;
        G = (-(wavelength - 645) / (645 - 580));
        B = 0;
    }
    else if (wavelength >= 645 && wavelength <= 750)
    {
        float attenuation = 0.3 + 0.7 * (750 - wavelength) / (750 - 645);

        R = attenuation;
        G = 0;
        B = 0;
    }
    else
    {
        R = 0;
        G = 0;
        B = 0;
    }
    
    return float3(R, G, B);
}

float Sample(float wavelengthNanometers, float theta)
{
    float refractiveIndex = CalculateRefractiveIndex(wavelengthNanometers * 0.001);
    
    // The asin(1 / n) * 6 - pi is derived by applying
    // numerical optimization from an angular deviation function
    // in accordance with Snell's Law:
    
    // D(a) = 2a - 4b + pi
    
    // In this context, a is the angle of incidence, and
    // b is the angle of refraction.
    // You'll just have to trust me on this one or do some
    // googling, but this deviation function is basically
    // derived from the internal reflection angles
    // in an assumedly perfectly spherical droplet of water.
    
    // Note that the angle of incidence comes from air, and
    // the angle of reflection enters water.
    // For the purposes of this derivation, the index of refraction
    // for air is assumed to be one.
    
    // Using Snell's law provides the following derivation:
    // sin(a) = n * sin(b)
    // b(a) = arcsin(sin(a) / n)
    // D(a) = 2a - 4 * arcsin(sin(a) / n) + pi
    
    // From here, it's a matter of finding the minimum of D(a), or
    // dD/da = 0. This is where the aforementioned optimization comes
    // into play.
    
    // I'll skip the calculus here because I'm lazy and my
    // fingers hurt slightly from typing this all out already, but
    // the result of such, after a moderate amount of work with
    // algebraic and trigonometric reductions, gives back
    // a rainbow angle of 6 * arcsin(1 / n) - pi.
    
    // From there, all that needs to be done is checking the
    // viewing angle against it.
    float angularDistance = distance(asin(1 / refractiveIndex) * 6 - 3.141, theta);    
    float intensity = exp(-pow(angularDistance, 2) / 0.0002);
    
    return intensity;
}

float4 PixelShaderFunction(float4 sampleColor : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    // Standard UV to world position stuff.
    // The origin of the rainbow is biased horizontally
    // towards the sun in order to make it look better.
    float2 worldStableUv = (uv - 0.5) / zoom + 0.5 + screenPosition / screenSize;
    float2 worldPosition = worldStableUv * screenSize;
    float2 origin = screenPosition + screenSize * float2(0.5, 0.9);
    origin.x = lerp(origin.x, sunPosition.x, 0.525);
    
    float3 viewDirection = normalize(float3(worldPosition - origin, 0));    
    float3 sunDirection = normalize(sunPosition - float3(worldPosition, 0));
    float sunViewOrthogonality = dot(sunDirection, viewDirection);
    float theta = acos(sunViewOrthogonality);
    
    // Apply step-wise increments along the visible
    // color spectrum, checking the viewing angle
    // against the rainbow angle, as determined via
    // the Sample function.
    float3 color = 0;
    float intensitySum = 0;
    for (float i = 0; i < 16; i++)
    {
        float colorInterpolant = i / 15;
        float wavelengthNanometers = lerp(380, 780, pow(colorInterpolant, 2));
        
        float intensity = Sample(wavelengthNanometers, theta);
        color += CalculateColorFromWavelength(wavelengthNanometers) * intensity;
        intensitySum += intensity;
    }
    
    return (float4(color, 1) * intensitySum / 20) * pow(1 - uv.y, 5) * sampleColor;
}

BEGIN_TECHNIQUE(Technique1)
    BEGIN_PASS(AutoloadPass)
        PIXEL_SHADER(compile ps_3_0 PixelShaderFunction())
    END_PASS
END_TECHNIQUE