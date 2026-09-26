using System;
using JumpKing;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
namespace MegaMappingExpansion
{
    internal sealed partial class SceneHost
    {
        private sealed class ScatterMesh
        {
            internal const int Rays=48,Steps=40;
            internal readonly VertexPositionColor[] Vertices=new VertexPositionColor[(Rays+1)*(Steps+1)];
            internal readonly short[] Indices=new short[Rays*Steps*6];
            internal ScatterMesh()
            {
                int n=0;for(int y=0;y<Steps;y++)for(int x=0;x<Rays;x++)
                {short a=(short)(y*(Rays+1)+x),b=(short)(a+1),c=(short)(a+Rays+1),d=(short)(c+1);
                    Indices[n++]=a;Indices[n++]=b;Indices[n++]=c;Indices[n++]=b;Indices[n++]=d;Indices[n++]=c;}
            }
        }
        private readonly Dictionary<string,ScatterMesh> scatterMeshes=new Dictionary<string,ScatterMesh>();
        private float SpotAt(LightData light,Vector2 point)
        {
            Vector2 d=point-new Vector2(light.X,light.Y);
            if(light.ConeWidth<=0||d.LengthSquared()<.01f)return 1;
            d.Normalize();return SpotLight.Angular(d,SpotLight.Direction(light,time),light.ConeWidth);
        }
        private void DrawScatter(LightData light)
        {
            if(light.Scatter<=0||light.ConeWidth<=0)return;
            Vector2 source=new Vector2(light.X,light.Y),direction=SpotLight.Direction(light,time);
            Rectangle blocker=KingLightBlocker();
            if(OccludesKing(light) && LightVisibility.SourceVisibility(source,blocker)<=0)return;
            Color color=prepared.Color(light.Color);float gain=LightGain(light)*light.Scatter;
            ScatterMesh mesh;if(!scatterMeshes.TryGetValue(light.Id,out mesh)){mesh=new ScatterMesh();scatterMeshes.Add(light.Id,mesh);}
            // Shared vertices eliminate the bright seams caused by overlapping
            // alpha-blended polar strips. One draw submits the entire beam.
            for(int ray=0;ray<=ScatterMesh.Rays;ray++)
            {
                float offset=(ray/(float)ScatterMesh.Rays-.5f)*light.ConeWidth*MathHelper.Pi/180;
                Vector2 d=RotateVector(direction,offset);
                float angular=SpotLight.Angular(d,direction,light.ConeWidth);
                for(int step=0;step<=ScatterMesh.Steps;step++)
                {
                    float distance=step*light.Radius/ScatterMesh.Steps;
                    Vector2 point=source+d*distance;
                    float visibility=SceneVisibility(source,point)*(OccludesKing(light)?LightVisibility.Sample(source,point,blocker,light.ShadowOpacity):1);
                    float fade=LightVisibility.Attenuation(distance,light.Radius,.6f);
                    mesh.Vertices[step*(ScatterMesh.Rays+1)+ray]=new VertexPositionColor(new Vector3(point,0),MultiplyAlpha(color,gain*angular*visibility*fade));
                }
            }
            DrawColoredMesh(mesh.Vertices,mesh.Indices);
        }
    }
}
