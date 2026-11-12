texture SpriteTexture : register(t0);
sampler SpriteSampler : register(s0) = sampler_state { Texture=<SpriteTexture>; MinFilter=POINT; MagFilter=POINT; MipFilter=NONE; AddressU=CLAMP; AddressV=CLAMP; };
texture Mask : register(t1);
sampler MaskSampler : register(s1) = sampler_state { Texture=<Mask>; MinFilter=POINT; MagFilter=POINT; MipFilter=NONE; AddressU=CLAMP; AddressV=CLAMP; };
float4x4 MatrixTransform;
float Time,LiquidLevel,LiquidTilt,LiquidWave,Facing;
struct Output {float4 Position:SV_POSITION;float4 Color:COLOR0;float2 UV:TEXCOORD0;};
Output Vertex(float4 position:POSITION0,float4 color:COLOR0,float2 uv:TEXCOORD0)
{Output o;o.Position=mul(position,MatrixTransform);o.Color=color;o.UV=uv;return o;}
float4 Pixel(Output input):SV_TARGET
{
    float4 source=tex2D(SpriteSampler,input.UV),mask=tex2D(MaskSampler,input.UV);
    float x=(mask.g-.5)*Facing;
    float surface=LiquidLevel+LiquidTilt*x+LiquidWave*sin(x*9+Time*3);
    float depth=surface-mask.b;
    float wet=step(0,depth)*step(.005,LiquidLevel)*mask.r;
    float crest=1-saturate(depth*32);
    float3 water=lerp(float3(.08,.34,.62),float3(.13,.73,.82),saturate(1-depth*1.1));
    water=lerp(water,float3(.65,1,.94),crest*.8);
    // Sparse rising glints are clipped to the wet interior
    float bubble=step(.982,sin(mask.g*83+floor(mask.b*21)*17))*step(.94,sin(mask.b*52+Time*3));
    water=lerp(water,float3(.7,1,1),bubble*.55);
    float alpha=lerp(source.a,.86,wet);
    float3 color=lerp(source.rgb,water*.86,wet);
    color+=source.rgb*wet*.14;
    return float4(min(color,alpha),alpha)*input.Color;
}
technique Vessel {pass Pass0 {VertexShader=compile vs_4_0_level_9_3 Vertex();PixelShader=compile ps_4_0_level_9_3 Pixel();}}
