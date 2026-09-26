using System;
using System.Reflection;
using JumpKing;
using JumpKing.Level;
using Microsoft.Xna.Framework;

namespace SubframeCharge
{
    // A bounded, read-only visual path for one native tick. No body behaviours,
    // event callbacks or speculative bounces. Path storage uses fixed arrays.
    internal sealed class PredictionPath
    {
        private const int MaxSteps=128, MaxBlocks=256, MaxScan=8192;
        private readonly Vector2[] points=new Vector2[MaxSteps+1];
        private readonly IBlock[] blocks=new IBlock[MaxBlocks];
        private readonly Rectangle[] bounds=new Rectangle[MaxBlocks];
        private Vector2 start, velocity;
        private Rectangle region;
        private int width,height,count,steps;
        private bool safe;
        private Vector2? nativeVelocity;
        private bool grounded;
        private Vector2? contactCorner;
        private float minX,maxX;
        private static readonly FieldInfo Screens=typeof(LevelManager).GetField("m_screens",BindingFlags.Static|BindingFlags.NonPublic);
        private static readonly FieldInfo Blocks=typeof(LevelScreen).GetField("m_hitboxes",BindingFlags.Instance|BindingFlags.NonPublic);

        internal void Begin(Vector2 position,Vector2 motion,int w,int h,bool cap)
        {
            Array.Clear(blocks,0,count); count=steps=0;
            start=position; velocity=motion; width=w; height=h; safe=w>0 && h>0;
            nativeVelocity=null; grounded=false; contactCorner=null;
            minX=cap ? -w/2f : float.NegativeInfinity; maxX=cap ? 480-w/2 : float.PositiveInfinity;
            var from=Hitbox(position); var to=Hitbox(position+motion);
            region=Rectangle.Union(from,to); region.Inflate(2,2);
        }
        private Rectangle Hitbox(Vector2 position)
        { return new Rectangle((int)position.X,(int)position.Y,width,height); }
        internal void Add(IBlock block)
        {
            if(!safe || block==null) return;
            Rectangle rectangle;
            // Do not run GetRect on a foreign object: it is arbitrary mod code,
            // and even a BoxBlock subclass can replace the meaning of its bounds.
            if(!JKRuntime.Geometry.NativeWorldGeometry.TryReadNativeBounds(block,out rectangle))
            { return; }
            if(!region.Intersects(rectangle)) return;
            // Unsupported material rules affect actual motion, already observed
            // by PositionHistory. They do not disable presentation for the scene.
            if(block is QuarkBlock || block is NoWindBlock || count==MaxBlocks) return;
            Rectangle overlap;
            // A foreign handler may deliberately place the real player inside a
            // nominal solid. Let drawing follow that authoritative movement.
            if(!(block is SandBlock) && !(block is WaterBlock) && rectangle.Intersects(Hitbox(start)) && (!(block is SlopeBlock)
                || PredictionCollision.Intersects((SlopeBlock)block,Hitbox(start),out overlap)==BlockCollisionType.Collision_Blocking)) return;
            blocks[count]=block; bounds[count++]=rectangle;
        }
        private bool Clear(Vector2 position)
        {
            Rectangle hitbox=Hitbox(position), overlap;
            for(int i=0;i<count;i++)
                if(!(blocks[i] is SandBlock) && !(blocks[i] is WaterBlock) && bounds[i].Intersects(hitbox) && (!(blocks[i] is SlopeBlock)
                    || PredictionCollision.Intersects((SlopeBlock)blocks[i],hitbox,out overlap)==BlockCollisionType.Collision_Blocking)) return false;
            return true;
        }
        internal void Build()
        {
            points[0]=start;
            if(!safe || !Clear(start)) { safe=false; return; }
            if(nativeVelocity.HasValue) PrepareNativeContact();
            if(contactCorner.HasValue && BuildContactPath(contactCorner.Value)) return;
            steps=Math.Min(MaxSteps,Math.Max(1,(int)Math.Ceiling(Math.Max(Math.Abs(velocity.X),Math.Abs(velocity.Y))*2)));
            Vector2 delta=velocity/steps;
            for(int i=1;i<=steps;i++)
            {
                Vector2 next=points[i-1];
                var diagonal=new Vector2(Math.Max(minX,Math.Min(maxX,next.X+delta.X)),next.Y+delta.Y);
                // Tangential slope motion can be clear even when an isolated
                // horizontal step enters the slope. Keep the complete direction.
                if(Clear(diagonal)) { points[i]=diagonal; continue; }
                var x=new Vector2(diagonal.X,next.Y);
                if(Clear(x)) next=x;
                var y=new Vector2(next.X,next.Y+delta.Y);
                if(Clear(y)) next=y;
                points[i]=next;
            }
        }
        internal Vector2 At(float alpha)
        {
            if(!safe || steps==0) return start;
            float phase=Math.Max(0,Math.Min(1,alpha))*steps;
            int index=Math.Min(steps,(int)phase);
            if(index==steps) return points[index];
            Vector2 next=Vector2.Lerp(points[index],points[index+1],phase-index);
            // Pixel rounding near a diagonal corner must not cut through it.
            return Clear(next) ? next : points[index];
        }
        private bool BuildContactPath(Vector2 corner)
        {
            // Prefer a straight, smooth segment. At an overhang that segment
            // may cut through the corner despite both native endpoints being
            // clear. Use the native X-then-Y route only in that case.
            var end=start+velocity;
            steps=Math.Min(MaxSteps,Math.Max(1,(int)Math.Ceiling(Math.Max(Math.Abs(velocity.X),Math.Abs(velocity.Y))*2)));
            bool clear=true;
            for(int i=1;i<=steps;i++)
            {
                points[i]=Vector2.Lerp(start,end,i/(float)steps);
                if(!Clear(points[i])) { clear=false; break; }
            }
            if(clear) return true;
            float first=Vector2.Distance(start,corner),second=Vector2.Distance(corner,end),length=first+second;
            steps=Math.Min(MaxSteps,Math.Max(1,(int)Math.Ceiling(length*2)));
            for(int i=1;i<=steps;i++)
            {
                float distance=length*i/steps;
                points[i]=distance<first && first>0 ? Vector2.Lerp(start,corner,distance/first)
                    : second>0 ? Vector2.Lerp(corner,end,(distance-first)/second) : end;
                if(!Clear(points[i])) return false;
            }
            return true;
        }
        // Resolve only a visual endpoint against stored, recognized geometry.
        // Native physics moves X, resolves it (possibly redirecting Y along a
        // slope), then moves Y. Microstepping the old displacement in the other
        // order produces an endpoint the real player never reaches.
        private void PrepareNativeContact()
        {
            bool sand=false,water=false,relevant=false;
            var box=Hitbox(start);
            for(int i=0;i<count;i++)
            {
                if(blocks[i] is SlopeBlock || blocks[i] is SandBlock || blocks[i] is WaterBlock) relevant=true;
                if(!bounds[i].Intersects(box)) continue;
                if(blocks[i] is SandBlock) sand=true;
                if(blocks[i] is WaterBlock) water=true;
            }
            if(!relevant) return;
            var raw=Vector2.Clamp(nativeVelocity.Value,new Vector2(-64),new Vector2(64));
            float scale=water ? .5f : 1f;
            var next=start;
            float dx=raw.X*scale*(sand ? .25f : 1f);
            if(grounded && velocity.X==0) dx=0;
            next.X=Math.Max(minX,Math.Min(maxX,next.X+dx));
            Vector2 normal;
            if(Contact(next,true,sand,raw.Y,out normal))
            {
                next.X=(int)next.X;
                int sign=dx>0 ? 1 : -1;
                bool clear=false;
                for(int n=0;n<MaxSteps;n++)
                {
                    next.X-=sign;
                    Vector2 found;
                    if(!Contact(next,true,sand,raw.Y,out found)) { clear=true; break; }
                    normal=found;
                }
                if(!clear) return;
                if(normal!=Vector2.Zero && !(grounded && Math.Abs(raw.X)<=PlayerValues.WALK_SPEED && raw.Y>0)
                    && Vector2.Dot(raw,normal)<0)
                {
                    normal.Normalize();
                    var tangent=new Vector2(normal.Y,-normal.X);
                    raw=Vector2.Dot(raw,tangent)*tangent;
                }
            }
            var corner=next;
            float dy=raw.Y*scale*(sand && raw.Y<=0 ? .5f : 1f);
            // Native sand adds this pixel while grounded outside the volume.
            if(!sand && grounded && raw.Y>0) dy+=1;
            next.Y+=dy;
            if(Contact(next,false,sand,raw.Y,out normal))
            {
                next.Y=(int)next.Y;
                int sign=raw.Y>0 ? 1 : -1;
                bool clear=false;
                for(int n=0;n<MaxSteps;n++)
                {
                    next.Y-=sign;
                    if(!Contact(next,false,sand,raw.Y,out normal)) { clear=true; break; }
                }
                if(!clear) return;
            }
            velocity=Vector2.Clamp(next-start,new Vector2(-64),new Vector2(64));
            contactCorner=corner;
        }
        private bool Contact(Vector2 position,bool horizontal,bool inSand,float vy,out Vector2 normal)
        {
            var box=Hitbox(position);
            bool collision=false;
            int closest=int.MaxValue,highestSlope=int.MaxValue,highestBox=int.MaxValue;
            normal=Vector2.Zero;
            Vector2 upperNormal=Vector2.Zero;
            for(int i=0;i<count;i++)
            {
                if(!bounds[i].Intersects(box) || blocks[i] is WaterBlock) continue;
                if(blocks[i] is SandBlock)
                { if(!inSand && (horizontal || vy<0)) collision=true; continue; }
                var slope=blocks[i] as SlopeBlock;
                Rectangle overlap;
                if(slope!=null)
                {
                    if(PredictionCollision.Intersects(slope,box,out overlap)!=BlockCollisionType.Collision_Blocking) continue;
                    var candidate=PredictionCollision.Normal(slope);
                    int distance=Math.Abs(bounds[i].Center.X-box.Center.X);
                    if(distance<=closest) { closest=distance; normal=candidate; }
                    if(bounds[i].Y<highestSlope) { highestSlope=bounds[i].Y; upperNormal=candidate; }
                }
                else highestBox=Math.Min(highestBox,bounds[i].Y);
                collision=true;
            }
            if(highestBox<=highestSlope && upperNormal.Y<0) normal=Vector2.Zero;
            return collision;
        }
        internal void Prepare(Vector2 position,Vector2 motion,Rectangle hitbox,int currentScreen,Vector2? incoming=null,bool onGround=false)
        {
            var screens=Screens==null ? null : Screens.GetValue(null) as LevelScreen[];
            bool available=screens!=null && currentScreen>=0 && currentScreen<screens.Length && screens[currentScreen]!=null;
            Begin(position,motion,hitbox.Width,hitbox.Height,available && !screens[currentScreen].CanTeleport);
            if(incoming.HasValue && !float.IsNaN(incoming.Value.X) && !float.IsNaN(incoming.Value.Y)
                && !float.IsInfinity(incoming.Value.X) && !float.IsInfinity(incoming.Value.Y))
            {
                nativeVelocity=incoming; grounded=onGround;
                // A slope can redirect Y during horizontal resolution. Include
                // that possible displacement in the bounded discovery region.
                int reach=(int)Math.Ceiling(Math.Min(64,Math.Max(Math.Abs(incoming.Value.X),Math.Abs(incoming.Value.Y))))+2;
                var nativeRegion=Hitbox(position); nativeRegion.Inflate(reach,reach);
                region=Rectangle.Union(region,nativeRegion);
            }
            if(!available || Blocks==null) { Build(); return; }
            // Do not predict a teleport before the native update performs it.
            if(screens[currentScreen].CanTeleport && (position.X+motion.X < -width/2f || position.X+motion.X > 480-width/2))
            { safe=false; return; }
            int scanned=0;
            for(int screen=Math.Max(0,currentScreen-1);screen<=Math.Min(screens.Length-1,currentScreen+1);screen++)
            {
                var values=screens[screen]==null ? null : Blocks.GetValue(screens[screen]) as IBlock[];
                if(values==null) continue;
                foreach(var block in values)
                {
                    if(++scanned>MaxScan) break;
                    Add(block);
                }
                if(scanned>MaxScan) break;
            }
            Build();
        }
    }
}
