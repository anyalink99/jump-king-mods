using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Serialization;
using JKRuntime.Particles;
using JumpKing.Level;
using Microsoft.Xna.Framework;

namespace JKRuntime
{
    internal static class ParticleTests
    {
        private static void Check(bool value,string message){if(!value)throw new Exception(message);}
        private sealed class Spark:ParticleState
        {internal object Borrowed;internal int Id;public override void Reset(){base.Reset();Borrowed=null;Id=0;}}
        private sealed class Moving:IBlock
        {
            internal Rectangle Bounds;internal bool Blocking=true;internal int Reads;
            public Rectangle GetRect(){Reads++;return Bounds;}
            public BlockCollisionType Intersects(Rectangle box,out Rectangle overlap)
            {overlap=Rectangle.Intersect(box,Bounds);return Bounds.Intersects(box)?(Blocking?BlockCollisionType.Collision_Blocking:BlockCollisionType.Collision_NonBlocking):BlockCollisionType.NoCollision;}
        }
        private static void Main()
        {Pool();Geometry();NativeLifetimes();Performance();Console.WriteLine("[OK] Particles: pooled lifetimes/order, shared world scopes, native/custom geometry, invalidation, bounded overflow and warm allocation budget");}
        private static void Pool()
        {
            int stepped=0;
            using(var pool=new ParticleSystem<Spark>(3,delegate(Spark p,float dt){p.Position+=p.Velocity*dt;stepped++;}))
            {
                var a=pool.Spawn();a.Id=1;a.Life=.01f;a.Borrowed=new object();
                var b=pool.Spawn();b.Id=2;b.Life=1;
                var c=pool.Spawn();c.Id=3;c.Life=1;
                Check(pool.Spawn()==null&&pool.DroppedCount==1,"Capacity rejects excess particles");
                pool.Update(.02f);Check(pool.Count==2&&pool[0]==b&&pool[1]==c&&stepped==2&&a.Borrowed==null,"Stable retirement releases resources before updating survivors");
                Check(ReferenceEquals(pool.Spawn(),a)&&a.Id==0,"Expired storage is reused and reset");a.Life=1;
                var replacement=pool.Spawn(true);replacement.Life=1;
                Check(replacement==b&&pool[0]==c&&pool[2]==b,"Oldest replacement preserves visual order");
                pool.Update(0);Check(stepped==2,"Zero delta is paused");
                bool reject=false;try{pool.Update(float.NaN);}catch(ArgumentOutOfRangeException){reject=true;}Check(reject,"Nonfinite deltas are rejected");
                pool.Clear();Check(pool.Count==0&&c.Life==0,"Clear resets pooled state");
            }
            ParticleSystem<Spark> reentrant=null;
            using(reentrant=new ParticleSystem<Spark>(2,delegate{reentrant.Spawn();}))
            {reentrant.Spawn().Life=1;bool rejected=false;try{reentrant.Update(.01f);}catch(InvalidOperationException){rejected=true;}Check(rejected&&reentrant.Count==1,"Reentrant mutations fail without corrupting the pool");reentrant.Clear();}
        }
        private static void Geometry()
        {
            var moving=new Moving{Bounds=new Rectangle(20,0,4,6)};
            var blocks=new IBlock[]{new BoxBlock(new Rectangle(-30,10,80,1)),new SlopeBlock(new Rectangle(-16,-16,16,16),SlopeType.TopLeft),new WaterBlock(new Rectangle(0,0,10,10)),moving};
            var region=new Rectangle(-64,-64,128,128);
            using(var world=new ParticleWorld(blocks))using(var frame=new ParticleCollisionFrame())
            {
                frame.Begin(world,region);
                for(int y=-25;y<20;y++)for(int x=-35;x<55;x++)
                {
                    var pixel=new Rectangle(x,y,1,1);bool expected=false;Rectangle overlap;
                    foreach(var block in blocks)if(block.Intersects(pixel,out overlap)==BlockCollisionType.Collision_Blocking){expected=true;break;}
                    Check(frame.Contains(new Vector2(x+.2f,y+.3f))==expected,"Prepared point query differs from native geometry");
                }
                moving.Blocking=false;frame.Begin(world,region);Check(!frame.Contains(new Vector2(21,1)),"Foreign blocking state updates each tick");
                moving.Blocking=true;moving.Bounds=new Rectangle(32,0,3,5);frame.Begin(world,region);
                Check(frame.Contains(new Vector2(33,1))&&!frame.Contains(new Vector2(21,1)),"Foreign movement crosses cell boundaries");
                Check(frame.Intersects(new Rectangle(20,1,20,1)),"Swept fire rectangle reaches an obstacle");
                frame.Clear();Check(!frame.Contains(new Vector2(33,1)),"Cleared frame releases query state");
                typeof(BoxBlock).GetField("m_collider",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(blocks[0],new Rectangle(-30,30,80,1));
                world.Invalidate();frame.Begin(world,region);Check(!frame.Contains(new Vector2(0,10))&&frame.Contains(new Vector2(0,30)),"Invalidation falls back to live bounds");
                world.Rebuild();frame.Begin(world,region);Check(frame.Contains(new Vector2(0,30)),"Explicit rebuild adopts native geometry edits");
            }
            using(var large=new ParticleWorld(new[]{(IBlock)new BoxBlock(new Rectangle(-100000,-100000,200000,200000))}))using(var frame=new ParticleCollisionFrame())
            {frame.Begin(large,region);Check(frame.Contains(Vector2.Zero),"Oversized blocks use bounded overflow storage");}
        }
        private static readonly FieldInfo Screens=typeof(LevelManager).GetField("m_screens",BindingFlags.Static|BindingFlags.NonPublic);
        private static readonly FieldInfo Blocks=typeof(LevelScreen).GetField("m_hitboxes",BindingFlags.Instance|BindingFlags.NonPublic);
        private static LevelScreen Screen(params IBlock[] blocks)
        {var screen=(LevelScreen)FormatterServices.GetUninitializedObject(typeof(LevelScreen));Blocks.SetValue(screen,blocks);return screen;}
        private static void NativeLifetimes()
        {
            object old=Screens.GetValue(null);
            try
            {
                var screens=new[]{Screen(new BoxBlock(new Rectangle(0,100,30,1))),Screen(new BoxBlock(new Rectangle(0,-10,30,1)))};Screens.SetValue(null,screens);
                var first=new RuntimeScope();var second=new RuntimeScope();
                var a=ParticleWorlds.PrepareNative(first);var b=ParticleWorlds.PrepareNative(second);
                Check(ReferenceEquals(a,b),"Consumers share one world index");first.Dispose();Check(!a.IsDisposed,"Other consumer retains its world lease");
                using(var frame=new ParticleCollisionFrame())
                {
                    frame.Begin(b,new Rectangle(-10,-20,60,140));Check(frame.Contains(new Vector2(10,-10))&&frame.Contains(new Vector2(10,100)),"Query spans screens independently of the camera");
                    ((IBlock[])Blocks.GetValue(screens[0]))[0]=new BoxBlock(new Rectangle(32,100,8,1));
                    frame.Begin(b,new Rectangle(-10,-20,60,140));Check(!frame.Contains(new Vector2(10,100))&&frame.Contains(new Vector2(34,100)),"Replaced entries in the same native array invalidate cells");
                    Blocks.SetValue(screens[1],new IBlock[]{new BoxBlock(new Rectangle(0,-12,30,1))});
                    frame.Begin(b,new Rectangle(-10,-20,60,140));Check(!frame.Contains(new Vector2(10,-10))&&frame.Contains(new Vector2(10,-12)),"Replaced block arrays cannot use stale cells");
                    Screens.SetValue(null,new[]{Screen(new BoxBlock(new Rectangle(40,100,8,2)))});
                    frame.Begin(b,new Rectangle(-10,-20,60,140));Check(!frame.Contains(new Vector2(10,100))&&frame.Contains(new Vector2(42,100)),"Old world cannot leak collision after replacement");
                    second.Dispose();Check(a.IsDisposed&&ParticleWorlds.Current==null,"Last preparation scope releases shared geometry");
                    Screens.SetValue(null,null);frame.Begin(null,new Rectangle(0,0,20,20));Check(!frame.Contains(Vector2.Zero),"Unloaded world is empty");
                }
            }
            finally{Screens.SetValue(null,old);}
        }
        private static void Performance()
        {
            var blocks=new List<IBlock>();for(int y=0;y<20;y++)for(int x=0;x<48;x++)blocks.Add(new BoxBlock(new Rectangle(x*10,y*18,8,3)));
            using(var world=new ParticleWorld(blocks))using(var frame=new ParticleCollisionFrame())using(var pool=new ParticleSystem<Spark>(256,delegate(Spark p,float dt){p.Position+=p.Velocity*dt;}))
            {
                Action tick=delegate{
                    if(pool.Count==0)for(int i=0;i<256;i++){var p=pool.Spawn();p.Life=.05f;p.Position=new Vector2(i,100);}
                    pool.Update(1f/60);frame.Begin(world,new Rectangle(0,0,480,360));
                    for(int i=0;i<256;i++){frame.Contains(new Vector2(i,100));frame.Intersects(new Rectangle(i,100,4,4));}frame.Clear();
                };
                for(int i=0;i<100;i++)tick();
                var method=typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread",Type.EmptyTypes);
                var allocated=method==null?null:(Func<long>)Delegate.CreateDelegate(typeof(Func<long>),method);
                var watch=new Stopwatch();long before=allocated==null?0:allocated();watch.Start();for(int i=0;i<1000;i++)tick();watch.Stop();long bytes=allocated==null?0:allocated()-before;
                Check(allocated==null||bytes<=128,"Warm particle simulation/collision allocates per update");
                Console.WriteLine("[PERF] Runtime particles: 256 particles + point/sweep queries, 960 blocks, {0:F3}ms/update, {1} bytes/1000 updates",watch.Elapsed.TotalMilliseconds/1000,bytes);
            }
        }
    }
}
