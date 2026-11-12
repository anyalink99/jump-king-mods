using System;
using System.IO;
using System.Reflection;
using JumpKing;
using JumpKing.GameManager;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace JKRuntime
{
    public enum PixelSampling { Nearest, Sharp }

    /// <summary>Owns the final sampling policy and its GPU resources. Dispose on the game thread.</summary>
    public sealed class FrameSamplingLease : IDisposable
    {
        private PixelSampling mode;
        private bool disposed;
        internal readonly Effect Effect;
        internal OwnedPatches Hooks;
        public PixelSampling Mode
        {
            get { return mode; }
            set {
                RuntimeApi.Kernel.CheckThread();
                if (disposed) throw new ObjectDisposedException("FrameSamplingLease");
                if (!Enum.IsDefined(typeof(PixelSampling), value)) throw new ArgumentOutOfRangeException("value");
                mode = value;
            }
        }
        internal FrameSamplingLease(GraphicsDevice device, PixelSampling value)
        {
            Mode = value;
            using (var stream = typeof(FramePresentation).Assembly.GetManifestResourceStream("JKRuntime.PixelScale.mgfxo"))
            {
                if (stream == null) throw new InvalidOperationException("Runtime pixel filter resource is missing");
                using (var bytes = new MemoryStream()) { stream.CopyTo(bytes); Effect = new Effect(device, bytes.ToArray()); }
            }
        }
        public void Dispose()
        {
            RuntimeApi.Kernel.CheckThread(); if (disposed) return;
            if (Hooks != null) { Hooks.Dispose(); Hooks = null; }
            Effect.Dispose(); FramePresentation.Release(this); disposed = true;
        }
    }

    /// <summary>Shared final-presentation sampling. Cameras opt in without rounding their logical offsets.</summary>
    public static class FramePresentation
    {
        private static FrameSamplingLease current;
        private static readonly FieldInfo Target = typeof(Game1).GetField("m_render_target", OwnedPatches.Members);
        private static readonly Action<Game1> Calculate = (Action<Game1>)Delegate.CreateDelegate(typeof(Action<Game1>),
            typeof(Game1).GetMethod("CalculateGameRect", OwnedPatches.Members));
        public static bool Enabled { get { return current != null; } }
        public static PixelSampling Sampling { get { return current == null ? PixelSampling.Nearest : current.Mode; } }

        /// <summary>Acquire once for an explicit feature lifetime, after graphics initialization. Only one policy owner is allowed.</summary>
        public static FrameSamplingLease Acquire(GraphicsDevice device, PixelSampling mode)
        {
            RuntimeApi.Kernel.CheckThread();
            if (device == null || device.IsDisposed) throw new ArgumentException("A live graphics device is required", "device");
            if (current != null) throw new InvalidOperationException("Final sampling already has an owner");
            var lease = new FrameSamplingLease(device, mode);
            try {
                lease.Hooks = new OwnedPatches("jk-runtime.frame-sampling");
                lease.Hooks.Add(typeof(Game1).GetMethod("DrawRenderTarget", OwnedPatches.Members),
                    prefix: typeof(FramePresentation).GetMethod("Native", OwnedPatches.Members), priority: 800);
                current = lease; return lease;
            } catch { lease.Dispose(); throw; }
        }
        internal static void Release(FrameSamplingLease lease) { if (ReferenceEquals(current, lease)) current = null; }

        private static bool Native(Game1 __instance)
        {
            // the registered camera owns its viewport, including any scene flips
            if (current == null || FrameComposition.HasExternalPresentation) return true;
            Calculate(__instance);
            Rectangle rect = __instance.GetGameRect();
            if (JumpGame.screenShakeManager != null) rect.Location += JumpGame.screenShakeManager.GetOffset();
            var style = FrameComposition.Style;
            Draw(Game1.spriteBatch, (Texture2D)Target.GetValue(__instance), rect.Location.ToVector2(),
                new Vector2(rect.Width / 480f, rect.Height / 360f), style.Tint, style.Effects, Matrix.Identity, RasterizerState.CullNone);
            return false;
        }

        /// <summary>Draw one complete texture outside an existing batch. Colors must be premultiplied. Transform may translate or flip; scale carries the magnification.</summary>
        public static void Draw(SpriteBatch batch, Texture2D texture, Vector2 position, Vector2 scale,
            Color tint, SpriteEffects effects, Matrix transform, RasterizerState rasterizer)
        {
            RuntimeApi.Kernel.CheckThread();
            if (batch == null || texture == null) throw new ArgumentNullException(batch == null ? "batch" : "texture");
            if (!(scale.X > 0) || !(scale.Y > 0) || float.IsInfinity(scale.X) || float.IsInfinity(scale.Y))
                throw new ArgumentOutOfRangeException("scale");
            var mode = Sampling; var device = batch.GraphicsDevice;
            Effect filter = null;
            if (mode == PixelSampling.Sharp && scale.X >= 1 && scale.Y >= 1) {
                filter = current.Effect;
                if (!ReferenceEquals(filter.GraphicsDevice, device)) throw new InvalidOperationException("Sampling belongs to another graphics device");
                filter.Parameters["Source"].SetValue(texture);
                filter.Parameters["SourceSize"].SetValue(new Vector2(texture.Width, texture.Height));
                filter.Parameters["OutputScale"].SetValue(scale);
                filter.Parameters["MatrixTransform"].SetValue(transform * Matrix.CreateOrthographicOffCenter(0, device.Viewport.Width, device.Viewport.Height, 0, 0, 1));
            }
            batch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, mode == PixelSampling.Nearest ? SamplerState.PointClamp : SamplerState.LinearClamp,
                DepthStencilState.None, rasterizer ?? RasterizerState.CullNone, filter, transform);
            try { batch.Draw(texture, position, null, tint, 0, Vector2.Zero, scale, effects, 0); }
            finally { batch.End(); }
        }
    }
}
