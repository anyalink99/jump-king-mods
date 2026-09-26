using System;
using System.Collections;
using System.Reflection;
using JKRuntime.Simulation;
using JumpKing.Level;

namespace ScreenSolver
{
    internal struct CustomWindProfile
    {
        internal bool Enabled;
        internal float LeftTime, RightTime, LeftIntensity, RightIntensity;
        internal static CustomWindProfile[] Capture(Assembly assembly, int count)
        {
            var profiles = new CustomWindProfile[count];
            var api = assembly.GetType("CustomWindSwitch.CustomWindSwitchApi", true);
            var instance = api.GetField("_instance", NativeWorld.Flags).GetValue(null);
            if (instance == null) return profiles;
            var source = (IDictionary)api.GetField("_freqs", NativeWorld.Flags).GetValue(instance);
            foreach (DictionaryEntry entry in source)
            {
                int screen = (int)entry.Key; var value = entry.Value; var type = value.GetType();
                if (screen < 0 || screen >= count) throw new NotSupportedException("Custom Wind Switch screen index is outside the map");
                var profile = new CustomWindProfile { Enabled = true,
                    LeftTime = (float)type.GetField("LeftWindTime").GetValue(value), RightTime = (float)type.GetField("RightWindTime").GetValue(value),
                    LeftIntensity = (float)type.GetField("LeftWindIntensity").GetValue(value), RightIntensity = (float)type.GetField("RightWindIntensity").GetValue(value) };
                foreach (float number in new[] { profile.LeftTime, profile.RightTime, profile.LeftIntensity, profile.RightIntensity })
                    if (float.IsNaN(number) || float.IsInfinity(number)) throw new NotSupportedException("Invalid Custom Wind Switch parameters");
                if (profile.LeftTime <= 0 || profile.RightTime <= 0 || float.IsInfinity(profile.LeftTime + profile.RightTime))
                    throw new NotSupportedException("Custom Wind Switch durations must be finite and positive");
                profiles[screen] = profile;
            }
            return profiles;
        }
        internal float Velocity(long tick, double clock, LevelScreen screen)
        {
            if (!Enabled) return NativeWind.Velocity(tick, clock, 0, screen.WindEndabled, screen.WindIntensity, screen.WindDirection);
            if (!screen.WindEndabled) return 0;
            // Preserve the installed transpiler's float operations, order and
            // fixed-direction exception, including its asymmetric phase formula.
            float time = (float)TimeSpan.FromSeconds(tick * clock).TotalSeconds;
            float cycle = LeftTime + RightTime;
            float phase = time % cycle <= LeftTime ? ((float)Math.PI / LeftTime) * (time % cycle) :
                ((float)Math.PI / RightTime) * ((time - LeftTime) % cycle + RightTime);
            float wave = (float)Math.Sin(phase);
            wave = (float)Math.Cos(phase) > 0 ? wave * 2 + 1 : wave * 2 - 1;
            wave = Math.Max(-1, Math.Min(1, wave));
            float multiplier = wave >= 0 ? RightIntensity : LeftIntensity;
            if (screen.WindIntensity != 0)
            {
                if (screen.WindDirection.HasValue) return screen.WindDirection.Value ? -0.0125f * screen.WindIntensity : 0.0125f * screen.WindIntensity;
                return (wave * multiplier) * (0.0125f * screen.WindIntensity);
            }
            return (wave * multiplier) * 0.1f;
        }
    }
}
