using System;
using System.Collections.Generic;
using System.Linq;
using JKRuntime.Simulation;

namespace ScreenSolver
{
    internal static partial class Tests
    {
        private static IEnumerable<SearchAction> ExitActions(SimulationSnapshot state)
        {
            if (state.Tick != 0) yield break;
            // Deliberately supply down and sides first: action enumeration is not priority.
            foreach (uint exit in new uint[] { 4, 2, 3, 1 })
                yield return new SearchAction("Exit " + exit, new[] { new SimulationInput(0, false, exit), new SimulationInput(0, false) });
        }
        private static void PriorityCases()
        {
            foreach (int allowed in new[] { 1, 2, 3, 4, 0 })
            {
                var registry = new SimulationRegistry();
                var seed = new SimulationSeed(new SimulationPose { Screen = 1, Width = 18, Height = 26 },
                    0, .017, "fixture-only", new byte[0], new[] { Claim });
                int captures = 0;
                using (registry.Register(new SimulationProvider("priority", "synthetic exit topology", new[] { Claim },
                    new[] { SimulationPhase.World }, s => { captures++; return new byte[0]; }, f => {
                        var p = f.Pose; uint d = f.Input.ExtraButtons;
                        if (d != 0 && allowed != 0 && d >= allowed)
                        {
                            // Left leads DOWN to 0; right leads UP to 2. This must not change their tier.
                            p.Screen = d == 1 || d == 3 ? 2 : 0;
                            p.Grounded = p.StableLanding = false;
                            if (d == 2 || d == 3) f.Emit(new SimulationEvent(d == 2 ? "teleport-left" : "teleport-right", p.Position, p.Screen));
                        }
                        else if (d == 0) p.Grounded = p.StableLanding = true;
                        f.Pose = p;
                    })))
                {
                    var targets = new[] { new SearchTarget(1, 0, ExitDirection.Down), new SearchTarget(1, 2, ExitDirection.Right),
                        new SearchTarget(1, 0, ExitDirection.Left), new SearchTarget(1, 2, ExitDirection.Up) };
                    using (var search = new PrioritySearch(targets, t => new SearchJob(registry.Open(seed), ExitActions, t)))
                    {
                        Check(captures == 0 && registry.ActiveSessions == 0, "Priority constructor started idle capture");
                        for (int i = 0; i < 100 && search.Status == SearchStatus.Running; i++) search.Advance(8, 100);
                        Check(search.Status == (allowed == 0 ? SearchStatus.ExhaustedActionSet : SearchStatus.SimulatedRoute), "Priority search failed: " + search.Detail);
                        if (allowed != 0)
                        {
                            uint chosen = search.Route[0].Input.ExtraButtons;
                            Check(chosen == (uint)allowed, "Wrong target priority: " + allowed + " -> " + chosen);
                            Check(search.Route.Length == 2, "Accepted exit before stable landing");
                        }
                        Check(!search.HigherPriorityUnresolved && registry.ActiveSessions == 0, "Priority status or disposal");
                    }
                    using (var search = new PrioritySearch(targets, t => new SearchJob(registry.Open(seed), ExitActions, t,
                        tickLimit: t[0].Direction == ExitDirection.Up ? 1 : 1000)))
                    {
                        for (int i = 0; i < 100 && search.Status == SearchStatus.Running; i++) search.Advance(8, 100);
                        Check(search.HigherPriorityUnresolved, "Lost upper-tier budget warning");
                        Check(search.Status == (allowed == 0 ? SearchStatus.SearchLimitReached : SearchStatus.SimulatedRoute), "Budget fallback status");
                        Check(search.Detail.Contains(allowed == 0 ? "limits" : "higher-priority"), "Budget fallback hid uncertainty");
                    }
                    int before = captures;
                    var cancelled = new PrioritySearch(targets, t => new SearchJob(registry.Open(seed), ExitActions, t));
                    cancelled.Dispose(); cancelled.Advance();
                    Check(captures == before && cancelled.Status == SearchStatus.Cancelled, "Cancelled priority search opened another session");
                }
            }
            Console.WriteLine("[OK] Exit priority: up > left/right > down, teleport identity, settling, lazy capture, budget disclosure and cancellation");
        }
    }
}
