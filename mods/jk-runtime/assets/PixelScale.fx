texture Source : register(t0);
sampler SourceSampler : register(s0) = sampler_state {
    Texture = <Source>; MinFilter = LINEAR; MagFilter = LINEAR;
    MipFilter = NONE; AddressU = CLAMP; AddressV = CLAMP;
};
float2 SourceSize;
float2 OutputScale;
float4x4 MatrixTransform;
struct Varyings { float4 position : SV_POSITION; float4 color : COLOR0; float2 uv : TEXCOORD0; };
Varyings Vertex(float4 position : POSITION0, float4 color : COLOR0, float2 uv : TEXCOORD0)
{
    Varyings result;
    result.position = mul(position, MatrixTransform); result.color = color; result.uv = uv;
    return result;
}
float4 Pixel(Varyings input) : SV_TARGET
{
    // only the display pixels crossing a texel edge need mixed colors
    float2 p = input.uv * SourceSize;
    float2 f = frac(p);
    float2 lower = min(f * OutputScale, 0.5);
    float2 upper = max((f - 1) * OutputScale + 0.5, 0);
    return tex2D(SourceSampler, (floor(p) + lower + upper) / SourceSize) * input.color;
}
technique BoundaryScale {
    pass P0 { VertexShader = compile vs_4_0_level_9_1 Vertex(); PixelShader = compile ps_4_0_level_9_1 Pixel(); }
}
