using System;
using System.Collections.Generic;
using JumpKing.Level;
using Microsoft.Xna.Framework;

namespace JKRuntime.Particles
{
    public enum ParticleCollisionMode { Native, Visual }
    public struct ParticleContact
    {
        public bool Hit { get { return Block != null; } }
        public IBlock Block { get; private set; }
        public float Fraction { get; private set; }
        public Vector2 Position { get; private set; }
        public Vector2 Normal { get; private set; }
        internal ParticleContact(IBlock block,float fraction,Vector2 position,Vector2 normal):this()
        {Block=block;Fraction=fraction;Position=position;Normal=normal;}
    }
    /// <summary>reusable, update-local collision queries. clear after simulation, never keep occupancy across ticks</summary>
    public sealed class ParticleCollisionFrame : IDisposable
    {
        private readonly ParticleGrid live=new ParticleGrid();
        private readonly Dictionary<long,bool> points=new Dictionary<long,bool>(4096);
        private readonly HashSet<IBlock> seen=new HashSet<IBlock>();
        private readonly Func<Vector2,bool> contains;
        private ParticleWorld world;
        private Rectangle region;
        private bool active,disposed;
        private int queries;
        private readonly bool visual;
        public int NarrowPhaseQueries {get{return queries;}}
        public Func<Vector2,bool> PointQuery {get{return contains;}}
        public ParticleCollisionFrame():this(ParticleCollisionMode.Native){}
        public ParticleCollisionFrame(ParticleCollisionMode mode)
        {if(mode!=ParticleCollisionMode.Native&&mode!=ParticleCollisionMode.Visual)throw new ArgumentOutOfRangeException("mode");visual=mode==ParticleCollisionMode.Visual;contains=Contains;}
        /// <summary>use prepared static cells plus live foreign geometry. Null uses native blocks in the requested region</summary>
        public void Begin(ParticleWorld prepared,Rectangle bounds)
        {
            RuntimeApi.Kernel.CheckThread();if(disposed)throw new ObjectDisposedException("ParticleCollisionFrame");Clear();
            if(bounds.Width<=0||bounds.Height<=0)throw new ArgumentOutOfRangeException("bounds");region=bounds;
            try
            {
                if(prepared!=null&&prepared.CanUse(bounds))
                {
                    world=prepared;
                    for(int i=0;i<world.Dynamic.Count;i++)Add(world.Dynamic[i]);
                }
                else if(prepared!=null&&!prepared.IsDisposed&&!prepared.IsNative)prepared.AddLive(live,bounds);
                else
                {
                    var screens=ParticleNative.ReadScreens();
                    if(screens!=null)for(int i=ParticleNative.First(bounds);i<=ParticleNative.Last(bounds,screens.Length);i++)
                    {var blocks=ParticleNative.ReadBlocks(screens[i]);if(blocks!=null)for(int j=0;j<blocks.Length;j++)if(blocks[j]!=null)Add(blocks[j]);}
                }
                active=true;
            }
            catch{Clear();throw;}
        }
        private void Add(IBlock block)
        {var overlap=Rectangle.Intersect(block.GetRect(),region);if(overlap.Width>0&&overlap.Height>0)live.Add(block,overlap);}
        public bool Contains(Vector2 position)
        {
            if(!active)return false;int x=(int)Math.Floor(position.X),y=(int)Math.Floor(position.Y);
            if(!region.Contains(x,y))return false;long key=ParticleGrid.Key(x,y);bool value;
            if(points.TryGetValue(key,out value))return value;
            var box=new Rectangle(x,y,1,1);
            value=(world!=null&&world.Static.Intersects(box,null,ref queries,visual))||live.Intersects(box,null,ref queries,visual);
            // author-controlled positions can't grow scratch memory without a bound
            if(points.Count<32768)points.Add(key,value);return value;
        }
        /// <summary>exact native rectangle query in the prepared region. Useful for conservative fire sweeps</summary>
        public bool Intersects(Rectangle bounds)
        {
            if(!active||bounds.Width<=0||bounds.Height<=0||!region.Intersects(bounds))return false;
            if(bounds.Width>4096||bounds.Height>4096)throw new ArgumentOutOfRangeException("bounds");
            seen.Clear();return (world!=null&&world.Static.Intersects(bounds,seen,ref queries,visual))||live.Intersects(bounds,seen,ref queries,visual);
        }
        /// <summary>Visual-mode sweep of an axis-aligned particle box. Position is its centre at earliest contact.</summary>
        public bool Sweep(Vector2 center,Vector2 movement,Vector2 halfSize,out ParticleContact hit)
        {
            hit=default(ParticleContact);
            if(!visual)throw new InvalidOperationException("Sweeps require visual collision mode");
            if(!Finite(center.X)||!Finite(center.Y)||!Finite(movement.X)||!Finite(movement.Y)||!Finite(halfSize.X)||!Finite(halfSize.Y)
                ||halfSize.X<0||halfSize.Y<0||halfSize.X>2048||halfSize.Y>2048||Math.Abs(movement.X)>4096||Math.Abs(movement.Y)>4096)throw new ArgumentException("Invalid particle sweep");
            if(!active)return false;
            var end=center+movement;
            int left=(int)Math.Floor(Math.Min(center.X,end.X)-halfSize.X),top=(int)Math.Floor(Math.Min(center.Y,end.Y)-halfSize.Y);
            var area=new Rectangle(left,top,(int)Math.Ceiling(Math.Max(center.X,end.X)+halfSize.X)-left+1,(int)Math.Ceiling(Math.Max(center.Y,end.Y)+halfSize.Y)-top+1);
            if(!region.Contains(area))throw new ArgumentException("Sweep exceeds the prepared particle region");
            seen.Clear();if(world!=null)world.Static.Sweep(area,center,movement,halfSize,seen,ref queries,ref hit);
            live.Sweep(area,center,movement,halfSize,seen,ref queries,ref hit);return hit.Hit;
        }
        private static bool Finite(float n){return !float.IsNaN(n)&&!float.IsInfinity(n);}
        public void Clear(){world=null;live.Clear();points.Clear();seen.Clear();active=false;queries=0;}
        public void Dispose(){Clear();disposed=true;}
    }
}
