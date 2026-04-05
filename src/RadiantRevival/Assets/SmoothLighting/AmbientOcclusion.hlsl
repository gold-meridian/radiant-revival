#include "../tmlbuild.h"
#include "../expressions.h"

sampler2D walls : register(s0);
sampler2D tiles : register(s1);

float2 lighting_buffer_size TEXTURE_SIZE(1);

float2 draw_offset;

float4 main(float2 pos : SV_POSITION, float2 uv : TEXCOORD0, float4 color : COLOR0) : COLOR0
{

}

#ifdef FX
technique Technique1
{
    pass OcclusionShader
    {
        PixelShader = compile ps_3_0 main();
    }
}
#endif // FX