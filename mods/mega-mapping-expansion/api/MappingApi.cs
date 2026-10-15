using System;
using System.Reflection;

[assembly: AssemblyVersion("2.0.0.0")]

namespace MegaMappingExpansion.Api
{
    // native timing stays separate from the corrected launch value
    public enum ChargePhase { Idle, Charging, Released, Cancelled }
    public sealed class ChargeSample
    {
        public long Sequence {get;set;}
        public ChargePhase Phase {get;set;}
        public bool Paused {get;set;}
        public double ElapsedMilliseconds {get;set;}
        public double HeldFrames {get;set;}
        public float NativeTimerSeconds {get;set;}
        public float? ReleasedFrames {get;set;}
        public string Provider {get;set;}
    }
    /// <summary>Opt in a factory that respects the supplied rectangles and sampler coordinates at every collision resolution.</summary>
    public static class PixelCollisionFactories
    {
        private static readonly System.Collections.Generic.HashSet<Type> types=new System.Collections.Generic.HashSet<Type>();
        public static bool Register(Type factoryType)
        {
            if(factoryType==null||factoryType.GetInterface("JumpKing.API.IBlockFactory")==null)throw new ArgumentException("Expected an IBlockFactory type");
            lock(types)return types.Add(factoryType);
        }
        public static bool Supports(Type factoryType){lock(types)return types.Contains(factoryType);}
    }
    /// <summary>An effect lease belongs to one scene generation. Disposal never cancels another generation's effect.</summary>
    public interface ISceneEffect:IDisposable
    {
        long Id{get;}
        bool Active{get;}
        double RemainingSeconds{get;}
    }
    public sealed class EffectInfo
    {
        public long Id{get;private set;}
        public string Definition{get;private set;}
        public string Owner{get;private set;}
        public double RemainingSeconds{get;private set;}
        public EffectInfo(long id,string definition,string owner,double remaining){Id=id;Definition=definition;Owner=owner;RemainingSeconds=remaining;}
    }
}