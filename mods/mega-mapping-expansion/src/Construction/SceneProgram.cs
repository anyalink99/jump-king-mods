using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MegaMappingExpansion.Api;

namespace MegaMappingExpansion.Construction
{
    internal sealed partial class SceneProgram : IDisposable
    {
        internal readonly SceneDocument Definition;
        internal readonly ComponentRegistry Registry;
        internal readonly SignalHub Signals;
        internal readonly ValueGraph Graph;
        internal readonly PropertyStore Properties;
        internal readonly FieldSystem Fields;
        internal readonly TransformSystem ObservationPoses;
        internal readonly SensorSystem Sensors;
        internal readonly Dictionary<string, CompiledComponent> Components;
        internal long Revision{get{return Signals.Generation;}}
        internal SceneProgram(SceneDocument definition)
        {
            Definition = definition=SceneDocument.Copy(definition); Registry = PrimitiveRegistry.Create(); Signals = new SignalHub();
            try
            {
                if (definition.Version != 1 || definition.Components == null || definition.Components.Length > 16384) throw new InvalidDataException("Scene supports at most 16384 components");
                if(definition.Variables==null||definition.Variables.Length>256||definition.SaveId!=null&&definition.SaveId.Length>128)throw new InvalidDataException("Scene supports at most 256 variables and a saveId of at most 128 characters");
                var compiled = definition.Components.Select(Registry.Compile).ToArray(); Components = new Dictionary<string, CompiledComponent>(StringComparer.Ordinal);
                foreach (var component in compiled) if (Components.ContainsKey(component.Id)) throw new InvalidDataException("Duplicate component: " + component.Id); else Components.Add(component.Id, component);
                DeclareObservations(Signals);
                NativeDescriptors.Declare(this);
                foreach (var component in compiled) for (int i = 0; i < component.Descriptor.Fields.Length; i++)
                {
                    var field = component.Descriptor.Fields[i]; var value = component.Baseline[i];
                    Signals.Declare(new SignalDescriptor(component.Id + "/" + field.Name, field.Kind, field.Units), value);
                }
                foreach(var component in compiled)if(component.Descriptor.Kind=="sensor")SensorSystem.Declare(Signals,component.Id);
                var variables = new HashSet<string>(StringComparer.Ordinal);
                foreach (var variable in definition.Variables)
                {
                    if (string.IsNullOrWhiteSpace(variable.Id) || variable.Id.Length>128 || !variables.Add(variable.Id) || !Enum.IsDefined(typeof(SceneStateScope), variable.Scope) || variable.Scope==SceneStateScope.Transient) throw new InvalidDataException("Variable requires a unique ID of 1..128 characters and Attempt or Save lifetime");
                    var value = SceneValue.Parse(variable.Kind, variable.Value);
                    Signals.Declare(new SignalDescriptor("variable:" + variable.Id, value.Kind), value); Signals.Publish("variable:" + variable.Id, value, ObservationQuality.Exact);
                    if (variable.Scope == SceneStateScope.Save && string.IsNullOrWhiteSpace(definition.SaveId)) throw new InvalidDataException("Saved variables require saveId");
                }
                Graph = new ValueGraph(definition.Graph, Signals); Properties = new PropertyStore(compiled, Graph, definition.Bindings);
                Properties.Changed=(id,property,impact)=>Signals.Publish(id+"/"+property,Properties.Read(id,property),ObservationQuality.Sampled);
                foreach(var pair in Properties.All)Signals.Publish(pair.Key,pair.Value.Value,ObservationQuality.Sampled);
                ValidateReferences(definition);
                NativeDescriptors.Validate(this);
                Fields=new FieldSystem(this);
                ObservationPoses=new TransformSystem(this);Sensors=new SensorSystem(this,ObservationPoses);
                PrepareLogic();
                PrepareWorldLayout();
            }
            catch { Signals.Dispose(); throw; }
        }
        private void ValidateReferences(SceneDocument document)
        {
            var assets = new HashSet<string>(StringComparer.Ordinal);
            foreach (var asset in document.Assets)
                if (asset == null || string.IsNullOrWhiteSpace(asset.Id) || !assets.Add(asset.Id) || (asset.Kind != "png" && asset.Kind != "vector") || string.IsNullOrWhiteSpace(asset.Path)
                    || asset.Columns < 1 || asset.Rows < 1 || asset.Columns > 256 || asset.Rows > 256 || asset.Frames < 0 || asset.Frames > asset.Columns * asset.Rows
                    || !SceneValue.Finite(asset.Fps) || asset.Fps < 0 || asset.Fps > 240) throw new InvalidDataException("Invalid or duplicate scene asset");
            foreach (var pair in Properties.All)
            {
                string value = pair.Value.Value.Text; var field = pair.Value.Descriptor; if (value == "") continue;
                if (field.Reference == ReferenceKind.Component && !Components.ContainsKey(value)) throw new InvalidDataException(pair.Key + ": unknown component " + value);
                if (field.Reference == ReferenceKind.Asset && !assets.Contains(value)) throw new InvalidDataException(pair.Key + ": unknown asset " + value);
                if (field.Reference == ReferenceKind.Signal) Signals.Describe(value);
            }
            foreach (var component in Components.Values)
            {
                foreach (var pair in new[] { new[] { "transform", "transform" }, new[] { "material", "material" }, new[] { "deformation", "deformation" }, new[] { "capture", "capture" } })
                {
                    if (!component.Descriptor.Fields.Any(f => f.Name == pair[0])) continue;
                    string id = Properties.Read(component.Id, pair[0]).Text;
                    if (id != "" && Components[id].Descriptor.Kind != pair[1]) throw new InvalidDataException(component.Id + ": " + pair[0] + " requires a " + pair[1] + " component");
                }
                if (component.Descriptor.Kind == "transform")
                {
                    var seen = new HashSet<string>(StringComparer.Ordinal); string current = component.Id;
                    while (current != "")
                    {
                        if (!seen.Add(current) || seen.Count > 64) throw new InvalidDataException(component.Id + ": cyclic or excessively deep transform hierarchy");
                        if (Components[current].Descriptor.Kind != "transform") throw new InvalidDataException(component.Id + ": parent must be a transform");
                        current = Properties.Read(current, "parent").Text;
                    }
                }
            }
            var exportIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var export in document.Exports)
            {
                if (string.IsNullOrWhiteSpace(export.Id) || !exportIds.Add(export.Id)) throw new InvalidDataException("Export requires a unique ID");
                if (export.Role == "component") { if (!Components.ContainsKey(export.Target)) throw new InvalidDataException(export.Id + ": missing exported component"); }
                else if (export.Role == "node") Graph.Describe(export.Target);
                else if (export.Role == "property") Properties.Require(export.Target);
                else if (export.Role == "signal") Signals.Describe(export.Target);
                else throw new InvalidDataException(export.Id + ": unsupported export role " + export.Role);
            }
        }
        internal void ComposeCurrentValues()
        {Graph.RefreshPure();graphDirty=false;UpdateEffects();Properties.Compose();}
        internal void Tick(double gameplayDelta, double presentationDelta,bool presentationLogic=false)
        {
            using(Signals.Batch())TickFrame(gameplayDelta,presentationDelta,presentationLogic);
        }
        private void TickFrame(double gameplayDelta, double presentationDelta,bool presentationLogic)
        {
            if(!SceneValue.Finite(gameplayDelta)||!SceneValue.Finite(presentationDelta)||gameplayDelta<0||presentationDelta<0||gameplayDelta>1||presentationDelta>1)throw new ArgumentOutOfRangeException("Scene deltas must be finite seconds in 0..1");
            gameplayTime += gameplayDelta; presentationTime += presentationDelta;
            Signals.Publish("time.gameplay", SceneValue.Number(gameplayTime), ObservationQuality.Exact);
            Signals.Publish("time.presentation", SceneValue.Number(presentationTime), ObservationQuality.Exact);
            Graph.Tick(gameplayDelta, presentationDelta);
            graphDirty=false;
            if (gameplayDelta > 0 || presentationLogic&&presentationDelta>0) { Signals.Dispatch(); TickControlTrees(gameplayDelta,presentationDelta); }
            Fields.Tick(gameplayDelta,presentationDelta);
            RefreshGraph();
            UpdateEffects(); Properties.Compose();ObserveText(gameplayDelta>0||presentationLogic&&presentationDelta>0);
        }
        internal static void DeclareObservations(SignalHub signals)
        {
            foreach (string id in new[] { "player.position", "player.center", "player.feet", "player.velocity" })
                signals.Declare(new SignalDescriptor(id, SceneValueKind.Vector2, id == "player.velocity" ? "px/s" : "px",true), SceneValue.Vector(0, 0));
            foreach (string id in new[] { "player.grounded", "player.facingLeft", "player.present", "charge.active", "paused", "scene.completed" }) signals.Declare(new SignalDescriptor(id, SceneValueKind.Boolean,observation:true), SceneValue.Boolean(false));
            signals.Declare(new SignalDescriptor("charge.nativeTimerSeconds",SceneValueKind.Number,"seconds",true),SceneValue.Number(0));
            signals.Declare(new SignalDescriptor("player.screen", SceneValueKind.Integer, "screen",true), SceneValue.Integer(1));
            foreach (string id in new[] { "charge.heldFrames", "charge.releasedFrames", "charge.maximumFrames" }) signals.Declare(new SignalDescriptor(id, SceneValueKind.Number, "F",true), SceneValue.Number(0));
            signals.Declare(new SignalDescriptor("charge.elapsedMilliseconds", SceneValueKind.Number, "ms",true), SceneValue.Number(0));
            signals.Declare(new SignalDescriptor("time.gameplay", SceneValueKind.Number, "seconds"), SceneValue.Number(0));
            signals.Declare(new SignalDescriptor("time.presentation", SceneValueKind.Number, "seconds"), SceneValue.Number(0));
            signals.Declare(new SignalDescriptor("scene.phase",SceneValueKind.Text,observation:true),SceneValue.String("playing"));
            signals.Declare(new SignalDescriptor("language.culture",SceneValueKind.Text,observation:true),SceneValue.String(""));
            NativeActorObservations.Declare(signals);
            signals.Declare(new SignalDescriptor("ending.arrival",SceneValueKind.Vector2,"px",true),SceneValue.Vector(0,0));
            PlayerObservations.Declare(signals);
        }
        public void Dispose() { foreach (var lease in ruleLeases) lease.Dispose(); ruleLeases.Clear(); Signals.Dispose(); }
    }
}
