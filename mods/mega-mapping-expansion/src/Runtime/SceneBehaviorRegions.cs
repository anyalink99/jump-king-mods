using System;
using System.Collections.Generic;
using System.Linq;
using MegaMappingExpansion.Api;
using Microsoft.Xna.Framework;

namespace MegaMappingExpansion
{
    internal sealed partial class SceneBehaviorEngine
    {
        private sealed class RegionState
        {
            internal RegionData Data;
            internal int Screen;
            internal float X, Y, Width, Height;
            internal bool Initialized, Inside, Fired;
            internal double Dwell;
            internal long Effect;
            internal RegionState Clone() { return (RegionState)MemberwiseClone(); }
        }
        private sealed class RuleState
        {
            internal RuleData Data; internal bool Fired; internal double Last = -1e10;
            internal RuleState Clone() { return (RuleState)MemberwiseClone(); }
        }
        private RegionState[] regions;
        private RuleState[] rules;
        private void InitializeRegions()
        {
            regions = scene.Regions.OrderBy(r => r.Id, StringComparer.Ordinal).Select(r => {
                SceneAnchor anchor = string.IsNullOrEmpty(r.Anchor) ? null : scene.Anchors.Single(a => a.Id == r.Anchor);
                return new RegionState { Data = r, Screen = anchor == null ? r.Screen : anchor.Screen,
                    X = anchor == null ? r.X : anchor.X, Y = anchor == null ? r.Y : anchor.Y,
                    Width = anchor == null ? r.Width : anchor.Width, Height = anchor == null ? r.Height : anchor.Height };
            }).ToArray();
            rules = scene.Rules.OrderBy(r => r.Id, StringComparer.Ordinal).Select(r => new RuleState { Data = r }).ToArray();
            Emit("start");
        }
        private bool Condition(string flag, string value) { return string.IsNullOrEmpty(flag) || GetFlag(flag) == value; }
        internal bool NeedsSupport(int screen) { return regions.Any(r => r.Screen == screen && r.Data.Test == "standing"); }
        internal RegionData[] ResolvedRegions()
        {
            return regions.Select(r => { RegionData view = r.Data.Copy(); view.Screen = r.Screen; view.X = r.X; view.Y = r.Y; view.Width = r.Width; view.Height = r.Height; return view; }).ToArray();
        }
        private bool Contains(RegionState state)
        {
            RegionData r = state.Data;
            if (!actor.Present || actor.Screen != state.Screen || (r.Grounded && !actor.Grounded) || !Condition(r.RequiresFlag, r.EqualsValue)) return false;
            if (r.Test == "standing")
            {
                if (!actor.Grounded || actor.Supports == null) return false;
                foreach (Rectangle contact in actor.Supports)
                    if (contact.Right > state.X && contact.Left < state.X + state.Width && contact.Bottom > state.Y && contact.Top < state.Y + state.Height) return true;
                return false;
            }
            float pad = state.Inside ? r.Hysteresis : 0;
            float left = state.X - pad, top = state.Y - pad, right = state.X + state.Width + pad, bottom = state.Y + state.Height + pad;
            Rectangle p = actor.Bounds;
            if (r.Test == "center" || r.Test == "feet")
            { float x = p.Center.X, y = r.Test == "feet" ? p.Bottom - 1 : p.Center.Y; return x >= left && x < right && y >= top && y < bottom; }
            if (r.Test == "contained") return p.Left >= left && p.Right <= right && p.Top >= top && p.Bottom <= bottom;
            return p.Right > left && p.Left < right && p.Bottom > top && p.Top < bottom;
        }
        private void ObserveRegions(double delta)
        {
            foreach (RegionState state in regions)
            {
                if (failedObservers.Contains("region:" + state.Data.Id)) continue;
                try
                {
                RegionData r = state.Data; bool inside = Contains(state);
                if (!state.Initialized)
                {
                    state.Initialized = true;
                    if (r.SpawnInside == "baseline") { state.Inside = inside; continue; }
                }
                if (inside && !state.Inside)
                {
                    state.Dwell += delta;
                    if (state.Dwell + 1e-9 < r.Dwell) continue;
                    state.Inside = true;
                    if (!r.Once || !state.Fired)
                    {
                        if (events.Count >= 256) throw new InvalidOperationException("Scene event queue limit exceeded before region entry");
                        if (!string.IsNullOrEmpty(r.Enter)) state.Effect = Activate(string.IsNullOrEmpty(r.Owner) ? "region:" + r.Id : "scene:" + r.Owner, r.Enter).Id;
                        Emit("enter:" + r.Id); state.Fired = true;
                    }
                }
                else if (!inside)
                {
                    state.Dwell = 0;
                    if (!state.Inside) continue;
                    state.Inside = false;
                    if (r.Lifetime == "inside" && state.Effect != 0) Cancel(state.Effect);
                    state.Effect = 0;
                    if (!string.IsNullOrEmpty(r.Exit)) Activate(string.IsNullOrEmpty(r.Owner) ? "exit:" + r.Id : "scene:" + r.Owner, r.Exit);
                    Emit("exit:" + r.Id);
                }
                }
                catch (Exception error) { RecordError("region:" + state.Data.Id, error); }
            }
        }
        private void DrainEvents()
        {
            if (observing) throw new InvalidOperationException("Recursive scene observation");
            observing = true;
            try
            {
                int budget = 256;
                while (events.Count > 0 && budget-- > 0)
                {
                    SceneEvent observed = events.Dequeue(); string name = observed.Name;
                    DispatchTreeEvent(observed);
                    foreach (RuleState state in rules)
                    {
                        RuleData rule = state.Data;
                        if (failedObservers.Contains("rule:" + rule.Id)) continue;
                        if (rule.Event != name || (rule.Screen > 0 && rule.Screen != (observed.Screen > 0 ? observed.Screen : actor.Screen)) || (state.Fired && rule.Once)
                            || gameplayTime - state.Last < rule.Cooldown || !Condition(rule.RequiresFlag, rule.EqualsValue)) continue;
                        try
                        {
                            string incremented = string.IsNullOrEmpty(rule.Increment) ? null : checked(int.Parse(GetFlag(rule.Increment), System.Globalization.CultureInfo.InvariantCulture) + rule.Amount).ToString(System.Globalization.CultureInfo.InvariantCulture);
                            if (((!string.IsNullOrEmpty(rule.SetFlag) && GetFlag(rule.SetFlag) != rule.Value) || incremented != null) && events.Count >= 256)
                                throw new InvalidOperationException("Scene event queue limit exceeded before rule transaction");
                            if (!string.IsNullOrEmpty(rule.Effect)) Activate(string.IsNullOrEmpty(rule.Owner) ? "rule:" + rule.Id : "scene:" + rule.Owner, rule.Effect);
                            state.Last = gameplayTime; state.Fired = true;
                            if (!string.IsNullOrEmpty(rule.SetFlag)) SetFlag(rule.SetFlag, rule.Value);
                            if (incremented != null) SetFlag(rule.Increment, incremented);
                            if (!string.IsNullOrEmpty(rule.Sound))
                            {
                                if (PlaySound == null) throw new InvalidOperationException("Native scene sound service was not bound");
                                PlaySound(rule.Sound);
                            }
                        }
                        catch (Exception error) { RecordError("rule:" + rule.Id, error); }
                    }
                }
                if (events.Count > 0) { events.Clear(); foreach (RuleState rule in rules) failedObservers.Add("rule:" + rule.Data.Id); RecordError("events", new InvalidOperationException("Rule cycle exceeded 256 events per tick; reload to re-enable rules")); }
            }
            finally { observing = false; }
        }
        internal string[] InspectRegions()
        { return regions.Select(r => r.Data.Id + ": " + (r.Inside ? "inside" : "outside") + (r.Fired ? ", fired" : "") + ", effect=" + r.Effect).ToArray(); }
        private sealed class Snapshot
        {
            internal SceneBehaviorEngine Owner;
            internal SceneActor Actor;
            internal double Gameplay, Presentation;
            internal ActiveEffect[] Effects;
            internal RegionState[] Regions;
            internal RuleState[] Rules;
            internal Dictionary<string, string> Flags;
            internal SceneEvent[] Events;
            internal string[] Failed;
            internal TreeState[] Trees;
        }
        internal object Capture()
        {
            Check(); if (observing) throw new InvalidOperationException("Cannot capture during scene dispatch");
            return new Snapshot { Owner = this, Actor = actor, Gameplay = gameplayTime, Presentation = presentationTime, Trees = Array.ConvertAll(treeStates, s => s.Clone()),
                Effects = active.Select(e => new ActiveEffect { Id = e.Id, Owner = e.Owner, Definition = e.Definition, Changes = e.Changes, Started = e.Started,
                    Lights = Array.ConvertAll(e.Lights, l => Copy(l)) }).ToArray(),
                Regions = Array.ConvertAll(regions, r => r.Clone()), Rules = Array.ConvertAll(rules, r => r.Clone()), Flags = new Dictionary<string, string>(flags), Events = events.ToArray(), Failed = failedObservers.ToArray() };
        }
        internal void ValidateSnapshot(object value)
        { Check(); var state = value as Snapshot; if (state == null || state.Owner != this) throw new InvalidOperationException("Scene snapshot belongs to another scene or reload generation"); }
        internal void Restore(object value)
        {
            ValidateSnapshot(value); Snapshot state = (Snapshot)value;
            active.Clear(); actor = state.Actor; gameplayTime = state.Gameplay; presentationTime = state.Presentation;
            foreach (ActiveEffect e in state.Effects)
            {
                active.Add(new ActiveEffect { Id = e.Id, Owner = e.Owner, Definition = e.Definition, Changes = e.Changes, Started = e.Started, Lights = Array.ConvertAll(e.Lights, l => Copy(l)) });
                foreach (Change change in e.Changes) touched.Add(change.Property);
            }
            regions = Array.ConvertAll(state.Regions, r => r.Clone()); rules = Array.ConvertAll(state.Rules, r => r.Clone());
            flags.Clear(); foreach (var flag in state.Flags) flags.Add(flag.Key, flag.Value);
            events.Clear(); foreach (SceneEvent name in state.Events) events.Enqueue(name);
            failedObservers.Clear(); foreach (string name in state.Failed) failedObservers.Add(name);
            RestoreTrees(state.Trees);
            Generation = System.Threading.Interlocked.Increment(ref generations);
            RefreshLights(); Recompose();
            if (FlagsChanged != null) FlagsChanged();
        }
    }
}
