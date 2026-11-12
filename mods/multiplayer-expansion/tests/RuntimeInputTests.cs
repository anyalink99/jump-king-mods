using System;
using System.Diagnostics;
using System.Reflection;

namespace MultiplayerExpansion
{
    internal static class RuntimeInputTests
    {
        internal static void Run(string path)
        {
            Assembly runtime = Client.LoadShared(path);
            Type source = runtime.GetType("JKRuntime.Input.KeyboardSource", true);
            Func<int, short> unusedReader = key => 0;
            Func<IntPtr> unusedFocus = () => new IntPtr(999);
            var automatic = Activator.CreateInstance(source, BindingFlags.Instance | BindingFlags.NonPublic, null,
                new object[] { unusedReader, unusedFocus, true }, null);
            var focusField = source.GetField("foreground", BindingFlags.Instance | BindingFlags.NonPublic);
            if (ReferenceEquals(focusField.GetValue(automatic), unusedFocus)) throw new Exception("Runtime keyboard wasn't routed through the lab.");
            if (((Func<IntPtr>)focusField.GetValue(automatic))() != IntPtr.Zero) throw new Exception("Runtime accepted input without an active host.");
            var overlay=HarmonyLib.AccessTools.Field(typeof(JumpKing.Controller.PadInstance),"_steam_overlay_active");
            object oldOverlay=overlay.GetValue(null);
            try {
                overlay.SetValue(null,true);
                if(((Func<IntPtr>)focusField.GetValue(automatic))()!=IntPtr.Zero ||
                    ((Func<int,short>)source.GetField("reader",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(automatic))(75)!=0)
                    throw new Exception("Steam Overlay leaked through Runtime's physical input source");
                object[] keyboardArgs={default(Microsoft.Xna.Framework.Input.KeyboardState)};
                HarmonyLib.AccessTools.Method(typeof(Client),"KeyboardState").Invoke(null,keyboardArgs);
                if(((Microsoft.Xna.Framework.Input.KeyboardState)keyboardArgs[0]).GetPressedKeys().Length!=0)
                    throw new Exception("Steam Overlay leaked through the native keyboard source");
            } finally {overlay.SetValue(null,oldOverlay);}

            var host = new IntPtr(10);
            var client = new IntPtr(11);
            var foreground = host;
            var root = host;
            int selected = 1;
            bool down = false;
            var actions = new int[11][][];
            for (int i = 0; i < actions.Length; i++) actions[i] = new int[0][];
            actions[5] = new[] { new[] { 27 } };
            Type edgesType = runtime.GetType("JKRuntime.Input.KeyboardActionEdges", true);
            var edges = (IDisposable)Activator.CreateInstance(edgesType, new object[] { actions, client,
                new Func<int, short>(key => key == 27 && down ? unchecked((short)0x8000) : (short)0),
                new Func<IntPtr>(() => InputRouting.Foreground(foreground, root, host, selected, 1, client)), false });
            var sample = edgesType.GetMethod("Sample");
            var take = edgesType.GetMethod("Take");
            Func<int> poll = delegate {
                long now = Stopwatch.GetTimestamp();
                sample.Invoke(edges, new object[] { now });
                return (int)take.Invoke(edges, new object[] { now });
            };
            using (edges)
            {
                poll(); down = true;
                if (poll() != 32 || poll() != 0) throw new Exception("Esc didn't produce exactly one native pause edge.");
                down = false; poll(); selected = 2; poll(); down = true;
                if (poll() != 0) throw new Exception("Esc reached the unselected client.");
                selected = 1;
                if (poll() != 0) throw new Exception("Switching clients replayed a held Esc.");
                down = false; poll(); down = true;
                if (poll() != 32) throw new Exception("Esc didn't recover after switching clients.");
                down = false; poll(); foreground = root = new IntPtr(20); poll(); down = true;
                if (poll() != 0) throw new Exception("Another application sent a pause edge.");
            }
            // use Runtime's real pointer state machine, without touching the physical mouse
            var pointerType = runtime.GetType("JKRuntime.UI.PointerState", true);
            var pointer = Activator.CreateInstance(pointerType, true);
            var pointerPoll = pointerType.GetMethod("Poll", BindingFlags.Instance | BindingFlags.NonPublic);
            var point = new Microsoft.Xna.Framework.Point(200, 100);
            Action<bool, bool> observe = delegate(bool hasFocus, bool left) {
                pointerPoll.Invoke(pointer, new object[] { point, true, hasFocus, true, left, false, 0, false });
            };
            Func<string, bool> state = name => (bool)pointerType.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(pointer);
            foreground = root = host; selected = 2;
            bool secondFocused = InputRouting.Foreground(foreground, root, host, selected, 2, client) == client;
            observe(secondFocused, false); observe(secondFocused, true);
            if (!state("Visible") || state("Click")) throw new Exception("Embedded UI cursor didn't activate without click-through");
            observe(secondFocused, false); observe(secondFocused, true);
            if (!state("Click")) throw new Exception("UI cursor couldn't click after activation");
            selected = 1;
            observe(InputRouting.Foreground(foreground, root, host, selected, 2, client) == client, true);
            if (state("Visible") || state("Click")) throw new Exception("The unselected UI cursor accepted a click");
            observe(true, true);
            if (state("Click")) throw new Exception("A held click leaked when switching back");
            Console.WriteLine("[OK] UIAPI+ pointer: embedded focus, activation, click routing and held-button isolation");
            Console.WriteLine("[OK] Runtime keyboard routing: Esc press/hold/release, client switching, external focus and Steam Overlay isolation");
        }
    }
}
