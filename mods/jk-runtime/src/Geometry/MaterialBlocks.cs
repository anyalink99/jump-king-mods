using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using JumpKing.Level;
using JumpKing.API;
using JumpKing.Level.Sampler;
using JumpKing.Player;
using Microsoft.Xna.Framework;

namespace JKRuntime.Geometry
{
    /// <summary>Provider-owned geometry for blocks whose state cannot be copied as a rectangle.</summary>
    public interface IMaterialBlockGeometry
    {
        bool CanCopy(IBlock block,out string reason);
        IBlock Copy(IBlock block,Rectangle bounds);
        Action<Rectangle> PrepareMove(IBlock block);
    }
    /// <summary>Shared, exact-type material geometry used by map authoring and the inspector.</summary>
    public static class MaterialBlocks
    {
        private sealed class Identity { internal string Id; internal Type Factory;internal Color Color; }
        private static readonly ConditionalWeakTable<IBlock,Identity> identities=new ConditionalWeakTable<IBlock,Identity>();
        private static readonly Dictionary<Type,FieldInfo[]> rectangles=new Dictionary<Type,FieldInfo[]>();
        private static readonly Dictionary<Type,IMaterialBlockGeometry> adapters=new Dictionary<Type,IMaterialBlockGeometry>();

        /// <summary>Registers a factory's copy and relocation contract before map loading. Repeat registration must use the same adapter.</summary>
        public static void RegisterFactory(Type factory,IMaterialBlockGeometry geometry)
        {
            RuntimeApi.Kernel.CheckThread();if(factory==null||!typeof(IBlockFactory).IsAssignableFrom(factory)||geometry==null)throw new ArgumentException("A block factory type and geometry adapter are required");
            IMaterialBlockGeometry previous;if(adapters.TryGetValue(factory,out previous)&&!ReferenceEquals(previous,geometry))throw new InvalidOperationException("Material factory adapter already registered");adapters[factory]=geometry;
        }
        private static IMaterialBlockGeometry Adapter(IBlock block)
        {Identity data;IMaterialBlockGeometry adapter;return block!=null&&identities.TryGetValue(block,out data)&&data.Factory!=null&&adapters.TryGetValue(data.Factory,out adapter)?adapter:null;}

        /// <summary>Checks the inspector's detached rectangular-copy contract without constructing a provider.</summary>
        public static bool CanCopy(IBlock block,out string reason)
        { RuntimeApi.Kernel.CheckThread();var adapter=Adapter(block);return adapter==null?Inspection.GimmickBlocks.CanCopy(block,out reason):adapter.CanCopy(block,out reason); }

        /// <summary>Checks the current factory path without executing writes into provider state.</summary>
        public static void ValidateDetached(IBlockFactory factory,Color color,Rectangle bounds,JumpKing.Workshop.Level level,LevelTexture texture,int screen,int x,int y,int cellSize=8)
        {
            RuntimeApi.Kernel.CheckThread();
            if(adapters.ContainsKey(factory.GetType()))return;
            var map=factory.GetType().GetInterfaceMap(typeof(IBlockFactory));
            var method=map.TargetMethods[Array.FindIndex(map.InterfaceMethods,m=>m.Name=="GetBlock")];
            var construction=new Inspection.GimmickConstruction(factory.GetType().Assembly,true);
            // probing a different position also catches registries that already contain the original
            if(cellSize<1||cellSize>480)throw new ArgumentOutOfRangeException("cellSize");bounds.Offset(cellSize,cellSize);
            var sample=construction.RunDetached(method,factory,color,bounds,level,texture,screen,x+1,y+1) as IBlock;
            string reason;if(!CanCopy(sample,out reason))throw new NotSupportedException(reason);
        }

        /// <summary>Prepares discovered native handler recipes before player activation.</summary>
        public static void PrepareHandler(Type block){RuntimeApi.Kernel.CheckThread();Inspection.GimmickHandlers.Prepare(block);}

        /// <summary>Reuses the player's native handler, or owns a bounded discovered registration.</summary>
        public static IDisposable AcquireHandler(PlayerEntity player,Type block)
        {RuntimeApi.Kernel.CheckThread();return Inspection.GimmickHandlers.Acquire(player,block);}

