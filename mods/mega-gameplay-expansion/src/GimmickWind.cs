using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JumpKing;
using JumpKing.Level;
using JumpKing.Player;
using JumpKing.BodyCompBehaviours;

namespace MegaGameplayExpansion
{
    internal sealed class GimmickWind : IDisposable
    {
        internal const string Id = "native.wind";
        internal static readonly string[] Patterns = { "Alternating", "Left", "Right", "Disabled" };
        private static readonly FieldInfo Flag = typeof(LevelScreen).GetField("m_wind_enabled", Gimmicks.Members);
        private static readonly FieldInfo Strength = typeof(LevelScreen).GetField("m_wind_intensity", Gimmicks.Members);
        private static readonly FieldInfo Direction = typeof(LevelScreen).GetField("m_wind_direction", Gimmicks.Members);
        private static readonly FieldInfo Latch = typeof(WindVelocityUpdateBehaviour).GetField("m_wind_enabled", Gimmicks.Members);
        private readonly List<Tuple<LevelScreen, object, object, object>> originals = new List<Tuple<LevelScreen, object, object, object>>();
        private readonly GimmickRule rule;
        private readonly object behaviour;
        private bool latchHeld, previousLatch, disposed;
        private readonly bool enabled;
        private readonly float intensity;
        private readonly bool? direction;
        internal static void AddEntry()
        {
            Gimmicks.Add(new GimmickEntry { Id = Id, Label = "Wind", Owner = "Jump King", Kind = "Wind", Family = "Environment", Geometry = "Metadata", Trigger = "Screen",
                Detail = "Native wind: alternating / left / right, strength and screen scope. Snow and NoWind suppression remain native." });
        }
        internal static void Validate(GimmickRule rule)
        {
            if (Flag == null || Strength == null || Direction == null || Latch == null) throw new InvalidOperationException("Native wind contract is unavailable.");
            if (!Patterns.Contains(rule.Value) || float.IsNaN(rule.WindStrength) || float.IsInfinity(rule.WindStrength) || rule.WindStrength < 0 || rule.WindStrength > 8)
                throw new InvalidOperationException("Choose a wind pattern and strength between 0x and 8x.");
        }
        internal static bool Owns(FieldInfo field) { return field != null && (field == Flag || field == Strength || field == Direction); }
        internal static bool SourceColour(Microsoft.Xna.Framework.Color colour)
        { return colour.G == 255 && colour.B == 0; }
        internal static string Status(LevelScreen[] screens, PlayerEntity player, GimmickRule rule)
        {
            if (!GimmickSession.InRange(rule, Camera.CurrentScreen)) return "Outside target screens";
            if (rule.Value == "Disabled" || rule.WindStrength == 0) return "Native wind forced off";
            var screen = screens[Camera.CurrentScreen];
            if (!screen.WindEndabled) return "Native wind flag changed by another owner";
            var snow = (JumpKing.API.IBlockBehaviour)typeof(JumpKing.Player.BodyComp).GetMethod("GetBlockBehaviour", Gimmicks.Members).Invoke(player.m_body, new object[] { typeof(SnowBlock) });
            if (snow != null && snow.IsPlayerOnBlock) return "Suppressed by native Snow";
            if (!screen.IsWindOnPosition(player.m_body.GetHitbox())) return "Suppressed by native NoWind volume";
            var behavior = player.m_body.GetBehaviourList().FirstOrDefault(b => b is WindVelocityUpdateBehaviour);
            if (behavior == null) return "No native wind behavior";
            return (bool)Latch.GetValue(behavior) ? "Native wind gates active" : rule.WindImmediate ? "Will activate on next gameplay tick" : "Waiting for native entry / grounding";
        }
        internal GimmickWind(LevelScreen[] screens, PlayerEntity player, GimmickRule value)
        {
            Validate(value); rule = Gimmicks.Copy(value);
            enabled = rule.Value != "Disabled" && rule.WindStrength > 0;
            intensity = rule.WindStrength * 8f;
            direction = rule.Value == "Left" ? (bool?)true : rule.Value == "Right" ? (bool?)false : null;
            behaviour = player.m_body.GetBehaviourList().FirstOrDefault(b => b is WindVelocityUpdateBehaviour);
            if (enabled && rule.WindImmediate && behaviour == null) throw new InvalidOperationException("The active controller has no native wind behavior. Choose native activation timing or use its own wind control.");
            try {
                for (int i = 0; i < screens.Length; i++) if (GimmickSession.InRange(rule, i)) {
                    originals.Add(Tuple.Create(screens[i], Flag.GetValue(screens[i]), Strength.GetValue(screens[i]), Direction.GetValue(screens[i])));
                    Flag.SetValue(screens[i], enabled); Strength.SetValue(screens[i], intensity); Direction.SetValue(screens[i], direction);
                }
            } catch { Dispose(); throw; }
        }
        internal void Tick()
        {
            if (disposed) return;
            bool hold = enabled && rule.WindImmediate && GimmickSession.InRange(rule, Camera.CurrentScreen);
            if (hold && !latchHeld) { previousLatch = (bool)Latch.GetValue(behaviour); latchHeld = true; }
            if (hold) Latch.SetValue(behaviour, true);
            else ReleaseLatch();
        }
        private void ReleaseLatch()
        { if (latchHeld) { if ((bool)Latch.GetValue(behaviour)) Latch.SetValue(behaviour, previousLatch); latchHeld = false; } }
        public void Dispose()
        {
            if (disposed) return; ReleaseLatch();
            foreach (var old in originals) {
                if (Equals(Flag.GetValue(old.Item1), enabled)) Flag.SetValue(old.Item1, old.Item2);
                if (Equals(Strength.GetValue(old.Item1), intensity)) Strength.SetValue(old.Item1, old.Item3);
                if (Equals(Direction.GetValue(old.Item1), direction)) Direction.SetValue(old.Item1, old.Item4);
            }
            originals.Clear(); disposed = true;
        }
    }
}
