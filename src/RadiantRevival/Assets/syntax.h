#ifndef SYNTAX_H
#define SYNTAX_H

// FX is a magic definition provided by the tml-build shader compiler.  Other
// shader compilers should implement this, such as the one in the tml-build
// bootstrap program and in-game asset hot reloaders.
#ifdef FX
#define ATTRIBUTE(type, name, expr) <type name=expr;>

#define _CS_EXPR(expr) ATTRIBUTE(string, csharpExpression, #expr)
#define CS_EXPR(expr) _CS_EXPR(expr)

#define BEGIN_TECHNIQUE(name) technique name {
#define END_TECHNIQUE }

#define BEGIN_PASS(name) pass name {
#define END_PASS }

#define PIXEL_SHADER(expr) PixelShader = expr;
#define VERTEX_SHADER(expr) VertexShader = expr;

// This syntax is kind of ridiculous:
// https://learn.microsoft.com/en-us/windows/win32/direct3dhlsl/dx-graphics-hlsl-sampler
#define SAMPLER(sampler_type) sampler_type
#define SAMPLER_TEXTURE(texture) Texture = <texture>;

#define _SAMPLER_VALUE_IMPL(name, value) name = value;
#else
#define ATTRIBUTE(type, name, expr)

#define CS_EXPR(expr)

#define BEGIN_TECHNIQUE(name)
#define END_TECHNIQUE

#define BEGIN_PASS(name)
#define END_PASS

#define PIXEL_SHADER(expr)
#define VERTEX_SHADER(expr)

#define SAMPLER(sampler_type, texture)
#define SAMPLER_TEXTURE(texture)

#define _SAMPLER_VALUE_IMPL(name, value) 
#endif

