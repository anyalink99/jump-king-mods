using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EntityComponent;
using JKRuntime;
using JKRuntime.Gameplay;
using JumpKing;
using JumpKing.API;
using JumpKing.Level;
using JumpKing.Player;
using JumpKing.MiscSystems;
using JumpKing.XnaWrappers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace WardrobePlus.Advanced
{
    // optional consumers discover this public bridge in the implementation assembly
    public static class PresentationEvents
    {
        private static readonly Dictionary<Type,string> surfaces=new Dictionary<Type,string>();
        /// <summary>register a stable surface name for a foreign block. dispose on module teardown</summary>
        public static IDisposable RegisterSurface(Type block,string name)
        {if(block==null||!typeof(IBlock).IsAssignableFrom(block))throw new ArgumentException("Expected an IBlock type");ManifestIO.Id(name,"surface");if(surfaces.ContainsKey(block))throw new InvalidOperationException("Surface already registered");surfaces.Add(block,name);return new SurfaceLease(block,name);}
        private sealed class SurfaceLease:IDisposable
        {private Type block;private readonly string name;internal SurfaceLease(Type value,string id){block=value;name=id;}public void Dispose(){if(block==null)return;string current;if(surfaces.TryGetValue(block,out current)&&current==name)surfaces.Remove(block);block=null;}}
        internal static string SurfaceName(Type block){string name;return surfaces.TryGetValue(block,out name)?name:block.FullName;}
        /// <summary>Raise a namespaced cosmetic event on the active player, on the game thread</summary>
        public static void Raise(string trigger)
        {if(!ManifestIO.ValidTrigger(trigger)||trigger.IndexOf('/')<0)throw new ArgumentException("Use an owner/event ID");if(PresentationRuntime.Live!=null)JKRuntime.Presentation.PlayerAppearance.Signal(PresentationRuntime.Live.Player,trigger);}
        public static event Action<PresentationEvent> Happened;
        internal static void Publish(PresentationEvent e)
        { PresentationBridge.Record(e); foreach(Action<PresentationEvent> callback in Happened==null?new Delegate[0]:Happened.GetInvocationList())
            try {callback(e.Copy());}catch(Exception error){Console.WriteLine("[Wardrobe+] Presentation observer: "+error.Message);} }
    }
    internal sealed class PresentationRuntime : Component,IDisposable
    {
        internal static PresentationRuntime Live;
        internal PresentationActor Actor;
        internal PlayerEntity Player;
        private PreparedAppearance generation;
        private ScreenShakeController nativeShake,customShake;
        private bool disposed,paused;
        private float previousY,stepTimer;
        private bool nativeShakeEnabled=true;
        private readonly OwnedPatches patches;
        private IDisposable events;
        private readonly RuntimeScope presentationServices=new RuntimeScope();
        private static readonly BindingFlags flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        private static readonly FieldInfo spriteField=typeof(PlayerEntity).GetField("m_sprite",flags),flipField=typeof(PlayerEntity).GetField("m_flip",flags),shakeField=typeof(PlayerEntity).GetField("m_screen_shake",flags);
        [ThreadStatic] private static SoundContext soundContext;
        private sealed class SoundContext
        {internal bool Surface=true,Equipment=true,SurfacePlayed,EquipmentPlayed;internal string Trigger;internal SoundContext Previous;internal PresentationRuntime Owner;internal PresentationEvent Event;}
        internal PresentationRuntime(PlayerEntity player)
        {
            Player=player; patches=new OwnedPatches("WardrobePlus.Presentation");
            try
            {
                nativeShake=(ScreenShakeController)shakeField.GetValue(player); customShake=JumpGame.screenShakeManager.CreateShakeController();
                Install(); Live=this; Sync();
                events=GameplayEvents.Subscribe("wardrobe-plus",Observe); AppearanceEvents.Changed+=Sync;
                presentationServices.Own(JKRuntime.Presentation.PlayerAppearance.RegisterContext("wardrobe-plus",BeginAppearance));
                presentationServices.Own(JKRuntime.Presentation.CosmeticPlayback.Register("wardrobe-plus",ghost=>PresentationBridge.CreateTypedActor(ghost)));
                presentationServices.Own(JKRuntime.Presentation.PlayerAppearance.SubscribeSignals(value=>{
                    if(value.Delivery==JKRuntime.Presentation.PresentationDelivery.Live&&ReferenceEquals(value.Player,Player))Raise(value.Trigger);
                }));
            }
            catch {Dispose();throw;}
        }
        private static MethodInfo Handler(string name) {return typeof(PresentationRuntime).GetMethod(name,flags);}
        private void Install()
        {
            var play=typeof(OneShotSound).GetMethod("PlayOneShot"); var custom=typeof(IJKSound).GetMethod("Play");
            foreach(var type in new[]{typeof(JumpState),typeof(IsOnGround),typeof(FailState)})
            {
                var sound=type.GetMethod("HandleSounds",flags);
                patches.Add(sound,prefix:Handler("BeforeSounds"),finalizer:Handler("AfterSounds"));
                patches.ReplaceCalls(sound,play,Handler("NativeSound"),type==typeof(IsOnGround)?6:4);
                patches.ReplaceCalls(sound,custom,Handler("CustomSound"),1);
                if(type!=typeof(FailState)) patches.Add(type.GetMethod("HandleParticles",flags),prefix:Handler("BeforeParticles"));
            }
            patches.Add(typeof(JumpState).GetMethod("DoJump",flags),prefix:Handler("JumpIntensity"));
            patches.Add(typeof(JumpState).GetMethod("MyRun",flags),postfix:Handler("ChargeUpdated"));
            patches.Add(typeof(PlayerEntity).GetMethod("Draw",flags),prefix:Handler("BeforeDraw"),finalizer:Handler("AfterDraw"));
            patches.ReplaceCalls(typeof(ScreenshakeManager).GetMethod("Update"),typeof(ScreenShakeController).GetMethod("GetCurrent"),Handler("ShakeContribution"),1);
            var water=typeof(Game1).Assembly.GetType("JumpKing.BodyCompBehaviours.WaterParticleSpawningBehaviour",true);
            patches.Add(water.GetMethod("ExecuteBehaviour"),prefix:Handler("BeforeWater"),finalizer:Handler("AfterWater"));
            var spawner=typeof(Game1).Assembly.GetType("JumpKing.Particles.JumpParticleEntity+ParticleSpawner",true);
            patches.Add(spawner.GetMethod("CreateWaterSplashParticle",new[]{typeof(Point),typeof(bool)}),prefix:Handler("WaterSplash"));
        }
        private void Sync()
        {
            var next=Controller.Enabled?Controller.Active:null;
            if(ReferenceEquals(next,generation)) { if(Actor!=null) { Actor.Equipment=NativeAppearance.Worn().ToArray(); Actor.Profiles.Clear(); Actor.Profiles.AddRange(Actor.Assets.Library.OutfitProfiles(next,Actor.Equipment)); } return; }
            bool needed=next!=null && next.Advanced!=null && !next.Advanced.Failed && (next.Advanced.Packages.Count>0 || next.SourceOutfit.Presentation.ShakeScale!=1);
            PresentationActor actor=needed?new PresentationActor(next,NativeAppearance.Worn(),false){WorldParticles=true}:null;
            if(Actor!=null) Actor.Dispose(); Actor=actor;generation=next; nativeShakeEnabled=true;
        }
        private bool Ready(BodyComp body) {return !disposed && !paused && !PresentationBridge.PlayingReplay && Actor!=null && ReferenceEquals(body,Player.m_body);}
        internal static string SurfaceOf(BodyComp body)
        {
            if(body.IsOnBlock(typeof(WaterBlock)))return "water"; if(body.IsOnBlock(typeof(IceBlock)))return "ice";
            if(body.IsOnBlock(typeof(SnowBlock)))return "snow";if(body.IsOnBlock(typeof(SandBlock)))return "sand";
            var block=body.OnBlocks().FirstOrDefault(); return block==null||block.Name=="SolidBlock"||block.Name=="BoxBlock"?"normal":PresentationEvents.SurfaceName(block);
        }
        private void Refresh()
        {
            Actor.Position=JKRuntime.Presentation.AppearanceGeometry.Resolve(Player).Anchor(Player.m_body.Position);Actor.Velocity=Player.m_body.Velocity;
            Actor.Surface=SurfaceOf(Player.m_body);Actor.Flipped=(SpriteEffects)flipField.GetValue(Player)==SpriteEffects.FlipHorizontally;
        }
        private PresentationEvent Event(string trigger)
        { Refresh(); var e=Actor.Event(trigger); if(trigger=="land"||trigger=="splat") e.Speed=Math.Abs(previousY); return e; }
        private static string Trigger(MethodBase method) {return method.DeclaringType==typeof(JumpState)?"jump":method.DeclaringType==typeof(FailState)?"splat":"land";}
        private static void BeforeSounds(PlayerNode __instance,BodyComp body,MethodBase __originalMethod,out SoundContext __state)
        {
            __state=null; var live=Live;if(live==null||!live.Ready(body))return;
            var e=live.Event(Trigger(__originalMethod));var actor=live.Actor;
            __state=new SoundContext{Previous=soundContext,Trigger=e.Trigger,Owner=live,Event=e};soundContext=__state;
            try {
                if(e.Trigger=="jump")actor.SetState("rise");else if(e.Trigger=="splat")actor.SetState("splat");else actor.SetState("land");
            } catch(Exception error) { __state.Surface=__state.Equipment=true;live.Fault(error); }
        }
        private static void AfterSounds(SoundContext __state,Exception __exception)
        {
            if(__state==null)return;soundContext=__state.Previous;
            if(__exception!=null||__state.Owner.Actor==null)return;
            try
            {
                // missing native clips still permit authored feedback, without moving
                // real native playback behind particles, observers or custom voices
                PlaySound(__state,false,null);PlaySound(__state,true,null);
                var actor=__state.Owner.Actor;if(actor==null)return;
                __state.Owner.nativeShakeEnabled=actor.Dispatch(__state.Event,"shake",true).Native;
                if(__state.Trigger=="splat")actor.Dispatch(__state.Event,"particles",false);
                PresentationEvents.Publish(__state.Event);
            }catch(Exception error){__state.Owner.Fault(error);}
        }
        private static void PlaySound(SoundContext context,bool equipment,Action native)
        {
            if(context.Owner.Actor==null){if(native!=null)native();return;}
            if(equipment?context.EquipmentPlayed:context.SurfacePlayed)
            {if(native!=null && (equipment?context.Equipment:context.Surface))native();return;}
            if(equipment)context.EquipmentPlayed=true;else context.SurfacePlayed=true;
            bool played=false;Exception playbackError=null;
            try
            {
                Action playback=native==null?null:(Action)(()=>{played=true;try{native();}catch(Exception error){playbackError=error;throw;}});
                bool keep=context.Owner.Actor.Dispatch(context.Event,equipment?"equipmentSound":"surfaceSound",true,playback).Native;
                if(equipment)context.Equipment=keep;else context.Surface=keep;
            }
            catch(Exception error)
            {if(playbackError!=null)throw;context.Owner.Fault(error);if(!played && native!=null)native();}
        }
        private static void NativeSound(OneShotSound sound)
        {
            var context=soundContext;if(context==null){sound.PlayOneShot();return;}
            var heavy=NativeEffects.SoundWrapper(context.Trigger=="splat"?"heavy-splat":"heavy-land");
            PlaySound(context,ReferenceEquals(sound,heavy),sound.PlayOneShot);
        }
        private static void CustomSound(IJKSound sound) {if(soundContext==null)sound.Play();else PlaySound(soundContext,false,sound.Play);}
        private static bool BeforeParticles(BodyComp body,MethodBase __originalMethod)
        {
            var live=Live;if(live==null||!live.Ready(body))return true;
            try{return live.Actor.Dispatch(live.Event(Trigger(__originalMethod)),"particles",true).Native;}
            catch(Exception error){live.Fault(error);return true;}
        }
        private static void JumpIntensity(JumpState __instance,float p_intensity)
        { if(Live!=null && Live.Ready(__instance.body))Live.Actor.Charge=__instance.body.IsOnBlock(typeof(SnowBlock))&&p_intensity>.2f?Math.Max(.3f,p_intensity):p_intensity; }
        private static readonly FieldInfo chargeTimer=typeof(JumpState).GetField("m_timer",flags);
        private static void ChargeUpdated(JumpState __instance)
        {if(Live!=null&&Live.Ready(__instance.body)){float time=(float)chargeTimer.GetValue(__instance);if(time>0)Live.Actor.Charge=MathHelper.Clamp(time/__instance.CHARGE_TIME,0,1);}}
        private static Point ShakeContribution(ScreenShakeController controller)
        {
            var live=Live;
            if(live!=null && live.Actor!=null)
            {
                if(ReferenceEquals(controller,live.customShake))return live.Actor.ShakeOffset.ToPoint();
                if(ReferenceEquals(controller,live.nativeShake))return live.nativeShakeEnabled?(controller.GetCurrent().ToVector2()*live.Actor.Selection.ShakeScale).ToPoint():Point.Zero;
            }
            return controller.GetCurrent();
        }
        [ThreadStatic] private static BodyComp waterBody;
        private static void BeforeWater(JumpKing.BodyCompBehaviours.BehaviourContext behaviourContext,out BodyComp __state) {__state=waterBody;waterBody=behaviourContext.BodyComp;}
        private static void AfterWater(BodyComp __state) {waterBody=__state;}
        private static bool WaterSplash(Point p_pos,bool p_enter)
        {
            var live=Live;if(live==null||!live.Ready(waterBody))return true;
            try
            {
                var e=live.Event(p_enter?"waterEnter":"waterExit");e.X=p_pos.X;e.Y=p_pos.Y;e.Surface="water";
                var sound=live.Actor.Dispatch(e,"surfaceSound",true);var particles=live.Actor.Dispatch(e,"particles",true);
                live.Actor.Dispatch(e,"equipmentSound",true);live.Actor.Dispatch(e,"shake",true);PresentationEvents.Publish(e);
                if(sound.Native && particles.Native)return true;
                if(sound.Native){var s=NativeEffects.SoundWrapper(p_enter?"water-enter":"water-exit") as IJKSound;if(s!=null)s.Play();}
                if(particles.Native)
                {
                    typeof(Game1).Assembly.GetType("JumpKing.Particles.JumpParticleEntity+ParticleSpawner",true)
                        .GetMethod("CreateWaterSplashParticle",new[]{typeof(int),typeof(int)}).Invoke(null,new object[]{p_pos.X,p_pos.Y});
                }
                return false;
            }catch(Exception error){live.Fault(error);return true;}
        }
        private static void BeforeDraw(PlayerEntity __instance,out PresentationActor __state)
        {
            __state=PresentationDraw.Current;var live=Live;if(PresentationBridge.PlayingReplay||live==null||live.Actor==null||!ReferenceEquals(live.Player,__instance))return;
            PresentationDraw.Current=live.Actor;
            try{PresentationDraw.Particles(live.Actor,"back",Vector2.Zero,1,true,Color.White);}catch(Exception error){PresentationDraw.Current=__state;live.Fault(error);}
        }
        private static void AfterDraw(PlayerEntity __instance,PresentationActor __state)
        {
            try{var live=Live;if(!PresentationBridge.PlayingReplay&&live!=null&&live.Actor!=null&&ReferenceEquals(live.Player,__instance))
                try{PresentationDraw.Particles(live.Actor,"front",Vector2.Zero,1,true,Color.White);}catch(Exception error){live.Fault(error);}}
            finally{PresentationDraw.Current=__state;}
        }
        protected override void LateUpdate(float delta)
        {
            if(disposed||paused||Actor==null||PresentationBridge.PlayingReplay)return;
            try
            {
                Refresh();string state=StateFor(JKRuntime.Presentation.PlayerAppearance.BaseSprite(Player));
                if(Actor.State=="land"&&Actor.StateTime<Hold("land",.08) && state=="idle")state="land";
                if(state=="fall"&&Actor.State=="rise")
                {Raise("apex");if(Hold("apex",0)>delta)Actor.SetState("apex");}
                // only authored apex clips delay descent. Leave before Update reaches
                // the clip's end so a looping clip can't flash its first pose again
                if(state=="fall"&&Actor.State=="apex"&&Actor.StateTime+delta<Hold("apex",0))state="apex";
                if(state=="recover"&&Actor.State!="recover")Raise("recover");
                Actor.SetState(state);Actor.Update(delta);
                if(new[]{"idle","walk","charge","rise","fall"}.Contains(state))Actor.Trigger(state);
                if(state=="walk") {stepTimer+=delta*Math.Max(1,Math.Abs(Actor.Velocity.X));if(stepTimer>=.3f){stepTimer%=.3f;Raise("step");}}else stepTimer=0;
                previousY=Actor.Velocity.Y;
            }catch(Exception error){Fault(error);}
        }
        private double Hold(string state,double fallback)
        {AnimationBinding binding;if(!Actor.Assets.Animations.TryGetValue(NativeAppearance.BaseItem,out binding))return fallback;var clip=binding.Definition.clips.Find(c=>c.state==state);return clip==null?fallback:Math.Min(.5,clip.frames.Sum(f=>(double)f.duration));}
        internal void Raise(string trigger)
        {if(!Ready(Player.m_body))return;var value=Event(trigger);foreach(var channel in ManifestIO.Channels)Actor.Dispatch(value,channel,false);PresentationEvents.Publish(value);}
        internal static string StateFor(Sprite sprite)
        {
            var sprites=Game1.instance.contentManager.playerSprites;
            if(sprite==sprites.jump_charge)return "charge";if(sprite==sprites.jump_up)return "rise";if(sprite==sprites.jump_fall||sprite==sprites.jump_bounce)return "fall";
            if(sprite==sprites.splat)return "splat";if(sprite==sprites.look_up)return "lookUp";
            if(sprite==sprites.walk_one||sprite==sprites.walk_two||sprite==sprites.walk_smear)return "walk";
            if(sprite==sprites.stretch_one||sprite==sprites.stretch_two||sprite==sprites.stretch_smear)return "recover";return "idle";
        }
        private void Observe(GameplayEvent e)
        {
            if(Actor==null)return;
            if(e.Kind==GameplayEventKind.PauseChanged){paused=e.Paused;Actor.Pause(paused);return;}
            if(e.Kind==GameplayEventKind.RestoreStarted||e.Kind==GameplayEventKind.Teleported){Actor.Reset();nativeShakeEnabled=true;return;}
            if(e.Kind==GameplayEventKind.ChargeStarted||e.Kind==GameplayEventKind.ChargeEnded)
            {Refresh();var value=Actor.Event(e.Kind==GameplayEventKind.ChargeStarted?"chargeStart":"chargeEnd");foreach(var channel in ManifestIO.Channels)Actor.Dispatch(value,channel,false);PresentationEvents.Publish(value);}
        }
        private void Fault(Exception error)
        {Controller.LastError="Presentation disabled: "+error.GetBaseException().Message;Console.WriteLine("[Wardrobe+] "+error);if(Actor!=null)Actor.Dispose();Actor=null;nativeShakeEnabled=true;}
        public void Dispose()
        {
            if(disposed)return;disposed=true;Enabled=false;AppearanceEvents.Changed-=Sync;if(events!=null)events.Dispose();
            presentationServices.Dispose();
            if(Actor!=null){Actor.Dispose();Actor=null;}if(customShake!=null)customShake.Dispose();patches.Dispose();if(ReferenceEquals(Live,this))Live=null;
        }
        private IDisposable BeginAppearance(JKRuntime.Presentation.AppearanceFrame frame)
        {
            if(Actor==null||Actor.Disposed||(frame.Player!=null&&!ReferenceEquals(frame.Player,Player)))return null;
            // Replay owns an independent context. A live sample must never replace it.
            if(PresentationBridge.PlayingReplay&&!frame.IsCapture)return null;
            var previous=PresentationDraw.Current;
            PresentationDraw.Current=Actor.Sample(frame.Pose,frame.WorldAnchor,(frame.Facing&SpriteEffects.FlipHorizontally)!=0);
            return new ContextLease(previous);
        }
        private sealed class ContextLease:IDisposable
        {
            private readonly PresentationActor previous;private bool disposed;
            internal ContextLease(PresentationActor value){previous=value;}
            public void Dispose(){if(disposed)return;disposed=true;PresentationDraw.Current=previous;}
        }
    }
}
