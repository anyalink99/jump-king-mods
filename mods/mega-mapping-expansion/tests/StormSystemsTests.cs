using System;
using System.IO;
using Microsoft.Xna.Framework;
namespace MegaMappingExpansion
{
    internal static partial class Tests
    {
        private static void StormSystemsChecks()
        {
            var planet=new PlanetData{X=240,Y=180,Radius=100};float coverage,facing;
            Vector2 uv=PlanetProjection.UV(planet,new Vector2(240,180),out coverage,out facing);
            Require(Vector2.Distance(uv,new Vector2(.5f,.5f))<.0001f && coverage==1 && facing==1,"sphere centre maps to its facing meridian");
            PlanetProjection.UV(planet,new Vector2(350,180),out coverage,out facing);
            Require(coverage==0,"planet does not cover pixels outside its limb");
            Require(PlanetProjection.Turn(30,120)==.25f && PlanetProjection.Turn(120,120)==0,"planet traverses all longitudes and completes a full rotation");
            var surf=new SurfData{Id="surf-test",Height=80,Period=7.3f,Wavelength=320};
            surf.Impacts=null;Require(surf.Impacts.Length==0,"absent XML impact children normalize to an empty contact list");
            for(int q=0;q<480;q+=17)
                Require(Vector2.Distance(SurfMotion.Point(surf,q,.13f,0),SurfMotion.Point(surf,q,.13f,surf.Period))<.001f,"wave material parcels close their period");
            Require(Math.Abs(SurfMotion.Point(surf,0,1,0).Y-(surf.Y+surf.Depth))<4,"deep wave motion decays into the water body");
            surf.Break=1.2f;
            bool hasOverhang=false;
            for(int q=0;q<80;q++)hasOverhang|=SurfMotion.Point(surf,q+1,0,0).X<SurfMotion.Point(surf,q,0,0).X;
            Require(hasOverhang,"the breaking crest has an open forward overhang");
            var frame=new SurfFrame();frame.Update(surf,3.57f);float error=0;
            for(int x=0;x<480;x+=3)for(int d=0;d<10;d++)
                error=Math.Max(error,Vector2.Distance(frame.Point(x,d/10f),SurfMotion.Point(surf,x,d/10f,3.57f)));
            Require(error<.2f,"shared surf deformation retains subpixel accuracy for foam and geometry");
            Vector2 smooth=SurfMotion.Point(surf,91,0,3.57f);
            surf.Chop=3.4f;surf.FoamDetail=1;
            Require(Vector2.Distance(smooth,SurfMotion.Point(surf,91,0,3.57f))>.05f,"short wind chop changes the carrier surface");
            frame.Update(surf,3.57f);error=0;
            for(int x=0;x<480;x+=3)for(int depth=0;depth<10;depth++)
                error=Math.Max(error,Vector2.Distance(frame.Point(x,depth/10f),SurfMotion.Point(surf,x,depth/10f,3.57f)));
            Require(error<.25f,"chopped water and its advected foam share a subpixel-accurate deformation");
            bool finite=true;
            for(int t=0;t<1800;t+=7)
            {
                Vector2 point=SurfMotion.Point(surf,91,.13f,t*.13f);
                finite&=!float.IsNaN(point.X)&&!float.IsNaN(point.Y)&&!float.IsInfinity(point.X)&&!float.IsInfinity(point.Y);
            }
            Require(finite,"chopped wave remains finite through independent phases");
            var distant=new SurfData{Height=5,Width=560,Wavelength=170,Period=11,Break=.7f,Chop=.25f};
            frame.Update(distant,7.17f);error=0;
            for(int x=0;x<480;x++)error=Math.Max(error,Vector2.Distance(frame.Point(x,.13f),SurfMotion.Point(distant,x,.13f,7.17f)));
            Require(error<.1f,"distant-swell sampling saves work without a visible contour regression");
            surf.Variation=1;
            Require(Vector2.Distance(SurfMotion.Point(surf,91,0,0),SurfMotion.Point(surf,91,0,surf.Period))>1,"variable wave sets do not repeat at the carrier period");
            var contact=new SurfImpactData{X=91,Y=220};
            float strongest=0,weakest=1;
            for(int t=0;t<300;t++){float pressure=SurfMotion.Impact(surf,contact,t*.1f);strongest=Math.Max(strongest,pressure);weakest=Math.Min(weakest,pressure);}
            Require(strongest>.95f&&weakest==0,"rock contacts receive arriving waves with quiet intervals, not continuous fountains");
            contact.ArrivalPhase=.25f;
            Require(SurfMotion.Impact(surf,contact,surf.Period*.75f)>.999f && SurfMotion.Impact(surf,contact,surf.Period*.25f)==0,"baked perspective contacts accept independent periodic arrival phases");
            contact.X+=80;
            Require(SurfMotion.Impact(surf,contact,surf.Period*.75f)>.999f,"baked arrival timing is not coupled to a horizontal cross-section coordinate");
            var light=new LightData{Id="spot-test",ConeWidth=25,Angle=180,SweepAngle=12,SweepPeriod=19};
            Vector2 direction=SpotLight.Direction(light,0);
            Require(SpotLight.Angular(direction,direction,25)>.999f && SpotLight.Angular(-direction,direction,25)==0,"spotlight illuminates its forward cone only");
            Require(Vector2.Distance(direction,SpotLight.Direction(light,19))<.0001f,"lighthouse sweep closes continuously");
            var scene=new SceneFile{Surfs=new[]{surf},Lights=new[]{light},Rains=new[]{new RainData{Id="snow-test",Snow=true,Drift=8}}};
            SceneValidation.Validate(scene,".",1);
            surf.FoamDetail=1.1f;bool detailRejected=false;
            try{SceneValidation.Validate(scene,".",1);}catch(InvalidDataException){detailRejected=true;}
            Require(detailRejected,"foam detail has a bounded geometry budget");surf.FoamDetail=1;
            surf.Period=0;bool rejected=false;try{SceneValidation.Validate(scene,".",1);}catch(InvalidDataException){rejected=true;}
            Require(rejected,"singular surf periods fail before render preparation");
        }
    }
}
