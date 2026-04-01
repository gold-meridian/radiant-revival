sampler uImage0 : register(s0);

float4 main(float2 uv : TEXCOORD0, float4 baseColor : COLOR0) : COLOR0
{
    float4 col = tex2D(uImage0, saturate(uv)) * baseColor;
    
    float dist = 1 - (length(uv - .5) * 8.6);
    
    col += pow(saturate(dist), 4);
    
    col = saturate(col);
    
    col.rgb * col.a;
    
    return col;
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
