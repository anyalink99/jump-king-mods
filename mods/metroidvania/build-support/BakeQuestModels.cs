using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

public sealed class V3 {public float x,y,z;public Vector3 Value {get{return new Vector3(x,y,z);}}}
public sealed class Q4 {public float x,y,z,w;public Quaternion Value{get{return new Quaternion(x,y,z,w);}}}
public sealed class Tr {public V3 position,scale;public Q4 rotation;}
public sealed class Mat {public string texture,name;}
public sealed class Part {public string mesh,name;public Tr[] transforms;public Mat[] materials;}
public sealed class SpriteInfo {public string name;public int view;public float pivotX,pivotY;}
internal static class BakeModels
{
    private sealed class Service:IGraphicsDeviceService
    {
        public GraphicsDevice GraphicsDevice{get;set;}
        public event EventHandler<EventArgs> DeviceCreated{add{}remove{}}
        public event EventHandler<EventArgs> DeviceDisposing{add{}remove{}}
        public event EventHandler<EventArgs> DeviceReset{add{}remove{}}
        public event EventHandler<EventArgs> DeviceResetting{add{}remove{}}
    }
    private sealed class MeshPart {internal VertexPositionNormalTexture[] Vertices;internal int[][] Indices;internal Mat[] Materials;}
    public static int Main(string[] args)
    {
        string input=Path.GetFullPath(args[0]),output=Path.GetFullPath(args[1]);Directory.CreateDirectory(output);
        using(var window=new Form{ShowInTaskbar=false})
        using(var device=new GraphicsDevice(GraphicsAdapter.DefaultAdapter,GraphicsProfile.HiDef,new PresentationParameters{DeviceWindowHandle=window.Handle,BackBufferWidth=256,BackBufferHeight=256}))
        using(var effect=new BasicEffect(device)){
            var services=new GameServiceContainer();services.AddService(typeof(IGraphicsDeviceService),new Service{GraphicsDevice=device});
            using(var content=new ContentManager(services,args[2]))foreach(string name in new[]{"base","tunic"}){
                var texture=content.Load<Texture2D>("Content/king/"+name);using(var stream=File.Create(Path.Combine(output,"king-"+name+".png")))texture.SaveAsPng(stream,texture.Width,texture.Height);
            }
            var info=new List<SpriteInfo>();
            effect.LightingEnabled=true;effect.AmbientLightColor=new Vector3(.5f);effect.DirectionalLight0.Enabled=true;effect.DirectionalLight0.Direction=Vector3.Normalize(new Vector3(-1,-1,-1));effect.DirectionalLight0.DiffuseColor=new Vector3(.65f);effect.World=Matrix.Identity;effect.Alpha=1;
            foreach(string selected in new JavaScriptSerializer().Deserialize<string[]>(File.ReadAllText(Path.Combine(input,"index.json")))){string file=Path.Combine(input,selected);
                var parts=new JavaScriptSerializer().Deserialize<Part[]>(File.ReadAllText(file));var meshes=new List<MeshPart>();Vector3 min=new Vector3(float.MaxValue),max=new Vector3(float.MinValue);
                foreach(var p in parts){
                    var m=new MeshPart{Materials=p.materials};Matrix transform=Matrix.Identity;foreach(var tr in p.transforms)transform=Matrix.CreateScale(tr.scale.Value)*Matrix.CreateFromQuaternion(tr.rotation.Value)*Matrix.CreateTranslation(tr.position.Value)*transform;
                    using(var r=new BinaryReader(File.OpenRead(Path.Combine(input,p.mesh)))){
                        int n=r.ReadInt32();m.Vertices=new VertexPositionNormalTexture[n];for(int i=0;i<n;i++){
                            var position=Vector3.Transform(new Vector3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle()),transform);var normal=Vector3.Normalize(Vector3.TransformNormal(new Vector3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle()),transform));var uv=new Vector2(r.ReadSingle(),1-r.ReadSingle());
                            m.Vertices[i]=new VertexPositionNormalTexture(position,normal,uv);min=Vector3.Min(min,position);max=Vector3.Max(max,position);
                        }
                        m.Indices=new int[r.ReadInt32()][];for(int i=0;i<m.Indices.Length;i++){m.Indices[i]=new int[r.ReadInt32()];for(int j=0;j<m.Indices[i].Length;j++)m.Indices[i][j]=r.ReadInt32();}
                    }meshes.Add(m);
                }
                if(meshes.Count==0)continue;
                var textures=new Dictionary<string,Texture2D>();
                try{
                    foreach(var m in meshes)foreach(var mat in m.Materials)if(mat.texture!=""&&!textures.ContainsKey(mat.texture))using(var stream=File.OpenRead(Path.Combine(input,mat.texture)))textures.Add(mat.texture,Texture2D.FromStream(device,stream));
                    for(int view=0;view<4;view++)using(var target=new RenderTarget2D(device,256,256,false,SurfaceFormat.Color,DepthFormat.Depth24)){
                        Vector3 center=(min+max)*.5f,size=max-min,direction=view==0?Vector3.Forward:view==1?Vector3.Right:view==2?Vector3.Normalize(new Vector3(1,0,-1)):Vector3.Backward;
                        float extent=Math.Max(size.Y,view==1?size.Z:size.X)*1.15f;
                        effect.View=Matrix.CreateLookAt(center+direction*Math.Max(10,size.Length()*3),center,Vector3.Up);effect.Projection=Matrix.CreateOrthographic(extent,extent,.01f,1000);
                        var pivot=new Viewport(0,0,256,256).Project(Vector3.Zero,effect.Projection,effect.View,Matrix.Identity);info.Add(new SpriteInfo{name=Path.GetFileNameWithoutExtension(file),view=view,pivotX=pivot.X,pivotY=pivot.Y});
                        device.SetRenderTarget(target);device.Clear(Color.Transparent);device.RasterizerState=RasterizerState.CullNone;device.BlendState=BlendState.Opaque;device.DepthStencilState=DepthStencilState.Default;device.SamplerStates[0]=SamplerState.LinearClamp;
                        foreach(var m in meshes)for(int sub=0;sub<m.Indices.Length;sub++){
                            Mat material=m.Materials[Math.Min(sub,m.Materials.Length-1)];if(material.name.Contains("IconPlaceholder"))continue;
                            effect.TextureEnabled=material.texture!="";effect.Texture=effect.TextureEnabled?textures[material.texture]:null;effect.DiffuseColor=material.name.Contains("black")?new Vector3(.1f):Vector3.One;
                            foreach(var pass in effect.CurrentTechnique.Passes){pass.Apply();device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList,m.Vertices,0,m.Vertices.Length,m.Indices[sub],0,m.Indices[sub].Length/3);}
                        }
                        device.SetRenderTarget(null);using(var stream=File.Create(Path.Combine(output,Path.GetFileNameWithoutExtension(file)+"-"+view+".png")))target.SaveAsPng(stream,256,256);
                    }
                }finally{foreach(var texture in textures.Values)texture.Dispose();}
                Console.WriteLine(Path.GetFileName(file)+" bounds "+min+" "+max);
            }
            File.WriteAllText(Path.Combine(output,"layout.json"),new JavaScriptSerializer().Serialize(info));
        }return 0;
    }
}
