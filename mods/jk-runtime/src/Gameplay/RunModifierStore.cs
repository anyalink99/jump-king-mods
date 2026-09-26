using System;
using System.Threading;
using JKRuntime.Settings;

namespace JKRuntime.Gameplay
{
    // One immutable latest snapshot and one writer. Durable XML writes never
    // block a registration callback or the first playable update.
    internal sealed class RunModifierStore
    {
        private readonly object gate = new object();
        private readonly string path;
        private readonly Action<RunModifierRecord> write;
        private readonly Action<Action> queue;
        private readonly ManualResetEvent idle = new ManualResetEvent(true);
        private RunModifierRecord latest, pending;
        private bool writing;

        internal RunModifierStore(string value) : this(value,
            record => AtomicXmlFile.Save(value, record),
            work => { if (!ThreadPool.QueueUserWorkItem(delegate { work(); })) throw new InvalidOperationException("Attribution writer unavailable"); })
        { AppDomain.CurrentDomain.ProcessExit += delegate { Wait(2000); }; }

        internal RunModifierStore(string value, Action<RunModifierRecord> save, Action<Action> enqueue)
        { path = value; write = save; queue = enqueue; }
        internal string Path { get { return path; } }
        internal RunModifierRecord Read()
        {
            lock (gate) { if (latest != null) return Copy(latest); }
            return AtomicXmlFile.Load<RunModifierRecord>(path);
        }
        internal void Submit(RunModifierRecord value)
        {
            var copy = Copy(value);
            lock (gate)
            {
                // A save-worker reset can race a final game-thread ledger write.
                // Preserve its metadata while that same attempt remains current.
                if (latest != null && latest.RunKey == copy.RunKey && copy.ResetNativePeak == 0)
                {
                    var reset = ModifierResetEvidence.FromSaved(latest);
                    if (reset != null) reset.StoreIn(copy);
                }
                latest = pending = copy;
                if (writing) return;
                writing = true; idle.Reset();
            }
            Schedule();
        }
        internal void SubmitReset(ModifierResetEvidence reset)
        {
            lock (gate)
            {
                if (latest == null || latest.RunKey != reset.PreviousKey) return;
                var copy = Copy(latest);
                reset.StoreIn(copy);
                latest = pending = copy;
                if (writing) return;
                writing = true; idle.Reset();
            }
            Schedule();
        }
        private void Schedule()
        {
            try { queue(Drain); }
            catch
            {
                lock (gate) { writing = false; idle.Set(); }
                throw;
            }
        }
        private void Drain()
        {
            while (true)
            {
                RunModifierRecord record;
                lock (gate)
                {
                    record = pending; pending = null;
                    if (record == null) { writing = false; idle.Set(); return; }
                }
                try { write(record); }
                catch (Exception error)
                { Console.WriteLine("[JK Runtime] Attribution history write failed; current-session evidence retained: " + error.Message); }
            }
        }
        internal bool Wait(int milliseconds) { return idle.WaitOne(milliseconds); }
        private static RunModifierRecord Copy(RunModifierRecord value)
        {
            var copy = new RunModifierRecord { Schema = value.Schema, RunKey = value.RunKey,
                NativePeak = value.NativePeak, Unknown = value.Unknown, InheritedNativePeak = value.InheritedNativePeak,
                ResetNativePeak = value.ResetNativePeak };
            if (value.UnknownReasons != null) copy.UnknownReasons.AddRange(value.UnknownReasons);
            if (value.InheritedSources != null)
                foreach (var source in value.InheritedSources)
                    copy.InheritedSources.Add(new RunModifierSource { Id = source.Id, Name = source.Name });
            if (value.ResetSources != null)
                foreach (var source in value.ResetSources)
                    copy.ResetSources.Add(new RunModifierSource { Id = source.Id, Name = source.Name });
            foreach (var source in value.Sources)
                copy.Sources.Add(new RunModifierSource { Id = source.Id, Name = source.Name });
            return copy;
        }
    }
}
