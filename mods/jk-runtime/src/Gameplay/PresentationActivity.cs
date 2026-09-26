using System;
using System.Collections.Generic;
using System.Linq;
using JumpKing.Player;

namespace JKRuntime.Gameplay
{
    /// <summary>Marks live presentation ticks while an actor's physics is suspended.</summary>
    /// <remarks>Does not disable components, stop clocks, serialize effects or grant controller ownership.</remarks>
    public static class PresentationActivity
    {
        private sealed class Entry { internal string Owner; internal BodyComp Body; }
        private static readonly List<Entry> entries = new List<Entry>();

        public static IDisposable Begin(string owner, BodyComp body)
        {
            RuntimeApi.Kernel.CheckThread();
            owner = ModuleDefinition.ValidId(owner);
            if (body == null) throw new ArgumentNullException("body");
            var entry = new Entry { Owner = owner, Body = body };
            entries.Add(entry);
            return new ActionLease(delegate { RuntimeApi.Kernel.CheckThread(); entries.Remove(entry); });
        }

        public static bool IsActive(BodyComp body)
        {
            RuntimeApi.Kernel.CheckThread();
            if (body == null) return false;
            foreach (var entry in entries) if (ReferenceEquals(entry.Body, body)) return true;
            return false;
        }

        public static string[] GetOwners(BodyComp body)
        {
            RuntimeApi.Kernel.CheckThread();
            return entries.Where(e => ReferenceEquals(e.Body, body)).Select(e => e.Owner).Distinct().OrderBy(s => s, StringComparer.Ordinal).ToArray();
        }
    }
}
