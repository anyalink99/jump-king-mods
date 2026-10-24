using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using JKRuntime;
using JKRuntime.World;
using JumpKing;

namespace MegaMappingExpansion
{
    internal sealed class NativeTransitions : ISideLinkRouter, JKRuntime.State.IStateParticipant
    {
        internal static NativeTransitions Current;
        internal TransitionEngine Engine { get; private set; }
        private ScenePersistence store;
        private int saveRevision;
        private bool active;
        private static readonly WorldStateCodec Codec = new WorldStateCodec(typeof(TransitionState), true);
        internal NativeTransitions(TransitionPlan plan, Func<string> directory)
        {
            Engine = new TransitionEngine(plan, NewSeed(plan));
            Dictionary<string, string> saved;
            TransitionSaveCoordinator.Load(plan.SaveId, directory, out store, out saved, out saveRevision);
            if (saved.Count != 0) Engine.Restore(Decode(saved));
        }
        private static ulong NewSeed(TransitionPlan plan)
        {
            if (plan.FixedSeed.HasValue) return plan.FixedSeed.Value;
            var bytes = new byte[8]; using (var random = RandomNumberGenerator.Create()) random.GetBytes(bytes);
            return BitConverter.ToUInt64(bytes, 0);
        }
        internal IDisposable Activate(JumpKing.Player.PlayerEntity player)
        {
            if (Current != null || player == null) throw new InvalidOperationException("Transition activation needs a live player and no previous owner");
            var scope = new RuntimeScope();
            try
            {
                Current = this; active = true;
                scope.Defer(delegate { active = false; if (Current == this) Current = null; Engine.Changed = Engine.LinksChanged = null; });
                Engine.LinksChanged = ApplyLinks; Engine.Changed = Publish;
                scope.Own(WorldRegistry.Shared.Bind<TransitionState>("mega.mapping.transitions", () => Engine.State, state => Restore(state), state => Validate(state)));
                scope.Own(JKRuntime.State.GameState.Snapshots.Register(this));
                scope.Own(JKRuntime.Gameplay.GameplayEvents.Subscribe("mega-mapping-expansion.transitions", value => {
                    if (value.Kind == JKRuntime.Gameplay.GameplayEventKind.RestoreCompleted || value.Kind == JKRuntime.Gameplay.GameplayEventKind.RestoreFailed) Publish();
                }));
                scope.Own(NativeSideLinks.RegisterRouter("mega-mapping-expansion", this));
                scope.Own(JKRuntime.Gameplay.PlayerUpdates.Register(player, "mega-mapping-expansion.transitions", JKRuntime.Gameplay.PlayerUpdatePhase.BeforeInput, delta => {
                    SynchronizeReset();
                    var box = player.m_body.GetHitbox(); int screen = Camera.CurrentScreen;
                    box.Y += screen * 360; Engine.Tick(screen, box);
                }));
                ApplyLinks(); Publish();
                ModEntry.Log("Transitions active: pools=" + Engine.Plan.Pools.Count + ", seed=" + Engine.Seed);
                return scope;
            }
            catch { scope.Dispose(); throw; }
        }
        private void ApplyLinks()
        { if (active) foreach (var link in Engine.Links()) NativeSideLinks.UpdateOwned("mega-mapping-expansion", link.Screen, link.Side, link.Target); }
        private void Publish()
        {
            if (!active || store == null || WorldControl.Following || !WorldExecution.CanPersist) return;
            TransitionSaveCoordinator.Publish(store, Encode(Engine.State));
        }
        private void SynchronizeReset()
        {
            if (saveRevision == System.Threading.Volatile.Read(ref TransitionSaveCoordinator.Revision)) return;
            ScenePersistence next;
            if (!TransitionSaveCoordinator.TryResetStore(store, out next, out saveRevision)) return;
            store = next;
            var engine = new TransitionEngine(Engine.Plan, NewSeed(Engine.Plan));
            Engine.Changed = Engine.LinksChanged = null; Engine = engine;
            Engine.Changed = Publish; Engine.LinksChanged = ApplyLinks; Engine.Publish();
        }
        private static Dictionary<string, string> Encode(TransitionState state)
        {
            string bytes = Convert.ToBase64String(Codec.Capture(state));
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i < bytes.Length; i += 250) result.Add((i / 250).ToString("D3", System.Globalization.CultureInfo.InvariantCulture), bytes.Substring(i, Math.Min(250, bytes.Length - i)));
            return result;
        }
        private static TransitionState Decode(Dictionary<string, string> values)
        {
            for (int i = 0; i < values.Count; i++) if (!values.ContainsKey(i.ToString("D3", System.Globalization.CultureInfo.InvariantCulture))) throw new System.IO.InvalidDataException("Incomplete transition save");
            return (TransitionState)Codec.Decode(Convert.FromBase64String(string.Concat(values.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Value))));
        }
        internal static void Emit(string name) { if (Current != null && !WorldControl.Following) { Current.SynchronizeReset(); Current.Engine.Emit(name); } }
        public bool Owns(int screen, int side) { return Engine.Owns(screen, side); }
        public ISideLinkDecision Prepare(int screen, int side) { SynchronizeReset(); return Engine.Prepare(screen, side); }
        public string Id { get { return Engine.Id; } }
        public int Version { get { return 1; } }
        public object Capture() { return Engine.Capture(); }
        public void Validate(object state) { Engine.Validate(state); }
        public void Restore(object state) { Engine.Restore(state); }
    }

    // a separate immutable packet lets native save workers handle routes without a scene
    internal static class TransitionSaveCoordinator
    {
        private static readonly object Sync = new object(), WriteSync = new object();
        private static ScenePersistence store;
        private static Dictionary<string, string> snapshot;
        private static long published, saved;
        private static string resetDirectory, resetEpoch, lastError;
        private static bool resetting;
        internal static int Revision;
        internal static void Load(string id, Func<string> directory, out ScenePersistence target, out Dictionary<string, string> values, out int revision)
        {
            lock (WriteSync) { target = new ScenePersistence("transitions:" + id, directory); values = target.Load(); lock (Sync) revision = Revision; }
        }
        internal static bool TryResetStore(ScenePersistence previous, out ScenePersistence next, out int revision)
        { lock (Sync) { revision = Revision; next = resetting || previous == null ? null : previous.AfterReset(resetDirectory, resetEpoch); return !resetting; } }
        internal static void Publish(ScenePersistence target, Dictionary<string, string> values)
        { lock (Sync) { if (resetting) return; store = target; snapshot = new Dictionary<string, string>(values, StringComparer.Ordinal); published++; } }
        internal static void Save()
        {
            lock (WriteSync)
            {
                ScenePersistence target; Dictionary<string, string> values; long generation;
                lock (Sync) { if (store == null || saved == published) return; target = store; values = snapshot; generation = published; }
                try { target.Save(values); lock (Sync) { if (generation == published) saved = generation; lastError = null; } }
                catch (Exception error) { bool report; lock (Sync) { report = lastError != error.Message; lastError = error.Message; } if (report) ModEntry.Log("Transition save remains pending: " + error); }
            }
        }
        internal static void Reset(string directory)
        {
            lock (Sync) { resetting = true; store = null; snapshot = null; System.Threading.Interlocked.Increment(ref Revision); }
            lock (WriteSync)
            {
                string epoch = null;
                try { epoch = SceneSaveCoordinator.ResetEpochFor(directory); }
                catch (Exception error) { ModEntry.Log("Transition save reset failed: " + error); }
                finally { lock (Sync) { resetDirectory = System.IO.Path.GetFullPath(directory); resetEpoch = epoch; resetting = false; System.Threading.Interlocked.Increment(ref Revision); } }
            }
        }
    }
}
