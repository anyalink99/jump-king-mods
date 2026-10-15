using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace WardrobePlus
{
    internal static partial class Controller
    {
        private sealed class EditState { internal Outfit Outfit; internal bool Enabled; }
        private static readonly List<EditState> undo = new List<EditState>(), redo = new List<EditState>();
        private static Action liveChange;
        private static string liveKey, historyKey;
        private static bool historyInitialized;
        private static long saveAfter;
        internal static bool Unsaved { get; private set; }
        internal static bool CanUndo { get { return undo.Count != 0 || (!historyInitialized && Data.Undo != null); } }
        internal static bool CanRedo { get { return redo.Count != 0; } }
        internal static Outfit Snapshot()
        { var result = Data.Enabled ? Data.Current.Copy() : NativeOutfit(); result.Equipment = EquipmentService.Capture(); return result; }
        private static EditState State()
        { var outfit = Data.Current.Copy(); outfit.Equipment = EquipmentService.Capture(); return new EditState { Outfit = outfit, Enabled = Data.Enabled }; }
        internal static void Edit(Outfit outfit, string gesture = null, bool restoreEquipment = false)
        {
            if (gesture == null || liveKey != gesture) QueueLive();
            var next = outfit.Copy(); var equipment = restoreEquipment && next.Equipment != null ? next.Equipment.Copy() : null;
            liveKey = gesture;
            liveChange = () => Commit(data => { data.Current = next; data.Enabled = true; }, true, "Saved", equipment, gesture, true);
        }
        private static void QueueLive()
        { if (liveChange != null) { pending.Enqueue(liveChange); liveChange = null; liveKey = null; } }
        internal static void ToggleEquipment(int item)
        {
            QueueLive();
            pending.Enqueue(() => Commit(data => { }, false, "Equipment saved", EquipmentService.Toggle(item), null, true));
        }
        internal static void LoadPreset(Outfit outfit) { Edit(outfit, null, true); }
        internal static void EndGesture() { Flush(); historyKey = null; }
        internal static bool Flush()
        { if (!initialized) return true; Pump(); return FlushSave(); }
        private static bool FlushSave()
        {
            if (!Unsaved) return true;
            try { Store.Save(Data); Unsaved = false; LastError = ""; Status = "Saved"; return true; }
            catch (Exception error)
            {
                LastError = error.GetBaseException().Message; Status = "Not saved: " + LastError;
                saveAfter = Stopwatch.GetTimestamp() + Stopwatch.Frequency; return false;
            }
        }
        private static void Commit(Action<WardrobeData> change, bool appearance, string message, EquipmentSelection equipment, string gesture, bool record)
        {
            if (Store.ReadOnly) throw new InvalidOperationException(Store.Warning.Length == 0 ? "Settings are read-only." : Store.Warning);
            if (gesture == null && !FlushSave()) throw new InvalidOperationException(Status);
            var before = State(); var candidate = Data.Copy(); change(candidate);
            if (appearance && candidate.Enabled) candidate.ImportedNative = true;
            if (record && (gesture == null || historyKey != gesture)) candidate.Undo = before.Outfit.Copy();
            candidate.Current.Equipment = equipment == null ? before.Outfit.Equipment.Copy() : equipment.Copy();
            Store.Validate(candidate);
            PreparedAppearance prepared = null;
            if (appearance)
            {
                if (candidate.Enabled && !Hooks.Installed) throw new InvalidOperationException(Hooks.Error);
                prepared = gesture != null && candidate.Enabled && Active != null && Active.CanRefit(candidate.Current)
                    ? Active.Refit(candidate.Current)
                    : PreparedAppearance.Build(candidate.Enabled ? candidate.Current : NativeOutfit(), Catalog, false);
            }
            var oldOptions = EquipmentService.SnapshotOptions(); bool saved = false, equipmentTouched = false;
            string warning = "";
            try
            {
                if (gesture == null) { Store.Save(candidate); saved = true; }
                if (equipment != null) { equipmentTouched = true; warning = EquipmentService.Restore(equipment); }
                if (prepared != null) NativeAppearance.Publish(prepared);
            }
            catch (Exception failure)
            {
                var failures = new List<Exception> { failure };
                if (equipmentTouched)
                {
                    try { EquipmentService.Restore(before.Outfit.Equipment); } catch (Exception error) { failures.Add(error); }
                    try { EquipmentService.RestoreOptions(oldOptions); } catch (Exception error) { failures.Add(error); }
                }
                try { if (prepared != null) prepared.Dispose(); } catch (Exception error) { failures.Add(error); }
                try { if (saved) Store.Save(Data); } catch (Exception error) { failures.Add(error); }
                if (failures.Count == 1) throw;
                throw new AggregateException("Outfit change failed; some rollback operations failed", failures);
            }
            Data = candidate;
            if (prepared != null) ReplaceActive(prepared);
            if (appearance || equipment != null) AppearanceEvents.Notify();
            if (record)
            {
                historyInitialized = true;
                if (gesture == null || historyKey != gesture)
                { undo.Add(before); if (undo.Count > 64) undo.RemoveAt(0); }
                redo.Clear(); historyKey = gesture;
            }
            if (gesture != null) { Unsaved = true; saveAfter = Stopwatch.GetTimestamp() + Stopwatch.Frequency / 4; }
            LastError = ""; Status = warning.Length != 0 ? warning : gesture == null ? message : "Saving...";
        }
        private static void NavigateHistory(bool forward)
        {
            QueueLive();
            pending.Enqueue(() => {
                var from = forward ? redo : undo; var to = forward ? undo : redo;
                if (from.Count == 0)
                {
                    if (!forward && !historyInitialized && Data.Undo != null) { from.Add(new EditState { Outfit = Data.Undo.Copy(), Enabled = true }); historyInitialized = true; }
                    else { Status = forward ? "Nothing to redo" : "Nothing to undo"; return; }
                }
                var previous = State(); var target = from[from.Count - 1];
                Commit(data => { data.Current = target.Outfit.Copy(); data.Enabled = target.Enabled; data.Undo = previous.Outfit.Copy(); },
                    true, forward ? "Change restored" : "Change undone", target.Outfit.Equipment, null, false);
                from.RemoveAt(from.Count - 1); to.Add(previous); historyKey = null;
            });
        }
        internal static void Redo() { NavigateHistory(true); }
    }
}
