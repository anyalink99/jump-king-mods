using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using JumpKing;
using Microsoft.Xna.Framework.Input;

namespace MultiplayerExpansion
{
    internal static partial class Client
    {
        private static void InstallRuntimePointer(Assembly assembly)
        {
            var pointer = assembly.GetType("JKRuntime.UI.UiPointer", true);
            harmony.Patch(AccessTools.Method(pointer, "Update"), transpiler: new HarmonyMethod(typeof(Client), "PointerCalls"));
            // the release gate must see the same mouse state as the menu pointer
            var release = assembly.GetType("JKRuntime.UI.UiPageInput", true);
            harmony.Patch(AccessTools.Method(release, "IsReleased"), transpiler: new HarmonyMethod(typeof(Client), "PointerCalls"));
        }
        private static IEnumerable<CodeInstruction> PointerCalls(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
        {
            int mouseCalls = 0, focusCalls = 0;
            foreach (var instruction in instructions)
            {
                var method = instruction.operand as MethodInfo;
                if (method != null && method.Name == "GetForegroundWindow" && method.DeclaringType.FullName == "JKRuntime.Input.MouseButtons")
                {
                    focusCalls++;
                    instruction.operand = AccessTools.Method(typeof(Client), "PointerForeground");
                }
                if (method != null && method.Name == "GetState" && method.DeclaringType == typeof(Mouse) && method.GetParameters().Length == 0)
                {
                    mouseCalls++;
                    instruction.operand = AccessTools.Method(typeof(Client), "PointerMouse");
                }
                yield return instruction;
            }
            if (mouseCalls != 1 || (__originalMethod.DeclaringType.Name == "UiPointer" && focusCalls != 1))
                throw new NotSupportedException("JK Runtime pointer input layout changed");
        }
        private static IntPtr PointerForeground()
        {
            if (!InputAvailable || Game1.instance == null) return IntPtr.Zero;
            var mouse = PointerMouse();
            var size = Game1.instance.GetScreenSize();
            var bounds = Game1.instance.Window.ClientBounds;
            var pixel = new Microsoft.Xna.Framework.Point(mouse.X * size.X / Math.Max(1, bounds.Width), mouse.Y * size.Y / Math.Max(1, bounds.Height));
            return Game1.instance.GetGameRect().Contains(pixel) ? lastWindow : IntPtr.Zero;
        }
        private static MouseState PointerMouse()
        {
            var original = Mouse.GetState();
            Native.Point point;
            if (lastWindow == IntPtr.Zero || !Native.GetCursorPos(out point) || !Native.ScreenToClient(lastWindow, ref point)) return default(MouseState);
            // embedded windows don't always receive WM_MOUSEMOVE after switching input
            return new MouseState(point.X, point.Y, original.ScrollWheelValue, PointerButton(1), PointerButton(4), PointerButton(2), PointerButton(5), PointerButton(6));
        }
        private static ButtonState PointerButton(int key)
        { return InputAvailable && (Native.GetAsyncKeyState(key) & 0x8000) != 0 ? ButtonState.Pressed : ButtonState.Released; }
    }
}
