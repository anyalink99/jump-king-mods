using System;
using JKRuntime.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace JKRuntime
{
    internal static class PointerTests
    {
        private static void Check(bool value, string name) { if (!value) throw new Exception(name); }
        private static void Main()
        {
            DiagnosticWrites();
            NativeCursor();
            WindowCursorLeases();
            KeyboardVisibility();
            ListViewport();
            DragCapture();
            Point mapped;
            Check(PointerState.Map(new Point(960, 540), new Rectangle(240, 0, 1440, 1080), out mapped) && mapped == new Point(240, 180), "Letterbox mapping");
            Check(!PointerState.Map(new Point(239, 540), new Rectangle(240, 0, 1440, 1080), out mapped), "Letterbox is not interactive");
            Check(PointerState.Map(new Point(1679, 1079), new Rectangle(240, 0, 1440, 1080), out mapped) && mapped == new Point(479, 359), "Last pixel mapping");
            var state = new PointerState(); var point = new Point(10, 10);
            state.Poll(point, true, true, true, false, false, 0, false);
            state.Poll(point, true, true, true, false, false, 120, false);
            Check(!state.Visible && state.Wheel == 0, "Wheel does not activate cursor");
            state.Poll(point, true, true, true, true, false, 120, false);
            Check(state.Visible && !state.Click && !state.Moved, "First click only activates");
            state.Poll(point, true, true, true, false, false, 180, false);
            Check(state.Wheel == 0, "Partial wheel detent retained");
            state.Poll(point, true, true, true, true, false, 240, false);
            Check(state.Click && state.Wheel == 1, "Second click and accumulated wheel");
            state.Poll(point, true, true, true, true, false, 240, false);
            Check(!state.Click, "Held mouse never repeats clicks");
            state.Poll(point, true, true, true, false, false, 240, true);
            Check(!state.Visible, "Keyboard hides pointer");
            state.Poll(point, true, false, true, true, false, 240, false);
            state.Poll(point, true, true, true, true, false, 240, false);
            Check(!state.Visible, "Held focus-restoring click cannot activate");
            state.Poll(point, true, true, true, false, false, 240, false);
            state.Poll(point, true, true, true, true, false, 240, false);
            Check(state.Visible, "Release and repress after focus re-arms");
            state.Poll(point, true, true, false, false, false, 240, false);
            Check(!state.Visible, "Capture or gameplay hides pointer");

            UiPointer.Reset(); object parent = new object(), child = new object(); int hits = 0, hovered = 0, scrolled = 0;
            UiPointer.BeginDraw(); UiPointer.BeginSurface(parent);
            UiPointer.Region(new Rectangle(0, 0, 100, 100), null, () => hits += 100);
            UiPointer.BeginSurface(child);
            UiPointer.ActionRegion(new Rectangle(0, 0, 100, 100), UiAction.Confirm, () => hovered++);
            UiPointer.ScrollRegion(new Rectangle(0, 0, 100, 100), delta => scrolled += delta);
            UiPointer.EndDraw();
            UiPointer.Poll(point, true, true, false, false, 0, false);
            UiPointer.Poll(point, true, true, true, false, 0, false);
            Check(UiPointer.Read(child, new UiInput()).Action == UiAction.None && hovered == 0, "Activation cannot click through");
            UiPointer.Poll(point, true, true, false, false, 0, false);
            UiPointer.Poll(point, true, true, true, false, 0, false);
            Check(UiPointer.Read(child, new UiInput()).Confirm && hovered == 1 && hits == 0, "Only top surface handles click");
            Check(UiPointer.Read(child, new UiInput()).Action == UiAction.None, "Queued action consumed once");
            UiPointer.Poll(point, true, true, false, false, -240, false);
            Check(scrolled == -2, "Wheel dispatches multiple steps to region");
            UiPointer.Poll(point, true, true, false, true, -240, false);
            Check(UiPointer.Read(child, new UiInput()).Cancel, "RMB goes back");
            UiPointer.Reset();
            Check(!UiPointer.Visible && !UiPointer.SuppressMouseBindings, "Reset releases gameplay input");
            Console.WriteLine("[OK] Pointer activation, focus, wheel, viewport, modal isolation and action routing");
        }
        private static void WindowCursorLeases()
        {
            UiPointer.Reset();
            var first = UiPointer.AcquireWindowCursor(); var second = UiPointer.AcquireWindowCursor();
            Check(UiPointer.WindowCursorActive && UiPointer.SuppressMouseBindings, "Window editor owns the native cursor and suppresses mouse gameplay bindings");
            UiPointer.BeginDraw(); UiPointer.EndDraw();
            Check(UiPointer.WindowCursorActive, "An empty native UI draw does not release the window editor cursor");
            first.Dispose(); first.Dispose();
            Check(UiPointer.WindowCursorActive, "Idempotent disposal preserves another editor's cursor lease");
            second.Dispose();
            Check(!UiPointer.WindowCursorActive && !UiPointer.SuppressMouseBindings, "Last editor releases cursor and gameplay bindings");
            var stale = UiPointer.AcquireWindowCursor(); UiPointer.Reset(); var current = UiPointer.AcquireWindowCursor(); stale.Dispose();
            Check(UiPointer.WindowCursorActive, "A stale lease cannot release a cursor acquired after reset");
            current.Dispose(); UiPointer.Reset();
            Console.WriteLine("[OK] Native window cursor ownership, draw persistence, disposal and reset");
        }
        private static void DiagnosticWrites()
        {
            Action queued = null; int jobs = 0;
            var written = new System.Collections.Generic.List<string>();
            var writer = new LatestDiagnosticWriter(written.Add, action => { queued = action; jobs++; });
            for (int i = 0; i < 1000; i++) writer.Publish(i.ToString());
            Check(jobs == 1 && written.Count == 0, "Pointer diagnostics never write synchronously and coalesce a blocked worker");
            queued(); Check(written.Count == 1 && written[0] == "999", "Only latest pending pointer state is written");
            writer.Publish("next"); queued(); Check(jobs == 2 && written[1] == "next", "Worker restarts for a later state");
            LatestDiagnosticWriter racing = null;
            racing = new LatestDiagnosticWriter(text => { written.Add(text); if (text == "first") racing.Publish("during write"); }, action => queued = action);
            racing.Publish("first"); queued(); Check(written[written.Count - 1] == "during write", "Changes arriving during a write are retained");
            int attempts = 0;
            var broken = new LatestDiagnosticWriter(text => { attempts++; throw new System.IO.IOException("fixture"); }, action => queued = action);
            broken.Publish("failure"); queued(); broken.Publish("ignored");
            Check(attempts == 1, "Diagnostic storage failure is contained");
        }
        private static void DragCapture()
        {
            UiPointer.Reset(); object owner = new object(); int changes = 0, completed = 0, cancelled = 0;
            UiPointer.BeginDraw(); UiPointer.BeginSurface(owner);
            UiPointer.DragRegion(new Rectangle(0, 0, 20, 20), p => changes++, c => { if (c) cancelled++; else completed++; }); UiPointer.EndDraw();
            UiPointer.Poll(new Point(5, 5), true, true, false, false, 0, false);
            UiPointer.Poll(new Point(5, 5), true, true, true, false, 0, false);
            Check(changes == 0, "first click does not begin a drag");
            UiPointer.Poll(new Point(5, 5), true, true, false, false, 0, false);
            UiPointer.Poll(new Point(5, 5), true, true, true, false, 0, false);
            UiPointer.Poll(new Point(150, 100), true, true, true, false, 0, false);
            UiPointer.Poll(new Point(150, 100), true, true, false, false, 0, false);
            Check(changes == 2 && completed == 1, "capture tracks outside original hit area and releases once");
            UiPointer.Poll(new Point(5, 5), true, true, true, false, 0, false); UiPointer.CancelCapture(owner);
            Check(cancelled == 1, "page close cancels capture"); UiPointer.Reset();
        }
        private static void KeyboardVisibility()
        {
            foreach (Keys key in Enum.GetValues(typeof(Keys)))
            {
                if (key == Keys.None) continue;
                var state = new PointerState();
                state.Poll(Point.Zero, true, true, true, false, false, 0, false);
                state.Poll(Point.Zero, true, true, true, true, false, 0, false);
                state.Poll(Point.Zero, true, true, true, false, false, 0, PointerState.HidesCursor(new KeyboardState(key)));
                Check(state.Visible == (key == Keys.Escape), "Keyboard visibility for physical key " + key);
            }
            Check(PointerState.HidesCursor(new KeyboardState(Keys.Escape, Keys.LeftShift)), "Escape with a modifier still hides the cursor");
            var hidden = new PointerState();
            hidden.Poll(Point.Zero, true, true, true, false, false, 0, PointerState.HidesCursor(new KeyboardState(Keys.Escape)));
            Check(!hidden.Visible, "Escape never activates a hidden cursor");
            Check(!PointerState.HidesCursor(new KeyboardState()), "Idle keyboard leaves mouse navigation active");
            Console.WriteLine("[OK] Every physical keyboard key hides the cursor except Escape; chords and hidden state preserved");
        }
        private static void ListViewport()
        {
            var viewport = new UiListViewport();
            Check(viewport.FirstVisible(40, 8, 0) == 0, "List initially starts at selected row");
            for (int pass = 0; pass < 10; pass++)
                foreach (int hovered in new[] { 7, 0, 6, 1 })
                    Check(viewport.FirstVisible(40, 8, hovered) == 0, "Hover near either edge never scrolls");
            int selected = viewport.Scroll(3, 40, 8, 7);
            Check(selected == 10 && viewport.FirstVisible(40, 8, selected) == 3, "Wheel moves viewport while retaining the hovered screen row");
            Check(viewport.FirstVisible(40, 8, 3) == 3 && viewport.FirstVisible(40, 8, 10) == 3, "Hover after wheel cannot recenter");
            viewport.FollowSelection(11, 40, 8);
            Check(viewport.FirstVisible(40, 8, 11) == 7, "Keyboard navigation explicitly follows selection");
            viewport.FollowSelection(39, 40, 8);
            Check(viewport.FirstVisible(40, 8, 39) == 32, "Keyboard wrap to last item stays visible");
            Check(viewport.Scroll(int.MaxValue, 40, 8, 39) == 39 && viewport.FirstVisible(40, 8, 39) == 32, "Wheel clamps at bottom");
            Check(viewport.Scroll(int.MinValue, 40, 8, 39) == 7 && viewport.FirstVisible(40, 8, 7) == 0, "Wheel clamps at top without overflow");
            viewport.FollowSelection(30, 40, 8);
            Check(viewport.FirstVisible(4, 8, 3) == 0 && viewport.Scroll(1, 4, 8, 3) == 3, "Shrinking and short lists clamp safely");
            Check(viewport.Scroll(-1, 0, 8, 0) == 0 && viewport.FirstVisible(0, 8, 0) == 0, "Empty list is stable");
            viewport.Reset();
            Check(viewport.FirstVisible(40, 8, 20) == 16, "New page can initialize around its restored selection");
            Console.WriteLine("[OK] Independent list viewport: hover, wheel, keyboard, wrap, shrink and reset");
        }
        private static void NativeCursor()
        {
            using (var window = new System.Windows.Forms.Form())
            using (var cursor = new NativeUiCursor())
            {
                window.Cursor = System.Windows.Forms.Cursors.Cross;
                Check(!cursor.Apply(null, 1) && !cursor.Active, "Missing native window keeps software fallback available");
                foreach (int scale in new[] { 1, 2, 3, 4 })
                {
                    Check(cursor.Apply(window, scale) && cursor.Active, "Native cursor acquired");
                    var installed = window.Cursor;
                    Check(installed.HotSpot == System.Drawing.Point.Empty, "Native hotspot stays on arrow tip");
                    Check(cursor.Apply(window, scale) && window.Cursor == installed, "Unchanged frames reuse native cursor");
                    CursorInfo info;
                    Check(GetIconInfo(installed.Handle, out info), "Native cursor exposes its bitmap");
                    try
                    {
                        using (var bitmap = System.Drawing.Image.FromHbitmap(info.Color))
                        {
                            // Cursor.Size reports the system default size, not the custom bitmap.
                            Check(bitmap.Size == new System.Drawing.Size(13 * scale, 16 * scale), "Native cursor scales in whole pixels");
                            var cream = bitmap.GetPixel(scale, scale);
                            Check(cream.R == UiTheme.Text.R && cream.G == UiTheme.Text.G && cream.B == UiTheme.Text.B,
                                "Native cursor preserves cream RGB channels");
                        }
                    }
                    finally { DeleteObject(info.Color); DeleteObject(info.Mask); }
                }
                cursor.Dispose();
                Check(!cursor.Active && window.Cursor == System.Windows.Forms.Cursors.Cross, "Release restores previous mod cursor");
                cursor.Apply(window, 2); window.Cursor = System.Windows.Forms.Cursors.Hand;
                cursor.Dispose();
                Check(window.Cursor == System.Windows.Forms.Cursors.Hand, "Release preserves newer cursor owner");
                cursor.Dispose();
            }
            Console.WriteLine("[OK] Native cursor: pixel scales, hotspot, color, reuse, ownership and release");
        }
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct CursorInfo
        {
            internal int IsIcon, X, Y;
            internal IntPtr Mask, Color;
        }
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool GetIconInfo(IntPtr cursor, out CursorInfo info);
        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr value);
    }
}
