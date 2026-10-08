using System;
using System.Reflection;
using JumpKing.Controller;

namespace JKRuntime.UI
{
    internal static class UiInputRouter
    {
        private static readonly Type PauseManagerType =
            typeof(JumpKing.Game1).Assembly.GetType("JumpKing.PauseMenu.PauseManager", true);
        private static readonly FieldInfo PauseManagerInstance =
            PauseManagerType.GetField("instance", BindingFlags.Public | BindingFlags.Static);
        private static readonly PropertyInfo IsPausedProperty =
            PauseManagerType.GetProperty("IsPaused", BindingFlags.Public | BindingFlags.Instance);
        internal static UiInput Read(PadState state, bool includeReset)
        {
            UiAction action = Resolve(state, includeReset);
            return new UiInput
            {
                Action = action,
                Cancel = action == UiAction.Cancel,
                Confirm = action == UiAction.Confirm,
                Up = action == UiAction.Up,
                Down = action == UiAction.Down,
                Left = action == UiAction.Left,
                Right = action == UiAction.Right,
                Secondary = action == UiAction.Secondary
            };
        }

        internal static UiInput FromAction(UiAction action)
        {
            return new UiInput { Action = action, Up = action == UiAction.Up, Down = action == UiAction.Down,
                Left = action == UiAction.Left, Right = action == UiAction.Right, Confirm = action == UiAction.Confirm,
                Cancel = action == UiAction.Cancel, Secondary = action == UiAction.Secondary };
        }
        internal static PadState ToPadState(UiInput input)
        {
            return new PadState { up = input.Up, down = input.Down, left = input.Left, right = input.Right,
                confirm = input.Confirm, cancel = input.Cancel, boots = input.Secondary, restart = input.Action == UiAction.Reset };
        }
        internal static UiAction Resolve(PadState state, bool includeReset)
        {
            if (state.cancel || state.pause) return UiAction.Cancel;
            if (state.confirm || state.jump) return UiAction.Confirm;
            if (includeReset && state.restart) return UiAction.Reset;
            if (state.boots) return UiAction.Secondary;
            if (state.up) return UiAction.Up;
            if (state.down) return UiAction.Down;
            if (state.left) return UiAction.Left;
            if (state.right) return UiAction.Right;
            return UiAction.None;
        }

        internal static void Consume()
        {
            ControllerManager.instance.MenuController.ConsumePadPresses();
        }

        internal static bool IsGamePaused()
        {
            object instance = PauseManagerInstance == null ? null : PauseManagerInstance.GetValue(null);
            return instance != null
                && IsPausedProperty != null
                && (bool)IsPausedProperty.GetValue(instance, null);
        }
    }
}
