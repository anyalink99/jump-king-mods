using System.Reflection;
using JKRuntime.Simulation;
using JumpKing.Player;
using JumpKing.BodyCompBehaviours;

namespace MegaGameplayExpansion
{
    // Feature-side state/commit contract; the shared adapter never uses ball contours.
    internal static class NativeFlight
    {
        internal const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        internal static T Get<T>(object target, string field) { return (T)target.GetType().GetField(field, Fields).GetValue(target); }
        internal static void Set(object target, string field, object value) { target.GetType().GetField(field, Fields).SetValue(target, value); }
        internal static void Validate() { NativeFlightSimulation.Validate(); }
        internal static BodyComp CreateShadow(BodyComp source, FlightWorld world) { return NativeFlightSimulation.CreateShadow(source, world); }
        internal static void CopyState(BodyComp from, BodyComp to) { NativeFlightSimulation.CopyState(from, to); }
        internal static bool TryPredict(BodyComp source, FlightWorld world, out BodyComp landing, out int ticks, out string reason)
        { return NativeFlightSimulation.TryPredict(source, world, out landing, out ticks, out reason); }
        internal static void Commit(BodyComp landing, BodyComp real, BehaviourContext context) { NativeFlightSimulation.Commit(landing, real, context); }
    }
}
