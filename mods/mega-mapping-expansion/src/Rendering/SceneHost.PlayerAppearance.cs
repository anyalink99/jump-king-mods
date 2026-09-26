using System;
using System.Collections.Generic;
using JumpKing;
using JumpKing.Player;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MegaMappingExpansion
{
    internal sealed partial class SceneHost
    {
        private readonly Dictionary<Sprite,Sprite> playerAppearances=new Dictionary<Sprite,Sprite>();
        private readonly Dictionary<Sprite,int> playerAppearanceVersions=new Dictionary<Sprite,int>();
        private readonly Dictionary<Texture2D,Color[]> playerAtlasPixels=new Dictionary<Texture2D,Color[]>();

        private bool TryPlayerSprite(PlayerEntity player,out Sprite sprite,out Texture2D texture,out Rectangle source,out SpriteEffects flip)
        {
            if(NativeSceneAdapter.TrySprite(player,out sprite,out texture,out source,out flip))return true;
            if(sprite==null)return false;
            Sprite native=sprite,appearance;
            int version=AppearanceVersion(native);
            int previousVersion;
            if(playerAppearanceVersions.TryGetValue(native,out previousVersion) && previousVersion!=version)
            {
                var obsolete=playerAppearances[native];Texture2D oldMask;
                if(silhouetteTextures.TryGetValue(obsolete.texture,out oldMask))
                {oldMask.Dispose();silhouetteTextures.Remove(obsolete.texture);}
                obsolete.texture.Dispose();playerAppearances.Remove(native);playerAppearanceVersions.Remove(native);
            }
            if(!playerAppearances.TryGetValue(native,out appearance))
            {
                var parts=new List<Sprite>();CollectAppearance(native,parts);
                if(parts.Count==0)return false;
                Rectangle bounds=Rectangle.Empty;
                foreach(Sprite part in parts)
                {
                    var rect=new Rectangle((-part.source.Size.ToVector2()*part.center).ToPoint(),part.source.Size);
                    bounds=bounds.IsEmpty?rect:Rectangle.Union(bounds,rect);
                }
                if(bounds.Width<1 || bounds.Height<1 || bounds.Width>512 || bounds.Height>512)return false;
                var composed=new Color[bounds.Width*bounds.Height];
                foreach(Sprite part in parts)
                {
                    Color[] atlas;
                    if(!playerAtlasPixels.TryGetValue(part.texture,out atlas))
                    {atlas=new Color[part.texture.Width*part.texture.Height];part.texture.GetData(atlas);playerAtlasPixels.Add(part.texture,atlas);}
                    Point offset=(-part.source.Size.ToVector2()*part.center).ToPoint()-bounds.Location;
                    for(int y=0;y<part.source.Height;y++)for(int x=0;x<part.source.Width;x++)
                    {
                        int index=(offset.Y+y)*bounds.Width+offset.X+x;
                        Color foreground=atlas[(part.source.Y+y)*part.texture.Width+part.source.X+x];
                        composed[index]=AppearanceOver(foreground,composed[index]);
                    }
                }
                var packed=new Texture2D(Game1.instance.GraphicsDevice,bounds.Width,bounds.Height);packed.SetData(composed);
                appearance=Sprite.CreateSpriteWithCenter(packed,packed.Bounds,new Vector2(-bounds.X/(float)bounds.Width,-bounds.Y/(float)bounds.Height));
                playerAppearances.Add(native,appearance);
                playerAppearanceVersions.Add(native,version);
            }
            sprite=appearance;texture=appearance.texture;source=appearance.source;return true;
        }
        private static int AppearanceVersion(Sprite sprite)
        {
            unchecked
            {
                int hash=sprite.source.GetHashCode()*397 ^ sprite.center.GetHashCode();
                if(sprite.texture!=null)hash=hash*397 ^ System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(sprite.texture);
                var parts=NativeSceneAdapter.SpriteParts(sprite);
                if(parts!=null)foreach(Sprite part in parts)hash=hash*397 ^ AppearanceVersion(part);
                return hash;
            }
        }
        private static void CollectAppearance(Sprite sprite,List<Sprite> parts)
        {
            var children=NativeSceneAdapter.SpriteParts(sprite);
            if(children!=null){foreach(Sprite child in children)CollectAppearance(child,parts);}
            else if(sprite.texture!=null && sprite.source.Width>0 && sprite.source.Height>0)parts.Add(sprite);
        }
        // Native MonoGame atlases contain premultiplied channels. Compose once
        // per animation frame, then share the result across rim and shadow passes.
        internal static Color AppearanceOver(Color foreground,Color background)
        {
            int inverse=255-foreground.A;
            return new Color((byte)Math.Min(255,foreground.R+background.R*inverse/255),
                (byte)Math.Min(255,foreground.G+background.G*inverse/255),
                (byte)Math.Min(255,foreground.B+background.B*inverse/255),
                (byte)Math.Min(255,foreground.A+background.A*inverse/255));
        }
        private void DisposePlayerAppearances()
        {foreach(Sprite sprite in playerAppearances.Values)sprite.texture.Dispose();playerAppearances.Clear();playerAppearanceVersions.Clear();playerAtlasPixels.Clear();}
    }
}
