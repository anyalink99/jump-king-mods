using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MegaMappingExpansion
{
    internal static class PlatformImageScan
    {
        // packed input is ARGB, matching an editable bitmap rather than XNA's ABGR
        internal static List<PlatformCell> Collect(MovingPlatformPlan plan,MapLayout layout,Func<int,int,uint> pixel)
        {
            var palette=new Dictionary<uint,string>();
            foreach(var p in plan.Paint)foreach(string color in new[]{p.Color,p.Range}.Where(c=>c!=""))palette.Add(0xff000000U|Convert.ToUInt32(color.Substring(1),16),color);
            var cells=new List<PlatformCell>();
            for(int screen=0;screen<layout.Screens;screen++)
            {
                int ox,oy;layout.Origin(screen,out ox,out oy);int width=layout.Width(screen),height=layout.Height(screen),stride=layout.CellWidth/width,scale=480/width;
                for(int y=0;y<height;y++)for(int x=0;x<width;x++)
                {
                    string color;uint value=pixel(ox+x*stride,oy+y*stride);if(!palette.TryGetValue(value,out color))continue;
                    int start=x;while(x+1<width&&pixel(ox+(x+1)*stride,oy+y*stride)==value)x++;
                    cells.Add(new PlatformCell{Color=color,Screen=screen+1,Rect=new PlatformRect(start*scale,y*scale,(x-start+1)*scale,scale)});
                    if(cells.Count>16384)throw new InvalidDataException("Too many platform marker runs; simplify the collision image");
                }
            }
            return cells;
        }
        internal static void ValidateBounds(MovingPlatformPlan plan,int screens)
        {
            foreach(var platform in plan.Platforms)
            {
                if(platform.Screen>screens)throw new InvalidDataException(platform.Id+": screen is outside the map");
                var motion=plan.Motions[plan.Profiles[platform.Profile].Motion];
                foreach(var point in motion.Points)foreach(var part in platform.Parts)
                {
                    double y=part.Y-(platform.Screen-1)*360+point.Position.Y;
                    if(part.X+point.Position.X<0||part.X+part.Width+point.Position.X>480||y<-(screens-1)*360||y+part.Height>360)throw new InvalidDataException(platform.Id+": route leaves the authored map bounds");
                }
            }
        }
    }
}
