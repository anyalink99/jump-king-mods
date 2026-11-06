using System;
using System.IO;
using System.Reflection;
using System.Security;
using System.Text;
using System.Threading;

namespace JKRuntime
{
    // Harmony's optional debug file mustn't abort patch installation or cleanup
    internal sealed class HarmonyLogScope : IDisposable
    {
        internal const int MaximumBytes = 4 * 1024 * 1024;
        internal static string LastError;
        private readonly Assembly engine;
        private readonly PropertyInfo writer;
        private readonly StreamWriter previous;
        private readonly SafeWriter current;

        internal static void Install(Assembly engine)
        {
            lock (engine)
            {
                var fileLog = engine.GetType("HarmonyLib.FileLog");
                var property = fileLog == null ? null : fileLog.GetProperty("LogWriter");
                if (property == null || !property.CanRead || !property.CanWrite || property.PropertyType != typeof(StreamWriter))
                    throw new NotSupportedException("Harmony doesn't expose a StreamWriter override");
                var existing = (StreamWriter)property.GetValue(null, null);
                if (existing is SafeWriter) return;
                property.SetValue(null, new SafeWriter(existing, fileLog.GetProperty("LogPath")), null);
            }
        }

        internal HarmonyLogScope(Assembly engine)
        {
            this.engine = engine;
            Monitor.Enter(engine);
            try
            {
                var fileLog = engine.GetType("HarmonyLib.FileLog");
                writer = fileLog == null ? null : fileLog.GetProperty("LogWriter");
                if (writer == null || !writer.CanRead || !writer.CanWrite || writer.PropertyType != typeof(StreamWriter)) return;
                previous = (StreamWriter)writer.GetValue(null, null);
                if (previous is SafeWriter) return;
                current = new SafeWriter(previous, fileLog.GetProperty("LogPath"));
                writer.SetValue(null, current, null);
            }
            catch { Monitor.Exit(engine); throw; }
        }

        public void Dispose()
        {
            try
            {
                // a callback may have installed its own writer while patching
                if (current != null && ReferenceEquals(writer.GetValue(null, null), current))
                    writer.SetValue(null, previous, null);
            }
            finally { Monitor.Exit(engine); }
        }

        private sealed class SafeWriter : StreamWriter
        {
            private readonly StreamWriter previous;
            private readonly PropertyInfo path;
            private readonly object gate = new object();
            private bool unavailable;
            internal SafeWriter(StreamWriter previous, PropertyInfo path) : base(Stream.Null)
            { this.previous = previous; this.path = path; }

            public override void WriteLine(string value)
            {
                lock (gate) WriteSafe(value);
            }

            private void WriteSafe(string value)
            {
                if (unavailable) return;
                try
                {
                    if (previous != null) { previous.WriteLine(value); return; }
                    string destination = path == null ? null : (string)path.GetValue(null, null);
                    if (destination == null) return;
                    byte[] bytes = Encoding.UTF8.GetBytes(value + Environment.NewLine);
                    using (var stream = new FileStream(destination, FileMode.Append, FileAccess.Write, FileShare.Read))
                    {
                        // retain an old oversized log as evidence; don't keep growing it
                        if (stream.Length + bytes.Length > MaximumBytes)
                        {
                            unavailable = true;
                            LastError = "Harmony debug log reached the 4 MiB limit: " + destination;
                            return;
                        }
                        stream.Write(bytes, 0, bytes.Length);
                    }
                }
                catch (Exception error)
                {
                    var invocation = error as TargetInvocationException;
                    var cause = invocation == null ? error : invocation.InnerException;
                    if (!(cause is IOException || cause is UnauthorizedAccessException || cause is SecurityException)) throw;
                    unavailable = true;
                    LastError = cause.GetType().Name + ": " + cause.Message;
                }
            }
        }
    }
}
