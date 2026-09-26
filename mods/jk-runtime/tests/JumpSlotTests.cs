using System;
using System.Linq;
using System.Threading;
using JKRuntime.Gameplay;

namespace JKRuntime
{
    internal static class JumpSlotTests
    {
        private static int checks, installs, releases;
        private static bool active, failInstall, failRelease;
        private static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
        private static Exception Fail(Action action)
        {
            try { action(); } catch (Exception error) { return error; }
            throw new Exception("Expected rejection");
        }
        private static void Acquire()
        {
            Check(!active && !JumpSlot.ChargePolicySuspended, "Policy installed while active or suspended");
            installs++; active = true;
            if (failInstall) throw new InvalidOperationException("install failure");
        }
        private static void Release()
        {
            Check(active, "Uninstall without an acquisition attempt");
            releases++;
            if (failRelease) throw new InvalidOperationException("release failure");
            active = false;
        }
        public static int Main()
        {
            try
            {
                bool baseActive = false, decorated = false;
                using (var baseline = JumpSlot.RegisterControllerPolicy(delegate { Check(!decorated, "Base installs before charge"); baseActive = true; },
                    delegate { Check(!decorated, "Charge detaches before base"); baseActive = false; }))
                using (var decoration = JumpSlot.RegisterChargePolicy(delegate { Check(baseActive, "Charge sees the final base graph"); decorated = true; }, delegate { decorated = false; }))
                {
                    using (JumpSlot.SuspendChargePolicy("exclusive-native-controller"))
                    {
                        Check(!baseActive && !decorated, "Exclusive controller suspends base and charge policies together");
                        JumpSlot.Refresh(); Check(!baseActive && !decorated, "Settings refresh cannot resume the suspended base graph");
                    }
                    Check(baseActive && decorated, "Restored controller is composed before charge resumes");
                }
                Check(!baseActive && !decorated, "Both policy owners detach cleanly");
                bool permitsCharge = false;
                using (JumpSlot.RegisterControllerPolicy(delegate { baseActive = true; }, delegate { baseActive = false; }, () => permitsCharge))
                using (JumpSlot.RegisterChargePolicy(Acquire, Release))
                {
                    Check(baseActive && !active && JumpSlot.ChargePolicySuspended, "Base controller can explicitly remove native charge eligibility");
                    permitsCharge = true; JumpSlot.Refresh(); Check(active && !JumpSlot.ChargePolicySuspended, "Charge resumes when the base restores native jumping");
                    permitsCharge = false; JumpSlot.Refresh(); Check(!active && JumpSlot.ChargePolicySuspended, "Live mode changes remove charge before replacing the base");
                }
                using (var decoration = JumpSlot.RegisterChargePolicy(delegate { decorated = true; }, delegate { decorated = false; }))
                {
                    Fail(() => JumpSlot.RegisterControllerPolicy(delegate { baseActive = true; throw new Exception("partial base acquisition"); }, delegate { baseActive = false; }));
                    Check(!baseActive && decorated, "Failed base registration restores the previous charge decorator");
                    using (JumpSlot.RegisterControllerPolicy(delegate { baseActive = true; }, delegate { baseActive = false; }))
                        Check(baseActive && decorated, "A clean base registration can retry after rollback");
                }

                var body = (JumpKing.Player.BodyComp)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(JumpKing.Player.BodyComp));
                using (var operation = PlayerControl.Acquire(body, "hammer"))
                {
                    PlayerControl.Lease denied;
                    Check(!PlayerControl.TryAcquire(body, "dash", out denied) && denied == null, "Exclusive movement refuses a second owner");
                    Check(PlayerControl.Available(body, "hammer") && !PlayerControl.Available(body, "ball"), "Operation continuation and explicit additive suspension");
                }
                Check(PlayerControl.Owner(body) == null, "Movement lease releases its body reference");
                var timer = System.Diagnostics.Stopwatch.StartNew();
                for (int i = 0; i < 1000000; i++) PlayerControl.Available(body, "ball");
                timer.Stop();
                Console.WriteLine("[COST] Unowned movement query: " + (timer.Elapsed.TotalMilliseconds / 1000000).ToString("F6") + " ms/call");
                using (var policy = JumpSlot.RegisterChargePolicy(Acquire, Release))
                {
                    Check(active, "Policy-first registration");
                    IDisposable first = null, second = null;
                    JumpSlot.Recompose(delegate {
                        Check(!active, "Detach precedes controller mutation");
                        first = JumpSlot.SuspendChargePolicy("controller-a");
                    });
                    Check(!active && JumpSlot.ChargePolicySuspended, "Controller reserves charge");
                    second = JumpSlot.SuspendChargePolicy("controller-b");
                    int count = installs;
                    for (int i = 0; i < 10; i++) JumpSlot.Refresh();
                    Check(installs == count && !active, "Settings refresh cannot bypass reservations");
                    first.Dispose(); first.Dispose();
                    Check(!active && JumpSlot.ChargePolicySuspended, "Independent reservations and repeated dispose");
                    Exception workerError = null;
                    var worker = new Thread(delegate() { try { second.Dispose(); } catch (Exception workerFailure) { workerError = workerFailure; } });
                    worker.Start(); Check(worker.Join(5000) && workerError is InvalidOperationException, "Wrong-thread disposal rejected");
                    JumpSlot.Recompose(delegate { second.Dispose(); Check(!active, "No reattachment inside graph restoration"); });
                    Check(active && !JumpSlot.ChargePolicySuspended, "Last release resumes charge after graph restoration");
                    var error = Fail(() => JumpSlot.Recompose(delegate { throw new Exception("controller failure"); }));
                    Check(active && error.ToString().Contains("controller failure"), "Failed controller change restores charge and retains cause");
                    failInstall = true;
                    error = Fail(() => JumpSlot.Recompose(delegate { throw new Exception("controller and policy failure"); }));
                    Check(!active && error.ToString().Contains("install failure") && error.ToString().Contains("controller and policy failure"), "Both failures retained; partial acquisition cleaned");
                    failInstall = false; JumpSlot.Refresh();
                    Check(active, "Cleaned acquisition failure is retryable");
                    Fail(() => JumpSlot.Recompose(() => JumpSlot.Refresh()));
                    Check(active, "Recursive composition rejected without losing the policy");
                }
                Check(!active && installs == releases, "Balanced native policy lifecycle");
                using (var controller = JumpSlot.SuspendChargePolicy("controller-first"))
                {
                    int count = installs;
                    var policy = JumpSlot.RegisterChargePolicy(Acquire, Release);
                    Check(!active && installs == count, "Controller-first load does not install a policy");
                    policy.Dispose(); policy.Dispose();
                    Check(installs == count, "Policy unload while suspended does not call unacquired cleanup");
                    using (JumpSlot.RegisterChargePolicy(Acquire, Release))
                        Check(!active, "Policy reload/settings change respects reservations");
                }
                Check(!active && !JumpSlot.ChargePolicySuspended, "No ghost callback after policy unload");
                IDisposable held = null;
                using (var policy = JumpSlot.RegisterChargePolicy(Acquire, Release))
                {
                    Fail(() => JumpSlot.Recompose(delegate {
                        held = JumpSlot.SuspendChargePolicy("partially-installed-controller");
                        throw new Exception("controller initialization failed");
                    }));
                    Check(!active && JumpSlot.ChargePolicySuspended, "Partial controller keeps charge suspended until its cleanup");
                    held.Dispose(); Check(active, "Explicit rollback releases a partial controller reservation");
                }
                failInstall = true;
                Fail(() => JumpSlot.RegisterChargePolicy(Acquire, Release));
                Check(!active, "Initial failed registration cleans its resources");
                failInstall = false;
                using (JumpSlot.RegisterChargePolicy(delegate {
                    Check(Fail(() => JumpSlot.SuspendChargePolicy("recursive-policy")) is InvalidOperationException, "Policy callbacks cannot mutate reservations");
                    Acquire();
                }, Release)) { }
                // An unknown cleanup result is the only terminal case. Do not
                // restore a controller into a graph potentially still owned by charge.
                var broken = JumpSlot.RegisterChargePolicy(Acquire, Release);
                failRelease = true; bool changed = false;
                Fail(() => JumpSlot.Recompose(delegate { changed = true; }));
                Check(!changed, "Failed detach prevents controller mutation");
                Check(Fail(() => JumpSlot.Refresh()) is InvalidOperationException, "Uncertain cleanup requires restart");
                GC.KeepAlive(broken);
                Console.WriteLine("[OK] JumpSlot: " + checks + " ownership/order/overlap/reload/rollback/thread/reentrancy/cleanup checks");
                return 0;
            }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
    }
}
