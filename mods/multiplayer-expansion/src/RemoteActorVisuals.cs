using System;
using System.Collections.Generic;
using JumpKing;
using JumpKing.JKMemory;
using JumpKing.JKMemory.KingSpriteLayers;
using JumpKing.Player.Skins;
using JumpKing.MiscEntities.WorldItems;
using JKRuntime.Presentation;

namespace MultiplayerExpansion
{
    internal static class RemoteActorVisuals
    {
        private static readonly Type skins=typeof(Game1).Assembly.GetType("JumpKing.Player.Skins.SkinManager",true);
        private static readonly System.Reflection.FieldInfo art=skins.GetField("m_king_sprites",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public);
        private static readonly System.Reflection.FieldInfo settings=skins.GetField("m_settings",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public);
        private static readonly SkinLayer[] order={SkinLayer.Cape,SkinLayer.Boots,SkinLayer.Shirt,SkinLayer.SnakeRing,SkinLayer.Hat};
        internal sealed class OutfitSet
        {
            private readonly Dictionary<int,Sprite> outfits=new Dictionary<int,Sprite>();
            private object native;
            private long revision=-1;
            private ulong equipment;
            internal Sprite Outfit(Regular.SpriteKey pose,ulong worn)
            {
            var current=Game1.instance.contentManager.playerSprites._CurrentSprites;
            if(!ReferenceEquals(native,current)||revision!=PlayerAppearance.Revision||equipment!=worn){outfits.Clear();native=current;revision=PlayerAppearance.Revision;equipment=worn;}
            int key=(int)pose;Sprite sprite;if(outfits.TryGetValue(key,out sprite))return sprite;
            var parts=new List<Sprite>();parts.Add(RemotePresentation.BaseSprite(current.regular.GetSprite(pose)));
            var available=art.GetValue(null) as Dictionary<Items,KingSprites>;var definition=(SkinSettings)settings.GetValue(null);
            if(available!=null&&definition.skins!=null)
                foreach(var layer in order)foreach(var skin in definition.skins)
                {
                    int item=(int)skin.item;KingSprites sprites;
                    if(item>=0&&item<18&&(equipment&(1UL<<item))!=0&&skin.layers.Length>0&&skin.layers[0]==layer&&available.TryGetValue(skin.item,out sprites))
                        parts.Add(sprites.regular.GetSprite(pose));
                }
            sprite=ActorVisuals.Layers(parts.ToArray());outfits[key]=sprite;return sprite;
            }
        }
        internal static void Prepare(InteractionPeer peer,InteractionFrame frame,Regular.SpriteKey pose)
        {
            if(frame.VisualState==null||frame.VisualState.Length==0)
            {peer.ReleaseAppearance();return;}
            if(peer.Actor==null)peer.Actor=ActorVisuals.Create();
            if(peer.Outfits==null)peer.Outfits=new OutfitSet();
            var canonical=JKRuntime.Geometry.MapTopology.Normalize(frame.Position,frame.Width,frame.Height);
            var facing=frame.Flip==1?Microsoft.Xna.Framework.Graphics.SpriteEffects.FlipHorizontally:frame.Flip==2?Microsoft.Xna.Framework.Graphics.SpriteEffects.FlipVertically:Microsoft.Xna.Framework.Graphics.SpriteEffects.None;
            bool cut=peer.AppearanceFrame!=null&&(peer.AppearanceFrame.Transition!=frame.Transition||Microsoft.Xna.Framework.Vector2.DistanceSquared(peer.AppearanceFrame.Position,frame.Position)>128*128);
            var local=JumpKing.GameManager.GameLoop.m_player;
            bool visible=local==null||Math.Abs(canonical.X-local.m_body.Position.X)<960&&Math.Abs(canonical.Y-local.m_body.Position.Y)<720;
            peer.Appearance=peer.Actor.Present(frame.VisualState,peer.Outfits.Outfit(pose,frame.Equipment),peer.Outfits.Outfit(Regular.SpriteKey.jump_charge,frame.Equipment),
                new ActorVisualFrame(AdvancedSession.Now,canonical+frame.AnchorOffset,frame.Velocity,facing,cut,frame.Presence==PlayerPresence.Paused,visible));
            peer.AppearanceFrame=frame;
        }
    }
}
