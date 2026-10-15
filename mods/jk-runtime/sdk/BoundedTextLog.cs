using System;
using System.IO;
using System.Text;

namespace JumpKingModTools
{
    // compiled into consumers, no new Runtime API or installed dependency
    internal static class BoundedTextLog
    {
        internal const int MaximumBytes = 4 * 1024 * 1024;
        private static readonly object Gate = new object();
        private static readonly Encoding Utf8 = new UTF8Encoding(false);

        internal static void Append(string path, string message, bool durable = false)
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
                string text = message ?? "";
                if (text.Length > 16384) text = text.Substring(0, 16384) + " [truncated]";
                byte[] bytes = Utf8.GetBytes(text + Environment.NewLine);
                // old versions may have left oversized logs, including rotated ones
                for (int i = 0; i < 3; i++) Trim(path + (i == 0 ? "" : "." + i));
                if (File.Exists(path) && new FileInfo(path).Length + bytes.Length > MaximumBytes)
                {
                    if (File.Exists(path + ".2")) File.Delete(path + ".2");
                    if (File.Exists(path + ".1")) File.Move(path + ".1", path + ".2");
                    File.Move(path, path + ".1");
                }
                using (var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    if (durable) stream.Flush(true);
                }
            }
        }

        private static void Trim(string path)
        {
            if (!File.Exists(path)) return;
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Linked log files aren't supported.");
            if (new FileInfo(path).Length <= MaximumBytes) return;
            byte[] tail = new byte[MaximumBytes];
            int count = 0;
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                stream.Seek(-MaximumBytes, SeekOrigin.End);
                int read;
                while (count < tail.Length && (read = stream.Read(tail, count, tail.Length - count)) > 0) count += read;
            }
            int start = 0;
            // don't leave a partial UTF-8 character at the start of the retained tail
            while (start < count && (tail[start] & 0xc0) == 0x80) start++;
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
                stream.Write(tail, start, count - start);
        }
    }
}
