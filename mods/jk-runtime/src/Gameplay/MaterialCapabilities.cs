using System;
using System.Collections.Generic;
using JumpKing.Level;
using Microsoft.Xna.Framework;

namespace JKRuntime.Gameplay
{
    public enum SupportKind { Unknown, Unsupported, Supported }
    public enum SpeedCapability { Unknown, Identity, Scaling, Additive, Custom }
    /// <summary>Detached input to a provider's pure predicate. State is an explicitly captured provider token, never a live body or block.</summary>
    public struct SupportQuery
    {
        public Rectangle Body { get; private set; }
        public Rectangle Surface { get; private set; }
        public Vector2 Velocity { get; private set; }
        public bool StartedInside { get; private set; }
        public long State { get; private set; }
        public SupportQuery(Rectangle body,Rectangle surface,Vector2 velocity,bool startedInside,long state) : this()
        { Body=body; Surface=surface; Velocity=velocity; StartedInside=startedInside; State=state; }
    }
    public delegate SupportKind SupportPredicate(SupportQuery query);
    /// <summary>Caller-owned per-operation budget. Share one instance across candidates; exhaustion remains Unknown.</summary>
    public struct SupportBudget
    {
        public int Remaining { get; private set; }
        public SupportBudget(int maximum) : this() { if(maximum<1 || maximum>4096)throw new ArgumentOutOfRangeException("maximum"); Remaining=maximum; }
        internal bool Take() { if(Remaining==0)return false; Remaining--; return true; }
    }
    /// <summary>Provider declarations, not inferred simulation coverage. IDs link to existing geometry and state services without registering them.</summary>
    public sealed class MaterialCapabilities
    {
        public Type BlockType { get; private set; }
        public SpeedCapability Speed { get; private set; }
        public string GeometryProfile { get; private set; }
        public string StateId { get; private set; }
        public SupportPredicate Support { get; private set; }
        public Func<IBlock,long> CaptureState { get; private set; }
        public MaterialCapabilities(Type blockType,SpeedCapability speed,SupportPredicate support=null,string geometryProfile=null,string stateId=null,Func<IBlock,long> captureState=null)
        {
            if(blockType==null || !typeof(IBlock).IsAssignableFrom(blockType) || blockType.ContainsGenericParameters || !Enum.IsDefined(typeof(SpeedCapability),speed))throw new ArgumentException("Invalid material declaration");
            if(geometryProfile!=null)ModuleDefinition.ValidId(geometryProfile);
            if(stateId!=null)ModuleDefinition.ValidId(stateId);
            BlockType=blockType; Speed=speed; Support=support; CaptureState=captureState; GeometryProfile=geometryProfile; StateId=stateId;
        }
    }
    /// <summary>Detached declaration inventory. Does not execute capture or support delegates.</summary>
    public sealed class MaterialInfo
    {
        public string Owner { get; internal set; }
        public MaterialCapabilities Capabilities { get; internal set; }
    }
    /// <summary>Scoped, exact-type material declarations. Queries never call native collision handlers and do not certify purity of provider code.</summary>
    public sealed class MaterialRegistry
    {
        private readonly Dictionary<Type,MaterialCapabilities> entries=new Dictionary<Type,MaterialCapabilities>();
        private readonly Dictionary<Type,string> owners=new Dictionary<Type,string>();
        private bool querying;
        public long Generation { get; private set; }
        public IDisposable Register(string owner,MaterialCapabilities declaration)
        {
            CheckMutation(); ModuleDefinition.ValidId(owner);
            if(declaration==null)throw new ArgumentNullException("declaration");
            if(entries.ContainsKey(declaration.BlockType))throw new InvalidOperationException("Material type already owned");
            entries.Add(declaration.BlockType,declaration); owners.Add(declaration.BlockType,owner); Generation++;
            return RuntimeResources.Track(owner,"material:"+declaration.BlockType.FullName,new ActionLease(delegate { CheckMutation(); if(entries.Remove(declaration.BlockType)) { owners.Remove(declaration.BlockType); Generation++; } }));
        }
        /// <summary>Allocate an on-demand declaration inventory for browsers or diagnostics. Never invoke this from Update.</summary>
        public MaterialInfo[] Inspect()
        {
            RuntimeApi.Kernel.CheckThread(); var result=new MaterialInfo[entries.Count]; int index=0;
            foreach(var pair in entries)result[index++]=new MaterialInfo {Owner=owners[pair.Key],Capabilities=pair.Value};
            return result;
        }
        /// <summary>Constant-time lookup. Null means undeclared; consumers must not infer properties from missing declarations.</summary>
        public MaterialCapabilities Resolve(Type blockType)
        { RuntimeApi.Kernel.CheckThread(); if(blockType==null)throw new ArgumentNullException("blockType"); MaterialCapabilities value; return entries.TryGetValue(blockType,out value)?value:null; }
        /// <summary>Run a declared pure predicate once. Reentry, exhaustion, invalid data, exceptions and missing coverage return Unknown.</summary>
        public SupportKind QuerySupport(Type blockType,SupportQuery query,ref SupportBudget budget)
        {
            RuntimeApi.Kernel.CheckThread();
            if(querying || !budget.Take())return SupportKind.Unknown;
            var entry=Resolve(blockType);
            if(entry==null || entry.Support==null || query.Body.Width<=0 || query.Body.Height<=0 || query.Surface.Width<=0 || query.Surface.Height<=0 || !Finite(query.Velocity.X) || !Finite(query.Velocity.Y))return SupportKind.Unknown;
            querying=true;
            try { var result=entry.Support(query); return result==SupportKind.Supported || result==SupportKind.Unsupported?result:SupportKind.Unknown; }
            catch { return SupportKind.Unknown; }
            finally { querying=false; }
        }
        /// <summary>Capture a declared material token once, then run its pure predicate. The capture delegate must be read-only and bounded; defaults to token zero.</summary>
        public SupportKind QuerySupport(IBlock block,Rectangle body,Vector2 velocity,bool startedInside,ref SupportBudget budget)
        {
            RuntimeApi.Kernel.CheckThread();
            if(block==null || querying || !budget.Take())return SupportKind.Unknown;
            var entry=Resolve(block.GetType()); if(entry==null || entry.Support==null)return SupportKind.Unknown;
            querying=true;
            SupportQuery query;
            try { query=new SupportQuery(body,block.GetRect(),velocity,startedInside,entry.CaptureState==null?0:entry.CaptureState(block)); }
            catch { return SupportKind.Unknown; }
            finally { querying=false; }
            // Capture and predicate share one public query budget unit.
            var evaluation=new SupportBudget(1); return QuerySupport(block.GetType(),query,ref evaluation);
        }
        private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
        private void CheckMutation() { RuntimeApi.Kernel.CheckThread(); if(querying)throw new InvalidOperationException("Material registry mutation during query"); }
    }
    /// <summary>Reusable pure predicates. Callers capture provider-specific enable/direction/overlap rules into State and StartedInside.</summary>
    public static class SupportPredicates
    {
        /// <summary>State 1 enables the top face. Reject upward motion, starting inside, nonhorizontal contact and absent horizontal overlap.</summary>
        public static SupportKind TopFace(SupportQuery query)
        {
            return query.State==1 && !query.StartedInside && query.Velocity.Y>=0 && query.Body.Bottom==query.Surface.Top && query.Body.Right>query.Surface.Left && query.Body.Left<query.Surface.Right
                ?SupportKind.Supported:SupportKind.Unsupported;
        }
    }
}
