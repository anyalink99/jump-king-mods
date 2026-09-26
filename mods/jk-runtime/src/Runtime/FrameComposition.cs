using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace JKRuntime
{
    public struct FrameStyle
    {
        public readonly Color Tint;
        public readonly SpriteEffects Effects;
        public FrameStyle(Color tint, SpriteEffects effects) { Tint = tint; Effects = effects; }
        public static FrameStyle Default { get { return new FrameStyle(Color.White, SpriteEffects.None); } }
    }
    /// <summary>Scene effects run on a complete logical screen before a camera assembles its viewport. The supplied target is borrowed and must never be disposed.</summary>
    public interface ISceneCompositor
    {
        bool Active { get; }
        FrameStyle Style { get; }
        void Compose(RenderTarget2D target);
    }
    /// <summary>Optional explicit preview request. The presenter creates a logical frame only when requested; ordinary frames allocate nothing for capture.</summary>
    public interface ISceneFrameCapture
    {
        bool CaptureRequested { get; }
        void Capture(RenderTarget2D frame);
    }
    /// <summary>Explicit screen-target and final-blit ownership for scene and camera adapters. Registration belongs to activation; preparation never publishes a compositor.</summary>
    public static class FrameComposition
    {
        private static ISceneCompositor scene;
        private static Func<bool> presenterReady;
        [ThreadStatic] private static ScreenPass screenPass;
        public static bool HasScene { get { return scene != null && scene.Active; } }
        public static bool InScreenPass { get { return screenPass != null; } }
        public static bool HasExternalPresentation { get { return presenterReady != null && presenterReady(); } }
        public static FrameStyle Style { get { return HasScene ? scene.Style : FrameStyle.Default; } }
        public static bool CaptureRequested
        { get { var capture = scene as ISceneFrameCapture; return HasScene && capture != null && capture.CaptureRequested; } }
        public static void Capture(RenderTarget2D frame)
        { var capture = scene as ISceneFrameCapture; if (HasScene && capture != null && capture.CaptureRequested) capture.Capture(frame); }
        public static IDisposable RegisterScene(ISceneCompositor value)
        {
            RuntimeApi.Kernel.CheckThread();
            if (value == null || scene != null) throw new InvalidOperationException("Scene compositor is already owned or invalid");
            scene = value;
            return new ActionLease(delegate { RuntimeApi.Kernel.CheckThread(); if (!ReferenceEquals(scene,value)) throw new InvalidOperationException("Scene compositor ownership lost"); scene=null; });
        }
        public static IDisposable RegisterPresenter(Func<bool> ready)
        {
            RuntimeApi.Kernel.CheckThread();
            if (ready == null || presenterReady != null) throw new InvalidOperationException("Final presentation is already owned or invalid");
            presenterReady = ready;
            return new ActionLease(delegate { RuntimeApi.Kernel.CheckThread(); if (presenterReady!=ready) throw new InvalidOperationException("Final presentation ownership lost"); presenterReady=null; });
        }
        public sealed class ScreenPass : IDisposable
        {
            private readonly ScreenPass previous;
            internal readonly RenderTarget2D Target;
            private bool disposed;
            public ScreenPass(RenderTarget2D value)
            {
                RuntimeApi.Kernel.CheckThread();
                if (value == null) throw new ArgumentNullException("value");
                previous=screenPass; Target=value; screenPass=this;
            }
            public void Dispose()
            {
                RuntimeApi.Kernel.CheckThread();
                if (disposed) return;
                if (!ReferenceEquals(screenPass,this)) throw new InvalidOperationException("Screen passes must end in reverse order");
                screenPass=previous; disposed=true;
            }
        }
        public static void ComposeScreen()
        {
            if (screenPass == null) throw new InvalidOperationException("A complete logical screen target is required");
            if (HasScene) scene.Compose(screenPass.Target);
        }
    }
}
