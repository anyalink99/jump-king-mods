using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;

namespace Metroidvania
{
    public enum GearSlot { None, Weapon, Shield, Body }
    public sealed class WeaponAttackDefinition
    {
        public readonly int Start,Active,RecoveryExtra,Multiplier;
        internal WeaponAttackDefinition(int start,int active,int recovery,int multiplier){Start=start;Active=active;RecoveryExtra=recovery;Multiplier=multiplier;}
    }
    public sealed class ItemDefinition
    {
        public readonly string Id, Name, Description;
        public readonly GearSlot Slot;
        public readonly int Damage, Reach, Armor;
        public readonly int ArtIndex,VisualId,LightCue,HeavyCue,HitCue,Heal;
        public readonly WeaponAttackDefinition LightAttack,HeavyAttack;
        internal ItemDefinition(string id,string name,string description,GearSlot slot,int damage,int reach,int armor,int artIndex,int visualId,int lightCue,int heavyCue,int hitCue,int heal,WeaponAttackDefinition light,WeaponAttackDefinition heavy)
        { Id=id;Name=name;Description=description;Slot=slot;Damage=damage;Reach=reach;Armor=armor;ArtIndex=artIndex;VisualId=visualId;LightCue=lightCue;HeavyCue=heavyCue;HitCue=hitCue;Heal=heal;LightAttack=light;HeavyAttack=heavy; }
    }
    public static class Catalog
    {
        private static readonly ItemDefinition[] values=ReadItems();
        public static readonly ReadOnlyCollection<ItemDefinition> Items=Array.AsReadOnly(values);
        private static ItemDefinition[] ReadItems(){ContentCatalog.Prepare();return ContentCatalog.Items;}
        public static ItemDefinition Find(string id) { foreach(var item in values) if(item.Id==id)return item;return null; }
        public static int Index(string id) { for(int i=0;i<values.Length;i++)if(values[i].Id==id)return i;return -1; }
        internal static ItemDefinition Visual(GearSlot slot,int id){foreach(var item in values)if(item.Slot==slot&&item.VisualId==id)return item;return null;}
        public static readonly string[] Positive = {"Mettle","Impulse","Refinement","Faith"};
        public static readonly string[] Negative = {"Guile","Composure","Crudeness","Spite"};
    }
    public sealed class ItemStack
    {
        public string Id;
        public int Count;
        public ItemStack() { }
        public ItemStack(string id,int count) { Id=id;Count=count; }
    }
    public sealed class CharacterProfile
    {
        public int Version=1, Experience, Points, Victories;
        public int[] Attributes=new int[4];
        public string Weapon="worn-blade", Shield="buckler", Body="travel-coat";
        public ItemStack[] Inventory={new ItemStack("worn-blade",1),new ItemStack("buckler",1),new ItemStack("travel-coat",1),new ItemStack("tonic",3)};
        public int Level { get { int n=1;foreach(int a in Attributes)n+=Math.Abs(a);return n; } }
        public int NextExperience { get { return 40+20*(Level+Points-1); } }
        public int Count(string id) { foreach(var item in Inventory)if(item.Id==id)return item.Count;return 0; }
        public string Equipped(GearSlot slot) { return slot==GearSlot.Weapon?Weapon:slot==GearSlot.Shield?Shield:slot==GearSlot.Body?Body:""; }
        public CharacterProfile Copy()
        {
            var p=(CharacterProfile)MemberwiseClone();p.Attributes=(int[])Attributes.Clone();p.Inventory=new ItemStack[Inventory.Length];
            for(int i=0;i<Inventory.Length;i++)p.Inventory[i]=new ItemStack(Inventory[i].Id,Inventory[i].Count);return p;
        }
        public static void Validate(CharacterProfile p)
        {
            if(p==null||p.Version!=1||p.Attributes==null||p.Attributes.Length!=4||p.Inventory==null||p.Inventory.Length>Catalog.Items.Count)
                throw new InvalidDataException("Unsupported character profile");
            foreach(int value in p.Attributes)if(value < -20||value>20)throw new InvalidDataException("Invalid attribute");
            if(p.Points<0||p.Points>80||p.Level+p.Points>81||p.Experience<0||p.Experience>=p.NextExperience||p.Victories<0||p.Victories>1000000)
                throw new InvalidDataException("Invalid progression");
            var seen=new HashSet<string>(StringComparer.Ordinal);
            foreach(var stack in p.Inventory) {
                var item=stack==null?null:Catalog.Find(stack.Id);
                if(item==null||!seen.Add(stack.Id)||stack.Count<1||stack.Count>(item.Slot==GearSlot.None?99:1))throw new InvalidDataException("Invalid inventory");
            }
            foreach(var slot in new[]{GearSlot.Weapon,GearSlot.Shield,GearSlot.Body}) {
                string id=p.Equipped(slot);var item=Catalog.Find(id);
                if(id!=""&&(item==null||item.Slot!=slot||p.Count(id)!=1))throw new InvalidDataException("Invalid equipped item");
            }
        }
        public bool Train(int axis,int direction)
        {
            if(axis<0||axis>=4||Math.Abs(direction)!=1||Points==0||Math.Abs(Attributes[axis])>=20)return false;
            if(Attributes[axis]!=0&&Math.Sign(Attributes[axis])!=direction)return false;
            Attributes[axis]+=direction;Points--;return true;
        }
        public void AddExperience(int amount)
        {
            if(amount<0||amount>100000)throw new ArgumentOutOfRangeException("amount");
            while(amount>0&&Level+Points<81) {
                int take=Math.Min(amount,NextExperience-Experience);amount-=take;Experience+=take;
                if(Experience==NextExperience){Experience=0;Points++;}
            }
        }
        public bool Equip(string id)
        {
            var item=Catalog.Find(id);if(item==null||item.Slot==GearSlot.None||Count(id)==0)return false;
            string next=Equipped(item.Slot)==id?"":id;
            if(item.Slot==GearSlot.Weapon)Weapon=next;else if(item.Slot==GearSlot.Shield)Shield=next;else Body=next;return true;
        }
        public void Grant(string id,int amount)
        {
            var item=Catalog.Find(id);if(item==null||amount<1||amount>99)throw new ArgumentException("Invalid item grant");
            foreach(var stack in Inventory)if(stack.Id==id){stack.Count=Math.Min(item.Slot==GearSlot.None?99:1,stack.Count+amount);return;}
            var next=new List<ItemStack>(Inventory);next.Add(new ItemStack(id,item.Slot==GearSlot.None?amount:1));Inventory=next.ToArray();
        }
        public bool Consume(string id)
        {
            for(int i=0;i<Inventory.Length;i++)if(Inventory[i].Id==id){
                if(--Inventory[i].Count==0){var next=new List<ItemStack>(Inventory);next.RemoveAt(i);Inventory=next.ToArray();}return true;
            }return false;
        }
        public void Victory()
        { Victories=Math.Min(1000000,Victories+1);AddExperience(25);if(Victories==1)Grant("iron-blade",1);if(Victories==3){Grant("reed-spear",1);Grant("iron-coat",1);} }
    }
    public struct CombatStats
    {
        public int Health, Damage, Reach, Armor, Recovery, Guard;
        public int LightStart,LightActive,HeavyStart,HeavyActive,HeavyRecovery,HeavyMultiplier;
        public static CombatStats From(CharacterProfile p)
        {
            var weapon=Catalog.Find(p.Weapon);var body=Catalog.Find(p.Body);var shield=Catalog.Find(p.Shield);
            return new CombatStats {Health=100+(p.Level-1)*4+Math.Max(0,p.Attributes[3])*3,
                Damage=(weapon==null?7:weapon.Damage)+Math.Max(0,p.Attributes[0])*2+Math.Max(0,-p.Attributes[2]),
                Reach=(weapon==null?18:weapon.Reach)+Math.Max(0,-p.Attributes[0]),
                Armor=(body==null?0:body.Armor)+Math.Max(0,p.Attributes[2]),
                Recovery=Math.Max(6,18-Math.Max(0,p.Attributes[1])/2),
                Guard=shield==null?0:shield.Armor+Math.Max(0,-p.Attributes[1])+Math.Max(0,-p.Attributes[3]),
                LightStart=weapon==null?ContentCatalog.LightStart:weapon.LightAttack.Start,LightActive=weapon==null?ContentCatalog.LightActive:weapon.LightAttack.Active,
                HeavyStart=weapon==null?ContentCatalog.HeavyStart:weapon.HeavyAttack.Start,HeavyActive=weapon==null?ContentCatalog.HeavyActive:weapon.HeavyAttack.Active,
                HeavyRecovery=weapon==null?ContentCatalog.HeavyRecovery:weapon.HeavyAttack.RecoveryExtra,HeavyMultiplier=weapon==null?ContentCatalog.HeavyMultiplier:weapon.HeavyAttack.Multiplier};
        }
    }
}
