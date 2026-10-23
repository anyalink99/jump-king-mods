using System;
using System.Linq;
using JKRuntime;
using JKRuntime.Gameplay;
using JumpKing;
using JumpKing.Player;
using Microsoft.Xna.Framework;
using MegaMappingExpansion.Api;

namespace MegaMappingExpansion.Construction
{
    internal sealed class NativeObservations : IDisposable
    {
        private readonly SceneProgram scene;
        private readonly PlayerEntity player;
        private readonly SensorSystem sensors;
        private readonly TransformSystem transforms;
        private readonly NativeContacts contacts;
        private readonly RuntimeScope resources=new RuntimeScope();
        private readonly ChargeMonitor charge;
        private readonly bool hasSensors;
        internal bool Completed;
        internal Func<bool> Enabled=()=>true;
        internal NativeObservations(SceneProgram program,PlayerEntity actor,TransformSystem poses,MovementObservationScope preparedMovement)
        {
            scene=program;player=actor;transforms=program.ObservationPoses;
            hasSensors=scene.Components.Values.Any(c=>c.Descriptor.Kind=="sensor");
            try
            {
                sensors=program.Sensors;charge=resources.Own(new ChargeMonitor());
                MovementObserver movement=preparedMovement==null?null:resources.Own(preparedMovement.Observe(actor.m_body));contacts=new NativeContacts(movement);
                resources.Own(GameplayEvents.Subscribe("mega-mapping-expansion.observations",ObserveEvent));
                resources.Own(PlayerUpdates.Register(actor,"mega-mapping-expansion.sensors",PlayerUpdatePhase.BeforeInput,ObserveContact,-100));
            }
            catch{resources.Dispose();throw;}
        }
        private bool Paused{get{return Completed||NativePause.IsPaused||JKRuntime.UI.UIApi.IsOpen;}}
        private void ObserveEvent(GameplayEvent sample)
        {
            if(JKRuntime.World.WorldControl.Following)return;
            if(sample.Kind==GameplayEventKind.RestoreStarted||sample.Kind==GameplayEventKind.RestoreCompleted||sample.Kind==GameplayEventKind.RestoreFailed)return;
            switch(sample.Kind)
            {
                case GameplayEventKind.ChargeStarted:charge.Begin();break;
                case GameplayEventKind.ChargeEnded:charge.End();break;
                case GameplayEventKind.Jump:charge.Release(sample.Jump==null?null:sample.Jump.CorrectedFrameCount,sample.Source,sample.Jump!=null&&sample.Jump.Automatic);break;
                case GameplayEventKind.Teleported:charge.End();break;
                case GameplayEventKind.PauseChanged:charge.Pause(sample.Paused);break;
            }
            if(!Enabled())return;
            scene.Signals.Emit(sample.Kind.ToString().ToLowerInvariant(),sample.Source,"player",sample.Screen+1,scene.GameplayTime,
                new SignalSample("position",SceneValue.Vector(sample.Position.X,sample.Position.Y+sample.Screen*360),ObservationQuality.Sampled,sample.Sequence),
                new SignalSample("velocity",SceneValue.Vector(sample.Velocity.X*60,sample.Velocity.Y*60),ObservationQuality.Sampled,sample.Sequence));
        }
        internal void Sample()
        {
            using(scene.Signals.Batch())SampleFrame();
        }
        private void SampleFrame()
        {
            if(JKRuntime.World.WorldControl.Following)return;
            var box=player.m_body.GetHitbox();int screen=Camera.CurrentScreenIndex1;double y=box.Y+(screen-1)*360;var velocity=player.m_body.Velocity;
            Publish("player.position",SceneValue.Vector(box.X,y));Publish("player.center",SceneValue.Vector(box.Center.X,y+box.Height/2));Publish("player.feet",SceneValue.Vector(box.Center.X,y+box.Height));
            Publish("player.velocity",SceneValue.Vector(velocity.X*60,velocity.Y*60));Publish("player.screen",SceneValue.Integer(screen));Publish("player.present",SceneValue.Boolean(player.IsAlive));
            Publish("player.grounded",SceneValue.Boolean(player.m_body.IsOnGround));Publish("player.facingLeft",SceneValue.Boolean(NativeSceneAdapter.FacingLeft(player)));Publish("paused",SceneValue.Boolean(Paused));
            Publish("scene.completed",SceneValue.Boolean(Completed));
            PlayerObservations.Publish(scene,velocity.X*60,velocity.Y*60,player.m_body.IsOnGround,NativeSceneAdapter.Walking,NativeSceneAdapter.Splat(player),Enabled()&&!Paused);
            charge.Pause(Paused);charge.Tick(NativeSceneAdapter.ChargeTimer(player));var sample=charge.Read();
            Publish("charge.active",SceneValue.Boolean(sample.Phase==ChargePhase.Charging));
            Publish("charge.nativeTimerSeconds",SceneValue.Number(sample.NativeTimerSeconds));
            scene.Signals.Publish("charge.heldFrames",SceneValue.Number(sample.HeldFrames),sample.ReleasedFrames.HasValue?ObservationQuality.Exact:ObservationQuality.Estimated);
            Publish("charge.maximumFrames",SceneValue.Number(sample.MaximumFrames));
            scene.Signals.Publish("charge.elapsedMilliseconds",SceneValue.Number(sample.ElapsedMilliseconds),ObservationQuality.Sampled);
            scene.Signals.Publish("charge.releasedFrames",SceneValue.Number(sample.ReleasedFrames??0),sample.ReleasedFrames.HasValue?ObservationQuality.Exact:ObservationQuality.Unavailable);
            if(!Paused&&Enabled()){transforms.Refresh();sensors.Observe(Actor(),new SurfaceObservation[0],"scene",1.0/60);}
        }
        private void Publish(string id,SceneValue value){scene.Signals.Publish(id,value,ObservationQuality.Sampled);}
        private ActorObservation Actor()
        {
            var box=player.m_body.GetHitbox();int screen=Camera.CurrentScreenIndex1;
            return new ActorObservation{Id="player",Screen=screen,Bounds=new SceneBox{X=box.X,Y=box.Y+(screen-1)*360,Width=box.Width,Height=box.Height},Velocity=SceneValue.Vector(player.m_body.Velocity.X*60,player.m_body.Velocity.Y*60)};
        }
        private void ObserveContact(float delta)
        {
            if(Paused||!Enabled()||JKRuntime.World.WorldControl.Following)return;
            charge.Limits(NativeSceneAdapter.ChargeLimit(player),player.m_body.GetMultipliers());
            NativeSceneAdapter.Walking=false;
            if(!hasSensors&&!scene.Signals.ObservesEvent("player.impact"))return;
            var surfaces=contacts.Read(player);
            foreach(var surface in surfaces)if(surface.Impact)
                scene.Signals.Emit("player.impact",surface.Id,"player",surface.Screen,scene.GameplayTime,
                    new SignalSample("position",surface.Position,surface.PositionQuality,0),
                    new SignalSample("normal",surface.Normal,surface.NormalQuality,0),
                    new SignalSample("velocity",surface.Velocity,surface.VelocityQuality,0),
                    new SignalSample("surface",SceneValue.String(surface.Type),ObservationQuality.Exact,0));
            transforms.Refresh();sensors.Observe(Actor(),surfaces,"contact",Math.Min(Math.Max(0,delta),.1));
        }
        internal void ClearTransient(){sensors.Clear();contacts.ResetEvidence();}
        internal void ResetEvidence(){contacts.ResetEvidence();}
        internal void RestoreObservedCharge()
        {
            var released=scene.Signals.Read("charge.releasedFrames");
            charge.Restore(new ChargeSample{Phase=scene.Signals.Read("charge.active").Value.X!=0?ChargePhase.Charging:released.Quality==ObservationQuality.Unavailable?ChargePhase.Idle:ChargePhase.Released,Paused=scene.Signals.Read("paused").Value.X!=0,ElapsedMilliseconds=scene.Signals.Read("charge.elapsedMilliseconds").Value.X,HeldFrames=scene.Signals.Read("charge.heldFrames").Value.X,NativeTimerSeconds=(float)scene.Signals.Read("charge.nativeTimerSeconds").Value.X,ReleasedFrames=released.Quality==ObservationQuality.Unavailable?(float?)null:(float)released.Value.X,Provider="world"});
            contacts.ResetEvidence();
        }
        internal ChargeSample CaptureCharge(){return charge.Capture();}
        internal void RestoreCharge(ChargeSample state){charge.Restore(state);contacts.ResetEvidence();}
        public void Dispose(){resources.Dispose();}
    }
}
