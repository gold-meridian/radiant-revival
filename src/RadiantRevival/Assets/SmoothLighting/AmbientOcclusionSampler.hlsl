sampler2D wall_tex : register(s0);
sampler2D tile_tex : register(s1);

float4 occlusion_color;

float4 main_mask(float2 uv : TEXCOORD0) : COLOR0
{
    return tex2D(tile_tex, uv);

    /*
    float4 mask = tex2D(wall_tex, uv);
    float4 blur = tex2D(tile_tex, uv);
    
    float4 color = lerp(mask, occlusion_color, pow(blur.a, 2) * mask.a);
    
    return blur;
    */
}

#ifdef FX
technique Technique1
{
    pass MaskShader
    {
        PixelShader = compile ps_2_0 main_mask();
    }
}
#endif // FX