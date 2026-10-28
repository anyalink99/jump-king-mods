using System;
using System.Collections.Generic;
using JKRuntime.Presentation;
using JumpKing;
using JumpKing.JKMemory.KingSpriteLayers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Metroidvania
{
    internal sealed class EquipmentArt : IDisposable
    {
        private static EquipmentArt current;
        private int users;
        internal readonly Texture2D Icons,Weapons,Armor;
        private readonly Dictionary<Sprite,byte> poses=new Dictionary<Sprite,byte>();
        private Sprite idle;
        private static readonly Vector2[] Hands={new Vector2(8,-11),new Vector2(8,-13),new Vector2(10,-11),new Vector2(8,-11),new Vector2(8,-4),new Vector2(5,-21),new Vector2(8,-14),new Vector2(8,-12),new Vector2(4,-4),new Vector2(9,-21),new Vector2(10,-15),new Vector2(9,-12),new Vector2(8,-11)};
        private EquipmentArt()
        {
            try{Icons=Read("icons");Weapons=Read("weapons");Armor=Read("armor");}
            catch{Dispose();throw;}
        }
        private static Texture2D Read(string name)
        {using(var stream=typeof(EquipmentArt).Assembly.GetManifestResourceStream("Metroidvania.Art."+name)){if(stream==null)throw new InvalidOperationException("Missing equipment atlas: "+name);return Texture2D.FromStream(Game1.spriteBatch.GraphicsDevice,stream);}}
        internal static IDisposable Acquire()
        {
            if(current==null||current.Icons.IsDisposed||!ReferenceEquals(current.Icons.GraphicsDevice,Game1.spriteBatch.GraphicsDevice))current=new EquipmentArt();
            current.users++;return new Lease(current);
        }
        private sealed class Lease : IDisposable
        {
            private EquipmentArt art;
            internal Lease(EquipmentArt owner){art=owner;}
            public void Dispose(){if(art==null)return;var owner=art;art=null;if(--owner.users==0){owner.Dispose();if(ReferenceEquals(current,owner))current=null;}}
        }
        internal static byte Pose(Sprite sprite)
        {
            if(current==null)return 0;
            var native=Game1.instance.contentManager.playerSprites;
            if(!ReferenceEquals(current.idle,native.idle)){
                current.idle=native.idle;current.poses.Clear();for(byte i=0;i<13;i++)current.poses[native._CurrentSprites.regular.GetSprite((Regular.SpriteKey)i)]=i;
            }
            byte pose;return sprite!=null&&current.poses.TryGetValue(sprite,out pose)?pose:(byte)0;
        }
        internal static void Icon(int index,Rectangle bounds)
        {if(current!=null&&index>=0&&index<current.Icons.Width/32)Game1.spriteBatch.Draw(current.Icons,bounds,new Rectangle(index*32,0,32,32),Color.White);}
        internal static void Draw(Vector2 anchor,int facing,byte[] state,Color tint,float scale=1)
        {
            if(current==null||state==null)return;SpriteEffects flip=facing==1?SpriteEffects.None:SpriteEffects.FlipHorizontally;
            int pose=state[5];var body=Catalog.Visual(GearSlot.Body,state[4]);var weapon=Catalog.Visual(GearSlot.Weapon,state[0]);var shield=Catalog.Visual(GearSlot.Shield,state[1]);
            if(body!=null)Game1.spriteBatch.Draw(current.Armor,anchor,new Rectangle(body.ArtIndex*192+(pose%4)*48,(pose/4)*48,48,48),tint,0,new Vector2(24,48),scale,flip,0);
            int light=weapon==null?ContentCatalog.LightStart:weapon.LightAttack.Start,lightActive=weapon==null?ContentCatalog.LightActive:weapon.LightAttack.Active,heavy=weapon==null?ContentCatalog.HeavyStart:weapon.HeavyAttack.Start,heavyActive=weapon==null?ContentCatalog.HeavyActive:weapon.HeavyAttack.Active;
            var action=(CombatAction)state[2];int age=state[3];bool swing=action==CombatAction.Light&&age>=light&&age<light+lightActive||action==CombatAction.Heavy&&age>=heavy&&age<heavy+heavyActive;
            bool prepare=action==CombatAction.Light&&age<light||action==CombatAction.Heavy&&age<heavy;
            var hand=anchor+new Vector2(facing*Hands[pose].X,Hands[pose].Y)*scale;
            if(weapon!=null){float angle=(swing?MathHelper.PiOver2:prepare?-.65f:pose==8?MathHelper.PiOver2:0)*facing;Game1.spriteBatch.Draw(current.Weapons,hand,new Rectangle(weapon.ArtIndex*64,0,64,64),tint,angle,new Vector2(32,42),scale,flip,0);}
            if(shield!=null)Game1.spriteBatch.Draw(current.Weapons,anchor+new Vector2(facing*(action==CombatAction.Guard?12:-8),pose==8?-4:pose==4?-6:-12)*scale,new Rectangle(shield.ArtIndex*64,0,64,64),tint,0,new Vector2(32,42),scale,flip,0);
        }
        public void Dispose(){if(Icons!=null)Icons.Dispose();if(Weapons!=null)Weapons.Dispose();if(Armor!=null)Armor.Dispose();}
    }
}
