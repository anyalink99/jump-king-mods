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
    /// <summary>Scene effects run on a complete logical screen before a camera assembles its viewport. the supplied target is borrowed and must never be disposed</summary>
    public interface ISceneCompositor
    {
        bool Active { get; }
        FrameStyle Style { get; }
        void Compose(RenderTarget2D target);
    }
    /// <summary>optional explicit preview request. the presenter creates a logical frame only when requested, ordinary frames allocate nothing for capture</summary>
    public interface ISceneFrameCapture
    {
        bool CaptureRequested { get; }
        void Capture(RenderTarget2D frame);
    }
    /// <summary>explicit screen-target and final-blit ownership for scene and camera adapters. registration belongs to activation, preparation never publishes a compositor</summary>
    public static class FrameComposition
    {
        private static ISceneCompositor scene;
        private static Func<bool> presenterReady;
        private static Func<Microsoft.Xna.Framework.Vector2,Microsoft.Xna.Framework.Vector2> worldProjector;
        [ThreadStatic] private static ScreenPass screenPass;
        [ThreadStatic] private static WorldPass worldPass;
        private static Action worldDraw;
        public static bool HasScene { get { return scene != null && scene.Active; } }
        public static bool InScreenPass { get { return screenPass != null; } }
        public static bool InWorldPass { get { return worldPass != null; } }
        /// <summary>Draw world actors once per visible screen, before its foreground. Own the registration in the attempt scope.</summary>
        public static IDisposable RegisterWorldDraw(Action draw)
        {
            RuntimeApi.Kernel.CheckThread();if(draw==null)throw new ArgumentNullException("draw");
            worldDraw+=draw;return new ActionLease(delegate {RuntimeApi.Kernel.CheckThread();worldDraw-=draw;});
        }
        /// <summary>Invoke world actors in the current screen pass. Doesn't advance simulation or draw viewport UI.</summary>
        public static void DrawWorld()
        {
            RuntimeApi.Kernel.CheckThread();if(worldPass==null)throw new InvalidOperationException("A world pass is required");
            var draw=worldDraw;if(draw!=null)draw();
        }
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
        { return RegisterPresenter(ready,null); }
        /// <summary>Also project late world overlays into the presenter's logical viewport.</summary>
        public static IDisposable RegisterPresenter(Func<bool> ready,Func<Microsoft.Xna.Framework.Vector2,Microsoft.Xna.Framework.Vector2> projectWorld)
        {
            RuntimeApi.Kernel.CheckThread();
            if (ready == null || presenterReady != null) throw new InvalidOperationException("Final presentation is already owned or invalid");
            presenterReady = ready;
            worldProjector=projectWorld;
            return new ActionLease(delegate { RuntimeApi.Kernel.CheckThread(); if (presenterReady!=ready) throw new InvalidOperationException("Final presentation ownership lost"); presenterReady=null;worldProjector=null; });
        }
        /// <summary>Project a canonical world point for a late overlay. Screen passes keep native screen transforms.</summary>
        public static Microsoft.Xna.Framework.Vector2 ProjectWorld(Microsoft.Xna.Framework.Vector2 point)
        {
            RuntimeApi.Kernel.CheckThread();
            if(worldPass!=null)return point+worldPass.Translation;
            if(InScreenPass) return JumpKing.Camera.TransformVector2(point);
            if(HasExternalPresentation && worldProjector!=null) return worldProjector(point);
            var origin=JumpKing.Camera.TransformVector2(Microsoft.Xna.Framework.Vector2.Zero);
            var chart=Geometry.MapTopology.ChartPosition(new Microsoft.Xna.Framework.Vector2(240,180)-origin,point);
            return JumpKing.Camera.TransformVector2(chart);
        }
        /// <summary>Canonical world projection for one logical screen, including atlas passes without scene effects.</summary>
        public sealed class WorldPass : IDisposable
        {
            private readonly WorldPass previous;
            internal readonly Vector2 Translation;
            private bool disposed;
            public WorldPass(Vector2 translation)
            {RuntimeApi.Kernel.CheckThread();previous=worldPass;Translation=translation;worldPass=this;}
            public void Dispose()
            {
                RuntimeApi.Kernel.CheckThread();if(disposed)return;
                if(!ReferenceEquals(worldPass,this))throw new InvalidOperationException("World passes must end in reverse order");
                worldPass=previous;disposed=true;
            }
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
