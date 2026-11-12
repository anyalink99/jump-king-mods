using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JumpKing;
using JumpKing.MiscEntities.WorldItems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using WardrobePlus;

internal static partial class WardrobeTests
{
    private static void EmbeddedItemGraphicsTests(GraphicsDevice device, SpriteBatch batch, RenderTarget2D target)
    {
        Check(OwnershipMasks.Embedded((int)Items.Crown,3,2,32,32).Visible[15*32+15],
            "The first reward crown belongs to Crown while being delivered, before equipment changes");
        Check(OwnershipMasks.Embedded((int)Items.CrownNBP,5,0,32,40).Visible[12*32+16]
            && OwnershipMasks.Embedded((int)Items.CrownNBP,5,25,32,40).Visible.Any(x=>x),
            "The NBP reward crown follows Babe from her head into her hands");
        Check(OwnershipMasks.Embedded((int)Items.CrownNBP,5,6,32,40)==null
            && OwnershipMasks.Embedded((int)Items.CrownNBP,4,18,32,32)==null,
            "Babe's replacement crown and its delivery remain unrelated to the player's reward crown");
        var cape = OwnershipMasks.Embedded((int)Items.CapeOwl,9,0,64,48);
        Check(cape.Visible[38*64+24] && !cape.Visible[15*64+32],
            "The third reward material covers the carried cape, not the owl's feathers or beak");

        using(var original = PreparedAppearance.Build(new Outfit(),Controller.Catalog,false))
        foreach(int owner in OwnershipMasks.EmbeddedItems)
        foreach(var kind in new[]{MaterialKind.Gold,MaterialKind.Diamond,MaterialKind.RedVelvet,MaterialKind.Cosmic})
        {
            var outfit = new Outfit(); outfit.SetMaterial(owner,kind);
            using(var changed = PreparedAppearance.Build(outfit,Controller.Catalog,false))
            {
                var cache = new Dictionary<Texture2D,Color[]>();
                bool protectedPixels=true, glass=true; int affected=0;
                for(int group=0;group<original.Base.m_groups.Count;group++)
                foreach(var frame in NativeAppearance.Frames(original.Base.m_groups[group]))
                {
                    var source=frame.Value;
                    var sprite=NativeAppearance.Frames(changed.Base.m_groups[group])[frame.Key];
                    var a=MaskFramePixels(source,cache); var b=MaskFramePixels(sprite,cache);
                    var mask=OwnershipMasks.Embedded(owner,group,frame.Key,source.source.Width,source.source.Height);
                    for(int p=0;p<a.Length;p++)
                    {
                        if(mask==null || !mask.Visible[p]) protectedPixels &= a[p]==b[p];
                        else if(a[p].A>0) { if(a[p]!=b[p])affected++; if(kind==MaterialKind.Diamond)glass &= b[p].A<a[p].A; }
                    }
                    if(kind==MaterialKind.Diamond && mask!=null)
                    {
                        var path=Path.Combine(output,"embedded-items"); Directory.CreateDirectory(path);
                        SaveEmbeddedFrame(device,source,Path.Combine(path,owner+"-"+group+"-"+frame.Key+"-original.png"));
                        SaveEmbeddedFrame(device,sprite,Path.Combine(path,owner+"-"+group+"-"+frame.Key+"-glass.png"));
                    }
                }
                Check(protectedPixels && affected>100 && glass, ((Items)owner)+" "+kind
                    +" affects embedded reward pixels across delivery and wearing while preserving all other artwork");
            }
        }
        MixedEndingMaterialTests(device,batch,target);
    }

    private static void SaveEmbeddedFrame(GraphicsDevice device,Sprite sprite,string path)
    {
        using(var texture=new Texture2D(device,sprite.source.Width,sprite.source.Height))
        {
            texture.SetData(FramePixels(sprite));
            using(var stream=File.Create(path)) texture.SaveAsPng(stream,texture.Width,texture.Height);
        }
    }

    private static void MixedEndingMaterialTests(GraphicsDevice device,SpriteBatch batch,RenderTarget2D target)
    {
        var plain=new Outfit(); plain.SetMaterial((int)Items.Crown,MaterialKind.Diamond);
        var mixed=plain.Copy(); mixed.SetMaterial(NativeAppearance.BaseItem,MaterialKind.Cosmic);
        using(var crown=PreparedAppearance.Build(plain,Controller.Catalog,false))
        using(var body=PreparedAppearance.Build(mixed,Controller.Catalog,false))
        {
            bool preserved=true;
            foreach(var flip in new[]{SpriteEffects.None,SpriteEffects.FlipHorizontally})
            {
                var sprites=new[]{NativeAppearance.Frames(crown.Base.m_groups[2])[10],NativeAppearance.Frames(body.Base.m_groups[2])[10]};
                var draws=new Color[2][];
                for(int i=0;i<2;i++)
                {
                    device.SetRenderTarget(target);device.Clear(Color.Transparent);
                    batch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.PointClamp);
                    sprites[i].Draw(new Vector2(240,200),flip);batch.End();device.SetRenderTarget(null);
                    draws[i]=new Color[480*360];target.GetData(draws[i]);
                }
                var mask=OwnershipMasks.Embedded((int)Items.Crown,2,10,64,64);
                var top=(new Vector2(240,200)-new Vector2(64,64)*sprites[0].center).ToPoint();
                for(int y=0;y<64;y++)for(int x=0;x<64;x++)if(mask.Visible[y*64+x])
                {
                    int index=(top.Y+y)*480+top.X+(flip==SpriteEffects.None?x:63-x);
                    preserved &= draws[0][index]==draws[1][index];
                }
            }
            Check(preserved,"A live Cosmic body preserves the independent Glass crown, including alpha and both facings");
        }
    }
}
