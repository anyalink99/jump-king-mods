using System.Reflection;
using JKRuntime.Simulation;
using JumpKing.Player;
using JumpKing.BodyCompBehaviours;

namespace NoWalkOff
{
    // native motion bookkeeping has no public field access
    internal static class NativeBody
    {
        internal const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        internal static T Get<T>(object target, string field) { return (T)target.GetType().GetField(field, Fields).GetValue(target); }
        internal static void Set(object target, string field, object value) { target.GetType().GetField(field, Fields).SetValue(target, value); }
    }
}
