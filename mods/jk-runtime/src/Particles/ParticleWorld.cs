using System;
using System.Collections.Generic;
using System.Reflection;
using JumpKing.Level;
using Microsoft.Xna.Framework;

namespace JKRuntime.Particles
{
    internal static class ParticleNative
    {
        private static readonly FieldInfo Screens=typeof(LevelManager).GetField("m_screens",BindingFlags.Static|BindingFlags.NonPublic);
        private static readonly FieldInfo Blocks=typeof(LevelScreen).GetField("m_hitboxes",BindingFlags.Instance|BindingFlags.NonPublic);
        internal static LevelScreen[] ReadScreens()
        {if(Screens==null||Screens.FieldType!=typeof(LevelScreen[]))throw new NotSupportedException("Native particle screen contract unavailable");return Screens.GetValue(null) as LevelScreen[];}
        internal static IBlock[] ReadBlocks(LevelScreen screen)
        {if(Blocks==null||Blocks.FieldType!=typeof(IBlock[]))throw new NotSupportedException("Native particle block contract unavailable");return screen==null?null:Blocks.GetValue(screen) as IBlock[];}
        internal static int First(Rectangle r){return Math.Max(0,(int)Math.Floor((360d-r.Bottom)/360));}
        internal static int Last(Rectangle r,int count){return Math.Min(count-1,(int)Math.Floor((359d-r.Top)/360));}
    }
    internal struct ParticleBlock
    {internal IBlock Block;internal Rectangle Bounds;}
    internal sealed class ParticleGrid
    {
        internal const int MaximumCells=131072;
        private readonly Dictionary<long,List<ParticleBlock>> cells=new Dictionary<long,List<ParticleBlock>>();
        private readonly List<List<ParticleBlock>> storage=new List<List<ParticleBlock>>();
        private readonly List<ParticleBlock> overflow=new List<ParticleBlock>();
        private int used;
        internal static long Key(int x,int y){return ((long)x<<32)|(uint)y;}
        internal void Clear(){for(int i=0;i<used;i++)storage[i].Clear();used=0;cells.Clear();overflow.Clear();}
        internal void Add(IBlock block,Rectangle bounds)
        {
            if(bounds.Width<=0||bounds.Height<=0)return;
            var entry=new ParticleBlock{Block=block,Bounds=bounds};
            int left=bounds.Left>>4,top=bounds.Top>>4,right=(bounds.Right-1)>>4,bottom=(bounds.Bottom-1)>>4;
            // Oversized author geometry remains queryable without an unbounded grid allocation
            long area=(long)(right-left+1)*(bottom-top+1);
            if(area>4096||area+used>MaximumCells){overflow.Add(entry);return;}
            for(int y=top;y<=bottom;y++)for(int x=left;x<=right;x++)
            {
                long key=Key(x,y);List<ParticleBlock> bucket;
                if(!cells.TryGetValue(key,out bucket))
                {if(used==storage.Count)storage.Add(new List<ParticleBlock>());bucket=storage[used++];cells.Add(key,bucket);}
                bucket.Add(entry);
            }
        }
        internal bool Intersects(Rectangle box,HashSet<IBlock> seen,ref int queries)
        {
            for(int y=box.Top>>4;y<=(box.Bottom-1)>>4;y++)for(int x=box.Left>>4;x<=(box.Right-1)>>4;x++)
            {List<ParticleBlock> bucket;if(cells.TryGetValue(Key(x,y),out bucket)&&Test(bucket,box,seen,ref queries))return true;}
            return Test(overflow,box,seen,ref queries);
        }
        private static bool Test(List<ParticleBlock> entries,Rectangle box,HashSet<IBlock> seen,ref int queries)
        {
            for(int i=0;i<entries.Count;i++)
            {
                var entry=entries[i];if(!entry.Bounds.Intersects(box)||(seen!=null&&!seen.Add(entry.Block)))continue;
                Rectangle overlap;queries++;
                if(entry.Block.Intersects(box,out overlap)==BlockCollisionType.Collision_Blocking)return true;
            }
            return false;
        }
    }

