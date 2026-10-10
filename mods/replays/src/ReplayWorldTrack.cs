using System;
using System.IO;
using System.Linq;
using JKRuntime;
using JKRuntime.World;

namespace Replays
{
    internal sealed class ReplayWorldRecording
    {
        private byte[] previous;
        private long bytes;
        private bool disabled;
        internal void Reset() {previous=null;bytes=0;disabled=false;}
        internal void Capture(ReplayData replay,int frame)
        {
            if(disabled || frame%3!=0)return;
            try {
                byte[] state=WorldRegistry.Shared.Capture();
                if(previous!=null && state.SequenceEqual(previous))return;
                if(bytes+state.Length>ReplayCodec.MaximumWorldBytes)throw new InvalidDataException("World track exceeds 64 MiB");
                replay.WorldEvents.Add(new ReplayWorldStateEvent{Frame=frame,State=state});previous=state;bytes+=state.Length;
            }catch(Exception error){
                // keep pose recording usable, never leave a silently truncated world timeline
                disabled=true;previous=null;replay.WorldEvents.Clear();
                RuntimeJournal.Record("replays.world","capture",error.GetBaseException().Message);
            }
        }
    }

    internal sealed class ReplayWorldPlayback : IDisposable
    {
        private readonly ReplayData replay;
        private WorldControl control;
        private int applied=-1;
        internal ReplayWorldPlayback(ReplayData value) {replay=value;}
        internal void Begin()
        {
            if(replay.WorldEvents.Count==0)return;
            // reject missing mods and schema changes before touching the current world
            WorldRegistry.Shared.Prepare(replay.WorldEvents[0].State);
            control=WorldControl.Begin("replays",replay.Header.WorldKey,1,WorldRole.Playback);
            try{Present(0);}catch{Dispose();throw;}
        }
        internal void Present(int frame)
        {
            if(control==null)return;
            int index=AtFrame(replay,frame);
            control.SetTime(ReplayTiming.InitialSeconds(replay.Header)+frame*replay.Header.TickDuration/(double)TimeSpan.TicksPerSecond);
            if(index==applied)return;
            control.Receive(replay.WorldEvents[index].State);applied=index;
        }
        internal static int AtFrame(ReplayData replay,int frame)
        {
            int low=0,high=replay.WorldEvents.Count-1;
            while(low<high){int middle=(low+high+1)/2;if(replay.WorldEvents[middle].Frame<=frame)low=middle;else high=middle-1;}
            return low;
        }
        public void Dispose() {if(control!=null){control.Dispose();control=null;}applied=-1;}
    }
}
