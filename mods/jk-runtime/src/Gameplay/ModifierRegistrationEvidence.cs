using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using JumpKing.API;
using JumpKing.Player;
using JumpKing.Mods;

namespace JKRuntime.Gameplay
{
    internal sealed class ModifierBodyEvidence
    {
        internal readonly Dictionary<IBodyCompBehaviour, int> Active = new Dictionary<IBodyCompBehaviour, int>(new BehaviourIdentity());
        internal readonly Dictionary<string, RunModifierSource> Sources = new Dictionary<string, RunModifierSource>(StringComparer.Ordinal);
        internal readonly Dictionary<string, string> UnknownReasons = new Dictionary<string, string>();
        internal uint InitialPeak, Peak;
        internal int Depth;
        internal bool Unknown, Closed;
        internal int ActiveCount { get { return Active.Values.Sum(); } }
        private sealed class BehaviourIdentity : IEqualityComparer<IBodyCompBehaviour>
        {
            public bool Equals(IBodyCompBehaviour a, IBodyCompBehaviour b) { return ReferenceEquals(a, b); }
            public int GetHashCode(IBodyCompBehaviour value) { return RuntimeHelpers.GetHashCode(value); }
        }
    }

    internal sealed class ModifierRegistrationObservation
    {
        internal BodyComp Body;
        internal IBodyCompBehaviour Behaviour;
        internal ModifierBodyEvidence Evidence;
        internal uint External;
        internal int Occurrences, Tracked;
        internal bool Remove, Completed, ExplicitOwner;
        internal string TraceStack;
    }

