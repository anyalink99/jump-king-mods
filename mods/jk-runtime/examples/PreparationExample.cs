using System;
using JKRuntime;
using JKRuntime.Gameplay;
using JKRuntime.Modules;

namespace RuntimeExamples
{
    // just prepare some small in-memory data so this example works without a map asset
    // swap Decode for your player-independent asset IO
    [RuntimeModule("example.preparation", "Preparation Example")]
    public static class PreparationExample
    {
        private static PreparedWorld world;
        private static int[] attempt;

        [OnWorldReady]
        public static void PrepareWorld(RuntimeScope scope)
        {
            PreparedWorld prepared = scope.Own(PreparedWorld.Decode());
            world = prepared;
            scope.Defer(delegate { if (ReferenceEquals(world, prepared)) world = null; });
        }

        [BeforeAttempt]
        public static void PrepareAttempt(RuntimeScope scope)
        {
            // read relevant saved configuration here if it can change per attempt
            // copy derived data, never attach a previous player's object to it
            int[] prepared = world.CreateAttempt();
            attempt = prepared;
            scope.Defer(delegate { if (ReferenceEquals(attempt, prepared)) attempt = null; });
        }

        [OnLevelStart]
        public static void Start(ModuleContext context)
        {
            if (attempt == null) throw new InvalidOperationException("Attempt was not prepared");
            int[] active = attempt;
            // just attach here, the data is already prepared
            // no discovery or asset loading deferred to the first tick
            context.Track(GameplayEvents.Subscribe(context.ModuleId, delegate(GameplayEvent e) {
                if (e.Kind == GameplayEventKind.Jump) active[0]++;
            }));
        }

        private sealed class PreparedWorld : IDisposable
        {
            private int[] values;
            private PreparedWorld(int[] data) { values = data; }
            public static PreparedWorld Decode() { return new PreparedWorld(new[] { 0, 10, 20 }); }
            public int[] CreateAttempt()
            {
                if (values == null) throw new ObjectDisposedException("PreparedWorld");
                return (int[])values.Clone();
            }
            public void Dispose() { values = null; }
        }
    }
}
