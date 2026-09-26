using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using JKRuntime.Simulation;

namespace ScreenSolver
{
    public enum SearchStatus { Running, SimulatedRoute, SearchLimitReached, Cancelled, Unsupported, Failed, ExhaustedActionSet }

    public sealed class SearchAction
    {
        private readonly SimulationInput[] inputs;
        public string Label { get; private set; }
        public bool EndOnLanding { get; private set; }
        public int Length { get { return inputs.Length; } }
        internal SimulationInput At(int index) { return inputs[index]; }
        public SearchAction(string label, IEnumerable<SimulationInput> frames, bool endOnLanding = false)
        {
            if (string.IsNullOrWhiteSpace(label) || frames == null) throw new ArgumentException("Action label/inputs required");
            inputs = frames.Take(1025).ToArray();
            if (inputs.Length == 0 || inputs.Length > 1024) throw new ArgumentException("Action length must be 1..1024 ticks");
            Label = label; EndOnLanding = endOnLanding;
        }
    }

    public sealed class RoutePoint
    {
        private readonly SimulationEvent[] events;
        public SimulationPose Pose { get; private set; }
        public long Tick { get; private set; }
        public SimulationInput Input { get; private set; }
        public SimulationEvent[] Events { get { return (SimulationEvent[])events.Clone(); } }
        internal RoutePoint(SimulationStepResult step, SimulationInput input)
        { Pose = step.State.Pose; Tick = step.State.Tick; events = step.Events; Input = input; }
    }

