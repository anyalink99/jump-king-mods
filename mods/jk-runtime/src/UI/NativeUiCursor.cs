using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace JKRuntime.UI
{
    // Windows moves this cursor independently of the game's fixed update/render loop.
    // Own only the game window's cursor; preserve other mods' cursor on release.
    internal sealed class NativeUiCursor : IDisposable
    {
        private Control window;
        private Cursor previous, cursor;
        private IntPtr handle;
        private int scale;
        internal bool Active { get { return window != null && !window.IsDisposed && cursor != null; } }

        internal bool Apply(Control target, int pixelScale)
        {
            if (target == null || target.IsDisposed) return false;
            pixelScale = Math.Max(1, Math.Min(16, pixelScale));
            if (target != window || pixelScale != scale) Dispose();
            if (cursor == null)
            {
                handle = Create(pixelScale);
                cursor = new Cursor(handle);
                window = target; previous = target.Cursor; scale = pixelScale;
            }
            if (target.Cursor != cursor) target.Cursor = cursor;
            return true;
        }

        public void Dispose()
        {
            if (window != null && !window.IsDisposed && window.Cursor == cursor) window.Cursor = previous;
            window = null; previous = null;
            if (cursor != null) { cursor.Dispose(); cursor = null; }
            // Cursor(IntPtr) does not own the native handle.
            if (handle != IntPtr.Zero) { DestroyCursor(handle); handle = IntPtr.Zero; }
        }

        private static IntPtr Create(int scale)
        {
            using (var bitmap = new Bitmap(13 * scale, 16 * scale, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            {
                for (int pass = 0; pass < 2; pass++)
                    for (int y = 0; y < UiPointer.Cursor.Length; y++)
                        for (int x = 0; x < UiPointer.Cursor[y].Length; x++)
                        {
                            char c = UiPointer.Cursor[y][x]; if (c == '.') continue;
                            var tint = c == 'W' ? UiTheme.Text : UiTheme.Gold;
                            Color color = pass == 0 ? Color.FromArgb(180, 0, 0, 0)
                                : c == 'X' ? Color.FromArgb(28, 24, 17) : Color.FromArgb(tint.R, tint.G, tint.B);
                            int offset = pass == 0 ? 1 : 0;
                            for (int dy = 0; dy < scale; dy++) for (int dx = 0; dx < scale; dx++)
                                bitmap.SetPixel((x + offset) * scale + dx, (y + offset) * scale + dy, color);
                        }
                IntPtr icon = bitmap.GetHicon(); IconInfo info = new IconInfo();
                try
                {
                    if (!GetIconInfo(icon, out info)) throw new Win32Exception();
                    info.IsIcon = false; info.XHotspot = info.YHotspot = 0;
                    IntPtr result = CreateIconIndirect(ref info);
                    if (result == IntPtr.Zero) throw new Win32Exception();
                    return result;
                }
                finally
                {
                    if (info.Mask != IntPtr.Zero) DeleteObject(info.Mask);
                    if (info.Color != IntPtr.Zero) DeleteObject(info.Color);
                    DestroyIcon(icon);
                }
            }
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct IconInfo
        {
            [MarshalAs(UnmanagedType.Bool)] internal bool IsIcon;
            internal int XHotspot, YHotspot;
            internal IntPtr Mask, Color;
        }
        [DllImport("user32.dll", SetLastError = true)] private static extern bool GetIconInfo(IntPtr icon, out IconInfo info);
        [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr CreateIconIndirect(ref IconInfo info);
        [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr icon);
        [DllImport("user32.dll")] private static extern bool DestroyCursor(IntPtr cursor);
        [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr value);
    }
}