    internal static class ModifierRegistrationEvidence
    {
        private static readonly FieldInfo External = typeof(BodyComp).GetField("m_externalBehavioursCount", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo Behaviours = typeof(BodyComp).GetField("m_behaviours", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly ConditionalWeakTable<BodyComp, ModifierBodyEvidence> bodies = new ConditionalWeakTable<BodyComp, ModifierBodyEvidence>();
        internal static uint ReadExternal(BodyComp body) { return (uint)External.GetValue(body); }
        internal static ModifierBodyEvidence Get(BodyComp body) { ModifierBodyEvidence value; return bodies.TryGetValue(body, out value) ? value : null; }
        internal static ModifierBodyEvidence Open(BodyComp body, uint peak)
        {
            var old = Get(body);
            if (old != null && (old.Closed || old.InitialPeak > peak)) bodies.Remove(body);
            return bodies.GetValue(body, b => new ModifierBodyEvidence { InitialPeak = peak, Peak = peak });
        }
        internal static void Validate()
        {
            if (External == null || External.FieldType != typeof(uint) || Behaviours == null
                || Behaviours.FieldType != typeof(LinkedList<IBodyCompBehaviour>))
                throw new InvalidOperationException("Required native modifier-registration evidence contract unavailable");
        }
        private static int Count(BodyComp body, IBodyCompBehaviour behaviour)
        {
            var list = (LinkedList<IBodyCompBehaviour>)Behaviours.GetValue(body);
            return list.Count(b => ReferenceEquals(b, behaviour));
        }
        internal static ModifierRegistrationObservation Capture(BodyComp body, IBodyCompBehaviour behaviour, bool remove, bool explicitOwner)
        {
            if (body == null || behaviour == null) return null;
            uint peak = RunModifiers.ReadNativePeak();
            var evidence = bodies.GetValue(body, b => new ModifierBodyEvidence { InitialPeak = peak, Peak = peak });
            // Keep observing teardown for the next native save reset. Closed
            // evidence never updates the completed run's immutable ledger.
            uint external = ReadExternal(body);
            RunModifiers.InvalidateResetCandidate();
            if (evidence.Depth == 0) CheckUnobserved(evidence, external, peak);
            var state = new ModifierRegistrationObservation { Body = body, Behaviour = behaviour, Evidence = evidence,
                External = external, Occurrences = Count(body, behaviour), Remove = remove, ExplicitOwner = explicitOwner };
            evidence.Active.TryGetValue(behaviour, out state.Tracked);
            evidence.Depth++;
            state.TraceStack = RunModifierTrace.Stack();
            if (RunModifierTrace.Enabled)
                RunModifierTrace.Record("call", "body=" + RuntimeHelpers.GetHashCode(body) + "; remove=" + remove
                    + "; explicit=" + explicitOwner + "; behaviour=" + behaviour.GetType().AssemblyQualifiedName
                    + "; external=" + external + "; tracked=" + evidence.ActiveCount + "; peak=" + peak
                    + "; depth=" + evidence.Depth + "\r\n" + state.TraceStack);
            return state;
        }
        internal static void Complete(ModifierRegistrationObservation state, bool success)
        {
            if (state == null || state.Completed) return;
            state.Completed = true;
            var evidence = state.Evidence;
            try
            {
                uint external = ReadExternal(state.Body);
                int count = Count(state.Body, state.Behaviour);
                uint peak = RunModifiers.ReadNativePeak();
                int tracked; evidence.Active.TryGetValue(state.Behaviour, out tracked);
                if (!state.Remove && success && external > state.External && count > state.Occurrences && peak != 0)
                {
                    // Both the explicit API and Harmony see the same call. Use
                    // observed multiplicity, never increment once per observer.
                    evidence.Active[state.Behaviour] = Math.Max(tracked, state.Tracked + count - state.Occurrences);
                    var source = Resolve(state.Behaviour.GetType().Assembly, state.ExplicitOwner);
                    RunModifierSource prior;
                    if (!evidence.Sources.TryGetValue(source.Id, out prior) || state.ExplicitOwner)
                        evidence.Sources[source.Id] = source;
                    evidence.Peak = Math.Max(evidence.Peak, peak);
                }
                if (state.Remove && success && count < state.Occurrences && tracked != 0)
                {
                    int remaining = Math.Min(tracked, Math.Max(0, state.Tracked - (state.Occurrences - count)));
                    if (remaining == 0) evidence.Active.Remove(state.Behaviour); else evidence.Active[state.Behaviour] = remaining;
                }
                if (RunModifierTrace.Enabled)
                    RunModifierTrace.Record("return", "body=" + RuntimeHelpers.GetHashCode(state.Body)
                        + "; behaviour=" + state.Behaviour.GetType().FullName + "; success=" + success
                        + "; remove=" + state.Remove + "; external=" + state.External + "->" + external
                        + "; occurrences=" + state.Occurrences + "->" + count + "; tracked=" + evidence.ActiveCount
                        + "; nativePeak=" + peak + "; depth=" + evidence.Depth);
            }
            finally
            {
                evidence.Depth--;
                if (evidence.Depth == 0)
                {
                    CheckUnobserved(evidence, ReadExternal(state.Body), RunModifiers.ReadNativePeak());
                    RunModifiers.AcceptEvidence(state.Body, evidence);
                    RunModifiers.RefreshResetCandidate(state.Body);
                }
            }
        }
        internal static void CheckUnobserved(ModifierBodyEvidence evidence, uint external, uint peak)
        {
            if (external != evidence.ActiveCount)
                Unknown(evidence, "external-count-mismatch", "external=" + external + "; tracked=" + evidence.ActiveCount);
            if (peak > evidence.Peak)
                Unknown(evidence, "unobserved-native-peak", "previous=" + evidence.Peak + "; native=" + peak);
            evidence.Peak = Math.Max(evidence.Peak, peak);
        }
        private static void Unknown(ModifierBodyEvidence evidence, string kind, string detail)
        {
            evidence.Unknown = true;
            if (evidence.UnknownReasons.ContainsKey(kind)) return;
            string reason = kind + ": " + detail;
            evidence.UnknownReasons.Add(kind, reason);
            if (RunModifierTrace.Enabled) RunModifierTrace.Record("unknown", reason + "\r\n" + RunModifierTrace.Stack());
        }
        private static RunModifierSource Resolve(Assembly assembly, bool explicitOwner)
        {
            string id = assembly.GetName().Name;
            var mods = ModLoader.Instance.LoadedMods.Where(m => m.Assembly == assembly).ToArray();
            string name;
            if (mods.Length == 1) name = mods[0].ModName;
            else if (explicitOwner)
            {
                var title = (AssemblyTitleAttribute)Attribute.GetCustomAttribute(assembly, typeof(AssemblyTitleAttribute));
                name = title == null || string.IsNullOrWhiteSpace(title.Title) ? id : title.Title;
            }
            else name = "Unknown mod (behaviour DLL: " + id + ")";
            return new RunModifierSource { Id = id, Name = name };
        }
    }
}
