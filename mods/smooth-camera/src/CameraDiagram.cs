using System;
using JumpKing;
using JKRuntime.UI;
using Microsoft.Xna.Framework;

namespace SmoothCamera
{
    internal sealed class CameraPreview
    {
        private readonly AxisMotion x = new AxisMotion(), y = new AxisMotion();
        private float time, playerX, playerY, previousX, previousY;
        internal void Update(CameraProfile profile, float delta)
        {
            if (delta <= 0 || float.IsNaN(delta) || float.IsInfinity(delta)) return;
            float dt = Math.Min(delta, .05f); time += dt;
            float phase = time % 12;
            // an ascent, a quiet landing, and a long fall use the production tracker
            float travel = phase < 4 ? -phase * 65 : phase < 6 ? -260 : phase < 10 ? -260 + (phase - 6) * 65 : 0;
            playerX = 240 + 160 * (float)Math.Sin(time * .7); playerY = -500 + travel;
            x.Options = profile.HorizontalAxis; y.Options = profile.Vertical;
            x.Observe(playerX, (playerX - previousX) / dt, 480, -240, 240, 0, false, false, profile.Horizontal);
            y.Observe(playerY, (playerY - previousY) / dt, 360, 0, 1440, (float)Math.Ceiling(-playerY / 360) * 360, false, false);
            x.Advance(dt, 48); y.Advance(dt, 48); previousX = playerX; previousY = playerY;
        }
        internal void Draw(Rectangle bounds, CameraProfile profile)
        {
            CameraDiagram.Fill(bounds, new Color(15, 19, 25));
            for (int row = 0; row < 6; row++)
            {
                float sy = 360 - row * 360 + y.Value;
                CameraDiagram.ScaledRect(bounds, new Rectangle(0, (int)sy, 480, 2), UiTheme.Border);
                CameraDiagram.ScaledRect(bounds, new Rectangle(60 + row % 2 * 180 + (int)x.Value, (int)sy - 70, 90, 6), new Color(65, 76, 87));
            }
            CameraDiagram.Window(bounds, profile);
            var king = new Rectangle((int)(playerX + x.Value) - 9, (int)(playerY + y.Value) - 14, 18, 28);
            CameraDiagram.ScaledRect(bounds, king, UiTheme.Text);
            CameraDiagram.ScaledRect(bounds, new Rectangle(king.X - 2, king.Y - 5, 22, 6), UiTheme.Gold);
            CameraDiagram.Outline(bounds, UiTheme.Border);
        }
    }
    internal static class CameraDiagram
    {
        internal static void Fill(Rectangle bounds, Color color)
        { if (bounds.Width > 0 && bounds.Height > 0) Game1.spriteBatch.Draw(Game1.instance.contentManager.Pixel.texture, bounds, color); }
        internal static void Outline(Rectangle r, Color color)
        {
            Fill(new Rectangle(r.X, r.Y, r.Width, 1), color); Fill(new Rectangle(r.X, r.Bottom - 1, r.Width, 1), color);
            Fill(new Rectangle(r.X, r.Y, 1, r.Height), color); Fill(new Rectangle(r.Right - 1, r.Y, 1, r.Height), color);
        }
        internal static void ScaledRect(Rectangle viewport, Rectangle rect, Color color)
        {
            float scale = viewport.Width / 480f;
            var draw = new Rectangle(viewport.X + (int)(rect.X * scale), viewport.Y + (int)(rect.Y * scale), Math.Max(1, (int)(rect.Width * scale)), Math.Max(1, (int)(rect.Height * scale)));
            Fill(Rectangle.Intersect(viewport, draw), color);
        }
        internal static void Window(Rectangle bounds, CameraProfile profile)
        {
            var h = profile.HorizontalAxis; var v = profile.Vertical;
            float sx = bounds.Width / 480f, sy = bounds.Height / 360f;
            float xn = h.Mode == FollowMode.Window ? h.Window / 2 : h.Mode == FollowMode.JumpKing ? h.NegativeBand : 0;
            float xp = h.Mode == FollowMode.Window ? h.Window / 2 : h.Mode == FollowMode.JumpKing ? h.PositiveBand : 0;
            float yn = v.Mode == FollowMode.Window ? v.Window / 2 : v.Mode == FollowMode.JumpKing ? v.NegativeBand : 0;
            float yp = v.Mode == FollowMode.Window ? v.Window / 2 : v.Mode == FollowMode.JumpKing ? v.PositiveBand : 0;
            int x = bounds.X + (int)(h.Focus * bounds.Width), y = bounds.Y + (int)(v.Focus * bounds.Height);
            var window = new Rectangle(x - (int)(xn * sx), y - (int)(yn * sy), Math.Max(1, (int)((xn + xp) * sx)), Math.Max(1, (int)((yn + yp) * sy)));
            if (!profile.Horizontal || h.Mode == FollowMode.Screen) { window.X = bounds.X; window.Width = bounds.Width; }
            if (v.Mode == FollowMode.Screen) { window.Y = bounds.Y; window.Height = bounds.Height; }
            Outline(Rectangle.Intersect(bounds, window), UiTheme.Cyan);
            Fill(new Rectangle(x - 3, y, 7, 1), UiTheme.Gold); Fill(new Rectangle(x, y - 3, 1, 7), UiTheme.Gold);
        }
        internal static void DrawDiagnostics(CameraDecision decision, float x, float y, float playerY, float playerX)
        {
            var bounds = new Rectangle(0, 0, 480, 360);
            if (decision.Profile == null) return;
            Window(bounds, decision.Profile);
            foreach (var zone in MapPolicy.Current.Zones)
            {
                var area = zone.Bounds; area.X += (int)x; area.Y += (int)y - (zone.Screen - 1) * 360;
                area = Rectangle.Intersect(bounds, area);
                if (area.Width > 0 && area.Height > 0) Outline(area, zone.Mode == CameraRule.Native ? UiTheme.Red : UiTheme.Gold);
            }
            Outline(Rectangle.Intersect(bounds, new Rectangle((int)(playerX + x) - 3, (int)(playerY + y) - 3, 7, 7)), UiTheme.Text);
            Fill(new Rectangle(8, 326, 464, 26), Color.Black * .8f);
            UiTheme.TextLine(decision.Reason + " | Screens " + (decision.First + 1) + "-" + (decision.Last + 1), new Vector2(14, 332), UiTheme.Text, true);
        }
    }
}
