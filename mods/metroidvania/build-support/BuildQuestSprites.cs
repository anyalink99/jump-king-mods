using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

internal sealed class BakeLayout {public string name;public int view;public float pivotX,pivotY;}
internal sealed class GearArtRecipe {public string kind;public int index,view,height;public float gain;}
internal sealed class GearRecipe {public string id,source,source_sprite;public GearArtRecipe art;}
internal static class BuildQuestSprites
{
    private static readonly int[] PoseX={0,1,2,3,0,1,2,0,3,1,2,3,0},PoseY={0,0,0,0,1,1,1,2,1,2,2,2,3};
    private static Color Pixel(Color color,float gain=1.45f)
    {
        if(color.A<96)return Color.FromArgb(0,0,0,0);
        return Color.FromArgb(255,Channel(color.R,gain),Channel(color.G,gain),Channel(color.B,gain));
    }
    private static int Channel(int c,float gain){return Math.Max(0,Math.Min(255,(int)Math.Round(Math.Pow(c/255.0,.82)*255*gain/17)*17));}
    private static Rectangle Bounds(Bitmap image)
    {
        int left=image.Width,top=image.Height,right=-1,bottom=-1;
        for(int y=0;y<image.Height;y++)for(int x=0;x<image.Width;x++)if(image.GetPixel(x,y).A>96){left=Math.Min(left,x);top=Math.Min(top,y);right=Math.Max(right,x);bottom=Math.Max(bottom,y);}
        if(right<left)throw new InvalidDataException("Empty source image");return Rectangle.FromLTRB(left,top,right+1,bottom+1);
    }
    private static Color Sample(Bitmap source,float x,float y,float width,float height)
    {
        float r=0,g=0,b=0,a=0,count=0;
        for(int py=(int)y;py<Math.Ceiling(y+height);py++)for(int px=(int)x;px<Math.Ceiling(x+width);px++){
            if(px<0||py<0||px>=source.Width||py>=source.Height)continue;float weight=Math.Max(0,Math.Min(px+1,x+width)-Math.Max(px,x))*Math.Max(0,Math.Min(py+1,y+height)-Math.Max(py,y));var c=source.GetPixel(px,py);float alpha=c.A/255f*weight;
            r+=c.R*alpha;g+=c.G*alpha;b+=c.B*alpha;a+=alpha;count+=weight;
        }
        if(a==0||a<.25f*count)return Color.FromArgb(0,0,0,0);return Color.FromArgb(255,(int)(r/a),(int)(g/a),(int)(b/a));
    }
    public static int Main(string[] args)
    {
        string sources=Path.GetFullPath(args[0]),renders=Path.Combine(sources,"renders"),output=Path.GetFullPath(args[1]);Directory.CreateDirectory(output);
        var layout=new JavaScriptSerializer().Deserialize<BakeLayout[]>(File.ReadAllText(Path.Combine(renders,"layout.json")));
        var recipes=new JavaScriptSerializer().Deserialize<GearRecipe[]>(File.ReadAllText(Path.Combine(sources,"items.json")));
        using(var icons=new Bitmap(recipes.Length*32,32,PixelFormat.Format32bppArgb)){
            for(int i=0;i<recipes.Length;i++)using(var source=new Bitmap(Path.Combine(sources,(recipes[i].source??recipes[i].id)+"-icon.png"))){
                var bounds=Bounds(source);float scale=Math.Min(28f/bounds.Width,28f/bounds.Height);int width=Math.Max(1,(int)Math.Round(bounds.Width*scale)),height=Math.Max(1,(int)Math.Round(bounds.Height*scale));
                for(int y=0;y<height;y++)for(int x=0;x<width;x++)icons.SetPixel(i*32+(32-width)/2+x,(32-height)/2+y,Pixel(Sample(source,bounds.X+x/scale,bounds.Y+y/scale,1/scale,1/scale),1.1f));
            }icons.Save(Path.Combine(output,"quest-icons.png"));
        }
        var held=recipes.Where(r=>r.art.kind=="weapon"||r.art.kind=="shield").ToArray();
        using(var weapons=new Bitmap((held.Max(r=>r.art.index)+1)*64,64,PixelFormat.Format32bppArgb)){
            foreach(var recipe in held){int i=recipe.art.index,view=recipe.art.view,height=recipe.art.height;string name=recipe.source;using(var source=new Bitmap(Path.Combine(renders,name+"-"+view+".png"))){
                var bounds=Bounds(source);var info=Array.Find(layout,p=>p.name==name&&p.view==view);float scale=height/(float)bounds.Height;
                int width=Math.Max(1,(int)Math.Round(bounds.Width*scale));int left=i*64+32-(int)Math.Round((info.pivotX-bounds.Left)*scale),top=42-(int)Math.Round((info.pivotY-bounds.Top)*scale);
                for(int y=0;y<height;y++)for(int x=0;x<width;x++){int px=left+x,py=top+y;if(px<i*64||px>=(i+1)*64||py<0||py>=64)continue;weapons.SetPixel(px,py,Pixel(Sample(source,bounds.X+x/scale,bounds.Y+y/scale,1/scale,1/scale)));}
            }}weapons.Save(Path.Combine(output,"quest-weapons.png"));
        }
        var bodies=recipes.Where(r=>r.art.kind=="body").ToArray();
        using(var armor=new Bitmap((bodies.Max(r=>r.art.index)+1)*192,192,PixelFormat.Format32bppArgb))
        using(var mask=new Bitmap(Path.Combine(renders,"king-tunic.png"))){
            foreach(var recipe in bodies)using(var source=new Bitmap(Path.Combine(renders,recipe.source+"-"+recipe.art.view+".png"))){int body=recipe.art.index;
                var bounds=Bounds(source);
                for(int pose=0;pose<13;pose++){
                    int sourceX=224+PoseX[pose]*48,sourceY=PoseY[pose]*48;Rectangle bodyBounds=new Rectangle();int left=48,top=48,right=-1,bottom=-1;
                    for(int y=0;y<48;y++)for(int x=0;x<48;x++)if(mask.GetPixel(sourceX+x,sourceY+y).A>96){left=Math.Min(left,x);top=Math.Min(top,y);right=Math.Max(right,x);bottom=Math.Max(bottom,y);}
                    if(right<left)continue;bodyBounds=Rectangle.FromLTRB(left,top,right+1,bottom+1);
                    for(int y=top;y<=bottom;y++)for(int x=left;x<=right;x++){
                        var native=mask.GetPixel(sourceX+x,sourceY+y);if(native.A<96)continue;
                        float u=(x-left+.5f)/bodyBounds.Width,v=(y-top+.5f)/bodyBounds.Height;
                        // use the breastplate's center, then keep the native cloth's pose and shading
                        var color=Sample(source,bounds.X+bounds.Width*(.25f+u*.5f),bounds.Y+bounds.Height*(.12f+v*.78f),Math.Max(1,bounds.Width*.5f/bodyBounds.Width),Math.Max(1,bounds.Height*.78f/bodyBounds.Height));
                        if(color.A==0)color=Sample(source,bounds.X+bounds.Width*.48f,bounds.Y+bounds.Height*(.12f+v*.78f),3,3);
                        float shade=.66f+.34f*(native.R+native.G+native.B)/(3f*255);color=Pixel(color,recipe.art.gain);
                        if(color.A>0)color=Color.FromArgb(255,(int)(color.R*shade),(int)(color.G*shade),(int)(color.B*shade));
                        armor.SetPixel(body*192+(pose%4)*48+x,(pose/4)*48+y,color);
                    }
                }
            }armor.Save(Path.Combine(output,"quest-armor.png"));
        }
        Console.WriteLine("[OK] Quest icons, held gear and "+bodies.Length*13+" native King armor poses: "+output);return 0;
    }
}
