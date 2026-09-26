using System;
using JumpKing.Controller;
using JKRuntime.UI;

namespace JKRuntime.Input
{
    public static class PhysicalBindings
    {
        public static int Epoch { get; private set; }
        public static void Invalidate() { Epoch++; }
        public static int[][] ResolvePhysicalBinding(PadInstance pad, int[] buttons)
        { return UIApi.ResolvePhysicalBinding(pad, buttons); }
    }

    public static class InputLog
    {
        private static event Action<string> listeners;
        public static IDisposable Subscribe(Action<string> sink)
        {
            if (sink == null) throw new ArgumentNullException("sink");
            listeners += sink;
            return new ActionLease(delegate { listeners -= sink; });
        }
        internal static void Write(string text)
        {
            var copy = listeners;
            if (copy == null) return;
            foreach (Action<string> sink in copy.GetInvocationList())
                try { sink(text); } catch { /* Diagnostics never alter physical input. */ }
        }
    }
}