// Abandon all hope ye who enter here: this is awesome macro magic.  See the
// intended output on godbolt:
// https://godbolt.org/#z:OYLghAFBqd5QCxAYwPYBMCmBRdBLAF1QCcAaPECAMzwBtMA7AQwFtMQByARg9KtQYEAysib0QXAMx8BBAKoBnTAAUAHpwAMvAFYTStJg1DIApACYAQuYukl9ZATwDKjdAGFUtAK4sGIABxcpK4AMngMmAByPgBGmMQgkhqkAA6oCoRODB7evgFBaRmOAmER0SxxCUm2mPbFDEIETMQEOT5%2BgTV1WY3NBKVRsfGJyQpNLW15nWN9A%2BWVIwCUtqhexMjsHOaSWDQRANQAggAqxwBKAJIWcsfYEAQAnimYpPvMbK%2BYqinEi/smkjcj2eb1YmABABEvj8AVZJNgTBpDojkWYdpg9ph9gB9NxCbHYAAayjOEGhvyOp0u11uEDGxHCwFeyAUCGaKWw32ImAUGQEr225MWKO2u3CWLxBOJpKFOMlRJJZK5wqRIrRYoOFmwAHELpFsbc3AAJSIXACKcju70wfwImGQCAYeAAjl4sdb/gB2Kyq9UY8X7bCRCEG7DG00W7BeiEi33ozH7LW6/XKQ5CIQQa1/FJMXmgthen2o%2BMBoMh1Pp6Ox4sarHKC6E7AhbFCI2HCHYGXK/bKPCqWpCNlYYj/SQQ/bk2FqksHABqnduhJbbY7XZ%2Bf1n8TtqkHTGHo/Hk8kRZRAHpT/tjgg8Ap9goHoImKp9jf9gBrcLofaoKj7Bn4ZAvFoVYFBAM8LwQAgCBSUDz3oZoGAAOhYPBkGIdIfwIRC0BYU9GAAWi8BRTwAd0/VASOIsiGEkMxT3wbkHB2BBaAUWh6NUfDgGIJgUmvFl8JYtj8IUVgUnoYhwP2AAxEgNn2bcCDWLEiH2OI72eZA8BoTAvwQeIsQyBh5JIzAwA4bl9jEEimAeW8ITHABORDp1rfYhEOABZZQQk7OkxIk7FgRtO8AviIKnnBOM3I87zfLOUNCWOOQzjuRTlL%2BY4viUyzIVHIFsuUgEEVVaL/QOFsvJ8ztsVnQ4QktbELjizMwVeAA3MQ3T%2BD08s67wouRMqEzcAB5SI3BOCAmFeGI/jGiaTmxabZpVGtyolcbJuOZaZrUv4mG2bYYlcja5VGuRIh2w4zm1DNEIev5cQuq7sRuu7luxWrDje278WxV5pH2MxXiCfYNDW0Uzuey7rr%2B3awaYEGrKBtAvEEV4HsQv40cEasoYTSq4pquqGruLH5q2qaieqhLSca14Yde96My%2Burfo%2B7FFkWCA2Z%2Blmvshv1Cdi2nvrJ7EzFaj59n67qcVF%2BLxca5qfOll5Za6m1TpFqqlfp7BsUkdXXjwdBVA6rWnsVkn6pVlrrRMABWCwzdUZ2IUtgbIaRc99kg6DYNPeDiCQlC0IwqgsJwvCGEIqjyMo0jwlo%2Bi8EYggdkcvCqCoe0CBEpo7QUbZRJYcT4kLphgEwKvi%2BrP3DnQdBuV5ORnYsLgADYPf2dASJIL8IAciFF2S1L2whVL0xAfYAHUzkOZRXk8i4zjOUazleNwQiq14LE31cV7XjeN4m7AhZnLFJ%2Bn/E5AgCmFb122yYgJuW55BQ5EZ76Of%2Bn3DiN2bq3BQs4O7d17v3Qe%2Bxh5jjHilbAN9sAz3novZe%2BxV7r03tvXe3l96H07MfLBZ83AXx1gGJB6ZaoP0ek/YmdM7Z3HfiA2cP92YCy5g3C8zDP5z3AT3J244oHECHiPeBE8IRT2QUIWeC8l5ENPjgveiYCFbwwSfTeW0yHDQoZI2%2B2I540OxnQsWBs37AN4Ww/m8NOGlUAReCwg94g5BIPw3uI8xohE3jAsRRJx7YGkhcEItwzjHAAJrKGwLPSI41sCvGUKNPUxxXghD1IgtRhxTRCFGucUa9Y3DxLCYvVeEJ6oWnbK8bUcg0xCAuJk8pnt9gLVnKNBqxwLjjU8uNUal83IHzOKuZ6XjSSPxpvrRhEBHEiOcZ4EgVi/6Cy4Rg6u0k6B2kki7CBgi%2B4DxET4uBfiEGBOCQuCJUT9gxMiHEnsiSropLSTdV4mSLjZNyfkwpxSLilJCA0yp1T0x1MiL8pp40WltI6ZELpMTelnU8ocbU2JjkhKMdbZ%2BDDX6eRWWs%2BI8yOEAL9pi1QhwnQKFQAQdCKQHhuO2cI9A5CDhwqXM815G9lBhJRSY8ZGKnzEpvGSilDxcU2PxReQlnk8ApBCJgdqtRqXjnCAQelWJGXYlXsobEvl5whA5WMl%2BloIBiolVKmVtAhWcxFRg8IqzaDrLlTs6BsDR6HNSki05kTomxPibc5J%2BxUlXMeUcLJOTWUXAKT2IpXkvllOqY0qpNTAXAuaa0m4EKoU9KVeo/UrqRm0N1ei/V4qGDWvWWa/%2BSzxUpGLfEO1tL9lOqSkcoJITwnuouZ6m5ST7n%2BoyUGt5oaPmRu%2BcCuNAL6kxu3qC5N7TOndJhQmNViKm1%2BVGTbfNdwK1VrIDiX%2BeLy0SsxZK1Ae5Xa5jtVQYCTBFU6IZRcdVcL1VePbNiCwdT7q5tXcrdd%2B7eIhCPegE9ChS2LLsX7IQt0LBZVUDlLEMRUCeAzWB7UFhEr%2BIgHLEKebP0QEQxBwq3IvbdWrGXCuI4hBhWIBoA87k0X%2BXLoFGYdo/gmG9CifYbHqP0JQwgiAkHoMQynEidjGD4WLpOaSMGCSknCmPKx9jq8s1LtJJJq60mixCYXdmiAynjiqdk2xyhd8IByOULpwT7GDPUOM6Z5EnoYwybsfKaU9weQEAAQTAMy52w1ThW4DeQh9jAGAjEMQIAQBnD3HgQwBAzjSrwP1RCHgWAsAEIhXcw5MURxLterE4jDa1IAFp3G5MAG86z5r4gVKSTzgyfN%2BcQrx5SQg8AAC9MAQGK6V%2BI3MUS5ZbBcQrEAuAAI4MsWgnAna8D8BwLQpBUCcDcNYawd5VjrCxNsHgpACCaBG8sN8IAAAszku5dzME7J2khHL%2BEchofwTsu5O30Jwfbk3tuzc4LwUCyQtvTZG6QOAsAkA4RSHQHFFAIBA5BwkZABgjBmA0FwZINAbXxFAhAGIr2YjhGaA8TgG3MfMGIA8UaMRtD51x7wHCbBBCjQYLQHHP3SBYBiF4YAbgxCsXJ4zzALBDCBc2DNhi%2Bc4s8le18e0Xg7SvYVbUV7tA8AxB4oTjwWBXvkrwCwTnMriCwaUFCHnRg5dGG28sc91dQF4EwCRUazwpsbf4IIEQYh2BcH2zIQQigVDqAZ7oEGMPjCLcsPoeXoFIDLFQCkeooEOD4WwLwVAWv/wi9D10IXzgyQMHcJ4doehQjinmMMAo6RMgCEmH4QvRQshzCGAkIIdhU8NHGK0LPeRa%2B1Hr70FoVeKgF9sI30vegZid7z9XiQyxSVrA2KPx7HAJukCmzNubHB9iqH8F3fCXd9v7Gh7z4GGhEJcEQpRiAuBCAkH%2BGiIbvBvtaG5qQPbTs9%2BSEkJ6fb%2B2u7w40I5F3kgu7T%2Be3P17RfD7EAL7Y3UbTgMwXgDXBHZIefOPd7TbMA0gLXPkPwfbIAA%3D
// Please don't touch this if you don't know what you're doing, it's sort of a
// pain to debug.
// This is mostly adapted from principles explained here:
// https://www.w3tutorials.net/blog/optional-parameters-with-c-macros/
//   -- tomat

