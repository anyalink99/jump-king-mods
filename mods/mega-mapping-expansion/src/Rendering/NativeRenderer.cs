using System;
using System.Collections.Generic;
using System.Linq;
using JumpKing;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MegaMappingExpansion.Api;

namespace MegaMappingExpansion.Construction
{
    internal sealed partial class NativeRenderer : IDisposable
    {
        private sealed class MeshBuffer
        {
            internal Vector2[] Points;
            internal SceneValue[] LocalPoints;
            internal VertexPositionColorTexture[] Vertices;
            internal DualVertex[] MaskedVertices;
            internal short[] Indices;
            internal PropertySlot[] Dependencies;
            internal SceneValue[] Values;
            internal ComponentPose Pose;
            internal bool Cached;
        }
        private readonly SceneProgram scene;
        private readonly GraphicsDevice device;
        private readonly NativeAssets assets;
        private readonly TransformSystem transforms;
        private readonly FieldSystem fields;
        private readonly JKRuntime.RuntimeScope resources=new JKRuntime.RuntimeScope();
        private readonly BasicEffect effect;
        private readonly DualTextureEffect dualEffect;
        private readonly BlendState multiplyBlend,emissionBlend;
        private readonly Dictionary<string,MeshBuffer> meshes=new Dictionary<string,MeshBuffer>(StringComparer.Ordinal);
        private readonly Dictionary<string,CompiledComponent[]> layers=new Dictionary<string,CompiledComponent[]>(StringComparer.Ordinal);
        private readonly Dictionary<string,Action<int>> drawing=new Dictionary<string,Action<int>>(StringComparer.Ordinal);
        private bool disposed;
        private bool membershipDirty=true;
        internal readonly string AssetIdentity;
        internal NativeRenderer(SceneProgram program,GraphicsDevice graphics,string root,NativeAssetPool pool=null)
        {
            scene=program;device=graphics;transforms=new TransformSystem(program);fields=program.Fields;
            try
            {
                if(pool==null)assets=resources.Own(new NativeAssets(device,root,program.Definition.Assets));
                else {var lease=resources.Own(pool.Acquire(device,root,program.Definition.Assets));assets=lease.Assets;}
                AssetIdentity=assets.ContentIdentity;
                resources.Own(DualVertex.Acquire());
                effect=resources.Own(new BasicEffect(device){TextureEnabled=true,VertexColorEnabled=true,Projection=Matrix.CreateOrthographicOffCenter(0,480,360,0,0,1)});
                dualEffect=resources.Own(new DualTextureEffect(device){VertexColorEnabled=true,Projection=Matrix.CreateOrthographicOffCenter(0,480,360,0,0,1)});
                multiplyBlend=resources.Own(new BlendState{ColorSourceBlend=Microsoft.Xna.Framework.Graphics.Blend.DestinationColor,ColorDestinationBlend=Microsoft.Xna.Framework.Graphics.Blend.InverseSourceAlpha,AlphaSourceBlend=Microsoft.Xna.Framework.Graphics.Blend.Zero,AlphaDestinationBlend=Microsoft.Xna.Framework.Graphics.Blend.One});
                emissionBlend=resources.Own(new BlendState{ColorSourceBlend=Microsoft.Xna.Framework.Graphics.Blend.One,ColorDestinationBlend=Microsoft.Xna.Framework.Graphics.Blend.One,AlphaSourceBlend=Microsoft.Xna.Framework.Graphics.Blend.Zero,AlphaDestinationBlend=Microsoft.Xna.Framework.Graphics.Blend.One});
                long vertices=0;
                foreach(var component in program.Components.Values)
                {
                    if(component.Descriptor.Kind!="mesh")continue;
                    if(Text(component.Id,"geometry")=="polygon")
                    {
                        var points=GeometryMath.Points(Text(component.Id,"outline"));double left=points.Min(p=>p.X),top=points.Min(p=>p.Y),width=points.Max(p=>p.X)-left,height=points.Max(p=>p.Y)-top;
                        if(width<=0||height<=0)throw new System.IO.InvalidDataException(component.Id+": polygon has no area");
                        meshes.Add(component.Id,new MeshBuffer{LocalPoints=points,Points=Array.ConvertAll(points,p=>new Vector2((float)((p.X-left)/width),(float)((p.Y-top)/height))),Vertices=new VertexPositionColorTexture[points.Length],MaskedVertices=new DualVertex[points.Length],Indices=GeometryMath.Triangulate(points)});continue;
                    }
                    int columns=(int)Number(component.Id,"columns"),rows=(int)Number(component.Id,"rows");
                    if(columns*rows>16000)throw new System.IO.InvalidDataException(component.Id+": mesh exceeds 16000 cells");
                    vertices+=(columns+1)*(rows+1);if(vertices>2000000)throw new System.IO.InvalidDataException("Prepared scene meshes exceed two million vertices");
                    var mesh=new MeshBuffer{Points=new Vector2[(columns+1)*(rows+1)],Vertices=new VertexPositionColorTexture[(columns+1)*(rows+1)],MaskedVertices=new DualVertex[(columns+1)*(rows+1)],Indices=new short[columns*rows*6]};int n=0;
                    for(int y=0;y<=rows;y++)for(int x=0;x<=columns;x++)
                    {
                        int i=y*(columns+1)+x;mesh.Points[i]=new Vector2(x/(float)columns,y/(float)rows);
                        if(x<columns&&y<rows){short a=(short)i,b=(short)(i+1),c=(short)(i+columns+1),d=(short)(i+columns+2);mesh.Indices[n++]=a;mesh.Indices[n++]=b;mesh.Indices[n++]=c;mesh.Indices[n++]=b;mesh.Indices[n++]=d;mesh.Indices[n++]=c;}
                    }
                    meshes.Add(component.Id,mesh);
                }
                transforms.Refresh();
                foreach(var pair in meshes)
                {
                    var dependencies=new List<PropertySlot>();var component=scene.Components[pair.Key];
                    foreach(var field in component.Descriptor.Fields)dependencies.Add(scene.Properties.Require(component.Id+"/"+field.Name));
                    string material=Text(component.Id,"material");if(material!="")foreach(var field in scene.Components[material].Descriptor.Fields)dependencies.Add(scene.Properties.Require(material+"/"+field.Name));
                    pair.Value.Dependencies=dependencies.ToArray();pair.Value.Values=new SceneValue[dependencies.Count];
                }
                PrepareDynamic();
                PrepareCaptures();
                PrepareSurfaces();
                PrepareViews();
                foreach(var component in scene.Components.Values)if(component.Descriptor.DrawFactory!=null)drawing.Add(component.Id,component.Descriptor.DrawFactory(this,component));
                BuildLayerIndex();
                scene.Properties.Changed+=PropertyChanged;resources.Defer(()=>scene.Properties.Changed-=PropertyChanged);
            }
            catch{resources.Dispose();throw;}
        }
        private SceneValue Value(string id,string name){return scene.Properties.Read(id,name);}
        private double Number(string id,string name){return Value(id,name).X;}
        private string Text(string id,string name){return Value(id,name).Text;}
        private static Vector2 Vector(SceneValue value){return new Vector2((float)value.X,(float)value.Y);}
        private ComponentPose Pose(CompiledComponent component){return transforms.Resolve(Text(component.Id,"transform"));}
        private Color Tint(string material,double coverage=1)
        {
            if(material=="")return new Color((float)coverage,(float)coverage,(float)coverage,(float)coverage);
            var color=Value(material,"color");float alpha=(float)(color.W*Number(material,"opacity")*coverage);
            return new Color((float)color.X*alpha,(float)color.Y*alpha,(float)color.Z*alpha,alpha);
        }
        private Texture2D Texture(string asset){return asset==""?Game1.instance.contentManager.Pixel.texture:assets.Bitmaps[asset].Texture;}
        private SamplerState Sampler(string material,bool strictPixels)
        {bool wrap=material!=""&&Text(material,"address")=="wrap";return !strictPixels&&material!=""&&Text(material,"sampling")=="linear"?(wrap?SamplerState.LinearWrap:SamplerState.LinearClamp):(wrap?SamplerState.PointWrap:SamplerState.PointClamp);}
        private BlendState Blend(string material)
        {string blend=material==""?"alpha":Text(material,"blend");return blend=="add"?emissionBlend:blend=="multiply"?multiplyBlend:BlendState.AlphaBlend;}
        private void PropertyChanged(string id,string property,ResourceImpact impact){if(impact==ResourceImpact.Membership)membershipDirty=true;}
        private void BuildLayerIndex()
        {
            layers.Clear();
            foreach(var group in scene.Components.Values.Where(c=>drawing.ContainsKey(c.Id)).GroupBy(c=>Text(c.Id,"layer")+"/"+(c.Descriptor.Kind=="stamp"?0:Pose(c).Screen)))
                layers.Add(group.Key,group.OrderBy(c=>Number(c.Id,"z")).ThenBy(c=>c.Id,StringComparer.Ordinal).ToArray());
            membershipDirty=false;
        }
        private IEnumerable<CompiledComponent> Commands(string layer,int screen)
        {
            CompiledComponent[] local,global;if(!layers.TryGetValue(layer+"/"+screen,out local))local=new CompiledComponent[0];if(!layers.TryGetValue(layer+"/0",out global))global=new CompiledComponent[0];int a=0,b=0;
            while(a<local.Length||b<global.Length)
            {
                if(a>=local.Length){yield return global[b++];continue;}if(b>=global.Length){yield return local[a++];continue;}
                double l=Number(local[a].Id,"z"),g=Number(global[b].Id,"z");if(l<g||(l==g&&string.CompareOrdinal(local[a].Id,global[b].Id)<0))yield return local[a++];else yield return global[b++];
            }
        }
        internal int MeshVertexUpdates{get;private set;}
        internal void Tick(){if(disposed)return;renderFrame++;QuadDrawCalls=0;MeshVertexUpdates=0;transforms.Refresh();if(membershipDirty)BuildLayerIndex();TickDynamic();}
        internal void DrawLayer(string layer,int screen)
        {
            if(disposed)return;
            Game1.instance.EndBatch();
            try
            {
                foreach(var component in Commands(layer,screen))
                {
                    if(Number(component.Id,"captureOnly")==0)DrawComponent(component,screen);
                }
            }
            finally{FlushQuads();Game1.instance.StartBatch();}
        }
        private void DrawSprite(CompiledComponent component,ComponentPose pose,string material)
        {
            if(material!=""&&Text(material,"capture")!="")
            {
                var texture=MaterialTexture(material,"");if(texture==null)return;
                var origin=Value(component.Id,"origin");DrawQuad(texture,new Rectangle(1,1,480,360),pose,SceneValue.Vector(-origin.X,-origin.Y),SceneValue.Vector(480,360),Tint(material),material,null);return;
            }
            string asset=Text(component.Id,"asset");if(asset=="")return;var bitmap=assets.Bitmaps[asset];
            DrawAnimatedSprite(component,pose,material,bitmap);
        }
        private void DrawMesh(CompiledComponent component,ComponentPose pose,string material)
        {
            FlushQuads();
            var mesh=meshes[component.Id];var size=Value(component.Id,"size");var tilt=Value(component.Id,"projectionTilt");string projection=Text(component.Id,"projection");
            bool disc=Text(component.Id,"geometry")=="disc";var offset=material==""?SceneValue.Vector(0,0):Value(material,"uvOffset");var uvScale=material==""?SceneValue.Vector(1,1):Value(material,"uvScale");
            string maskId=material==""?"":Text(material,"mask");Texture2D mask=maskId==""?null:masks[maskId];bool screenMask=mask!=null&&Text(material,"maskSpace")=="screen";
            string deformation=Text(component.Id,"deformation");
            double ambient=material==""?1:Number(material,"ambient"),diffuse=material==""?0:Number(material,"diffuse");var normalLight=material==""?SceneValue.Vector(0,0):Value(material,"normalLight");double lightZ=Math.Sqrt(Math.Max(0,1-normalLight.X*normalLight.X-normalLight.Y*normalLight.Y));
            bool rebuild=deformation!=""||!mesh.Cached||!SamePose(mesh.Pose,pose);
            for(int i=0;i<mesh.Dependencies.Length;i++)if(!mesh.Dependencies[i].Value.Equals(mesh.Values[i])){mesh.Values[i]=mesh.Dependencies[i].Value;rebuild=true;}
            if(rebuild)for(int i=0;i<mesh.Points.Length;i++)
            {
                var uv=mesh.Points[i];
                if(disc){double x=uv.X-.5,y=uv.Y-.5,length=Math.Sqrt(x*x+y*y);if(length>.5)uv=new Vector2((float)(.5+x*.5/length),(float)(.5+y*.5/length));}
                var projected=ProjectionMath.Project(projection,uv.X,uv.Y,tilt,false);
                var local=mesh.LocalPoints==null?SceneValue.Vector((uv.X-.5)*size.X,(uv.Y-.5)*size.Y):mesh.LocalPoints[i];
                if(deformation!="")local=fields.Deform(deformation,local,pose);
                var position=pose.RenderPoint(local);
                var tint=Tint(material,projected.Coverage);float shade=(float)(ambient+diffuse*Math.Max(0,projected.NormalX*normalLight.X+projected.NormalY*normalLight.Y+projected.Facing*lightZ));tint=new Color((byte)Math.Min(255,tint.R*shade),(byte)Math.Min(255,tint.G*shade),(byte)Math.Min(255,tint.B*shade),tint.A);
                mesh.Vertices[i]=new VertexPositionColorTexture(new Vector3((float)position.X,(float)position.Y,0),tint,
                    MaterialUV(material,new Vector2((float)(projected.U*uvScale.X+offset.X),(float)(projected.V*uvScale.Y+offset.Y))));
                if(mask!=null)mesh.MaskedVertices[i]=new DualVertex{Position=mesh.Vertices[i].Position,Color=mesh.Vertices[i].Color,UV=mesh.Vertices[i].TextureCoordinate,MaskUV=screenMask?new Vector2((float)position.X/mask.Width,(float)position.Y/mask.Height):uv};
            }
            if(rebuild){MeshVertexUpdates+=mesh.Points.Length;mesh.Pose=pose;mesh.Cached=true;}
            var texture=MaterialTexture(material,Text(component.Id,"asset"));if(texture==null)return;device.BlendState=Blend(material);device.DepthStencilState=DepthStencilState.None;device.RasterizerState=RasterizerState.CullNone;
            effect.DiffuseColor=Vector3.One;dualEffect.DiffuseColor=new Vector3(.5f);
            int passes=material!=""&&Number(material,"emission")>0?2:1;
            for(int p=0;p<passes;p++)
            {
                if(p==1){device.BlendState=emissionBlend;effect.DiffuseColor=new Vector3((float)Number(material,"emission"));dualEffect.DiffuseColor=effect.DiffuseColor*.5f;}
                if(mask==null){effect.Texture=texture;foreach(var pass in effect.CurrentTechnique.Passes){pass.Apply();device.SamplerStates[0]=Sampler(material,pose.PixelSnap);device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList,mesh.Vertices,0,mesh.Vertices.Length,mesh.Indices,0,mesh.Indices.Length/3);}}
                else{dualEffect.Texture=texture;dualEffect.Texture2=mask;foreach(var pass in dualEffect.CurrentTechnique.Passes){pass.Apply();device.SamplerStates[0]=Sampler(material,pose.PixelSnap);device.SamplerStates[1]=SamplerState.PointClamp;device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList,mesh.MaskedVertices,0,mesh.MaskedVertices.Length,mesh.Indices,0,mesh.Indices.Length/3);}}
            }
        }
        private static bool SamePose(ComponentPose a,ComponentPose b)
        {return a.X==b.X&&a.Y==b.Y&&a.ScaleX==b.ScaleX&&a.ScaleY==b.ScaleY&&a.Rotation==b.Rotation&&a.Screen==b.Screen&&a.PixelSnap==b.PixelSnap;}
        public void Dispose(){if(disposed)return;disposed=true;resources.Dispose();meshes.Clear();layers.Clear();drawing.Clear();}
    }
}
