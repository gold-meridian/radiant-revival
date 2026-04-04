sampler uImage0 : register(s0);

float shadowRotation;

float4 shadowColor;

float2 rotate(float2 coords, float2 center, float angle)
{
    float2 translatedCoords = coords - center;
    
    float c = cos(angle);
    float s = sin(angle);
    
    float2x2 mat = float2x2(float2(c, s), float2(-s, c));
                    
    float2 rotatedCoords;
    rotatedCoords.x = dot(translatedCoords, mat[0].xy);
    rotatedCoords.y = dot(translatedCoords, mat[1].xy);
    
    return rotatedCoords;
}

float4 main(float2 uv : TEXCOORD0, float4 baseColor : COLOR0) : COLOR0
{
    uv = rotate(uv, 0.5, shadowRotation);
    
    float4 rings = tex2D(uImage0, saturate(uv + 0.5)) * baseColor;
    
    float light = saturate(abs(uv.x * 3.4f));
    light = 1 - pow(2, 15 * (light - 1));
    
    light *= step(0, uv.y);

    return lerp(rings, shadowColor * rings, light);
}

#ifdef FX
technique Technique1
{
    pass RingShader
    {
        PixelShader = compile ps_2_0 main();
    }
}
#endif // FX
