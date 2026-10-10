using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using JKRuntime.World;

namespace MegaMappingExpansion
{
    internal sealed partial class SceneBehaviorEngine
    {
        private sealed class WorldRegion
        {
            [WorldField] public bool Initialized,Inside,Fired;
            [WorldField] public double Dwell;
            [WorldField] public long Effect;
        }
        private sealed class WorldRule {[WorldField] public bool Fired;[WorldField] public double Last;}
        private sealed class WorldEvent {[WorldField] public string Name;[WorldField] public int Screen;}
        private sealed class WorldEffectState
        {
            [WorldField] public long Id;
            [WorldField] public string Owner;
            [WorldField] public string[] Definition;
            [WorldField] public double Started;
        }
        private sealed class WorldScene
        {
            [WorldField] public string Layout;
            [WorldField] public double Gameplay,Presentation;
            [WorldField] public WorldTreeState[] Trees;
            [WorldField] public WorldRegion[] Regions;
            [WorldField] public WorldRule[] Rules;
            [WorldField] public Dictionary<string,string> Flags;
            [WorldField] public WorldEvent[] Events;
            [WorldField] public string[] Failed;
            [WorldField] public WorldEffectState[] Effects;
        }
        private readonly Dictionary<Api.EffectDefinition,string[]> worldDefinitions=new Dictionary<Api.EffectDefinition,string[]>();
        private readonly Dictionary<string,PreparedEffect> receivedDefinitions=new Dictionary<string,PreparedEffect>(StringComparer.Ordinal);
        private string worldLayout;
        internal IDisposable BindWorld()
        {return WorldRegistry.Shared.Bind<WorldScene>("mega.mapping.scene",CaptureWorld,RestoreWorld,ValidateWorld);}
        private string LayoutIdentity()
        {
            if(worldLayout==null){
                // topology, authored actions and resources must agree, not just DTO fields
                var serializer=new XmlSerializer(typeof(SceneFile));string xml;
                using(var text=new StringWriter(CultureInfo.InvariantCulture)){serializer.Serialize(text,scene);xml=text.ToString();}
                using(var hash=SHA256.Create())worldLayout=BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(xml))).Replace("-","");
            }
            return worldLayout;
        }
        private string[] Definition(Api.EffectDefinition value)
        {
            string[] parts;if(worldDefinitions.TryGetValue(value,out parts))return parts;
            var serializer=new XmlSerializer(typeof(Api.EffectDefinition));string xml;
            using(var text=new StringWriter(CultureInfo.InvariantCulture)){serializer.Serialize(text,value);xml=Convert.ToBase64String(Encoding.UTF8.GetBytes(text.ToString()));}
            parts=Enumerable.Range(0,(xml.Length+255)/256).Select(i=>xml.Substring(i*256,Math.Min(256,xml.Length-i*256))).ToArray();
            if(parts.Length>128)throw new InvalidDataException("Scene effect exceeds world-state budget");
            if(worldDefinitions.Count>=256)worldDefinitions.Clear();worldDefinitions.Add(value,parts);return parts;
        }
        private PreparedEffect DecodeDefinition(string[] parts)
        {
            if(parts==null||parts.Length>128||parts.Any(p=>p==null))throw new InvalidDataException("Invalid scene effect definition");
            string encoded=string.Concat(parts);PreparedEffect prepared;
            if(receivedDefinitions.TryGetValue(encoded,out prepared))return prepared;
            byte[] bytes=Convert.FromBase64String(encoded);
            // StringWriter emits a UTF-16 declaration; the transport stores UTF-8 bytes
            using(var text=new StringReader(new UTF8Encoding(false,true).GetString(bytes)))using(var reader=XmlReader.Create(text,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=48000}))
                prepared=PrepareEffect((Api.EffectDefinition)new XmlSerializer(typeof(Api.EffectDefinition)).Deserialize(reader));
            if(receivedDefinitions.Count>=256)receivedDefinitions.Clear();receivedDefinitions.Add(encoded,prepared);return prepared;
        }
        private WorldScene CaptureWorld()
        {
            Check();if(observing)throw new InvalidOperationException("Cannot capture during scene dispatch");
            return new WorldScene{Layout=LayoutIdentity(),Gameplay=gameplayTime,Presentation=presentationTime,
                Trees=Array.ConvertAll(treeStates,s=>s.Clone()),
                Regions=Array.ConvertAll(regions,r=>new WorldRegion{Initialized=r.Initialized,Inside=r.Inside,Fired=r.Fired,Dwell=r.Dwell,Effect=r.Effect}),
                Rules=Array.ConvertAll(rules,r=>new WorldRule{Fired=r.Fired,Last=r.Last}),Flags=new Dictionary<string,string>(flags),
                Events=events.Select(e=>new WorldEvent{Name=e.Name,Screen=e.Screen}).ToArray(),Failed=failedObservers.ToArray(),
                Effects=active.Select(e=>new WorldEffectState{Id=e.Id,Owner=e.Owner,Definition=Definition(e.Definition),Started=e.Started}).ToArray()};
        }
        private void ValidateWorld(WorldScene value)
        {
            Check();
            if(value.Layout!=LayoutIdentity()||value.Gameplay<0||value.Presentation<0||value.Trees==null||value.Trees.Length!=treeStates.Length
                ||value.Regions==null||value.Regions.Length!=regions.Length||value.Rules==null||value.Rules.Length!=rules.Length
                ||value.Flags==null||value.Flags.Count!=flags.Count||value.Flags.Keys.Any(k=>!flags.ContainsKey(k))
                ||value.Events==null||value.Events.Length>256||value.Failed==null||value.Effects==null||value.Effects.Length>256)
                throw new InvalidDataException("Scene world state differs from authored scene");
            foreach(var flag in value.Flags)NarrativeValidation.FlagValue(scene.Flags.First(f=>f.Id==flag.Key),flag.Value);
            for(int i=0;i<value.Trees.Length;i++){
                var tree=value.Trees[i];if(tree==null||!Enum.IsDefined(typeof(WorldTreeResult),tree.Result)
                    ||tree.Result==WorldTreeResult.Running&&(tree.Nodes==null||tree.Nodes.Length!=treePrograms[i].Code.Length)
                    ||tree.Result!=WorldTreeResult.Running&&tree.Nodes!=null)throw new InvalidDataException("Invalid scene tree state");
                if(tree.Nodes!=null)for(int n=0;n<tree.Nodes.Length;n++){
                    var state=tree.Nodes[n];if(!Enum.IsDefined(typeof(WorldTreeResult),state.Result)||state.Cursor<0||state.Cursor>treePrograms[i].Code[n].Children.Length||state.Iterations<0||state.Started<0||state.Effect<0)
                        throw new InvalidDataException("Invalid scene tree cursor");
                }
            }
            if(value.Regions.Any(r=>r==null||r.Dwell<0||r.Effect<0)||value.Rules.Any(r=>r==null)
                ||value.Events.Any(e=>e==null||string.IsNullOrWhiteSpace(e.Name)||e.Screen<0)||value.Failed.Any(f=>f==null)
                ||value.Effects.Any(e=>e==null||e.Id<=0||e.Started<0||string.IsNullOrWhiteSpace(e.Owner))||value.Effects.Select(e=>e.Id).Distinct().Count()!=value.Effects.Length)
                throw new InvalidDataException("Invalid scene world effects");
            foreach(var effect in value.Effects)DecodeDefinition(effect.Definition);
        }
        private void RestoreWorld(WorldScene value)
        {
            ValidateWorld(value);
            var previous=active.ToDictionary(e=>e.Id);active.Clear();gameplayTime=value.Gameplay;presentationTime=value.Presentation;
            foreach(var effect in value.Effects){
                var prepared=DecodeDefinition(effect.Definition);
                ActiveEffect existing;
                if(previous.TryGetValue(effect.Id,out existing)&&existing.Owner==effect.Owner&&ReferenceEquals(existing.Definition,prepared.Definition)){
                    existing.Started=effect.Started;active.Add(existing);
                }else active.Add(new ActiveEffect{Id=effect.Id,Owner=effect.Owner,Definition=prepared.Definition,Changes=prepared.Changes,Started=effect.Started,Lights=Array.ConvertAll(prepared.Lights,l=>Copy(l))});
                foreach(var change in prepared.Changes)touched.Add(change.Property);serial=Math.Max(serial,effect.Id);
            }
            for(int i=0;i<regions.Length;i++){var from=value.Regions[i];var to=regions[i];to.Initialized=from.Initialized;to.Inside=from.Inside;to.Fired=from.Fired;to.Dwell=from.Dwell;to.Effect=from.Effect;}
            for(int i=0;i<rules.Length;i++){rules[i].Fired=value.Rules[i].Fired;rules[i].Last=value.Rules[i].Last;}
            flags.Clear();foreach(var flag in value.Flags)flags.Add(flag.Key,flag.Value);
            events.Clear();foreach(var valueEvent in value.Events)events.Enqueue(new SceneEvent{Name=valueEvent.Name,Screen=valueEvent.Screen});
            failedObservers.Clear();foreach(string failure in value.Failed)failedObservers.Add(failure);
            RestoreTrees(value.Trees);RefreshLights();Recompose();
            // restoration is presentation only, never publish remote save flags
        }
    }
}
