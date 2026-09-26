using System;
using System.Reflection;
using JumpKing;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace WardrobePlus
{
    internal struct CrystalPixel
    {
        internal int X, Y, Dx, Dy;
        internal float Coverage, Transmission;
        internal Color Surface;
        internal static CrystalPixel Create(Color input, int x, int y, bool edge)
        {
            int cx = x / 7, cy = y / 9;
            bool triangle = (x % 7) * 9 > (y % 9) * 7;
            bool seam = Math.Abs((x % 7) * 9 - (y % 9) * 7) < 6;
            var surface = MaterialBaker.ShadeRefractive(MaterialKind.Diamond, new Color(input.R * 255 / input.A, input.G * 255 / input.A, input.B * 255 / input.A),
                x, y, edge, (dx, dy) => Color.Transparent);
            return new CrystalPixel { X = x, Y = y, Dx = (cx * 3 + cy) % 5 - 2 + (triangle ? 1 : -1), Dy = (cy * 3 + cx) % 5 - 2,
                Coverage = input.A / 255f, Transmission = edge ? .12f : seam ? .16f : triangle ? .48f : .62f,
                Surface = Color.Lerp(surface, new Color(242,246,250), .18f) };
        }
    }

    internal sealed class CrystalSprite : Sprite, IDisposable
    {
        internal readonly CrystalPixel[] Pixels;
        internal readonly CrystalMaps Maps;
        internal readonly Rectangle MapSource;
        internal readonly Point MapOffset;
        private readonly bool ownsMaps;
        internal CrystalSprite(Sprite fallback, CrystalPixel[] pixels, CrystalMaps maps = null, Rectangle? mapSource = null, Point? mapOffset = null)
        {
            texture = fallback.texture; source = fallback.source; center = fallback.center; SetColor(fallback.GetColor()); Pixels = pixels;
            ownsMaps = maps == null; Maps = maps ?? CrystalMaps.FromPixels(texture.GraphicsDevice, source.Width, source.Height, pixels);
            MapSource = mapSource ?? new Rectangle(0,0,source.Width,source.Height); MapOffset = mapOffset ?? Point.Zero;
        }
        internal CrystalSprite Fitted(Sprite fallback, int x, int y)
        {
            var shifted = (CrystalPixel[])Pixels.Clone();
            for (int i = 0; i < shifted.Length; i++) { shifted[i].X += x; shifted[i].Y += y; }
            return new CrystalSprite(fallback, shifted, Maps, MapSource, new Point(MapOffset.X + x, MapOffset.Y + y));
        }
        public void Dispose() { if (ownsMaps) Maps.Dispose(); }
        public override void Draw(float x, float y, SpriteEffects effect = SpriteEffects.None) { Draw(new Vector2(x, y), effect); }
        public override void Draw(Point position, SpriteEffects effect = SpriteEffects.None) { Draw(position.ToVector2(), effect); }
        public override void Draw(Vector2 position, SpriteEffects effect = SpriteEffects.None)
        {
            Vector2 top = (position - source.Size.ToVector2() * center).ToPoint().ToVector2();
            if (!CrystalRenderer.Draw(this, top, Vector2.One, effect)) base.Draw(position, effect);
        }
        public override void Draw(Rectangle destination, SpriteEffects effect = SpriteEffects.None)
        {
            Vector2 top = destination.Location.ToVector2() - source.Size.ToVector2() * center;
            Vector2 scale = new Vector2(destination.Width / (float)source.Width, destination.Height / (float)source.Height);
            if (!CrystalRenderer.Draw(this, top, scale, effect)) base.Draw(destination, effect);
        }
        internal void DrawScaled(Vector2 anchor, float scale, SpriteEffects effect)
        {
            Vector2 top = anchor - source.Size.ToVector2() * center * scale;
            if (!CrystalRenderer.Draw(this, top, new Vector2(scale), effect))
                Game1.spriteBatch.Draw(texture, top, source, GetColor(), 0, Vector2.Zero, scale, effect, 0);
        }
    }

    // Capture the current color target on the GPU. Restore its entire contents
    // after rebinding (the game's target uses DiscardContents), then shade only
    // covered sprite pixels in a single shader pass. Optical maps are prepared once.
    internal static class CrystalRenderer
    {
        // Parked experiment: release builds use ordinary transparent Glass sprites.
        internal static bool ExperimentalEnabled
        {
            get {
#if WARDROBE_REFRACTION
                return true;
#else
                return false;
#endif
            }
        }
        private static RenderTarget2D snapshot;
        private static SpriteBatch batch;
        private static Effect crystalEffect;
        private static RasterizerState clipped;
        internal static bool Suspended;
        internal static int Captures;
        internal static int ShadedQuads;
        private static readonly BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;
        private static readonly FieldInfo Sort = Field("_sortMode"), Blend = Field("_blendState"), Sampler = Field("_samplerState"),
            Depth = Field("_depthStencilState"), Raster = Field("_rasterizerState"), Effect = Field("_effect"), MatrixField = Field("_matrix"), Begun = Field("_beginCalled");
        private static FieldInfo Field(string name) { return typeof(SpriteBatch).GetField(name, Flags); }
        internal static bool Supported { get { return Sort != null && Blend != null && Sampler != null && Depth != null && Raster != null && Effect != null && MatrixField != null && Begun != null; } }

        internal static bool Draw(CrystalSprite sprite, Vector2 top, Vector2 scale, SpriteEffects flip)
        {
            if (!ExperimentalEnabled) return false;
            if (sprite.Pixels.Length == 0 || sprite.GetColor().A == 0) return true;
            if (Suspended || !Supported || scale.X <= 0 || scale.Y <= 0) return false;
            var caller = Game1.spriteBatch;
            if (!(bool)Begun.GetValue(caller)) return false;
            var sort = (SpriteSortMode)Sort.GetValue(caller);
            var depth = (DepthStencilState)Depth.GetValue(caller);
            var effect = (Effect)Effect.GetValue(caller);
            var transform = (Matrix?)MatrixField.GetValue(caller);
            Matrix matrix = transform ?? Matrix.Identity;
            // Scene and UI use axis-aligned batches. Sorted/effect/depth passes
            // keep the neutral texture fallback without disturbing their contract.
            if ((sort != SpriteSortMode.Deferred && sort != SpriteSortMode.Immediate) || effect != null
                || (depth != null && (depth.DepthBufferEnable || depth.StencilEnable))
                || matrix.M12 != 0 || matrix.M21 != 0 || matrix.M14 != 0 || matrix.M24 != 0 || matrix.M11 <= 0 || matrix.M22 <= 0) return false;
            var device = caller.GraphicsDevice;
            var targets = device.GetRenderTargets();
            if (targets.Length != 1) return false;
            var target = targets[0].RenderTarget as RenderTarget2D;
            if (target == null || target.Format != SurfaceFormat.Color) return false;
            var blend = (BlendState)Blend.GetValue(caller);
            var sampler = (SamplerState)Sampler.GetValue(caller);
            var raster = (RasterizerState)Raster.GetValue(caller);
            var viewport = device.Viewport; var scissor = device.ScissorRectangle;
            var destination = Destination(sprite, top, scale, flip, matrix, viewport);
            Rectangle clip = viewport.Bounds;
            if (raster != null && raster.ScissorTestEnable) clip = Rectangle.Intersect(clip, scissor);
            if (Rectangle.Intersect(destination, clip).IsEmpty) return true;
            Ensure(device, target.Width, target.Height);
            var gpuBlend = device.BlendState; var gpuDepth = device.DepthStencilState; var gpuRaster = device.RasterizerState;
            var gpuSampler = device.SamplerStates[0];
            var gpuTexture = device.Textures[0];
            var gpuTexture1 = device.Textures[1]; var gpuTexture2 = device.Textures[2];
            var gpuSampler1 = device.SamplerStates[1]; var gpuSampler2 = device.SamplerStates[2];
            caller.End();
            bool ownOpen = false, restored = false;
            try
            {
                device.SetRenderTarget(snapshot);
                batch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone); ownOpen = true;
                batch.Draw(target, new Rectangle(0, 0, target.Width, target.Height), Color.White);
                batch.End(); ownOpen = false;
                device.SetRenderTargets(targets);
                device.Viewport = new Viewport(0, 0, target.Width, target.Height);
                batch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone); ownOpen = true;
                batch.Draw(snapshot, new Rectangle(0, 0, target.Width, target.Height), Color.White);
                batch.End(); ownOpen = false; restored = true;
                device.ScissorRectangle = clip;
                var region = sprite.MapSource;
                float atlasWidth = sprite.Maps.Surface.Width, atlasHeight = sprite.Maps.Surface.Height;
                bool flippedX = (flip & SpriteEffects.FlipHorizontally) != 0, flippedY = (flip & SpriteEffects.FlipVertically) != 0;
                crystalEffect.Parameters["Surface"].SetValue(sprite.Maps.Surface);
                crystalEffect.Parameters["MatrixTransform"].SetValue(Matrix.CreateOrthographicOffCenter(0, target.Width, target.Height, 0, 0, -1));
                crystalEffect.Parameters["Facets"].SetValue(sprite.Maps.Facets);
                crystalEffect.Parameters["Scene"].SetValue(snapshot);
                crystalEffect.Parameters["InverseSceneSize"].SetValue(new Vector2(1f / target.Width, 1f / target.Height));
                crystalEffect.Parameters["OffsetScale"].SetValue(new Vector2(scale.X * matrix.M11 * (flippedX ? -1 : 1), scale.Y * matrix.M22 * (flippedY ? -1 : 1)));
                crystalEffect.Parameters["Dispersion"].SetValue((float)Math.Round(scale.X * matrix.M11 * .7f));
                crystalEffect.Parameters["ScreenOrigin"].SetValue(new Vector2(flippedX ? destination.Right : destination.Left, flippedY ? destination.Bottom : destination.Top));
                crystalEffect.Parameters["ScreenExtent"].SetValue(new Vector2(destination.Width * (flippedX ? -1 : 1), destination.Height * (flippedY ? -1 : 1)));
                crystalEffect.Parameters["MapRegion"].SetValue(new Vector4(region.X / atlasWidth, region.Y / atlasHeight, region.Width / atlasWidth, region.Height / atlasHeight));
                batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, clipped, crystalEffect); ownOpen = true;
                batch.Draw(sprite.Maps.Surface, destination, region, sprite.GetColor(), 0, Vector2.Zero, flip, 0);
                ShadedQuads++;
                batch.End(); ownOpen = false; Captures++;
            }
            finally
            {
                if (ownOpen) batch.End();
                if (!restored)
                {
                    device.SetRenderTargets(targets);
                    device.Viewport = new Viewport(0, 0, target.Width, target.Height);
                    batch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.PointClamp);
                    batch.Draw(snapshot, new Rectangle(0, 0, target.Width, target.Height), Color.White); batch.End();
                }
                device.Viewport = viewport; device.ScissorRectangle = scissor;
                device.BlendState = gpuBlend; device.DepthStencilState = gpuDepth; device.RasterizerState = gpuRaster; device.SamplerStates[0] = gpuSampler;
                device.Textures[0] = gpuTexture;
                device.Textures[1] = gpuTexture1; device.Textures[2] = gpuTexture2;
                device.SamplerStates[1] = gpuSampler1; device.SamplerStates[2] = gpuSampler2;
                caller.Begin(sort, blend, sampler, depth, raster, effect, transform);
            }
            return true;
        }
        private static Rectangle Destination(CrystalSprite sprite, Vector2 top, Vector2 scale, SpriteEffects flip, Matrix matrix, Viewport viewport)
        {
            int x = (flip & SpriteEffects.FlipHorizontally) == 0 ? sprite.MapOffset.X : sprite.source.Width - sprite.MapOffset.X - sprite.MapSource.Width;
            int y = (flip & SpriteEffects.FlipVertically) == 0 ? sprite.MapOffset.Y : sprite.source.Height - sprite.MapOffset.Y - sprite.MapSource.Height;
            Vector2 a = Vector2.Transform(top + new Vector2(x * scale.X, y * scale.Y), matrix);
            Vector2 b = Vector2.Transform(top + new Vector2((x + sprite.MapSource.Width) * scale.X, (y + sprite.MapSource.Height) * scale.Y), matrix);
            int left = (int)Math.Round(a.X) + viewport.X, upper = (int)Math.Round(a.Y) + viewport.Y;
            return new Rectangle(left, upper, (int)Math.Round(b.X) + viewport.X - left, (int)Math.Round(b.Y) + viewport.Y - upper);
        }
        private static void Ensure(GraphicsDevice device, int width, int height)
        {
            if (snapshot != null && !snapshot.IsDisposed && snapshot.GraphicsDevice == device && snapshot.Width == width && snapshot.Height == height) return;
            Release();
            try
            {
                snapshot = new RenderTarget2D(device, width, height, false, SurfaceFormat.Color, DepthFormat.None);
                batch = new SpriteBatch(device);
                using (var stream = typeof(CrystalRenderer).Assembly.GetManifestResourceStream("WardrobePlus.Crystal.mgfxo"))
                using (var reader = new System.IO.BinaryReader(stream)) crystalEffect = new Effect(device, reader.ReadBytes((int)stream.Length));
                clipped = new RasterizerState { CullMode = CullMode.None, ScissorTestEnable = true };
            }
            catch { Release(); throw; }
        }
        internal static void Prepare(GraphicsDevice device) { Ensure(device, 480, 360); }
        internal static void Release()
        {
            if (snapshot != null) snapshot.Dispose(); if (batch != null) batch.Dispose(); if (crystalEffect != null) crystalEffect.Dispose(); if (clipped != null) clipped.Dispose();
            snapshot = null; batch = null; crystalEffect = null; clipped = null;
        }
    }
}
