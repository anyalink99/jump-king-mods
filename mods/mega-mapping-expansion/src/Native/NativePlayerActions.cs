using System;
using System.Linq;
using System.Reflection;
using EntityComponent.BT;
using JKRuntime;
using JKRuntime.Gameplay;
using JKRuntime.Presentation;
using JumpKing;
using JumpKing.API;
using JumpKing.Controller;
using JumpKing.Player;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MegaMappingExpansion.Api;

namespace MegaMappingExpansion.Construction
{
    // physics ownership lives here; timings, directions and effects belong to the scene
    internal sealed class NativePlayerActions : IDisposable
    {
        private const string LeaseId="mega-mapping-expansion.player-actions";
        private readonly SceneProgram scene;
        private readonly PlayerEntity player;
        private readonly BehaviorTreeComp brain;
        private readonly RuntimeScope resources=new RuntimeScope();
        private readonly Sprite hidden=new EmptySprite();
        private sealed class EmptySprite : Sprite
        {
            public override void Draw(Vector2 position,SpriteEffects effects=SpriteEffects.None){}
            public override void Draw(float x,float y,SpriteEffects effects=SpriteEffects.None){}
            public override void Draw(Point point,SpriteEffects effects=SpriteEffects.None){}
            public override void Draw(Rectangle rectangle,SpriteEffects effects=SpriteEffects.None){}
        }
        private RuntimeScope hold;
        private string target,owner;
        private bool bodyEnabled,brainEnabled;
        private static readonly FieldInfo Ground=typeof(BodyComp).GetField("_is_on_ground",OwnedPatches.Members);
        private static readonly FieldInfo Knocked=typeof(BodyComp).GetField("_knocked",OwnedPatches.Members);
        private static readonly FieldInfo Last=typeof(BodyComp).GetField("_last_velocity",OwnedPatches.Members);
        internal static void Prepare(SceneProgram scene)
        {
            if(!scene.Components.Values.Any(c=>c.Descriptor.Kind=="playerControl"))return;
            if(Ground==null||Ground.FieldType!=typeof(bool)||Knocked==null||Knocked.FieldType!=typeof(bool)||Last==null||Last.FieldType!=typeof(Vector2))
                throw new NotSupportedException("Installed player movement fields are incompatible with scene actions");
        }
        internal NativePlayerActions(SceneProgram program,PlayerEntity actor)
        {
            scene=program;player=actor;brain=player.GetComponent<BehaviorTreeComp>();
            if(brain==null)throw new InvalidOperationException("Player actions require the native behavior tree");
            try
            {
                if(scene.Components.Values.Any(c=>c.Descriptor.Kind=="playerControl"))
                    resources.Own(PlayerVisuals.Register(player,LeaseId,VisualPhase.Replacement,s=>hidden,()=>target!=null&&scene.Properties.Read(target,"hidePlayer").X!=0));
                scene.CancelNativeOwner=CancelOwner;
                foreach(var c in scene.Components.Values.Where(c=>c.Descriptor.Kind=="playerControl"))Publish(c.Id,"captured",SceneValue.Boolean(false));
            }
            catch{resources.Dispose();throw;}
        }
        internal static SceneValue ReadInput()
        {
            if(ControllerManager.instance==null)return SceneValue.Vector(0,0);
            var pad=ControllerManager.instance.GetPadState();
            return SceneValue.Vector((pad.right?1:0)-(pad.left?1:0),(pad.down?1:0)-(pad.up?1:0));
        }
        private void Publish(string id,string name,SceneValue value){scene.Signals.Publish(id+"."+name,value,ObservationQuality.Exact);}
        private void Event(string id,string name){scene.Signals.Emit(id+"."+name,id,"player",Camera.CurrentScreenIndex1,scene.GameplayTime);}
        private void Deny(NativeSceneAction action){action.Succeeded=false;Event(action.Target,"denied");}
        internal bool Handle(NativeSceneAction action)
        {
            if(action.Operation!="capturePlayer"&&action.Operation!="releasePlayer"&&action.Operation!="setPlayerVelocity"&&action.Operation!="addPlayerVelocity")return false;
            if(JKRuntime.World.WorldControl.Following){action.Succeeded=false;return true;}
            if(action.Operation=="releasePlayer") {if(target==action.Target){player.GetComponent<InputComponent>().TryConsumeJump();Release(false);}return true;}
            if(NativePause.IsPaused||JKRuntime.UI.UIApi.IsOpen){action.Succeeded=false;return true;}
            if(action.Operation=="capturePlayer") {CapturePlayer(action);return true;}
            if(target!=null&&target!=action.Target||!PlayerControl.Available(player.m_body,LeaseId)||target==null&&(!brain.Enabled||!player.m_body.Enabled||PresentationActivity.IsActive(player.m_body))) {Deny(action);return true;}
            var v=action.Value.Value;
            if(v.Kind!=SceneValueKind.Vector2||Math.Abs(v.X)>6000||Math.Abs(v.Y)>6000)throw new InvalidOperationException("Player velocity must be finite and within +/-6000 px/s per axis");
            var velocity=new Vector2((float)v.X/60,(float)v.Y/60);
            if(action.Operation=="addPlayerVelocity")velocity+=player.m_body.Velocity;
            if(Math.Abs(velocity.X)>100||Math.Abs(velocity.Y)>100)throw new InvalidOperationException("Accumulated player velocity exceeds 6000 px/s");
            SetVelocity(velocity);player.GetComponent<InputComponent>().TryConsumeJump();return true;
        }
        private void CapturePlayer(NativeSceneAction action)
        {
            if(target!=null||!brain.Enabled||!player.m_body.Enabled||PresentationActivity.IsActive(player.m_body)) {Deny(action);return;}
            var box=player.m_body.GetHitbox();Vector2 position=player.m_body.Position;
            string transform=scene.Properties.Read(action.Target,"transform").Text;
            if(transform!="")
            {
                var poses=new TransformSystem(scene);var pose=poses.Resolve(transform);
                // screen-local authoring converts to the native upward world stack here
                if(pose.Screen!=0&&pose.Screen!=Camera.CurrentScreenIndex1){Deny(action);return;}
                int screen=pose.Screen==0?Camera.CurrentScreenIndex1:pose.Screen;
                position=new Vector2((float)pose.X-box.Width/2f,(float)pose.Y-(screen-1)*360-box.Height/2f);
                Rectangle overlap;JumpKing.Level.AdvCollisionInfo collision;
                if(JumpKing.Level.LevelManager.CheckCollision(new Rectangle((int)position.X,(int)position.Y,box.Width,box.Height),out overlap,out collision)){Deny(action);return;}
            }
            PlayerControl.Lease lease;if(!PlayerControl.TryAcquire(player.m_body,LeaseId,out lease)){Deny(action);return;}
            hold=new RuntimeScope();hold.Own(lease);
            try
            {
                bodyEnabled=player.m_body.Enabled;brainEnabled=brain.Enabled;Suspend();target=action.Target;owner=action.Owner;
                var entry=action.Event==null?null:action.Event.Payload.FirstOrDefault(p=>p.Id=="velocity"&&p.Value.Kind==SceneValueKind.Vector2&&p.Quality!=ObservationQuality.Unavailable);
                Publish(target,"entryVelocity",entry==null?SceneValue.Vector(player.m_body.Velocity.X*60,player.m_body.Velocity.Y*60):entry.Value);
                player.m_body.Position=position;SetVelocity(Vector2.Zero);Publish(target,"captured",SceneValue.Boolean(true));Event(target,"acquired");
            }
            catch{Release(true);throw;}
        }
        private void Suspend()
        {
            hold.Own(ComponentSuspension.Acquire(LeaseId,player.m_body,bodyEnabled));
            hold.Own(ComponentSuspension.Acquire(LeaseId,brain,brainEnabled));
            hold.Own(PresentationActivity.Begin(LeaseId,player.m_body));
        }
        private void SetVelocity(Vector2 value)
        {player.m_body.Velocity=value;Last.SetValue(player.m_body,value);Ground.SetValue(player.m_body,false);Knocked.SetValue(player.m_body,false);}
        private void Release(bool cancel)
        {
            string previous=target;target=null;owner=null;
            if(hold!=null){if(cancel)SetVelocity(Vector2.Zero);hold.Dispose();hold=null;}
            if(previous!=null)Publish(previous,"captured",SceneValue.Boolean(false));
        }
        private void CancelOwner(string id){if(owner==id)Release(true);}
        internal sealed class State {internal string Target,Owner;internal bool BodyEnabled,BrainEnabled;}
        internal State Capture(){return new State{Target=target,Owner=owner,BodyEnabled=bodyEnabled,BrainEnabled=brainEnabled};}
        internal void Restore(State state)
        {
            Release(false);if(state==null||state.Target==null)return;
            PlayerControl.Lease lease;if(!PlayerControl.TryAcquire(player.m_body,LeaseId,out lease))throw new InvalidOperationException("Player is owned by another mechanic during scene restore");
            hold=new RuntimeScope();hold.Own(lease);
            try{target=state.Target;owner=state.Owner;bodyEnabled=state.BodyEnabled;brainEnabled=state.BrainEnabled;Suspend();Publish(target,"captured",SceneValue.Boolean(true));}
            catch{Release(true);throw;}
        }
        internal void Cancel(){Release(true);}
        public void Dispose(){Cancel();scene.CancelNativeOwner=null;resources.Dispose();}
    }
}
