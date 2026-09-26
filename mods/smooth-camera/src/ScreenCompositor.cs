using System;
using JumpKing;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SmoothCamera
{
    internal sealed class ScreenCompositor : IDisposable
    {
        private RenderTarget2D atlas, screenTarget;
        private RasterizerState clipped;
        private JKRuntime.RuntimeScope resources = new JKRuntime.RuntimeScope();
        private int cachedRevision = -1, topScreen = -1, bottomScreen = -1;
        private int cachedLeft = -1, cachedRight = -1, cachedAnchor, side;
        private bool wide;
        internal int SceneRenders { get; private set; }

        internal void Draw(float translation, int screenCount, Action<int> drawScreen, bool defer = false, int revision = -1,
            float horizontal = 0, int left = -1, int right = -1, int anchor = 0)
        {
            var device = Game1.spriteBatch.GraphicsDevice;
            bool scene = JKRuntime.FrameComposition.HasScene;
            EnsureTargets(device, left >= 0 || right >= 0, scene);
            int bottom = Math.Max(0, Math.Min(screenCount - 1, (int)Math.Floor(translation / CameraMotion.Height)));
            int top = Math.Max(bottom, Math.Min(screenCount - 1, (int)Math.Ceiling(translation / CameraMotion.Height)));
            int visibleSide = horizontal > 0 ? -1 : horizontal < 0 ? 1 : 0;
            bool refresh = revision < 0 || cachedRevision != revision || topScreen != top || bottomScreen != bottom
                || cachedLeft != left || cachedRight != right || cachedAnchor != anchor || side != visibleSide;
            if (!refresh && defer)
            {
                // The world is already captured. Clear the UI layer without
                // rebinding its discard target or allocating a binding array.
                Game1.instance.EndBatch(); device.Clear(Color.Transparent); Game1.instance.StartBatch();
                return;
            }
            var targets = device.GetRenderTargets(); var viewport = device.Viewport; var scissor = device.ScissorRectangle;
            Game1.instance.EndBatch();
            bool batchOpen = false;
            try
            {
                if (refresh)
                {
                    cachedRevision = -1; topScreen = bottomScreen = -1;
                    device.SetRenderTarget(atlas); device.Clear(Color.Black);
                    for (int column = -1; column <= 1; column++)
                    for (int row = top; row >= bottom; row--)
                    {
                        if (column != 0 && (!wide || column != visibleSide)) continue;
                        int destination = column < 0 ? left : right;
                        int index = column == 0 ? row : destination < 0 ? -1 : destination + row - anchor;
                        if (index < 0 || index >= screenCount) continue;
                        var screenViewport = new Viewport((column + (wide ? 1 : 0)) * CameraMotion.Width, (top - row) * CameraMotion.Height, CameraMotion.Width, CameraMotion.Height);
                        if (scene) { device.SetRenderTarget(screenTarget); device.Clear(Color.Black); }
                        else device.Viewport = screenViewport;
                        Game1.instance.StartBatch(); batchOpen = true;
                        using (new RenderContext(index, 0, column, row))
                        {
                            if (scene) using (new JKRuntime.FrameComposition.ScreenPass(screenTarget)) drawScreen(index);
                            else drawScreen(index);
                        }
                        Game1.instance.EndBatch(); batchOpen = false;
                        if (scene)
                        {
                            device.SetRenderTarget(atlas); device.Viewport = screenViewport;
                            Game1.instance.StartBatch(); batchOpen = true;
                            Game1.spriteBatch.Draw(screenTarget, Vector2.Zero, Color.White);
                            Game1.instance.EndBatch(); batchOpen = false;
                        }
                    }
                    topScreen = top; bottomScreen = bottom; cachedRevision = revision;
                    cachedLeft = left; cachedRight = right; cachedAnchor = anchor; side = visibleSide; SceneRenders++;
                }
            }
            finally
            {
                try { if (batchOpen) Game1.instance.EndBatch(); }
                finally
                {
                    device.SetRenderTargets(targets); device.Viewport = viewport; device.ScissorRectangle = scissor;
                    // The native target is the stationary UI layer during deferred
                    // presentation; never rely on DiscardContents retaining pixels.
                    device.Clear(defer ? Color.Transparent : Color.Black);
                    Game1.instance.StartBatch();
                }
            }
            if (!defer) Game1.spriteBatch.Draw(atlas, new Vector2(horizontal - (wide ? CameraMotion.Width : 0), translation - topScreen * CameraMotion.Height), Color.White);
        }

        internal void Present(Rectangle destination, Texture2D overlay, float translation, float horizontal = 0, JKRuntime.FrameStyle? requestedStyle = null)
        {
            if (atlas == null || topScreen < 0) return;
            var device = Game1.spriteBatch.GraphicsDevice;
            var previousScissor = device.ScissorRectangle; var previousRasterizer = device.RasterizerState;
            try
            {
                device.ScissorRectangle = Rectangle.Intersect(destination, device.Viewport.Bounds);
                var scale = new Vector2(destination.Width / (float)CameraMotion.Width, destination.Height / (float)CameraMotion.Height);
                var position = new Vector2(destination.X + (horizontal - (wide ? CameraMotion.Width : 0)) * scale.X, destination.Y + (translation - topScreen * CameraMotion.Height) * scale.Y);
                var style = requestedStyle ?? JKRuntime.FrameStyle.Default;
                bool flipX = (style.Effects & SpriteEffects.FlipHorizontally) != 0;
                bool flipY = (style.Effects & SpriteEffects.FlipVertically) != 0;
                var transform = Matrix.CreateScale(flipX ? -1 : 1, flipY ? -1 : 1, 1)
                    * Matrix.CreateTranslation(flipX ? 2 * destination.X + destination.Width : 0,
                        flipY ? 2 * destination.Y + destination.Height : 0, 0);
                // Translate at output resolution, retaining fractional logical
                // pixels. Point sampling keeps the original pixel art sharp.
                Game1.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, clipped, null, transform);
                Game1.spriteBatch.Draw(atlas, position, null, style.Tint, 0, Vector2.Zero, scale, SpriteEffects.None, 0);
                Game1.spriteBatch.Draw(overlay, destination, style.Tint);
                Game1.spriteBatch.End();
            }
            finally { device.ScissorRectangle = previousScissor; device.RasterizerState = previousRasterizer; }
        }

        internal void Capture(Texture2D overlay, float translation, float horizontal)
        {
            var device = overlay.GraphicsDevice;
            var targets = device.GetRenderTargets(); var viewport = device.Viewport; var scissor = device.ScissorRectangle;
            using (var frame = new RenderTarget2D(device, CameraMotion.Width, CameraMotion.Height))
            try
            {
                device.SetRenderTarget(frame); device.Clear(Color.Black);
                Present(new Rectangle(0, 0, CameraMotion.Width, CameraMotion.Height), overlay, translation, horizontal);
                device.SetRenderTarget(null);
                JKRuntime.FrameComposition.Capture(frame);
            }
            finally { device.SetRenderTargets(targets); device.Viewport = viewport; device.ScissorRectangle = scissor; }
        }

        private void EnsureTargets(GraphicsDevice device, bool horizontal, bool scene)
        {
            if (atlas != null && !atlas.IsDisposed && ReferenceEquals(atlas.GraphicsDevice, device) && wide == horizontal && (screenTarget != null) == scene) return;
            Dispose();
            try
            {
                wide = horizontal;
                atlas = resources.Own(new RenderTarget2D(device, CameraMotion.Width * (wide ? 3 : 1), CameraMotion.Height * 2, false,
                    SurfaceFormat.Color, DepthFormat.None, 0, scene ? RenderTargetUsage.PreserveContents : RenderTargetUsage.DiscardContents));
                if (scene) screenTarget = resources.Own(new RenderTarget2D(device, CameraMotion.Width, CameraMotion.Height));
                clipped = resources.Own(new RasterizerState { CullMode = CullMode.None, ScissorTestEnable = true });
            }
            catch { Dispose(); throw; }
        }

        public void Dispose()
        {
            cachedRevision = topScreen = bottomScreen = -1;
            resources.Dispose();
            resources = new JKRuntime.RuntimeScope();
            atlas = screenTarget = null; clipped = null;
        }
    }
}
