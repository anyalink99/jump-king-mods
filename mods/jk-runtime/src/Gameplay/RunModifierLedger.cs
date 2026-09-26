using System;
using System.Collections.Generic;
using System.Linq;

namespace JKRuntime.Gameplay
{
    // Companion metadata only. Never changes the game's save or legitimacy flag.
    public sealed class RunModifierRecord
    {
        public int Schema = 1;
        public string RunKey;
        public uint NativePeak;
        public bool Unknown;
        public List<string> UnknownReasons = new List<string>();
        public List<RunModifierSource> Sources = new List<RunModifierSource>();
        public uint InheritedNativePeak;
        public List<RunModifierSource> InheritedSources = new List<RunModifierSource>();
        public uint ResetNativePeak;
        public List<RunModifierSource> ResetSources = new List<RunModifierSource>();
    }

    public sealed class RunModifierSource
    {
        public string Id;
        public string Name;
    }

    internal sealed class RunModifierLedger
    {
        internal readonly RunModifierRecord Record;
        internal RunModifierLedger(string key, uint nativePeak, RunModifierRecord saved, ModifierResetEvidence reset = null)
        {
            bool matches = saved != null && saved.Schema == 1 && saved.RunKey == key
                && saved.NativePeak <= nativePeak && saved.Sources != null
                && saved.InheritedNativePeak <= saved.NativePeak
                && (saved.InheritedNativePeak == 0 || (saved.InheritedSources != null && saved.InheritedSources.Count != 0
                    && saved.InheritedSources.All(s => s != null && !string.IsNullOrWhiteSpace(s.Id) && !string.IsNullOrWhiteSpace(s.Name))))
                && saved.Sources.All(s => s != null && !string.IsNullOrWhiteSpace(s.Id) && !string.IsNullOrWhiteSpace(s.Name));
            Record = matches ? saved : new RunModifierRecord { RunKey = key, Unknown = nativePeak != 0 };
            if (Record.UnknownReasons == null) Record.UnknownReasons = new List<string>();
            if (Record.Unknown && Record.UnknownReasons.Count == 0)
                Reason(matches ? "legacy-unknown: older history did not record a reason"
                    : "initial-native-flag: no matching history; nativePeak=" + nativePeak);
            if (matches && nativePeak > saved.NativePeak)
                Reason("saved-peak-increased: saved=" + saved.NativePeak + "; native=" + nativePeak);
            Record.NativePeak = nativePeak;
            if (!matches && reset != null && reset.Matches(key, nativePeak))
            {
                Record.Unknown = false;
                Record.UnknownReasons.Clear();
                Record.InheritedNativePeak = reset.Count;
                foreach (var source in reset.Sources)
                    Record.InheritedSources.Add(new RunModifierSource { Id = source.Id, Name = source.Name });
            }
        }
        internal bool Reason(string reason)
        {
            Record.Unknown = true;
            if (Record.UnknownReasons.Contains(reason) || Record.UnknownReasons.Count >= 16) return false;
            Record.UnknownReasons.Add(reason);
            return true;
        }
        internal bool Add(string id, string name)
        {
            if (Record.Sources.Any(s => s.Id == id)) return false;
            Record.Sources.Add(new RunModifierSource { Id = id, Name = name });
            return true;
        }
        internal bool Observe(uint external, int knownActive, uint peak, bool ownRegistration)
        {
            bool unknown = external > knownActive || (!ownRegistration && peak > Record.NativePeak);
            bool changed = peak > Record.NativePeak || (unknown && !Record.Unknown);
            Record.NativePeak = Math.Max(Record.NativePeak, peak);
            Record.Unknown |= unknown;
            return changed;
        }
        internal string[] Names()
        {
            var inherited = Record.InheritedSources ?? new List<RunModifierSource>();
            var names = Record.Sources.Concat(inherited).Select(s => s.Name).Distinct().OrderBy(s => s, StringComparer.Ordinal).ToList();
            if (Record.Unknown || (names.Count == 0 && Record.NativePeak != 0 && Record.InheritedNativePeak == 0)) names.Add("Unknown source / earlier session");
            return names.ToArray();
        }
        internal string[] ResultRows()
        {
            return Names();
        }
    }
}
