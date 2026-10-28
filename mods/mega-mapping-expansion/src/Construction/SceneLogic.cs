using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MegaMappingExpansion.Api;

namespace MegaMappingExpansion.Construction
{
    internal sealed class EffectInstance
    {
        internal long Id;
        internal string Owner, Definition;
        internal double Started;
    }
    internal sealed class RuleProgress
    {
        internal bool Fired, Faulted;
        internal double Last = -1e20;
    }
    internal sealed class CompiledOverride
    {
        internal string Id,Target,Source,Mode;
        internal SceneValue Literal;
    }
    internal sealed class NativeSceneAction
    {
        internal string Operation, Target;
        internal SceneValue? Value;
        internal SceneEventSample Event;
    }
    internal sealed partial class SceneProgram
    {
        private readonly Dictionary<string, EffectData> effects = new Dictionary<string, EffectData>(StringComparer.Ordinal);
        private readonly Dictionary<string,CompiledOverride[]> effectValues=new Dictionary<string,CompiledOverride[]>(StringComparer.Ordinal);
        private readonly List<EffectInstance> activeEffects = new List<EffectInstance>();
        private readonly Dictionary<string, VariableData> variableDefinitions = new Dictionary<string, VariableData>(StringComparer.Ordinal);
        private readonly Dictionary<string, RuleProgress> ruleProgress = new Dictionary<string, RuleProgress>(StringComparer.Ordinal);
        private readonly List<IDisposable> ruleLeases = new List<IDisposable>();
        private readonly List<string> logicErrors = new List<string>();
        private long effectSerial;
        private double gameplayTime, presentationTime;
        private bool graphDirty;
        internal Action<NativeSceneAction> InvokeNative;
        internal Action VariablesChanged;
        internal double GameplayTime { get { return gameplayTime; } }
        internal double PresentationTime { get { return presentationTime; } }
        internal string[] Errors { get { return logicErrors.Concat(Signals.Errors).Concat(Graph.Errors).Take(64).ToArray(); } }
        internal void ReportError(string id,Exception error){if(logicErrors.Count==64)logicErrors.RemoveAt(0);logicErrors.Add(id+": "+error.GetBaseException().Message);}
        private void PublishClocks(){Signals.Publish("time.gameplay",SceneValue.Number(gameplayTime),ObservationQuality.Exact);Signals.Publish("time.presentation",SceneValue.Number(presentationTime),ObservationQuality.Exact);}
        private void PrepareLogic()
        {
            foreach (var variable in Definition.Variables) variableDefinitions.Add(variable.Id, variable);
            foreach (var effect in Definition.Effects)
            {
                if (effect == null || string.IsNullOrWhiteSpace(effect.Id) || effects.ContainsKey(effect.Id) || effect.Values == null || effect.Values.Length > 128
                    || !SceneValue.Finite(effect.Duration) || effect.Duration < 0 || effect.Duration > 86400 || !SceneValue.Finite(effect.FadeIn) || effect.FadeIn < 0
                    || !SceneValue.Finite(effect.FadeOut) || effect.FadeOut < 0 || (effect.Duration == 0 ? effect.FadeOut != 0 : effect.FadeIn + effect.FadeOut > effect.Duration)
                    || (effect.Repeat != "refresh" && effect.Repeat != "replace" && effect.Repeat != "ignore" && effect.Repeat != "stack") || !Enum.IsDefined(typeof(SceneClock), effect.Clock)) throw new InvalidDataException("Invalid scene effect");
                foreach (var value in effect.Values)
                {
                    if (value == null) throw new InvalidDataException(effect.Id + ": null property override");
                    var field = Properties.Require(value.Target).Descriptor;
                    var typed = string.IsNullOrEmpty(value.Source) ? field.Parse(value.Value) : ValueGraph.Zero(Graph.Describe(value.Source).Kind);
                    Properties.Validate(new PropertyContribution { Id=effect.Id, Owner="validation", Target=value.Target, Mode=value.Mode, Value=typed });
                }
                effects.Add(effect.Id, effect);
                effectValues.Add(effect.Id,effect.Values.Select((v,i)=>new CompiledOverride{Id=i.ToString(System.Globalization.CultureInfo.InvariantCulture),Target=v.Target,Source=v.Source,Mode=v.Mode,Literal=string.IsNullOrEmpty(v.Source)?Properties.Require(v.Target).Descriptor.Parse(v.Value):ValueGraph.Zero(Graph.Describe(v.Source).Kind)}).ToArray());
            }
            if (Definition.Effects.Length > 512 || Definition.Rules.Length > 512) throw new InvalidDataException("Scene supports at most 512 effects and rules");
            foreach (var rule in Definition.Rules.OrderBy(r => r.Id, StringComparer.Ordinal))
            {
                if (rule == null || string.IsNullOrWhiteSpace(rule.Id) || ruleProgress.ContainsKey(rule.Id) || string.IsNullOrWhiteSpace(rule.Event) || rule.Event.Length > 128
                    || !Enum.IsDefined(typeof(SceneClock),rule.Clock) || !SceneValue.Finite(rule.Cooldown) || rule.Cooldown < 0 || rule.Cooldown > 86400 || rule.Actions == null || rule.Actions.Length > 128) throw new InvalidDataException("Invalid scene rule");
                if (!string.IsNullOrEmpty(rule.Condition) && Graph.Describe(rule.Condition).Kind != SceneValueKind.Boolean) throw new InvalidDataException(rule.Id + ": condition must be boolean");
                foreach (var action in rule.Actions) ValidateAction(action);
                var progress = new RuleProgress(); ruleProgress.Add(rule.Id, progress); var current = rule;
                ruleLeases.Add(Signals.SubscribeEvent(rule.Event, sample => RunRule(current, progress, sample)));
            }
            PrepareTrees();
        }
        internal SceneValue ReadVariable(string id)
        { if (!variableDefinitions.ContainsKey(id)) throw new InvalidDataException("Unknown scene variable: " + id); return Signals.Read("variable:" + id).Value; }
        internal void SetVariable(string id, SceneValue value, SceneEventSample cause = null)
        {
            VariableData definition; if (!variableDefinitions.TryGetValue(id, out definition) || definition.Kind != value.Kind) throw new InvalidDataException("Unknown variable or wrong type: " + id);
            if (ReadVariable(id).Equals(value)) return;
            if (cause != null) Signals.Emit("variable:" + id, id, cause.Actor, cause.Screen, gameplayTime, new SignalSample("value", value, ObservationQuality.Exact, cause.Sequence));
            Signals.Publish("variable:" + id, value, ObservationQuality.Exact);graphDirty=true; if (VariablesChanged != null) VariablesChanged();
        }
        private void ValidateAction(ActionData action)
        {
            if (action == null || string.IsNullOrWhiteSpace(action.Target)) throw new InvalidDataException("Action requires a target");
            switch (action.Operation)
            {
                case "set": case "increment":
                    var current = ReadVariable(action.Target); SceneValueKind kind = string.IsNullOrEmpty(action.Source) ? current.Kind : Graph.Describe(action.Source).Kind;
                    if (kind != current.Kind) throw new InvalidDataException(action.Target + ": action source type differs from variable");
                    if (action.Operation == "increment" && kind != SceneValueKind.Integer && kind != SceneValueKind.Number) throw new InvalidDataException("Increment requires a numeric variable");
                    if (string.IsNullOrEmpty(action.Source)) SceneValue.Parse(kind, action.Value); break;
                case "effect": case "cancel": if (!effects.ContainsKey(action.Target)) throw new InvalidDataException("Unknown action effect: " + action.Target); break;
                case "stamp": case "burst":
                    CompiledComponent component;
                    if (!Components.TryGetValue(action.Target, out component) || component.Descriptor.Kind != (action.Operation == "stamp" ? "stamp" : "emitter")) throw new InvalidDataException("Action target has the wrong component kind");
                    SceneValueKind nativeKind=action.Operation=="stamp"?SceneValueKind.Vector2:SceneValueKind.Number;
                    if (!string.IsNullOrEmpty(action.Source)){if(Graph.Describe(action.Source).Kind!=nativeKind)throw new InvalidDataException(action.Target+": native action source type differs");}
                    else if(!string.IsNullOrEmpty(action.Value))SceneValue.Parse(nativeKind,action.Value);break;
                case "impulse":
                    if(!Fields.IsSpring(action.Target))throw new InvalidDataException(action.Target+": impulse requires a spring field");
                    if(!string.IsNullOrEmpty(action.Source)){if(Graph.Describe(action.Source).Kind!=SceneValueKind.Number)throw new InvalidDataException("Impulse force must be a number");}
                    else SceneValue.Parse(SceneValueKind.Number,action.Value);break;
                case "sound": if (action.Target.Length > 128 || action.Target.IndexOfAny(new[] { '/', '\\', ':', '\0' }) >= 0) throw new InvalidDataException("Sound action requires a native event_music key"); break;
                case "playAudio":
                    CompiledComponent audio;
                    if(!Components.TryGetValue(action.Target,out audio)||audio.Descriptor.Kind!="audio")throw new InvalidDataException("playAudio requires an audio component");break;
                case "capture":case "clearCapture":
                    CompiledComponent capture;
                    if(!Components.TryGetValue(action.Target,out capture)||capture.Descriptor.Kind!="capture"||Properties.Read(action.Target,"mode").Text!="hold"
                        ||!string.IsNullOrEmpty(action.Source)||!string.IsNullOrEmpty(action.Value))throw new InvalidDataException("Capture actions require a hold capture target and no value");break;
                case "emit": if (action.Target.Length > 128) throw new InvalidDataException("Event action ID exceeds 128 characters"); break;
                case "platformStart":case "platformStop":case "platformReverse":case "platformReset":
                    if(action.Target.Length>96||!string.IsNullOrEmpty(action.Source)||!string.IsNullOrEmpty(action.Value))throw new InvalidDataException("Platform actions require only a platform or group target");break;
                default: throw new InvalidDataException("Unknown scene action: " + action.Operation);
            }
        }
        private void RunRule(ReactionData rule, RuleProgress state, SceneEventSample sample)
        {
            RefreshGraph();
            double time=Clock(rule.Clock);
            if (state.Faulted || (state.Fired && rule.Once) || time - state.Last < rule.Cooldown || (!string.IsNullOrEmpty(rule.Condition) && (!Graph.Available(rule.Condition)||Graph.Read(rule.Condition).X == 0))) return;
            try
            {
                foreach (var action in rule.Actions) Execute(action, "rule:" + rule.Id, sample);
                state.Fired = true; state.Last = time;
            }
            catch (Exception error)
            {
                state.Faulted = true; CancelOwner("rule:" + rule.Id);
                if (logicErrors.Count == 64) logicErrors.RemoveAt(0); logicErrors.Add(rule.Id + ": " + error.GetBaseException().Message);
            }
        }
        internal void Execute(ActionData action, string owner, SceneEventSample cause)
        {
            RefreshGraph();
            switch (action.Operation)
            {
                case "set": case "increment":
                    var previous = ReadVariable(action.Target);
                    if(!string.IsNullOrEmpty(action.Source)&&!Graph.Available(action.Source))throw new InvalidOperationException("Action source is unavailable: "+action.Source);
                    var value = string.IsNullOrEmpty(action.Source) ? SceneValue.Parse(previous.Kind, action.Value) : Graph.Read(action.Source);
                    if (action.Operation == "increment") value = previous.Kind == SceneValueKind.Integer ? SceneValue.Integer(checked((int)previous.X + (int)value.X)) : SceneValue.Number(previous.X + value.X);
                    SetVariable(action.Target, value, cause); break;
                case "effect": Activate(owner, action.Target); break;
                case "cancel": foreach (var effect in activeEffects.Where(e => e.Owner == owner && e.Definition == action.Target).ToArray()) Cancel(effect.Id); break;
                case "emit": Signals.Emit(action.Target, owner, cause.Actor, cause.Screen, gameplayTime, cause.Payload); break;
                case "platformStart":case "platformStop":case "platformReverse":case "platformReset":
                    if(NativeMovingPlatforms.Current==null)throw new InvalidOperationException("No moving platforms are active");
                    NativeMovingPlatforms.Current.Command(action.Target,action.Operation.Substring(8).ToLowerInvariant());break;
                case "impulse":
                    if(!string.IsNullOrEmpty(action.Source)&&!Graph.Available(action.Source))throw new InvalidOperationException("Impulse source is unavailable: "+action.Source);
                    var point=cause.Payload.FirstOrDefault(p=>p.Id=="position"&&p.Value.Kind==SceneValueKind.Vector2);
                    Fields.Impulse(action.Target,point==null?Properties.Read(action.Target,"origin"):point.Value,string.IsNullOrEmpty(action.Source)?SceneValue.Parse(SceneValueKind.Number,action.Value).X:Graph.Read(action.Source).X);break;
                default:
                    if (InvokeNative == null) throw new InvalidOperationException("Native scene action service is unavailable");
                    if(!string.IsNullOrEmpty(action.Source)&&!Graph.Available(action.Source))throw new InvalidOperationException("Native action source is unavailable: "+action.Source);
                    SceneValue? nativeValue=string.IsNullOrEmpty(action.Source)?(string.IsNullOrEmpty(action.Value)?(SceneValue?)null:SceneValue.Parse(action.Operation=="stamp"?SceneValueKind.Vector2:SceneValueKind.Number,action.Value)):Graph.Read(action.Source);
                    InvokeNative(new NativeSceneAction { Operation=action.Operation, Target=action.Target, Value=nativeValue, Event=cause }); break;
            }
        }
        internal long Activate(string owner, string id)
        {
            EffectData definition; if (string.IsNullOrWhiteSpace(owner) || owner.Length > 128 || !effects.TryGetValue(id, out definition)) throw new InvalidDataException("Effect requires a valid owner and definition");
            var old = activeEffects.FirstOrDefault(e => e.Owner == owner && e.Definition == id);
            if (old != null && (definition.Repeat == "ignore" || definition.Repeat == "refresh"))
            { if (definition.Repeat == "refresh") old.Started = Clock(definition.Clock); return old.Id; }
            if (definition.Repeat == "replace" || !string.IsNullOrEmpty(definition.Group))
                foreach (var effect in activeEffects.Where(e => e.Owner == owner && (e.Definition == id || (!string.IsNullOrEmpty(definition.Group) && effects[e.Definition].Group == definition.Group))).ToArray()) Cancel(effect.Id);
            if (activeEffects.Count >= 256 || activeEffects.Count(e => e.Owner == owner && e.Definition == id) >= 32) throw new InvalidOperationException("Active effect budget exceeded");
            var instance = new EffectInstance { Id=++effectSerial, Owner=owner, Definition=id, Started=Clock(definition.Clock) }; activeEffects.Add(instance); return instance.Id;
        }
        private double Clock(SceneClock clock) { return clock == SceneClock.Gameplay ? gameplayTime : presentationTime; }
        private void RefreshGraph(){if(!graphDirty)return;Graph.Tick(0,0);graphDirty=false;}
        internal bool EffectActive(long id) { return activeEffects.Any(e => e.Id == id); }
        internal double Remaining(long id)
        { var effect = activeEffects.FirstOrDefault(e => e.Id == id); if (effect == null) return 0; var definition = effects[effect.Definition]; return definition.Duration == 0 ? double.PositiveInfinity : Math.Max(0, definition.Duration - (Clock(definition.Clock) - effect.Started)); }
        internal void Cancel(long id) { activeEffects.RemoveAll(e => e.Id == id); Properties.RemoveOwner("effect:" + id); }
        internal void CancelOwner(string owner) { foreach (var effect in activeEffects.Where(e => e.Owner == owner).ToArray()) Cancel(effect.Id); }
        private void UpdateEffects()
        {
            for(int effectIndex=activeEffects.Count-1;effectIndex>=0;effectIndex--)
            {
                var effect=activeEffects[effectIndex];
                var definition = effects[effect.Definition]; double age = Math.Max(0, Clock(definition.Clock) - effect.Started);
                if (definition.Duration > 0 && age >= definition.Duration) { Cancel(effect.Id); continue; }
                double gain = definition.FadeIn > 0 ? Math.Min(1, age / definition.FadeIn) : 1;
                if (definition.FadeOut > 0) gain = Math.Min(gain, Math.Max(0, (definition.Duration - age) / definition.FadeOut));
                var overrides=effectValues[effect.Definition];
                for (int i = 0; i < overrides.Length; i++)
                {
                    var value = overrides[i];
                    if(!string.IsNullOrEmpty(value.Source)&&!Graph.Available(value.Source))continue;
                    Properties.Set(new PropertyContribution { Id=value.Id, Owner="effect:" + effect.Id, Target=value.Target,
                        Mode=value.Mode, Priority=definition.Priority, Value=string.IsNullOrEmpty(value.Source) ? value.Literal : Graph.Read(value.Source), Gain=gain });
                }
            }
        }
    }
}
