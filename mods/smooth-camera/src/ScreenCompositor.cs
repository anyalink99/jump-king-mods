using System;
using System.Collections.Generic;
using JumpKing;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SmoothCamera
{
    // capture native views separately so their final placement stays fractional
    internal sealed class ScreenCompositor : IDisposable
    {
        private sealed class ScreenCapture { internal ScreenImage Image; internal RenderTarget2D Target; }
        private readonly List<ScreenCapture> captures = new List<ScreenCapture>();
        // shipped Total Eclipse modules discover these fields for transparent video layers
        // both target aliases borrow the current capture; there's no second atlas
        private RenderTarget2D atlas, screenTarget;
        private int cachedAnchor;
        private RasterizerState clipped;
        private int cachedRevision = -1;
        private RenderTarget2D samplingAtlas;
        private Vector2 samplingOrigin;
        private bool samplingDirty = true;
        internal int SceneRenders { get; private set; }
        internal void Draw(float translation, int screenCount, Action<int> drawScreen, bool defer = false, int revision = -1,
            float horizontal = 0, int left = -1, int right = -1, int anchor = 0)
        {
            cachedAnchor = anchor;
            var images = new List<ScreenImage>();
            int bottom = Math.Max(0, Math.Min(screenCount - 1, (int)Math.Floor(translation / 360)));
            int top = Math.Max(bottom, Math.Min(screenCount - 1, (int)Math.Ceiling(translation / 360)));
            for (int row = top; row >= bottom; row--)
            {
                images.Add(new ScreenImage(row, Vector2.Zero));
                int destination = horizontal > 0 ? left : horizontal < 0 ? right : -1;
                int linked = destination + row - anchor;
                if (destination >= 0 && linked >= 0 && linked < screenCount)
                    images.Add(new ScreenImage(linked, new Vector2(horizontal > 0 ? -480 : 480, (destination - anchor) * 360)));
            }
            DrawImages(images.ToArray(), new Vector2(horizontal, translation), drawScreen, defer, revision, false, true);
        }
        internal void DrawPlan(ScreenImage[] images, Vector2 view, Action<int> drawScreen, bool defer, int revision, bool composition = true, int anchor = 0)
        { cachedAnchor = anchor; DrawImages(images, view, drawScreen, defer, revision, true, composition); }
        private void DrawImages(ScreenImage[] images, Vector2 view, Action<int> drawScreen, bool defer, int revision, bool planned, bool composition)
        {
            var device = Game1.spriteBatch.GraphicsDevice;
            bool changed = captures.Count != images.Length;
            if (!changed) for (int i = 0; i < images.Length; i++)
                if (captures[i].Image.Screen != images[i].Screen || captures[i].Image.Offset != images[i].Offset) { changed = true; break; }
            bool refresh = changed || revision < 0 || cachedRevision != revision || planned && Renderer.SubjectDrawOffset != Vector2.Zero;
            if (changed)
            {
                foreach (var capture in captures) capture.Target.Dispose(); captures.Clear();
                foreach (var image in images) captures.Add(new ScreenCapture { Image = image,
                    Target = new RenderTarget2D(device, 480, 360, false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents) });
            }
            if (clipped == null) clipped = new RasterizerState { CullMode = CullMode.None, ScissorTestEnable = true };
            var targets = device.GetRenderTargets(); var viewport = device.Viewport; var scissor = device.ScissorRectangle;
            Game1.instance.EndBatch(); bool open = false;
            try
            {
                if (refresh)
                {
                    samplingDirty = true;
                    cachedRevision = -1;
                    foreach (var capture in captures)
                    {
                        atlas = screenTarget = capture.Target;
                        device.SetRenderTarget(capture.Target); device.Clear(Color.Black);
                        Game1.instance.StartBatch(); open = true;
                        var image = capture.Image;
                        int column = (int)Math.Round(image.Offset.X / 480), row = image.Screen - (int)Math.Round(image.Offset.Y / 360);
                        using (new RenderContext(image.Screen, 0, column, row, planned ? (Vector2?)image.Offset : null))
                        using (composition ? new JKRuntime.FrameComposition.WorldPass(Camera.Offset + new Vector2(0, image.Screen * 360)) : null)
                        {
                            if (composition && JKRuntime.FrameComposition.HasScene)
                                using (new JKRuntime.FrameComposition.ScreenPass(capture.Target)) drawScreen(image.Screen);
                            else drawScreen(image.Screen);
                        }
                        Game1.instance.EndBatch(); open = false;
                    }
                    cachedRevision = revision; SceneRenders++;
                }
                // build while capturing; rebinding a final target can discard its UI/margins
                if (JKRuntime.FramePresentation.Enabled && captures.Count > 0) PrepareSamplingAtlas();
            }
            finally
            {
                try { if (open) Game1.instance.EndBatch(); }
                finally
                { atlas = screenTarget = null;
                    device.SetRenderTargets(targets); device.Viewport = viewport; device.ScissorRectangle = scissor;
                    device.Clear(defer ? Color.Transparent : Color.Black); Game1.instance.StartBatch(); }
            }
            if (!defer) foreach (var capture in captures)
                Game1.spriteBatch.Draw(capture.Target, capture.Image.Offset + new Vector2(view.X, view.Y - capture.Image.Screen * 360), Color.White);
        }
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        internal void Present(Rectangle destination, Texture2D overlay, float translation, float horizontal = 0, JKRuntime.FrameStyle? requestedStyle = null)
        { PresentImages(destination, overlay, translation, horizontal, requestedStyle, true); }
        // map-owned Present hooks belong to the game viewport, not the settings preview
        internal void PresentPreview(Rectangle destination, Texture2D overlay, float translation, float horizontal)
        { PresentImages(destination, overlay, translation, horizontal, null); }
        private void PresentImages(Rectangle destination, Texture2D overlay, float translation, float horizontal, JKRuntime.FrameStyle? requestedStyle, bool final = false)
        {
            if (captures.Count == 0) return;
            var device = Game1.spriteBatch.GraphicsDevice;
            var oldScissor = device.ScissorRectangle; var oldRasterizer = device.RasterizerState;
            var style = requestedStyle ?? JKRuntime.FrameStyle.Default;
            bool flipX = (style.Effects & SpriteEffects.FlipHorizontally) != 0, flipY = (style.Effects & SpriteEffects.FlipVertically) != 0;
            var transform = Matrix.CreateScale(flipX ? -1 : 1, flipY ? -1 : 1, 1)
                * Matrix.CreateTranslation(flipX ? 2 * destination.X + destination.Width : 0, flipY ? 2 * destination.Y + destination.Height : 0, 0);
            var scale = new Vector2(destination.Width / 480f, destination.Height / 360f);
            try
            {
                if (final && JKRuntime.FramePresentation.Enabled && samplingAtlas != null && !samplingDirty)
                {
                    device.ScissorRectangle = Rectangle.Intersect(destination, device.Viewport.Bounds);
                    // stitch first so filtering can read both sides of a screen seam
                    JKRuntime.FramePresentation.Draw(Game1.spriteBatch, samplingAtlas,
                        destination.Location.ToVector2() + (samplingOrigin + new Vector2(horizontal, translation)) * scale,
                        scale, style.Tint, SpriteEffects.None, transform, clipped);
                    JKRuntime.FramePresentation.Draw(Game1.spriteBatch, overlay, destination.Location.ToVector2(),
                        scale, style.Tint, SpriteEffects.None, transform, clipped);
                    return;
                }
                device.ScissorRectangle = Rectangle.Intersect(destination, device.Viewport.Bounds);
                Game1.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, clipped, null, transform);
                foreach (var capture in captures)
                {
                    var offset = capture.Image.Offset + new Vector2(horizontal, translation - capture.Image.Screen * 360);
                    Game1.spriteBatch.Draw(capture.Target, destination.Location.ToVector2() + offset * scale, null, style.Tint, 0, Vector2.Zero, scale, SpriteEffects.None, 0);
                }
                Game1.spriteBatch.Draw(overlay, destination, style.Tint); Game1.spriteBatch.End();
            }
            finally { device.ScissorRectangle = oldScissor; device.RasterizerState = oldRasterizer; }
        }
        private void PrepareSamplingAtlas()
        {
            if (!samplingDirty && samplingAtlas != null) return;
            Vector2 low = new Vector2(float.MaxValue), high = new Vector2(float.MinValue);
            foreach (var capture in captures) {
                var origin = capture.Image.Offset - new Vector2(0, capture.Image.Screen * 360);
                low = Vector2.Min(low, origin); high = Vector2.Max(high, origin + new Vector2(480, 360));
            }
            int width = (int)(high.X - low.X), height = (int)(high.Y - low.Y);
            var device = Game1.spriteBatch.GraphicsDevice;
            if (samplingAtlas == null || samplingAtlas.Width != width || samplingAtlas.Height != height) {
                if (samplingAtlas != null) samplingAtlas.Dispose();
                samplingAtlas = new RenderTarget2D(device, width, height, false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
            }
            var targets = device.GetRenderTargets(); var viewport = device.Viewport; var scissor = device.ScissorRectangle;
            try {
                device.SetRenderTarget(samplingAtlas); device.Clear(Color.Black);
                Game1.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.PointClamp);
                try {
                    foreach (var capture in captures)
                        Game1.spriteBatch.Draw(capture.Target, capture.Image.Offset - new Vector2(0, capture.Image.Screen * 360) - low, Color.White);
                } finally { Game1.spriteBatch.End(); }
                samplingOrigin = low; samplingDirty = false;
            } finally { device.SetRenderTargets(targets); device.Viewport = viewport; device.ScissorRectangle = scissor; }
        }
        internal void Capture(Texture2D overlay, float translation, float horizontal)
        {
            var device = overlay.GraphicsDevice; var targets = device.GetRenderTargets(); var viewport = device.Viewport; var scissor = device.ScissorRectangle;
            using (var frame = new RenderTarget2D(device, 480, 360))
            try { device.SetRenderTarget(frame); device.Clear(Color.Black); PresentImages(new Rectangle(0, 0, 480, 360), overlay, translation, horizontal, null);
                device.SetRenderTarget(null); JKRuntime.FrameComposition.Capture(frame); }
            finally { device.SetRenderTargets(targets); device.Viewport = viewport; device.ScissorRectangle = scissor; }
        }
        public void Dispose()
        { atlas = screenTarget = null; cachedAnchor = 0;
            if (samplingAtlas != null) samplingAtlas.Dispose(); samplingAtlas = null; samplingDirty = true;
            foreach (var capture in captures) capture.Target.Dispose(); captures.Clear(); if (clipped != null) clipped.Dispose(); clipped = null; cachedRevision = -1; }
    }
}
