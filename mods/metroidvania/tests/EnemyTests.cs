using System;
using System.IO;
using System.Diagnostics;
using System.Linq;
using JumpKing;
using JumpKing.JKMemory.KingSpriteLayers;
using JKRuntime.Presentation;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Metroidvania
{
    internal static partial class MetroidvaniaTests
    {
        private static Battle QuestBattle(EnemyDefinition definition)
        {var battle=new Battle(CombatStats.From(new CharacterProfile()),definition);Check(battle.Spawn(new Vector2(36,78),0,80,"gate"),"Quest crawler spawn accepted");return battle;}
        private static void QuestEnemies(string scratch)
        {
            var definition=EnemyDefinition.Load();Check(definition.ContentHash.Length==64&&definition.FrameCount>100,"Baked Quest clips and content identity loaded");
            var b=QuestBattle(definition);Check(b.Enemies[0].Health==200&&b.Enemies[0].Bounds.Width==32,"Crawler uses its own health and body");
            Check(!b.Spawn(new Vector2(36,78),0,80,"gate"),"Repeated map spawn ID refused");
            Step(b,102);Check(b.Player.Health==100,"Original lunge windup cannot deal early damage");Step(b,5);Check(b.Player.Health==100-Math.Max(1,32-b.Stats.Armor),"Animated lunge hitbox lands on its active frames");
            int health=b.Player.Health;Step(b,140);Check(b.Player.Health==health,"Two animated attack boxes share one consumed hit");Check(b.Enemies[0].Position.X>=19.9f&&b.Enemies[0].Position.X<21,"Source root motion advances the lunge by 16 pixels");
            b=QuestBattle(definition);Step(b,100);Send(b,1,CombatCommandKind.Guard);Step(b,5);Check(b.Player.Health==100&&b.Enemies[0].Action==CombatAction.Hurt,"Timed guard interrupts the imported attack");
            b=QuestBattle(definition);for(int i=0;i<260;i++)b.Step(King,r=>r.Bottom>100||r.X==25);Check(b.Player.Health==100,"Imported reach cannot hit through a wall");
            b=QuestBattle(definition);for(int i=0;i<200;i++)b.Step(King,r=>r.Bottom>100&&r.X>40);Check(b.Enemies[0].Position.X==36,"Root motion stops at an unsupported edge");
            b=QuestBattle(definition);b.Player.Invulnerable=1000;Send(b,1,CombatCommandKind.Light);Step(b,8);Check(b.Enemies[0].Health<200&&b.Enemies[0].Action==CombatAction.Hurt,"Native king can strike an animated hurtbox");
            Check(Enumerable.Range(0,b.EventCount).Any(i=>b.Event(i).Kind==CombatEventKind.EnemyCue&&b.Event(i).Amount==definition.Recipe.HurtSounds[0].Cue),"Hits emit the crawler's own pain cue");
            b=QuestBattle(definition);int voice=0,attack=0,movement=0;for(int i=0;i<250;i++){b.Step(King,Floor);for(int j=0;j<b.EventCount;j++){var e=b.Event(j);if(e.Kind!=CombatEventKind.EnemyCue)continue;if(e.Amount==definition.Attacks[0].Sounds[0].Cue){voice++;Check(b.Enemies[0].Age==16,"Quest attack voice keeps its source timing");}if(e.Amount==definition.Attacks[0].Sounds[1].Cue){attack++;Check(b.Enemies[0].Age==92,"Quest attack sound keeps its source timing");}if(e.Amount==definition.Attacks[0].Sounds[2].Cue)movement++;}}
            Check(voice==1&&attack==1&&movement==1,"Attack cues aren't repeated across active frames");
            b=QuestBattle(definition);Step(b,75);var saved=b.Capture();var copy=QuestBattle(definition);copy.Restore(saved);Step(b,240);Step(copy,240);Check(Snapshot(b)==Snapshot(copy),"Enemy animation, motion and hit consumption survive restoration");
            var different=b.Capture();different.Content=new string('f',64);string before=Snapshot(b);Reject(()=>b.Restore(different),"Foreign enemy table refused");Check(before==Snapshot(b),"Rejected content cannot partly restore a fight");
            different=b.Capture();different.Enemies[0].Archetype=9;Reject(()=>b.Restore(different),"Unknown enemy kind refused");
            different=b.Capture();different.Enemies[0].Archetype=0;Reject(()=>b.Restore(different),"Removed placeholder kind cannot return through a snapshot");
            b=QuestBattle(definition);b.Player.Invulnerable=1000;b.Enemies[0].Health=1;Send(b,1,CombatCommandKind.Light);Step(b,8);Check(b.Enemies[0].Health==0&&b.Enemies[0].Action==CombatAction.Down,"Defeat starts the source collapse animation");Step(b,10);Check(b.Enemies[0].Age==10,"Dead enemy visuals advance without new rewards");Check(!Enumerable.Range(0,b.EventCount).Any(i=>b.Event(i).Kind==CombatEventKind.Defeated),"Corpse updates don't repeat XP");
            var authored=new EncounterFile{Version=2,Enemies=new[]{new EnemySpawn{Id="hall",X=220,Y=308}}};EncounterFile.Validate(authored,1);
            authored.Enemies[0].Type="unknown";Reject(()=>EncounterFile.Validate(authored,1),"Unknown authored archetype rejected");authored.Enemies=new[]{new EnemySpawn{Id="hall"},new EnemySpawn{Id="hall"}};Reject(()=>EncounterFile.Validate(authored,1),"Duplicate map spawn IDs rejected");
            authored.Enemies=new EnemySpawn[0];authored.Version=1;Reject(()=>EncounterFile.Validate(authored,1),"Quest spawns need the explicit new schema");
            string map=Path.Combine(scratch,"quest-map"),config=Path.Combine(map,"props","metroidvania","encounters.xml");Directory.CreateDirectory(Path.GetDirectoryName(config));
            File.WriteAllText(config,"<Metroidvania Version=\"2\"><Enemy Id=\"gate\" Type=\"fslave-crawler\" X=\"220\" Y=\"308\"/></Metroidvania>");var loaded=EncounterFile.Read(map,1);Check(loaded.Enemies.Length==1&&loaded.Enemies[0].Id=="gate","Real authored XML loads the typed enemy");
            File.WriteAllText(config,"<Metroidvania Version=\"1\"><Crawler Id=\"old\" X=\"220\" Y=\"314\"/></Metroidvania>");bool oldRefused=false;try{EncounterFile.Read(map,1);}catch(InvalidOperationException){oldRefused=true;}Check(oldRefused,"Removed prototype spawns can't be loaded");
            File.WriteAllText(config,"<Metroidvania Version=\"2\"><Enemy Id=\"gate\" Typo=\"crawler\"/></Metroidvania>");bool refused=false;try{EncounterFile.Read(map,1);}catch(InvalidOperationException){refused=true;}Check(refused,"XML typos fail instead of silently changing the encounter");
            b=QuestBattle(definition);Check(b.Spawn(new Vector2(0,78),0,16,"edge"),"A short border patrol still creates its authored enemy");
            b=new Battle(CombatStats.From(new CharacterProfile()),definition);for(int i=0;i<16;i++)Check(b.Spawn(new Vector2(i*22,78),0,400,"enemy-"+i),"Bounded imported spawn "+i);b.Player.Invulnerable=1000;Step(b,100);
            var counterMethod=typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread",Members);var allocated=counterMethod==null?null:(Func<long>)Delegate.CreateDelegate(typeof(Func<long>),counterMethod);
            var clock=new Stopwatch();int[] collections={GC.CollectionCount(0),GC.CollectionCount(1),GC.CollectionCount(2)};long initial=allocated==null?0:allocated();clock.Start();Step(b,60000);clock.Stop();long bytes=allocated==null?-1:allocated()-initial;
            if(allocated!=null)Check(bytes==0,"Prepared enemy simulation doesn't allocate per tick");
            string report="60,000 ticks, 16 Quest crawlers, cheap fixture terrain: "+clock.Elapsed.TotalMilliseconds.ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+" ms; allocated bytes="+bytes+"; GC collections: "+string.Join(",",Enumerable.Range(0,3).Select(i=>(GC.CollectionCount(i)-collections[i]).ToString()))+". Excludes native collision, input, audio and drawing.";
            File.WriteAllText(Path.Combine(scratch,"enemy-performance.txt"),report);Console.WriteLine(report);
        }
        private static void EnemySheet(GraphicsDevice device,string scratch)
        {
            using(var target=new RenderTarget2D(device,480,240))using(var batch=new SpriteBatch(device)){
                Game1.spriteBatch=batch;using(var art=new EnemyArt()){
                    device.SetRenderTarget(target);device.Clear(new Color(30,37,46));batch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.PointClamp);
                    var ages=new[]{0,40,104,112,160,230};
                    for(int row=0;row<2;row++)for(int i=0;i<ages.Length;i++){
                        var actor=new FighterState{Id=2,Archetype=1,Health=200,Action=CombatAction.Light,Age=ages[i],Facing=row==0?1:-1};
                        art.Draw(actor,new Vector2(40+i*80,65+row*100),Color.White);
                    }
                    var collapse=new[]{0,30,60,75,89};for(int i=0;i<collapse.Length;i++)art.Draw(new FighterState{Id=2,Archetype=1,Health=0,Action=CombatAction.Down,Age=collapse[i],Facing=1},new Vector2(110+i*80,230),Color.White*Math.Min(1,(90-collapse[i])/30f));
                    var native=Game1.instance.contentManager.playerSprites._CurrentSprites.regular.GetSprite((Regular.SpriteKey)0);
                    foreach(var layer in PlayerAppearance.Layers(native))if(layer.texture!=null)batch.Draw(layer.texture,new Vector2(25,232)-layer.source.Size.ToVector2()*layer.center,layer.source,Color.White);
                    batch.End();device.SetRenderTarget(null);using(var stream=File.Create(Path.Combine(scratch,"quest-crawler-attack.png")))target.SaveAsPng(stream,target.Width,target.Height);
                    var pixels=new Color[480*240];target.GetData(pixels);Check(pixels.Count(c=>c.R!=30||c.G!=37||c.B!=46)>1000,"Native GPU renders both crawler facings and attack poses");
                    Console.WriteLine("[OK] Quest crawler: animated atlas, native GPU and mirrored attack poses");
                }
            }
        }
    }
}
