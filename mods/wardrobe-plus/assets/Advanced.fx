texture SpriteTexture : register(t0);
sampler SpriteSampler : register(s0) = sampler_state { Texture=<SpriteTexture>; MinFilter=POINT; MagFilter=POINT; MipFilter=NONE; AddressU=CLAMP; AddressV=CLAMP; };
texture Pattern : register(t1);
sampler PatternSampler : register(s1) = sampler_state { Texture=<Pattern>; MinFilter=POINT; MagFilter=POINT; MipFilter=NONE; AddressU=WRAP; AddressV=WRAP; };
texture Mask : register(t2);
sampler MaskSampler : register(s2) = sampler_state { Texture=<Mask>; MinFilter=POINT; MagFilter=POINT; MipFilter=NONE; AddressU=CLAMP; AddressV=CLAMP; };
texture Palette : register(t3);
sampler PaletteSampler : register(s3) = sampler_state { Texture=<Palette>; MinFilter=POINT; MagFilter=POINT; MipFilter=NONE; AddressU=CLAMP; AddressV=CLAMP; };
float4x4 MatrixTransform;
float Time;
float Charge;
float Mode;
float Strength;
float HasMask;
float4 Tint;
float2 Scroll;
float2 PatternSize;
float2 AtlasSize;
struct Output { float4 Position:SV_POSITION; float4 Color:COLOR0; float2 UV:TEXCOORD0; };
Output Vertex(float4 position:POSITION0,float4 color:COLOR0,float2 uv:TEXCOORD0)
{ Output o; o.Position=mul(position,MatrixTransform);o.Color=color;o.UV=uv;return o; }
float4 Pixel(Output input):SV_TARGET
{
    float4 source=tex2D(SpriteSampler,input.UV);
    float3 rgb=source.rgb/max(source.a,0.0001);
    float luminance=dot(rgb,float3(.2126,.7152,.0722));
    float3 result=rgb;
    float amount=Strength*lerp(1,tex2D(MaskSampler,input.UV).r,HasMask);
    float alpha=source.a;
    if(Mode<1.5) result=rgb*Tint.rgb;
    else if(Mode<2.5) result=tex2D(PaletteSampler,float2(luminance,.5)).rgb;
    else if(Mode<3.5) result=saturate(rgb+Tint.rgb*(.35+.65*Charge));
    else if(Mode<4.5) result=tex2D(PatternSampler,(input.UV*AtlasSize+Time*Scroll)/PatternSize).rgb;
    else if(Mode<5.5) result=lerp(float3(.19,.055,.005),float3(1,1,.65),luminance);
    else if(Mode<6.5) {result=lerp(float3(.25,.45,.6),float3(.85,.97,1),luminance);alpha*=lerp(1,.12+.3*luminance,amount);}
    else if(Mode<7.5) result=lerp(float3(.07,.005,.035),float3(1,.13,.52),luminance);
    else result=tex2D(PatternSampler,(input.UV*AtlasSize+Time*float2(1.5,-.4))/PatternSize).rgb;
    return float4(lerp(rgb,result,amount)*alpha,alpha)*input.Color;
}
technique Surface { pass Pass0 { VertexShader=compile vs_4_0_level_9_1 Vertex();PixelShader=compile ps_4_0_level_9_1 Pixel(); } }
