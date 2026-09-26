using System;
using System.IO;
namespace MegaMappingExpansion
{
    internal static partial class Tests
    {
        private static void SurfaceShadowChecks()
        {
            StormSystemsChecks();
            var rimLight=new LightData{Radius=700};
            Require(LightEnvelope.Rim(120,rimLight)==0 && LightEnvelope.Rim(20,rimLight)>0,
                "a broad fill light does not outline the King outside the local rim zone");
            rimLight.RimIntensity=0;
            Require(LightEnvelope.Rim(0,rimLight)==0,"fill-only lights can explicitly disable rim contribution");
            var footprint=new ShadowFootprint(60,45);
            foreach(var source in new[]{new Microsoft.Xna.Framework.Vector2(110,90),new Microsoft.Xna.Framework.Vector2(-30,40),new Microsoft.Xna.Framework.Vector2(237,175)})
                foreach(var box in new[]{new Microsoft.Xna.Framework.Rectangle(70,50,18,26),new Microsoft.Xna.Framework.Rectangle(105,82,18,26),new Microsoft.Xna.Framework.Rectangle(197,145,18,26)})
                {
                    footprint.Build(source,box,400,4,4);
                    bool complete=true;
                    for(int y=0;y<45;y++)for(int x=0;x<60;x++)
                        if(LightVisibility.Sample(source,new Microsoft.Xna.Framework.Vector2((x+.5f)*4,(y+.5f)*4),box,1)<1)
                            complete &= x>=footprint.Left[y]&&x<=footprint.Right[y];
                    Require(complete,"bounded shadow updates conservatively contain every affected soft-shadow pixel");
                }
            Require(GroundProjection.Height(292,280,.5f)==24,"rear sun flattens upright height towards foreground");
            Require(GroundProjection.X(100,24,-.5f)==88,"directional ground shadow retains authored lateral shear");
            Require(LightEnvelope.Pulse(0,280,.2f,0)==.8f && LightEnvelope.Pulse(140,280,.2f,0)==1,
                "slow planetary bounce reaches its authored low and high values");
            Require(Math.Abs(LightEnvelope.Pulse(0,280,.2f,0)-LightEnvelope.Pulse(280,280,.2f,0))<.0001f,
                "light pulse closes continuously");
            var surface=new ShadowSurfaceData{Id="roof-shadow",PlaneY=280,Outline="10,280;90,280;90,310;10,310"};
            var data=new SceneFile{ShadowSurfaces=new[]{surface},ScreenLooks=new[]{new ScreenLook{Screen=1,AmbientLight="#FFF3DC",AmbientIntensity=.95f}}};
            SceneValidation.Validate(data,".",1);
            Require(new SceneFile().ShadowSurfaces.Length==0,"existing scenes do not acquire ground shadows");
            var solid=new Microsoft.Xna.Framework.Color((byte)60,(byte)40,(byte)20,(byte)255);
            Require(SceneHost.AppearanceOver(Microsoft.Xna.Framework.Color.Transparent,solid)==solid,
                "transparent equipment does not erase the underlying King silhouette");
            Require(SceneHost.AppearanceOver(solid,Microsoft.Xna.Framework.Color.White)==solid,
                "opaque equipment replaces underlying pixels without increasing shadow alpha");
            surface.ScaleY=0;
            bool rejected=false;try{SceneValidation.Validate(data,".",1);}catch(InvalidDataException){rejected=true;}
            Require(rejected,"singular ground projection fails before rendering");
            var pin=new Microsoft.Xna.Framework.Vector2(12,20);
            Require(ClothMotion.Bend(pin,pin,40,3,1,4)==pin,"cloth attachment remains fixed");
            var hem=new Microsoft.Xna.Framework.Vector2(25,60);
            Require(Microsoft.Xna.Framework.Vector2.Distance(ClothMotion.Bend(hem,pin,40,3,0,4),ClothMotion.Bend(hem,pin,40,3,4,4))<.0001f,
                "cloth fold and swing phases close without a reset");
        }
    }
}
