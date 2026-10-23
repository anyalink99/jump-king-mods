using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Linq;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Gdi=System.Drawing;

namespace OverlayPlus
{
    internal sealed class GameUnicodeAtlas:IDisposable
    {
        private readonly Dictionary<char,Rectangle> glyphs=new Dictionary<char,Rectangle>();
        private readonly Dictionary<char,float> advances=new Dictionary<char,float>();
        internal readonly Texture2D Texture;
        internal const float Size=12, LineHeight=16;
        internal GameUnicodeAtlas(GraphicsDevice device,string gameDirectory)
        {
            var chars=new List<char>();for(int c=32;c<384;c++)chars.Add((char)c);for(int c=1024;c<1280;c++)chars.Add((char)c);chars.AddRange("←→↑↓•…−✓✕★Δ●○");
            using(var collection=new Gdi.Text.PrivateFontCollection())
            {
            collection.AddFontFile(Path.Combine(gameDirectory,"Content/font/ttf_lanapixel.ttf"));
            using(var bitmap=new Gdi.Bitmap(1024,1024,PixelFormat.Format32bppArgb))
            using(var g=Gdi.Graphics.FromImage(bitmap))
            using(var font=new Gdi.Font(collection.Families[0],Size,Gdi.FontStyle.Regular,Gdi.GraphicsUnit.Pixel))
            using(var format=(Gdi.StringFormat)Gdi.StringFormat.GenericTypographic.Clone())
            {
                format.FormatFlags|=Gdi.StringFormatFlags.MeasureTrailingSpaces;g.Clear(Gdi.Color.Transparent);g.TextRenderingHint=Gdi.Text.TextRenderingHint.SingleBitPerPixelGridFit;
                int i=0;foreach(char c in chars.Distinct()){int x=(i%25)*40,y=(i/25)*40;i++;string text=c.ToString();float advance=g.MeasureString(text,font,100,format).Width;glyphs[c]=new Rectangle(x+1,y+1,38,38);advances[c]=Math.Max(1,advance);g.DrawString(text,font,Gdi.Brushes.White,x+2,y+2,format);}
                var data=bitmap.LockBits(new Gdi.Rectangle(0,0,1024,1024),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);try{byte[] raw=new byte[1024*1024*4];Marshal.Copy(data.Scan0,raw,0,raw.Length);var colors=new Color[1024*1024];for(int n=0;n<colors.Length;n++){byte a=raw[n*4+3];colors[n]=new Color(a,a,a,a);}Texture=new Texture2D(device,1024,1024);Texture.SetData(colors);}finally{bitmap.UnlockBits(data);}
            }
            }
        }
        internal float Measure(string text,float size){float line=0,max=0;foreach(char c in text??""){if(c=='\n'){max=Math.Max(max,line);line=0;}else line+=Advance(c)*size/Size;}return Math.Max(max,line);}
        private float Advance(char c){float v;return advances.TryGetValue(c,out v)?v:advances['?'];}
        internal void Draw(SpriteBatch batch,string text,Vector2 position,float size,Color color)
        {float x=position.X,y=position.Y,scale=size/Size;foreach(char c in text??""){if(c=='\r')continue;if(c=='\n'){x=position.X;y+=LineHeight*scale;continue;}Rectangle source;if(!glyphs.TryGetValue(c,out source))source=glyphs['?'];batch.Draw(Texture,new Vector2((float)Math.Round(x-scale),(float)Math.Round(y-scale)),source,color,0,Vector2.Zero,scale,SpriteEffects.None,0);x+=Advance(c)*scale;}}
        public void Dispose(){Texture.Dispose();}
    }
    internal sealed class GameFont
    {
        private readonly SpriteFont native;
        private readonly HashSet<char> characters;
        private readonly GameUnicodeAtlas unicode;
        internal GameFont(SpriteFont font,GameUnicodeAtlas fallback){native=font;characters=new HashSet<char>(font.Characters);unicode=fallback;}
        private bool NativeText(string text){return (text??"").All(c=>c=='\r'||c=='\n'||characters.Contains(c));}
        internal float Measure(string text,float size){return NativeText(text)?native.MeasureString(text??"").X*size/Math.Max(1,native.LineSpacing):unicode.Measure(text,size);}
        internal void Draw(SpriteBatch batch,string text,Vector2 at,float size,Color color){if(!NativeText(text)){unicode.Draw(batch,text,at,size,color);return;}batch.DrawString(native,text??"",new Vector2((float)Math.Round(at.X),(float)Math.Round(at.Y)),color,0,Vector2.Zero,size/Math.Max(1,native.LineSpacing),SpriteEffects.None,0);}
    }
    internal sealed class Canvas:IDisposable
    {
        internal readonly GraphicsDevice Device;
        internal readonly SpriteBatch Batch;
        private readonly Texture2D pixel;
        private readonly Dictionary<string,GameFont> fonts=new Dictionary<string,GameFont>();
        private readonly GameUnicodeAtlas unicode;
        private readonly Dictionary<string,Texture2D> images=new Dictionary<string,Texture2D>(StringComparer.OrdinalIgnoreCase);
        private readonly RasterizerState raster=new RasterizerState {CullMode=CullMode.None,ScissorTestEnable=true};
        private Rectangle clip;
        private Rectangle passClip;
        private Matrix transform;
        private float pixelScale;
        private Surface surface;
        private Viewport oldViewport;
        private Rectangle oldScissor;
        private BlendState oldBlend;
        private SamplerState oldSampler;
        private RasterizerState oldRaster;
        private DepthStencilState oldDepth;
        internal Canvas(GraphicsDevice device,string gameDirectory=null)
        {Device=device;Batch=new SpriteBatch(device);pixel=new Texture2D(device,1,1);pixel.SetData(new[]{Color.White});try{unicode=new GameUnicodeAtlas(device,gameDirectory??Path.GetDirectoryName(typeof(JumpKing.Game1).Assembly.Location));var f=JumpKing.Game1.instance.contentManager.font;fonts.Add("Menu",new GameFont(f.MenuFont,unicode));fonts.Add("Small",new GameFont(f.MenuFontSmall,unicode));fonts.Add("Location",new GameFont(f.LocationFont,unicode));fonts.Add("Story",new GameFont(f.StyleFont??f.MenuFont,unicode));fonts.Add("Gargoyle",new GameFont(f.GargoyleFont??f.MenuFont,unicode));}catch{Dispose();throw;}}
        internal void PrepareImages(IEnumerable<Widget> widgets)
        {
            foreach(string name in widgets.Where(w=>w.Kind==WidgetKind.Image).Select(w=>w.ImagePath).Distinct()){
                if(string.IsNullOrWhiteSpace(name)||images.ContainsKey(name))continue;
                try{if(Path.IsPathRooted(name)||name.Contains(".."))throw new InvalidDataException("Image paths must be relative to Overlay+/Images");string p=Path.Combine(Store.Root,"Images",name);using(var f=File.OpenRead(p)){if(f.Length>16*1024*1024)throw new InvalidDataException("Image exceeds 16 MB");var t=Texture2D.FromStream(Device,f);if(t.Width>4096||t.Height>4096){t.Dispose();throw new InvalidDataException("Image exceeds 4096 pixels");}var data=new Color[t.Width*t.Height];t.GetData(data);for(int i=0;i<data.Length;i++)data[i]=new Color(data[i].R*data[i].A/255,data[i].G*data[i].A/255,data[i].B*data[i].A/255,data[i].A);t.SetData(data);images.Add(name,t);}}
                catch(Exception e){Store.Warning="Image "+name+": "+e.Message;}
            }
        }
        internal void Begin(Surface value,Rectangle? region=null)
        {
            surface=value;oldViewport=Device.Viewport;oldScissor=Device.ScissorRectangle;oldBlend=Device.BlendState;oldSampler=Device.SamplerStates[0];oldRaster=Device.RasterizerState;oldDepth=Device.DepthStencilState;
            pixelScale=surface.Scale;transform=Matrix.CreateScale(pixelScale);
            Device.Viewport=new Viewport(0,0,(int)Math.Ceiling(surface.Window.Width*surface.Scale),(int)Math.Ceiling(surface.Window.Height*surface.Scale));
            passClip=region??surface.Window;clip=passClip;Start();
        }
        private void Start(){Device.ScissorRectangle=Rectangle.Intersect(Device.Viewport.Bounds,new Rectangle((int)(clip.X*pixelScale),(int)(clip.Y*pixelScale),Math.Max(0,(int)(clip.Width*pixelScale)),Math.Max(0,(int)(clip.Height*pixelScale))));Batch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.PointClamp,DepthStencilState.None,raster,null,transform);}
        internal void Clip(Rectangle bounds){Batch.End();clip=Rectangle.Intersect(passClip,bounds);Start();}
        internal void Unclip(){Clip(passClip);}
        internal void End(){try{Batch.End();}finally{Device.Viewport=oldViewport;Device.ScissorRectangle=oldScissor;Device.BlendState=oldBlend;Device.SamplerStates[0]=oldSampler;Device.RasterizerState=oldRaster;Device.DepthStencilState=oldDepth;}}
        internal void Fill(Rectangle rect,Color color){if(rect.Width>0&&rect.Height>0)Batch.Draw(pixel,rect,color);}
        internal void Border(Rectangle r,Color c,int thickness=1){Fill(new Rectangle(r.X,r.Y,r.Width,thickness),c);Fill(new Rectangle(r.X,r.Bottom-thickness,r.Width,thickness),c);Fill(new Rectangle(r.X,r.Y,thickness,r.Height),c);Fill(new Rectangle(r.Right-thickness,r.Y,thickness,r.Height),c);}
        internal float Measure(string s,float size,string font="Menu"){return Font(font).Measure(s,size);}
        private GameFont Font(string name){GameFont f;return fonts.TryGetValue(name??"",out f)?f:fonts["Menu"];}
        internal void Text(string text,float x,float y,float size,Color color,string font="Menu",bool shadow=false,bool outline=false)
        {var f=Font(font);if(shadow)f.Draw(Batch,text,new Vector2(x+1,y+2),size,Color.Black*(color.A/255f));if(outline)foreach(var d in new[]{new Vector2(-1,0),new Vector2(1,0),new Vector2(0,-1),new Vector2(0,1)})f.Draw(Batch,text,new Vector2(x,y)+d,size,Color.Black*(color.A/255f));f.Draw(Batch,text,new Vector2(x,y),size,color);}
        internal string Fit(string text,float width,float size,string font="Menu")
        {text=text??"";if(Measure(text,size,font)<=width)return text;while(text.Length>0&&Measure(text+"...",size,font)>width)text=text.Substring(0,text.Length-1);return text.Length==0?"":text+"...";}
        internal void Wrapped(string text,Rectangle rect,float size,Color color,string font,string align,bool shadow,bool outline)
        {
            float y=rect.Y;foreach(string paragraph in (text??"").Replace("\r","").Split('\n')){string line="";foreach(string word in paragraph.Split(' ')){string next=line.Length==0?word:line+" "+word;if(Measure(next,size,font)>rect.Width&&line.Length>0){Line(line,rect,y,size,color,font,align,shadow,outline);y+=size*1.3f;line="";}foreach(char ch in (line.Length==0?word:" "+word)){if(Measure(line+ch,size,font)>rect.Width&&line.Length>0){Line(line,rect,y,size,color,font,align,shadow,outline);y+=size*1.3f;line="";}line+=ch;}if(y>rect.Bottom)return;}Line(line,rect,y,size,color,font,align,shadow,outline);y+=size*1.3f;if(y>rect.Bottom)return;}
        }
        private void Line(string text,Rectangle r,float y,float size,Color color,string font,string align,bool shadow,bool outline){if(y+size>r.Bottom)return;float x=r.X;if(align=="Center")x+=(r.Width-Measure(text,size,font))/2;if(align=="Right")x+=r.Width-Measure(text,size,font);Text(text,x,y,size,color,font,shadow,outline);}
        internal bool Image(string path,Rectangle rect,float opacity){Texture2D image;if(!images.TryGetValue(path??"",out image))return false;float scale=Math.Min(rect.Width/(float)image.Width,rect.Height/(float)image.Height);var dst=new Rectangle(rect.X+(rect.Width-(int)(image.Width*scale))/2,rect.Y+(rect.Height-(int)(image.Height*scale))/2,(int)(image.Width*scale),(int)(image.Height*scale));Batch.Draw(image,dst,Color.White*opacity);return true;}
        internal static Color ColorOf(uint argb,float opacity=1){byte a=(byte)(argb>>24);float alpha=a/255f*opacity;return new Color((byte)(((argb>>16)&255)*alpha),(byte)(((argb>>8)&255)*alpha),(byte)((argb&255)*alpha),(byte)(255*alpha));}
        internal void Frame(Rectangle bounds,float opacity=1,bool fill=true)
        {
            var sprites=JumpKing.Game1.instance.contentManager.gui.FrameSprites;int cell=Math.Max(2,Math.Min(sprites[0,0].source.Width*2,Math.Min(bounds.Width,bounds.Height)/2));if(bounds.Width<cell*2||bounds.Height<cell*2)return;
            for(int x=0;x<3;x++)for(int y=0;y<3;y++){if(x==1&&y==1&&!fill)continue;var sprite=sprites[x,y];int dx=x==0?bounds.X:x==1?bounds.X+cell:bounds.Right-cell,dy=y==0?bounds.Y:y==1?bounds.Y+cell:bounds.Bottom-cell;var r=new Rectangle(dx,dy,x==1?bounds.Width-2*cell:cell,y==1?bounds.Height-2*cell:cell);Batch.Draw(sprite.texture,r,sprite.source,Color.White*opacity);}
        }
        public void Dispose(){if(unicode!=null)unicode.Dispose();foreach(var t in images.Values)t.Dispose();fonts.Clear();images.Clear();pixel.Dispose();Batch.Dispose();raster.Dispose();}
    }
}
