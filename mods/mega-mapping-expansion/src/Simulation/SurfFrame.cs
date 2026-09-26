using System;
using Microsoft.Xna.Framework;
namespace MegaMappingExpansion
{
    // Shared deformation for mesh, foam and streaks. Sampling does not allocate
    // or evaluate trigonometry; historical spray uses the analytic motion model.
    internal sealed class SurfFrame
    {
        private const int Samples=768, DepthSamples=128;
        private readonly Vector2[] displacement=new Vector2[Samples+1];
        private readonly float[] decay=new float[DepthSamples+1];
        private SurfData surf;
        private int samples;
        private float start,span,seconds;
        internal SurfFrame(){for(int i=0;i<=DepthSamples;i++)decay[i]=(float)Math.Exp(-i/(float)DepthSamples*3.2f);}
        internal void Update(SurfData data,float time)
        {
            surf=data;seconds=time;start=-data.Height*2;span=data.Width+data.Height*4;
            samples=data.Height<10?192:data.Height<30?384:Samples;
            for(int i=0;i<=samples;i++)
            {float q=start+i/(float)samples*span;displacement[i]=SurfMotion.Point(data,q,0,time)-new Vector2(data.X+q,data.Y);}
        }
        internal Vector2 Point(float q,float depth)
        {
            float index=(q-start)/span*samples;
            if(index<0||index>samples||depth<0||depth>1)return SurfMotion.Point(surf,q,depth,seconds);
            int a=Math.Min(samples-1,(int)index);float t=index-a;
            float di=depth*DepthSamples;int d=Math.Min(DepthSamples-1,(int)di);
            float attenuation=MathHelper.Lerp(decay[d],decay[d+1],di-d);
            return new Vector2(surf.X+q,surf.Y+depth*surf.Depth)+Vector2.Lerp(displacement[a],displacement[a+1],t)*attenuation;
        }
    }
}
