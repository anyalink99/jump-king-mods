using System;

namespace MultiplayerExpansion
{
    internal sealed class FrameMap
    {
        private readonly Func<ulong> read;
        private bool valid;
        private ulong value;
        internal FrameMap(Func<ulong> read) { this.read=read; }
        internal ulong Value
        {
            get { if(!valid){value=read();valid=true;}return value; }
        }
        internal void Invalidate() { valid=false; }
    }
}
