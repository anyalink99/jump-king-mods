using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using JKRuntime;
using JKRuntime.Settings;
using Microsoft.Xna.Framework;

namespace Metroidvania
{
    internal static partial class MetroidvaniaTests
    {
        private static int checks;
        private static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
        private static void Reject(Action action,string message){bool rejected=false;try{action();}catch(InvalidDataException){rejected=true;}Check(rejected,message);}
        private static bool Floor(Rectangle box){return box.Bottom>100;}
        private static readonly Func<Rectangle,bool> FlatFloor=Floor;
        private static readonly EnemyDefinition CrawlerData=EnemyDefinition.Load();
        private static readonly Vector2 King=new Vector2(0,74);
        private static Battle NewBattle(){var b=new Battle(CombatStats.From(new CharacterProfile()),CrawlerData);b.Spawn(new Vector2(36,78),0,80);return b;}
        private static void Step(Battle b,int count){for(int i=0;i<count;i++)b.Step(King,FlatFloor);}
        private static void Send(Battle b,ulong serial,CombatCommandKind kind,int facing=1){Check(b.Command(new CombatCommand{Actor=1,Serial=serial,Kind=kind,Facing=facing}),"Command accepted: "+kind);}
        private static string Snapshot(Battle b){using(var text=new StringWriter()){new XmlSerializer(typeof(BattleSnapshot)).Serialize(text,b.Capture());return text.ToString();}}
        public static int Main(string[] args)
        {
            try{
                string scratch=args[0];Directory.CreateDirectory(scratch);
                if(args.Length>1&&args[1]=="--graphics"){Graphics(args[2],scratch,args.Length>3?int.Parse(args[3]):0);return 0;}
                if(args.Length>1&&args[1]=="--audio"){NativeAudio();return 0;}
                Profiles();Combat();Snapshots();Falls();AudioEvents();Waves();QuestEnemies(scratch);Infrastructure();Persistence(scratch);Encounters();NativeGeometry();GameSession.ValidateNative();Performance(scratch);
                Console.WriteLine("[OK] Metroidvania: "+checks+" behavior/data-safety checks");return 0;
            }catch(Exception e){Console.Error.WriteLine(e);return 1;}
        }
        private static void Profiles()
        {
            var p=new CharacterProfile();CharacterProfile.Validate(p);Check(p.Level==1&&p.Count("tonic")==3,"Starter inventory");
            p.AddExperience(39);Check(p.Points==0&&p.Experience==39,"XP stays below threshold");p.AddExperience(1);Check(p.Points==1&&p.Experience==0&&p.Level==1,"Level grants an unspent point");
            Check(p.Train(0,1)&&p.Level==2&&p.Points==0,"Allocate a point");Check(!p.Train(0,-1),"No free respec");
            var copy=p.Copy();copy.Attributes[0]=12;copy.Inventory[0].Count=2;Check(p.Attributes[0]==1&&p.Inventory[0].Count==1,"Profile edits are detached");
            Check(!p.Equip("iron-blade"),"Unowned gear refused");Check(p.Equip("worn-blade")&&p.Weapon=="","Unequip weapon");Check(p.Equip("worn-blade")&&p.Weapon=="worn-blade","Re-equip weapon");
            p.Victory();Check(p.Count("iron-blade")==1,"First reward");p.Victory();p.Victory();Check(p.Count("reed-spear")==1&&p.Count("iron-coat")==1,"Third reward");
            Check(p.Equip("iron-blade")&&CombatStats.From(p).Damage>16,"Equipped gear changes combat stats");CharacterProfile.Validate(p);
            for(int i=0;i<3;i++)Check(p.Consume("tonic"),"Tonic consumed");Check(!p.Consume("tonic"),"Empty stack cannot underflow");
            p.AddExperience(100000);Check(p.Level+p.Points==81,"Progression has a cap");CharacterProfile.Validate(p);
            p.Inventory=new[]{new ItemStack("tonic",1),new ItemStack("tonic",2)};Reject(()=>CharacterProfile.Validate(p),"Duplicate inventory rejected");
            p=new CharacterProfile();p.Weapon="missing";Reject(()=>CharacterProfile.Validate(p),"Unknown equipment rejected");
            p=new CharacterProfile();p.Attributes[1]=21;Reject(()=>CharacterProfile.Validate(p),"Out-of-range attribute rejected");
        }
        private static void Combat()
        {
            var b=NewBattle();b.Player.Invulnerable=1000;Send(b,1,CombatCommandKind.Light);Step(b,7);Check(b.Enemies[0].Health==200,"Startup cannot hit");Step(b,1);Check(b.Enemies[0].Health==184,"Active window deals damage");Step(b,4);Check(b.Enemies[0].Health==184,"One target hit once per swing");
            Check(!b.Command(new CombatCommand{Actor=1,Serial=1,Kind=CombatCommandKind.Heavy,Facing=1}),"Duplicate command refused");
            Check(!b.Command(new CombatCommand{Actor=99,Serial=2,Kind=CombatCommandKind.Light,Facing=1}),"Foreign actor refused");
            Check(!b.Command(new CombatCommand{Actor=1,Serial=2,Kind=CombatCommandKind.Light,Facing=0}),"Invalid facing refused");
            Step(b,25);Send(b,2,CombatCommandKind.Heavy);Step(b,19);Check(b.Enemies[0].Health==184,"Heavy windup");Step(b,1);Check(b.Enemies[0].Health==152,"Heavy hit doubles damage");
            b=NewBattle();Send(b,1,CombatCommandKind.Light);for(int i=0;i<12;i++)b.Step(King,r=>r.Bottom>100||r.X==25);Check(b.Enemies[0].Health==200,"Terrain blocks a swing");
            b=NewBattle();Step(b,100);Send(b,1,CombatCommandKind.Guard);Step(b,5);Check(b.Player.Health==100&&b.Enemies[0].Action==CombatAction.Hurt,"Opening guard parries");
            b=NewBattle();Step(b,100);Send(b,1,CombatCommandKind.Guard,-1);Step(b,5);Check(b.Player.Health<100,"Guard has a facing");
            b=NewBattle();Step(b,90);Send(b,1,CombatCommandKind.Guard);Step(b,15);Check(b.Player.Health<100&&b.Player.Action==CombatAction.Guard,"Late guard has chip damage");
            b=new Battle(CombatStats.From(new CharacterProfile{Shield=""}),CrawlerData);Check(!b.Command(new CombatCommand{Actor=1,Serial=1,Kind=CombatCommandKind.Guard,Facing=1}),"Shield required");
            b=NewBattle();b.Player.Health=1;Step(b,105);Check(b.Player.Action==CombatAction.Down&&b.Player.Health==0,"Zero health enters recovery");
            b.SetStats(CombatStats.From(new CharacterProfile()));Check(b.Player.Health==0,"Stat refresh cannot revive a downed actor");Step(b,150);Check(b.Player.Health==b.Player.MaximumHealth&&b.Player.Invulnerable>0,"Recovery restores health with grace");
            Check(!b.Heal(40),"Full health cannot consume healing");b.Player.Health-=40;Check(b.Heal(40)&&b.Player.Health==b.Player.MaximumHealth,"Heal applies once");
            b=NewBattle();Send(b,1,CombatCommandKind.Heavy);Step(b,26);Check(b.Enemies[0].Health==168,"Heavy damage remains single-hit");
        }
        private static void Snapshots()
        {
            var a=NewBattle();Send(a,1,CombatCommandKind.Light);Step(a,9);var saved=a.Capture();
            var b=NewBattle();b.Restore(saved);Step(a,120);Step(b,120);Check(Snapshot(a)==Snapshot(b),"Restore reproduces subsequent ticks exactly");
            b.Restore(saved);Check(b.EventCount==0,"Restoring doesn't replay rewards or effects");saved.Player.Position=new Vector2(999,999);saved.Enemies[0].Health=1;Check(b.Player.Position!=saved.Player.Position&&b.Enemies[0].Health!=1,"Restore detaches snapshot arrays/actors");
            var invalid=b.Capture();invalid.Player.Position=new Vector2(float.NaN,0);string before=Snapshot(b);Reject(()=>b.Restore(invalid),"Invalid snapshot refused");Check(before==Snapshot(b),"Failed restore doesn't partially mutate battle");
            invalid=b.Capture();invalid.Enemies[0].Id=1;Reject(()=>b.Restore(invalid),"Duplicate actor IDs rejected");
            invalid=b.Capture();invalid.Enemies[0].Position=new Vector2(float.PositiveInfinity,0);Reject(()=>b.Restore(invalid),"Infinite positions rejected");
            var capped=new Battle(CombatStats.From(new CharacterProfile()),CrawlerData);for(int i=0;i<16;i++)Check(capped.Spawn(new Vector2(i*22,84),i*22,i*22+20),"Bounded spawn "+i);Check(!capped.Spawn(new Vector2(0,84),0,20),"Enemy cap enforced");
            var sleep=new Battle(CombatStats.From(new CharacterProfile()),CrawlerData);sleep.Spawn(new Vector2(30,-1000),10,80);sleep.Enemies[0].Action=CombatAction.Light;Step(sleep,100);Check(sleep.Enemies[0].Age==0,"Distant AI clock sleeps");
        }
        private static void Persistence(string scratch)
        {
            string path=Path.Combine(scratch,"character.xml");var store=new CharacterStore(path);Check(store.Writable,"Missing profile is writable");store.Edit(p=>{p.Victory();return true;});Check(store.Dirty&&store.Save(),"Save pending rewards");
            store.Edit(p=>p.Equip("iron-blade"));Check(store.Save()&&File.Exists(path+".bak"),"Atomic save retains previous profile");var reopened=new CharacterStore(path);Check(reopened.Current.Weapon=="iron-blade"&&reopened.Current.Victories==1,"Save round trip");
            byte[] good=File.ReadAllBytes(path);File.WriteAllText(path,"<broken>");var damaged=new CharacterStore(path);Check(!damaged.Writable&&!damaged.Edit(p=>true)&&!damaged.Save(),"Corrupt profile stays protected even with backup");Check(File.ReadAllText(path)=="<broken>","Corrupt original preserved");
            File.WriteAllBytes(path,good);store=new CharacterStore(path);File.AppendAllText(path,"\n");store.Edit(p=>{p.Victory();return true;});Check(!store.Save()&&store.Dirty,"External edits retain pending changes and refuse overwrite");
            string future=Path.Combine(scratch,"future.xml");File.WriteAllText(future,"<CharacterProfile><Version>2</Version></CharacterProfile>");var unknown=new CharacterStore(future);Check(!unknown.Writable,"Future schema protected");
            future=Path.Combine(scratch,"unknown.xml");File.WriteAllText(future,"<CharacterProfile><FutureInventory>1</FutureInventory></CharacterProfile>");Check(!new CharacterStore(future).Writable,"Unknown save fields protected");
            var prefs=new Preferences();Preferences.Validate(prefs);Check(!prefs.Enabled,"Mode starts off");prefs.Bindings=new[]{new SavedBinding{Device="keyboard",Action="light",Primary=new[]{74}}};Preferences.Validate(prefs);var copy=prefs.Copy();copy.Bindings[0].Primary[0]=75;Check(prefs.Bindings[0].Primary[0]==74,"Bindings clone independently");
        }
        private static void Encounters()
        {
            EncounterFile.Validate(new EncounterFile{Enemies=new[]{new EnemySpawn{Id="guard",Screen=1,X=200,Y=300}}},1);
            Reject(()=>EncounterFile.Validate(new EncounterFile{Enemies=new[]{new EnemySpawn{Id="guard",Screen=2}}},1),"Nonexistent screen refused");
            Reject(()=>EncounterFile.Validate(new EncounterFile{Enemies=new[]{new EnemySpawn{Id="a"},new EnemySpawn{Id="a"}}},1),"Duplicate spawn IDs refused");
            EquipmentVisual.Validate(new byte[]{1,1,1,8,1,0});bool bad=false;try{EquipmentVisual.Validate(new byte[]{200,1,1,8,1,0});}catch(InvalidOperationException){bad=true;}Check(bad,"Visual payload rejects unknown gear");
        }
        private static void NativeGeometry()
        {
            var world=new CollisionWorld(new[]{new JumpKing.Level.IBlock[]{new JumpKing.Level.BoxBlock(new Rectangle(10,330,100,30))},new JumpKing.Level.IBlock[]{new JumpKing.Level.BoxBlock(new Rectangle(10,-30,100,30))}});
            Check(world.Blocked(new Rectangle(20,335,1,1)),"Native lower screen collision");Check(world.Blocked(new Rectangle(20,-20,1,1)),"Native upper screen collision");
            Check(!world.Blocked(new Rectangle(150,335,1,1)),"Native open space remains open");Check(world.Placement(new Vector2(40,308)),"Native flat support accepts a crawler");
            Check(!world.Placement(new Vector2(150,308)),"Empty space cannot support a crawler");Check(world.Blocked(new Rectangle(-1,100,1,1)),"Crawlers can't leave through a side seam");
        }
        private static void Performance(string scratch)
        {
            var b=new Battle(CombatStats.From(new CharacterProfile()),CrawlerData);for(int i=0;i<16;i++)b.Spawn(new Vector2(i*22,84),i*22,i*22+20);
            Step(b,100);int[] collections={GC.CollectionCount(0),GC.CollectionCount(1),GC.CollectionCount(2)};var watch=Stopwatch.StartNew();Step(b,60000);watch.Stop();
            string report="60,000 fixed ticks, 16 enemies, cheap fixture terrain: "+watch.Elapsed.TotalMilliseconds.ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+" ms; GC collections: "+string.Join(",",Enumerable.Range(0,3).Select(i=>(GC.CollectionCount(i)-collections[i]).ToString()))+". Excludes native collision, rendering, input and disk IO.";
            File.WriteAllText(Path.Combine(scratch,"performance.txt"),report);Console.WriteLine(report);
        }
    }
}