        /// <summary>Copies scalar parameters and the exact block type; source geometry is unchanged.</summary>
        public static IBlock Copy(IBlock block,Rectangle bounds)
        {
            RuntimeApi.Kernel.CheckThread();
            if(bounds.Width<=0||bounds.Height<=0)throw new ArgumentException("Material bounds must be positive");
            var adapter=Adapter(block);var copy=adapter==null?Inspection.GimmickBlocks.Copy(block,bounds):adapter.Copy(block,bounds);Identity data;
            if(copy==null||ReferenceEquals(copy,block)||copy.GetType()!=block.GetType()||copy.GetRect()!=bounds)throw new InvalidOperationException("Material copy must be a distinct instance of the exact source type at the requested bounds");
            if(identities.TryGetValue(block,out data))identities.Add(copy,new Identity{Factory=data.Factory,Color=data.Color});return copy;
        }

        /// <summary>Prepares relocation once. The returned action only updates the detached collider's stored geometry.</summary>
        public static Action<Rectangle> PrepareMove(IBlock block)
        {
            RuntimeApi.Kernel.CheckThread();string reason;
            if(!CanCopy(block,out reason))throw new NotSupportedException(reason);
            var adapter=Adapter(block);
            if(adapter!=null){var move=adapter.PrepareMove(block);if(move==null)throw new InvalidOperationException("Material adapter did not provide relocation");return bounds=>{RuntimeApi.Kernel.CheckThread();move(bounds);if(block.GetRect()!=bounds)throw new InvalidOperationException("Material adapter did not relocate its collider");};}
            if(block.GetType()==typeof(SlopeBlock))
            {
                var box=typeof(SlopeBlock).GetField("m_box",BindingFlags.Instance|BindingFlags.NonPublic);
                var lines=typeof(SlopeBlock).GetField("m_lines",BindingFlags.Instance|BindingFlags.NonPublic);
                var origin=block.GetRect();var baseline=(ErikMaths.Line[])((ErikMaths.Line[])lines.GetValue(block)).Clone();
                var current=(ErikMaths.Line[])lines.GetValue(block);
                return bounds=>{
                    RuntimeApi.Kernel.CheckThread();
                    if(bounds.Width!=origin.Width||bounds.Height!=origin.Height)throw new ArgumentException("Moving slope dimensions must remain unchanged");
                    int x=bounds.X-origin.X,y=bounds.Y-origin.Y;
                    for(int i=0;i<current.Length;i++){current[i]=baseline[i];current[i].p0=new Point(baseline[i].p0.X+x,baseline[i].p0.Y+y);current[i].p1=new Point(baseline[i].p1.X+x,baseline[i].p1.Y+y);}
                    box.SetValue(block,bounds);
                };
            }
            FieldInfo[] fields;
            if(!rectangles.TryGetValue(block.GetType(),out fields))
            {
                var found=new List<FieldInfo>();
                for(Type type=block.GetType();type!=null&&type!=typeof(object);type=type.BaseType)
                    found.AddRange(type.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly).Where(f=>f.FieldType==typeof(Rectangle)));
                rectangles.Add(block.GetType(),fields=found.ToArray());
            }
            return bounds=>{
                RuntimeApi.Kernel.CheckThread();
                if(bounds.Width<=0||bounds.Height<=0)throw new ArgumentException("Material bounds must be positive");
                foreach(var field in fields)field.SetValue(block,bounds);
            };
        }

        /// <summary>Assigns a stable authored identity, independent of a moving block's coordinates.</summary>
        public static void Identify(IBlock block,string id)
        {
            RuntimeApi.Kernel.CheckThread();
            if(block==null||string.IsNullOrWhiteSpace(id))throw new ArgumentException("A block and nonempty identity are required");
            identities.GetValue(block,b=>new Identity()).Id=id;
        }

        /// <summary>Returns null for geometry without an authored identity.</summary>
        public static string IdentityOf(IBlock block)
        { Identity identity;return block!=null&&identities.TryGetValue(block,out identity)?identity.Id:null; }

        internal static void Remember(IBlock block,Type factory,Color color)
        {if(block==null||block.GetType().IsValueType)return;var data=identities.GetValue(block,b=>new Identity());data.Factory=factory;data.Color=color;}

        /// <summary>Reads observed factory provenance, without invoking a provider or guessing its palette.</summary>
        public static bool TryGetSource(IBlock block,out Type factory,out Color color)
        {
            Identity data;if(block!=null&&identities.TryGetValue(block,out data)&&data.Factory!=null){factory=data.Factory;color=data.Color;return true;}
            factory=null;color=default(Color);return false;
        }
    }
}
