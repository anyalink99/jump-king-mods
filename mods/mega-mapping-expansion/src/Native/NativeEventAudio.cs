using System;
using System.Collections.Generic;
using System.Linq;
using JumpKing.XnaWrappers;

namespace MegaMappingExpansion.Construction
{
    // borrow native voices; the wall still owns its trigger and repeat policy
    internal sealed class NativeEventAudio : IDisposable
    {
        private readonly IJKSound[] sounds;
        private readonly HashSet<IJKSound> held=new HashSet<IJKSound>();
        internal NativeEventAudio(IEnumerable<IJKSound> voices){sounds=voices.Where(s=>s!=null).Distinct().ToArray();}
        internal void Tick(bool paused)
        {
            if(!paused){Release();return;}
            foreach(var sound in sounds)
            {
                if(sound.State==JKSoundState.Playing){sound.Pause();held.Add(sound);}
                else if(sound.State==JKSoundState.Stopped)held.Remove(sound);
            }
        }
        private void Release()
        {
            foreach(var sound in held)if(sound.State==JKSoundState.Paused)sound.Resume();
            held.Clear();
        }
        public void Dispose(){Release();}
    }
}
