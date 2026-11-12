using System;
using JKRuntime;
using JKRuntime.Presentation;
using Microsoft.Xna.Framework.Graphics;

namespace GraphicsExample
{
    // the host owns this scope after graphics startup and closes it on exit
    public sealed class GraphicsOptions : IDisposable
    {
        private readonly RuntimeScope scope = new RuntimeScope();
        private FrameSamplingLease filter;
        public GraphicsOptions()
        {
            try { scope.Own(SmoothRendering.Register("example.graphics")); }
            catch { scope.Dispose(); throw; }
        }
        public void EnableFilter(GraphicsDevice device)
        {
            if(filter==null)filter=scope.Own(FramePresentation.Acquire(device,PixelSampling.Sharp));
        }
        public void PrepareAttempt(RuntimeScope attempt)
        {
            if(SmoothRendering.Enabled)SmoothRendering.Prepare();
            attempt.Defer(SmoothRendering.Reset);
        }
        public void SetSmooth(bool value) { SmoothRendering.Setting.Set(value); }
        public void Dispose() { scope.Dispose(); }
    }
}
