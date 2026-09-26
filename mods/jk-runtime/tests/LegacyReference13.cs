// Frozen reference-only subset of the shipped 1.3 public contract.
// Never run this assembly: the compatibility test replaces it with today's Runtime.
using System;
[assembly: System.Reflection.AssemblyVersion("1.0.0.0")]
namespace JKRuntime
{
    public sealed class RuntimeScope : IDisposable
    {
        public IDisposable Defer(Action action) { throw new NotImplementedException(); }
        public void Dispose() { throw new NotImplementedException(); }
    }
    public static class RuntimeApi
    {
        public static bool Supports(string feature) { throw new NotImplementedException(); }
        public static Gameplay.MechanicRegistry Mechanics { get { throw new NotImplementedException(); } }
    }
}
namespace JKRuntime.Gameplay
{
    [Flags] public enum MechanicEffects { None = 0, Input = 1, Charge = 2, Movement = 4, Collision = 8, Presentation = 16, Time = 32 }
    public enum MechanicSource { Setting, Equipment, Surface, Zone, Screen, Controller }
    public sealed class MechanicState
    {
        public bool Active { get { throw new NotImplementedException(); } }
        public MechanicState(bool enabled, bool available, bool active, MechanicSource source, string reason) { throw new NotImplementedException(); }
    }
    public sealed class MechanicInfo
    {
        public string Id { get { throw new NotImplementedException(); } }
        public MechanicState State { get { throw new NotImplementedException(); } }
    }
    public sealed class MechanicRegistry
    {
        public IDisposable Register(string owner, string id, Version version, MechanicEffects effects, Func<MechanicState> read) { throw new NotImplementedException(); }
        public MechanicInfo[] Inspect() { throw new NotImplementedException(); }
    }
}
