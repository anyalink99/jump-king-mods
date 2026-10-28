using System;
using System.IO;
using JumpKing;
using JumpKing.Player;
using JKRuntime.Presentation;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace HammerKing
{
    internal sealed class HammerActorVisual : IActorVisual
    {
        internal static byte[] Capture(PlayerEntity player)
        {var visual=player.GetComponent<HammerVisual>();return visual==null?null:visual.Capture();}
        public Sprite Compose(Sprite outfit,Sprite chargeOutfit,byte[] state)
        {
            if(state==null||state.Length!=9||state[8]>1)throw new InvalidDataException("Invalid Hammer visual state");
            var offset=new Vector2(BitConverter.ToSingle(state,0),BitConverter.ToSingle(state,4));
            if(float.IsNaN(offset.X)||float.IsNaN(offset.Y)||Math.Abs(offset.X)>256||Math.Abs(offset.Y)>256)
                throw new InvalidDataException("Invalid Hammer head offset");
            return ActorVisuals.Layers(new HammerActorSprite(offset,state[8]!=0),outfit);
        }
        public void Dispose(){}
        private sealed class HammerActorSprite : Sprite
        {
            private readonly Vector2 offset;
            private readonly bool contact;
            internal HammerActorSprite(Vector2 value,bool touching){offset=value;contact=touching;}
            public override void Draw(Vector2 anchor,SpriteEffects facing=SpriteEffects.None)
            {HammerSprite.DrawHammer(Game1.spriteBatch,Game1.instance.contentManager.Pixel.texture,anchor+new Vector2(0,-13),anchor+offset,contact,GetColor());}
        }
    }
}
