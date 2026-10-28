using System;
using System.IO;
using System.Security.Cryptography;
using Microsoft.Xna.Framework;

namespace Metroidvania
{
    internal struct EnemyPose {internal float Root;internal Rectangle[] Hurt,Attack;}
    public sealed class EnemyDefinition
    {
        private sealed class Clip {internal int First,Count,Ticks;}
        private readonly Clip[] clips;
        private readonly EnemyPose[] frames;
        internal readonly EnemyRecipe Recipe;
        public readonly string ContentHash,Id,CatalogHash;
        public readonly int Index;
        internal int FrameCount {get{return frames.Length;}}
        internal int DeathTicks {get{return Math.Min(Recipe.Death,clips[3].Ticks);}}
        internal AttackDefinition[] Attacks {get{return Recipe.Attacks;}}
        internal EnemyDefinition(Stream source,EnemyRecipe recipe)
        {
            Recipe=recipe;Id=recipe.Id;Index=recipe.Index;CatalogHash=ContentCatalog.Hash;
            byte[] bytes;using(var memory=new MemoryStream()){source.CopyTo(memory);bytes=memory.ToArray();}
            using(var hash=SHA256.Create())ContentHash=BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();
            using(var reader=new BinaryReader(new MemoryStream(bytes,false))){
                if(new string(reader.ReadChars(4))!="MVE2")throw new InvalidDataException("Unsupported enemy table");int fps=reader.ReadInt32(),clipCount=reader.ReadInt32(),count=reader.ReadInt32(),hurt=reader.ReadInt32(),attack=reader.ReadInt32();
                if(fps<5||fps>60||60%fps!=0||clipCount!=4+recipe.Attacks.Length||count<clipCount||count>2048||hurt<1||hurt>8||attack<1||attack>8)throw new InvalidDataException("Enemy table budget");FrameTicks=60/fps;
                clips=new Clip[clipCount];int next=0;
                for(int i=0;i<clips.Length;i++){var clip=new Clip{First=reader.ReadInt32(),Count=reader.ReadInt32(),Ticks=reader.ReadInt32()};if(clip.First!=next||clip.Count<1||clip.Ticks<1||clip.Ticks>1800||clip.First+clip.Count>count)throw new InvalidDataException("Invalid enemy clip");clips[i]=clip;next+=clip.Count;}
                if(next!=count)throw new InvalidDataException("Missing enemy frames");frames=new EnemyPose[count];
                for(int i=0;i<count;i++){float root=reader.ReadSingle();if(float.IsNaN(root)||float.IsInfinity(root)||Math.Abs(root)>256)throw new InvalidDataException("Invalid enemy root motion");var pose=new EnemyPose{Root=root,Hurt=new Rectangle[hurt],Attack=new Rectangle[attack]};for(int j=0;j<hurt;j++)pose.Hurt[j]=Box(reader);for(int j=0;j<attack;j++)pose.Attack[j]=Box(reader);frames[i]=pose;}
                if(reader.BaseStream.Position!=reader.BaseStream.Length)throw new InvalidDataException("Trailing enemy data");
                foreach(var action in recipe.Attacks)if(action.Clip<4||action.Clip>=clips.Length||action.ActiveTick>=clips[action.Clip].Ticks)throw new InvalidDataException("Invalid attack timeline");
            }
        }
        internal readonly int FrameTicks;
        private static Rectangle Box(BinaryReader reader)
        {int x=reader.ReadInt16(),y=reader.ReadInt16(),w=reader.ReadInt16(),h=reader.ReadInt16();if(Math.Abs(x)>256||Math.Abs(y)>256||w<0||h<0||w>256||h>256)throw new InvalidDataException("Invalid enemy collider");return new Rectangle(x,y,w,h);}
        internal static EnemyDefinition Load(string id=null)
        {ContentCatalog.Prepare();var recipe=ContentCatalog.Enemy(id??ContentCatalog.DefaultEnemy);if(recipe==null)throw new InvalidDataException("Unknown enemy definition: "+id);using(var source=typeof(EnemyDefinition).Assembly.GetManifestResourceStream(recipe.Data)){if(source==null)throw new InvalidDataException("Missing enemy table: "+recipe.Id);return new EnemyDefinition(source,recipe);}}
        internal int ClipTicks(int clip){return clips[clip].Ticks;}
        internal int FrameIndex(FighterState actor)
        {
            int state=actor.Health==0?3:actor.Action==CombatAction.Light?Attacks[actor.AttackSlot].Clip:actor.Action==CombatAction.Hurt?2:actor.Moving?1:0;
            int tick=state<2?actor.AnimationTick:actor.Age;var clip=clips[state];if(state<2)tick%=clip.Ticks;if(state==3)tick=Math.Min(tick,Recipe.Hold);
            return clip.First+Math.Min(clip.Count-1,Math.Max(0,tick)/FrameTicks);
        }
        internal EnemyPose Pose(FighterState actor){return frames[FrameIndex(actor)];}
        internal float AttackMotion(int slot,int age)
        {var clip=clips[Attacks[slot].Clip];int frame=Math.Min(clip.Count-1,Math.Max(0,age)/FrameTicks);float a=frames[clip.First+frame].Root,b=frames[clip.First+Math.Min(clip.Count-1,frame+1)].Root;return MathHelper.Lerp(a,b,(age%FrameTicks)/(float)FrameTicks);}
        internal static Vector2 Anchor(FighterState actor){return actor.Position+new Vector2(actor.AnchorX,actor.AnchorY);}
        internal static Rectangle WorldBox(FighterState actor,Rectangle local)
        {var p=Anchor(actor);return new Rectangle((int)Math.Floor(p.X)+(actor.Facing>0?local.X:-local.Right),(int)Math.Floor(p.Y)+local.Y,local.Width,local.Height);}
    }
}
