using System;
using System.Linq;
using JKRuntime.Simulation;
using JKRuntime.UI;
using JumpKing;
using JumpKing.Level;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ScreenSolver
{
    internal sealed class SolverPage : IUiPage, IUiPageInputPolicy
    {
        private SolveCapture capture;
        private readonly string error;
        private readonly UiFrame frame = new UiFrame(new Rectangle(5, 5, 470, 350));
        private RoutePoint[] route = new RoutePoint[0];
        private string[] instructions = new string[0];
        private int screen, cursor;
        private bool close, disposed, finished;
        public bool WantsClose { get { return close; } }
        public bool HandlesCancel { get { return true; } }
        internal SolverPage(SolveCapture value, string failure)
        { capture = value; error = failure; if (value != null) screen = value.Seed.Pose.Screen; }
        public void OnOpen() { }
        public void OnClose() { if (disposed) return; disposed = true; if (capture != null) capture.Dispose(); capture = null; route = new RoutePoint[0]; }
        public void Update(UiInput input, float delta)
        {
            if (disposed) { close = true; return; }
            if (input.Action == UiAction.Cancel) { close = true; if (capture != null) capture.Dispose(); return; }
            if (capture == null) return;
            if (!finished)
            {
                capture.Search.Advance(6, 192);
                finished = capture.Search.Status != SearchStatus.Running;
                if (finished) { route = capture.Search.Route; instructions = capture.Search.Instructions; }
            }
            if (input.Action == UiAction.Up) screen = Math.Min(capture.Screens.Length - 1, screen + 1);
            if (input.Action == UiAction.Down) screen = Math.Max(0, screen - 1);
            if (route.Length > 0 && (input.Action == UiAction.Left || input.Action == UiAction.Right))
            {
                cursor = Math.Max(0, Math.Min(route.Length - 1, cursor + (input.Action == UiAction.Right ? 1 : -1) * 5));
                screen = route[cursor].Pose.Screen;
            }
        }
        private static void Text(string text, int x, int y, int width, Color color)
        { UiTheme.TextLine(UiTheme.FitText(text, width, true), new Vector2(x, y), color, true); }
        private static void Fill(Rectangle r, Color c)
        { Game1.spriteBatch.Draw(Game1.instance.contentManager.Pixel.texture, r, c); }
        private static Vector2 Plot(Vector2 point, int screen)
        { return new Vector2(15 + point.X * .55f, 66 + (point.Y + screen * 360) * .55f); }
        private static void Line(Vector2 a, Vector2 b, Color color)
        {
            var delta = b - a;
            Game1.spriteBatch.Draw(Game1.instance.contentManager.Pixel.texture, a, null, color,
                (float)Math.Atan2(delta.Y, delta.X), Vector2.Zero, new Vector2(delta.Length(), 1), SpriteEffects.None, 0);
        }
        public void Draw()
        {
            frame.Draw(); Text("SCREEN SOLVER", 17, 18, 440, UiTheme.Gold);
            if (capture == null)
            {
                UiTheme.WrappedText(error ?? "Solver closed", new Rectangle(20, 65, 438, 230), UiTheme.Red);
                UiTheme.CommandBar(new Rectangle(20, 318, 435, 22), new UiCommand("ESC", "Close")); return;
            }
            var search = capture.Search;
            Text(finished ? search.Status.ToString() : "Searching " + search.DirectionLabel + "... " + search.SimulatedTicks + " ticks", 17, 37, 440, UiTheme.Cyan);
            Fill(new Rectangle(14, 65, 266, 200), UiTheme.Ink);
            foreach (var block in (IBlock[])typeof(LevelScreen).GetField("m_hitboxes", NativeWorld.Flags).GetValue(capture.Screens[screen]))
            {
                var r = block.GetRect(); // Loaded colliders already have world coordinates.
                var bounds = Rectangle.Intersect(new Rectangle(15 + (int)(r.X * .55f), 66 + (int)((r.Y + screen * 360) * .55f),
                    Math.Max(1, (int)(r.Width * .55f)), Math.Max(1, (int)(r.Height * .55f))), new Rectangle(15, 66, 264, 198));
                Color c = block is WaterBlock ? new Color(23, 63, 105) : block is IceBlock ? new Color(115, 207, 230)
                    : block is SnowBlock ? Color.White : block is SandBlock ? UiTheme.Gold : block is NoWindBlock ? new Color(45, 60, 65) : UiTheme.Border;
                var slope = block as SlopeBlock;
                if (slope != null)
                {
                    // Use captured collision lines, not an assumed triangle
                    // orientation: native and corrected bottom-left differ.
                    var edges = (ErikMaths.Line[])typeof(SlopeBlock).GetField("m_lines", NativeWorld.Flags).GetValue(slope);
                    for (int y = bounds.Top; y < bounds.Bottom; y++)
                    {
                        float min = float.PositiveInfinity, max = float.NegativeInfinity, scan = y + .5f;
                        foreach (var edge in edges)
                        {
                            var a = Plot(edge.p0.ToVector2(), screen); var b = Plot(edge.p1.ToVector2(), screen);
                            if (!(a.Y <= scan && b.Y > scan || b.Y <= scan && a.Y > scan)) continue;
                            float x = a.X + (scan - a.Y) * (b.X - a.X) / (b.Y - a.Y);
                            min = Math.Min(min, x); max = Math.Max(max, x);
                        }
                        if (float.IsInfinity(min) || float.IsInfinity(max)) continue;
                        int left = (int)Math.Ceiling(min - .5f), right = (int)Math.Ceiling(max - .5f);
                        var row = Rectangle.Intersect(new Rectangle(left, y, Math.Max(0, right - left), 1), new Rectangle(15, 66, 264, 198));
                        if (row.Width > 0 && row.Height > 0) Fill(row, c);
                    }
                }
                else if (bounds.Width > 0 && bounds.Height > 0) Fill(bounds, c);
            }
            if (capture.Seed.Pose.Screen == screen) Dot(capture.Seed.Pose.Position, UiTheme.Gold, 4);
            for (int i = 0; i < route.Length; i++)
            {
                var point = route[i];
                if (point.Pose.Screen != screen) continue;
                var p = Plot(point.Pose.Position + new Vector2(9, 13), screen);
                if (!Inside(p)) continue;
                if (i > 0 && route[i - 1].Pose.Screen == screen && !point.Events.Any(e => e.Kind.StartsWith("teleport")))
                {
                    var a = Plot(route[i - 1].Pose.Position + new Vector2(9, 13), screen);
                    if (Inside(a)) Line(a, p, UiTheme.Cyan);
                }
                if (point.Events.Any(e => e.Kind == "takeoff")) Dot(point.Pose.Position, UiTheme.Gold, 3);
            }
            Text("Screen " + (screen + 1) + " / " + capture.Screens.Length, 17, 274, 262, UiTheme.Muted);
            Text("ACTIONS", 291, 66, 164, UiTheme.Gold);
            for (int i = 0; i < Math.Min(12, instructions.Length); i++) Text((i + 1) + ". " + instructions[i], 291, 86 + i * 14, 166, UiTheme.Text);
            if (route.Length > 0)
            {
                var selected = route[cursor]; Dot(selected.Pose.Position, Color.White, 4);
                var wind = selected.Events.FirstOrDefault(e => e.Kind == "wind" || e.Kind == "wind-vertical");
                Text("t+" + (selected.Tick - capture.Seed.Tick) + "  " + (selected.Input.Jump ? "CHARGE" : "RELEASE") +
                    "  " + (selected.Input.Direction < 0 ? "LEFT" : selected.Input.Direction > 0 ? "RIGHT" : "-"), 17, 290, 440, UiTheme.Text);
                if (wind != null) Text("Wind " + (wind.Value == 0 ? "CALM " : wind.Kind == "wind-vertical" ?
                    (wind.Value < 0 ? "UP " : "DOWN ") : (wind.Value < 0 ? "LEFT " : "RIGHT ")) +
                    wind.Value.ToString("0.0000"), 291, 274, 166, UiTheme.Cyan);
            }
            else UiTheme.WrappedText(search.Detail, new Rectangle(291, 90, 165, 167), UiTheme.Muted);
            bool timed = capture.Timed;
            Text(search.HigherPriorityUnresolved ? "Higher-priority route unresolved (budget)." : timed ? "Wind: captured phase only; world time continues." : "Simulation preview. No autoplay. Close to resume.",
                17, 307, 440, timed || search.HigherPriorityUnresolved ? UiTheme.Gold : UiTheme.Muted);
            UiTheme.CommandBar(new Rectangle(17, 328, 438, 20), new UiCommand("UP/DN", "Screen"), new UiCommand("L/R", "Time"), new UiCommand("ESC", "Close"));
        }
        private static bool Inside(Vector2 p) { return p.X >= 15 && p.X < 279 && p.Y >= 66 && p.Y < 264; }
        private void Dot(Vector2 world, Color color, int size)
        { var p = Plot(world + new Vector2(9, 13), screen); if (Inside(p)) Fill(new Rectangle((int)p.X - size / 2, (int)p.Y - size / 2, size, size), color); }
    }
}
