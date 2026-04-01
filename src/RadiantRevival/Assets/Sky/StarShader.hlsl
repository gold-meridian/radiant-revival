sampler uImage0 : register(s0);

float4 main(float2 uv : TEXCOORD0, float4 baseColor : COLOR0) : COLOR0
{
    float4 col = tex2D(uImage0, saturate(uv)) * baseColor;
    
    clip(col.a);

    float dist = 1 - (length(uv - .5) * 10);
    
    col += saturate(pow(dist, 4));
    
    col = saturate(col);
    
    return col * col.a;
}

#ifdef FX
technique Technique1
{
    pass PanelShader
    {
        PixelShader = compile ps_2_0 main();
    }
}
#endif // FX
