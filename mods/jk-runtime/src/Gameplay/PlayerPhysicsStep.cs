using System;
using JumpKing.Player;

namespace JKRuntime.Gameplay
{
    /// <summary>Current fractional native body step. Velocities remain in native pixels per tick.</summary>
    public static class PlayerPhysicsStep
    {
        private static BodyComp body;
        private static float fraction = 1;
        public static float Fraction(BodyComp target) { return ReferenceEquals(target, body) ? fraction : 1; }
        public static Scope Enter(BodyComp target, float value)
        {
            RuntimeApi.Kernel.CheckThread();
            if (target == null || value <= 0 || value > 1 || float.IsNaN(value)) throw new ArgumentOutOfRangeException("value");
            var scope = new Scope(body, fraction); body = target; fraction = value; return scope;
        }
        public struct Scope : IDisposable
        {
            private readonly BodyComp previous;
            private readonly float value;
            internal Scope(BodyComp target, float scale) { previous = target; value = scale; }
            public void Dispose() { body = previous; fraction = value; }
        }
    }
}
