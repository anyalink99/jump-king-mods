using System;
using System.Collections.Generic;
using System.Linq;
using JKRuntime;
using JKRuntime.Gameplay;
using JKRuntime.Modules;
using JumpKing;
using JumpKing.Player;
using Microsoft.Xna.Framework.Graphics;
using MegaMappingExpansion.Api;

namespace MegaMappingExpansion.Construction
{
    internal sealed class NativeScene:IDisposable,JKRuntime.State.IStateParticipant,ISceneCompositor
    {
        internal static NativeScene Current{get;private set;}
        internal readonly SceneProgram Program;
        internal readonly NativeRenderer Renderer;
        internal readonly SceneGateway Gateway;
        internal readonly string Root;
        internal readonly NativeControllers Controllers;
        private readonly RuntimeScope resources=new RuntimeScope();
        private RuntimeScope activation;
        private NativeObservations observations;
        private readonly NativeCapture capture;
        private readonly NativeActorObservations actors;
        private readonly NativeEventAudio eventAudio;
        private ScenePersistence persistence;
        private int saveRevision;
        private bool disposed,completed,enabled=true;
        private bool activatedBefore;
        private bool rendered;
        private ChargeSample suspendedCharge;
        internal NativeScene(SceneDocument document,string root,NativeAssetPool pool)
        {
            Root=root;
            try
            {
                Program=resources.Own(new SceneProgram(document));Gateway=resources.Own(new SceneGateway(Program));
                if(!string.IsNullOrWhiteSpace(document.SaveId))
                {
                    Dictionary<string,string> saved;SceneSaveCoordinator.Load(document.SaveId,ScenePersistence.NativeDirectory,out persistence,out saved,out saveRevision);Program.LoadVariables(saved);
                }
                Program.Tick(0,0);
                Renderer=resources.Own(new NativeRenderer(Program,Game1.instance.GraphicsDevice,root,pool));
                Program.AssetIdentity(Renderer.AssetIdentity);
                actors=new NativeActorObservations(Program);capture=new NativeCapture{Actors=actors};Renderer.DrawNativeSelection=capture.Draw;
                foreach(var action in Actions(document))if(action.Operation=="sound"&&!Game1.instance.contentManager.audio.music.event_music.ContainsKey(action.Target))throw new System.IO.InvalidDataException("Native event sound is missing: "+action.Target);
                var soundKeys=Actions(document).Where(a=>a.Operation=="sound").Select(a=>a.Target).Concat(
                    Game1.instance.contentManager.props.RaymanScreens.Values.SelectMany(s=>s.walls)
                    .Where(w=>w.sfx_info!=null).SelectMany(w=>new[]{w.sfx_info.sfx_name_enable,w.sfx_info.sfx_name_disable}));
                var eventSounds=Game1.instance.contentManager.audio.music.event_music;
                eventAudio=resources.Own(new NativeEventAudio(soundKeys.Where(k=>k!=null&&eventSounds.ContainsKey(k)).Select(k=>eventSounds[k])));
                HiddenWallObservations.Validate(document);
                Controllers=resources.Own(new NativeControllers(Program,root,pool.Sounds));
                Program.AssetIdentity(Renderer.AssetIdentity+Controllers.AudioIdentity);
                Program.InvokeNative=Invoke;Program.VariablesChanged=PublishSaved;Program.WorldRestored=WorldRestored;
            }
            catch{resources.Dispose();throw;}
        }
        private static IEnumerable<ActionData> Actions(SceneDocument document)
        {
            foreach(var rule in document.Rules)foreach(var action in rule.Actions)yield return action;
            foreach(var tree in document.Trees)foreach(var node in tree.Nodes)foreach(var action in Actions(node))yield return action;
        }
        private static IEnumerable<ActionData> Actions(ControlNodeData node)
        {
            if(node.Action!=null)yield return node.Action;
            foreach(var child in node.Nodes)foreach(var action in Actions(child))yield return action;
        }
        internal void Activate(ModuleContext context,PlayerEntity player,MovementObservationScope movement)
        {
            if(disposed||activation!=null)throw new InvalidOperationException("Construction scene is already active or disposed");
            if(Current!=null)throw new InvalidOperationException("The previous construction scene must release activation first");
            var scope=new RuntimeScope();
            try
            {
                observations=scope.Own(new NativeObservations(Program,player,new TransformSystem(Program),movement));observations.Enabled=()=>enabled;
                observations.Completed=completed;if(suspendedCharge!=null)observations.RestoreCharge(suspendedCharge);
                capture.Attach(player);scope.Defer(()=>capture.Attach(null));
                Controllers.Activate(scope);
                scope.Own(new HiddenWallObservations(Program,()=>enabled&&!completed));
                if(NativeTransitions.Current!=null)foreach(string eventId in NativeTransitions.Current.Engine.Plan.Triggers.Where(t=>t.On=="event").Select(t=>t.Event).Distinct(StringComparer.Ordinal))
                    scope.Own(Program.Signals.SubscribeEvent(eventId,sample=>NativeTransitions.Emit(sample.Id)));
                scope.Own(Program.BindWorld());scope.Own(JKRuntime.State.GameState.Snapshots.Register(this));scope.Own(FrameComposition.RegisterScene(this));
                activation=scope;Current=this;PublishSaved();
                if(!activatedBefore){Program.Signals.Emit("scene.ready","scene","",Camera.CurrentScreenIndex1,Program.GameplayTime);activatedBefore=true;}
            }
            catch{observations=null;scope.Dispose();throw;}
        }
        internal void Deactivate()
        {if(observations!=null)suspendedCharge=observations.CaptureCharge();if(eventAudio!=null)eventAudio.Tick(false);if(Current==this)Current=null;if(activation!=null){activation.Dispose();activation=null;}observations=null;}
        private void Invoke(NativeSceneAction action)
        {
            if(Renderer.HandleAction(action))return;
            if(action.Operation=="playAudio"){Controllers.PlayVoice(action.Target);return;}
            if(action.Operation=="sound"){if(!JKRuntime.World.WorldControl.Following)Game1.instance.contentManager.audio.music.event_music[action.Target].Play();return;}
            throw new InvalidOperationException("Unknown native scene action: "+action.Operation);
        }
        private void PublishSaved(){if(persistence!=null&&JKRuntime.World.WorldExecution.CanPersist&&!JKRuntime.World.WorldControl.Following&&Current==this&&!disposed)SceneSaveCoordinator.Publish(persistence,Program.SavedVariables());}
        internal void Tick(double delta)
        {
            if(disposed||Current!=this)return;
            if(persistence!=null&&saveRevision!=SceneSaveCoordinator.Revision)
            {
                var next=SceneSaveCoordinator.ResetStore(persistence,out saveRevision);
                if(next!=null){persistence=next;Program.ResetSavedVariables();PublishSaved();}else saveRevision--;
            }
            if(enabled!=MappingSettings.Enabled){enabled=MappingSettings.Enabled;ClearTransient();}
            if(observations!=null)observations.Sample();
            if(!JKRuntime.World.WorldControl.Following)
            {
                bool ending=NativeActorObservations.IsEnding,paused=NativePause.IsPaused||JKRuntime.UI.UIApi.IsOpen,endingExit=false;
                using(Program.Signals.Batch())
                {
                    actors.Sample();string phase=ending?"ending":completed?"stopped":"playing";
                    string previous=Program.Signals.Read("scene.phase").Value.Text;
                    if(previous!=phase)
                    {
                        Program.Signals.Publish("scene.phase",SceneValue.String(phase),ObservationQuality.Sampled);
                        if(previous=="ending"){endingExit=true;Program.Signals.Emit("scene.ending.exit","native.ending","",Camera.CurrentScreenIndex1,Program.PresentationTime);}
                        if(ending)Program.Signals.Emit("scene.ending.enter","native.ending","",Camera.CurrentScreenIndex1,Program.PresentationTime);
                    }
                }
                Program.Tick(enabled&&!paused&&!completed?delta:0,PresentationDelta(enabled,paused,Renderer.AnimateWhilePaused(Camera.CurrentScreenIndex1),delta),enabled&&!paused&&(ending||endingExit));
            }
            Controllers.Tick(delta,enabled,NativePause.IsPaused||JKRuntime.UI.UIApi.IsOpen,completed||NativeActorObservations.IsEnding);
            eventAudio.Tick(enabled&&(NativePause.IsPaused||JKRuntime.UI.UIApi.IsOpen));
            Renderer.Tick();
        }
        internal void Complete(){completed=true;Program.Signals.Publish("scene.completed",SceneValue.Boolean(true),ObservationQuality.Sampled);if(observations!=null)observations.Completed=true;}
        internal static double PresentationDelta(bool enabled,bool paused,bool animateWhilePaused,double delta)
        {return enabled&&(!paused||animateWhilePaused)?delta:0;}
        private void ClearTransient(){Renderer.ClearTransient();Controllers.ResetTransient();if(observations!=null)observations.ClearTransient();}
        private void WorldRestored()
        {Renderer.ClearTransient();Controllers.ResetTransient();completed=Program.Signals.Read("scene.completed").Value.X!=0;if(observations!=null){observations.Completed=completed;observations.RestoreObservedCharge();}}
        public bool Active{get{return Current==this&&!disposed&&MappingSettings.Enabled;}}
        internal void Rendered()
        {if(rendered)return;rendered=true;ModEntry.Log("Rendered construction frame; screen="+Camera.CurrentScreenIndex1+", quad draws="+Renderer.QuadDrawCalls+", mesh vertex updates="+Renderer.MeshVertexUpdates);}
        public FrameStyle Style{get{JKRuntime.Presentation.CameraShot shot;return Renderer.Style(JKRuntime.Presentation.CameraShots.TryRead(out shot)?shot.Screen+1:Camera.CurrentScreenIndex1);}}
        public void Compose(RenderTarget2D target){Renderer.Compose(target,Camera.CurrentScreenIndex1);}
        public string Id{get{return "mega.mapping.scene-state";}}
        public int Version{get{return 2;}}
        private sealed class Snapshot
        {
            internal NativeScene Owner;
            internal ProgramSnapshot Program;
            internal ChargeSample Charge;
            internal bool Completed;
        }
        public object Capture(){return new Snapshot{Owner=this,Program=Program.Capture(),Charge=observations.CaptureCharge(),Completed=completed};}
        public void Validate(object state)
        {var snapshot=state as Snapshot;if(snapshot==null||snapshot.Owner!=this||snapshot.Charge==null)throw new InvalidOperationException("Construction scene changed since snapshot capture");Program.Validate(snapshot.Program);}
        public void Restore(object state)
        {
            Validate(state);var snapshot=(Snapshot)state;Program.Restore(snapshot.Program);completed=snapshot.Completed;observations.Completed=completed;observations.RestoreCharge(snapshot.Charge);Renderer.ClearTransient();Controllers.ResetTransient();
        }
        public void Dispose(){if(disposed)return;Deactivate();disposed=true;resources.Dispose();}
    }
    internal sealed partial class SceneProgram
    {
        internal void ResetSavedVariables()
        {
            foreach(var definition in variableDefinitions.Values.Where(v=>v.Scope==SceneStateScope.Save))Signals.Publish("variable:"+definition.Id,SceneValue.Parse(definition.Kind,definition.Value),ObservationQuality.Exact);graphDirty=true;
        }
    }
}
