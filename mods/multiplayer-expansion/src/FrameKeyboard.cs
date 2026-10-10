using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Input;

namespace MultiplayerExpansion
{
    // native controls and UI ask for the same keyboard several times per tick
    internal sealed class FrameKeyboard
    {
        private readonly Func<int,short> read;
        private readonly Func<int,bool> reserved;
        private KeyboardState state;
        private bool sampled;
        internal FrameKeyboard(Func<int,short> reader,Func<int,bool> excluded) { read=reader;reserved=excluded; }
        internal void Begin() { sampled=false; }
        internal KeyboardState Read(bool available)
        {
            if(!available) { Begin();return default(KeyboardState); }
            if(sampled) return state;
            var keys=new List<Keys>();
            for(int key=8;key<255;key++)
                if(!reserved(key) && (read(key)&0x8000)!=0) keys.Add((Keys)key);
            state=new KeyboardState(keys.ToArray());sampled=true;
            return state;
        }
    }
}
