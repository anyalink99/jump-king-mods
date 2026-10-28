using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Prism
{
    internal sealed class Renderer : IDisposable
    {
        private readonly GraphicsDevice device;
        private readonly SpriteBatch batch;
        private readonly Beatmap chart;
        private readonly Texture2D pixel, glow, disc, nebula;
        private readonly RenderTarget2D capture;
        private readonly Vector3[] stars = new Vector3[210];
        internal Vector2 Offset;
        internal WindFrame Wind;
        internal float WindVisibility = 1, GlowStrength = 1;
        private Rectangle ArtRect { get { return new Rectangle((int)Offset.X, (int)Offset.Y, 480, 360); } }
        private static readonly Rectangle Full = new Rectangle(0, 0, 480, 360);
        private static readonly Color[] Colors = { new Color(73, 216, 255), new Color(153, 107, 255), new Color(251, 107, 190), new Color(255, 181, 101), new Color(86, 230, 201) };
        internal Renderer(GraphicsDevice device, SpriteBatch batch, Beatmap chart)
        {
            this.device = device; this.batch = batch; this.chart = chart;
            try
            {
                pixel = new Texture2D(device, 1, 1); pixel.SetData(new[] { Color.White });
                glow = Radial(device, false); disc = Radial(device, true);
                nebula = new Texture2D(device, 480, 360);
                var data = new Color[480 * 360];
                for (int y = 0; y < 360; y++) for (int x = 0; x < 480; x++)
                {
                    double ridge = y - 215 + x * .26 + 19 * Math.Sin(x * .019);
                    double cloud = Math.Exp(-ridge * ridge / 4300) * (.48 + .22 * Math.Sin(x * .033 + y * .022) + .18 * Math.Cos(y * .047 - x * .009));
                    double dust = .5 + .5 * Math.Sin(x * .051 + Math.Sin(y * .037) * 2.5);
                    data[y * 480 + x] = new Color((float)(.012 + cloud * .078), (float)(.018 + cloud * .032), (float)(.035 + cloud * .14 + dust * .008), 1f);
                }
                nebula.SetData(data);
                var random = new Random(357);
                for (int i = 0; i < stars.Length; i++) stars[i] = new Vector3(random.Next(480), random.Next(360), (float)random.NextDouble());
                capture = new RenderTarget2D(device, 480, 360, false, SurfaceFormat.Color, DepthFormat.None);
            }
            catch { Dispose(); throw; }
        }
        private static Texture2D Radial(GraphicsDevice device, bool hard)
        {
            var data = new Color[128 * 128];
            for (int y = 0; y < 128; y++) for (int x = 0; x < 128; x++)
            {
                double r = Math.Sqrt((x - 63.5) * (x - 63.5) + (y - 63.5) * (y - 63.5)) / 63;
                float a = hard ? (float)Math.Max(0, Math.Min(1, (1 - r) * 32)) : (float)(Math.Exp(-r * r * 5) * Math.Max(0, 1 - r));
                data[y * 128 + x] = Color.White * a;
            }
            var t = new Texture2D(device, 128, 128); try { t.SetData(data); return t; } catch { t.Dispose(); throw; }
        }
        private void Line(Vector2 a, Vector2 b, Color color, float width)
        {
            Vector2 d = b - a;
            batch.Draw(pixel, a, null, color, (float)Math.Atan2(d.Y, d.X), new Vector2(0, .5f), new Vector2(d.Length(), width), SpriteEffects.None, 0);
        }
        private void Bloom(Vector2 center, float width, float height, Color color, float angle = 0)
        { batch.Draw(glow, center, null, color, angle, new Vector2(64), new Vector2(width / 128, height / 128), SpriteEffects.None, 0); }
        private void Circle(Vector2 c, float rx, float ry, Color color, float start, float end, float tilt, float thickness)
        {
            Vector2 previous = Vector2.Zero; float cs = (float)Math.Cos(tilt), sn = (float)Math.Sin(tilt);
            for (int i = 0; i <= 96; i++)
            {
                float a = start + (end - start) * i / 96;
                float x = (float)Math.Cos(a) * rx, y = (float)Math.Sin(a) * ry;
                var p = c + new Vector2(x * cs - y * sn, x * sn + y * cs);
                if (i > 0) Line(previous, p, color, thickness);
                previous = p;
            }
        }
        internal static Color Palette(double time)
        {
            double p = time / 13; int index = (int)Math.Floor(p); float f = (float)(p - index); f = f * f * (3 - 2 * f);
            return Color.Lerp(Colors[index % Colors.Length], Colors[(index + 1) % Colors.Length], f);
        }
        internal void Background(double time, int screen, ScreenArt art, bool gentle)
        {
            Color color = Palette(time); float pulse = chart.Pulse(time) * (gentle ? .22f : 1);
            batch.Draw(nebula, Full, Color.Lerp(Color.White, color, .37f));
            Bloom(new Vector2(330, 215), 590, 250, color * .12f, -.28f);
            for (int i = 0; i < stars.Length; i++)
            {
                Vector3 s = stars[i]; double drift = Wind.Offset * (.015 + s.Z * .035);
                float x = (float)((s.X + drift + screen * 17) % 480); if (x < 0) x += 480;
                float y = (s.Y + screen * 31) % 360;
                float light = .22f + s.Z * .5f + (float)Math.Sin(time * .6 + i * 7.1) * .12f;
                batch.Draw(pixel, new Rectangle((int)x, (int)y, 1, 1), Color.Lerp(color, Color.White, s.Z) * light);
                if (i % 31 == 0) { Bloom(new Vector2(x, y), 12, 12, color * (.18f + pulse * .2f)); Line(new Vector2(x - 2, y), new Vector2(x + 3, y), Color.White * .22f, 1); }
            }
            var center = new Vector2(278 + (float)Math.Sin(screen * .81) * 46, 115 + (float)Math.Sin(screen * .39) * 19);
            BlackHole(center, time, color, pulse);
            DrawNotes(time, color, gentle);
            DrawWind(art, color);
            batch.Draw(art.Halo, ArtRect, color * (.55f + pulse * .45f) * GlowStrength);
            batch.Draw(art.Fill, ArtRect, Color.White);
            batch.Draw(art.Edge, ArtRect, color * (.7f + pulse * .25f));
            batch.Draw(art.Materials, ArtRect, Color.White);
        }
        private void DrawWind(ScreenArt art, Color color)
        {
            float strength = Math.Min(2, Math.Abs(Wind.Velocity) / .1f);
            if (strength < .015f) return;
            float direction = Math.Sign(Wind.Velocity);
            for (int i = 0; i < 54; i++)
            {
                var star = stars[i];
                float x = (float)((star.X + Wind.Offset * (.6 + star.Z)) % 480); if (x < 0) x += 480;
                int y = (int)star.Y;
                // Sheltered areas remain visually still even when the screen has wind.
                if (art.ShelterCells[y * 480 + (int)x] != 0) continue;
                float length = 5 + strength * (8 + 9 * star.Z);
                float opacity = Math.Min(.7f, (.15f + strength * .18f) * WindVisibility);
                for (int segment = 3; segment >= 0; segment--)
                {
                    float a = x - direction * length * segment / 4;
                    float b = x - direction * length * (segment + 1) / 4;
                    WindLine(art, y, (int)Math.Min(a, b), (int)Math.Max(a, b), color * opacity * (1 - segment / 4f));
                }
                WindLine(art, y, (int)x, (int)x + 2, Color.Lerp(color, Color.White, .55f) * opacity);
            }
        }
        private void WindLine(ScreenArt art, int y, int left, int right, Color tint)
        {
            int x = Math.Max(0, left), end = Math.Min(480, right);
            while (x < end)
            {
                while (x < end && art.ShelterCells[y * 480 + x] != 0) x++;
                int start = x;
                while (x < end && art.ShelterCells[y * 480 + x] == 0) x++;
                if (x > start) batch.Draw(pixel, new Rectangle(start + (int)Offset.X, y + (int)Offset.Y, x - start, 1), tint);
            }
        }
        private void BlackHole(Vector2 c, double time, Color color, float pulse)
        {
            float tilt = -.19f, radius = 31 + pulse * 1.2f;
            Bloom(c, 275, 155, color * .23f);
            Bloom(c, 255, 36, new Color(255, 159, 111) * .32f, tilt);
            // Far side of the accretion disk is occluded by the event horizon.
            for (int i = 0; i < 44; i++)
            {
                float r = 42 + i * 1.3f;
                float light = (.42f + (float)Math.Sin(i * 1.91 + time * .7) * .11f) * (1 - i / 52f);
                Color tint = Color.Lerp(new Color(255, 223, 177), color, i / 44f) * light;
                Circle(c, r, r * .22f, tint, 0, MathHelper.TwoPi, tilt, 1.2f);
            }
            // Lensed upper arc and a faint lower image frame the black interior.
            for (int i = 9; i >= 0; i--)
                Circle(c, radius + 2 + i * .7f, radius + 2 + i * .65f, Color.Lerp(new Color(255, 223, 185), color, i / 9f) * (.12f + (9 - i) * .02f), MathHelper.Pi, MathHelper.TwoPi, tilt, 1.4f);
            batch.Draw(disc, c, null, new Color(1, 2, 6), 0, new Vector2(64), new Vector2(radius * 2 / 128), SpriteEffects.None, 0);
            Circle(c, radius + 1, radius + 1, new Color(255, 231, 208) * .68f, 0, MathHelper.TwoPi, 0, .75f);
            // The near side crosses in front, with orbiting hot fragments.
            for (int i = 0; i < 9; i++)
                Circle(c + new Vector2(0, 2), 43 + i * 5, 7 + i * 1.1f, Color.Lerp(new Color(255, 221, 175), color, i / 11f) * (.55f - i * .044f), 0, MathHelper.Pi, tilt, 1.2f);
            for (int i = 0; i < 15; i++)
            {
                double a = time * (.16 + i * .007) + i * 2.4; float r = 53 + i % 5 * 10;
                var p = c + new Vector2((float)Math.Cos(a) * r, (float)Math.Sin(a) * r * .23f);
                p.Y -= (p.X - c.X) * .19f;
                Bloom(p, 10, 5, new Color(255, 211, 172) * .42f, tilt);
            }
        }
        private Vector2 NotePoint(Vector2 p) { return new Vector2(28 + p.X / 512 * 424, 34 + p.Y / 384 * 270); }
        private void DrawNotes(double time, Color color, bool gentle)
        {
            if (gentle) return;
            int last = chart.LastNote(time);
            for (int i = last; i >= 0 && i >= last - 28; i--)
            {
                Note n = chart.Notes[i]; double age = time - n.Time;
                if (age > Math.Max(2.2, n.End - n.Time + .8)) continue;
                var p = NotePoint(n.Position);
                float life = Math.Max(0, 1 - (float)age / 1.7f), r = 4 + (float)age * (14 + n.Distance * 46);
                if (life > 0)
                {
                    Circle(p, r, r * .55f, color * life * .16f, 0, MathHelper.TwoPi, -.2f, .7f);
                    var tail = n.Direction * (float)age * (22 + 60 * n.Distance);
                    Bloom(p + tail, 18 * life, 18 * life, color * life * .24f);
                    Line(p + tail, p + tail - n.Direction * (5 + 15 * n.Distance), color * life * .3f, .8f);
                }
                if ((n.Kind & 2) != 0 && time <= n.End + .5)
                {
                    float a = (float)Math.Min(1, (n.End + .5 - time) * 2) * .27f;
                    for (int j = 1; j < n.Path.Length; j++) Line(NotePoint(n.Path[j - 1]), NotePoint(n.Path[j]), color * a, 1);
                    float progress = (float)Math.Min(1, age / Math.Max(.001, n.End - n.Time));
                    int seg = Math.Min(n.Path.Length - 2, (int)(progress * (n.Path.Length - 1)));
                    if (seg >= 0) Bloom(Vector2.Lerp(NotePoint(n.Path[seg]), NotePoint(n.Path[seg + 1]), progress * (n.Path.Length - 1) - seg), 19, 19, color * .42f);
                }
                if ((n.Kind & 8) != 0 && time <= n.End) Circle(new Vector2(240, 180), 125, 48, color * .18f, (float)time, (float)time + 4.5f, -.3f, 1);
            }
        }
        internal void Overlay(double time, ScreenArt art, bool gentle)
        {
            Color color = Palette(time); float pulse = chart.Pulse(time) * (gentle ? .2f : 1);
            batch.Draw(art.Halo, ArtRect, color * (.4f + pulse * .5f) * GlowStrength);
            batch.Draw(art.Edge, ArtRect, color * (.18f + pulse * .26f));
        }
        internal void Foreground(double time, ScreenArt art, bool mirrors, bool gentle)
        {
            batch.Draw(art.Water, ArtRect, Color.White);
            if (mirrors && art.Mirrors.Length > 0) Reflect(time, art);
            Color color = Palette(time); float pulse = chart.Pulse(time) * (gentle ? .2f : 1);
            batch.Draw(art.Halo, ArtRect, color * (.28f + pulse * .4f) * GlowStrength);
            batch.Draw(art.Edge, ArtRect, color * (.48f + pulse * .25f));
            batch.Draw(art.Materials, ArtRect, Color.White);
        }
        private void Reflect(double time, ScreenArt art)
        {
            var bindings = device.GetRenderTargets();
            if (bindings.Length != 1) return;
            var frame = bindings[0].RenderTarget as RenderTarget2D;
            if (frame == null || frame.Width != 480 || frame.Height != 360) return;
            var viewport = device.Viewport;
            batch.End();
            try
            {
                device.SetRenderTarget(capture); device.Clear(Color.Transparent);
                Begin(); batch.Draw(frame, Full, Color.White); batch.End();
                device.SetRenderTargets(bindings);
                // Native targets may discard their contents when rebound. Rebuild from the copy.
                device.Clear(Color.Transparent); Begin(); batch.Draw(capture, Full, Color.White); batch.End();
            }
            finally { device.SetRenderTargets(bindings); device.Viewport = viewport; Begin(); }
            foreach (var strip in art.Mirrors)
            {
                int dx = strip.X + (int)Offset.X, dy = strip.Y + (int)Offset.Y, sy = strip.SourceY + (int)Offset.Y;
                if (dy < 0 || dy >= 360 || sy < 0 || sy >= 360) continue;
                int left = Math.Max(0, dx), right = Math.Min(480, dx + strip.Width);
                if (right <= left) continue;
                int width = right - left;
                int shift = (int)(Math.Sin(time * 1.3 + strip.Y * .21) * strip.Depth / 10);
                int sx = Math.Max(0, Math.Min(480 - width, left + shift));
                float alpha = .30f * (1 - strip.Depth / 21f);
                batch.Draw(capture, new Rectangle(left, dy, width, 1), new Rectangle(sx, sy, width, 1), new Color(161, 201, 236) * alpha);
            }
        }
        internal void Begin() { batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp); }
        public void Dispose() { foreach (var t in new Texture2D[] { pixel, glow, disc, nebula, capture }) if (t != null) t.Dispose(); }
    }
}
