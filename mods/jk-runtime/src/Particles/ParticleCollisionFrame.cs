using System;
using System.Collections.Generic;
using JumpKing.Level;
using Microsoft.Xna.Framework;

namespace JKRuntime.Particles
{
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
        public int NarrowPhaseQueries {get{return queries;}}
        public Func<Vector2,bool> PointQuery {get{return contains;}}
        public ParticleCollisionFrame(){contains=Contains;}
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
            value=(world!=null&&world.Static.Intersects(box,null,ref queries))||live.Intersects(box,null,ref queries);
            // author-controlled positions can't grow scratch memory without a bound
            if(points.Count<32768)points.Add(key,value);return value;
        }
        /// <summary>exact native rectangle query in the prepared region. Useful for conservative fire sweeps</summary>
        public bool Intersects(Rectangle bounds)
        {
            if(!active||bounds.Width<=0||bounds.Height<=0||!region.Intersects(bounds))return false;
            if(bounds.Width>4096||bounds.Height>4096)throw new ArgumentOutOfRangeException("bounds");
            seen.Clear();return (world!=null&&world.Static.Intersects(bounds,seen,ref queries))||live.Intersects(bounds,seen,ref queries);
        }
        public void Clear(){world=null;live.Clear();points.Clear();seen.Clear();active=false;queries=0;}
        public void Dispose(){Clear();disposed=true;}
    }
}
