using System;
using System.Collections.Generic;
using JKRuntime.Simulation;

namespace ScreenSolver
{
    internal static class NativeActions
    {
        // Only for a native, static world with no wind or timed provider. The
        // first eight key bytes are the clock; every pose/state byte is retained.
        internal static string StaticIdentity(SimulationSnapshot state)
        { var bytes = Convert.FromBase64String(state.Key); return Convert.ToBase64String(bytes, 8, bytes.Length - 8); }
        internal static IEnumerable<SearchAction> Expand(SimulationSnapshot state)
        {
            var m = NativeMemory.Decode(state.Read(NativePlayer.Id));
            if (!state.Pose.Grounded && !m.Sand)
            {
                yield return new SearchAction("Fall / settle", Repeat(0, false, 240), true);
                yield break;
            }
            if (m.Splat)
            {
                yield return new SearchAction("Recover", Repeat(1, false, 36));
                yield break;
            }
            // Every integer native hold, with release as a separate tick.
            // Start with high jumps to find useful progress early, without
            // removing shorter jumps from the finite action set.
            int maximum = m.Water ? 72 : 36;
            for (int hold = maximum; hold >= 1; hold--)
                foreach (int direction in new[] { -1, 1, 0 })
                {
                    var frames = new List<SimulationInput>();
                    frames.AddRange(Repeat(direction, true, hold));
                    frames.AddRange(Repeat(0, false, 240));
                    yield return new SearchAction((direction < 0 ? "Left" : direction > 0 ? "Right" : "Up") +
                        (m.Subframe ? " SFC " + (hold * m.InputClock * 1000).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "ms" : " hold " + hold + "t"), frames, true);
                }
            foreach (int length in new[] { 1, 2, 4, 8, 16, 32, 64 })
                foreach (int direction in new[] { -1, 1 })
                    yield return new SearchAction((direction < 0 ? "Walk left " : "Walk right ") + length + "t", Repeat(direction, false, length));
            foreach (int length in new[] { 1, 8, 30, 60, 120 })
                yield return new SearchAction("Wait " + length + "t", Repeat(0, false, length));
        }
        private static IEnumerable<SimulationInput> Repeat(int direction, bool jump, int ticks)
        { for (int i = 0; i < ticks; i++) yield return new SimulationInput(direction, jump); }
    }
}
