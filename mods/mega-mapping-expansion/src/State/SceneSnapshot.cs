using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MegaMappingExpansion.Api;

namespace MegaMappingExpansion.Construction
{
    internal sealed class ProgramSnapshot
    {
        internal SceneProgram Owner;
        internal GraphSnapshot Graph;
        internal Dictionary<string, SceneValue> Variables;
        internal Dictionary<string, RuleProgress> Rules;
        internal EffectInstance[] Effects;
        internal SceneEventSample[] Events;
        internal double Gameplay, Presentation;
        internal long Serial;
        internal JKRuntime.World.WorldTreeState[] Trees;
        internal SceneEventSample[] Causes;
        internal Dictionary<string,double[]> Fields;
        internal SensorSnapshot Sensors;
        internal SignalSample[] Observations;
    }
    internal sealed partial class SceneProgram
    {
        internal ProgramSnapshot Capture()
        {
            return new ProgramSnapshot { Owner=this, Graph=Graph.Capture(), Variables=variableDefinitions.Keys.ToDictionary(id => id, ReadVariable, StringComparer.Ordinal),
                Rules=ruleProgress.ToDictionary(p => p.Key, p => new RuleProgress { Fired=p.Value.Fired, Faulted=p.Value.Faulted, Last=p.Value.Last }, StringComparer.Ordinal),
                Effects=activeEffects.Select(e => new EffectInstance { Id=e.Id, Owner=e.Owner, Definition=e.Definition, Started=e.Started }).ToArray(),
                Events=Signals.CaptureEvents(), Gameplay=gameplayTime, Presentation=presentationTime, Serial=effectSerial,
                Trees=Array.ConvertAll(trees,t=>t.State.Clone()), Causes=Array.ConvertAll(trees,t=>t.Cause),Fields=Fields.Capture(),Sensors=Sensors.Capture(),Observations=Signals.CaptureObservations() };
        }
        internal void Validate(ProgramSnapshot state)
        {
            if (state == null || state.Owner != this || state.Variables == null || state.Variables.Count != variableDefinitions.Count || state.Rules == null || state.Rules.Count != ruleProgress.Count
                || state.Effects == null || state.Effects.Length > 256 || state.Events == null || state.Events.Length > 256 || state.Events.Any(e => e == null)
                || state.Trees==null || state.Causes==null || state.Trees.Length!=trees.Length || state.Causes.Length!=trees.Length
                || !SceneValue.Finite(state.Gameplay) || !SceneValue.Finite(state.Presentation) || state.Gameplay < 0 || state.Presentation < 0 || state.Serial < 0)
                throw new InvalidDataException("Scene snapshot belongs to another scene or has invalid state");
            Graph.Validate(state.Graph);
            Fields.Validate(state.Fields);
            Sensors.Validate(state.Sensors);
            Signals.ValidateObservations(state.Observations);
            foreach (var variable in state.Variables)
                if (!variableDefinitions.ContainsKey(variable.Key) || variable.Value.Kind != variableDefinitions[variable.Key].Kind) throw new InvalidDataException("Snapshot variable differs from its declaration");
            foreach (var rule in state.Rules) if (!ruleProgress.ContainsKey(rule.Key) || rule.Value == null || !SceneValue.Finite(rule.Value.Last)) throw new InvalidDataException("Snapshot rule differs from its declaration");
            var ids = new HashSet<long>();
            foreach (var effect in state.Effects)
                if (effect == null || effect.Id < 1 || effect.Id > state.Serial || !ids.Add(effect.Id) || string.IsNullOrWhiteSpace(effect.Owner) || !effects.ContainsKey(effect.Definition)
                    || !SceneValue.Finite(effect.Started) || effect.Started < 0 || effect.Started > (effects[effect.Definition].Clock == SceneClock.Gameplay ? state.Gameplay : state.Presentation))
                    throw new InvalidDataException("Invalid active effect snapshot");
            for(int i=0;i<trees.Length;i++)
            {
                var tree=state.Trees[i];
                if(tree==null || !Enum.IsDefined(typeof(JKRuntime.World.WorldTreeResult),tree.Result)
                    || (tree.Result==JKRuntime.World.WorldTreeResult.Running && (tree.Nodes==null || tree.Nodes.Length!=trees[i].Code.Length || state.Causes[i]==null))
                    || (tree.Result!=JKRuntime.World.WorldTreeResult.Running && tree.Nodes!=null))throw new InvalidDataException("Invalid control tree snapshot");
            }
        }
        internal void Restore(ProgramSnapshot state)
        {
            using(Signals.Batch())RestoreValues(state);
        }
        private void RestoreValues(ProgramSnapshot state)
        {
            Validate(state);
            Signals.AdvanceGeneration();
            foreach (var effect in activeEffects.ToArray()) Cancel(effect.Id);
            Graph.Restore(state.Graph);Properties.RestoreBindings(); gameplayTime=state.Gameplay; presentationTime=state.Presentation; effectSerial=state.Serial;
            PublishClocks();
            Fields.Restore(state.Fields);
            Sensors.Restore(state.Sensors);
            Signals.RestoreObservations(state.Observations);
            foreach (var variable in state.Variables) Signals.Publish("variable:" + variable.Key, variable.Value, ObservationQuality.Exact);
            foreach (var rule in state.Rules) { var progress=ruleProgress[rule.Key]; progress.Fired=rule.Value.Fired; progress.Faulted=rule.Value.Faulted; progress.Last=rule.Value.Last; }
            activeEffects.AddRange(state.Effects.Select(e => new EffectInstance { Id=e.Id, Owner=e.Owner, Definition=e.Definition, Started=e.Started }));
            for(int i=0;i<trees.Length;i++){trees[i].State=state.Trees[i].Clone();trees[i].Cause=state.Causes[i];}
            Signals.RestoreEvents(state.Events); UpdateEffects(); Properties.Compose();ObserveText(false);
            if (VariablesChanged != null) VariablesChanged();
        }
        internal Dictionary<string, string> SavedVariables()
        { return variableDefinitions.Values.Where(v => v.Scope == SceneStateScope.Save).ToDictionary(v => v.Id, v => ReadVariable(v.Id).ToString(), StringComparer.Ordinal); }
        internal void LoadVariables(Dictionary<string, string> saved)
        {
            var values = new Dictionary<string, SceneValue>(StringComparer.Ordinal);
            foreach (var pair in saved)
            {
                VariableData definition;
                if (variableDefinitions.TryGetValue(pair.Key, out definition) && definition.Scope == SceneStateScope.Save) values.Add(pair.Key, SceneValue.Parse(definition.Kind, pair.Value));
            }
            foreach (var pair in values) Signals.Publish("variable:" + pair.Key, pair.Value, ObservationQuality.Exact);
            graphDirty=true;
        }
    }
}
