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
        private sealed class SampledSprite:Sprite
        {internal Point Offset; internal SampledSprite(Texture2D value,Point offset){texture=value;source=value.Bounds;Offset=offset;center=new Vector2(-offset.X/(float)value.Width,-offset.Y/(float)value.Height);}
            public override void Draw(Vector2 anchor,SpriteEffects effects=SpriteEffects.None){Game1.spriteBatch.Draw(texture,PlayerTopLeft(this,anchor),GetColor());}}
        private static Vector2 PlayerTopLeft(Sprite sprite,Vector2 anchor)
        {var sample=sprite as SampledSprite;return sample==null?anchor-sprite.source.Size.ToVector2()*sprite.center:new Vector2((float)Math.Floor(anchor.X)+sample.Offset.X,(float)Math.Floor(anchor.Y)+sample.Offset.Y);}
        private PlayerEntity sampledPlayer;
        private Sprite sampledAppearance;
        private int sampledSignature;
        private int sampledBodySignature;
        private SpriteEffects sampledFlip;

        private void PreparePlayerAppearance()
        {
            if(!MappingSettings.Enabled || (scene.ShadowSurfaces.Length==0 && scene.Waters.Length==0 && scene.Options.PlayerRimOpacity<=0 && reflectionSources.Count==0))return;
            var player=GameLoopPlayer();if(player==null)return;
            var body=JKRuntime.Presentation.PlayerAppearance.Resolve(player);
            int bodySignature=AppearanceVersion(body.Sprite)^(int)body.Facing;
            if(sampledPlayer==player&&sampledAppearance!=null&&sampledBodySignature==bodySignature&&JKRuntime.Presentation.PlayerAppearance.IsStatic(body.Sprite))return;
            DisposePlayerAppearances();
            Sprite sprite;Texture2D texture;Rectangle source;SpriteEffects flip;
            TryPlayerSprite(player,out sprite,out texture,out source,out flip);
            sampledPlayer=player;sampledAppearance=sprite;sampledSignature=AppearanceVersion(JKRuntime.Presentation.PlayerAppearance.BaseSprite(player));
            sampledFlip=flip;sampledBodySignature=bodySignature;
        }
        private bool TryPlayerSprite(PlayerEntity player,out Sprite sprite,out Texture2D texture,out Rectangle source,out SpriteEffects flip)
        {
            if(player!=null && sampledPlayer==player && sampledAppearance!=null && sampledSignature==AppearanceVersion(JKRuntime.Presentation.PlayerAppearance.BaseSprite(player)))
            {sprite=sampledAppearance;texture=sprite.texture;source=sprite.source;flip=sampledFlip;return true;}
            NativeSceneAdapter.TrySprite(player,out sprite,out texture,out source,out flip);
            if(player==null)return false;
            sprite=JKRuntime.Presentation.PlayerAppearance.Resolve(player).Sprite;
            if(sprite!=null&&sprite.GetType()==typeof(Sprite)&&sprite.texture!=null){texture=sprite.texture;source=sprite.source;return true;}
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
                var frame=JKRuntime.Presentation.PlayerAppearance.Resolve(player);
                JKRuntime.Presentation.AppearancePixels pixels;
                try {pixels=JKRuntime.Presentation.AppearanceCapture.Read(frame,128);}
                catch {pixels=JKRuntime.Presentation.AppearanceCapture.Read(frame,512,true);}
                var packed=pixels.CreateTexture(Game1.instance.GraphicsDevice);
                appearance=new SampledSprite(packed,pixels.Offset);
                playerAppearances.Add(native,appearance);
                playerAppearanceVersions.Add(native,version);
            }
            sprite=appearance;texture=appearance.texture;source=appearance.source;flip=SpriteEffects.None;return true;
        }
        private static int AppearanceVersion(Sprite sprite)
        {return JKRuntime.Presentation.PlayerAppearance.Signature(sprite)^(int)JKRuntime.Presentation.PlayerAppearance.Revision;}
        // native MonoGame atlases contain premultiplied channels. Compose once
        // per animation frame, then share the result across rim and shadow passes
        internal static Color AppearanceOver(Color foreground,Color background)
        {
            int inverse=255-foreground.A;
            return new Color((byte)Math.Min(255,foreground.R+background.R*inverse/255),
                (byte)Math.Min(255,foreground.G+background.G*inverse/255),
                (byte)Math.Min(255,foreground.B+background.B*inverse/255),
                (byte)Math.Min(255,foreground.A+background.A*inverse/255));
        }
        private void DisposePlayerAppearances()
        {sampledAppearance=null;sampledPlayer=null;foreach(Sprite sprite in playerAppearances.Values){Texture2D mask;if(silhouetteTextures.TryGetValue(sprite.texture,out mask)){mask.Dispose();silhouetteTextures.Remove(sprite.texture);}sprite.texture.Dispose();}playerAppearances.Clear();playerAppearanceVersions.Clear();playerAtlasPixels.Clear();}
    }
}
