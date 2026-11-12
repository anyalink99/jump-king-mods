using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JKRuntime.World;
using Microsoft.Xna.Framework;

namespace MegaMappingExpansion
{
    internal sealed class TransitionPoolState
    {
        [WorldField] public string Id;
        [WorldField] public ulong Random;
        [WorldField] public int Cursor, Current;
        [WorldField] public int[] History, Bag;
    }
    internal sealed class TransitionState
    {
        [WorldField] public string Layout;
        [WorldField] public ulong Seed;
        [WorldField] public TransitionPoolState[] Pools;
        [WorldField] public bool[] Contacts;
        [WorldField] public int Screen;
    }
    internal sealed class TransitionEngine : ISideLinkRouter, JKRuntime.State.IStateParticipant
    {
        internal readonly TransitionPlan Plan;
        private Dictionary<string, TransitionChoice> choices;
        private bool[] contacts;
        private int screen = -1;
        private long revision;
        private bool pending;
        internal ulong Seed { get; private set; }
        internal Action Changed, LinksChanged;
        internal TransitionEngine(TransitionPlan plan, ulong seed)
        {
            Plan = plan; Seed = plan.FixedSeed ?? seed; contacts = new bool[plan.Triggers.Count];
            choices = plan.Pools.ToDictionary(p => p.Id, p => new TransitionChoice { Random = TransitionSelection.Seed(Seed, p.Id) }, StringComparer.Ordinal);
            foreach (var pool in plan.Pools.Where(p => p.Refresh != "crossing")) TransitionSelection.Pick(pool, choices[pool.Id]);
        }
        public string Id { get { return "mega.mapping.transitions-state"; } }
        public int Version { get { return 1; } }
        internal IEnumerable<TransitionLink> Links()
        {
            foreach (var exit in Plan.Exits)
            {
                var pool = Plan.Pools.Find(p => p.Id == exit.Pool); int current = choices[pool.Id].Current;
                yield return new TransitionLink { Screen = exit.Screen, Side = exit.Side, Target = pool.Screens[Math.Max(0, current)] };
            }
            foreach (var pool in Plan.Pools.Where(p => p.Schemes != null)) foreach (var link in pool.Schemes[choices[pool.Id].Current].Links) yield return link;
        }
        public bool Owns(int source, int side) { return Plan.Exits.Any(e => e.Screen == source && e.Side == side) || Plan.Pools.Any(p => p.Schemes != null && p.Schemes[0].Links.Any(l => l.Screen == source && l.Side == side)); }
        private sealed class Decision : ISideLinkDecision
        {
            internal TransitionEngine Engine;
            internal string Pool;
            internal TransitionChoice Next;
            internal long Revision;
            internal bool Closed;
            public int Target { get; internal set; }
            public void Commit()
            {
                if (Closed || Engine.revision != Revision) throw new InvalidOperationException("Stale transition decision");
                if (Next != null) Engine.choices[Pool] = Next;
                Closed = true; Engine.pending = false; Engine.revision++;
                Engine.Publish();
            }
            public void Cancel() { if (!Closed) { Closed = true; Engine.pending = false; } }
        }
        public ISideLinkDecision Prepare(int source, int side)
        {
            Authority();
            if (pending) throw new InvalidOperationException("A transition decision is already in progress");
            var exit = Plan.Exits.Find(e => e.Screen == source && e.Side == side);
            var decision = new Decision { Engine = this, Revision = revision };
            if (exit != null)
            {
                var pool = Plan.Pools.Find(p => p.Id == exit.Pool); decision.Pool = pool.Id;
                var state = choices[pool.Id];
                if (pool.Refresh == "crossing") { state = state.Copy(); TransitionSelection.Pick(pool, state); decision.Next = state; }
                decision.Target = pool.Screens[state.Current];
            }
            else
            {
                var link = Links().FirstOrDefault(l => l.Screen == source && l.Side == side);
                if (link == null) throw new InvalidOperationException("Unowned transition");
                decision.Target = link.Target;
            }
            pending = true; return decision;
        }
        internal void Reroll(IEnumerable<string> ids)
        {
            Authority(); if (pending) throw new InvalidOperationException("Cannot reroll during a crossing");
            var pools = ids.Select(id => Plan.Pools.Find(p => p.Id == id)).ToArray();
            if (pools.Any(p => p == null || p.Refresh != "event") || pools.Select(p => p.Id).Distinct().Count() != pools.Length) throw new InvalidOperationException("Reroll needs unique event-refresh pools");
            var next = new Dictionary<string, TransitionChoice>(choices, StringComparer.Ordinal);
            foreach (var pool in pools) { var state = choices[pool.Id].Copy(); TransitionSelection.Pick(pool, state); next[pool.Id] = state; }
            choices = next; revision++; Publish();
        }
        internal void Emit(string name)
        {
            Authority();
            // one event can name several triggers; pick each affected pool only once
            var ids = Plan.Triggers.Where(t => t.On == "event" && t.Event == name).SelectMany(t => t.Pools).Distinct().ToArray();
            if (ids.Length != 0) Reroll(ids);
        }
        internal void Tick(int currentScreen, Rectangle localBounds)
        {
            if (!WorldExecution.CanSimulate || WorldControl.Following) return;
            var ids = new HashSet<string>(); bool changed = screen != currentScreen;
            for (int i = 0; i < Plan.Triggers.Count; i++)
            {
                var trigger = Plan.Triggers[i]; bool inside = trigger.Screen == currentScreen && (trigger.On == "screen-enter" || trigger.On == "touch" && new Rectangle(trigger.X, trigger.Y, trigger.Width, trigger.Height).Intersects(localBounds));
                bool fire = trigger.On == "screen-enter" ? inside && currentScreen != screen : trigger.On == "touch" && inside && !contacts[i];
                if (fire) foreach (string id in trigger.Pools) ids.Add(id);
                changed |= contacts[i] != inside; contacts[i] = inside;
            }
            screen = currentScreen;
            if (ids.Count > 0) Reroll(ids.OrderBy(id => id, StringComparer.Ordinal));
            else if (changed && Changed != null) Changed();
        }
        internal void Publish() { if (LinksChanged != null) LinksChanged(); if (Changed != null) Changed(); }
        private static void Authority() { if (!WorldExecution.CanSimulate || WorldControl.Following) throw new InvalidOperationException("Transition state is controlled by " + WorldControl.Owner); }
        public object Capture()
        {
            if (pending) throw new InvalidOperationException("Cannot capture a half-completed crossing");
            return new TransitionState { Layout = Plan.Identity, Seed = Seed, Screen = screen, Contacts = (bool[])contacts.Clone(), Pools = Plan.Pools.Select(p => {
                var state = choices[p.Id]; return new TransitionPoolState { Id = p.Id, Random = state.Random, Current = state.Current, Cursor = state.Cursor, History = state.History.ToArray(), Bag = state.Bag.ToArray() };
            }).ToArray() };
        }
        public void Validate(object packet)
        {
            var state = packet as TransitionState;
            if (state == null || state.Layout != Plan.Identity || Plan.FixedSeed.HasValue && state.Seed != Plan.FixedSeed.Value || state.Pools == null || state.Pools.Length != Plan.Pools.Count || state.Contacts == null || state.Contacts.Length != contacts.Length || state.Screen < -1 || state.Screen >= Plan.Screens) throw new InvalidDataException("Transition state differs from authored transitions");
            for (int i = 0; i < Plan.Pools.Count; i++)
            {
                var pool = Plan.Pools[i]; var value = state.Pools[i];
                if (value == null || value.Id != pool.Id || value.Random == 0 || value.Cursor < 0 || value.Cursor >= pool.Count || value.Current < (pool.Refresh == "crossing" ? -1 : 0) || value.Current >= pool.Count || value.History == null || value.History.Length > pool.AvoidLast || value.History.Any(n => n < 0 || n >= pool.Count) || value.History.Distinct().Count() != value.History.Length || value.Bag == null || value.Bag.Length > pool.Count || value.Bag.Any(n => n < 0 || n >= pool.Count) || value.Bag.Distinct().Count() != value.Bag.Length || pool.Mode != "shuffle" && value.Bag.Length != 0 || value.History.Length > 0 && value.History[value.History.Length - 1] != value.Current || pool.Mode == "sequence" && value.Current >= 0 && value.Cursor != (value.Current + 1) % pool.Count) throw new InvalidDataException("Invalid transition pool state: " + pool.Id);
                if (pool.Mode != "sequence" && value.Cursor != 0 || value.Current < 0 && (value.History.Length != 0 || value.Bag.Length != 0 || value.Cursor != 0 || value.Random != TransitionSelection.Seed(state.Seed, pool.Id)) || value.Current >= 0 && pool.AvoidLast > 0 && value.History.Length == 0 || value.Bag.Contains(value.Current)) throw new InvalidDataException("Inconsistent transition pool state: " + pool.Id);
            }
        }
        public void Restore(object packet)
        {
            Validate(packet); if (pending) throw new InvalidOperationException("Cannot restore during a crossing");
            var state = (TransitionState)packet; var next = new Dictionary<string, TransitionChoice>(StringComparer.Ordinal);
            foreach (var value in state.Pools) { var choice = new TransitionChoice { Random = value.Random, Current = value.Current, Cursor = value.Cursor }; choice.History.AddRange(value.History); choice.Bag.AddRange(value.Bag); next.Add(value.Id, choice); }
            choices = next; Seed = state.Seed; contacts = (bool[])state.Contacts.Clone(); screen = state.Screen; revision++;
            if (LinksChanged != null) LinksChanged();
        }
        internal TransitionState State { get { return (TransitionState)Capture(); } }
    }
}
