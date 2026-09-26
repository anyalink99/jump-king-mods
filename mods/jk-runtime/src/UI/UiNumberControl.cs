using System;
using System.Globalization;
using Microsoft.Xna.Framework;

namespace JKRuntime.UI
{
    /// <summary>Bounded numeric editor using the same value setter for keys, wheel and pointer drag.
    /// Draw within a page surface; cancel capture before page teardown. Values snap to Step.</summary>
    public sealed class UiNumberControl
    {
        private readonly Func<double> read;
        private readonly Action<double> write;
        private long lastDragSound;
        public double Minimum { get; private set; }
        public double Maximum { get; private set; }
        public double Step { get; private set; }
        public UiNumberControl(double minimum, double maximum, double step, Func<double> getValue, Action<double> setValue)
        {
            if (double.IsNaN(minimum) || double.IsInfinity(minimum) || double.IsNaN(maximum) || double.IsInfinity(maximum)
                || double.IsNaN(step) || double.IsInfinity(step) || maximum <= minimum || step <= 0) throw new ArgumentException("Finite bounds and positive step are required");
            if (getValue == null || setValue == null) throw new ArgumentNullException("value delegates");
            Minimum = minimum; Maximum = maximum; Step = step; read = getValue; write = setValue;
        }
        public void Set(double value)
        { SetValue(value, false); }
        private void SetValue(double value, bool dragging)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) throw new ArgumentOutOfRangeException("value");
            value = Math.Max(Minimum, Math.Min(Maximum, value));
            double next = Math.Max(Minimum, Math.Min(Maximum, Minimum + Math.Round((value - Minimum) / Step) * Step));
            if (read() == next) return;
            try { write(next); }
            catch { UiSounds.Play(UiSound.Error); throw; }
            long now = System.Diagnostics.Stopwatch.GetTimestamp();
            if (dragging && lastDragSound != 0 && now - lastDragSound < System.Diagnostics.Stopwatch.Frequency / 16) return;
            if (dragging) lastDragSound = now;
            UiSounds.Play(UiSound.Change);
        }
        public bool Update(UiInput input)
        {
            if (input.Action != UiAction.Left && input.Action != UiAction.Right) return false;
            Set(read() + (input.Action == UiAction.Left ? -Step : Step)); return true;
        }
        public void Draw(Rectangle bounds)
        {
            if (bounds.Width < 32 || bounds.Height < 32) throw new ArgumentException("Numeric editor needs 32x32 pixels");
            double value = read();
            UiTheme.TextLine(value.ToString("0.###", CultureInfo.InvariantCulture), bounds.Location.ToVector2(), UiTheme.Text, true);
            Rectangle bar = new Rectangle(bounds.X, bounds.Y + 20, bounds.Width, 10);
            UiTheme.Panel(bar, UiTheme.PanelFill, UiTheme.Border);
            int x = bar.X + (int)Math.Round(Math.Max(0, Math.Min(1, (value - Minimum) / (Maximum - Minimum))) * (bar.Width - 4));
            UiTheme.Panel(new Rectangle(x, bar.Y - 2, 4, 14), UiTheme.Gold, UiTheme.Border);
            double original = value;
            UiPointer.DragRegion(new Rectangle(bar.X, bar.Y - 5, bar.Width, 20),
                p => SetValue(Minimum + Math.Max(0, Math.Min(1, (p.X - bar.X) / (double)Math.Max(1, bar.Width - 1))) * (Maximum - Minimum), true),
                cancelled => { lastDragSound = 0; if (cancelled) { write(original); UiSounds.Play(UiSound.Back); } });
            UiPointer.ScrollRegion(bounds, steps => Set(read() + Step * steps));
        }
    }
}
