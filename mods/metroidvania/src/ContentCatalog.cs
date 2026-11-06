using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Xml;

namespace Metroidvania
{
    internal sealed class SoundMarker {internal readonly int Tick,Cue;internal SoundMarker(int tick,int cue){Tick=tick;Cue=cue;}}
    public sealed class AttackDefinition
    {
        public readonly string Id;
        public readonly int Clip,Weight,ActiveTick,Damage;
        internal readonly SoundMarker[] Sounds;
        internal AttackDefinition(XmlElement e){Id=e.GetAttribute("Id");Clip=ContentCatalog.Int(e,"Clip",4,11);Weight=ContentCatalog.Int(e,"Weight",1,1000);ActiveTick=ContentCatalog.Int(e,"ActiveTick",0,1800);Damage=ContentCatalog.Int(e,"Damage",1,1000);Sounds=ContentCatalog.Markers(e,"Sounds");}
    }
    internal sealed class EnemyRecipe
    {
        internal string Id,Name,Data,Atlas;
        internal int Index,Health,Width,Height,AnchorX,AnchorY,Range,HeightRange,Cooldown,Hurt,Death,Hold,Cell,Columns,PivotX,PivotY;
        internal float Walk;
        internal AttackDefinition[] Attacks;
        internal SoundMarker[] WalkSounds,IdleSounds,HurtSounds,DeathSounds;
    }
    internal static class ContentCatalog
    {
        private static readonly Dictionary<string,int> cueIds=new Dictionary<string,int>(StringComparer.Ordinal);
        private static readonly Dictionary<string,EnemyRecipe> enemyIds=new Dictionary<string,EnemyRecipe>(StringComparer.Ordinal);
        private static bool loaded;
        internal static string Hash,DefaultEnemy;
        internal static string[] CueNames;
        internal static EnemyRecipe[] Enemies;
        internal static ItemDefinition[] Items;
        internal static int LightStart,LightActive,HeavyStart,HeavyActive,HeavyRecovery,HeavyMultiplier;
        internal static void Prepare()
        {
            if(loaded)return;
            using(var stream=typeof(ContentCatalog).Assembly.GetManifestResourceStream("Metroidvania.Content")){
                if(stream==null)throw new InvalidDataException("Missing compiled content catalogue");
                byte[] bytes;using(var memory=new MemoryStream()){stream.CopyTo(memory);bytes=memory.ToArray();}
                using(var hash=SHA256.Create())Hash=BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();
                var doc=new XmlDocument{XmlResolver=null};using(var reader=XmlReader.Create(new MemoryStream(bytes,false),new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=1048576}))doc.Load(reader);
                var root=doc.DocumentElement;if(root.Name!="Content"||root.GetAttribute("Version")!="1")throw new InvalidDataException("Unsupported content catalogue");
                cueIds.Clear();enemyIds.Clear();DefaultEnemy=root.GetAttribute("DefaultEnemy");var audio=root.SelectNodes("Audio/Cue");if(audio.Count<1||audio.Count>64)throw new InvalidDataException("Audio catalogue budget");CueNames=new string[audio.Count];
                foreach(XmlElement e in audio){int index=Int(e,"Index",0,audio.Count-1);string id=e.GetAttribute("Id");if(CueNames[index]!=null||cueIds.ContainsKey(id))throw new InvalidDataException("Duplicate cue");CueNames[index]=id;cueIds.Add(id,index);}
                var attack=(XmlElement)root.SelectSingleNode("PlayerAttack");LightStart=Int(attack,"light_start",1,120);LightActive=Int(attack,"light_active",1,120);HeavyStart=Int(attack,"heavy_start",1,120);HeavyActive=Int(attack,"heavy_active",1,120);HeavyRecovery=Int(attack,"heavy_recovery_extra",1,120);HeavyMultiplier=Int(attack,"heavy_multiplier",1,8);
                var items=root.SelectNodes("Items/Item");if(items.Count<1||items.Count>128)throw new InvalidDataException("Item catalogue budget");Items=new ItemDefinition[items.Count];
                var itemIds=new HashSet<string>(StringComparer.Ordinal);var visualIds=new HashSet<string>(StringComparer.Ordinal);
                foreach(XmlElement e in items){int index=Int(e,"Index",0,items.Count-1);GearSlot slot;if(!Enum.TryParse(e.GetAttribute("Slot"),out slot)||!Enum.IsDefined(typeof(GearSlot),slot)||Items[index]!=null||!itemIds.Add(e.GetAttribute("Id")))throw new InvalidDataException("Invalid item slot or ID");
                    int visual=Int(e,"VisualId",0,255);if(slot!=GearSlot.None&&(visual==0||!visualIds.Add(slot+":"+visual)))throw new InvalidDataException("Duplicate appearance token");
                    var light=new WeaponAttackDefinition(Int(e,"LightStart",1,120),Int(e,"LightActive",1,120),0,1);var heavy=new WeaponAttackDefinition(Int(e,"HeavyStart",1,120),Int(e,"HeavyActive",1,120),Int(e,"HeavyRecovery",1,120),Int(e,"HeavyMultiplier",1,8));
                    Items[index]=new ItemDefinition(e.GetAttribute("Id"),e.GetAttribute("Name"),e.GetAttribute("Description"),slot,Int(e,"Damage",0,1000),Int(e,"Reach",0,128),Int(e,"Armor",0,200),Int(e,"ArtIndex",0,127),visual,Cue(e.GetAttribute("LightCue")),Cue(e.GetAttribute("HeavyCue")),Cue(e.GetAttribute("HitCue")),Int(e,"Heal",0,2000),light,heavy);
                }
                var enemies=root.SelectNodes("Enemies/Enemy");if(enemies.Count<1||enemies.Count>32)throw new InvalidDataException("Enemy catalogue budget");Enemies=new EnemyRecipe[enemies.Count];
                foreach(XmlElement e in enemies){
                    if(e.GetAttribute("Behavior")!="grounded-melee")throw new InvalidDataException("Unsupported enemy behavior");
                    var value=new EnemyRecipe{Id=e.GetAttribute("Id"),Index=Int(e,"Index",1,enemies.Count),Data=e.GetAttribute("Data"),Atlas=e.GetAttribute("Atlas"),Health=Int(e,"Health",1,2000),Width=Int(e,"BodyWidth",1,128),Height=Int(e,"BodyHeight",1,128),AnchorX=Int(e,"AnchorX",-128,128),AnchorY=Int(e,"AnchorY",-128,128),Range=Int(e,"AttackRange",1,256),HeightRange=Int(e,"HeightRange",0,128),Cooldown=Int(e,"Cooldown",0,600),Hurt=Int(e,"HurtTicks",1,600),Death=Int(e,"DeathTicks",1,600),Hold=Int(e,"DeathHoldTick",0,600),Cell=Int(e,"Cell",16,128),Columns=Int(e,"Columns",1,32),PivotX=Int(e,"PivotX",0,128),PivotY=Int(e,"PivotY",0,128),Walk=Float(e,"WalkSpeed",0,4),WalkSounds=Markers(e,"WalkSounds"),IdleSounds=Markers(e,"IdleSounds"),HurtSounds=Markers(e,"HurtSounds")};
                    value.Name=e.GetAttribute("Name");value.DeathSounds=Markers(e,"DeathSounds");if(value.Name.Length<1||value.Name.Length>64)throw new InvalidDataException("Invalid enemy display name");
                    var actions=e.SelectNodes("Attack");if(actions.Count<1||actions.Count>8)throw new InvalidDataException("Attack catalogue budget");value.Attacks=new AttackDefinition[actions.Count];for(int i=0;i<actions.Count;i++)value.Attacks[i]=new AttackDefinition((XmlElement)actions[i]);
                    if(value.Hold>value.Death||value.PivotX>value.Cell||value.PivotY>value.Cell||Enemies[value.Index-1]!=null||enemyIds.ContainsKey(value.Id))throw new InvalidDataException("Invalid enemy recipe");Enemies[value.Index-1]=value;enemyIds.Add(value.Id,value);
                }
                if(!enemyIds.ContainsKey(DefaultEnemy))throw new InvalidDataException("Missing default enemy");
            }loaded=true;
        }
        internal static int Int(XmlElement e,string name,int min,int max){int value;if(e==null||!int.TryParse(e.GetAttribute(name),NumberStyles.Integer,CultureInfo.InvariantCulture,out value)||value<min||value>max)throw new InvalidDataException("Invalid content field: "+name);return value;}
        private static float Float(XmlElement e,string name,float min,float max){float value;if(!float.TryParse(e.GetAttribute(name),NumberStyles.Float,CultureInfo.InvariantCulture,out value)||float.IsNaN(value)||float.IsInfinity(value)||value<min||value>max)throw new InvalidDataException("Invalid content field: "+name);return value;}
        internal static int Cue(string id){if(id=="")return -1;int index;if(!cueIds.TryGetValue(id,out index))throw new InvalidDataException("Unknown audio cue: "+id);return index;}
        internal static SoundMarker[] Markers(XmlElement e,string name){var nodes=e.SelectNodes(name+"/Sound");if(nodes.Count>64)throw new InvalidDataException("Sound marker budget");var ticks=new Dictionary<int,int>();var result=new SoundMarker[nodes.Count];for(int i=0;i<result.Length;i++){var item=(XmlElement)nodes[i];int tick=Int(item,"Tick",0,1800),count;ticks.TryGetValue(tick,out count);if(count>=4)throw new InvalidDataException("Simultaneous sound marker budget");ticks[tick]=count+1;result[i]=new SoundMarker(tick,Cue(item.GetAttribute("Cue")));}return result;}
        internal static EnemyRecipe Enemy(string id){Prepare();EnemyRecipe value;return enemyIds.TryGetValue(id??DefaultEnemy,out value)?value:null;}
    }
}
