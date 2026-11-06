using System;
using JumpKing;
using JKRuntime.UI;
using Microsoft.Xna.Framework;

namespace SmoothCamera
{
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
