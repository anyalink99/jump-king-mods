using System;
using System.Collections.Generic;

namespace Prism
{
    internal struct WindFrame { internal float Velocity; internal double Offset; }
    internal sealed class WindPresentation
    {
        private sealed class State { internal double Stamp, Offset; }
        private readonly Dictionary<int, State> screens = new Dictionary<int, State>();
        private double time;
        internal void Register(int screen) { screens.Add(screen, new State()); }
        internal void Advance(double delta) { if (delta > 0 && !double.IsInfinity(delta)) time += Math.Min(.1, delta); }
        internal WindFrame Sample(int screen, float velocity)
        {
            if (float.IsNaN(velocity) || float.IsInfinity(velocity)) velocity = 0;
            State state; if (!screens.TryGetValue(screen, out state)) return new WindFrame { Velocity = velocity };
            // The native sample supplies direction. Audio time never decides the wind.
            state.Offset += velocity * Math.Min(.1, Math.Max(0, time - state.Stamp)) * 520;
            state.Stamp = time;
            return new WindFrame { Velocity = velocity, Offset = state.Offset };
        }
        internal void Reset() { time = 0; foreach (var s in screens.Values) { s.Stamp = s.Offset = 0; } }
    }
}
