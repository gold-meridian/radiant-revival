#ifndef _COLORS_H
#define _COLORS_H

static const float3x3 kCONEtoLMS = float3x3(
         0.4121656120, 0.2118591070, 0.0883097947,
         0.5362752080, 0.6807189584, 0.2818474174,
         0.0514575653, 0.1074065790, 0.6302613616);
    
static const float3x3 kLMStoCONE = float3x3(
         4.0767245293, -1.2681437731, -.0041119885,
        -3.3072168827, 2.6093323231, -.7034763098,
         0.2307590544, -.3411344290, 1.7068625689);

float3 toOklab(float3 rgb)
{
    return pow(mul(kCONEtoLMS, rgb), 0.33333);
}

float3 toRGB(float3 oklab)
{
    return mul(kLMStoCONE, pow(oklab, 3));
}

float4 oklabLerp(float4 colA, float4 colB, float h)
{
    float3 lmsA = toOklab(colA.rgb);
    float3 lmsB = toOklab(colB.rgb);
    
    float3 lms = lerp(lmsA, lmsB, h);
    
    return float4(toRGB(lms), lerp(colA.a, colB.a, h));
}

#endif // _COLORS_H