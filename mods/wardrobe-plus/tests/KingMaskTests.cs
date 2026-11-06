using System;
using System.Linq;
using WardrobePlus;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

internal static partial class WardrobeTests
{
    private static void KingMaskGraphicsTests(GraphicsDevice device,SpriteBatch batch,RenderTarget2D target)
    {
        var bird = OwnershipMasks.For(NativeAppearance.BaseItem,4,13,64,96);
        var couple = OwnershipMasks.For(NativeAppearance.BaseItem,2,0,32,32);
        Check(!bird.Visible[37*64+32]&&!bird.Occluded[37*64+32],"The bird above NBP King is separate from the vessel geometry");
        Check(!couple.Visible[17*32+17]&&couple.Occluded[17*32+17],"A carried Babe is protected while filling the hidden King contour");
        Check(!couple.Visible[4*32+14],"A coronation crown is excluded from the base reskin");
        var outfit=new Outfit();var catalog=Controller.Catalog;
        using(var original=PreparedAppearance.Build(outfit,catalog,false))
        foreach(var kind in new[]{MaterialKind.Gold,MaterialKind.Diamond,MaterialKind.RedVelvet,MaterialKind.Cosmic})
        {
            outfit.SetMaterial(NativeAppearance.BaseItem,kind);
            using(var changed=PreparedAppearance.Build(outfit,catalog,false))
            {
                bool preserved=true,live=true;int protectedPixels=0;
                for(int group=0;group<original.Base.m_groups.Count;group++)
                foreach(var pair in NativeAppearance.Frames(original.Base.m_groups[group]))
                {
                    var before=FramePixels(pair.Value);var sprite=NativeAppearance.Frames(changed.Base.m_groups[group])[pair.Key];var after=FramePixels(sprite);
                    var ownership=OwnershipMasks.For(NativeAppearance.BaseItem,group,pair.Key,pair.Value.source.Width,pair.Value.source.Height);
                    var mask=ownership==null?null:ownership.Visible;
                    for(int p=0;p<before.Length;p++)if(mask==null||!mask[p]){preserved &= before[p]==after[p];if(before[p].A>0)protectedPixels++;}
                    if(kind!=MaterialKind.Cosmic)continue;
                    foreach(var flip in new[]{SpriteEffects.None,SpriteEffects.FlipHorizontally})
                    {
                        Color[][] draws=new Color[2][];
                        for(int pass=0;pass<2;pass++)
                        {
                            device.SetRenderTarget(target);device.Clear(Color.Transparent);
                            batch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.PointClamp);
                            (pass==0?pair.Value:sprite).Draw(new Vector2(240,200),flip);batch.End();device.SetRenderTarget(null);
                            draws[pass]=new Color[480*360];target.GetData(draws[pass]);
                        }
                        int w=pair.Value.source.Width,h=pair.Value.source.Height;
                        var top=(new Vector2(240,200)-new Vector2(w,h)*pair.Value.center).ToPoint();
                        for(int y=0;y<h;y++)for(int x=0;x<w;x++)if(mask==null||!mask[y*w+x])
                        {
                            int screenX=top.X+(flip==SpriteEffects.None?x:w-1-x),index=(top.Y+y)*480+screenX;
                            live &= draws[0][index]==draws[1][index];
                        }
                    }
                }
                Check(preserved&&protectedPixels>1000,kind+" leaves protected NPC, bird and prop pixels byte-identical in every native atlas frame");
                if(kind==MaterialKind.Cosmic)Check(live,"Cosmic's live shader preserves protected pixels in both facing directions");
            }
        }
    }
}
