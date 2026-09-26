using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Replays
{
    internal sealed class ReplayRepository
    {
        private readonly string directory;
        private readonly object sync = new object();
        private IList<ReplaySummary> cachedSummaries;
        private int revision;
        private readonly Func<string[]> scan;
        private readonly Action<string, ReplayData> write;
        private readonly JKRuntime.BackgroundWorkQueue writer = new JKRuntime.BackgroundWorkQueue("replay-save", 3);
        private sealed class PendingSave { internal ReplayData Data; internal JKRuntime.BackgroundWork Work; }
        private readonly List<PendingSave> pending = new List<PendingSave>();
        internal JKRuntime.BackgroundWork LastSave { get; private set; }
        internal int Revision { get { return Volatile.Read(ref revision); } }

        internal ReplayRepository(string path, Func<string[]> enumerate = null, Action<string, ReplayData> serialize = null)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Replay directory is required", "path");
            directory = Path.GetFullPath(path);
            Directory.CreateDirectory(directory);
            scan = enumerate ?? delegate { return Directory.GetFiles(directory, "*.jkr"); };
            write = serialize ?? new Action<string, ReplayData>(ReplayCodec.Write);
        }

        internal static ReplayRepository CreateDefault()
        {
            return new ReplayRepository(ReplayPaths.Files);
        }

        internal IList<ReplaySummary> List()
        { int ignored; return List(out ignored); }

        internal IList<ReplaySummary> List(out int generation)
        {
            while (true)
            {
            int observed;
            lock (sync)
            {
                observed = revision;
                if (cachedSummaries != null)
                { generation = observed; return new List<ReplaySummary>(cachedSummaries); }
            }
            List<ReplaySummary> result = new List<ReplaySummary>();
            foreach (string path in scan())
            {
                try
                {
                    result.Add(ReplayCodec.ReadSummary(path));
                }
                catch (Exception error)
                {
                    Console.WriteLine(
                        "[Replays] Ignoring unreadable replay "
                        + Path.GetFileName(path)
                        + ": "
                        + error.Message);
                }
            }
            result.Sort(
                delegate(ReplaySummary left, ReplaySummary right)
                {
                    return right.Header.CreatedUtc.CompareTo(
                        left.Header.CreatedUtc);
                });
            lock (sync)
            {
                if (revision != observed) continue;
                cachedSummaries = result;
                generation = observed;
                return new List<ReplaySummary>(cachedSummaries);
            }
            }
        }

        internal void Refresh() { lock (sync) { revision++; cachedSummaries = null; } }

        internal ReplayData Load(string id)
        {
            ReplaySummary summary = Find(id);
            return summary == null ? null : ReplayCodec.Read(summary.FilePath);
        }

        internal ReplaySummary Find(string id)
        {
            foreach (ReplaySummary summary in List())
            {
                if (string.Equals(
                    summary.Header.Id,
                    id,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return summary;
                }
            }
            return null;
        }

        internal ReplaySummary Save(ReplayData replay)
        {
            string safeWorld = SafeFilePart(replay.Header.WorldName);
            string stamp = replay.Header.CreatedUtc.ToLocalTime()
                .ToString("yyyyMMdd-HHmmss");
            string suffix = replay.Header.Id.Length > 8
                ? replay.Header.Id.Substring(0, 8)
                : replay.Header.Id;
            string fileName = stamp + "-" + safeWorld + "-" + suffix + ".jkr";
            string finalPath = Path.Combine(directory, fileName);
            string temporary = finalPath + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                write(temporary, replay);
                using (var stream = new FileStream(temporary, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) stream.Flush(true);
                ReplaySummary summary = ReplayCodec.ReadSummary(temporary);
                File.Move(temporary, finalPath);
                summary.FilePath = finalPath;
                Refresh();
                VerificationBridge.Published(summary);
                return summary;
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        internal bool QueueSave(ReplayData replay)
        {
            if (replay == null) return false;
            return QueueSnapshot(delegate { return replay; });
        }

        // Manual snapshots use two slots. A recording already in progress owns
        // the third completion slot, so winning cannot discard it under backpressure.
        internal bool CanStartRecording
        {
            get { lock (sync) { PruneSaved(); return pending.Count < 3; } }
        }
        private void PruneSaved() { pending.RemoveAll(value => value.Work != null && value.Work.State == JKRuntime.BackgroundWorkState.Succeeded); }
        internal bool QueueCompleted(ReplayData replay)
        { return replay != null && QueueSnapshot(delegate { return replay; }, 3); }
        internal bool QueueSnapshot(Func<ReplayData> capture) { return QueueSnapshot(capture, 2); }
        private bool QueueSnapshot(Func<ReplayData> capture, int capacity)
        {
            var entry = new PendingSave();
            lock (sync)
            {
                PruneSaved();
                if (pending.Count >= capacity) return false;
                pending.Add(entry);
            }
            bool accepted = false;
            try
            {
                JKRuntime.BackgroundWork work;
                accepted = writer.TryPrepare(delegate
                {
                    entry.Data = capture();
                    if (entry.Data == null) throw new InvalidOperationException("Replay snapshot is unavailable");
                    return delegate { SavePending(entry); };
                }, out work);
                if (accepted) { lock (sync) { entry.Work = work; LastSave = work; } }
                return accepted;
            }
            finally { if (!accepted) lock (sync) pending.Remove(entry); }
        }
        private void SavePending(PendingSave entry)
        {
            Save(entry.Data);
            // Failed writes retain at most three recordings for explicit retry.
            // Successful writes release them immediately, even if no UI opens.
            entry.Data = null;
        }
        internal bool RetryFailed()
        {
            lock (sync)
            {
                foreach (var entry in pending)
                {
                    if (entry.Work == null || entry.Work.State != JKRuntime.BackgroundWorkState.Failed) continue;
                    JKRuntime.BackgroundWork work;
                    if (!writer.TryEnqueue(delegate { SavePending(entry); }, out work)) return false;
                    entry.Work = work; LastSave = work;
                    return true;
                }
            }
            return false;
        }
        internal string SaveStatus
        {
            get
            {
                lock (sync)
                {
                    if (pending.Exists(value => value.Work != null && value.Work.State == JKRuntime.BackgroundWorkState.Failed)) return "SAVE FAILED - REFRESH TO RETRY";
                    if (writer.PendingCount != 0) return "SAVING REPLAY...";
                    return LastSave != null && LastSave.State == JKRuntime.BackgroundWorkState.Succeeded ? "REPLAY SAVED" : "";
                }
            }
        }
        internal bool Drain(int milliseconds) { return writer.Drain(milliseconds); }

        internal bool Delete(string id)
        {
            ReplaySummary summary = Find(id);
            if (summary == null) return false;
            string path = Path.GetFullPath(summary.FilePath);
            string root = directory.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Replay path escaped its repository");
            File.Delete(path);
            Refresh();
            return true;
        }

        private static string SafeFilePart(string value)
        {
            string text = string.IsNullOrWhiteSpace(value) ? "jump-king" : value;
            foreach (char invalid in Path.GetInvalidFileNameChars())
                text = text.Replace(invalid, '-');
            text = text.Trim();
            if (text.Length > 32) text = text.Substring(0, 32).Trim();
            return text.Length == 0 ? "jump-king" : text;
        }
    }
}
