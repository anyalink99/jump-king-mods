using JKRuntime;
using JKRuntime.Modules;
using JKRuntime.World;

namespace RuntimeExamples
{
    [RuntimeModule("example.world", "Shared World Example")]
    public static class WorldExample
    {
        [OnLevelStart]
        public static void Start(ModuleContext context)
        {
            // networking and playback discover the same state without a mod dependency
            var gate = new Gate();
            context.Track(WorldRegistry.Shared.Attach("example.world.gate", gate));
        }
    }

    public sealed class Gate
    {
        [WorldField] public bool Open;
        [WorldField] public double Remaining;
        public int PersonalDisplayMode;

        public void Tick(double delta, bool touched)
        {
            if (!WorldExecution.CanSimulate) return;
            if (touched) { Open = true; Remaining = 2; }
            Remaining = System.Math.Max(0, Remaining - delta);
            if (Remaining == 0) Open = false;
        }
    }
}
