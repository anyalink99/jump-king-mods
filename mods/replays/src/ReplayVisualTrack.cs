using System;
using JKRuntime.Presentation;

namespace Replays
{
    internal sealed class ReplayVisualStateEvent
    {
        internal int Frame;
        internal byte[] State;
    }
    internal sealed class ReplayVisualRecording
    {
        private byte[] previous=new byte[0];
        private long bytes;
        private bool failed;
        internal void Reset(){previous=new byte[0];bytes=0;failed=false;}
        internal void Capture(ReplayData replay,int frame,JumpKing.Player.PlayerEntity player)
        {
            if(failed)return;
            byte[] state=ActorVisuals.Capture(player);
            if(Same(previous,state))return;
            bytes+=state.Length+8;
            if(bytes>ReplayCodec.MaximumVisualBytes)
            {
                failed=true;replay.VisualEvents.Clear();
                JKRuntime.RuntimeJournal.Record("replays","actor-visual.capture","Visual track exceeded 64 MiB; discarded for this attempt");return;
            }
            replay.VisualEvents.Add(new ReplayVisualStateEvent {Frame=frame,State=state});previous=state;
        }
        internal static bool Same(byte[] a,byte[] b)
        {if(a.Length!=b.Length)return false;for(int i=0;i<a.Length;i++)if(a[i]!=b[i])return false;return true;}
    }
    internal static class ReplayVisualTrack
    {
        private static readonly byte[] Empty=new byte[0];
        internal static byte[] AtFrame(ReplayData replay,int frame)
        {
            var events=replay.VisualEvents;int low=0,high=events.Count-1,found=-1;
            while(low<=high){int middle=low+(high-low)/2;if(events[middle].Frame<=frame){found=middle;low=middle+1;}else high=middle-1;}
            return found<0?Empty:events[found].State;
        }
    }
}
