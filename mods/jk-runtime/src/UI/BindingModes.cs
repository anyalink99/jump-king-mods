using System;
using Microsoft.Xna.Framework;

namespace JKRuntime.UI
{
    public enum UiBindingMode { Hold, Press, Both }

    /// <summary>Declares the fixed activation mode of an automatically discovered enum binding.</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class UiBindingModeAttribute : Attribute
    {
        public UiBindingMode Mode { get; private set; }
        public UiBindingModeAttribute(UiBindingMode mode)
        {
            if (!Enum.IsDefined(typeof(UiBindingMode), mode)) throw new ArgumentOutOfRangeException("mode");
            Mode = mode;
        }
    }

    /// <summary>Optional provider-owned activation mode. The UI edits it; the mod implements its behavior.</summary>
    public sealed class UiBindingModeOption
    {
        private readonly Func<UiBindingMode> read;
        private readonly Action<UiBindingMode> write;
        private readonly UiBindingMode[] modes;
        public UiBindingMode Default { get; private set; }
        private readonly string reason;
        /// <summary>Optional context guard. Return a reason to lock editing; the provider owns its gameplay fallback.</summary>
        public Func<string> UnavailableReason { get; set; }
        /// <summary>Explains a temporary gameplay fallback without locking the saved preference.</summary>
        public Func<string> FallbackReason { get; set; }
        public bool Available { get { return UnavailableReason == null || string.IsNullOrEmpty(UnavailableReason()); } }
        public string Reason { get { return !Available ? UnavailableReason() : FallbackReason == null ? reason : FallbackReason() ?? reason; } }
        public bool CanChange { get { return Available && write != null && modes.Length > 1; } }
        public UiBindingMode[] AvailableModes { get { return (UiBindingMode[])modes.Clone(); } }
        public UiBindingModeOption(Func<UiBindingMode> read, Action<UiBindingMode> write, UiBindingMode defaultMode = UiBindingMode.Hold)
            : this(read, write, defaultMode, UiBindingMode.Hold, UiBindingMode.Press) { }
        public UiBindingModeOption(Func<UiBindingMode> read, Action<UiBindingMode> write, UiBindingMode defaultMode, params UiBindingMode[] availableModes)
        {
            if (read == null) throw new ArgumentNullException("read");
            if (write == null) throw new ArgumentNullException("write");
            if (availableModes == null || availableModes.Length < 2) throw new ArgumentException("At least two modes are required");
            modes = (UiBindingMode[])availableModes.Clone();
            foreach (var mode in modes) Validate(mode);
            if (Array.IndexOf(modes, defaultMode) < 0) throw new ArgumentException("Default mode must be available");
            this.read = read; this.write = write; Default = defaultMode;
        }
        public UiBindingModeOption(UiBindingMode mode, string reason)
        { Validate(mode); Default = mode; modes = new[] { mode }; read = () => mode; this.reason = reason; }
        public UiBindingMode Value
        {
            get { var value = read(); CheckAvailable(value); return value; }
            set { CheckAvailable(value); if (!CanChange) throw new InvalidOperationException("This action has no alternate mode"); write(value); }
        }
        private void CheckAvailable(UiBindingMode value)
        { Validate(value); if (Array.IndexOf(modes, value) < 0) throw new ArgumentOutOfRangeException("value"); }
        private static void Validate(UiBindingMode value)
        { if (!Enum.IsDefined(typeof(UiBindingMode), value)) throw new ArgumentOutOfRangeException("value"); }
        internal void Cycle() { Value = modes[(Array.IndexOf(modes, Value) + 1) % modes.Length]; }
        internal void Reset() { Value = Default; }
    }

    internal static class BindingColumns
    {
        // leave a separate footer row for mode reasons and capture status
        internal const int ListRows = 7;
        internal static void Message(Rectangle frame, string text, Color color)
        {
            var row = UiTheme.FooterRow(frame, 1);
            UiTheme.TextLine(UiTheme.FitText(text ?? "", row.Width, true), new Vector2(row.X, row.Y + 4), color, true);
        }
        internal static UiCommand[] Commands(int slot)
        {
            return slot == 2 ? new[] { UiInputHints.Command(UiAction.Confirm, "CHANGE"),
                UiInputHints.Command(UiAction.Reset, "DEFAULT"), UiInputHints.Command(UiAction.Cancel, "BACK") }
                : new[] { UiInputHints.Command(UiAction.Confirm, "REBIND"), UiInputHints.Command(UiAction.Secondary, "CLEAR"),
                UiInputHints.Command(UiAction.Reset, "DEFAULT"), UiInputHints.Command(UiAction.Cancel, "BACK") };
        }
        internal static bool Editable(UiBindingDefinition binding) { return binding != null && binding.Mode != null && binding.Mode.CanChange; }
        internal static int First(UiBindingDefinition binding) { return Editable(binding) ? 2 : 0; }
        internal static int Clamp(int slot, UiBindingDefinition binding) { return slot == 2 && !Editable(binding) ? 0 : slot; }
        internal static int Step(int slot, int direction, UiBindingDefinition binding)
        {
            int position = slot == 2 ? 0 : slot + 1;
            position = Math.Max(Editable(binding) ? 0 : 1, Math.Min(2, position + direction));
            return position == 0 ? 2 : position - 1;
        }
        internal static int X(int slot, bool modes) { return modes ? 216 + (slot == 2 ? 0 : slot + 1) * 76 : 282 + slot * 84; }
        internal static int Width(bool modes) { return modes ? 68 : 74; }
        internal static int LabelWidth(bool modes) { return modes ? 162 : 228; }
        internal static void Headers(bool modes)
        {
            UiTheme.TextLine("PRIMARY", new Vector2(X(0, modes) + 4, 39), UiTheme.Muted, true);
            UiTheme.TextLine("SECONDARY", new Vector2(X(1, modes) + 4, 39), UiTheme.Muted, true);
            if (modes) UiTheme.TextLine("MODE", new Vector2(X(2, true) + 4, 39), UiTheme.Muted, true);
        }
        internal static void DrawMode(UiBindingDefinition binding, Rectangle bounds, bool selected)
        {
            string text;
            try { text = binding == null || binding.Mode == null ? "Unknown" : !binding.Mode.Available ? "Native" : binding.Mode.Value.ToString(); } catch { text = "Unknown"; }
            if (!Editable(binding)) {
                UiTheme.Keycap(text, bounds, false, UiTheme.Muted);
                return;
            }
            UiTheme.Keycap(text, bounds, selected);
        }
    }
}
