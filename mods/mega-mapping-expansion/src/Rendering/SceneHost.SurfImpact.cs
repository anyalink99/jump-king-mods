using System;
using Microsoft.Xna.Framework;
namespace MegaMappingExpansion
{
    internal sealed partial class SceneHost
    {
        private readonly FoamMesh impactFoam=new FoamMesh();
        private void DrawSurfImpact(SurfData surf,SurfImpactData contact)
        {
            FoamMesh foam=impactFoam;foam.Count=0;
            int seed=StableHash(surf.Id)+(int)(contact.X*719+contact.Y*137);
            Color crest=prepared.Color(surf.CrestColor);
            Vector2 origin=new Vector2(contact.X,contact.Y);
            float angle=MathHelper.ToRadians(contact.Angle);
            Vector2 normal=new Vector2((float)Math.Cos(angle),(float)Math.Sin(angle));
            Vector2 tangent=new Vector2(-normal.Y,normal.X);
            // A sheet climbs the rock then drains. The origin is an authored rock
            // contact, independent of the free crest's travelling material frame.
            float arrival=SurfMotion.Impact(surf,contact,time);
            float backwash=SurfMotion.Impact(surf,contact,time-.72f)*.6f;
            for(int i=0;i<68;i++)
            {
                float side=(Hash01(seed+i*193)-.5f)*contact.Width;
                float reach=(.12f+Hash01(seed+i*397)*.88f)*contact.Reach;
                float rise=reach*arrival;
                Vector2 foot=origin+tangent*side;
                Vector2 tip=foot+normal*rise;
                if(arrival>.015f)foam.Line(foot,tip,MultiplyAlpha(crest,arrival*(.04f+Hash01(seed+i*571)*.17f)),.6f+arrival*1.1f);
                if(backwash>.025f)
                {
                    float drain=PositiveModulo(time*.8f+Hash01(seed+i*911),1);
                    Vector2 p=foot+normal*(reach*.36f*(1-drain));
                    foam.Line(p,p+new Vector2(surf.Wind*.006f,2+drain*7),MultiplyAlpha(crest,backwash*(1-drain)*.5f),.7f);
                }
            }
            // Birth-time pressure ties ballistic droplets to wave arrival. Each
            // parcel has a distinct lifetime; no common reset of the whole plume.
            for(int i=0;i<contact.Particles;i++)
            {
                float life=1.1f+Hash01(seed+i*71)*1.7f;
                float age=PositiveModulo(time+Hash01(seed+i*379)*life,life);
                float born=time-age,pressure=SurfMotion.Impact(surf,contact,born);
                if(pressure<.08f)continue;
                float spread=Hash01(seed+i*131)-.5f;
                float speed=(float)Math.Sqrt(contact.Reach*116)*(.45f+Hash01(seed+i*577)*.55f)*pressure;
                Vector2 velocity=normal*speed+tangent*(spread*38);
                Vector2 p=origin+tangent*(spread*contact.Width)+velocity*age+new Vector2(surf.Wind*age*age*.15f,age*age*29);
                float fade=(float)Math.Sin(age/life*Math.PI)*pressure*.72f;
                Vector2 trail=velocity*.014f+new Vector2(0,age*.8f);
                foam.Line(p,p+trail,MultiplyAlpha(crest,fade),i%9==0?1.4f:.7f);
            }
            DrawColoredMesh(foam.Vertices,foam.Indices,foam.Count*4,foam.Count*6);
        }
    }
}
