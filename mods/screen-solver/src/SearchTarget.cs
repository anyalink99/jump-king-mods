using System;
using System.Collections.Generic;
using System.Linq;
using JKRuntime.Simulation;

namespace ScreenSolver
{
    public enum ExitDirection { Any, Up, Left, Right, Down }

    public sealed class SearchTarget
    {
        public int SourceScreen { get; private set; }
        public int Screen { get; private set; }
        public ExitDirection Direction { get; private set; }
        public SearchTarget(int sourceScreen, int screen, ExitDirection direction)
        {
            if (sourceScreen < 0 || screen < 0 || !Enum.IsDefined(typeof(ExitDirection), direction))
                throw new ArgumentException("Invalid screen target");
            SourceScreen = sourceScreen; Screen = screen; Direction = direction;
        }
        internal bool Accepts(SimulationPose pose, ScreenExit exit)
        {
            return pose.Screen == Screen && pose.StableLanding &&
                (Direction == ExitDirection.Any || (exit.Source == SourceScreen && exit.Direction == Direction));
        }
    }

    // Path history is part of the search identity, not a guess from screen IDs.
    // A side teleport to a higher numbered screen is still a side exit.
    internal struct ScreenExit
    {
        internal int Source;
        internal ExitDirection Direction;
        internal string Key { get { return ":" + Source + ":" + (int)Direction; } }
        internal ScreenExit Next(SimulationPose previous, SimulationStepResult next)
        {
            foreach (var e in next.Events)
            {
                if (e.Kind == "teleport-left" || e.Kind == "teleport-right")
                    return new ScreenExit { Source = previous.Screen,
                        Direction = e.Kind == "teleport-left" ? ExitDirection.Left : ExitDirection.Right };
            }
            if (previous.Screen != next.State.Pose.Screen)
                return new ScreenExit { Source = previous.Screen,
                    Direction = next.State.Pose.Screen > previous.Screen ? ExitDirection.Up : ExitDirection.Down };
            return this;
        }
    }

    // Each tier uses the same captured seed, never a fresh live-world capture.
    // Left and right share a tier: neither arbitrarily outranks the other.
    public sealed class PrioritySearch : IDisposable
    {
        private Func<SearchTarget[], SearchJob> create;
        private readonly SearchTarget[][] tiers;
        private SearchJob current;
        private int tier = -1, completedTicks;
        private bool limited, disposed;
        private RoutePoint[] route = new RoutePoint[0];
        private string[] instructions = new string[0];
        public SearchStatus Status { get; private set; }
        public string Detail { get; private set; }
        public bool HigherPriorityUnresolved { get { return limited; } }
        public int SimulatedTicks { get { return completedTicks + (current == null ? 0 : current.SimulatedTicks); } }
        public RoutePoint[] Route { get { return (RoutePoint[])route.Clone(); } }
        public string[] Instructions { get { return (string[])instructions.Clone(); } }
        public string DirectionLabel { get { return tier < 0 || tier >= tiers.Length ? "" : Label(tiers[tier][0].Direction); } }
        public PrioritySearch(IEnumerable<SearchTarget> targets, Func<SearchTarget[], SearchJob> factory)
        {
            if (targets == null || factory == null) throw new ArgumentNullException();
            var values = targets.Take(257).ToArray();
            if (values.Length > 256 || values.Any(t => t == null || t.Direction == ExitDirection.Any))
                throw new ArgumentException("Explicit exit directions required");
            tiers = values.GroupBy(t => Rank(t.Direction)).OrderBy(g => g.Key).Select(g => g.ToArray()).ToArray();
            create = factory; Status = SearchStatus.Running; Detail = "Ready to search upward first";
        }
        private static int Rank(ExitDirection d) { return d == ExitDirection.Up ? 0 : d == ExitDirection.Down ? 2 : 1; }
        private static string Label(ExitDirection d) { return d == ExitDirection.Up ? "Up" : d == ExitDirection.Down ? "Down" : "Left / right"; }
        public void Advance(int milliseconds = 8, int tickBudget = 256)
        {
            if (milliseconds < 1 || milliseconds > 100 || tickBudget < 1) throw new ArgumentOutOfRangeException("budget");
            if (Status != SearchStatus.Running) return;
            try
            {
                if (current == null)
                {
                    if (++tier == tiers.Length)
                    {
                        Finish(limited ? SearchStatus.SearchLimitReached : SearchStatus.ExhaustedActionSet,
                            limited ? "Search limits reached; a route may still exist" : "No route in the supplied finite action set");
                        return;
                    }
                    current = create(tiers[tier]);
                    if (current == null) throw new InvalidOperationException("Search factory returned no job");
                }
                current.Advance(milliseconds, tickBudget);
                Detail = DirectionLabel + ": " + current.Detail;
                if (current.Status == SearchStatus.Running) return;
                if (current.Status == SearchStatus.SimulatedRoute)
                {
                    route = current.Route; instructions = current.Instructions;
                    Finish(SearchStatus.SimulatedRoute, Detail + (limited ? "; higher-priority search was limited, not disproved" : ""));
                }
                else if (current.Status == SearchStatus.ExhaustedActionSet || current.Status == SearchStatus.SearchLimitReached)
                {
                    limited |= current.Status == SearchStatus.SearchLimitReached;
                    completedTicks += current.SimulatedTicks; current.Dispose(); current = null;
                }
                else Finish(current.Status, Detail); // Never hide an unsupported mechanic or a failed replay.
            }
            catch (NotSupportedException error) { Finish(SearchStatus.Unsupported, error.Message); }
            catch (Exception error) { Finish(SearchStatus.Failed, error.Message); }
        }
        private void Finish(SearchStatus status, string detail)
        {
            Status = status; Detail = detail;
            if (current != null) { completedTicks += current.SimulatedTicks; current.Dispose(); current = null; }
            create = null; disposed = true;
        }
        public void Dispose() { if (!disposed) Finish(SearchStatus.Cancelled, "Cancelled"); }
    }
}
