using JKRuntime;
using JKRuntime.Audio;
using JKRuntime.Gameplay;
using JKRuntime.Modules;

namespace CommonEffectsExample
{
    [RuntimeModule("example.effects", "Jump sound")]
    public static class Module
    {
        private static PreparedSound sound;
        // Embed your WAV under this resource name when building the example.
        [OnWorldReady]
        public static void Prepare(RuntimeScope scope)
        {
            var value = scope.Own(PreparedSound.FromResource(typeof(Module).Assembly, "Example.jump.wav"));
            sound = value;
            scope.Defer(() => { if (ReferenceEquals(sound, value)) sound = null; });
        }
        [OnLevelStart]
        public static void Start(ModuleContext context)
        {
            var voice = sound;
            context.Track(voice.BindToAttempt("example.effects"));
            context.Track(GameplayEvents.Subscribe("example.effects", value =>
            {
                if (value.Kind != GameplayEventKind.Jump) return;
                voice.Play();
            }));
        }
    }
}
