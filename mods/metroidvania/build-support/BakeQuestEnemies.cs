using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

public sealed class EnemyMaterial {public string texture,name;}
public sealed class EnemyPart {public string mesh;public EnemyMaterial[] materials;}
public sealed class EnemyFrame {public string state;public int frame;public EnemyPart[] parts;}
public sealed class EnemyClip {public string state,clip;public int first,frames;public float duration;}
public sealed class EnemyRender {public int cell,columns,pivot_x,pivot_y;public float center_x,center_y,extent;}
public sealed class EnemyBake {public string id;public int fps;public EnemyRender render;public EnemyClip[] clips;public EnemyFrame[] frames;}
internal static class BakeQuestEnemies
{
    private static void SaveAtlas(string path,Color[] pixels,int width,int height)
    {
        // the game's PNG writer truncates large IDAT streams; use the offline encoder
        using(var image=new System.Drawing.Bitmap(width,height,System.Drawing.Imaging.PixelFormat.Format32bppArgb)){
            var data=image.LockBits(new System.Drawing.Rectangle(0,0,width,height),System.Drawing.Imaging.ImageLockMode.WriteOnly,System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            try{var bytes=new byte[width*height*4];for(int i=0;i<pixels.Length;i++){bytes[i*4]=pixels[i].B;bytes[i*4+1]=pixels[i].G;bytes[i*4+2]=pixels[i].R;bytes[i*4+3]=pixels[i].A;}System.Runtime.InteropServices.Marshal.Copy(bytes,0,data.Scan0,bytes.Length);}
            finally{image.UnlockBits(data);}
            image.Save(path,System.Drawing.Imaging.ImageFormat.Png);
        }
    }
    public static int Main(string[] args)
    {
        try{
            string input=Path.GetFullPath(args[0]),output=Path.GetFullPath(args[1]);Directory.CreateDirectory(output);
            var serializer=new JavaScriptSerializer{MaxJsonLength=16000000};var data=serializer.Deserialize<EnemyBake>(File.ReadAllText(Path.Combine(input,"enemy.json")));
            int cell=data.render.cell,columns=data.render.columns;int rows=(data.frames.Length+columns-1)/columns;
            if(cell<16||cell>128||columns<1||columns>32||data.frames.Length>2048||cell*rows>8192||cell*columns>8192)throw new InvalidDataException("Enemy atlas budget exceeded");
            using(var window=new Form{ShowInTaskbar=false})
            using(var device=new GraphicsDevice(GraphicsAdapter.DefaultAdapter,GraphicsProfile.HiDef,new PresentationParameters{DeviceWindowHandle=window.Handle,BackBufferWidth=cell,BackBufferHeight=cell}))
            using(var effect=new BasicEffect(device))
            using(var target=new RenderTarget2D(device,cell,cell,false,SurfaceFormat.Color,DepthFormat.Depth24))
            using(var atlas=new Texture2D(device,cell*columns,cell*rows)){
                var textures=new Dictionary<string,Texture2D>();var pixels=new Color[cell*cell*columns*rows];var framePixels=new Color[cell*cell];
                try{
                    effect.LightingEnabled=true;effect.AmbientLightColor=new Vector3(.5f);effect.DirectionalLight0.Enabled=true;effect.DirectionalLight0.Direction=Vector3.Normalize(new Vector3(-1,-1,-1));effect.DirectionalLight0.DiffuseColor=new Vector3(.6f);effect.World=Matrix.Identity;
                    var center=new Vector3(data.render.center_x,data.render.center_y,0);effect.View=Matrix.CreateLookAt(center+new Vector3(0,0,-30),center,Vector3.Up);effect.Projection=Matrix.CreateOrthographic(data.render.extent,data.render.extent,.01f,100);
                    for(int index=0;index<data.frames.Length;index++){
                        device.SetRenderTarget(target);device.Clear(Color.Transparent);device.RasterizerState=RasterizerState.CullNone;device.BlendState=BlendState.Opaque;device.DepthStencilState=DepthStencilState.Default;device.SamplerStates[0]=SamplerState.PointClamp;
                        foreach(var part in data.frames[index].parts)using(var reader=new BinaryReader(File.OpenRead(Path.Combine(input,part.mesh)))){
                            var vertices=new VertexPositionNormalTexture[reader.ReadInt32()];for(int i=0;i<vertices.Length;i++)vertices[i]=new VertexPositionNormalTexture(new Vector3(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle()),new Vector3(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle()),new Vector2(reader.ReadSingle(),1-reader.ReadSingle()));
                            int count=reader.ReadInt32();for(int sub=0;sub<count;sub++){
                                var indices=new int[reader.ReadInt32()];for(int i=0;i<indices.Length;i++)indices[i]=reader.ReadInt32();var mat=part.materials[Math.Min(sub,part.materials.Length-1)];
                                if(mat.texture!=""&&!textures.ContainsKey(mat.texture))using(var stream=File.OpenRead(Path.Combine(input,mat.texture)))textures.Add(mat.texture,Texture2D.FromStream(device,stream));
                                effect.TextureEnabled=mat.texture!="";effect.Texture=effect.TextureEnabled?textures[mat.texture]:null;effect.DiffuseColor=mat.name.ToLowerInvariant().Contains("black")?new Vector3(.1f):Vector3.One;
                                foreach(var pass in effect.CurrentTechnique.Passes){pass.Apply();device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList,vertices,0,vertices.Length,indices,0,indices.Length/3);}
                            }
                        }
                        device.SetRenderTarget(null);target.GetData(framePixels);int nonempty=0;
                        for(int y=0;y<cell;y++)for(int x=0;x<cell;x++){var color=framePixels[y*cell+x];if(color.A!=0)nonempty++;pixels[((index/columns)*cell+y)*cell*columns+(index%columns)*cell+x]=color;}
                        if(nonempty<30)throw new InvalidDataException("Empty enemy frame: "+data.frames[index].state+" "+data.frames[index].frame);
                    }
                    SaveAtlas(Path.Combine(output,data.id+".png"),pixels,atlas.Width,atlas.Height);
                    File.WriteAllText(Path.Combine(output,"layout.json"),serializer.Serialize(new{cell=cell,columns=columns,pivotX=data.render.pivot_x,pivotY=data.render.pivot_y,fps=data.fps,clips=data.clips}));
                    Console.WriteLine("[OK] Animated Quest enemy "+data.id+": "+data.frames.Length+" poses, "+atlas.Width+"x"+atlas.Height);
                }finally{foreach(var texture in textures.Values)texture.Dispose();}
            }return 0;
        }catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
}
