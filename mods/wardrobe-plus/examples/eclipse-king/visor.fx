texture SpriteTexture : register(t0);
sampler SpriteSampler : register(s0) = sampler_state { Texture=<SpriteTexture>; MinFilter=POINT; MagFilter=POINT; MipFilter=NONE; AddressU=CLAMP; AddressV=CLAMP; };
texture Mask : register(t1);
sampler MaskSampler : register(s1) = sampler_state { Texture=<Mask>; MinFilter=POINT; MagFilter=POINT; MipFilter=NONE; AddressU=CLAMP; AddressV=CLAMP; };
float4x4 MatrixTransform;
float Time, Charge, IsCharging, JumpAge, LandingAge, JumpCharge;
struct Output { float4 Position:SV_POSITION; float4 Color:COLOR0; float2 UV:TEXCOORD0; };
Output Vertex(float4 position:POSITION0,float4 color:COLOR0,float2 uv:TEXCOORD0)
{ Output o; o.Position=mul(position,MatrixTransform);o.Color=color;o.UV=uv;return o; }
float4 Pixel(Output input):SV_TARGET
{
    float4 source=tex2D(SpriteSampler,input.UV);
    float4 mask=tex2D(MaskSampler,input.UV);
    float resting=.13+.012*sin(Time*1.4);
    float airborne=step(0,JumpAge);
    float afterLanding=step(0,LandingAge)*(1-airborne+airborne*step(LandingAge,JumpAge));
    float flight=airborne*(.78*JumpCharge*exp(-max(0,JumpAge)*1.6)+.65*exp(-max(0,JumpAge)*24));
    float heat=lerp(resting+flight*(1-afterLanding),saturate(Charge),IsCharging);
    heat*=1-afterLanding*(1-IsCharging)*.88*exp(-max(0,LandingAge)*12);
    heat=saturate(heat);
    // the stem ignites first, the horizontal opening fills outwards afterwards
    float fill=saturate((heat-mask.b+.13)*3.846);
    float local=lerp(heat,heat*fill,IsCharging);
    float3 white=float3(255,248,210)/255;
    float3 glow=saturate(float3(.18+local*2,.07+max(0,local-.2)*1.15,.04+max(0,local-.7)*2.6));
    float slit=step(.75,mask.r);
    float ray=step(.25,mask.r)*(1-slit)*saturate((heat-.84)*6.25)*.65;
    float3 color=lerp(source.rgb,glow*source.a,slit);
    color=lerp(color,float3(1,.68,.25)*source.a,mask.g*heat*heat*.65);
    color=lerp(color,white,ray);
    return float4(color,max(source.a,ray))*input.Color;
}
technique SolarVisor { pass Pass0 { VertexShader=compile vs_4_0_level_9_3 Vertex();PixelShader=compile ps_4_0_level_9_3 Pixel(); } }
