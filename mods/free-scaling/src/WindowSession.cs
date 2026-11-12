using System;
using System.Drawing;
using System.Windows.Forms;
using JKRuntime;
using JumpKing;

namespace FreeScaling
{
    internal sealed class WindowSession : IDisposable
    {
        private readonly Form form;
        private readonly Size oldSize, oldMinimum;
        private readonly Point oldLocation;
        private readonly FormWindowState oldState;
        private readonly bool oldResize, oldFullscreen, oldBorderless;
        private FrameSamplingLease sampling;
        private bool changing, disposed;
        internal PixelSampling Sampling { get { return sampling.Mode; } set { sampling.Mode = value; } }
        internal WindowSession(Preferences prefs)
        {
            form = Control.FromHandle(Game1.instance.Window.Handle) as Form;
            if (form == null) throw new NotSupportedException("Graphics+ needs the Windows game window");
            oldSize = form.ClientSize; oldMinimum = form.MinimumSize; oldLocation = form.Location; oldState = form.WindowState;
            oldResize = Game1.instance.Window.AllowUserResizing; oldFullscreen = Game1.graphics.IsFullScreen; oldBorderless = Game1.instance.Window.IsBorderless;
            changing = true;
            try {
                sampling = FramePresentation.Acquire(Game1.instance.GraphicsDevice, prefs.Sampling);
                Game1.graphics.IsFullScreen = false; Game1.instance.Window.IsBorderless = false;
                form.WindowState = FormWindowState.Normal;
                var area = (prefs.HasPosition ? Screen.FromPoint(new Point(prefs.X, prefs.Y)) : Screen.FromControl(form)).WorkingArea;
                var size = Fit(prefs.Width == 0 ? oldSize : new Size(prefs.Width, prefs.Height), area.Size);
                Game1.graphics.PreferredBackBufferWidth = size.Width; Game1.graphics.PreferredBackBufferHeight = size.Height;
                Game1.graphics.ApplyChanges();
                Game1.instance.Window.AllowUserResizing = true;
                form.MinimumSize = new Size(480 + form.Width - form.ClientSize.Width, 360 + form.Height - form.ClientSize.Height);
                // keep the title bar reachable after monitor or DPI changes
                form.Location = FitPosition(prefs.HasPosition ? new Point(prefs.X, prefs.Y) : form.Location, form.Size, area);
                form.ResizeEnd += Resized;
                Game1.instance.Window.ClientSizeChanged += Resized;
            } catch { Restore(); throw; }
            finally { changing = false; }
        }
        internal static Size Fit(Size requested, Size workArea)
        { return new Size(Math.Max(480, Math.Min(requested.Width, workArea.Width - 24)), Math.Max(360, Math.Min(requested.Height, workArea.Height - 64))); }
        internal static Point FitPosition(Point requested, Size window, Rectangle area)
        { return new Point(Math.Max(area.Left, Math.Min(requested.X, area.Right - window.Width)),
            Math.Max(area.Top, Math.Min(requested.Y, area.Bottom - window.Height))); }
        private void Resized(object sender, EventArgs args)
        {
            if (changing || disposed || form.WindowState != FormWindowState.Normal) return;
            var size = form.ClientSize;
            if (size.Width < 480 || size.Height < 360 || size.Width > 32768 || size.Height > 32768) return;
            ModEntry.RememberWindow(size.Width, size.Height, form.Left, form.Top);
        }
        private void Restore()
        {
            form.ResizeEnd -= Resized; Game1.instance.Window.ClientSizeChanged -= Resized;
            if (sampling != null) { sampling.Dispose(); sampling = null; }
            if (form.IsDisposed || Game1.instance.GraphicsDevice.IsDisposed) return;
            form.MinimumSize = oldMinimum; Game1.instance.Window.AllowUserResizing = oldResize;
            form.WindowState = FormWindowState.Normal;
            Game1.instance.Window.IsBorderless = oldBorderless; Game1.graphics.IsFullScreen = oldFullscreen;
            Game1.graphics.PreferredBackBufferWidth = oldSize.Width; Game1.graphics.PreferredBackBufferHeight = oldSize.Height;
            Game1.graphics.ApplyChanges(); form.Location = oldLocation; form.WindowState = oldState;
        }
        public void Dispose()
        {
            if (disposed) return;
            // capture moves without ResizeEnd too, before restoring the native window
            Resized(null, EventArgs.Empty);
            changing = true; try { Restore(); disposed = true; } finally { changing = false; }
        }
    }
}
