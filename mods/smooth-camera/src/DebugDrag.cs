using System;
using System.Reflection;
using System.Runtime.InteropServices;
using EntityComponent;
using JumpKing;
using JumpKing.Controller;
using JumpKing.GameManager;
using JumpKing.Level;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace SmoothCamera
{
    internal struct DebugViewport
    {
        internal float X, Y, Width, Height;
        internal DebugViewport(Rectangle rect, Point output, Point client)
        {
            float x = client.X / (float)Math.Max(1, output.X), y = client.Y / (float)Math.Max(1, output.Y);
            X = rect.X * x; Y = rect.Y * y; Width = rect.Width * x; Height = rect.Height * y;
        }
        internal Vector2 Logical(Point point)
        { return new Vector2((point.X - X) * 480 / Width, (point.Y - Y) * 360 / Height); }
        internal Point Client(Vector2 point)
        { return new Point((int)Math.Round(X + point.X * Width / 480), (int)Math.Round(Y + point.Y * Height / 360)); }
    }

    internal sealed class DebugDragState
    {
        internal bool Active;
        internal Vector2 World;
        private Point sampled;
        internal void Reset() { Active = false; }
        internal Vector2 Move(Point mouse, DebugViewport viewport, Vector2 view, int screens)
        {
            if (!Active)
            {
                var point = viewport.Logical(mouse);
                World = new Vector2(CameraMotion.Clamp(point.X, 0, 480), CameraMotion.Clamp(point.Y, 0, 360)) - view;
            }
            else World += viewport.Logical(mouse) - viewport.Logical(sampled);
            World.X = CameraMotion.Clamp(World.X, -view.X, 480 - view.X);
            World.Y = CameraMotion.Clamp(World.Y, -(screens - 1) * 360, 360);
            sampled = mouse; Active = true;
            return World;
        }
        internal Point Reproject(Point mouse, DebugViewport viewport, Vector2 view)
        {
            // retain movement that arrived after the simulation sampled input
            var pending = mouse - sampled;
            sampled = viewport.Client(World + view);
            return sampled + pending;
        }
        internal static float Frame(float view, float player, AxisSettings options, float length, float min, float max, float native)
        {
            if (options.Mode == FollowMode.Screen) return CameraMotion.Clamp(native, min, max);
            float before = options.Mode == FollowMode.Window ? options.Window / 2 : options.Mode == FollowMode.JumpKing ? options.NegativeBand : 0;
            float after = options.Mode == FollowMode.Window ? options.Window / 2 : options.Mode == FollowMode.JumpKing ? options.PositiveBand : 0;
            return CameraMotion.Clamp(CameraMotion.Clamp(view, options.Focus * length - before - player,
                options.Focus * length + after - player), min, max);
        }
    }

    internal static class DebugDrag
    {
        internal static readonly MethodInfo Click = typeof(Game1).Assembly.GetType("JumpKing.Player.DebugTeleport", true)
            .GetMethod("Click", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo Overlay = typeof(PadInstance).GetField("_steam_overlay_active", BindingFlags.Static | BindingFlags.NonPublic);
        internal static readonly DebugDragState State = new DebugDragState();
        private static bool waitForRelease;
        private static int sampledScreen;
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
        private static bool WindowFocused
        {
            get { return Game1.instance != null && Game1.instance.IsActive
                && Game1.instance.Window != null && Game1.instance.Window.Handle != IntPtr.Zero
                && GetForegroundWindow() == Game1.instance.Window.Handle; }
        }
        private static bool Available
        {
            get { return LevelDebugState.instance != null && Game1.instance != null && Game1.instance.IsActive
                && !Renderer.TargetOverridden && !JKRuntime.Gameplay.NativePause.IsPaused && !JKRuntime.UI.UIApi.IsOpen
                && Overlay != null && !(bool)Overlay.GetValue(null); }
        }
        private static DebugViewport Viewport(Rectangle rect)
        {
            var game = Game1.instance;
            return new DebugViewport(rect, game.GetScreenSize(), game.Window.ClientBounds.Size);
        }
        internal static void Reset() { State.Reset(); waitForRelease = false; }
        private static void Suspend() { State.Reset(); waitForRelease = true; }
        internal static void ObserveWindow()
        {
            // MonoGame can still report active before Windows activation catches up
            if (LevelDebugState.instance != null && !WindowFocused) Suspend();
        }
        internal static bool BeforeClick(Component __instance, MouseState mouse)
        {
            // guard the native fallback too, a background click must do nothing
            if (!WindowFocused || !Available) { Suspend(); return false; }
            if (waitForRelease)
            {
                if (mouse.LeftButton == ButtonState.Released) waitForRelease = false;
                return false;
            }
            if (!Renderer.Running || !Renderer.CanPresent || !Renderer.Decision.Active || Renderer.Blocked
                || CameraControls.Gestures.Current == CameraMode.Focus)
            { State.Reset(); return true; }
            if (mouse.LeftButton != ButtonState.Pressed) { State.Reset(); return false; }
            var viewport = Viewport(Game1.instance.GetGameRect());
            if (viewport.Width <= 0 || viewport.Height <= 0) { Reset(); return false; }
            var pointer = viewport.Logical(mouse.Position);
            if (!State.Active && (pointer.X < 0 || pointer.X >= 480 || pointer.Y < 0 || pointer.Y >= 360))
            { Suspend(); return false; }
            var body = __instance.GetComponent<JumpKing.Player.BodyComp>();
            var bounds = body.GetHitbox();
            var view = new Vector2(Renderer.View.X, Renderer.View.Y);
            int screen = Camera.CurrentScreen;
            var actual = body.Position + new Vector2(bounds.Width * .5f, bounds.Height * .5f);
            var screens = (LevelScreen[])Renderer.Screens.GetValue(null);
            bool crossedRight = State.Active && State.World.X > 360 && actual.X < 120
                && PortalViews.Connects(screens, sampledScreen, false, State.World.Y, screen);
            bool crossedLeft = State.Active && State.World.X < 120 && actual.X > 360
                && PortalViews.Connects(screens, sampledScreen, true, State.World.Y, screen);
            if (crossedRight || crossedLeft)
            {
                // body teleports before debug input, don't write the old world back
                State.World = actual;
                view += new Vector2(crossedRight ? 480 : -480, (screen - sampledScreen) * 360);
            }
            var center = State.Move(mouse.Position, viewport, view, LevelManager.TotalScreens);
            body.Position = center - new Vector2(bounds.Width * .5f, bounds.Height * .5f);
            body.Velocity = Vector2.Zero;
            // zero-speed dragging bypasses native follow; collision queries need the new screen now
            Camera.UpdateCamera(body.GetHitbox().Center);
            sampledScreen = Camera.CurrentScreen;
            return false;
        }
        internal static void AfterUpdate()
        {
            ObserveWindow();
            if (!State.Active) return;
            if (!Available) { Suspend(); return; }
            if (!Renderer.Decision.Active || CameraControls.Gestures.Current == CameraMode.Focus
                || Mouse.GetState().LeftButton != ButtonState.Pressed) { State.Reset(); return; }
            // native wheel teleports run after Click, keep their resulting position
            var body = GameLoop.m_player.m_body;
            var bounds = body.GetHitbox();
            State.World = body.Position + new Vector2(bounds.Width * .5f, bounds.Height * .5f);
            sampledScreen = Camera.CurrentScreen;
        }
        internal static bool Frame(float previousY)
        {
            ObserveWindow();
            if (!State.Active) return false;
            if (!Available) { Suspend(); return false; }
            if (CameraControls.Gestures.Current == CameraMode.Focus || Mouse.GetState().LeftButton != ButtonState.Pressed)
            { State.Reset(); return false; }
            var motion = Renderer.Motion;
            float y = DebugDragState.Frame(previousY, State.World.Y, motion.Options, 360,
                motion.FirstScreen * 360, motion.LastScreen * 360, Camera.CurrentScreen * 360);
            motion.Place(y);
            Renderer.Horizontal.Drag(Renderer.View.X, State.World.X);
            Renderer.View.Place(Renderer.Horizontal.Translation, y, State.World.Y);
            return true;
        }
        internal static void Present(Rectangle destination)
        {
            ObserveWindow();
            if (!State.Active) return;
            if (!Available || !WindowFocused) { Suspend(); return; }
            if (CameraControls.Gestures.Current == CameraMode.Focus) { State.Reset(); return; }
            var mouse = Mouse.GetState();
            if (mouse.LeftButton != ButtonState.Pressed) { Reset(); return; }
            var viewport = Viewport(destination);
            if (viewport.Width <= 0 || viewport.Height <= 0) return;
            var point = State.Reproject(mouse.Position, viewport, new Vector2(Renderer.View.X, Renderer.View.Y));
            if (point != mouse.Position) Mouse.SetPosition(point.X, point.Y);
        }
    }
}