#define CONCAT(a, b) CONCAT_(a, b)
#define CONCAT_(a, b) a##b
#define _COUNT_ARGS(...) _COUNT_ARGS_(__VA_ARGS__, 3, 2, 1, 0)
#define _COUNT_ARGS_(a1, a2, a3, count, ...) count

#define _SAMPLER_VALUE(...) CONCAT(_SAMPLER_VALUE_, _COUNT_ARGS(__VA_ARGS__))(__VA_ARGS__)
#define _SAMPLER_VALUE_2(name, value) _SAMPLER_VALUE_IMPL(name, value)
#define _SAMPLER_VALUE_3(name, idx, value) _SAMPLER_VALUE_IMPL(name[idx], value)

// https://learn.microsoft.com/en-us/windows/win32/direct3d9/effect-states#sampler-stage-states

// AddressU[16] dword (D3DTEXTUREADDRESS: WRAP, MIRROR, CLAMP, BORDER, MIRRORONCE)
#define ADDRESS_U(...) _SAMPLER_VALUE(AddressU, __VA_ARGS__)

// AddressV[16] dword (D3DTEXTUREADDRESS: WRAP, MIRROR, CLAMP, BORDER, MIRRORONCE)
#define ADDRESS_V(...) _SAMPLER_VALUE(AddressV, __VA_ARGS__)

// AddressW[16] dword (D3DTEXTUREADDRESS: WRAP, MIRROR, CLAMP, BORDER, MIRRORONCE)
#define ADDRESS_W(...) _SAMPLER_VALUE(AddressW, __VA_ARGS__)

// BorderColor[16] D3DCOLOR (D3DTEXTUREFILTERTYPE: NONE, POINT, LINEAR, ANISOTROPIC, PYRAMIDALQUAD, GUASSIANQUAD, CONVOLUTIONMONO)
#define BORDER_COLOR(...) _SAMPLER_VALUE(BorderColor, __VA_ARGS__)

// MagFilter[16] dword (D3DTEXTUREFILTERTYPE: NONE, POINT, LINEAR, ANISOTROPIC, PYRAMIDALQUAD, GUASSIANQUAD, CONVOLUTIONMONO)
#define MAG_FILTER(...) _SAMPLER_VALUE(MagFilter, __VA_ARGS__)

// MaxAnisotropy[16] dword
#define MAX_ANISOTROPY(...) _SAMPLER_VALUE(MaxAnisotropy, __VA_ARGS__)

// MaxMipLevel[16] int
#define MAX_MIP_LEVEL(...) _SAMPLER_VALUE(MaxMipLevel, __VA_ARGS__)

// MinFilter[16] dword (D3DTEXTUREFILTERTYPE: NONE, POINT, LINEAR, ANISOTROPIC, PYRAMIDALQUAD, GUASSIANQUAD, CONVOLUTIONMONO)
#define MIN_FILTER(...) _SAMPLER_VALUE(MinFilter, __VA_ARGS__)

// MipFilter[16] dword (D3DTEXTUREFILTERTYPE: NONE, POINT, LINEAR, ANISOTROPIC, PYRAMIDALQUAD, GUASSIANQUAD, CONVOLUTIONMONO)
#define MIP_FILTER(...) _SAMPLER_VALUE(MipFilter, __VA_ARGS__)

// MipMapLoadBias[16] float
#define MIP_MAP_LOAD_BIAS(...) _SAMPLER_VALUE(MipMapLoadBias, __VA_ARGS__)

// SRGBTexture bool
#define SRGB_TEXTURE(value) _SAMPLER_VALUE(SRGBTexture, value)

#endif