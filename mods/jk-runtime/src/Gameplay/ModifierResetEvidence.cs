using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JumpKing.Player;

namespace JKRuntime.Gameplay
{
    // Only a witnessed native save reset can transfer attribution across attempts.
    // A matching count alone is never evidence that two runs have the same cause.
    internal sealed class ModifierResetEvidence
    {
        internal string PreviousKey;
        internal uint Count;
        internal readonly List<RunModifierSource> Sources = new List<RunModifierSource>();
        private static readonly PropertyInfo NativeCount = typeof(BodyComp).Assembly
            .GetType("JumpKing.MiscSystems.FullRunManager", true)
            .GetProperty("ModifiersCount", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        internal static uint ReadNativeCount() { return NativeCount == null ? uint.MaxValue : (uint)NativeCount.GetValue(null, null); }

        internal static ModifierResetEvidence Capture(BodyComp body, string key)
        {
            if (body == null || key == null || NativeCount == null) return null;
            var evidence = ModifierRegistrationEvidence.Get(body);
            uint count = ReadNativeCount();
            if (count == 0 || evidence == null || evidence.Depth != 0 || evidence.Unknown
                || count != evidence.ActiveCount || count != ModifierRegistrationEvidence.ReadExternal(body)) return null;
            var result = new ModifierResetEvidence { PreviousKey = key, Count = count };
            foreach (var behaviour in evidence.Active.Keys)
            {
                string id = behaviour.GetType().Assembly.GetName().Name;
                RunModifierSource source;
                if (!evidence.Sources.TryGetValue(id, out source)) return null;
                if (!result.Sources.Any(s => s.Id == id))
                    result.Sources.Add(new RunModifierSource { Id = id, Name = source.Name });
            }
            return result;
        }
        internal bool Matches(string key, uint initialPeak)
        {
            return key != PreviousKey && Root(key) == Root(PreviousKey) && Count == initialPeak;
        }
        internal void StoreIn(RunModifierRecord record)
        {
            record.ResetNativePeak = Count;
            record.ResetSources = Sources.Select(s => new RunModifierSource { Id = s.Id, Name = s.Name }).ToList();
        }
        internal static ModifierResetEvidence FromSaved(RunModifierRecord record)
        {
            if (record == null || record.Schema != 1 || string.IsNullOrEmpty(record.RunKey) || record.ResetNativePeak == 0
                || record.ResetSources == null || record.ResetSources.Count == 0
                || record.ResetSources.Any(s => s == null || string.IsNullOrWhiteSpace(s.Id) || string.IsNullOrWhiteSpace(s.Name))) return null;
            var result = new ModifierResetEvidence { PreviousKey = record.RunKey, Count = record.ResetNativePeak };
            foreach (var source in record.ResetSources)
                result.Sources.Add(new RunModifierSource { Id = source.Id, Name = source.Name });
            return result;
        }
        private static string Root(string key)
        { int split = key.IndexOf('|'); return split < 0 ? "" : key.Substring(0, split); }
    }
}
