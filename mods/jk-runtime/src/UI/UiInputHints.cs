using System;
using JumpKing.Controller;

namespace JKRuntime.UI
{
    /// <summary>Physical button labels for the same logical actions that modal pages receive.</summary>
    public static class UiInputHints
    {
        /// <summary>Resolve at draw time so device switches and rebinding update the displayed key.</summary>
        public static UiCommand Command(UiAction action, string label)
        { return new UiCommand(Key(action), label) { PointerAction = action }; }

        /// <summary>For native menus that distinguish Confirm from Jump rather than using UiAction.</summary>
        public static UiCommand Command(JKpadButtons action, string label)
        {
            UiAction mapped = action == JKpadButtons.Confirm || action == JKpadButtons.Jump ? UiAction.Confirm
                : action == JKpadButtons.Cancel || action == JKpadButtons.Pause ? UiAction.Cancel
                : action == JKpadButtons.Boots ? UiAction.Secondary : action == JKpadButtons.Restart ? UiAction.Reset
                : action == JKpadButtons.Up ? UiAction.Up : action == JKpadButtons.Down ? UiAction.Down
                : action == JKpadButtons.Left ? UiAction.Left : action == JKpadButtons.Right ? UiAction.Right : UiAction.None;
            return new UiCommand(Key(action), label) { PointerAction = mapped };
        }

        public static string Key(JKpadButtons action)
        {
            var manager = ControllerManager.instance;
            var pad = manager == null ? null : manager.GetMain();
            return pad == null || !pad.IsValid || !pad.IsConnected || !pad.GetBind().Enabled ? "-" : Binding(pad, action);
        }

        public static string Key(UiAction action)
        {
            var manager = ControllerManager.instance;
            return manager == null ? "-" : Key(manager.GetMain(), action);
        }

        /// <summary>Shows one valid physical binding, including a Controls+ chord. Missing bindings use '-'.</summary>
        public static string Key(PadInstance pad, UiAction action)
        {
            if (pad == null || !pad.IsValid || !pad.IsConnected || !pad.GetBind().Enabled) return "-";
            JKpadButtons primary;
            JKpadButtons? alternate = null;
            switch (action)
            {
                case UiAction.Up: primary = JKpadButtons.Up; break;
                case UiAction.Down: primary = JKpadButtons.Down; break;
                case UiAction.Left: primary = JKpadButtons.Left; break;
                case UiAction.Right: primary = JKpadButtons.Right; break;
                case UiAction.Confirm: primary = JKpadButtons.Confirm; alternate = JKpadButtons.Jump; break;
                case UiAction.Cancel: primary = JKpadButtons.Cancel; alternate = JKpadButtons.Pause; break;
                case UiAction.Secondary: primary = JKpadButtons.Boots; break;
                case UiAction.Reset: primary = JKpadButtons.Restart; break;
                default: return "-";
            }
            string value = Binding(pad, primary);
            return value != "-" || !alternate.HasValue ? value : Binding(pad, alternate.Value);
        }

        private static string Binding(PadInstance pad, JKpadButtons action)
        {
            foreach (int button in pad.GetBind().GetButtonBind(action) ?? new int[0])
            {
                string label = pad.GetPad().ButtonToString(button);
                if (!string.IsNullOrWhiteSpace(label)) return UiTheme.NormalizeKey(label);
            }
            return "-";
        }
    }
}
