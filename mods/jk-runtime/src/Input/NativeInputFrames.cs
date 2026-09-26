using System;
using System.Reflection;
using JumpKing.Controller;

namespace JKRuntime.Input
{
    /// <summary>Publishes complete native input frames. Call on the game thread after native device polling.</summary>
    public static class NativeInputFrames
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly FieldInfo Current = typeof(PadInstance).GetField("current_state", Flags);
        private static readonly FieldInfo Previous = typeof(PadInstance).GetField("last_state", Flags);
        private static readonly FieldInfo MenuState = typeof(MenuController).GetField("_menu_state", Flags);
        private static MenuFrame activeMenu;

        /// <summary>Held includes any one-tick pulses. Pressed is restricted to those held actions.
        /// Native getters and already compiled consumers see the same snapshot without getter hooks.</summary>
        public static void Publish(PadInstance pad, PadState held, PadState pressed)
        {
            RuntimeApi.Kernel.CheckThread();
            if (pad == null) throw new ArgumentNullException("pad");
            PadState previous = held;
            previous.up &= !pressed.up; previous.down &= !pressed.down;
            previous.left &= !pressed.left; previous.right &= !pressed.right;
            previous.jump &= !pressed.jump; previous.pause &= !pressed.pause;
            previous.confirm &= !pressed.confirm; previous.cancel &= !pressed.cancel;
            previous.boots &= !pressed.boots; previous.snake &= !pressed.snake;
            previous.restart &= !pressed.restart;
            Previous.SetValue(pad, previous);
            Current.SetValue(pad, held);
        }

        /// <summary>Owns one menu update's input. Consumed input is never restored on exit.
        /// Nested frames are rejected before changing native state. Dispose on the game thread.</summary>
        public static IDisposable BeginMenu(MenuController menu, PadState pressed)
        {
            RuntimeApi.Kernel.CheckThread();
            if (menu == null) throw new ArgumentNullException("menu");
            if (activeMenu != null) throw new InvalidOperationException("A native menu input frame is already active");
            var frame = new MenuFrame(menu);
            MenuState.SetValue(menu, pressed);
            activeMenu = frame;
            return frame;
        }
        private sealed class MenuFrame : IDisposable
        {
            private MenuController menu;
            internal MenuFrame(MenuController value) { menu = value; }
            public void Dispose()
            {
                RuntimeApi.Kernel.CheckThread();
                if (menu == null) return;
                try { menu.ConsumePadPresses(); }
                finally { menu = null; activeMenu = null; }
            }
        }
    }
}
