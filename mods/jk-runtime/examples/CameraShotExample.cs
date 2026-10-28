using JKRuntime;
using JKRuntime.Modules;
using JKRuntime.Presentation;
using Microsoft.Xna.Framework;

namespace RuntimeExamples
{
    [RuntimeModule("example.camera-shot", "Camera shot example")]
    public static class CameraShotExample
    {
        [OnWorldReady] public static void Prepare(RuntimeScope scope)
        { NativeScreenView.Prepare(scope); }

        [OnLevelStart] public static void Start(ModuleContext context)
        {
            var scope=context.Track(new RuntimeScope());
            var shot=CameraShots.Acquire("example.camera-shot",0,scope);
            shot.Publish(new CameraShot(new Vector2(240,180),0,false,0,0));
            // a real scene clears this lease when its showcase finishes
        }
    }
}
