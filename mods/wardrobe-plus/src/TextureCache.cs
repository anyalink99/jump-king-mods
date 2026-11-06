using System;
using System.Collections.Generic;
using System.IO;
using JumpKing;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace WardrobePlus
{
    internal sealed class TextureLease : IDisposable
    {
        internal Texture2D Texture;
        private Action release;
        internal TextureLease(Texture2D texture, Action action) { Texture = texture; release = action; }
        public void Dispose() { if (release == null) return; var action = release; release = null; action(); }
    }
    internal static class TextureCache
    {
        private sealed class Entry { internal ContentManager Content; internal Texture2D Texture; internal int References; }
        private static readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
        private static long generation;
        internal static void Invalidate() { generation++; }
        internal static TextureLease Acquire(string asset)
        {
            var info = new FileInfo(asset + ".xnb");
            if (!info.Exists) throw new FileNotFoundException("Appearance texture missing", info.FullName);
            string key = asset + "|" + info.Length + "|" + info.LastWriteTimeUtc.Ticks + "|" + generation;
            Entry entry;
            if (!entries.TryGetValue(key, out entry))
            {
                entry = new Entry { Content = new ContentManager(Game1.instance.Services) };
                try { entry.Texture = entry.Content.Load<Texture2D>(asset); entries.Add(key, entry); }
                catch { entry.Content.Dispose(); throw; }
            }
            entry.References++;
            return new TextureLease(entry.Texture, delegate { if (--entry.References == 0) { entries.Remove(key); entry.Content.Dispose(); } });
        }
    }
}
