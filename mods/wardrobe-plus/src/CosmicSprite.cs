using System;
using System.Diagnostics;
using System.Reflection;
using JumpKing;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace WardrobePlus
{
    internal sealed class CosmicSprite : Sprite
    {
        internal readonly Texture2D Mask;
        internal readonly Rectangle MaskSource;
        internal readonly Point MaskOffset;
        internal CosmicSprite(Sprite fallback, Texture2D mask, Rectangle region, Point offset)
        { texture = fallback.texture; source = fallback.source; center = fallback.center; SetColor(fallback.GetColor()); Mask = mask; MaskSource = region; MaskOffset = offset; }
        internal CosmicSprite Fitted(Sprite fallback, int x, int y)
        { return new CosmicSprite(fallback, Mask, MaskSource, new Point(MaskOffset.X + x, MaskOffset.Y + y)); }
        public override void Draw(float x, float y, SpriteEffects effect = SpriteEffects.None) { Draw(new Vector2(x,y), effect); }
        public override void Draw(Point position, SpriteEffects effect = SpriteEffects.None) { Draw(position.ToVector2(), effect); }
        public override void Draw(Vector2 position, SpriteEffects effect = SpriteEffects.None)
        {
            var top = (position - source.Size.ToVector2() * center).ToPoint().ToVector2();
            if (!CosmicRenderer.Draw(this, top, Vector2.One, effect, true)) base.Draw(position, effect);
        }
        public override void Draw(Rectangle destination, SpriteEffects effect = SpriteEffects.None)
        {
            var top = destination.Location.ToVector2() - source.Size.ToVector2() * center;
            var scale = new Vector2(destination.Width / (float)source.Width, destination.Height / (float)source.Height);
            if (!CosmicRenderer.Draw(this, top, scale, effect, true)) base.Draw(destination, effect);
        }
        internal void DrawScaled(Vector2 anchor, float scale, SpriteEffects flip)
        {
            var top = anchor - source.Size.ToVector2() * center * scale;
            if (!CosmicRenderer.Draw(this, top, new Vector2(scale), flip, false))
                Game1.spriteBatch.Draw(texture, top, source, GetColor(), 0, Vector2.Zero, scale, flip, 0);
        }
    }

    // One masked quad. Never binds/copies a render target or reads GPU pixels.
    internal static class CosmicRenderer
    {
        private static Effect shader;
        private static Texture2D nebula, stars;
        private static readonly Stopwatch clock = Stopwatch.StartNew();
        private static float? frameTime;
        internal static void EndFrame() { frameTime = null; }
        private static float Time()
        {
            if (TestTime.HasValue) return TestTime.Value;
            if (!frameTime.HasValue) frameTime = (float)(clock.Elapsed.TotalSeconds % 4096);
            return frameTime.Value;
        }
        internal static float? TestTime = null;
        internal static bool Suspended = false;
        internal static int Draws;
        private static readonly BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;
        private static FieldInfo Field(string name) { return typeof(SpriteBatch).GetField(name, Flags); }
        private static readonly MethodInfo CameraTransform = typeof(Camera).GetMethod("TransformVector2");
        private static readonly object[] CameraZero = { Vector2.Zero };
        private static readonly FieldInfo Sort = Field("_sortMode"), Blend = Field("_blendState"), Sampler = Field("_samplerState"),
            Depth = Field("_depthStencilState"), Raster = Field("_rasterizerState"), EffectField = Field("_effect"), MatrixField = Field("_matrix"), Begun = Field("_beginCalled");
        internal static bool Supported { get { return Sort != null && Blend != null && Sampler != null && Depth != null && Raster != null && EffectField != null && MatrixField != null && Begun != null; } }
        internal static bool Draw(CosmicSprite sprite, Vector2 top, Vector2 scale, SpriteEffects flip, bool world)
        {
            if (sprite.GetColor().A == 0) return true;
            if (Suspended || !Supported || scale.X <= 0 || scale.Y <= 0) return false;
            var batch = Game1.spriteBatch;
            if (!(bool)Begun.GetValue(batch)) return false;
            var sort = (SpriteSortMode)Sort.GetValue(batch);
            var effect = (Effect)EffectField.GetValue(batch);
            var depth = (DepthStencilState)Depth.GetValue(batch);
            var transform = (Matrix?)MatrixField.GetValue(batch);
            if ((sort != SpriteSortMode.Deferred && sort != SpriteSortMode.Immediate) || effect != null
                || (depth != null && (depth.DepthBufferEnable || depth.StencilEnable))) return false;
            Matrix matrix = transform ?? Matrix.Identity;
            if (matrix.M12 != 0 || matrix.M21 != 0 || matrix.M14 != 0 || matrix.M24 != 0 || matrix.M11 <= 0 || matrix.M22 <= 0) return false;
            var device = batch.GraphicsDevice;
            var blend = (BlendState)Blend.GetValue(batch); var sampler = (SamplerState)Sampler.GetValue(batch); var raster = (RasterizerState)Raster.GetValue(batch);
            int ox = (flip & SpriteEffects.FlipHorizontally) == 0 ? sprite.MaskOffset.X : sprite.source.Width - sprite.MaskOffset.X - sprite.MaskSource.Width;
            int oy = (flip & SpriteEffects.FlipVertically) == 0 ? sprite.MaskOffset.Y : sprite.source.Height - sprite.MaskOffset.Y - sprite.MaskSource.Height;
            Vector2 position = top + new Vector2(ox * scale.X, oy * scale.Y);
            var a = Vector2.Transform(position, matrix); var b = Vector2.Transform(position + sprite.MaskSource.Size.ToVector2() * scale, matrix);
            var viewport = device.Viewport;
            var clip = new Rectangle(0,0,viewport.Width,viewport.Height);
            if (raster != null && raster.ScissorTestEnable)
            { var scissor = device.ScissorRectangle; scissor.Offset(-viewport.X,-viewport.Y); clip = Rectangle.Intersect(clip,scissor); }
            if (b.X <= clip.Left || a.X >= clip.Right || b.Y <= clip.Top || a.Y >= clip.Bottom) return true;
            Prepare(device);
            var texture0 = device.Textures[0]; var texture1 = device.Textures[1]; var texture2 = device.Textures[2];
            var sampler0 = device.SamplerStates[0]; var sampler1 = device.SamplerStates[1]; var sampler2 = device.SamplerStates[2];
            var gpuBlend = device.BlendState; var gpuDepth = device.DepthStencilState; var gpuRaster = device.RasterizerState;
            batch.End(); bool open = false;
            try
            {
                shader.Parameters["MatrixTransform"].SetValue(matrix * Matrix.CreateOrthographicOffCenter(0,viewport.Width,viewport.Height,0,0,-1));
                // Invoke the current native entry point: do not inline an unpatched
                // camera transform before Smooth Camera installs its render hooks.
                shader.Parameters["CameraOrigin"].SetValue(world ? (Vector2)CameraTransform.Invoke(null,CameraZero) : Vector2.Zero);
                shader.Parameters["Clock"].SetValue(Time());
                shader.Parameters["Mask"].SetValue(sprite.Mask);
                shader.Parameters["Nebula"].SetValue(nebula); shader.Parameters["Stars"].SetValue(stars);
                batch.Begin(SpriteSortMode.Deferred, blend, SamplerState.PointClamp, depth, raster, shader, transform); open = true;
                batch.Draw(sprite.Mask, position, sprite.MaskSource, sprite.GetColor(), 0, Vector2.Zero, scale, flip, 0);
                batch.End(); open = false; Draws++;
            }
            finally
            {
                if (open) batch.End();
                device.Textures[0] = texture0; device.Textures[1] = texture1; device.Textures[2] = texture2;
                device.SamplerStates[0] = sampler0; device.SamplerStates[1] = sampler1; device.SamplerStates[2] = sampler2;
                device.BlendState = gpuBlend; device.DepthStencilState = gpuDepth; device.RasterizerState = gpuRaster;
                batch.Begin(sort, blend, sampler, depth, raster, effect, transform);
            }
            return true;
        }
        internal static void Prepare(GraphicsDevice device)
        {
            if (shader != null && !shader.IsDisposed && shader.GraphicsDevice == device) return;
            Release();
            try
            {
                var assembly = typeof(CosmicRenderer).Assembly;
                using (var stream = assembly.GetManifestResourceStream("WardrobePlus.Cosmic.mgfxo"))
                using (var reader = new System.IO.BinaryReader(stream)) shader = new Effect(device, reader.ReadBytes((int)stream.Length));
                using (var stream = assembly.GetManifestResourceStream("WardrobePlus.cosmic-nebula.png")) nebula = Texture2D.FromStream(device,stream);
                using (var stream = assembly.GetManifestResourceStream("WardrobePlus.cosmic-stars.png")) stars = Texture2D.FromStream(device,stream);
            }
            catch { Release(); throw; }
        }
        internal static void Release()
        {
            if (shader != null) shader.Dispose(); if (nebula != null) nebula.Dispose(); if (stars != null) stars.Dispose();
            shader = null; nebula = stars = null;
        }
    }
}
