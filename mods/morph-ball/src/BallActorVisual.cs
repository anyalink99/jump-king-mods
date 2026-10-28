using System;
using System.IO;
using JumpKing;
using JumpKing.Player;
using JKRuntime.Presentation;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MorphBallMod
{
    internal sealed class BallActorVisual : IActorVisual
    {
        private Texture2D texture;
        private Sprite charge;
        private long revision=-1;
        internal static byte[] Capture(MorphController controller)
        {
            if(controller==null||controller.MorphAmount<=0)return null;
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
            {
                writer.Write(controller.MorphAmount);writer.Write(controller.RollAngle);writer.Write(controller.StickyAttached);
                var normal=controller.ContactNormal;var offset=controller.GetVisualContactOffset(OutfitTextureBuilder.BallSize/2f);
                writer.Write(normal.X);writer.Write(normal.Y);writer.Write(offset.X);writer.Write(offset.Y);
                return stream.ToArray();
            }
        }
        public Sprite Compose(Sprite outfit,Sprite chargeOutfit,byte[] state)
        {
            if(state.Length!=25)throw new InvalidDataException("Invalid Ball King visual state");
            using(var reader=new BinaryReader(new MemoryStream(state)))
            {
                float amount=Read(reader,1),angle=Read(reader,100000000);byte attached=reader.ReadByte();
                var normal=new Vector2(Read(reader,1),Read(reader,1));var offset=new Vector2(Read(reader,64),Read(reader,64));
                if(amount<0||attached>1)throw new InvalidDataException("Invalid Ball King visual state");
                if(texture==null||texture.IsDisposed||!object.ReferenceEquals(charge,chargeOutfit)||revision!=PlayerAppearance.Revision)
                {
                    var next=OutfitTextureBuilder.Build(chargeOutfit);if(texture!=null)texture.Dispose();texture=next;
                    charge=chargeOutfit;revision=PlayerAppearance.Revision;
                }
                return new MorphSprite(outfit,texture,outfit,amount,angle,attached!=0,normal,offset);
            }
        }
        private static float Read(BinaryReader reader,float limit)
        {float value=reader.ReadSingle();if(float.IsNaN(value)||Math.Abs(value)>limit)throw new InvalidDataException("Invalid Ball King visual number");return value;}
        public void Dispose(){if(texture!=null)texture.Dispose();texture=null;}
    }
}
