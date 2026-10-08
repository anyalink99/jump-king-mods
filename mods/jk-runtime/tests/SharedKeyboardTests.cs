using System;
using System.Diagnostics;
using System.Threading;
using JKRuntime.Input;

namespace JKRuntime
{
    internal static class SharedKeyboardTests
    {
        private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        private static void Main()
        {
            int reads=0; bool held=false; IntPtr focus=new IntPtr(1);
            var source=new KeyboardSource(delegate(int key) { reads++; return (short)(held && key==65 ? -32768 : 0); }, ()=>focus, false);
            using (var first=source.Subscribe(new[]{65,65,66}))
            using (var second=source.Subscribe(new[]{65,67}))
            {
                source.Poll(); Check(reads==3,"Shared poll reads each requested physical key once");
                KeyboardSample a,b;
                Check(first.TryRead(out a) && second.TryRead(out b) && a.Timestamp==b.Timestamp && !a.IsDown(65),"Independent subscribers receive the same timestamped observation");
                held=true; source.Poll(); held=false; source.Poll();
                Check(first.TryRead(out a) && a.IsDown(65) && first.TryRead(out a) && !a.IsDown(65),"Complete tap survives delayed consumption");
                Check(second.TryRead(out b) && b.IsDown(65) && second.TryRead(out b) && !b.IsDown(65),"One reader cannot consume another reader's tap");
                focus=new IntPtr(2); source.Poll(); Check(first.TryRead(out a) && a.Foreground==focus,"Focus belongs to each physical observation");
                for(int i=0;i<KeyboardSource.Capacity+1;i++) source.Poll();
                Check(first.TryRead(out a) && !a.Reliable,"Overflow invalidates edge evidence explicitly");
                int count=0; while(first.TryRead(out a)) count++;
                Check(count==KeyboardSource.Capacity,"Observation history is bounded");
                reads=0; var timer=Stopwatch.StartNew();
                for(int i=0;i<10000;i++) { source.Poll(); first.TryRead(out a); second.TryRead(out b); }
                Console.WriteLine("[COST] Shared keyboard: 10000 union samples/3 keys/two cursors={0:F3}ms; physical reads={1}, independent baseline=40000 (synthetic readers)",timer.Elapsed.TotalMilliseconds,reads);
            }
            using(var entered=new ManualResetEvent(false))
            using(var resume=new ManualResetEvent(false))
            {
                var blocked=new KeyboardSource(delegate(int key) { entered.Set(); resume.WaitOne(); return 0; },()=>new IntPtr(1),false);
                var subscription=blocked.Subscribe(new[]{65});
                var worker=new Thread(blocked.Poll); worker.Start(); Check(entered.WaitOne(2000),"Controlled physical reader blocked");
                var release=new Thread(subscription.Dispose); release.Start(); bool responsive;
                try { responsive=release.Join(1000); }
                finally { resume.Set(); worker.Join(); release.Join(); }
                Check(responsive,"Subscription release never waits for native input calls");
                KeyboardSample sample; Check(!subscription.TryRead(out sample),"Disposed subscriber receives no late input");
            }
            Console.WriteLine("[OK] Shared keyboard: union sampling, independent taps, focus, overflow, blocked-reader release and bounded storage");
            SourceWorkerLifecycle();
        }
        private static void SourceWorkerLifecycle()
        {
            int reads = 0;
            using (var observed = new AutoResetEvent(false))
            using (var sampler = new HighRateInputSampler(false, delegate(int key) { return 0; },
                delegate(int user) { Interlocked.Increment(ref reads); observed.Set(); return default(Microsoft.Xna.Framework.Input.GamePadState); }))
            {
                sampler.Start(); Check(sampler.SourceWorkerCount == 0, "Unconfigured sampler creates no physical source workers");
                sampler.Configure(new int[0], new[] { new XInputJumpBinding(0, new[] { 0 }) }, true);
                Check(observed.WaitOne(2000) && sampler.SourceWorkerCount == 1, "One configured pad starts only one worker");
                sampler.Configure(new int[0], false); Thread.Sleep(20);
                int stoppedReads = Volatile.Read(ref reads); Thread.Sleep(30);
                Check(Volatile.Read(ref reads) == stoppedReads, "Disabled source waits without native polling");
                observed.Reset();
                sampler.Configure(new int[0], new[] { new XInputJumpBinding(2, new[] { 0 }) }, true);
                Check(observed.WaitOne(2000) && sampler.SourceWorkerCount == 2, "New pad starts independently while old worker waits");
                sampler.Dispose();
                Check(SpinWait.SpinUntil(() => sampler.SourceWorkerCount == 0, 2000), "Disposal wakes and releases idle source workers");
            }
            Console.WriteLine("[COST] Physical source workers: unconfigured 0 (previously 5), one injected pad 1; removed sources wait for configuration without polling");
        }
    }
}
