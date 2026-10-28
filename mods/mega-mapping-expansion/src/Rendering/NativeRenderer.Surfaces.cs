using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MegaMappingExpansion.Api;
using JumpKing;

namespace MegaMappingExpansion.Construction
{
    internal sealed partial class NativeRenderer
    {
        private sealed class SurfaceBuffer
        {
            internal SceneValue[] Local,World;
            internal VertexPositionColorTexture[] Vertices;
            internal DualVertex[] MaskedVertices;
            internal short[] Indices;
            internal bool Active;
        }
        private sealed class LightBuffer
        {
            internal readonly VertexPositionColorTexture[] Vertices=new VertexPositionColorTexture[193*13];
            internal readonly short[] Indices=new short[192*12*6];
            internal SurfaceBuffer[] Occluders;
            internal LightBuffer()
            {
                int n=0;
                for(int ring=0;ring<12;ring++)for(int ray=0;ray<192;ray++)
                {
                    short a=(short)(ring*193+ray),b=(short)(a+1),c=(short)(a+193),d=(short)(c+1);
                    Indices[n++]=a;Indices[n++]=b;Indices[n++]=c;Indices[n++]=b;Indices[n++]=d;Indices[n++]=c;
                }
            }
        }
        private readonly Dictionary<string,SurfaceBuffer> surfaces=new Dictionary<string,SurfaceBuffer>(StringComparer.Ordinal);
        private readonly Dictionary<string,LightBuffer> lights=new Dictionary<string,LightBuffer>(StringComparer.Ordinal);
        private long surfaceFrame=-1;
        private int surfaceScreen;
        private void PrepareSurfaces()
        {
            foreach(var component in scene.Components.Values.Where(c=>c.Descriptor.Kind=="receiver"))
            {
                if(surfaces.Count>=256)throw new InvalidDataException("Scene supports at most 256 receiver contours");
                var points=GeometryMath.Points(Text(component.Id,"outline"));
                surfaces.Add(component.Id,new SurfaceBuffer{Local=points,World=new SceneValue[points.Length],Vertices=new VertexPositionColorTexture[points.Length],MaskedVertices=new DualVertex[points.Length],Indices=GeometryMath.Triangulate(points)});
                if(Text(component.Id,"mode")!="occlusion")
                {
                    string material=Text(component.Id,"material");
                    if(material==""||Text(material,"capture")=="")throw new InvalidDataException(component.Id+": a projected receiver needs a material with a capture source");
                }
            }
            foreach(var component in scene.Components.Values.Where(c=>c.Descriptor.Kind=="light"))
            {
                if(lights.Count>=64)throw new InvalidDataException("Scene supports at most 64 lights");
                string selection=Text(component.Id,"occluders");
                var selected=scene.Components.Values.Where(c=>c.Descriptor.Kind=="receiver"&&Text(c.Id,"mode")=="occlusion"&&Selected(c,selection)).Select(c=>surfaces[c.Id]).ToArray();
                if(selected.Sum(s=>s.Local.Length)>2048)throw new InvalidDataException(component.Id+": light occlusion exceeds 2048 contour edges");
                lights.Add(component.Id,new LightBuffer{Occluders=selected});
            }
            foreach(var component in scene.Components.Values.Where(c=>c.Descriptor.Kind=="text"))
            {
                string font=Text(component.Id,"font");
                if(!NativeNarrativeText.IsFont(font)&&!assets.Bitmaps.ContainsKey(font))throw new InvalidDataException(component.Id+": font must be a native preset or a font atlas asset ID");
                if(!NativeNarrativeText.IsFont(font)&&Text(component.Id,"characters").Length>assets.Bitmaps[font].Frames)throw new InvalidDataException(component.Id+": font atlas has fewer cells than its character table");
                if(NativeNarrativeText.IsFont(font))PrepareNativeText(component.Id);
            }
        }
        private void RefreshSurfaces(int screen)
        {
            if(surfaceFrame==renderFrame&&surfaceScreen==screen)return;surfaceFrame=renderFrame;surfaceScreen=screen;
            foreach(var entry in surfaces)
            {
                var pose=Pose(scene.Components[entry.Key]);
                entry.Value.Active=pose.Visible&&(pose.Screen==0||pose.Screen==screen);
                for(int i=0;i<entry.Value.Local.Length;i++)entry.Value.World[i]=pose.Point(entry.Value.Local[i]);
            }
        }
        private void DrawReceiver(CompiledComponent component,ComponentPose pose,string material,int screen)
        {
            FlushQuads();
            string mode=Text(component.Id,"mode");if(mode=="occlusion")return;
            var projection=Value(component.Id,"projection");if(Math.Abs(projection.Y)<1e-8)return;
            var capture=captures[Text(material,"capture")];Texture2D texture=mode=="shadow"?capture.Alpha:MaterialTexture(material,"");
            var buffer=surfaces[component.Id];var tint=Tint(material,Number(component.Id,"opacity"));
            string maskId=Text(material,"mask");Texture2D mask=maskId==""?null:masks[maskId];bool screenMask=mask!=null&&Text(material,"maskSpace")=="screen";
            double left=0,top=0,width=1,height=1;
            if(mask!=null&&!screenMask){left=buffer.Local.Min(p=>p.X);top=buffer.Local.Min(p=>p.Y);width=Math.Max(.001,buffer.Local.Max(p=>p.X)-left);height=Math.Max(.001,buffer.Local.Max(p=>p.Y)-top);}
            for(int i=0;i<buffer.Local.Length;i++)
            {
                var local=buffer.Local[i];double sourceY=-local.Y/Math.Abs(projection.Y),sourceX=local.X-sourceY*projection.X;
                var point=pose.RenderPoint(local);var source=pose.RenderPoint(SceneValue.Vector(sourceX,sourceY));
                buffer.Vertices[i]=new VertexPositionColorTexture(new Vector3((float)point.X,(float)point.Y,0),tint,new Vector2((float)((source.X+1)/482),(float)((source.Y+1)/362)));
                if(mask!=null)buffer.MaskedVertices[i]=new DualVertex{Position=buffer.Vertices[i].Position,Color=tint,UV=buffer.Vertices[i].TextureCoordinate,MaskUV=screenMask?new Vector2((float)point.X/mask.Width,(float)point.Y/mask.Height):new Vector2((float)((local.X-left)/width),(float)((local.Y-top)/height))};
            }
            effect.Texture=texture;effect.DiffuseColor=Vector3.One;device.BlendState=Blend(material);device.DepthStencilState=DepthStencilState.None;device.RasterizerState=RasterizerState.CullNone;
            if(mask==null)foreach(var pass in effect.CurrentTechnique.Passes){pass.Apply();device.SamplerStates[0]=Sampler(material,pose.PixelSnap);device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList,buffer.Vertices,0,buffer.Vertices.Length,buffer.Indices,0,buffer.Indices.Length/3);}
            else{dualEffect.Texture=texture;dualEffect.Texture2=mask;dualEffect.DiffuseColor=new Vector3(.5f);foreach(var pass in dualEffect.CurrentTechnique.Passes){pass.Apply();device.SamplerStates[0]=Sampler(material,pose.PixelSnap);device.SamplerStates[1]=SamplerState.PointClamp;device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList,buffer.MaskedVertices,0,buffer.MaskedVertices.Length,buffer.Indices,0,buffer.Indices.Length/3);}}
        }
        private static double Cross2(double ax,double ay,double bx,double by){return ax*by-ay*bx;}
        private static bool Enclosed(double x,double y,SurfaceBuffer[] occluders)
        {
            foreach(var surface in occluders)if(surface.Active&&GeometryMath.Contains(surface.World,x,y))return true;
            return false;
        }
        private static double RayLimit(double x,double y,double dx,double dy,double radius,SurfaceBuffer[] occluders)
        {
            double nearest=radius;
            foreach(var surface in occluders)
            {
                if(!surface.Active)continue;
                for(int i=0;i<surface.World.Length;i++)
                {
                    var a=surface.World[i];var b=surface.World[(i+1)%surface.World.Length];double ex=b.X-a.X,ey=b.Y-a.Y,denominator=Cross2(dx,dy,ex,ey);
                    if(Math.Abs(denominator)<1e-10)continue;
                    double distance=Cross2(a.X-x,a.Y-y,ex,ey)/denominator,along=Cross2(a.X-x,a.Y-y,dx,dy)/denominator;
                    if(distance>=0&&distance<nearest&&along>=0&&along<=1)nearest=distance;
                }
            }
            return nearest;
        }
        private void DrawLight(CompiledComponent component,ComponentPose pose,int screen)
        {
            string id=component.Id;double intensity=Number(id,"intensity");var color=Value(id,"color");
            if(intensity<=0||color.W<=0)return;
            FlushQuads();
            RefreshSurfaces(screen);var buffer=lights[id];double radius=Number(id,"radius")*Math.Max(Math.Abs(pose.ScaleX),Math.Abs(pose.ScaleY)),cone=Number(id,"cone")*Math.PI/180;
            double start=pose.Rotation*Math.PI/180-cone/2,softness=Number(id,"softness"),scatter=Number(id,"scatter");
            int samples=buffer.Occluders.Length==0||softness==0?1:4;
            for(int sample=scatter>0?-1:0;sample<samples;sample++)
            {
                double angle=sample*Math.PI/2,jitter=sample<0?0:radius*.025*softness,x=pose.X+Math.Cos(angle)*jitter,y=pose.Y+Math.Sin(angle)*jitter;
                bool enclosed=sample>=0&&Enclosed(x,y,buffer.Occluders);
                for(int ray=0;ray<=192;ray++)
                {
                    double a=start+cone*ray/192,dx=Math.Cos(a),dy=Math.Sin(a),limit=sample<0?radius:enclosed?0:RayLimit(x,y,dx,dy,radius,buffer.Occluders);
                    double edge=cone>=Math.PI*2-.001?1:Math.Min(1,Math.Min(ray,192-ray)/(1+softness*16));
                    for(int ring=0;ring<=12;ring++)
                    {
                        double distance=limit*ring/12,gain=intensity*color.W*edge*Math.Pow(Math.Max(0,1-distance/Math.Max(1,radius)),1+softness*3)*(sample<0?scatter*.1:1.0/samples);
                        // the last ring closes the shadow edge without leaking past its contour
                        if(ring==12)gain=0;
                        buffer.Vertices[ring*193+ray]=new VertexPositionColorTexture(new Vector3((float)(x+dx*distance),(float)(y+dy*distance),0),new Color((float)Math.Min(1,color.X*gain),(float)Math.Min(1,color.Y*gain),(float)Math.Min(1,color.Z*gain),0),Vector2.Zero);
                    }
                }
                effect.Texture=whiteTexture;effect.DiffuseColor=Vector3.One;device.BlendState=emissionBlend;device.DepthStencilState=DepthStencilState.None;device.RasterizerState=RasterizerState.CullNone;
                foreach(var pass in effect.CurrentTechnique.Passes){pass.Apply();device.SamplerStates[0]=SamplerState.PointClamp;device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList,buffer.Vertices,0,buffer.Vertices.Length,buffer.Indices,0,buffer.Indices.Length/3);}
            }
        }
    }
}
