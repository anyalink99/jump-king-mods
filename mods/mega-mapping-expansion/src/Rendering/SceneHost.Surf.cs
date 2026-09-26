using System;
using System.Collections.Generic;
using JumpKing;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
namespace MegaMappingExpansion
{
    internal sealed partial class SceneHost
    {
        private sealed class SurfMesh
        {
            internal readonly int Columns,Rows;
            internal readonly VertexPositionColor[] Vertices;
            internal readonly short[] Indices;
            internal readonly Vector2[] Displacements;
            internal readonly float[] Faces,Crests,Coordinates,SinFold,CosFold;
            internal readonly float[] SinA,CosA,SinB,CosB;
            internal readonly FoamMesh Foam=new FoamMesh();
            internal readonly SurfFrame Frame=new SurfFrame();
            internal SurfMesh(SurfData surf)
            {
                // Distant 5px swells do not need the same vertical tessellation
                // as an overturning 100px crest. Foam detail is unchanged.
                Columns=surf.Height<30?160:240;Rows=surf.Height<10?6:surf.Height<30?12:24;
                Vertices=new VertexPositionColor[(Columns+1)*(Rows+1)];Indices=new short[Columns*Rows*6];
                Displacements=new Vector2[Columns+1];Faces=new float[Columns+1];Crests=new float[Columns+1];Coordinates=new float[Columns+1];
                SinFold=new float[Columns+1];CosFold=new float[Columns+1];SinA=new float[Columns+1];CosA=new float[Columns+1];SinB=new float[Columns+1];CosB=new float[Columns+1];
                for(int x=0;x<=Columns;x++)
                {
                    float q=Coordinates[x]=-surf.Height*2+x/(float)Columns*(surf.Width+surf.Height*4);
                    float fold=q*.41f+(float)Math.Sin(q*.097f)*3;
                    SinFold[x]=(float)Math.Sin(fold);CosFold[x]=(float)Math.Cos(fold);
                    SinA[x]=(float)Math.Sin(q*.32f);CosA[x]=(float)Math.Cos(q*.32f);
                    SinB[x]=(float)Math.Sin(q*.117f);CosB[x]=(float)Math.Cos(q*.117f);
                }
                int n=0;for(int y=0;y<Rows;y++)for(int x=0;x<Columns;x++)
                {short a=(short)(y*(Columns+1)+x),b=(short)(a+1),c=(short)(a+Columns+1),d=(short)(c+1);
                    Indices[n++]=a;Indices[n++]=b;Indices[n++]=c;Indices[n++]=b;Indices[n++]=d;Indices[n++]=c;}
            }
        }
        private sealed class FoamMesh
        {
            private const int Capacity=8100;
            internal readonly VertexPositionColor[] Vertices=new VertexPositionColor[Capacity*4];
            internal readonly short[] Indices=new short[Capacity*6];
            internal int Count;
            internal FoamMesh()
            {
                for(int i=0;i<Capacity;i++)
                {int n=i*6;short a=(short)(i*4);Indices[n]=a;Indices[n+1]=(short)(a+1);Indices[n+2]=(short)(a+2);Indices[n+3]=(short)(a+1);Indices[n+4]=(short)(a+3);Indices[n+5]=(short)(a+2);}
            }
            internal void Line(Vector2 a,Vector2 b,Color color,float width)
            {
                Vector2 edge=b-a;float length=edge.Length();
                if(length<.01f||color.A==0)return;
                if(Math.Max(a.X,b.X)<-width||Math.Min(a.X,b.X)>480+width||Math.Max(a.Y,b.Y)<-width||Math.Min(a.Y,b.Y)>360+width)return;
                if(Count>=Capacity)throw new InvalidOperationException("Surf foam mesh capacity exceeded");
                Vector2 normal=new Vector2(-edge.Y,edge.X)*(width*.5f/length);
                int n=Count++*4;
                Vertices[n]=new VertexPositionColor(new Vector3(a-normal,0),color);
                Vertices[n+1]=new VertexPositionColor(new Vector3(b-normal,0),color);
                Vertices[n+2]=new VertexPositionColor(new Vector3(a+normal,0),color);
                Vertices[n+3]=new VertexPositionColor(new Vector3(b+normal,0),color);
            }
        }
        private readonly Dictionary<string,SurfMesh> surfMeshes=new Dictionary<string,SurfMesh>();
        private BasicEffect surfEffect;
        private void DrawSurf(SurfData surf)
        {
            SurfMesh mesh;if(!surfMeshes.TryGetValue(surf.Id,out mesh)){mesh=new SurfMesh(surf);surfMeshes.Add(surf.Id,mesh);}
            Color body=prepared.Color(surf.Color),crest=prepared.Color(surf.CrestColor);
            int seed=StableHash(surf.Id);
            mesh.Frame.Update(surf,time);
            // Each depth row is the same surface profile with exponential decay.
            // Evaluate the expensive periodic curve once per column, not 25 times.
            for(int x=0;x<=mesh.Columns;x++)
            {
                float coordinate=mesh.Coordinates[x];
                mesh.Displacements[x]=mesh.Frame.Point(coordinate,0)-new Vector2(surf.X+coordinate,surf.Y);
                float phase=SurfMotion.Phase(surf,coordinate,time)*MathHelper.TwoPi;
                mesh.Faces[x]=(.5f+.5f*(float)Math.Sin(phase-.5f))*.42f;
                float crestBand=Math.Max(0,(float)Math.Cos(phase+.28f));crestBand*=crestBand;mesh.Crests[x]=crestBand*crestBand*crestBand;
            }
            for(int y=0;y<=mesh.Rows;y++)
            {
                float depth=y/(float)mesh.Rows;
                float decay=(float)Math.Exp(-depth*3.2f),sa=(float)Math.Sin(depth*16),ca=(float)Math.Cos(depth*16),sb=(float)Math.Sin(depth*9),cb=(float)Math.Cos(depth*9);
                float sf=(float)Math.Sin(depth*21),cf=(float)Math.Cos(depth*21);
                for(int x=0;x<=mesh.Columns;x++)
                {
                float coordinate=mesh.Coordinates[x];
                Vector2 p=new Vector2(surf.X+coordinate,surf.Y+depth*surf.Depth)+mesh.Displacements[x]*decay;
                float face=mesh.Faces[x]*(1-depth);
                float vein=(mesh.SinA[x]*ca+mesh.CosA[x]*sa)*(mesh.SinB[x]*cb-mesh.CosB[x]*sb)*.1f;
                float foam=mesh.Crests[x]*Math.Max(0,1-depth*9);
                float folds=.5f+.5f*(mesh.SinFold[x]*cf-mesh.CosFold[x]*sf);
                float sheet=surf.FoamDetail>0?.18f:.48f;
                Color c=Color.Lerp(body,crest,MathHelper.Clamp(face+vein+foam*(sheet+folds*(surf.FoamDetail>0?.28f:.38f)),0,.93f));
                mesh.Vertices[y*(mesh.Columns+1)+x]=new VertexPositionColor(new Vector3(p,0),c);
                }
            }
            DrawColoredMesh(mesh.Vertices,mesh.Indices);
            // Surface foam follows material parcels, including the overturning lip.
            // Fine streamlines inside the face are separate from the bright crest.
            DrawSurfFoam(surf,crest,seed,mesh.Foam,mesh.Frame);
        }
        private void DrawColoredMesh(VertexPositionColor[] vertices,short[] indices)
        {DrawColoredMesh(vertices,indices,vertices.Length,indices.Length);}
        private void DrawColoredMesh(VertexPositionColor[] vertices,short[] indices,int vertexCount,int indexCount)
        {
            if(indexCount==0)return;
            GraphicsDevice device=Game1.instance.GraphicsDevice;
            if(surfEffect==null)surfEffect=new BasicEffect(device){VertexColorEnabled=true};
            Game1.instance.EndBatch();
            try
            {
                surfEffect.Projection=Matrix.CreateOrthographicOffCenter(0,device.Viewport.Width,device.Viewport.Height,0,0,1);
                device.BlendState=BlendState.AlphaBlend;device.DepthStencilState=DepthStencilState.None;device.RasterizerState=RasterizerState.CullNone;
                foreach(var pass in surfEffect.CurrentTechnique.Passes){pass.Apply();device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList,vertices,0,vertexCount,indices,0,indexCount/3);}
            }
            finally{Game1.instance.StartBatch();}
        }
        private void DrawSurfFoam(SurfData surf,Color crest,int seed,FoamMesh foam,SurfFrame frame)
        {
            foam.Count=0;
            int contourSamples=surf.Height<10?128:surf.Height<30?192:320;
            int flowBands=surf.Height<10?0:surf.Height<30?1:2;
            for(int i=0;i<contourSamples;i++)
            {
                float q=-surf.Height*2+i/(float)contourSamples*(surf.Width+surf.Height*4);
                float phase=SurfMotion.Phase(surf,q,time)*MathHelper.TwoPi;
                float strength=.18f+.65f*(float)Math.Pow(Math.Max(0,Math.Cos(phase)),4);
                Vector2 a=frame.Point(q,0),b=frame.Point(q+2.5f,0);
                foam.Line(a,b,MultiplyAlpha(crest,strength*(.45f+Hash01(seed+i*71)*.55f)),1);
                if(i%3==0)
                    for(int band=1;band<=flowBands;band++)
                    {
                        float depth=.025f*band+Hash01(seed+i*31)*.04f;
                        a=frame.Point(q,depth);
                        for(int flow=1;flow<=6;flow++)
                        {
                            float dd=depth+flow*.035f;
                            b=frame.Point(q+flow*1.8f,dd);
                            foam.Line(a,b,MultiplyAlpha(crest,strength*.16f*(1-flow/8f)),.6f);a=b;
                        }
                    }
            }
            // Irregular foam sheet wraps over the crest, with small open pockets.
            // Its coordinates are material-space, so the foam bends with the wave.
            int foamCount=surf.Height>30?(surf.FoamDetail>0?1550:2450):surf.Height<10?120:240;
            for(int i=0;i<foamCount;i++)
            {
                int crestIndex=(int)Math.Floor(-SurfMotion.Travel(surf,time)+surf.Phase)+(i%3);
                int foamSeed=seed+(i/3)*193+crestIndex*7919;
                float q=SurfMotion.CrestCoordinate(surf,time,crestIndex)+(Hash01(foamSeed)-.5f)*surf.Wavelength*.38f;
                float phase=SurfMotion.Phase(surf,q,time)*MathHelper.TwoPi;
                float envelope=(float)Math.Pow(Math.Max(0,Math.Cos(phase+.28f)),5);
                if(envelope<.1f)continue;
                float depth=Hash01(foamSeed+311)*.24f;
                Vector2 p=frame.Point(q,depth);
                float size=(1.1f+Hash01(foamSeed+91)*2.7f)*(1-depth*2);
                Vector2 next=frame.Point(q+size,depth+.009f);
                foam.Line(p,next,MultiplyAlpha(crest,envelope*(1-depth*2.8f)),size>1.8f?2.1f:1);
            }
            // Ballistic spray is emitted from the crest at its historical phase.
            // Birth/death fade occurs while subpixel, so wrap has no visible snap.
            for(int i=0;i<surf.Spray;i++)
            {
                float life=.7f+Hash01(seed+i*71)*1.3f;
                float age=PositiveModulo(time/life+Hash01(seed+i*97),1)*life;
                float born=time-age;
                int crestIndex=(int)Math.Floor(-SurfMotion.Travel(surf,born)+surf.Phase)+(i%3);
                float q=SurfMotion.CrestCoordinate(surf,born,crestIndex);
                Vector2 source=SurfMotion.Point(surf,q,0,born);
                float spread=Hash01(seed+i*131)-.5f;
                Vector2 p=source+new Vector2(age*(surf.Wind+spread*38),-age*(16+surf.Height*.9f*Hash01(seed+i*317))+age*age*29);
                float opacity=(float)Math.Sin(age/life*Math.PI)*.68f;
                foam.Line(p,p+new Vector2(surf.Wind*.012f,1),MultiplyAlpha(crest,opacity),i%7==0?1.3f:.7f);
            }
            DrawFoamLace(surf,crest,seed,foam,frame);
            DrawColoredMesh(foam.Vertices,foam.Indices,foam.Count*4,foam.Count*6);
        }
    }
}
