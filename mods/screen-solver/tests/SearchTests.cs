using System;
using System.Collections.Generic;
using JKRuntime.Simulation;

namespace ScreenSolver
{
    internal static partial class Tests
    {
        private static void Check(bool value, string text) { if (!value) throw new Exception(text); }
        private static SimulationRequirement Claim { get { return new SimulationRequirement("fixture.world", new Version(1, 0)); } }
        private static SimulationSeed Seed()
        { return new SimulationSeed(new SimulationPose { Width = 18, Height = 26 }, 0, .017, "fixture-only", new byte[0], new[] { Claim }); }
        private static IEnumerable<SearchAction> Actions(SimulationSnapshot state)
        {
            yield return new SearchAction("Wait", new[] { new SimulationInput(0, false) });
            yield return new SearchAction("Jump", new[] { new SimulationInput(1, true), new SimulationInput(0, false) }, true);
        }
        private static void Run(SearchJob job) { for (int i = 0; i < 2000 && job.Status == SearchStatus.Running; i++) job.Advance(8, 16); }
        private static void Main()
        {
            try
            {
                NativeParity();
                PriorityCases();
                var registry = new SimulationRegistry();
                using (registry.Register(new SimulationProvider("fixture", "deterministic test world, not game parity", new[] { Claim },
                    new[] { SimulationPhase.World }, s => new byte[] { 0 }, f => {
                        var pose = f.Pose; var data = f.State;
                        // A timed gate: only jump after waiting. Merely entering
                        // the next screen without a stable landing is not success.
                        if (f.Input.Jump && f.Tick % 3 == 0) { pose.Screen = 1; data[0] = 1; }
                        else if (data[0] == 1) { pose.Grounded = true; pose.StableLanding = true; }
                        f.Pose = pose; f.State = data;
                        f.Emit(new SimulationEvent("wind", pose.Position, f.Tick % 3 == 0 ? 1 : -1));
                    })))
                {
                    using (var job = new SearchJob(registry.Open(Seed()), Actions, 1))
                    {
                        Run(job); Check(job.Status == SearchStatus.SimulatedRoute, "Timed route not found/replayed: " + job.Detail);
                        Check(job.Route.Length >= 4 && job.Route[job.Route.Length - 1].Pose.StableLanding, "Airborne crossing accepted");
                        Check(job.Route[0].Events.Length == 1 && registry.ActiveSessions == 0, "Wind trace / release");
                    }
                    using (var job = new SearchJob(registry.Open(Seed()), Actions, 1, tickLimit: 1))
                    { Run(job); Check(job.Status == SearchStatus.SearchLimitReached && job.Route.Length == 0, "Budget lied about impossibility"); }
                    var cancelled = new SearchJob(registry.Open(Seed()), Actions, 1); cancelled.Dispose(); cancelled.Advance();
                    Check(cancelled.Status == SearchStatus.Cancelled && registry.ActiveSessions == 0, "Cancel leaked work");
                }
                using (registry.Register(new SimulationProvider("unknown", "fixture", new[] { Claim }, new[] { SimulationPhase.World },
                    s => new byte[0], f => { throw new NotSupportedException("Unknown gimmick"); })))
                using (var job = new SearchJob(registry.Open(Seed()), Actions, 1))
                { Run(job); Check(job.Status == SearchStatus.Unsupported && job.Route.Length == 0, "Unsupported produced fallback route"); }
                int calls = 0;
                using (registry.Register(new SimulationProvider("impure", "intentional bad fixture", new[] { Claim }, new[] { SimulationPhase.World },
                    s => new byte[0], f => { var p = f.Pose; p.Screen = 1; p.Grounded = p.StableLanding = true; p.Position.X = calls++; f.Pose = p; })))
                using (var job = new SearchJob(registry.Open(Seed()), Actions, 1))
                { Run(job); Check(job.Status == SearchStatus.Failed && job.Route.Length == 0, "Non-deterministic replay was published"); }
                calls = 0;
                using (registry.Register(new SimulationProvider("events", "intentional bad event fixture", new[] { Claim }, new[] { SimulationPhase.World },
                    s => new byte[0], f => {
                        var p = f.Pose; p.Screen = 1; p.Grounded = p.StableLanding = true; f.Pose = p;
                        f.Emit(new SimulationEvent("wind", p.Position, calls++));
                    })))
                using (var job = new SearchJob(registry.Open(Seed()), Actions, 1))
                { Run(job); Check(job.Status == SearchStatus.Failed && job.Route.Length == 0, "Changing events with equal end state escaped replay validation"); }
                Console.WriteLine("[OK] Screen Solver: timed waiting route, stable landing, replay validation, wind events, cancellation, limits and unsupported mechanics");
            }
            catch (Exception error) { Console.Error.WriteLine(error); Environment.Exit(1); }
        }
    }
}
