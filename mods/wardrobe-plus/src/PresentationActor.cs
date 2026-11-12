using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using JumpKing;
using JumpKing.PlayerPreferences;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;

namespace WardrobePlus.Advanced
{
    internal sealed class PresentationActor : IDisposable
    {
        internal sealed class Particle : JKRuntime.Particles.ParticleState
        {
            internal EffectBinding Effect;
            internal float FlowDirection;
            internal bool Grounded;
            internal Color StartColor,EndColor;
            internal Texture2D Texture;
            internal Sprite[] NativeSprites;
            public override void Reset(){base.Reset();Effect=null;Texture=null;NativeSprites=null;Grounded=false;FlowDirection=0;StartColor=EndColor=default(Color);}
        }
        private sealed class Emitter { internal EffectBinding Effect; internal PresentationEvent Event; internal float Age,Fraction; }
        private sealed class Voice { internal EffectBinding Effect; internal SoundEffectInstance Instance; }
        private sealed class Shake { internal EffectBinding Effect; internal float Age; }
        internal readonly PresentationAssets Assets;
        internal readonly LiquidMotion Liquid;
        internal PreparedAppearance Source;
        private IDisposable appearanceLease;
        internal readonly PresentationSelection Selection;
        internal readonly List<ProfileBinding> Profiles;
        internal readonly JKRuntime.Particles.ParticleSystem<Particle> Particles;
        internal readonly JKRuntime.Particles.ParticleBatch ParticleBatch=new JKRuntime.Particles.ParticleBatch(2048);
        private readonly SpilledWater.CollisionFrame waterCollision=new SpilledWater.CollisionFrame();
        private Func<Vector2,bool> updateCollision;
        private readonly List<Emitter> emitters=new List<Emitter>();
        private readonly List<Voice> voices=new List<Voice>();
        private readonly Dictionary<string,EffectBinding> loops=new Dictionary<string,EffectBinding>();
        private PresentationEvent stoppedFor;
        private readonly List<Shake> shakes=new List<Shake>();
        private readonly Dictionary<string,double> cooldowns=new Dictionary<string,double>();
        private readonly Dictionary<string,float> joints=new Dictionary<string,float>(),jointVelocities=new Dictionary<string,float>();
        private readonly Dictionary<int,double> markerTimes=new Dictionary<int,double>();
        internal readonly Queue<string> Trace=new Queue<string>();
        private Random random=new Random(1);
        internal Vector2 Position,Velocity,ShakeOffset;
        internal bool WorldParticles;
        internal Func<Vector2,bool> ParticleCollision;
        internal bool Flipped,Silent=true,AllowShake,Disposed;
        internal double Time,StateTime;
        internal string State="idle",Surface="normal";
        internal float Charge;
        internal float JumpCharge;
        private double jumpTime=-1,landingTime=-1;
        internal float JumpAge {get{return jumpTime<0?-1:(float)Math.Max(0,Time-jumpTime);}}
        internal float LandingAge {get{return landingTime<0?-1:(float)Math.Max(0,Time-landingTime);}}
        internal int[] Equipment=new int[0];
        internal long Sequence;
        internal string PreviousState="idle";
        internal double PreviousStateTime;
        private double transitionTime;
        internal PresentationActor(PreparedAppearance prepared,IEnumerable<int> worn,bool silent)
        {
            Particles=new JKRuntime.Particles.ParticleSystem<Particle>(2048,StepParticle);
            prepared.Advanced.Prepare();
            Assets=prepared.Advanced.Retain(); Selection=prepared.SourceOutfit.Presentation.Copy();
            if(Assets.Shaders.Values.Any(s=>s.Parameters["LiquidLevel"]!=null))Liquid=new LiquidMotion();
            Equipment=worn.ToArray(); Profiles=Assets.Library.OutfitProfiles(prepared,Equipment); Silent=silent; AllowShake=!silent;
            Source=prepared;appearanceLease=prepared.Retain();
        }
        // Drawing samples state without dispatching events or moving the live actor's clock.
        internal PresentationActor Sample(string pose,Vector2 position,bool flipped)
        {
            var sample=(PresentationActor)MemberwiseClone();sample.Position=position;sample.Flipped=flipped;
            if(pose!=null&&pose!=State){sample.State=sample.PreviousState=pose;sample.StateTime=sample.PreviousStateTime=0;sample.transitionTime=Time-1;}
            return sample;
        }
        internal ChannelDecision Dispatch(PresentationEvent e,string channel,bool native,Action nativePlayback=null)
        {
            if(Disposed) return new ChannelDecision();
            if(!ReferenceEquals(e,stoppedFor))
            {
                Stop(e.Trigger);stoppedFor=e;
                if(e.Trigger=="jump"){jumpTime=Time;JumpCharge=e.Charge;}
                if(e.Trigger=="land"||e.Trigger=="splat")landingTime=Time;
                if(Liquid!=null)Liquid.Event(e);
            }
            var decision=Assets.Library.Decide(channel,e,Profiles,Selection);
            if(decision.Native && nativePlayback!=null)nativePlayback();
            foreach(var effect in decision.Effects) Emit(effect,e);
            foreach(var rule in decision.Rules) { Trace.Enqueue(e.Trigger+"/"+channel+" "+rule); while(Trace.Count>32) Trace.Dequeue(); }
            return decision;
        }
        internal PresentationEvent Event(string trigger)
        { return new PresentationEvent { Trigger=trigger,Surface=Surface,Charge=Charge,Speed=Velocity.Length(),X=Position.X,Y=Position.Y,
            VelocityX=Velocity.X,VelocityY=Velocity.Y,Flipped=Flipped,Equipment=Equipment.Select(i=>((JumpKing.MiscEntities.WorldItems.Items)i).ToString()).ToArray(),Time=Time,Sequence=++Sequence }; }
        internal void Trigger(string trigger)
        { var e=Event(trigger); foreach(var channel in ManifestIO.Channels) Dispatch(e,channel,false); }
        internal void SetState(string state)
        {
            if(state==State) return; PreviousState=State;PreviousStateTime=StateTime; State=state; StateTime=0; transitionTime=Time; markerTimes.Clear();
            if(PreviousState=="charge") Stop("chargeEnd");
        }
        internal float Blend(AnimationClip clip) {return clip.transition<=0?1:MathHelper.Clamp((float)((Time-transitionTime)/clip.transition),0,1);}
        internal AnimationClip Clip(AnimationBinding binding,string state)
        { return binding.Definition.clips.Find(c=>c.state==state) ?? binding.Definition.clips.Find(c=>c.state=="idle"); }
        internal static int FrameIndex(AnimationClip clip,double time)
        {
            double length=clip.frames.Sum(f=>(double)f.duration); double phase=clip.loop?Math.Max(0,time)%length:Math.Min(Math.Max(0,time),length-1e-7);
            for(int i=0;i<clip.frames.Count;i++) { phase-=clip.frames[i].duration; if(phase<0) return i; } return clip.frames.Count-1;
        }
        internal AnimationFrame Frame(AnimationBinding binding) { var c=Clip(binding,State); return c==null?null:c.frames[FrameIndex(c,StateTime)]; }
        internal Vector2 Anchor(string name)
        {
            AnimationBinding b; if(Assets.Animations.TryGetValue(NativeAppearance.BaseItem,out b))
                return Anchor(b,name);
            switch(name) { case "head": return new Vector2(0,-28); case "back": return new Vector2(Flipped?7:-7,-19); default:return Vector2.Zero; }
        }
        internal Vector2 Anchor(AnimationBinding binding,string name)
        {var f=Frame(binding);var a=f==null?null:f.anchors.Find(x=>x.id==name);if(a!=null)return new Vector2(Flipped?-a.x:a.x,a.y);return name=="head"?new Vector2(0,-28):name=="back"?new Vector2(Flipped?7:-7,-19):Vector2.Zero;}
        internal float AnchorRotation(AnimationBinding binding,string name)
        {var f=Frame(binding);var a=f==null?null:f.anchors.Find(x=>x.id==name);return a==null?0:MathHelper.ToRadians(a.rotation*(Flipped?-1:1));}
        internal void Emit(EffectBinding binding,PresentationEvent e)
        {
            var d=binding.Definition; double last;
            if(cooldowns.TryGetValue(binding.Key,out last) && Time-last<d.cooldown) return;
            cooldowns[binding.Key]=Time;
            if(d.kind=="particles")
            {
                Spawn(binding,e,(int)Math.Round(d.count*binding.Scale*Selection.ParticleScale));
                if(d.rate>0 && emitters.Count<64 && !emitters.Any(x=>x.Effect.Key==binding.Key)) emitters.Add(new Emitter{Effect=binding,Event=e.Copy()});
            }
            else if(d.kind=="sound") Play(binding);
            else if(AllowShake && shakes.Count<16) shakes.Add(new Shake{Effect=binding});
        }
        private float Random(float min,float max) { return min+(float)random.NextDouble()*(max-min); }
        private void Spawn(EffectBinding binding,PresentationEvent e,int count)
        {
            var d=binding.Definition; int alive=Particles.Count(p=>p.Effect.Key==binding.Key);
            if(d.liquidScale && Liquid!=null)count=(int)Math.Round(count*Liquid.Level/LiquidMotion.Capacity);
            count=Math.Min(count,Math.Min(2048-Particles.Count,d.maxAlive-alive));
            if(d.collision=="water")count=Math.Min(count,256-Particles.Count(p=>p.Effect.Definition.collision=="water"));
            if(count<=0)return;
            var startColor=Color(d.color);var endColor=Color(d.endColor);
            var nativeSprites=d.native.Length>0?NativeEffects.ParticleSprites(d.native):null;
            var texture=d.texture.Length>0?Assets.Texture(binding.Package,d.texture):null;
            for(int i=0;i<count;i++)
            {
                float angle=MathHelper.ToRadians(d.angle+Random(-d.spread/2,d.spread/2)); float speed=Math.Max(0,d.speed+Random(-d.speedSpread,d.speedSpread));
                var offset=Anchor(d.anchor)+new Vector2(e.Flipped?-d.offsetX:d.offsetX,d.offsetY);
                var p=Particles.Spawn();if(p==null)break;
                p.Effect=binding;p.Position=(d.space=="world"?new Vector2(e.X,e.Y):Vector2.Zero)+offset;
                p.Velocity=new Vector2((float)Math.Cos(angle)*(e.Flipped?-1:1),(float)Math.Sin(angle))*speed;
                p.StartColor=startColor;p.EndColor=endColor;p.Texture=texture;p.NativeSprites=nativeSprites;
                p.FlowDirection=i%2==0?-1:1;p.Life=Math.Max(.01f,d.life+Random(-d.lifeSpread,d.lifeSpread));p.Rotation=MathHelper.ToRadians(d.rotation);
            }
        }
        private void Play(EffectBinding binding)
        {
            if(binding.Definition.loop&&loops.Count<32)loops[binding.Key]=binding;
            if(Silent || Selection.SoundVolume<=0 || voices.Count>=32) return;
            var d=binding.Definition;
            if(voices.Count(v=>v.Effect.Key==binding.Key)>=d.voices || (d.loop && voices.Any(v=>v.Effect.Key==binding.Key))) return;
            var sound=d.native.Length>0?NativeEffects.Sound(d.native):Assets.Sound(binding.Package,d.sounds[random.Next(d.sounds.Count)]);
            if(sound==null) return;
            var instance=sound.CreateInstance();
            try { instance.IsLooped=d.loop; instance.Pitch=MathHelper.Clamp(d.pitch+Random(-d.pitchSpread,d.pitchSpread),-1,1);
                instance.Volume=MathHelper.Clamp(d.volume*binding.Scale*Selection.SoundVolume*MasterVolume(),0,1); instance.Play(); voices.Add(new Voice{Effect=binding,Instance=instance}); }
            catch { instance.Dispose(); throw; }
        }
        private static readonly Type soundPrefsType=typeof(Game1).Assembly.GetType("JumpKing.PlayerPreferences.SoundPrefsRuntime",true).BaseType;
        private static readonly System.Reflection.FieldInfo soundPrefsInstance=soundPrefsType.GetField("instance");
        private static readonly System.Reflection.MethodInfo getSoundPrefs=soundPrefsType.GetMethod("GetPrefs");
        private static float MasterVolume()
        {
            object owner=soundPrefsInstance.GetValue(null); if(owner==null)return 0;
            object prefs=getSoundPrefs.Invoke(owner,null); var type=prefs.GetType();
            return (bool)type.GetField("sfx_on").GetValue(prefs)?(float)type.GetField("master").GetValue(prefs):0;
        }
        internal void Pause(bool paused)
        { foreach(var v in voices) { if(paused && v.Instance.State==SoundState.Playing) v.Instance.Pause(); else if(!paused && v.Instance.State==SoundState.Paused) v.Instance.Resume(); } }
        private void Stop(string trigger)
        {
            foreach(var key in loops.Where(v=>v.Value.Definition.stopOn==trigger).Select(v=>v.Key).ToArray())loops.Remove(key);
            for(int i=voices.Count-1;i>=0;i--) if(voices[i].Effect.Definition.loop && voices[i].Effect.Definition.stopOn==trigger) { voices[i].Instance.Dispose(); voices.RemoveAt(i); }
            emitters.RemoveAll(e=>e.Effect.Definition.loop && e.Effect.Definition.stopOn==trigger);
        }
        internal void Update(float delta)
        {
            if(Disposed || delta<=0) return; delta=Math.Min(delta,.1f); Time+=delta; StateTime+=delta;
            if(Liquid!=null)Liquid.Update(delta,Velocity,State);
            if(!Silent)foreach(var loop in loops.Values.ToArray())if(!voices.Any(v=>v.Effect.Key==loop.Key))Play(loop);
            updateCollision=ParticleCollision ?? (WorldParticles?waterCollision.Prepare(Particles):null);
            try {Particles.Update(delta);}
            finally {waterCollision.Clear();updateCollision=null;}
            for(int i=emitters.Count-1;i>=0;i--)
            {
                var emitter=emitters[i]; var d=emitter.Effect.Definition; emitter.Age+=delta;
                if(!d.loop && emitter.Age>d.duration) { emitters.RemoveAt(i); continue; }
                emitter.Fraction+=d.rate*emitter.Effect.Scale*Selection.ParticleScale*delta; int count=(int)emitter.Fraction; emitter.Fraction-=count;
                if(count>0) Spawn(emitter.Effect,Event(emitter.Event.Trigger),count);
            }
            for(int i=voices.Count-1;i>=0;i--) { var v=voices[i]; if(v.Instance.State==SoundState.Stopped) {v.Instance.Dispose(); voices.RemoveAt(i);}
                else v.Instance.Volume=MathHelper.Clamp(v.Effect.Definition.volume*v.Effect.Scale*Selection.SoundVolume*MasterVolume(),0,1); }
            ShakeOffset=Vector2.Zero;
            for(int i=shakes.Count-1;i>=0;i--) { var s=shakes[i]; var d=s.Effect.Definition; s.Age+=delta;
                if(s.Age>=d.duration) {shakes.RemoveAt(i);continue;} float phase=(float)Math.Sin(s.Age*d.frequency*Math.PI*2);
                if(d.waveform=="noise") phase=Random(-1,1); else if(d.waveform=="alternating") phase=Math.Sign(phase);
                ShakeOffset+=new Vector2(d.shakeX,d.shakeY)*phase*(1-s.Age/d.duration)*s.Effect.Scale*Selection.ShakeScale; }
            ShakeOffset=Vector2.Clamp(ShakeOffset,new Vector2(-16),new Vector2(16));
            foreach(var b in Assets.Animations.Values)
            {
                if(b.Item!=NativeAppearance.BaseItem && !Equipment.Contains(b.Item)) continue;
                var clip=Clip(b,State); if(clip==null) continue;double from;
                if(!markerTimes.TryGetValue(b.Item,out from))from=-1e-8;
                EmitMarkers(b,clip,from,StateTime);markerTimes[b.Item]=StateTime;
                foreach(var n in b.Definition.attachments)
                {
                    string key=b.Package.Manifest.id+"/"+b.Definition.id+"/"+n.id; float angle,velocity; joints.TryGetValue(key,out angle); jointVelocities.TryGetValue(key,out velocity);
                    float target=MathHelper.Clamp(-Velocity.X*n.inertia*(Flipped?-1:1),-45,45); float dt=delta/4;
                    for(int j=0;j<4;j++) { velocity+=((target-angle)*n.stiffness-velocity*n.damping)*dt; angle+=velocity*dt; }
                    joints[key]=MathHelper.Clamp(angle,-90,90); jointVelocities[key]=velocity;
                }
            }
        }
        private void EmitMarkers(AnimationBinding binding,AnimationClip clip,double from,double to)
        {
            double length=clip.frames.Sum(f=>(double)f.duration);
            // Seek reconstruction normally advances one tick, cap catch-up independently of author input
            long first=clip.loop?(long)Math.Max(0,Math.Floor(from/length)):0;
            long last=clip.loop?(long)Math.Max(0,Math.Floor(to/length)):0;
            first=Math.Max(first,last-24);
            for(long cycle=first;cycle<=last;cycle++)
            {
                double at=cycle*length;
                foreach(var frame in clip.frames)
                {
                    if(at>from && at<=to)foreach(var reference in frame.effects)Emit(Assets.Library.ResolveEffect(binding.Package,reference),Event(State));
                    at+=frame.duration;
                }
            }
        }
        private void StepParticle(Particle p,float delta)
        {
            var d=p.Effect.Definition;
            if(d.collision=="water"){SpilledWater.Step(p,delta,updateCollision);return;}
            p.Velocity.Y+=d.gravity*delta;p.Velocity*=(float)Math.Exp(-d.drag*delta);p.Position+=p.Velocity*delta;p.Rotation+=MathHelper.ToRadians(d.spin)*delta;
        }
        internal float Joint(AnimationBinding b,Attachment n) { float value; joints.TryGetValue(b.Package.Manifest.id+"/"+b.Definition.id+"/"+n.id,out value); return value; }
        internal void Reset(int seed=1)
        {
            foreach(var v in voices) v.Instance.Dispose(); voices.Clear();loops.Clear();stoppedFor=null; Particles.Clear(); emitters.Clear(); shakes.Clear(); cooldowns.Clear(); joints.Clear(); jointVelocities.Clear(); markerTimes.Clear();
            ShakeOffset=Vector2.Zero; Time=StateTime=PreviousStateTime=transitionTime=0; random=new Random(seed); State=PreviousState="idle";
            jumpTime=landingTime=-1;JumpCharge=0;
            if(Liquid!=null)Liquid.Reset();
            waterCollision.Clear();
            ParticleBatch.Clear();
        }
        internal static Color Color(string value)
        { uint c=uint.Parse(value.Substring(1),NumberStyles.HexNumber,CultureInfo.InvariantCulture); return new Color((byte)(c>>24),(byte)(c>>16),(byte)(c>>8),(byte)c); }
        public void Dispose() { if(Disposed)return; Reset();Particles.Dispose();ParticleBatch.Dispose(); Assets.Dispose(); if(appearanceLease!=null)appearanceLease.Dispose();appearanceLease=null;Disposed=true; }
    }
}
