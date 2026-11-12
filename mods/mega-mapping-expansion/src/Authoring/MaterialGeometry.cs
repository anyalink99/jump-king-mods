using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

namespace MegaMappingExpansion
{
    internal static class MaterialGeometry
    {
        internal static List<PlatformRect> Compile(MaterialArea area,string root)
        {
            var filled=new bool[480*360];double[] polygon=area.Shape=="polygon"?MaterialPlan.Points(area.Outline):null;
            Bitmap mask=null;
            try
            {
                if(area.Shape=="mask")
                {
                    string path=Path.GetFullPath(Path.Combine(root,area.Mask));string prefix=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
                    if(!path.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Material mask leaves map");
                    using(var image=Image.FromFile(path))
                    {if(image.Width>4096||image.Height>4096)throw new InvalidDataException("Material mask exceeds 4096 pixels per axis");mask=new Bitmap(image);}
                }
                foreach(var rect in area.Parts)for(int y=rect.Y;y<rect.Y+rect.Height;y++)for(int x=rect.X;x<rect.X+rect.Width;x++)
                {
                    bool inside=true;double px=x+.5-rect.X,py=y+.5-rect.Y;
                    if(area.Shape=="disc"){double dx=px*2/rect.Width-1,dy=py*2/rect.Height-1;inside=dx*dx+dy*dy<=1;}
                    else if(polygon!=null)inside=Contains(polygon,px,py);
                    else if(mask!=null)inside=mask.GetPixel((x-rect.X)*mask.Width/rect.Width,(y-rect.Y)*mask.Height/rect.Height).A>=128;
                    if(inside)filled[y*480+x]=true;
                }
                foreach(var hole in area.Holes)for(int y=hole.Y;y<hole.Y+hole.Height;y++)for(int x=hole.X;x<hole.X+hole.Width;x++)filled[y*480+x]=false;
                return Compact(filled,480,360);
            }
            finally{if(mask!=null)mask.Dispose();}
        }
        internal static bool Contains(double[] p,double x,double y)
        {
            bool inside=false;int count=p.Length/2;
            for(int i=0,j=count-1;i<count;j=i++)
            {
                double ax=p[j*2],ay=p[j*2+1],bx=p[i*2],by=p[i*2+1];
                if((ay>y)!=(by>y)&&x<(bx-ax)*(y-ay)/(by-ay)+ax)inside=!inside;
            }
            return inside;
        }
        internal static List<PlatformRect> Compact(bool[] cells,int width,int height)
        {
            var result=new List<PlatformRect>();var previous=new Dictionary<string,int>();
            for(int y=0;y<height;y++)
            {
                var next=new Dictionary<string,int>();
                for(int x=0;x<width;x++)
                {
                    if(!cells[y*width+x])continue;int start=x;
                    while(x+1<width&&cells[y*width+x+1])x++;
                    string key=start+":"+(x-start+1);int index;
                    if(previous.TryGetValue(key,out index)){var r=result[index];r.Height++;result[index]=r;}
                    else{index=result.Count;result.Add(new PlatformRect(start,y,x-start+1,1));}
                    next.Add(key,index);
                    if(result.Count>32768)throw new InvalidDataException("Material shape exceeds 32768 rectangles");
                }
                previous=next;
            }
            return result;
        }
        internal static List<List<PlatformRect>> Groups(IEnumerable<PlatformRect> input,bool connected)
        {
            var rects=input.OrderBy(r=>r.Y).ThenBy(r=>r.X).ToArray();
            if(rects.Length>32768)throw new InvalidDataException("Material paint exceeds 32768 rectangles; use connected or rectangles mode");
            if(!connected||rects.Length==0)return rects.Select(r=>new List<PlatformRect>{r}).ToList();
            int width=rects.Max(r=>r.X+r.Width),height=rects.Max(r=>r.Y+r.Height);
            var grid=new int[width*height];var parents=Enumerable.Range(0,rects.Length).ToArray();
            for(int i=0;i<rects.Length;i++)
            {
                var r=rects[i];for(int y=r.Y;y<r.Y+r.Height;y++)for(int x=r.X;x<r.X+r.Width;x++)grid[y*width+x]=i+1;
            }
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)
            {
                int a=grid[y*width+x]-1;if(a<0)continue;
                if(x>0&&grid[y*width+x-1]>0)parents[Root(parents,a)]=Root(parents,grid[y*width+x-1]-1);
                if(y>0&&grid[(y-1)*width+x]>0)parents[Root(parents,a)]=Root(parents,grid[(y-1)*width+x]-1);
            }
            return Enumerable.Range(0,rects.Length).GroupBy(i=>Root(parents,i)).Select(g=>g.Select(i=>rects[i]).ToList()).OrderBy(g=>g[0].Y).ThenBy(g=>g[0].X).ToList();
        }
        private static int Root(int[] parents,int i){while(parents[i]!=i){parents[i]=parents[parents[i]];i=parents[i];}return i;}
    }
}
