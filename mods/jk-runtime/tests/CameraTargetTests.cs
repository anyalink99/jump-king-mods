using System;
using System.Threading;
using JKRuntime.Presentation;
using Microsoft.Xna.Framework;

namespace JKRuntime
{
    internal static class CameraTargetTests
    {
        private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        private static void Throws<T>(Action action) where T : Exception
        { try { action(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name); }

        public static int Main()
        {
            CameraTarget target;
            Check(RuntimeApi.Supports("camera-targets-v1"), "Camera target contract can be discovered");
            int resources = RuntimeResources.Inspect().Length;
            Check(!CameraTargets.TryRead(out target), "No session means no camera override");
            var scope = new RuntimeScope();
            var lease = CameraTargets.Acquire("test.viewer", scope);
            Check(!CameraTargets.TryRead(out target), "Acquiring without a frame doesn't expose a default target");
            using (var other = new RuntimeScope())
                Throws<InvalidOperationException>(() => CameraTargets.Acquire("test.other", other));
            lease.Publish(new CameraTarget(new Vector2(100, -500), new Vector2(0, -60), 2, false));
            Check(CameraTargets.TryRead(out target) && target.Screen == 2, "Published camera subject is readable");
            var old = target;
            lease.Publish(new CameraTarget(new Vector2(100, -501), Vector2.Zero, 2, true));
            CameraTargets.TryRead(out target);
            Check(target.Revision > old.Revision && target.Discontinuity == old.Discontinuity && target.Paused,
                "Pause updates the frame without cutting continuity");
            Check(old.Center.Y == -500 && !old.Paused, "Readers retain independent snapshots");
            lease.Publish(new CameraTarget(new Vector2(100, -510), Vector2.Zero, 2, true), true);
            CameraTargets.TryRead(out target);
            Check(target.Discontinuity > old.Discontinuity, "A short paused seek still marks a cut");
            Throws<ArgumentException>(() => lease.Publish(new CameraTarget(new Vector2(float.NaN, 0), Vector2.Zero, 0, false)));
            Throws<ArgumentOutOfRangeException>(() => lease.Publish(new CameraTarget(Vector2.Zero, Vector2.Zero, -1, false)));
            CameraTarget unchanged; CameraTargets.TryRead(out unchanged);
            Check(unchanged.Revision == target.Revision, "Rejected publication preserves the last valid frame");
            Exception threadError = null;
            var thread = new Thread(() => { try { CameraTarget ignored; CameraTargets.TryRead(out ignored); } catch (Exception error) { threadError = error; } });
            thread.Start(); thread.Join();
            Check(threadError is InvalidOperationException, "Camera state is game-thread only");
            scope.Dispose(); lease.Dispose();
            Check(!CameraTargets.TryRead(out target), "Session teardown restores the normal subject");
            Throws<ObjectDisposedException>(() => lease.Publish(default(CameraTarget)));
            Throws<ObjectDisposedException>(() => CameraTargets.Acquire("test.closed", scope));
            Check(RuntimeResources.Inspect().Length == resources, "Closed scopes leave no target registrations");
            using (var next = new RuntimeScope())
            {
                CameraTargets.Acquire("test.next", next).Publish(default(CameraTarget));
                CameraTargets.TryRead(out target);
                Check(target.Discontinuity > unchanged.Discontinuity, "Replacing a session cuts even if no draw saw the gap");
            }
            Check(!CameraTargets.TryRead(out target), "Next attempt also releases ownership");
            Console.WriteLine("[OK] Camera target ownership, snapshots, pause, cuts, validation and cleanup");
            return 0;
        }
    }
}
