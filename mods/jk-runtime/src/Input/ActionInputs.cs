using System;
using System.Collections.Generic;
using JumpKing.Controller;

namespace JKRuntime.Input
{
    public struct ActionFrame
    {
        public readonly long Tick;
        public readonly bool Down, Pressed, Released;
        internal ActionFrame(long tick, bool down, bool pressed, bool released)
        { Tick = tick; Down = down; Pressed = pressed; Released = released; }
    }
    /// <summary>Logical frame actions and shared physical evidence are separate views of the same action ID.</summary>
    public static class ActionInputs
    {
        private sealed class ActionEntry { internal Func<bool> Read; internal ActionFrame Frame; internal long Tick = -1; internal bool Known, Reading; }
        private static readonly Dictionary<string, ActionEntry> actions = new Dictionary<string, ActionEntry>(StringComparer.Ordinal) {
            { "native.jump", new ActionEntry { Read = () => ControllerManager.instance != null && ControllerManager.instance.GetPadState().jump } }
        };
        private static long tick;
        internal static void BeginTick() { tick++; }
        internal static void BeginLevel() { tick++; foreach (var entry in actions.Values) { entry.Known = false; entry.Tick = -1; } }
        public static IDisposable Register(string owner, string id, Func<bool> readCachedFrame)
        {
            RuntimeApi.Kernel.CheckThread(); ModuleDefinition.ValidId(owner); ModuleDefinition.ValidId(id);
            if (readCachedFrame == null || actions.ContainsKey(id)) throw new ArgumentException("Action ID already owned or reader missing");
            var entry = new ActionEntry { Read = readCachedFrame }; actions.Add(id, entry);
            return RuntimeResources.Track(owner, "frame-action:" + id, new ActionLease(delegate { RuntimeApi.Kernel.CheckThread(); actions.Remove(id); }));
        }
        /// <summary>Read once per gameplay tick from the cached logical binding. Initial held state is not a press; not a physical timestamp.</summary>
        public static ActionFrame Read(string id)
        {
            RuntimeApi.Kernel.CheckThread();
            ActionEntry entry;
            if (!actions.TryGetValue(id, out entry)) throw new KeyNotFoundException("Unknown action: " + id);
            if (entry.Tick != tick)
            {
                if (entry.Reading) throw new InvalidOperationException("Recursive frame action read: " + id);
                entry.Reading = true;
                try {
                    bool down = entry.Read();
                    entry.Frame = new ActionFrame(tick, down, entry.Known && down && !entry.Frame.Down, entry.Known && !down && entry.Frame.Down);
                    entry.Known = true; entry.Tick = tick;
                } finally { entry.Reading = false; }
            }
            return entry.Frame;
        }
    }
}
