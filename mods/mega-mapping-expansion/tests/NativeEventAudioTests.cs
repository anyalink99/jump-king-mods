using System;
using JumpKing.XnaWrappers;
using MegaMappingExpansion.Construction;

namespace MegaMappingExpansion
{
    internal static partial class Tests
    {
        private sealed class BorrowedVoice : IJKSound
        {
            public bool IsLooped{get;set;}
            public float Volume{get;set;}
            public TimeSpan Duration{get{return TimeSpan.FromSeconds(2);}}
            public JKSoundState State{get;private set;}
            internal int Plays,Pauses,Resumes;
            internal BorrowedVoice(){State=JKSoundState.Stopped;}
            public void Play(){Plays++;State=JKSoundState.Playing;}
            public void Pause(){Pauses++;State=JKSoundState.Paused;}
            public void Resume(){Resumes++;State=JKSoundState.Playing;}
            public void Stop(){State=JKSoundState.Stopped;}
        }
        private static void NativeEventAudioChecks()
        {
            var voice=new BorrowedVoice();var foreign=new BorrowedVoice();var silent=new BorrowedVoice();
            voice.Play();foreign.Play();foreign.Pause();
            using(var pause=new NativeEventAudio(new IJKSound[]{voice,voice,foreign,silent,null}))
            {
                pause.Tick(true);pause.Tick(true);
                Require(voice.Pauses==1&&voice.State==JKSoundState.Paused,"A native wall voice pauses once, even if several walls share it");
                pause.Tick(false);pause.Tick(false);
                Require(voice.Resumes==1&&voice.Plays==1,"Resume continues the native clip without firing its trigger again");
                Require(foreign.Resumes==0&&silent.Plays==0,"MME doesn't resume another owner's pause or start an idle voice");
                pause.Tick(true);voice.Stop();pause.Tick(false);
                Require(voice.State==JKSoundState.Stopped&&voice.Resumes==1,"A stopped clip stays stopped when the menu closes");
                voice.Play();pause.Tick(true);
            }
            Require(voice.State==JKSoundState.Playing&&voice.Resumes==2,"Releasing the scene releases only its borrowed pause");
            Require(foreign.State==JKSoundState.Paused,"Teardown leaves an external pause alone");
        }
    }
}
