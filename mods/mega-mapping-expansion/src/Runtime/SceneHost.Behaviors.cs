using System;
using System.Collections.Generic;
using System.Linq;
using JumpKing;
using MegaMappingExpansion.Api;
using Microsoft.Xna.Framework;

namespace MegaMappingExpansion
{
    internal sealed partial class SceneHost
    {
        internal SceneBehaviorEngine behaviors;
        internal SceneFile SceneData { get { return scene; } }
        internal string RootPath { get { return levelRoot; } }
        private readonly HashSet<PropData> dirtyProps = new HashSet<PropData>();
        private bool lightMembershipDirty;
        private IDisposable gameplayEvents;
        private HashSet<string> behaviorEvents;
        private int blockerRevision;
        internal bool DebugRegions;
        internal void StepBehaviorPreview(double seconds)
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-debug") < 0) throw new InvalidOperationException("Scene stepping requires -debug");
            if (seconds <= 0 || seconds > 1) throw new ArgumentOutOfRangeException("seconds");
            time += (float)seconds; behaviors.Tick(seconds, true, true, behaviors.Actor); ResolveBehaviorTransforms();
        }
        private void InitializeBehaviors()
        {
            foreach (PropData prop in scene.Props.Concat(scene.Nodes)) attachedProps.Add(prop.Id, prop);
            behaviors = new SceneBehaviorEngine(scene, delegate(object target, string property) {
                var prop = target as PropData;
                if (prop != null && (property == "motion" || property == "tint")) dirtyProps.Add(prop);
                if (target is SceneAnchor) { blockerRevision++; work.RefreshBlockers(scene); }
                if (target == scene.Options && Current == this) MappingState.Commit(scene);
            });
            behaviorEvents = new HashSet<string>(BehaviorTreeCompiler.Events(scene), StringComparer.Ordinal);
            if (behaviorEvents.Any(NativeHiddenWalls.IsEvent))
            {
                var nativeWalls = new HashSet<string>(Game1.instance.contentManager.props.RaymanScreens.Values.SelectMany(c => c.walls).Select(w => w.texture_name), StringComparer.Ordinal);
                foreach (string name in behaviorEvents)
                    if (NativeHiddenWalls.IsEvent(name) && !nativeWalls.Contains(name.Substring(name.IndexOf(':') + 1)))
                        throw new System.IO.InvalidDataException(name + ": hidden-wall texture_name was not found in native props/hidden_walls");
            }
            foreach (RuleData rule in scene.Rules)
                if (!string.IsNullOrEmpty(rule.Sound) && !Game1.instance.contentManager.audio.music.event_music.ContainsKey(rule.Sound))
                    throw new System.IO.InvalidDataException(rule.Id + ": native event sound not found: " + rule.Sound + "; place compiled audio in audio/music/event_music");
            behaviors.PlaySound = delegate(string key) { Game1.instance.contentManager.audio.music.event_music[key].Play(); };
            foreach (string sound in behaviors.TreeSounds)
                if (!Game1.instance.contentManager.audio.music.event_music.ContainsKey(sound))
                    throw new System.IO.InvalidDataException("Behavior tree: native event sound not found: " + sound);
            LoadPersistence();
        }
        private void ActivateBehaviorEvents()
        {
            if (gameplayEvents != null) return;
            if (!Enum.GetNames(typeof(JKRuntime.Gameplay.GameplayEventKind)).Any(name => behaviorEvents.Contains(name.ToLowerInvariant()))) return;
            gameplayEvents = JKRuntime.Gameplay.GameplayEvents.Subscribe("mega-mapping-expansion", delegate(JKRuntime.Gameplay.GameplayEvent value) {
                if (Current != this || !MappingSettings.Enabled) return;
                if (value.Kind == JKRuntime.Gameplay.GameplayEventKind.RestoreStarted || value.Kind == JKRuntime.Gameplay.GameplayEventKind.RestoreCompleted || value.Kind == JKRuntime.Gameplay.GameplayEventKind.RestoreFailed) return;
                string name = value.Kind.ToString().ToLowerInvariant();
                if (behaviorEvents.Contains(name)) behaviors.Emit(name, value.Screen + 1);
            });
        }
        private void TickBehaviors(float delta)
        {
            if (behaviors == null) return;
            SynchronizePersistentReset();
            var player = GameLoopPlayer();
            var sample = new SceneActor { Present = player != null, Screen = Camera.CurrentScreenIndex1 };
            if (player != null)
            {
                sample.Bounds = Camera.TransformRect(player.m_body.GetHitbox()); sample.FacingLeft = NativeSceneAdapter.FacingLeft(player);
                sample.Grounded = player.m_body.IsOnGround;
                if (sample.Grounded && behaviors.NeedsSupport(sample.Screen))
                    sample.Supports = NativeSupport.Sample(player.m_body.GetHitbox());
            }
            bool stepping = previewPaused && frameDelta > 0;
            bool gameplay = !JKRuntime.Gameplay.NativePause.IsPaused && !JKRuntime.UI.UIApi.IsOpen;
            behaviors.Tick(previewPaused ? frameDelta : Math.Min(Math.Max(delta, 0), .1f), (gameplay && !previewPaused) || stepping, !previewPaused || stepping, sample);
            ResolveBehaviorTransforms();
        }
        internal void ResolveBehaviorTransforms()
        {
            foreach (PropData prop in dirtyProps) prepared.Refresh(prop);
            dirtyProps.Clear(); propPoses.Clear();
            bool membership = behaviors.MembershipChanged;
            foreach (PropData prop in work.Attachments)
            {
                PropPose pose = ResolvePropPose(prop);
                if (pose.Visible && prop.Screen != pose.Screen) { prop.Screen = pose.Screen; membership = true; }
            }
            foreach (LightData light in scene.Lights)
            {
                light.AttachmentVisible = true; light.IgnoreAttachmentActor = false;
                if (string.IsNullOrEmpty(light.Attach)) continue;
                PropPose pose = AttachmentPose(light.Attach);
                light.AttachmentVisible = pose.Visible;
                light.IgnoreAttachmentActor = PlayerAttachment(light.Attach);
                if (!pose.Visible) continue;
                Vector2 offset = new Vector2(light.OffsetX * pose.Scale * pose.ScaleX, light.OffsetY * pose.Scale * pose.ScaleY);
                float cosine = (float)Math.Cos(pose.Rotation), sine = (float)Math.Sin(pose.Rotation);
                light.X = pose.Position.X + offset.X * cosine - offset.Y * sine;
                light.Y = pose.Position.Y + offset.X * sine + offset.Y * cosine;
                if (light.Screen != pose.Screen) { light.Screen = pose.Screen; membership = true; }
            }
            if (membership || lightMembershipDirty)
            {
                work = new SceneWorkIndex(scene);
                renderPlans.Clear(); BuildRenderQueues(); lightFieldScreen = -1;
                foreach (LightData gone in lightFields.Keys.Where(l => !scene.Lights.Contains(l)).ToArray()) lightFields.Remove(gone);
                foreach (string gone in scatterMeshes.Keys.Where(id => !scene.Lights.Any(l => l.Id == id)).ToArray()) scatterMeshes.Remove(gone);
                behaviors.ConsumeMembershipChange(); lightMembershipDirty = false;
            }
        }
        private sealed class BehaviorSnapshot
        {
            internal SceneHost Owner; internal object State; internal float Time;
            internal Dictionary<int, float> Entered;
        }
        internal object CaptureBehavior()
        { return new BehaviorSnapshot { Owner = this, State = behaviors.Capture(), Time = time, Entered = new Dictionary<int, float>(screenEnteredAt) }; }
        internal void ValidateBehavior(object state)
        {
            var snapshot = state as BehaviorSnapshot;
            if (snapshot == null || snapshot.Owner != this) throw new InvalidOperationException("Scene changed since snapshot capture");
            behaviors.ValidateSnapshot(snapshot.State);
        }
        internal void RestoreBehavior(object state)
        {
            ValidateBehavior(state); var snapshot = (BehaviorSnapshot)state;
            behaviors.Restore(snapshot.State); time = snapshot.Time;
            screenEnteredAt.Clear(); foreach (var entry in snapshot.Entered) screenEnteredAt.Add(entry.Key, entry.Value);
            reactions.Clear(); waterSurfaces.Clear(); gazeOffsets.Clear(); propPoses.Clear(); lightMembershipDirty = true;
        }
    }

    internal sealed class SceneGateway : IMappingScene, JKRuntime.State.IStateParticipant, IDisposable
    {
        private readonly int thread = System.Threading.Thread.CurrentThread.ManagedThreadId;
        private bool disposed;
        private void Check() { if (thread != System.Threading.Thread.CurrentThread.ManagedThreadId) throw new InvalidOperationException("Mapping requires the game thread"); if (disposed) throw new ObjectDisposedException("Mapping capability"); }
        public void Dispose() { disposed = true; }
        private SceneHost Host { get { Check(); var host = SceneHost.Current; if (host == null) throw new InvalidOperationException("No active Mapping scene"); return host; } }
        public bool Available { get { if (disposed) return false; Check(); return SceneHost.Current != null; } }
        public long Generation { get { return Host.behaviors.Generation; } }
        public ISceneEffect Activate(string owner, string definitionId) { return Host.behaviors.Activate(owner, definitionId); }
        public ISceneEffect Apply(string owner, EffectDefinition definition) { return Host.behaviors.Apply(owner, definition); }
        public SceneObjectInfo[] InspectObjects() { return Host.behaviors.InspectObjects(); }
        public ScenePropertyInfo[] DescribeProperties(string objectId) { return Host.behaviors.DescribeProperties(objectId); }
        public EffectInfo[] InspectEffects() { return Host.behaviors.InspectEffects(); }
        public string[] InspectErrors() { return Host.behaviors.InspectErrors(); }
        public string GetFlag(string id) { return Host.behaviors.GetFlag(id); }
        public void SetFlag(string id, string value) { Host.behaviors.SetFlag(id, value); }
        public void Emit(string eventId) { Host.behaviors.Emit(eventId); }
        public string Id { get { return "mega.mapping.scene-state"; } }
        public int Version { get { return 1; } }
        public object Capture() { return Available ? Host.CaptureBehavior() : null; }
        public void Validate(object state) { if (Available) Host.ValidateBehavior(state); else if (state != null) throw new InvalidOperationException("Mapping scene is unavailable"); }
        public void Restore(object state) { Validate(state); if (Available) Host.RestoreBehavior(state); }
    }
}
