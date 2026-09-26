using System;
using Microsoft.Xna.Framework;
namespace MegaMappingExpansion
{
    internal sealed partial class SceneHost
    {
        private void DrawFoamLace(SurfData surf,Color crest,int seed,FoamMesh mesh,SurfFrame frame)
        {
            if(surf.FoamDetail<=0)return;
            int count=(int)(surf.FoamDetail*330);
            float travel=SurfMotion.Travel(surf,time);
            for(int i=0;i<count;i++)
            {
                int index=(int)Math.Floor(-travel+surf.Phase)+(i%3);
                int cell=seed+(i/3)*193+index*7919;
                float lifetime=3.4f+Hash01(cell+317)*3;
                float age=PositiveModulo(time/lifetime+Hash01(cell+711),1);
                float envelope=(float)Math.Sin(age*Math.PI);
                float q=SurfMotion.CrestCoordinate(surf,time,index)+(Hash01(cell)-.5f)*surf.Wavelength*.38f;
                float depth=.012f+Hash01(cell+61)*.19f+age*.095f;
                float width=1.4f+Hash01(cell+97)*4.2f;
                float stretch=.013f+Hash01(cell+137)*.032f+age*.019f;
                // Advected pockets have thin, uneven walls and elongated drainage
                // channels. They deform in the same material frame as the water.
                Vector2 first=Vector2.Zero,previous=Vector2.Zero;
                float pressure=Math.Max(0,(float)Math.Cos(SurfMotion.Phase(surf,q,time)*MathHelper.TwoPi));
                for(int side=0;side<=6;side++)
                {
                    float a=side*MathHelper.TwoPi/6;
                    float uneven=.72f+Hash01(cell+(side%6)*511)*.5f;
                    float u=q+(float)Math.Cos(a)*width*uneven;
                    float v=depth+(float)Math.Sin(a)*stretch*uneven;
                    Vector2 p=frame.Point(u,Math.Max(0,v));
                    if(side==0)first=p;
                    else
                    {
                        float alpha=envelope*pressure*(.35f+Hash01(cell+side*371)*.4f)*(1-depth*1.9f);
                        mesh.Line(previous,p,MultiplyAlpha(crest,alpha),.55f+Hash01(cell+side*71)*.5f);
                    }
                    previous=p;
                }
                if(i%3==0)
                {
                    Vector2 tail=frame.Point(q-width*.4f,depth+stretch*2.6f);
                    mesh.Line(first,tail,MultiplyAlpha(crest,envelope*pressure*.19f),.55f);
                }
            }
        }
    }
}
