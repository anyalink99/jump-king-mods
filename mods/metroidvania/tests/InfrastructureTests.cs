using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;
using Microsoft.Xna.Framework;

namespace Metroidvania
{
    internal static partial class MetroidvaniaTests
    {
        private static AttackDefinition ActionData(int clip,int damage)
        {var doc=new XmlDocument();doc.LoadXml("<Attack Id='fixture-strike' Clip='"+clip+"' Weight='1' ActiveTick='102' Damage='"+damage+"'><Sounds/></Attack>");return new AttackDefinition(doc.DocumentElement);}
        private static byte[] ExtraAttack(byte[] source)
        {
            using(var input=new BinaryReader(new MemoryStream(source,false)))using(var memory=new MemoryStream())using(var output=new BinaryWriter(memory)){
                output.Write(input.ReadBytes(4));int fps=input.ReadInt32(),clips=input.ReadInt32(),frames=input.ReadInt32(),hurt=input.ReadInt32(),attack=input.ReadInt32();output.Write(fps);output.Write(clips+1);output.Write(frames);output.Write(hurt);output.Write(attack);
                int first=0,count=0,ticks=0;for(int i=0;i<clips;i++){first=input.ReadInt32();count=input.ReadInt32();ticks=input.ReadInt32();output.Write(first);output.Write(count);output.Write(ticks);}output.Write(frames);output.Write(count);output.Write(ticks);
                int stride=4+(hurt+attack)*8;byte[] poses=input.ReadBytes(frames*stride);output.Write(poses);output.Write(poses,first*stride,count*stride);output.Flush();byte[] result=memory.ToArray();Array.Copy(BitConverter.GetBytes(frames+count),0,result,12,4);return result;
            }
        }
        private static void Infrastructure()
        {
            Check(Catalog.Find("tonic").Heal==40,"Consumable behavior comes from the item definition");
            Check(Catalog.Find("worn-blade").LightAttack.Start==CombatStats.From(new CharacterProfile()).LightStart,"Player stats resolve their weapon action definition");
            Check(Catalog.Visual(GearSlot.Shield,1).Id=="buckler"&&Catalog.Visual(GearSlot.Body,2).Id=="iron-coat","Existing appearance tokens keep their saved meaning");
            bool frozen=false;try{((IList<ItemDefinition>)Catalog.Items)[0]=Catalog.Items[1];}catch(NotSupportedException){frozen=true;}Check(frozen,"Published item catalogue can't be changed by a consumer");
            var recipe=(EnemyRecipe)typeof(object).GetMethod("MemberwiseClone",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(CrawlerData.Recipe,null);
            recipe.Id="fixture-other-melee";recipe.Index=2;recipe.Health=320;recipe.Width=24;recipe.AnchorX=12;recipe.Attacks=new[]{ActionData(4,15),ActionData(5,9)};
            recipe.DeathSounds=new[]{new SoundMarker(0,ContentCatalog.Cue("punch")),new SoundMarker(1,ContentCatalog.Cue("block"))};recipe.WalkSounds=new[]{new SoundMarker(0,ContentCatalog.Cue("block"))};recipe.IdleSounds=new[]{new SoundMarker(0,ContentCatalog.Cue("punch"))};
            byte[] bytes;using(var source=typeof(EnemyDefinition).Assembly.GetManifestResourceStream(CrawlerData.Recipe.Data))using(var memory=new MemoryStream()){source.CopyTo(memory);bytes=memory.ToArray();}
            var alternate=new EnemyDefinition(new MemoryStream(ExtraAttack(bytes),false),recipe);
            var b=new Battle(CombatStats.From(new CharacterProfile()),new[]{CrawlerData,alternate});
            Check(b.Spawn(new Vector2(36,78),0,80,"other",alternate.Id),"Another archetype uses the same spawn command");
            Check(b.Spawn(new Vector2(300,78),200,400,"original",CrawlerData.Id),"Two archetypes share one battle");
            Check(b.Enemies[0].MaximumHealth==320&&b.Enemies[0].Bounds.Width==24&&b.Enemies[1].MaximumHealth==200,"Instances use their own shared definition rather than a named branch");
            b.Enemies[0].AttackSerial=1;Step(b,105);
            Check(b.Enemies[0].AttackSlot==1&&b.Player.Health==100-Math.Max(1,9-b.Stats.Armor),"The common controller selects and executes another data-defined attack");
            var saved=b.Capture();var restored=new Battle(CombatStats.From(new CharacterProfile()),new[]{alternate,CrawlerData});restored.Restore(saved);Step(b,130);Step(restored,130);Check(Snapshot(b)==Snapshot(restored),"Mixed archetypes and selected attacks restore without replaying logic");
            var missing=new Battle(CombatStats.From(new CharacterProfile()),CrawlerData);Reject(()=>missing.Restore(saved),"A missing definition rejects restoration before mutation");
            int before=b.Enemies.Count;Check(!b.Spawn(new Vector2(20,78),0,80,"missing","missing")&&b.Enemies.Count==before,"Unknown archetype can't create a partial actor");
            var death=new Battle(CombatStats.From(new CharacterProfile()),alternate);death.Spawn(new Vector2(36,78),0,80);death.Enemies[0].Health=1;death.Player.Invulnerable=1000;Send(death,1,CombatCommandKind.Light);Step(death,8);
            Check((CueMask(death,"worn-blade")&(1UL<<ContentCatalog.Cue("punch")))!=0,"Defeat presents a definition's start marker");Step(death,1);Check((CueMask(death,"worn-blade")&(1UL<<ContentCatalog.Cue("block")))!=0,"Defeat advances its own sound timeline");var ended=death.Capture();death.Restore(ended);Check(death.EventCount==0,"Restoring a corpse doesn't replay defeat sounds");Step(death,1);Check(death.EventCount==0,"Consumed defeat markers don't repeat");
            var loops=new Battle(CombatStats.From(new CharacterProfile()),alternate);loops.Spawn(new Vector2(300,78),200,400);Step(loops,1);Check((CueMask(loops,"worn-blade")&(1UL<<ContentCatalog.Cue("block")))!=0,"Walking presents a frame-zero loop marker");loops.Step(King,r=>true);Check((CueMask(loops,"worn-blade")&(1UL<<ContentCatalog.Cue("punch")))!=0,"Returning to idle presents its own frame-zero marker");
            var stats=CombatStats.From(new CharacterProfile());stats.LightStart=3;stats.LightActive=2;var timed=new Battle(stats,CrawlerData);Send(timed,1,CombatCommandKind.Light);Step(timed,2);Check(CueMask(timed,"worn-blade")==0,"Alternate weapon timing has no premature swing");Step(timed,1);Check((CueMask(timed,"worn-blade")&(1UL<<Catalog.Find("worn-blade").LightCue))!=0,"Action timing is read from resolved stats");
        }
    }
}
