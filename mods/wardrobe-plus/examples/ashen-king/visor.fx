texture SpriteTexture : register(t0);
sampler SpriteSampler : register(s0) = sampler_state { Texture=<SpriteTexture>; MinFilter=POINT; MagFilter=POINT; MipFilter=NONE; AddressU=CLAMP; AddressV=CLAMP; };
texture Mask : register(t1);
sampler MaskSampler : register(s1) = sampler_state { Texture=<Mask>; MinFilter=POINT; MagFilter=POINT; MipFilter=NONE; AddressU=CLAMP; AddressV=CLAMP; };
float4x4 MatrixTransform;
float Charge;
struct Output { float4 Position:SV_POSITION; float4 Color:COLOR0; float2 UV:TEXCOORD0; };
Output Vertex(float4 position:POSITION0,float4 color:COLOR0,float2 uv:TEXCOORD0)
{ Output o; o.Position=mul(position,MatrixTransform);o.Color=color;o.UV=uv;return o; }
float4 Pixel(Output input):SV_TARGET
{
    float4 source=tex2D(SpriteSampler,input.UV);
    float heat=saturate(Charge);
    float3 cold=float3(15,13,18)/255;
    float3 ember=float3(205,45,12)/255;
    float3 hot=float3(255,231,151)/255;
    float3 glow=lerp(lerp(cold,ember,saturate(heat*2)),hot,saturate(heat*2-1));
    float mask=tex2D(MaskSampler,input.UV).r;
    return float4(lerp(source.rgb,glow*source.a,mask),source.a)*input.Color;
}
technique Visor { pass Pass0 { VertexShader=compile vs_4_0_level_9_1 Vertex();PixelShader=compile ps_4_0_level_9_1 Pixel(); } }
