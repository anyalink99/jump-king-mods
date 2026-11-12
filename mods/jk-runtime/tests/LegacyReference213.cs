// frozen subset of API 2.13, checked against its full signature baseline
// only a compiler reference; replaced before the consumer runs
using System;
[assembly:System.Reflection.AssemblyVersion("1.0.0.0")]
namespace JKRuntime.World
{
    public sealed class WorldRegistry
    {
        public IDisposable Register(string id,ulong schema,Func<byte[]> capture,Func<byte[],Action> prepare){throw new NotImplementedException();}
        public byte[] Capture(){throw new NotImplementedException();}
        public void Apply(byte[] bytes){throw new NotImplementedException();}
    }
}
namespace JKRuntime.Gameplay
{
    public sealed class MaterialRegistry
    {
        public long Generation{get{throw new NotImplementedException();}}
    }
}
namespace JKRuntime.Input
{
    public static class NativeInputFrames
    {
        public static void Publish(JumpKing.Controller.PadInstance pad,JumpKing.Controller.PadState held,JumpKing.Controller.PadState pressed){throw new NotImplementedException();}
    }
}
namespace JKRuntime.Presentation
{
    public static class ActorVisuals
    {
        public static bool Valid(byte[] bytes){throw new NotImplementedException();}
    }
}
namespace JKRuntime.State
{
    public sealed class SnapshotService
    {
        public long RestoreEpoch{get{throw new NotImplementedException();}}
    }
}
