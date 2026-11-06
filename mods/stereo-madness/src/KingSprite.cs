using System;
using System.Collections;
using System.Runtime.CompilerServices;
using JumpKing;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace StereoMadness
{
    // Flatten native outfit layers once per appearance, then map them onto the GD form
    internal sealed class KingSprite : Sprite, IDisposable, JKRuntime.Presentation.IAppearanceProjection
    {
        private readonly Controller controller;
        private Texture2D cube, ship;
        private int signature;
        internal KingSprite(Controller value) { controller = value; Refresh(); }
        internal void Refresh()
        {
            Sprite source = Game1.instance.contentManager.playerSprites.jump_charge;
            int hash=JKRuntime.Presentation.PlayerAppearance.Signature(source)^(int)JKRuntime.Presentation.PlayerAppearance.Revision;
            if(cube!=null&&hash==signature)return;
            var filtered=JKRuntime.Presentation.PlayerAppearance.Filter(source,JKRuntime.Presentation.AppearanceLayerRole.ExcludeFromForm|JKRuntime.Presentation.AppearanceLayerRole.WorldEffect);
            var sample=JKRuntime.Presentation.PlayerAppearance.FromSprite(filtered,Vector2.Zero,SpriteEffects.None,"charge");
            var pixels=JKRuntime.Presentation.AppearanceCapture.Read(sample,512,true);
            int w=pixels.Width,h=pixels.Height;var flat=pixels.Pixels;
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
        public Vector2 Project(Vector2 worldAnchor) {return controller.PlayerPosition;}
        public override void Draw(Vector2 position, SpriteEffects effects = SpriteEffects.None)
        {
            if (controller.Model.Dead) return;
            Texture2D t = controller.Model.Ship ? ship : cube;
            Game1.spriteBatch.Draw(t, position, null, Color.White, (float)controller.ViewRotation,
                new Vector2(t.Width / 2f, t.Height / 2f), 1f, SpriteEffects.None, 0);
        }
        public override void Draw(float x, float y, SpriteEffects effects = SpriteEffects.None) { Draw(new Vector2(x, y), effects); }
        public override void Draw(Point point, SpriteEffects effects = SpriteEffects.None) { Draw(point.ToVector2(), effects); }
        public override void Draw(Rectangle rect, SpriteEffects effects = SpriteEffects.None) { Draw(rect.Center.ToVector2(), effects); }
        public void Dispose() { if (cube != null) cube.Dispose(); if (ship != null) ship.Dispose(); cube = ship = null; }
    }
}
