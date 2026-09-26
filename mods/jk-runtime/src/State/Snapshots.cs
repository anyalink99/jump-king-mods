using System;
using System.Collections.Generic;
using System.Linq;

namespace JKRuntime.State
{
    /// <summary>Versioned transaction participant. Capture and Validate must not mutate gameplay; Restore must support rollback to captured state.</summary>
    public interface IStateParticipant
    {
        string Id { get; }
        int Version { get; }
        object Capture();
        void Validate(object snapshot);
        void Restore(object snapshot);
    }
    public sealed class StateSnapshot
    {
        internal long Generation;
        internal object[] Values;
        internal IStateParticipant[] Owners;
        internal int[] Versions;
        public string[] Participants { get { return Owners.Select(p => p.Id).ToArray(); } }
    }
    /// <summary>Game-thread state transactions. Failed rollback poisons further operations. RestoreEpoch advances after validation and undo capture, before application, including applications that roll back.</summary>
    public sealed class SnapshotService
    {
        private readonly SortedDictionary<string, IStateParticipant> participants = new SortedDictionary<string, IStateParticipant>(StringComparer.Ordinal);
        private long generation;
        private bool busy, poisoned;
        public long RestoreEpoch { get; private set; }
        public IDisposable Register(IStateParticipant participant)
        {
            Check();
            if (participant == null || participant.Version < 1) throw new ArgumentException("Invalid state participant");
            ModuleDefinition.ValidId(participant.Id);
            participants.Add(participant.Id, participant); generation++;
            return new ActionLease(delegate { Check(); participants.Remove(participant.Id); generation++; });
        }
        private void Check()
        {
            RuntimeApi.Kernel.CheckThread();
            if (busy || poisoned) throw new InvalidOperationException("State transaction is reentrant or a previous rollback failed");
        }
        /// <summary>Capture the registered participant set. Snapshots expire when registration or participant versions change.</summary>
        public StateSnapshot Capture()
        {
            Check(); busy = true;
            try { var owners = participants.Values.ToArray(); return new StateSnapshot { Generation = generation, Owners = owners, Versions = owners.Select(p => p.Version).ToArray(), Values = owners.Select(p => p.Capture()).ToArray() }; }
            finally { busy = false; }
        }
        public bool IsCurrent(StateSnapshot snapshot)
        { RuntimeApi.Kernel.CheckThread(); return snapshot != null && snapshot.Generation == generation && participants.Values.SequenceEqual(snapshot.Owners) && participants.Values.Select(p => p.Version).SequenceEqual(snapshot.Versions); }
        /// <summary>Validate participants, capture undo state, then restore. Failure attempts reverse rollback and reports incomplete cleanup.</summary>
        public void Restore(StateSnapshot snapshot)
        {
            Check();
            if (!IsCurrent(snapshot))
                throw new InvalidOperationException("Snapshot belongs to a different level or participant set");
            busy = true;
            try
            {
                for (int i = 0; i < snapshot.Owners.Length; i++) snapshot.Owners[i].Validate(snapshot.Values[i]);
                object[] undo = snapshot.Owners.Select(p => p.Capture()).ToArray();
                // Hardware timestamps are monotonic evidence, not rewindable state.
                // Invalidate consumers even when an attempted restore rolls back.
                RestoreEpoch++;
                Gameplay.GameplayEvents.Emit(Gameplay.GameplayEventKind.RestoreStarted, "runtime.state", Microsoft.Xna.Framework.Vector2.Zero, Microsoft.Xna.Framework.Vector2.Zero, -1);
                int applied = -1;
                try
                {
                    for (int i = 0; i < snapshot.Owners.Length; i++) { applied = i; snapshot.Owners[i].Restore(snapshot.Values[i]); }
                    Gameplay.GameplayEvents.Emit(Gameplay.GameplayEventKind.RestoreCompleted, "runtime.state", Microsoft.Xna.Framework.Vector2.Zero, Microsoft.Xna.Framework.Vector2.Zero, -1);
                }
                catch (Exception failure)
                {
                    var failures = new List<Exception> { failure };
                    for (int i = applied; i >= 0; i--)
                        try { snapshot.Owners[i].Restore(undo[i]); } catch (Exception rollback) { failures.Add(rollback); }
                    if (failures.Count > 1) poisoned = true;
                    Gameplay.GameplayEvents.Emit(Gameplay.GameplayEventKind.RestoreFailed, "runtime.state", Microsoft.Xna.Framework.Vector2.Zero, Microsoft.Xna.Framework.Vector2.Zero, -1);
                    throw new AggregateException("State restore failed; rollback " + (poisoned ? "incomplete" : "completed"), failures);
                }
            }
            finally { busy = false; }
        }
    }
    public static class GameState
    {
        public static readonly SnapshotService Snapshots = new SnapshotService();
    }
}
