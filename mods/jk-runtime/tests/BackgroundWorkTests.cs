using System;
using System.Threading;
using EntityComponent;
using JKRuntime.Gameplay;

namespace JKRuntime
{
    internal static class BackgroundWorkTests
    {
        private sealed class Subject : Entity { internal Subject() : base(false) { } }
        private sealed class Counter : Component { }
        private static void Check(bool result, string message) { if (!result) throw new Exception(message); }
        private static void Main()
        {
            using (var start = new ManualResetEvent(false))
            using (var release = new ManualResetEvent(false))
            using (var jobs = new BackgroundWorkQueue("fixture", 1))
            {
                BackgroundWork first, rejected;
                Check(jobs.TryEnqueue(delegate { start.Set(); release.WaitOne(); throw new InvalidOperationException("storage failure"); }, out first), "Accepted first job");
                Check(start.WaitOne(3000), "Worker starts");
                int prepared = 0;
                Check(!jobs.TryPrepare(delegate { prepared++; return delegate { }; }, out rejected) && prepared == 0, "Full queues reject before copying a snapshot");
                Check(!jobs.Drain(0) && first.State == BackgroundWorkState.Running, "Completion is not reported at enqueue time");
                release.Set(); Check(jobs.Drain(3000), "Failed jobs release queue capacity");
                Check(first.State == BackgroundWorkState.Failed && first.Error == "storage failure" && first.Action == null, "Failure is observable without retaining payload");
                BackgroundWork next;
                Check(jobs.TryEnqueue(delegate { }, out next) && jobs.Drain(3000) && next.State == BackgroundWorkState.Succeeded, "Worker recovers after failure and drains success");
                bool threw = false;
                try { jobs.TryPrepare(delegate { throw new InvalidOperationException("snapshot"); }, out rejected); } catch (InvalidOperationException) { threw = true; }
                Check(threw && jobs.PendingCount == 0, "Preparation failure releases its reserved capacity");
                jobs.Dispose();
                Check(!jobs.TryEnqueue(delegate { }, out rejected), "Closed queues reject new work");
            }
            var entity = new Subject();
            int count = entity.GetComponents().Length;
            for (int i = 0; i < 50; i++)
            {
                var component = new Counter();
                var owned = new ComponentAttachment(entity, component);
                Check(entity.GetComponents().Length == count + 1, "Owned component attaches once");
                owned.Dispose(); owned.Dispose();
                Check(entity.GetComponents().Length == count && !component.Enabled, "Repeated toggles remove the native reference instead of accumulating disabled components");
            }
            Console.WriteLine("[OK] Bounded background capacity/completion/failure/drain and native component ownership");
        }
    }
}
