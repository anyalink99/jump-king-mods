using System;
using System.Collections;
using System.Runtime.CompilerServices;
using JumpKing;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace StereoMadness
{
    // Flatten native outfit layers once per appearance, then map them onto the GD form.
    internal sealed class KingSprite : Sprite, IDisposable
    {
        private readonly Controller controller;
        private Texture2D cube, ship;
        private int signature;
        internal KingSprite(Controller value) { controller = value; Refresh(); }
        private static IList Layers(Sprite sprite)
        { var p = sprite.GetType().GetProperty("Sprites"); return p == null ? new Sprite[] { sprite } : (IList)p.GetValue(sprite, null); }
        internal void Refresh()
        {
            Sprite source = Game1.instance.contentManager.playerSprites.jump_charge;
            IList layers = Layers(source); int hash = 17;
            unchecked { foreach (Sprite s in layers) hash = hash * 31 + s.source.GetHashCode() + s.GetColor().GetHashCode() + (s.texture == null ? 0 : RuntimeHelpers.GetHashCode(s.texture)); }
            if (cube != null && hash == signature) return;
            Rectangle bounds = Rectangle.Empty;
            foreach (Sprite s in layers) if (s.texture != null) {
                var rect = new Rectangle((-s.source.Size.ToVector2() * s.center).ToPoint(), s.source.Size);
                bounds = bounds.IsEmpty ? rect : Rectangle.Union(bounds, rect);
            }
            int w = Math.Max(1, bounds.Width), h = Math.Max(1, bounds.Height);
            var flat = new Color[w * h];
            foreach (Sprite s in layers) if (s.texture != null) {
                var pixels = new Color[s.source.Width * s.source.Height]; s.texture.GetData(0, s.source, pixels, 0, pixels.Length);
                Point offset = (-s.source.Size.ToVector2() * s.center).ToPoint() - bounds.Location;
                Color tint = s.GetColor();
                for (int y = 0; y < s.source.Height; y++) for (int x = 0; x < s.source.Width; x++) {
                    Color p = pixels[y * s.source.Width + x]; int i = (y + offset.Y) * w + x + offset.X;
                    var f = new Color(p.R * tint.R * tint.A / 65025, p.G * tint.G * tint.A / 65025, p.B * tint.B * tint.A / 65025, p.A * tint.A / 255);
                    Color b = flat[i]; int inv = 255 - f.A;
                    flat[i] = new Color(Math.Min(255, f.R + b.R * inv / 255), Math.Min(255, f.G + b.G * inv / 255), Math.Min(255, f.B + b.B * inv / 255), Math.Min(255, f.A + b.A * inv / 255));
                }
            }
            var square = new Color[30 * 30];
            int left = w - 1, top = h - 1, right = 0, bottom = 0;
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) if (flat[y * w + x].A > 32) {
                left = Math.Min(left, x); right = Math.Max(right, x); top = Math.Min(top, y); bottom = Math.Max(bottom, y);
            }
            int cropW = Math.Max(1, right - left + 1), cropH = Math.Max(1, bottom - top + 1);
            Color edge = new Color(16, 20, 32), baseColor = Color.White;
            for (int i = 0; i < flat.Length; i++) if (flat[i].A > 32) { baseColor = flat[i]; break; }
            for (int y = 0; y < 30; y++) for (int x = 0; x < 30; x++) {
                int sx = left + x * cropW / 30, sy = top + y * cropH / 30;
                Color p = flat[Math.Min(h - 1, sy) * w + Math.Min(w - 1, sx)];
                if (p.A <= 32) for (int radius = 1; radius < Math.Max(cropW, cropH) && p.A <= 32; radius++)
                    for (int dx = -radius; dx <= radius; dx++) {
                        int xx = Math.Max(left, Math.Min(right, sx + dx));
                        int yy = Math.Max(top, Math.Min(bottom, sy + (dx == -radius || dx == radius ? 0 : radius)));
                        Color candidate = flat[yy * w + xx]; if (candidate.A > 32) { p = candidate; break; }
                    }
                square[y * 30 + x] = x < 2 || y < 2 || x >= 28 || y >= 28 ? edge : p.A > 32 ? p : baseColor;
            }
            var flying = new Color[40 * 30];
            for (int y = 0; y < 30; y++) for (int x = 0; x < 40; x++) {
                if (x >= 7 && x < 27 && y >= 1 && y < 21) flying[y * 40 + x] = square[(y - 1) * 30 / 20 * 30 + (x - 7) * 30 / 20];
                if (y >= 18 && y < 27 && x >= y - 18 && x < 40 - (y - 18) * 2) flying[y * 40 + x] = y == 18 || y == 26 ? edge : baseColor;
                if (x < 6 && y >= 20 && y < 25) flying[y * 40 + x] = Color.OrangeRed;
            }
            Texture2D nextCube = Make(square, 30, 30), nextShip = null;
            try { nextShip = Make(flying, 40, 30); } catch { nextCube.Dispose(); throw; }
            Dispose(); cube = nextCube; ship = nextShip; signature = hash;
        }
        private static Texture2D Make(Color[] data, int w, int h)
        { var t = new Texture2D(Game1.instance.GraphicsDevice, w, h); try { t.SetData(data); return t; } catch { t.Dispose(); throw; } }
        public override void Draw(Vector2 position, SpriteEffects effects = SpriteEffects.None)
        {
            if (controller.Model.Dead) return;
            Texture2D t = controller.Model.Ship ? ship : cube;
            Game1.spriteBatch.Draw(t, controller.PlayerPosition, null, Color.White, (float)controller.ViewRotation,
                new Vector2(t.Width / 2f, t.Height / 2f), 1f, SpriteEffects.None, 0);
        }
        public override void Draw(float x, float y, SpriteEffects effects = SpriteEffects.None) { Draw(new Vector2(x, y), effects); }
        public override void Draw(Point point, SpriteEffects effects = SpriteEffects.None) { Draw(point.ToVector2(), effects); }
        public override void Draw(Rectangle rect, SpriteEffects effects = SpriteEffects.None) { Draw(rect.Center.ToVector2(), effects); }
        public void Dispose() { if (cube != null) cube.Dispose(); if (ship != null) ship.Dispose(); cube = ship = null; }
    }
}
