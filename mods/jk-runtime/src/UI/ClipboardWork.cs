using System;
using System.Threading;
using System.Windows.Forms;

namespace JKRuntime.UI
{
    // One outstanding STA call for the whole process. A blocked clipboard cannot
    // freeze a frame or spawn unlimited replacement threads. It never owns a page.
    internal sealed class ClipboardWork
    {
        private static int busy;
        internal static Func<string> Read = Clipboard.GetText;
        internal static Action<string> Write = Clipboard.SetText;
        private int done;
        internal bool Done { get { return Volatile.Read(ref done) != 0; } }
        internal string Text, Error;
        internal static ClipboardWork Start(bool copy, string text)
        {
            if (Interlocked.CompareExchange(ref busy, 1, 0) != 0) return null;
            var work = new ClipboardWork();
            var read = Read; var write = Write;
            var thread = new Thread(delegate() {
                try { if (copy) write(text); else work.Text = read(); }
                catch (Exception error) { work.Error = error.Message; }
                finally { Interlocked.Exchange(ref busy, 0); Volatile.Write(ref work.done, 1); }
            });
            try { thread.IsBackground = true; thread.Name = "JK Runtime clipboard"; thread.SetApartmentState(ApartmentState.STA); thread.Start(); }
            catch { Interlocked.Exchange(ref busy, 0); throw; }
            return work;
        }
    }
}
