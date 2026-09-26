using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml.Serialization;
using MegaMappingExpansion.Api;
using Microsoft.Xna.Framework;

namespace MegaMappingExpansion
{
    internal struct SceneActor
    {
        internal bool Present, FacingLeft, Grounded;
        internal int Screen;
        internal Rectangle Bounds;
        internal Rectangle[] Supports;
    }

    internal sealed partial class SceneBehaviorEngine : IMappingScene, IDisposable
    {
        private sealed class Change { internal SceneProperty Property; internal object Value; internal string Mode; }
        private sealed class PreparedEffect { internal EffectDefinition Definition; internal Change[] Changes; internal LightData[] Lights; }
        private sealed class ActiveEffect
        {
            internal long Id;
            internal string Owner;
            internal EffectDefinition Definition;
            internal Change[] Changes;
            internal LightData[] Lights;
            internal double Started;
        }
        private sealed class Lease : ISceneEffect
        {
            private readonly SceneBehaviorEngine engine;
            private readonly long generation;
            public long Id { get; private set; }
            internal Lease(SceneBehaviorEngine value, long id) { engine = value; Id = id; generation = value.Generation; }
            public bool Active { get { engine.CheckThread(); return engine.Available && generation == engine.Generation && engine.active.Exists(e => e.Id == Id); } }
            public double RemainingSeconds { get { return Active ? engine.Remaining(engine.active.Find(e => e.Id == Id)) : 0; } }
            public void Dispose() { if (Active) engine.Cancel(Id); }
        }
        private static long generations;
        private readonly int thread = Thread.CurrentThread.ManagedThreadId;
        private readonly SceneFile scene;
        private readonly SceneProperties properties;
        private readonly Action<object, string> changed;
        private readonly Dictionary<string, PreparedEffect> preparedEffects = new Dictionary<string, PreparedEffect>(StringComparer.Ordinal);
        private readonly Dictionary<string, LightData> templates;
        private readonly List<ActiveEffect> active = new List<ActiveEffect>();
        private readonly HashSet<SceneProperty> touched = new HashSet<SceneProperty>();
        private readonly Dictionary<string, string> flags = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly LightData[] authoredLights;
        private struct SceneEvent { internal string Name; internal int Screen; }
        private readonly Queue<SceneEvent> events = new Queue<SceneEvent>();
        internal Action<string> PlaySound;
        private readonly List<string> errors = new List<string>();
        private readonly HashSet<string> failedObservers = new HashSet<string>(StringComparer.Ordinal);
        private double gameplayTime, presentationTime;
        private long serial;
        private bool disposed, observing;
        private SceneActor actor;
        internal bool MembershipChanged { get; private set; }
        public bool Available { get { return !disposed; } }
        public long Generation { get; private set; }
        internal SceneActor Actor { get { return actor; } }
        internal SceneProperties Properties { get { return properties; } }
        internal Action FlagsChanged;
        internal void CancelEffect(long id) { Cancel(id); }
        internal void ClearOwner(string owner)
        { Check(); active.RemoveAll(e => e.Owner == owner); RefreshLights(); Recompose(); }
        internal SceneBehaviorEngine(SceneFile data, Action<object, string> onChanged)
        {
            scene = data; changed = onChanged ?? delegate { }; properties = new SceneProperties(data);
            templates = data.LightTemplates.ToDictionary(e => e.Id, StringComparer.Ordinal);
            authoredLights = data.Lights;
            foreach (FlagData flag in data.Flags) flags.Add(flag.Id, flag.Value);
            Generation = Interlocked.Increment(ref generations);
            InitializeTrees();
            InitializeRegions();
            foreach (EffectDefinition effect in data.Effects) preparedEffects.Add(effect.Id, PrepareEffect(effect));
        }
        private void CheckThread()
        { if (Thread.CurrentThread.ManagedThreadId != thread) throw new InvalidOperationException("Scene API requires the game thread"); }
        private void Check()
        { CheckThread(); if (disposed) throw new ObjectDisposedException("Mapping scene"); }
        internal static T Copy<T>(T value)
        {
            var serializer = new XmlSerializer(typeof(T));
            using (var text = new StringWriter(CultureInfo.InvariantCulture))
            { serializer.Serialize(text, value); using (var reader = new StringReader(text.ToString())) return (T)serializer.Deserialize(reader); }
        }
        private Change[] Prepare(EffectDefinition definition)
        {
            BehaviorValidation.Effect(definition, templates.Keys);
            var result = new List<Change>();
            foreach (SceneChange action in definition.Changes)
            {
                SceneProperty property = properties.Require(action);
                object value = property.Parse(action.Value, action.Mode);
                if (action.Mode != "set" && !(value is float) && !(value is int)) throw new InvalidDataException("Only numeric properties support add/multiply");
                if (property.Target is ScreenLook && property.Name == "ambientIntensity" && (action.Mode != "set" || definition.FadeIn > 0 || definition.FadeOut > 0))
                    throw new InvalidDataException("Screen ambientIntensity is an inheritance switch; use ambientScale for fades/add/multiply");
                var prop = property.Target as PropData;
                if (prop != null && property.Name == "motion" && (string)value == "path") SceneValidation.ParsePath(prop.Path, prop.Id);
                result.Add(new Change { Property = property, Value = value, Mode = action.Mode });
            }
            return result.ToArray();
        }
        public ISceneEffect Activate(string owner, string definitionId)
        {
            Check(); PreparedEffect effect;
            if (!preparedEffects.TryGetValue(definitionId ?? "", out effect)) throw new KeyNotFoundException("Unknown scene effect: " + definitionId);
            return StartEffect(owner, effect);
        }
        public ISceneEffect Apply(string owner, EffectDefinition definition)
        {
            Check();
            if (string.IsNullOrWhiteSpace(owner) || owner.Length > 128) throw new ArgumentException("Effect owner is required (1..128 characters)");
            if (definition == null) throw new ArgumentNullException("definition");
            return StartEffect(owner, PrepareEffect(definition));
        }
        private PreparedEffect PrepareEffect(EffectDefinition definition)
        {
            EffectDefinition copy = Copy(definition);
            Change[] changes = Prepare(copy);
            // Prepare every mutation/resource before replacing any existing effect.
            LightData[] lights = Array.ConvertAll(copy.Lights, s => {
                LightData light = templates[s.Template].Clone();
                if (!string.IsNullOrEmpty(s.Attach)) light.Attach = s.Attach;
                light.OffsetX += s.OffsetX; light.OffsetY += s.OffsetY;
                BehaviorValidation.Attachment(light.Attach, scene);
                return light;
            });
            return new PreparedEffect { Definition = copy, Changes = changes, Lights = lights };
        }
        private ISceneEffect StartEffect(string owner, PreparedEffect prepared)
        {
            if (string.IsNullOrWhiteSpace(owner) || owner.Length > 128) throw new ArgumentException("Effect owner is required (1..128 characters)");
            EffectDefinition copy = prepared.Definition; Change[] changes = prepared.Changes;
            var previous = active.Find(e => e.Owner == owner && e.Definition.Id == copy.Id);
            if (previous != null && copy.Repeat == "ignore") return new Lease(this, previous.Id);
            if (previous != null && copy.Repeat == "refresh")
            {
                // Refresh preserves the same instance and attachment, and restarts the fade envelope.
                previous.Started = Clock(previous.Definition); Recompose(); return new Lease(this, previous.Id);
            }
            var removed = active.FindAll(e => e.Owner == owner && ((copy.Repeat == "replace" && e.Definition.Id == copy.Id)
                || (!string.IsNullOrEmpty(copy.Group) && e.Definition.Group == copy.Group)));
            if (copy.Repeat == "stack" && active.Count(e => e.Owner == owner && e.Definition.Id == copy.Id) >= copy.MaxStacks)
                throw new InvalidOperationException("Effect stack limit reached: " + copy.Id);
            int retainedLights = active.Where(e => !removed.Contains(e)).Sum(e => e.Lights.Length);
            if (active.Count - removed.Count >= 128 || retainedLights + prepared.Lights.Length > 32)
                throw new InvalidOperationException("Scene live budget exceeded (128 effects / 32 spawned lights)");
            LightData[] lights = Array.ConvertAll(prepared.Lights, light => light.Clone());
            var effect = new ActiveEffect { Id = ++serial, Owner = owner, Definition = copy, Changes = changes, Lights = lights, Started = Clock(copy) };
            for (int i = 0; i < lights.Length; i++) lights[i].Id = "@effect:" + effect.Id + ":" + i;
            foreach (ActiveEffect old in removed) active.Remove(old);
            active.Add(effect);
            active.Sort((a, b) => {
                int order = a.Definition.Priority.CompareTo(b.Definition.Priority);
                if (order == 0) order = string.CompareOrdinal(a.Definition.Id, b.Definition.Id);
                if (order == 0) order = string.CompareOrdinal(a.Owner, b.Owner);
                return order == 0 ? a.Id.CompareTo(b.Id) : order;
            });
            foreach (Change change in changes) touched.Add(change.Property);
            RefreshLights(); Recompose();
            return new Lease(this, effect.Id);
        }
        private double Clock(EffectDefinition definition) { return definition.Clock == "presentation" ? presentationTime : gameplayTime; }
        private double Remaining(ActiveEffect effect)
        { return effect.Definition.Duration == 0 ? double.PositiveInfinity : Math.Max(0, effect.Definition.Duration - (Clock(effect.Definition) - effect.Started)); }
        private double Gain(ActiveEffect effect)
        {
            double age = Math.Max(0, Clock(effect.Definition) - effect.Started), gain = 1;
            if (effect.Definition.FadeIn > 0) gain = Math.Min(1, age / effect.Definition.FadeIn);
            if (effect.Definition.Duration > 0 && effect.Definition.FadeOut > 0) gain = Math.Min(gain, Remaining(effect) / effect.Definition.FadeOut);
            return Math.Max(0, gain);
        }
        private void Recompose()
        {
            foreach (SceneProperty property in touched)
            {
                object value = property.Baseline; string owner = "authored";
                foreach (ActiveEffect effect in active)
                    foreach (Change change in effect.Changes)
                        if (change.Property == property)
                        { double gain = Gain(effect); value = property.Blend(value, change.Value, change.Mode, gain); if (gain > 0) owner = effect.Owner + "/" + effect.Definition.Id; }
                property.Owner = owner;
                if (!Equals(property.Read(), value)) { property.Write(value); changed(property.Target, property.Name); }
            }
            foreach (ActiveEffect effect in active)
                for (int i = 0; i < effect.Lights.Length; i++) effect.Lights[i].Intensity = templates[effect.Definition.Lights[i].Template].Intensity * (float)Gain(effect);
        }
        private void RefreshLights()
        {
            var next = authoredLights.Concat(active.SelectMany(e => e.Lights)).ToArray();
            if (!scene.Lights.SequenceEqual(next)) { scene.Lights = next; MembershipChanged = true; }
        }
        private void Cancel(long id)
        { Check(); if (active.RemoveAll(e => e.Id == id) != 0) { RefreshLights(); Recompose(); } }
        internal void Tick(double delta, bool gameplay, bool presentation, SceneActor sample)
        {
            Check(); if (double.IsNaN(delta) || double.IsInfinity(delta) || delta < 0 || delta > 1) throw new ArgumentOutOfRangeException("delta");
            actor = sample;
            if (gameplay) gameplayTime += delta;
            if (presentation) presentationTime += delta;
            bool expired = active.RemoveAll(e => e.Definition.Duration > 0 && Remaining(e) <= 1e-9) != 0;
            if (expired) RefreshLights();
            if (expired || active.Exists(e => e.Definition.FadeIn > 0 || e.Definition.FadeOut > 0)) Recompose();
            if (gameplay) { ObserveRegions(delta); DrainEvents(); TickTrees(); }
        }
        internal void ConsumeMembershipChange() { MembershipChanged = false; }
        internal void ResetRun()
        {
            Check(); active.Clear(); events.Clear(); failedObservers.Clear(); gameplayTime = presentationTime = 0;
            foreach (FlagData flag in scene.Flags) flags[flag.Id] = flag.Value;
            ResetTrees();
            InitializeRegions(); Generation = Interlocked.Increment(ref generations); RefreshLights(); Recompose();
        }
        public string GetFlag(string id) { Check(); string value; if (!flags.TryGetValue(id ?? "", out value)) throw new KeyNotFoundException("Unknown scene flag: " + id); return value; }
        public void SetFlag(string id, string value)
        {
            Check(); GetFlag(id);
            if (value == null || value.Length > 256) throw new ArgumentException("Flag values must be 0..256 characters");
            NarrativeValidation.FlagValue(scene.Flags.First(f => f.Id == id), value);
            if (flags[id] == value) return;
            Emit("flag:" + id); flags[id] = value;
            if (FlagsChanged != null) FlagsChanged();
        }
        public void Emit(string eventId)
        { Emit(eventId, 0); }
        internal void Emit(string eventId, int eventScreen)
        { Check(); if (string.IsNullOrWhiteSpace(eventId) || eventId.Length > 128) throw new ArgumentException("Event ID required (1..128 characters)"); if (events.Count >= 256) throw new InvalidOperationException("Scene event queue limit exceeded"); events.Enqueue(new SceneEvent { Name = eventId, Screen = eventScreen }); }
        public EffectInfo[] InspectEffects()
        { Check(); return active.Select(e => new EffectInfo(e.Id, e.Definition.Id, e.Owner, Remaining(e))).ToArray(); }
        public string[] InspectErrors() { Check(); return errors.ToArray(); }
        public ScenePropertyInfo[] DescribeProperties(string objectId)
        {
            Check(); var selected = properties.Values.Values.Where(p => p.Id == objectId).ToArray();
            if (selected.Length == 0) throw new KeyNotFoundException("Unknown scene object: " + objectId);
            return selected.Select(p => new ScenePropertyInfo(p.Name, p.Color ? "color" : p.Member.PropertyType == typeof(string) ? "choice" : p.Member.PropertyType == typeof(bool) ? "boolean" : p.Member.PropertyType == typeof(int) ? "integer" : "number", p.Min, p.Max, p.Choices)).ToArray();
        }
        private void RecordError(string owner, Exception error)
        { if (errors.Count == 64) errors.RemoveAt(0); errors.Add(owner + ": " + error.GetBaseException().Message); failedObservers.Add(owner); }
        public SceneObjectInfo[] InspectObjects()
        {
            Check(); return properties.Values.Values.GroupBy(p => p.Id).Select(group => {
                SceneProperty first = group.First(); PropertyInfoScreen screen = new PropertyInfoScreen(first.Target);
                return new SceneObjectInfo(group.Key, first.Target.GetType().Name.Replace("Data", ""), screen.Value,
                    group.Select(p => p.Name).ToArray(), group.Select(p => SceneProperty.Format(p.Read())).ToArray(), group.Select(p => p.Owner).ToArray());
            }).ToArray();
        }
        private struct PropertyInfoScreen
        { internal int Value; internal PropertyInfoScreen(object value) { var property = value.GetType().GetProperty("Screen"); Value = property == null ? 0 : (int)property.GetValue(value, null); } }
        public void Dispose()
        {
            CheckThread(); if (disposed) return;
            active.Clear(); RefreshLights(); Recompose(); events.Clear(); disposed = true;
        }
    }
}
