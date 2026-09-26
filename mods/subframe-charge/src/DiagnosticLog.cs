using JKRuntime.Input;
using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Text;

namespace SubframeCharge
{
    internal static class DiagnosticLog
    {
        internal const string FileName = "SubframeCharge.log";
        internal const int MaximumBytes = 16 * 1024 * 1024;
        private static readonly object Sync = new object();
        private static readonly object FileSync = new object();
        private static readonly Queue<string> Pending = new Queue<string>();
        private static readonly string Session = Guid.NewGuid().ToString("N");
        private static Timer writer;
        private static int dropped;

        internal static void Write(string message)
        {
            try
            {
                if (message != null && message.Length > 8192) message = message.Substring(0, 8192) + " [truncated]";
                string line = DateTime.Now.ToString("O", CultureInfo.InvariantCulture)
                    + " " + message + Environment.NewLine;
                lock (Sync)
                {
                    if (writer == null)
                    {
                        Pending.Enqueue(Header(false));
                        writer = new Timer(delegate
                        {
                            // A slow disk must not accumulate blocked timer callbacks.
                            if (!Monitor.TryEnter(FileSync)) return;
                            try { Flush(); }
                            finally { Monitor.Exit(FileSync); }
                        }, null, 100, 100);
                        AppDomain.CurrentDomain.ProcessExit += delegate { Flush(); };
                    }
                    if (Pending.Count < 4096) Pending.Enqueue(line);
                    else dropped++;
                }
            }
            catch
            {
            }
        }

        // Disk I/O never runs on the game/sampler thread during normal play.
        internal static void Flush()
        {
            try
            {
                lock (FileSync)
                {
                    string[] lines;
                    int lost;
                    lock (Sync)
                    {
                        if (Pending.Count == 0) return;
                        lines = Pending.ToArray();
                        Pending.Clear();
                        lost = dropped;
                        dropped = 0;
                    }
                    // Contract tests load the release DLL but keep diagnostics
                    // in _INTERNAL, never beside the Workshop payload.
                    string directory = AppDomain.CurrentDomain.GetData("SubframeCharge.LogDirectory") as string
                        ?? JKRuntime.PackageHost.GetDataDirectory(Assembly.GetExecutingAssembly());
                    if (string.IsNullOrEmpty(directory)) return;
                    string path = Path.Combine(directory, FileName);
                    Rotate(path);
                    FileStream stream = null;
                    try
                    {
                        UTF8Encoding utf8 = new UTF8Encoding(false);
                        stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
                        for (int i = 0; i < lines.Length + (lost == 0 ? 0 : 1); i++)
                        {
                            byte[] bytes = utf8.GetBytes(i < lines.Length ? lines[i]
                                : "diagnostic queue overflow dropped=" + lost + Environment.NewLine);
                            if (stream.Length + bytes.Length > MaximumBytes)
                            {
                                stream.Dispose(); stream = null;
                                Rotate(path, true);
                                stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
                            }
                            stream.Write(bytes, 0, bytes.Length);
                        }
                    }
                    finally { if (stream != null) stream.Dispose(); }
                }
            }
            catch { }
        }

        // Three bounded 16 MiB generations, writer only.
        internal static void Rotate(string path, bool force = false)
        {
            if (!File.Exists(path) || (!force && new FileInfo(path).Length < MaximumBytes)) return;
            string oldest = path + ".2";
            string previous = path + ".1";
            if (File.Exists(oldest)) File.Delete(oldest);
            if (File.Exists(previous)) File.Move(previous, oldest);
            File.Move(path, previous);
            File.WriteAllText(path, Header(true));
        }

        private static string Header(bool continuation)
        {
            return DateTime.Now.ToString("O", CultureInfo.InvariantCulture)
                + (continuation ? " session continuation id=" : " session start id=") + Session
                + " mod=" + Assembly.GetExecutingAssembly().GetName().Version
                + " buildId=" + Assembly.GetExecutingAssembly().ManifestModule.ModuleVersionId
                + " timingModel=native-eligibility-pause-v2 holdStepMs=17"
                + " game=" + typeof(JumpKing.Game1).Assembly.GetName().Version
                + " runtime=" + Environment.Version + " processBits=" + (IntPtr.Size * 8)
                + " inputDiagnostics=1"
                + " evidenceTrace=sfc-evidence-v1 qpcFrequency=" + System.Diagnostics.Stopwatch.Frequency
                + Environment.NewLine;
        }
    }
}