    /// <summary>prepared cosmetic collision broad phase. exact native bounds are static, foreign blocks are refreshed by each query frame</summary>
    public sealed class ParticleWorld : IDisposable
    {
        internal readonly ParticleGrid Static=new ParticleGrid();
        internal readonly List<IBlock> Dynamic=new List<IBlock>();
        private readonly List<IBlock> source;
        private LevelScreen[] screens;
        internal readonly bool IsNative;
        private readonly IBlock[][] arrays;
        private readonly IBlock[][] entries;
        public bool IsDisposed {get;private set;}
        public bool IsInvalidated {get;private set;}
        public int BlockCount {get{return source.Count;}}
        /// <summary>Prepare a detached block set. Blocks are borrowed and must be pure collision providers</summary>
        public ParticleWorld(IEnumerable<IBlock> blocks):this(blocks,null){}
        internal ParticleWorld(IEnumerable<IBlock> blocks,LevelScreen[] native)
        {
            RuntimeApi.Kernel.CheckThread();if(blocks==null)throw new ArgumentNullException("blocks");source=new List<IBlock>();
            var seen=new HashSet<IBlock>();foreach(var block in blocks)if(block!=null&&seen.Add(block))source.Add(block);
            screens=native;IsNative=native!=null;
            if(native!=null)
            {
                arrays=new IBlock[native.Length][];entries=new IBlock[native.Length][];
                for(int i=0;i<native.Length;i++){arrays[i]=ParticleNative.ReadBlocks(native[i]);entries[i]=arrays[i]==null?null:(IBlock[])arrays[i].Clone();}
            }
            Rebuild();
        }
        /// <summary>rebuild stored bounds explicitly, in a preparation phase. Needed after editing exact native block geometry in place</summary>
        public void Rebuild()
        {
            RuntimeApi.Kernel.CheckThread();if(IsDisposed)throw new ObjectDisposedException("ParticleWorld");Static.Clear();Dynamic.Clear();IsInvalidated=true;
            foreach(var block in source)
            {
                Rectangle bounds;
                if(Geometry.NativeWorldGeometry.TryReadNativeBounds(block,out bounds))Static.Add(block,bounds);
                else Dynamic.Add(block);
            }
            IsInvalidated=false;
        }
        /// <summary>stop using prepared bounds as soon as native geometry changes, live native fallback is still available</summary>
        public void Invalidate(){RuntimeApi.Kernel.CheckThread();IsInvalidated=true;}
        internal bool CanUse(Rectangle region)
        {
            if(IsDisposed||IsInvalidated)return false;if(screens==null)return true;
            if(!ReferenceEquals(screens,ParticleNative.ReadScreens()))return false;
            for(int i=ParticleNative.First(region);i<=ParticleNative.Last(region,screens.Length);i++)
            {
                if(!ReferenceEquals(arrays[i],ParticleNative.ReadBlocks(screens[i])))return false;
                if(arrays[i]!=null)for(int j=0;j<arrays[i].Length;j++)if(!ReferenceEquals(arrays[i][j],entries[i][j]))return false;
            }
            return true;
        }
        internal void AddLive(ParticleGrid grid,Rectangle region)
        {
            foreach(var block in source){var bounds=block.GetRect();var overlap=Rectangle.Intersect(bounds,region);if(overlap.Width>0&&overlap.Height>0)grid.Add(block,overlap);}
        }
        public void Dispose(){RuntimeApi.Kernel.CheckThread();if(IsDisposed)return;Static.Clear();Dynamic.Clear();source.Clear();if(arrays!=null){Array.Clear(arrays,0,arrays.Length);Array.Clear(entries,0,entries.Length);}screens=null;IsDisposed=true;}
    }

    /// <summary>shared native-world preparation. each consumer owns a lease in its OnWorldReady scope</summary>
    public static class ParticleWorlds
    {
        private sealed class Shared
        {internal LevelScreen[] Screens;internal ParticleWorld World;internal int References;}
        private sealed class Lease:IDisposable
        {
            internal Shared Owner;
            public void Dispose(){RuntimeApi.Kernel.CheckThread();if(Owner==null)return;var value=Owner;Owner=null;if(--value.References==0){value.World.Dispose();if(ReferenceEquals(current,value))current=null;}}
        }
        private static Shared current;
        /// <summary>Borrow the prepared native world, or null. the caller must not dispose this value</summary>
        public static ParticleWorld Current {get{RuntimeApi.Kernel.CheckThread();return current==null?null:current.World;}}
        /// <summary>build once for the loaded world, keep in the given scope, and share with other consumers. no player hooks are installed</summary>
        public static ParticleWorld PrepareNative(RuntimeScope scope)
        {
            RuntimeApi.Kernel.CheckThread();if(scope==null)throw new ArgumentNullException("scope");var screens=ParticleNative.ReadScreens();
            if(screens==null)throw new InvalidOperationException("Native world is not loaded");
            if(current==null||!ReferenceEquals(current.Screens,screens))
            {
                using(RuntimeApi.MeasureStartup("runtime.particles.world"))
                {
                    var blocks=new List<IBlock>();foreach(var screen in screens){var array=ParticleNative.ReadBlocks(screen);if(array!=null)blocks.AddRange(array);}
                    current=new Shared{Screens=screens,World=new ParticleWorld(blocks,screens)};
                }
            }
            var shared=current;shared.References++;var lease=new Lease{Owner=shared};
            try{scope.Own(lease);}catch{lease.Dispose();throw;}return shared.World;
        }
    }
}
