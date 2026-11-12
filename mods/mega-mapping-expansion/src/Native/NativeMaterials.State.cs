using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using JKRuntime.World;
using MegaMappingExpansion.Construction;

namespace MegaMappingExpansion
{
    internal sealed class MaterialPacket
    {
        [WorldField] internal string Identity;
        [WorldField] internal bool[] Enabled;
    }
    internal sealed partial class NativeMaterials
    {
        private MaterialInstance[] stateOrder;
        private string stateIdentity;
        private ScenePersistence persistence;
        private int saveRevision;
        public string Id{get{return "mega.mapping.materials";}}
        public int Version{get{return 1;}}
        internal void PrepareState(bool persist=false)
        {
            stateOrder=Instances.OrderBy(i=>i.Id,StringComparer.Ordinal).ToArray();
            string geometry=string.Join(";",stateOrder.Select(i=>i.Id+":"+i.Definition.Id+":"+string.Join("/",i.Blocks.Select(b=>b.GetType().AssemblyQualifiedName+":"+b.GetRect()))));
            using(var sha=SHA256.Create())stateIdentity=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(Plan.Identity+"\n"+geometry))).Replace("-","");
            if(persist)
            {
                System.Collections.Generic.Dictionary<string,string> saved;
                MaterialSaveCoordinator.Load(Path.GetFullPath(root),ScenePersistence.NativeDirectory,out persistence,out saved,out saveRevision);
                string identity;
                if(saved.TryGetValue("identity",out identity)&&identity==stateIdentity)
                {
                    string bits=string.Concat(Enumerable.Range(0,(stateOrder.Length+4095)/4096).Select(i=>saved[i.ToString("D3")]));
                    if(bits.Length!=stateOrder.Length||bits.Any(c=>c!='0'&&c!='1'))throw new InvalidDataException("Invalid material save");
                    Restore(new MaterialPacket{Identity=stateIdentity,Enabled=bits.Select(c=>c=='1').ToArray()});
                }
            }
        }
        private MaterialPacket Packet()
        {if(stateOrder==null)PrepareState();return new MaterialPacket{Identity=stateIdentity,Enabled=stateOrder.Select(i=>i.Enabled).ToArray()};}
        public object Capture(){return Packet();}
        public void Validate(object value)
        {
            if(stateOrder==null)PrepareState();var packet=value as MaterialPacket;
            if(packet==null||packet.Identity!=stateIdentity||packet.Enabled==null||packet.Enabled.Length!=stateOrder.Length)throw new InvalidDataException("Material map or state changed");
        }
        public void Restore(object value)
        {Validate(value);var packet=(MaterialPacket)value;for(int i=0;i<stateOrder.Length;i++)stateOrder[i].Enabled=packet.Enabled[i];MaterialContacts.ClearAllSweeps();PublishState();}
        private void CheckReset()
        {
            if(saveRevision==System.Threading.Volatile.Read(ref MaterialSaveCoordinator.Revision))return;
            ScenePersistence next;if(MaterialSaveCoordinator.TryResetStore(persistence,out next,out saveRevision)){persistence=next;foreach(var instance in Instances)instance.Enabled=instance.Definition.Enabled;PublishState();}
        }
        private void PublishState()
        {
            if(Current!=this||persistence==null||WorldControl.Following||!WorldExecution.CanPersist)return;
            var packet=Packet();string bits=new string(packet.Enabled.Select(b=>b?'1':'0').ToArray());
            var values=new System.Collections.Generic.Dictionary<string,string>{{"identity",stateIdentity}};
            for(int i=0;i<bits.Length;i+=4096)values.Add((i/4096).ToString("D3"),bits.Substring(i,Math.Min(4096,bits.Length-i)));
            MaterialSaveCoordinator.Publish(persistence,values);
        }
        internal static void ValidatePlan(MaterialPlan plan,MovingPlatformPlan platforms,SceneDocument scene,string root,int screens,bool geometry=true)
        {
            foreach(var paint in plan.Paint)if(paint.Screens!=null&&paint.Screens.Any(s=>s>screens))throw new InvalidDataException("Material palette scope is outside the map: "+paint.Color);
            foreach(var paint in plan.Paint)if(platforms.Owns(paint.Color))throw new InvalidDataException("Material palette overlaps a moving-platform marker: "+paint.Color);
            foreach(var area in plan.Areas){if(area.Screen>screens)throw new InvalidDataException("Material area screen is outside the map: "+area.Id);if(geometry)MaterialGeometry.Compile(area,root);}
            foreach(var profile in platforms.Profiles.Values.Where(p=>p.Material!=""))
            {
                var definition=plan.Require(profile.Material);
                if(definition.Layers.Any(l=>plan.Sources[l.Source].Sizing!="rectangle"))throw new InvalidDataException("Moving materials need sizing=rectangle: "+profile.Material);
                if(definition.Collision!="trigger")throw new InvalidDataException("Moving materials must use collision=trigger; the platform profile owns solid/oneWay/trigger support");
            }
            if(scene==null)return;
            foreach(var action in NativeScene.Actions(scene).Where(a=>a.Operation!=null&&a.Operation.StartsWith("material",StringComparison.Ordinal)))
            {
                if(string.IsNullOrEmpty(action.Target)||!plan.Materials.ContainsKey(action.Target)&&!plan.Areas.Any(a=>a.Id==action.Target)&&!platforms.Platforms.Any(p=>"platform-"+p.Id==action.Target)&&!(action.Target.StartsWith("paint-",StringComparison.Ordinal)&&plan.Paint.Count>0))
                    throw new InvalidDataException("Unknown material action target: "+action.Target);
            }
        }
    }
}
