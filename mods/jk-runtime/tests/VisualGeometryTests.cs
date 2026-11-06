using System;
using System.Linq;
using JKRuntime.Geometry;
using JKRuntime.Particles;
using JumpKing.Level;
using Microsoft.Xna.Framework;

namespace JKRuntime
{
    internal static class VisualGeometryTests
    {
        private static void Check(bool value,string message){if(!value)throw new Exception(message);}
        private static void Main()
        {
            var bounds=new Rectangle(0,0,8,8);
            foreach(var type in new[]{SlopeType.TopLeft,SlopeType.TopRight,SlopeType.BottomLeft,SlopeType.BottomRight})
            {
                var source=new SlopeBlock(bounds,type);var native=NativeWorldGeometry.ReadSlopeVertices(source);
                var shape=new VisualShape(VisualGeometry.ReadSlope(source));
                for(int y=0;y<8;y++)for(int x=0;x<8;x++)
                {
                    bool filled=type==SlopeType.TopLeft?x+y>=7:type==SlopeType.TopRight?y>=x:type==SlopeType.BottomLeft?x>=y:x+y<=7;
                    Check(shape.Contains(new Vector2(x+.5f,y+.5f))==filled,"Declared Debug sprite coverage: "+type);
                }
                Check(native.SequenceEqual(NativeWorldGeometry.ReadSlopeVertices(source)),"Reading visual shapes must not change native lines");
                var copy=NativeWorldGeometry.TransformSlopeCollision(source,new Rectangle(16,-360,16,16));
                Check(NativeWorldGeometry.ReadSlopeVertices(copy).SequenceEqual(native.Select(p=>p*2+new Vector2(16,-360))),"Copies transform actual native lines, including southwest quirks");
                var detached=shape.Vertices;detached[0]=new Vector2(999,999);Check(shape.Vertices[0]!=detached[0],"Public vertices are detached");
            }
            var slope=new SlopeBlock(new Rectangle(0,0,64,64),SlopeType.TopRight);VisualShape surface;VisualGeometry.TryRead(slope,out surface);
            Check(surface.Intersects(new Rectangle(8,48,5,5)),"A small box wholly inside the solid triangle collides");
            Check(!surface.Intersects(new Rectangle(48,8,5,5)),"The opposite empty half stays empty");
            float time;Vector2 normal;
            Check(surface.Sweep(new Vector2(40,0),new Vector2(0,64),Vector2.Zero,out time,out normal)&&Math.Abs(time-.625f)<.0001f,"Sweeps hit the diagonal, not its bounding box");
            Check(normal.X>.7f&&normal.Y<-.7f,"Diagonal contact has its actual outward normal");
            var southwest=new VisualShape(VisualGeometry.DeclaredSlope(new Rectangle(0,0,64,64),SlopeType.BottomLeft));
            Check(southwest.Sweep(new Vector2(0,40),new Vector2(64,0),Vector2.Zero,out time,out normal)&&Math.Abs(time-.625f)<.0001f&&normal.X<-.7f&&normal.Y>.7f,"Southwest has its own swept surface and normal");
            var floor=new VisualShape(VisualGeometry.DeclaredSlope(new Rectangle(0,40,64,8),SlopeType.None));
            Check(floor.Sweep(new Vector2(20,0),new Vector2(0,100),new Vector2(2),out time,out normal)&&Math.Abs(time-.38f)<.0001f&&normal==new Vector2(0,-1),"Thin surfaces cannot be tunneled through");
            Check(!floor.Sweep(new Vector2(20,38),new Vector2(0,-10),new Vector2(2),out time,out normal),"Contact moving outward doesn't stick");
            using(var world=new ParticleWorld(new IBlock[]{slope,new WaterBlock(new Rectangle(70,0,20,20))}))
            using(var visual=new ParticleCollisionFrame(ParticleCollisionMode.Visual))
            using(var legacy=new ParticleCollisionFrame())
            {
                var region=new Rectangle(-20,-20,140,140);visual.Begin(world,region);legacy.Begin(world,region);
                Rectangle overlap;var small=new Rectangle(8,48,5,5);
                Check(legacy.Intersects(small)==(((IBlock)slope).Intersects(small,out overlap)==BlockCollisionType.Collision_Blocking),"Existing particle callers retain native semantics");
                Check(visual.Intersects(small)&&!visual.Contains(new Vector2(75,5)),"Visual particles see filled slopes and keep water nonblocking");
                ParticleContact hit;Check(visual.Sweep(new Vector2(40,0),new Vector2(0,64),Vector2.Zero,out hit)&&hit.Normal.X>.7f,"Prepared particle index returns diagonal contacts");
                world.Invalidate();visual.Begin(world,region);Check(visual.Intersects(small),"Live fallback uses the same visual geometry");
            }
            Console.WriteLine("[OK] Shared visual geometry: all Debug orientations, detached/native contours, filled-area tests, diagonal sweeps and particle compatibility");
        }
    }
}
