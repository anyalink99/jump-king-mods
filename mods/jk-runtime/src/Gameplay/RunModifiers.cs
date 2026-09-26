using System;
using System.IO;
using System.Reflection;
using JumpKing;
using JumpKing.API;
using JumpKing.GameManager;
using JumpKing.Player;
using JumpKing.MiscSystems.Achievements;
using JumpKing.SaveThread.SaveComponents;
using JKRuntime.Settings;

namespace JKRuntime.Gameplay
{
    // Optional for other authors: use these native-equivalent registration
    // methods to attribute even a marker registered and removed in one tick.
    // BodyPipeline does this automatically. No Harmony version is required.
    public static class RunModifiers
    {
        private static readonly FieldInfo External = typeof(BodyComp).GetField("m_externalBehavioursCount", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly PropertyInfo Snapshot = typeof(BodyComp).Assembly.GetType("JumpKing.SaveThread.SaveLube", true).GetProperty("PlayerStatsAttemptSnapshot", BindingFlags.Public | BindingFlags.Static);
        private static readonly FieldInfo Completion = typeof(GameLoop).GetField("m_ending_body_modifiers", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo Counter = Completion == null ? null : Completion.FieldType.GetField("counter");
        private static ModifierBodyEvidence evidence;
        private static BodyComp body;
        private static WeakReference lastBody;
        private static volatile ModifierResetEvidence resetCandidate, pendingReset;
        private static RunModifierLedger ledger;
        private static RunModifierStore store;
        private static bool collecting, diskFailed;
        private static ModifierResultsOverlay overlay;

        public static bool Register(BodyComp target, IBodyCompBehaviour behaviour)
        { return Add(target, behaviour, delegate { return target.RegisterBehaviour(behaviour); }); }
        public static bool RegisterBefore(BodyComp target, IBodyCompBehaviour behaviour, IBodyCompBehaviour anchor)
        { return Add(target, behaviour, delegate { return target.RegisterBehaviourBefore(behaviour, anchor); }); }
        public static bool RegisterAfter(BodyComp target, IBodyCompBehaviour behaviour, IBodyCompBehaviour anchor)
        { return Add(target, behaviour, delegate { return target.RegisterBehaviourAfter(behaviour, anchor); }); }
        public static bool Remove(BodyComp target, IBodyCompBehaviour behaviour)
        {
            if (target == null) throw new ArgumentNullException("target");
            return Invoke(target, behaviour, true, delegate { return target.RemoveBehaviour(behaviour); });
        }
        private static bool Add(BodyComp target, IBodyCompBehaviour behaviour, Func<bool> register)
        {
            if (target == null) throw new ArgumentNullException("target");
            if (behaviour == null) throw new ArgumentNullException("behaviour");
            return Invoke(target, behaviour, false, register);
        }
        private static bool Invoke(BodyComp target, IBodyCompBehaviour behaviour, bool remove, Func<bool> call)
        {
            RuntimeApi.Kernel.CheckThread();
            ModifierRegistrationObservation state = null;
            try { state = ModifierRegistrationEvidence.Capture(target, behaviour, remove, true); }
            catch (Exception error) { Console.WriteLine("[JK Runtime] Modifier evidence unavailable: " + error.Message); }
            bool result = false;
            try { result = call(); return result; }
            finally
            {
                try { ModifierRegistrationEvidence.Complete(state, result); }
                catch (Exception error) { Console.WriteLine("[JK Runtime] Modifier evidence unavailable: " + error.Message); }
            }
        }
        internal static void ValidateContract()
        {
            if (External == null || External.FieldType != typeof(uint) || Snapshot == null
                || Snapshot.PropertyType != typeof(PlayerStats) || Counter == null || Counter.FieldType != typeof(uint))
                throw new InvalidOperationException("Required native run-modifier attribution contract unavailable");
            ModifierResultsOverlay.ValidateContract();
            ModifierRegistrationEvidence.Validate();
        }
        internal static string RunKey(string root, PlayerStats snapshot)
        {
            // The attempt-start snapshot is stable across quits/resumes, unlike
            // the current session counter; victory/new-game takes a new snapshot.
            return Path.GetFullPath(root).ToLowerInvariant() + "|" + snapshot.attempts + "|"
                + snapshot.session + "|" + snapshot._ticks + "|" + snapshot.jumps + "|" + snapshot.falls;
        }
        internal static void Start()
        {
            ValidateContract();
            if (overlay != null && overlay.IsAlive) overlay.Destroy();
            PlayerStats snapshot;
            using (StartupTrace.Measure("run-modifiers.native-attempt-snapshot")) snapshot = (PlayerStats)Snapshot.GetValue(null, null);
            Begin(GameLoop.m_player.m_body, RunKey(Game1.instance.contentManager.root, snapshot),
                FullRunSave.fullRunSave.CurrentBodyCompModifiers,
                Path.GetFullPath(Path.Combine("Content", "Saves", "JKRuntime.RunModifiers.xml")));
            overlay = new ModifierResultsOverlay();
        }
        internal static void Begin(BodyComp target, string key, uint nativePeak, string savePath)
        {
            RunModifierTrace.Initialize();
            body = target;
            lastBody = new WeakReference(target);
            evidence = ModifierRegistrationEvidence.Open(target, nativePeak);
            diskFailed = false;
            if (store == null || !string.Equals(store.Path, savePath, StringComparison.OrdinalIgnoreCase))
                store = new RunModifierStore(savePath);
            RunModifierRecord saved = null;
            try { using (StartupTrace.Measure("run-modifiers.read")) saved = store.Read(); }
            catch (Exception error) { Console.WriteLine("[JK Runtime] Attribution history unreadable: " + error.Message); }
            // Early OnLevelStart registrations already raised nativePeak. Match
            // persisted history against the peak BEFORE those captured calls.
            string savedPeak = saved == null ? "none" : saved.NativePeak.ToString();
            ledger = new RunModifierLedger(key, evidence.InitialPeak, saved, pendingReset ?? ModifierResetEvidence.FromSaved(saved));
            pendingReset = null;
            if (RunModifierTrace.Enabled)
            {
                RunModifierTrace.Record("begin", "run=" + key + "; body=" + System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(target)
                    + "; nativePeak=" + nativePeak + "; beforeObservedCalls=" + evidence.InitialPeak
                    + "; external=" + ReadExternal() + "; tracked=" + evidence.ActiveCount
                    + "; observer=" + ModifierRegistrationObserver.Status
                    + "; savedRun=" + (saved == null ? "none" : saved.RunKey)
                    + "; savedPeak=" + savedPeak
                    + "; reasons=" + string.Join(" | ", ledger.Record.UnknownReasons));
                foreach (var item in target.GetBehaviourList())
                    RunModifierTrace.Record("body-at-begin", item.GetType().AssemblyQualifiedName);
            }
            collecting = true;
            AcceptEvidence(body, evidence); Observe(); Save();
            RefreshResetCandidate(target);
        }
        internal static void Observe()
        {
            if (!collecting || ledger == null || body == null) return;
            if (evidence.Depth != 0) return;
            ModifierRegistrationEvidence.CheckUnobserved(evidence, ReadExternal(), ReadNativePeak());
            if (evidence.Unknown) InvalidateResetCandidate();
            AcceptEvidence(body, evidence);
        }
        internal static void AcceptEvidence(BodyComp target, ModifierBodyEvidence value)
        {
            if (!collecting || target != body || ledger == null) return;
            bool changed = false;
            foreach (var source in value.Sources.Values) changed |= ledger.Add(source.Id, source.Name);
            foreach (var reason in value.UnknownReasons.Values) changed |= ledger.Reason(reason);
            changed |= value.Unknown && !ledger.Record.Unknown;
            ledger.Record.Unknown |= value.Unknown;
            changed |= ledger.Observe(ReadExternal(), value.ActiveCount, value.Peak, true);
            if (changed) Save();
        }
        private static uint ReadExternal() { return (uint)External.GetValue(body); }
        internal static uint ReadNativePeak()
        {
            object completion = GameLoop.instance == null ? null : Completion.GetValue(GameLoop.instance);
            return Math.Max(FullRunSave.fullRunSave.CurrentBodyCompModifiers, completion == null ? 0 : (uint)Counter.GetValue(completion));
        }
        internal static void Finish()
        {
            if (collecting && RunModifierTrace.Enabled)
                RunModifierTrace.Record("finish", "run=" + ledger.Record.RunKey + "; observer=" + ModifierRegistrationObserver.Status);
            Observe(); collecting = false; body = null;
            if (evidence != null) evidence.Closed = true;
            // The foreground observer must outlive module/player teardown: the
            // native StatsScreen runs later, after the ending picture.
        }
        internal static void Unload()
        {
            Finish();
            if (overlay != null && overlay.IsAlive) overlay.Destroy();
            overlay = null;
        }
        public static string[] GetContributors()
        { RuntimeApi.Kernel.CheckThread(); return ledger == null ? new string[0] : ledger.Names(); }
        /// <summary>Detached registration evidence for the current or last completed attempt.
        /// Game-thread only. Null means unavailable. Registrations do not prove feature use.</summary>
        public static RunModifierRecord GetEvidence()
        {
            RuntimeApi.Kernel.CheckThread();
            if (collecting) Observe();
            if (ledger == null) return null;
            var r = ledger.Record;
            var copy = new RunModifierRecord { Schema=r.Schema, RunKey=r.RunKey, NativePeak=r.NativePeak,
                Unknown=r.Unknown || diskFailed, InheritedNativePeak=r.InheritedNativePeak, ResetNativePeak=r.ResetNativePeak };
            copy.UnknownReasons.AddRange(r.UnknownReasons);
            if (diskFailed) copy.UnknownReasons.Add("Attribution persistence failed");
            foreach (var s in r.Sources) copy.Sources.Add(new RunModifierSource { Id=s.Id, Name=s.Name });
            foreach (var s in r.InheritedSources) copy.InheritedSources.Add(new RunModifierSource { Id=s.Id, Name=s.Name });
            foreach (var s in r.ResetSources) copy.ResetSources.Add(new RunModifierSource { Id=s.Id, Name=s.Name });
            return copy;
        }
        internal static string[] GetUnknownReasons()
        { return ledger == null ? new string[0] : ledger.Record.UnknownReasons.ToArray(); }
        internal static string[] GetResultRows()
        { return ledger == null ? new[] { "Attribution unavailable" } : ledger.ResultRows(); }
        internal static uint InheritedNativePeak { get { return ledger == null ? 0 : ledger.Record.InheritedNativePeak; } }
        internal static string[] InheritedNames()
        {
            return ledger == null || ledger.Record.InheritedSources == null ? new string[0]
                : ledger.Record.InheritedSources.ConvertAll(s => s.Name).ToArray();
        }
        internal static ModifierResetEvidence CaptureReset()
        {
            // DeleteSaves runs on the native save worker. Only consume a copied,
            // immutable game-thread snapshot here; never traverse player state.
            pendingReset = null;
            var candidate = resetCandidate;
            return candidate != null && candidate.Count == ModifierResetEvidence.ReadNativeCount() ? candidate : null;
        }
        internal static void CompleteReset(ModifierResetEvidence reset)
        {
            pendingReset = reset != null && ReferenceEquals(reset, resetCandidate)
                && ModifierResetEvidence.ReadNativeCount() == reset.Count
                && FullRunSave.fullRunSave.CurrentBodyCompModifiers == reset.Count ? reset : null;
            if (pendingReset != null && store != null)
            {
                // This can run after the last playable attempt. Persist the
                // copied reset metadata so quitting on results does not lose it.
                store.SubmitReset(reset);
            }
            if (pendingReset != null && RunModifierTrace.Enabled)
                RunModifierTrace.Record("native-reset", "previousRun=" + reset.PreviousKey + "; inheritedCount=" + reset.Count
                    + "; sources=" + string.Join(" | ", reset.Sources.ConvertAll(s => s.Name)));
        }
        internal static void CancelReset() { pendingReset = null; }
        internal static void InvalidateResetCandidate() { resetCandidate = null; }
        internal static void RefreshResetCandidate(BodyComp target)
        {
            resetCandidate = lastBody != null && ReferenceEquals(lastBody.Target, target)
                ? ModifierResetEvidence.Capture(target, ledger == null ? null : ledger.Record.RunKey) : null;
        }
        internal static bool WaitForWrites(int milliseconds) { return store == null || store.Wait(milliseconds); }
        private static void Save()
        {
            if (RunModifierTrace.Enabled)
                RunModifierTrace.Record("ledger", "run=" + ledger.Record.RunKey + "; peak=" + ledger.Record.NativePeak
                    + "; sources=" + string.Join(" | ", ledger.Names()) + "; reasons=" + string.Join(" | ", ledger.Record.UnknownReasons));
            if (diskFailed) return;
            var reset = pendingReset;
            if (reset != null && reset.PreviousKey == ledger.Record.RunKey) reset.StoreIn(ledger.Record);
            try { using (StartupTrace.Measure("run-modifiers.queue-write")) store.Submit(ledger.Record); }
            catch (Exception error)
            {
                diskFailed = true;
                Console.WriteLine("[JK Runtime] Attribution history could not be saved; current-session display remains available: " + error);
            }
        }
    }
}
