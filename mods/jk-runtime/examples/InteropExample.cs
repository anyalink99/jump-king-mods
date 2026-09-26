using System;
using JKRuntime;
using JKRuntime.Gameplay;
using JKRuntime.Modules;
using JumpKing.Level;
using Microsoft.Xna.Framework;

namespace RuntimeExamples
{
    [RuntimeModule("example.interop", "Material and movement evidence example")]
    public static class InteropExample
    {
        private static MovementObservationScope prepared;
        private static MovementObserver observer;
        [OnWorldReady]
        public static void Prepare(RuntimeScope scope)
        {
            using(RuntimeApi.MeasureStartup("example.interop.prepare"))
            {
                var value=scope.Own(MovementObservation.Prepare()); prepared=value;
                scope.Defer(delegate { if(ReferenceEquals(prepared,value))prepared=null; });
            }
        }
        [OnLevelStart]
        public static void Start(ModuleContext context)
        {
            // Declarations are published at activation. The provider owns state
            // capture correctness; the predicate receives only detached values.
            context.Track(RuntimeApi.Materials.Register("example.interop",new MaterialCapabilities(
                typeof(ExampleSurface),SpeedCapability.Identity,SupportPredicates.TopFace,captureState:CaptureState)));
            var value=context.Track(prepared.Observe(JumpKing.GameManager.GameLoop.m_player.m_body)); observer=value;
            context.Track(new ClearObserver(value));
        }
        public static void SetRecording(bool enabled) { if(observer!=null)observer.Recording=enabled; }
        public static MovementTrace CaptureReport() { return observer==null?null:observer.Capture(); }
        private static long CaptureState(IBlock block) { return ((ExampleSurface)block).SolidFromAbove?1:0; }
        private sealed class ClearObserver : IDisposable
        {
            private readonly MovementObserver value;
            internal ClearObserver(MovementObserver observer) { value=observer; }
            public void Dispose() { if(ReferenceEquals(observer,value))observer=null; }
        }
        // The declaration does not implement gameplay collision or register a
        // factory. A real provider must make its collision rule match the query.
        public sealed class ExampleSurface : BoxBlock
        {
            public bool SolidFromAbove=true;
            public ExampleSurface(Rectangle rectangle) : base(rectangle) { }
        }
    }
}
