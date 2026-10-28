using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Script.Serialization;
using Microsoft.Xna.Framework;

namespace WardrobePlus.Advanced
{
    /// <summary>optional replay integration using BCL and MonoGame types only. Game-thread API</summary>
    public static class PresentationBridge
    {
        public const int Version=1;
        private static long sequence;
        internal static bool PlayingReplay {get{return JKRuntime.Presentation.CosmeticPlayback.Playing;}}
        public static IDisposable Playback() {return JKRuntime.Presentation.CosmeticPlayback.Playback();}
        public static long Sequence {get{return JKRuntime.Presentation.CosmeticPlayback.Sequence;}}
        internal static void Record(PresentationEvent e)
        {
            var copy=e.Copy();copy.Sequence=++sequence;
            JKRuntime.Presentation.CosmeticPlayback.Record(new JavaScriptSerializer().Serialize(copy));
        }
        public static string[] ReadEvents(long after) {return JKRuntime.Presentation.CosmeticPlayback.ReadEvents(after);}
        internal static JKRuntime.Presentation.ICosmeticActor CreateTypedActor(bool ghost)
        {var actor=CreateActor(ghost);return actor==null?null:new TypedActor(actor);}
        private sealed class TypedActor:JKRuntime.Presentation.ICosmeticActor
        {
            private object actor;internal TypedActor(object value){actor=value;}
            public void Advance(int frame,double stateAge,Vector2 anchor,Vector2 velocity,string state,bool flipped,int[] equipment,string[] events,bool silent)
            {PresentationBridge.Advance(actor,frame,stateAge,anchor.X,anchor.Y,velocity.X,velocity.Y,state,flipped,equipment,events,silent);}
            public void Pause(bool paused){PresentationBridge.Pause(actor,paused);}
            public IDisposable BeginDraw(Color tint){return PresentationBridge.BeginDraw(actor,tint);}
            public void Dispose(){if(actor==null)return;((IDisposable)actor).Dispose();actor=null;}
        }
        public static object CreateActor(bool ghost)
        {
            if(!Controller.Enabled||Controller.Active==null||Controller.Active.Advanced==null||Controller.Active.Advanced.Failed)return null;
            return new PresentationActor(Controller.Active,NativeAppearance.Worn(),ghost){AllowShake=false,Source=Controller.Active,WorldParticles=true};
        }
        public static void Advance(object handle,int frame,double stateAge,float x,float y,float vx,float vy,string state,bool flipped,int[] equipment,string[] events,bool silent)
        {
            var actor=handle as PresentationActor;if(actor==null||actor.Disposed)return;
            actor.Silent=silent;actor.AllowShake=false;actor.Position=new Vector2(x,y);actor.Velocity=new Vector2(vx,vy);actor.Flipped=flipped;
            equipment=equipment??new int[0];
            if(!actor.Equipment.SequenceEqual(equipment) && actor.Source!=null){actor.Profiles.Clear();actor.Profiles.AddRange(actor.Assets.Library.OutfitProfiles(actor.Source,equipment));}
            actor.Equipment=equipment;actor.Time=frame/60d;actor.SetState(state);actor.StateTime=Math.Max(0,stateAge-1d/60);
            foreach(var json in events??new string[0])
            {
                if(json.Length>4096)continue;
                try {
                var e=new JavaScriptSerializer{MaxJsonLength=4096,RecursionLimit=8}.Deserialize<PresentationEvent>(json);
                if(e==null||!ManifestIO.ValidTrigger(e.Trigger)||e.Surface==null||e.Equipment==null||e.Equipment.Length>32)continue;
                ManifestIO.Range(e.Charge,0,1,"Replay charge");ManifestIO.Range(e.Speed,0,10000,"Replay speed");
                ManifestIO.Range(e.X,-10000000,10000000,"Replay X");ManifestIO.Range(e.Y,-10000000,10000000,"Replay Y");
                actor.Surface=e.Surface;actor.Charge=e.Charge;
                foreach(var channel in ManifestIO.Channels)actor.Dispatch(e,channel,false);
                } catch(System.ArgumentException){}catch(System.IO.InvalidDataException){}
            }
            actor.Update(1f/60);actor.Time=frame/60d;actor.StateTime=Math.Max(0,stateAge);
            if(new[]{"idle","walk","charge","rise","fall"}.Contains(state))actor.Trigger(state);
        }
        public static void Pause(object handle,bool paused){var actor=handle as PresentationActor;if(actor!=null)actor.Pause(paused);}
        public static IDisposable BeginDraw(object handle,Color tint)
        {var actor=handle as PresentationActor;return actor==null||actor.Disposed?null:new DrawScope(actor,tint);}
        private sealed class DrawScope:IDisposable
        {
            private readonly PresentationActor previous,actor;private readonly Color tint;private bool disposed;
            internal DrawScope(PresentationActor value,Color color)
            {previous=PresentationDraw.Current;actor=value;tint=color;PresentationDraw.Current=actor;
                try{PresentationDraw.Particles(actor,"back",Vector2.Zero,1,true,tint);}catch{PresentationDraw.Current=previous;throw;}}
            public void Dispose(){if(disposed)return;disposed=true;try{PresentationDraw.Particles(actor,"front",Vector2.Zero,1,true,tint);}finally{PresentationDraw.Current=previous;}}
        }
    }
}
