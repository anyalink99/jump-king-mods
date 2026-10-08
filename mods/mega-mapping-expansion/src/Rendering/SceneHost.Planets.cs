using System;
using System.Collections.Generic;
using JumpKing;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
namespace MegaMappingExpansion
{
    internal sealed partial class SceneHost
    {
        private sealed class PlanetMesh
        {
            internal readonly VertexPositionColorTexture[] Vertices;
            internal readonly Vector2[] UV;
            internal readonly Color[] Tints;
            internal readonly short[] Indices;
            internal PlanetMesh(PlanetData p)
            {
                const int cols=120,rows=90;
                Vertices=new VertexPositionColorTexture[(cols+1)*(rows+1)];UV=new Vector2[Vertices.Length];Tints=new Color[Vertices.Length];
                Indices=new short[cols*rows*6];int n=0;
                for(int y=0;y<=rows;y++)for(int x=0;x<=cols;x++)
                {
                    int i=y*(cols+1)+x;Vector2 position=new Vector2(x*4,y*4);
                    float coverage,facing;UV[i]=PlanetProjection.UV(p,position,out coverage,out facing);
                    float shade=.64f+.36f*facing;
                    Tints[i]=MultiplyAlpha(new Color(shade*.88f,shade*.96f,shade),coverage);
                    Vertices[i]=new VertexPositionColorTexture(new Vector3(position,0),Tints[i],UV[i]);
                    if(x<cols&&y<rows)
                    {short a=(short)i,b=(short)(i+1),c=(short)(i+cols+1),d=(short)(i+cols+2);Indices[n++]=a;Indices[n++]=b;Indices[n++]=c;Indices[n++]=b;Indices[n++]=d;Indices[n++]=c;}
                }
            }
        }
        private readonly Dictionary<string,PlanetMesh> planetMeshes=new Dictionary<string,PlanetMesh>();
        private BasicEffect planetEffect;
        private void DrawPlanet(PlanetData planet)
        {
            PlanetMesh mesh=planetMeshes[planet.Id];
            GraphicsDevice device=Game1.instance.GraphicsDevice;
            if(planetEffect==null)planetEffect=new BasicEffect(device){TextureEnabled=true,VertexColorEnabled=true};
            Game1.instance.EndBatch();
            try
            {
                planetEffect.Projection=Matrix.CreateOrthographicOffCenter(0,480,360,0,0,1);
                device.BlendState=BlendState.AlphaBlend;device.DepthStencilState=DepthStencilState.None;device.RasterizerState=RasterizerState.CullNone;
                for(int layer=0;layer<2;layer++)
                {
                    string asset=layer==0?planet.SurfaceAsset:planet.CloudAsset;if(string.IsNullOrEmpty(asset))continue;
                    float turn=PlanetProjection.Turn(time,layer==0?planet.Period:planet.CloudPeriod);
                    for(int i=0;i<mesh.Vertices.Length;i++)
                    {mesh.Vertices[i].TextureCoordinate=mesh.UV[i]+new Vector2(turn,0);mesh.Vertices[i].Color=layer==0?mesh.Tints[i]:MultiplyAlpha(mesh.Tints[i],planet.CloudOpacity);}
                    planetEffect.Texture=vectors[asset].Texture;
                    foreach(EffectPass pass in planetEffect.CurrentTechnique.Passes)
                    {pass.Apply();device.SamplerStates[0]=SamplerState.LinearWrap;device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList,mesh.Vertices,0,mesh.Vertices.Length,mesh.Indices,0,mesh.Indices.Length/3);}
                }
            }
            finally{Game1.instance.StartBatch();}
        }
    }
}
