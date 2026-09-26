using System;
using System.Collections.Generic;
using System.Linq;
using JKRuntime.Input;
using JumpKing;
using JumpKing.Controller;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace JKRuntime.UI
{
    // The render pass describes hit areas; callbacks run only on the game update thread.
    public static class UiPointer
    {
        private sealed class RegionEntry
        {
            internal Rectangle Bounds;
            internal Action Hover, Click;
            internal Action<int> Scroll;
            internal Action<Point> Drag;
            internal Action<bool> EndDrag;
        }
        private sealed class Surface
        {
            internal object Owner;
            internal bool Enabled;
            internal readonly List<RegionEntry> Regions = new List<RegionEntry>();
        }
        private static Surface drawing, presented;
        private static RegionEntry captured;
        private static object captureOwner;
        private static readonly Dictionary<object, UiAction> Pending = new Dictionary<object, UiAction>();
        internal static readonly PointerState State = new PointerState();
        private static bool hookedDraw, restoreCursor, originalCursor;
        private static readonly NativeUiCursor NativeCursor = new NativeUiCursor();
        private static bool nativeCursorFailed;
        private static readonly HashSet<object> windowCursorOwners = new HashSet<object>();
        internal static bool WindowCursorActive { get { return windowCursorOwners.Count != 0; } }
        /// <summary>Use UIApi+'s native window cursor for a full-window editor.
        /// Dispose on close. This owns only cursor presentation and mouse-binding suppression.</summary>
        public static IDisposable AcquireWindowCursor()
        {
            var token = new object(); windowCursorOwners.Add(token);
            return new WindowCursorLease(token);
        }
        private sealed class WindowCursorLease : IDisposable
        {
            private object token;
            internal WindowCursorLease(object value) { token = value; }
            public void Dispose() { if (token == null) return; windowCursorOwners.Remove(token); token = null; if (!WindowCursorActive) RestoreSystemCursor(); }
        }
        public static bool Visible { get { return State.Visible && presented != null && State.Inside; } }
        public static Point Position { get { return State.Position; } }
        internal static bool SuppressMouseBindings { get { return WindowCursorActive || presented != null && presented.Enabled; } }

        /// <summary>Declare the top interactive page during Draw. A capture page blocks underlying pointer input.</summary>
        public static void BeginSurface(object owner, bool acceptsPointer = true)
        {
            if (!hookedDraw || owner == null) return;
            drawing = new Surface { Owner = owner, Enabled = acceptsPointer };
        }
        public static void Region(Rectangle bounds, Action hover, Action click)
        {
            if (drawing == null || !drawing.Enabled || bounds.Width <= 0 || bounds.Height <= 0) return;
            drawing.Regions.Add(new RegionEntry { Bounds = bounds, Hover = hover, Click = click });
        }
        public static void ScrollRegion(Rectangle bounds, Action<int> scroll)
        {
            if (drawing == null || !drawing.Enabled || scroll == null) return;
            drawing.Regions.Add(new RegionEntry { Bounds = bounds, Scroll = scroll });
        }
        /// <summary>Capture a left-button drag on the top surface. Changed receives game pixels.
        /// Completed receives true on cancellation (focus/surface/keyboard), false on release.
        /// Call during Draw; capture callbacks continue outside the original region.</summary>
        public static void DragRegion(Rectangle bounds, Action<Point> changed, Action<bool> completed = null)
        {
            if (drawing == null || !drawing.Enabled || bounds.Width <= 0 || bounds.Height <= 0) return;
            if (changed == null) throw new ArgumentNullException("changed");
            drawing.Regions.Add(new RegionEntry { Bounds = bounds, Drag = changed, EndDrag = completed });
        }
        private static void ReleaseCapture(bool cancelled)
        {
            RegionEntry previous = captured; captured = null; captureOwner = null;
            if (previous != null && previous.EndDrag != null)
                try { previous.EndDrag(cancelled); }
                catch (Exception error) { PointerDiagnostics.Status("Drag cleanup failed: " + error.GetBaseException().Message); }
        }
        /// <summary>Cancel this page's drag before releasing resources during OnClose.</summary>
        public static void CancelCapture(object owner) { if (ReferenceEquals(owner, captureOwner)) ReleaseCapture(true); }
        public static void ActionRegion(Rectangle bounds, UiAction action, Action hover = null)
        {
            if (drawing == null || action == UiAction.None) return;
            object owner = drawing.Owner;
            Region(bounds, hover, () => Queue(owner, action));
        }
        internal static void Queue(object owner, UiAction action) { if (owner != null) Pending[owner] = action; }
        internal static UiInput Read(object owner, UiInput input)
        {
            UiAction action;
            if (!Pending.TryGetValue(owner, out action)) return input;
            Pending.Remove(owner);
            return input.Action == UiAction.None ? UiInputRouter.FromAction(action) : input;
        }
        internal static void BeginDraw() { PointerDiagnostics.Frame(); hookedDraw = true; drawing = null; }
        internal static void EndDraw()
        {
            presented = drawing; drawing = null; hookedDraw = false;
            if (WindowCursorActive) return;
            if (presented == null) { State.Hide(); RestoreSystemCursor(); }
            if (Visible && !NativeCursor.Active) DrawCursor(Position);
        }
        internal static void Reset()
        {
            ReleaseCapture(true);
            drawing = presented = null; hookedDraw = false; Pending.Clear(); State.Reset();
            windowCursorOwners.Clear();
            RestoreSystemCursor();
        }
        private static void RestoreSystemCursor()
        {
            NativeCursor.Dispose();
            if (restoreCursor && Game1.instance != null) Game1.instance.IsMouseVisible = originalCursor;
            restoreCursor = false;
        }
        internal static void Update()
        {
            try
            {
                var game = Game1.instance;
                bool focused = game != null && game.IsActive && MouseButtons.GetForegroundWindow() == game.Window.Handle;
                MouseState mouse = Mouse.GetState();
                if (WindowCursorActive)
                {
                    State.Hide(); ReleaseCapture(true);
                    if (!focused) { RestoreSystemCursor(); return; }
                    if (!restoreCursor) { originalCursor = game.IsMouseVisible; restoreCursor = true; }
                    Rectangle client = game.Window.ClientBounds; Point screen = game.GetScreenSize();
                    int scale = Math.Max(1, (int)Math.Round(game.GetGameRect().Width * (double)client.Width / Math.Max(1, screen.X) / 480));
                    game.IsMouseVisible = NativeCursor.Apply(System.Windows.Forms.Control.FromHandle(game.Window.Handle), scale);
                    return;
                }
                bool hasUi = presented != null;
                if (hasUi && focused)
                {
                    if (!restoreCursor) { originalCursor = game.IsMouseVisible; restoreCursor = true; }
                }
                else RestoreSystemCursor();
                Point mapped;
                bool inside = false;
                if (game == null) mapped = Point.Zero;
                else
                {
                    Rectangle client = game.Window.ClientBounds; Point screen = game.GetScreenSize();
                    Point pixel = new Point(mouse.X * screen.X / Math.Max(1, client.Width), mouse.Y * screen.Y / Math.Max(1, client.Height));
                    inside = PointerState.Map(pixel, game.GetGameRect(), out mapped);
                }
                bool keyboard = PointerState.HidesCursor(Keyboard.GetState());
                if (!keyboard && ControllerManager.instance != null)
                    keyboard = ControllerManager.instance.GetConnectedPads().Any(p => p.GetPad().GetSaveIdentifier() != "pc_keyboard_jump_king"
                        && p.GetPad().GetPressedButtons().Length != 0);
                Poll(mapped, inside, focused, mouse.LeftButton == ButtonState.Pressed,
                    mouse.RightButton == ButtonState.Pressed, mouse.ScrollWheelValue, keyboard);
                if (hasUi && focused)
                {
                    bool native = false;
                    if (!nativeCursorFailed)
                        try
                        {
                            Rectangle client = game.Window.ClientBounds;
                            Point screen = game.GetScreenSize();
                            int scale = Math.Max(1, (int)Math.Round(game.GetGameRect().Width * (double)client.Width / Math.Max(1, screen.X) / 480));
                            native = NativeCursor.Apply(System.Windows.Forms.Control.FromHandle(game.Window.Handle), scale);
                        }
                        catch (Exception error)
                        {
                            NativeCursor.Dispose(); nativeCursorFailed = true;
                            PointerDiagnostics.Status("Software cursor fallback: " + error.GetBaseException().Message);
                        }
                    game.IsMouseVisible = native && Visible;
                }
            }
            catch (Exception error)
            {
                PointerDiagnostics.Status("Input error: " + error.GetBaseException().Message);
                Reset(); Console.WriteLine("[JK Runtime UI] Pointer disabled for this frame: " + error.GetBaseException().Message);
            }
        }
        internal static void Poll(Point position, bool inside, bool focused, bool left, bool right, int wheel, bool keyboard)
        {
            Pending.Clear();
            bool allowed = presented != null && presented.Enabled;
            State.Poll(position, inside, focused, allowed, left, right, wheel, keyboard);
            PointerDiagnostics.Observe(presented == null ? null : presented.Owner, allowed, focused, inside, left, right, keyboard);
            if (captured != null)
            {
                if (!allowed || !focused || !inside || keyboard || !ReferenceEquals(captureOwner, presented.Owner)) ReleaseCapture(true);
                else if (!left) ReleaseCapture(false);
                else if (State.Moved) captured.Drag(position);
                return;
            }
            if (!State.Visible || !State.Inside || !allowed) return;
            Surface target = presented;
            if (State.Back) { Queue(target.Owner, UiAction.Cancel); return; }
            var regions = target.Regions;
            RegionEntry hit = regions.LastOrDefault(r => r.Scroll == null && r.Bounds.Contains(position));
            if (hit != null)
            {
                if (State.Click && hit.Drag != null)
                { captured = hit; captureOwner = target.Owner; hit.Drag(position); return; }
                if ((State.Moved || State.Click) && hit.Hover != null) hit.Hover();
                if (State.Click && hit.Click != null) hit.Click();
            }
            if (State.Wheel != 0)
            {
                RegionEntry scroll = regions.LastOrDefault(r => r.Scroll != null && r.Bounds.Contains(position));
                if (scroll != null) scroll.Scroll(State.Wheel);
                else Queue(target.Owner, State.Wheel > 0 ? UiAction.Up : UiAction.Down);
            }
        }

        internal static readonly string[] Cursor = {
            "X...........", "XWX.........", "XWWX........", "XWWWX.......", "XWWWWX......",
            "XWWWWWX.....", "XWWWWWWX....", "XWWWWWWWX...", "XWWWWWGGGX..", "XWWWXXXXX...",
            "XWWXGGX.....", "XWX.XGGX....", "XX..XGGX....", ".....XGX....", "......X....." };
        internal static void DrawCursor(Point point)
        {
            var pixel = Game1.instance.contentManager.Pixel.texture;
            for (int y = 0; y < Cursor.Length; y++) for (int x = 0; x < Cursor[y].Length; x++)
                if (Cursor[y][x] != '.') Game1.spriteBatch.Draw(pixel, new Rectangle(point.X + x + 1, point.Y + y + 1, 1, 1), new Color(0, 0, 0, 180));
            for (int y = 0; y < Cursor.Length; y++) for (int x = 0; x < Cursor[y].Length; x++)
            {
                char c = Cursor[y][x]; if (c == '.') continue;
                Game1.spriteBatch.Draw(pixel, new Rectangle(point.X + x, point.Y + y, 1, 1), c == 'X' ? new Color(28, 24, 17) : c == 'W' ? UiTheme.Text : UiTheme.Gold);
            }
        }
    }

    internal sealed class PointerState
    {
        // Physical keys count even when unbound. Escape can navigate back in mouse mode.
        internal static bool HidesCursor(KeyboardState keyboard)
        { return keyboard.GetPressedKeys().Any(key => key != Keys.Escape); }
        internal bool Visible, Inside, Click, Back, Moved;
        internal Point Position;
        internal int Wheel;
        private bool armed, lastLeft, lastRight, initialized;
        private int lastWheel, remainder;
        internal void Hide() { Visible = false; }
        internal void Reset() { Visible = Inside = armed = initialized = false; remainder = 0; }
        internal static bool Map(Point point, Rectangle viewport, out Point mapped)
        {
            mapped = Point.Zero;
            if (viewport.Width <= 0 || viewport.Height <= 0 || !viewport.Contains(point)) return false;
            mapped = new Point((int)((long)(point.X - viewport.X) * 480 / viewport.Width), (int)((long)(point.Y - viewport.Y) * 360 / viewport.Height));
            return true;
        }
        internal void Poll(Point point, bool inside, bool focus, bool ui, bool left, bool right, int wheel, bool keyboard)
        {
            Click = Back = false; Wheel = 0; Moved = point != Position; Position = point; Inside = inside;
            int delta = initialized ? wheel - lastWheel : 0; initialized = true; lastWheel = wheel;
            bool pressed = left && !lastLeft, back = right && !lastRight; lastLeft = left; lastRight = right;
            if (!focus || !ui) { Visible = false; armed = false; remainder = 0; return; }
            if (!left && !right) armed = true;
            if (keyboard) { Visible = false; remainder = 0; return; }
            if (!inside) { remainder = 0; return; }
            if (!Visible)
            {
                if (pressed && armed) { Visible = true; Moved = false; }
                remainder = 0; return; // Activation never clicks through to a menu action.
            }
            Click = pressed && armed; Back = back && armed;
            remainder += delta; Wheel = Math.Max(-8, Math.Min(8, remainder / 120)); remainder %= 120;
        }
    }
}
