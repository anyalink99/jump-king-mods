using System;
using JKRuntime;
using JKRuntime.Gameplay;
internal static class LegacyConsumer13
{
    private static int Main()
    {
        if (!RuntimeApi.Supports("mechanic-inventory-v1")) return 1;
        int released = 0;
        using (var scope = new RuntimeScope())
        {
            var lease = RuntimeApi.Mechanics.Register("legacy.fixture", "legacy.fixture.rule", new Version(1,0), MechanicEffects.Presentation,
                () => new MechanicState(true, true, false, MechanicSource.Setting, "compiled against 1.3 signatures"));
            scope.Defer(lease.Dispose); scope.Defer(() => released++);
            var rows = RuntimeApi.Mechanics.Inspect();
            if (rows.Length != 1 || rows[0].Id != "legacy.fixture.rule" || rows[0].State.Active) return 2;
        }
        if (released != 1 || RuntimeApi.Mechanics.Inspect().Length != 0) return 3;
        Console.WriteLine("[OK] Unchanged consumer compiled against frozen 1.3 reference executes and releases on current Runtime");
        return 0;
    }
}
