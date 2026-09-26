// Surface alpha is coverage; Surface RGB is straight, colorless reflection.
// The offset map stores integer facet offsets and transmission independently.
texture Surface : register(t0);
sampler SurfaceSampler : register(s0) = sampler_state { Texture = <Surface>; MinFilter = POINT; MagFilter = POINT; MipFilter = NONE; AddressU = CLAMP; AddressV = CLAMP; };
texture Facets : register(t1);
sampler FacetSampler : register(s1) = sampler_state { Texture = <Facets>; MinFilter = POINT; MagFilter = POINT; MipFilter = NONE; AddressU = CLAMP; AddressV = CLAMP; };
texture Scene : register(t2);
sampler SceneSampler : register(s2) = sampler_state { Texture = <Scene>; MinFilter = POINT; MagFilter = POINT; MipFilter = NONE; AddressU = CLAMP; AddressV = CLAMP; };
float2 InverseSceneSize;
float2 OffsetScale;
float Dispersion;
float2 ScreenOrigin;
float2 ScreenExtent;
float4 MapRegion;
float4x4 MatrixTransform;
struct VertexOutput { float4 Position : SV_POSITION; float4 Color : COLOR0; float2 TexCoord : TEXCOORD0; };
VertexOutput Vertex(float4 position : POSITION0, float4 color : COLOR0, float2 uv : TEXCOORD0)
{
    VertexOutput output;
    output.Position = mul(position, MatrixTransform);
    output.Color = color;
    output.TexCoord = uv;
    return output;
}

float4 Crystal(VertexOutput input) : SV_TARGET
{
    float4 tint = input.Color;
    float2 uv = input.TexCoord;
    float4 surface = tex2D(SurfaceSampler, uv);
    float4 facet = tex2D(FacetSampler, uv);
    float2 offset = round((facet.rg * 255.0 - float2(4.0, 3.0)) * OffsetScale);
    float2 screen = ScreenOrigin + (uv - MapRegion.xy) / MapRegion.zw * ScreenExtent + offset;
    float3 transmitted;
    transmitted.r = tex2D(SceneSampler, (screen - float2(Dispersion, 0)) * InverseSceneSize).r;
    transmitted.g = tex2D(SceneSampler, screen * InverseSceneSize).g;
    transmitted.b = tex2D(SceneSampler, (screen + float2(Dispersion, 0)) * InverseSceneSize).b;
    return float4(lerp(surface.rgb, transmitted, facet.b) * surface.a * tint.rgb, surface.a * tint.a);
}

technique Refraction
{
    pass Pass0 { VertexShader = compile vs_4_0_level_9_1 Vertex(); PixelShader = compile ps_4_0_level_9_1 Crystal(); }
}
