texture Mask : register(t0);
sampler MaskSampler : register(s0) = sampler_state { Texture = <Mask>; MinFilter = POINT; MagFilter = POINT; MipFilter = NONE; AddressU = CLAMP; AddressV = CLAMP; };
texture Nebula : register(t1);
sampler NebulaSampler : register(s1) = sampler_state { Texture = <Nebula>; MinFilter = LINEAR; MagFilter = LINEAR; MipFilter = NONE; AddressU = WRAP; AddressV = WRAP; };
texture Stars : register(t2);
sampler StarSampler : register(s2) = sampler_state { Texture = <Stars>; MinFilter = LINEAR; MagFilter = LINEAR; MipFilter = NONE; AddressU = WRAP; AddressV = WRAP; };
float4x4 MatrixTransform;
float2 CameraOrigin;
float Clock;
struct VertexOutput { float4 Position : SV_POSITION; float4 Color : COLOR0; float2 TexCoord : TEXCOORD0; float2 Field : TEXCOORD1; };
VertexOutput Vertex(float4 position : POSITION0, float4 color : COLOR0, float2 uv : TEXCOORD0)
{
    VertexOutput output;
    output.Position = mul(position, MatrixTransform);
    output.Color = color;
    output.TexCoord = uv;
    output.Field = position.xy - CameraOrigin;
    return output;
}
float4 Cosmic(VertexOutput input) : SV_TARGET
{
    float4 mask = tex2D(MaskSampler, input.TexCoord);
    float2 p = input.Field;
    float drift = Clock * 3.0;
    float3 galaxy = tex2D(NebulaSampler, (p + float2(drift * 0.5, drift * -0.125)) / 512.0).rgb;
    float3 nearStar = tex2D(StarSampler, (p + float2(drift * -0.25, drift * 0.125)) / 512.0).rgb;
    float3 farStar = tex2D(StarSampler, (p * 0.5 + float2(197, 71) + float2(drift * 0.125, 0)) / 512.0).rgb;
    float twinkle = 0.62 + 0.38 * sin(Clock * 0.78539816 + nearStar.g * 6.2831853);
    float distant = 0.68 + 0.32 * sin(Clock * 0.39269908 + farStar.g * 6.2831853);
    float breath = 0.9 + 0.1 * sin(Clock * 0.19634954);
    float3 field = float3(0.001, 0.002, 0.006) + galaxy * breath
        + nearStar.r * twinkle * float3(0.78, 0.90, 1.0)
        + farStar.r * distant * float3(0.30, 0.22, 0.50);
    // The old artwork contributes only a faint trace of luminance, never hue.
    field += mask.g * 0.012;
    float3 color = lerp(saturate(field), float3(1,1,1), mask.r);
    return float4(color * mask.a * input.Color.rgb, mask.a * input.Color.a);
}
technique Portal { pass Pass0 { VertexShader = compile vs_4_0_level_9_1 Vertex(); PixelShader = compile ps_4_0_level_9_1 Cosmic(); } }