    // Cooperative tick-by-tick search. No task, thread or Update hook is created
    // here. The UI owns calling Advance while a Solve request is active.
    public sealed class SearchJob : IDisposable
    {
        private sealed class Node
        {
            internal SimulationSnapshot State;
            internal Node Parent;
            internal SearchAction Action;
            internal int Ticks, Depth;
            internal ScreenExit Exit;
        }
        private readonly SimulationSession session;
        private Func<SimulationSnapshot, IEnumerable<SearchAction>> actions;
        private Func<SimulationSnapshot, string> identity;
        private readonly SearchTarget[] targets;
        private readonly int maxNodes, maxTicks;
        private readonly long maxBytes;
        private readonly List<Node> open = new List<Node>();
        private readonly HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
        private Node current, candidate;
        private SearchAction[] choices;
        private int choice, tick, expanded;
        private SimulationSnapshot branch;
        private ScreenExit branchExit, replayExit;
        private bool airborne, disposed;
        private long retainedBytes;
        private readonly List<RoutePoint> route = new List<RoutePoint>();
        private readonly List<string> instructions = new List<string>();
        private Queue<Node> replay;
        private Node replaySegment;
        private SimulationSnapshot replayState;
        private int replayTick;
        private int replayPass, replayIndex;
        private readonly List<string> replayKeys = new List<string>();
        public SearchStatus Status { get; private set; }
        public string Detail { get; private set; }
        public int SimulatedTicks { get; private set; }
        public RoutePoint[] Route { get { return route.ToArray(); } }
        public string[] Instructions { get { return instructions.ToArray(); } }
        public SearchJob(SimulationSession simulation, Func<SimulationSnapshot, IEnumerable<SearchAction>> expand,
            int targetScreen, int nodeLimit = 2000, int tickLimit = 500000, long byteLimit = 64 * 1024 * 1024)
            : this(simulation, expand, new[] { new SearchTarget(0, targetScreen, ExitDirection.Any) }, nodeLimit, tickLimit, byteLimit) { }
        public SearchJob(SimulationSession simulation, Func<SimulationSnapshot, IEnumerable<SearchAction>> expand,
            IEnumerable<SearchTarget> destinations, int nodeLimit = 2000, int tickLimit = 500000, long byteLimit = 64 * 1024 * 1024,
            Func<SimulationSnapshot, string> equivalentState = null)
        {
            if (simulation == null || expand == null || destinations == null || nodeLimit < 1 || tickLimit < 1 || byteLimit < 1)
                throw new ArgumentException("Invalid search configuration");
            targets = destinations.Take(257).ToArray();
            if (targets.Length == 0 || targets.Length > 256 || targets.Any(t => t == null)) throw new ArgumentException("Invalid destinations");
            session = simulation; actions = expand;
            identity = equivalentState ?? (s => s.Key);
            maxNodes = nodeLimit; maxTicks = tickLimit; maxBytes = byteLimit;
            open.Add(new Node { State = session.Initial }); seen.Add(identity(session.Initial) + new ScreenExit().Key);
            retainedBytes = Cost(session.Initial); Status = SearchStatus.Running; Detail = "Searching";
        }
        private static long Cost(SimulationSnapshot state) { return state.Key.Length * 3L + 256; }
        public void Advance(int milliseconds = 8, int tickBudget = 256)
        {
            if (milliseconds < 1 || milliseconds > 100 || tickBudget < 1) throw new ArgumentOutOfRangeException("budget");
            if (Status != SearchStatus.Running) return;
            var timer = Stopwatch.StartNew(); int start = SimulatedTicks;
            try
            {
                while (Status == SearchStatus.Running && timer.ElapsedMilliseconds < milliseconds && SimulatedTicks - start < tickBudget)
                {
                    if (SimulatedTicks >= maxTicks || retainedBytes > maxBytes)
                    { Finish(SearchStatus.SearchLimitReached, "Search budget reached; no impossibility claim"); break; }
                    if (replay != null) { ReplayTick(); continue; }
                    if (current == null)
                    {
                        if (open.Count == 0) { Finish(SearchStatus.ExhaustedActionSet, "No route in the supplied finite action set"); break; }
                        if (expanded >= maxNodes) { Finish(SearchStatus.SearchLimitReached, "Node budget reached"); break; }
                        // Prefer proximity to this tier, including downward goals.
                        // This is ordering only, never a reachability/optimality claim.
                        int best = 0;
                        for (int i = 1; i < open.Count; i++)
                            if (Score(open[i]) < Score(open[best])) best = i;
                        current = open[best]; open.RemoveAt(best); expanded++;
                        choices = actions(current.State).Take(257).ToArray();
                        if (choices.Length > 256 || choices.Any(a => a == null)) throw new InvalidOperationException("Action provider exceeded its budget");
                        choice = 0; branch = null;
                    }
                    if (choice >= choices.Length) { current = null; choices = null; continue; }
                    if (branch == null) { branch = current.State; branchExit = current.Exit; tick = 0; airborne = !branch.Pose.Grounded; }
                    var action = choices[choice];
                    var result = session.Step(branch, action.At(tick)); SimulatedTicks++; tick++;
                    branchExit = branchExit.Next(branch.Pose, result);
                    branch = result.State; airborne |= !branch.Pose.Grounded;
                    if (IsGoal(branch, branchExit))
                    { candidate = new Node { State = branch, Parent = current, Action = action, Ticks = tick, Exit = branchExit }; BeginReplay(); continue; }
                    if (tick >= action.Length || (action.EndOnLanding && airborne && branch.Pose.StableLanding))
                    {
                        if (seen.Add(identity(branch) + branchExit.Key))
                        {
                            retainedBytes += Cost(branch) + action.Length * 16L;
                            open.Add(new Node { State = branch, Parent = current, Action = action, Ticks = tick, Depth = current.Depth + 1, Exit = branchExit });
                        }
                        branch = null; choice++;
                    }
                }
            }
            catch (NotSupportedException error) { Finish(SearchStatus.Unsupported, error.Message); }
            catch (Exception error) { Finish(SearchStatus.Failed, error.Message); }
        }
        private bool IsGoal(SimulationSnapshot state, ScreenExit exit) { return targets.Any(t => t.Accepts(state.Pose, exit)); }
        private float Score(Node node)
        {
            return targets.Min(t => t.Direction == ExitDirection.Left ? node.State.Pose.Position.X
                : t.Direction == ExitDirection.Right ? 480 - node.State.Pose.Position.X
                : Math.Abs(node.State.Pose.Position.Y - (180 - t.Screen * 360))) + node.Depth;
        }
        private void BeginReplay()
        {
            var chain = new Stack<Node>(); for (var node = candidate; node.Parent != null; node = node.Parent) chain.Push(node);
            replay = new Queue<Node>(chain); replayState = session.Initial; replaySegment = null;
            replayExit = new ScreenExit();
            open.Clear(); seen.Clear(); current = null; choices = null; branch = null;
            replayPass++; replayIndex = 0;
            Detail = replayPass == 1 ? "Replaying candidate from its isolated initial state" : "Checking per-tick determinism";
        }
        private void ReplayTick()
        {
            if (replaySegment == null)
            {
                if (replay.Count == 0)
                {
                    if (replayState.Key != candidate.State.Key || replayExit.Key != candidate.Exit.Key || !IsGoal(replayState, replayExit))
                        throw new InvalidOperationException("Candidate replay did not reproduce the goal");
                    if (replayPass == 1) { BeginReplay(); return; }
                    Finish(SearchStatus.SimulatedRoute, "Replay-checked simulation; not a manual playtest"); return;
                }
                replaySegment = replay.Dequeue(); replayTick = 0;
                if (replayPass == 1) instructions.Add(replaySegment.Action.Label + " (" + replaySegment.Ticks + " ticks)");
            }
            var input = replaySegment.Action.At(replayTick++);
            var step = session.Step(replayState, input); SimulatedTicks++;
            replayExit = replayExit.Next(replayState.Pose, step); replayState = step.State;
            string traceKey = step.State.Key + EventKey(step.Events);
            if (replayPass == 1)
            {
                route.Add(new RoutePoint(step, input)); replayKeys.Add(traceKey);
                retainedBytes += 128 + step.Events.Length * 1024L + traceKey.Length * 2L;
            }
            else if (replayIndex >= replayKeys.Count || replayKeys[replayIndex] != traceKey)
                throw new InvalidOperationException("Non-deterministic provider: per-tick replay or events diverged");
            replayIndex++;
            if (replayTick == replaySegment.Ticks)
            {
                if (replayState.Key != replaySegment.State.Key) throw new InvalidOperationException("Non-deterministic provider: replay diverged");
                replaySegment = null;
            }
        }
        private static string EventKey(SimulationEvent[] events)
        {
            using (var stream = new System.IO.MemoryStream())
            using (var writer = new System.IO.BinaryWriter(stream))
            {
                writer.Write(events.Length);
                foreach (var e in events) { writer.Write(e.Kind); writer.Write(e.Position.X); writer.Write(e.Position.Y); writer.Write(e.Value); }
                return ":" + Convert.ToBase64String(stream.ToArray());
            }
        }
        private void Finish(SearchStatus status, string detail)
        {
            Status = status; Detail = detail;
            if (status != SearchStatus.SimulatedRoute) { route.Clear(); instructions.Clear(); }
            DisposeResources();
        }
        private void DisposeResources()
        {
            if (disposed) return; disposed = true; session.Dispose(); open.Clear(); seen.Clear();
            current = candidate = replaySegment = null; choices = null; branch = replayState = null; replay = null;
            actions = null; identity = null; replayKeys.Clear();
        }
        public void Dispose()
        { if (Status == SearchStatus.Running) Finish(SearchStatus.Cancelled, "Cancelled"); else DisposeResources(); }
    }
}
