using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using JumpKing;
using JumpKing.MiscEntities.WorldItems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using WardrobePlus;

internal static partial class WardrobeTests
{
    private static void ItemMaskGraphicsTests(GraphicsDevice device, SpriteBatch batch, RenderTarget2D target)
    {
        var catalog = Controller.Catalog;
        var originalOutfit = new Outfit();
        using (var original = PreparedAppearance.Build(originalOutfit, catalog, false))
        foreach (var kind in new[] { MaterialKind.Gold, MaterialKind.Diamond, MaterialKind.RedVelvet, MaterialKind.Cosmic })
        using (var changed = PreparedAppearance.Build(new Outfit { Material = kind }, catalog, false))
        {
            var readbacks = new Dictionary<Texture2D, Color[]>();
            bool foreignPreserved = true, complete = true, livePreserved = true;
            int recovered = 0;
            foreach (var part in original.Items)
            {
                int item = (int)part.Key;
                for (int group = 0; group < part.Value.m_groups.Count; group++)
                foreach (var frame in NativeAppearance.Frames(part.Value.m_groups[group]))
                {
                    var source = frame.Value;
                    var result = NativeAppearance.Frames(changed.Items[part.Key].m_groups[group])[frame.Key];
                    var before = MaskFramePixels(source, readbacks); var after = MaskFramePixels(result, readbacks);
                    var mask = OwnershipMasks.For(item, group, frame.Key, source.source.Width, source.source.Height);
                    var body = OwnershipMasks.For(NativeAppearance.BaseItem, group, frame.Key, source.source.Width, source.source.Height);
                    complete &= mask != null;
                    if (mask == null) continue;
                    for (int p = 0; p < before.Length; p++)
                    {
                        bool itemPose = group == 0 || group == 2 || group == 3 || group == 4 || group == 6;
                        if (before[p].A > 0 && itemPose) complete &= mask.Visible[p];
                        if (!itemPose) complete &= !mask.Visible[p];
                        if (!mask.Visible[p]) foreignPreserved &= before[p] == after[p];
                        else if (before[p].A > 0 && !body.Visible[p])
                        {
                            // Boots and capes extend outside the body's ownership
                            recovered++;
                            if (kind == MaterialKind.Diamond) complete &= after[p].A < before[p].A;
                        }
                    }
                    if (kind == MaterialKind.Cosmic && group != 0)
                    foreach (var flip in new[] { SpriteEffects.None, SpriteEffects.FlipHorizontally })
                    {
                        var draws = new Color[2][];
                        for (int pass = 0; pass < 2; pass++)
                        {
                            device.SetRenderTarget(target); device.Clear(Color.Transparent);
                            batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
                            (pass == 0 ? source : result).Draw(new Vector2(240, 200), flip);
                            batch.End(); device.SetRenderTarget(null);
                            draws[pass] = new Color[480 * 360]; target.GetData(draws[pass]);
                        }
                        int w = source.source.Width, h = source.source.Height;
                        var top = (new Vector2(240, 200) - new Vector2(w, h) * source.center).ToPoint();
                        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
                        if (!mask.Visible[y * w + x])
                        {
                            int index = (top.Y + y) * 480 + top.X + (flip == SpriteEffects.None ? x : w - 1 - x);
                            livePreserved &= draws[0][index] == draws[1][index];
                        }
                    }
                }
            }
            Check(complete && recovered > 10000, kind + " uses every native item's own mask, including boots and capes outside the body silhouette");
            Check(foreignPreserved, kind + " preserves every foreign pixel and transparent cutout in item atlases");
            if (kind == MaterialKind.Cosmic) Check(livePreserved, "Live Cosmic preserves protected item pixels in every ending pose and both facings");
        }
        ItemMaskSourceSwitchTests();
    }

    private static Color[] MaskFramePixels(Sprite sprite, Dictionary<Texture2D, Color[]> cache)
    {
        Color[] atlas;
        if (!cache.TryGetValue(sprite.texture, out atlas))
        {
            atlas = new Color[sprite.texture.Width * sprite.texture.Height];
            sprite.texture.GetData(atlas); cache.Add(sprite.texture, atlas);
        }
        var pixels = new Color[sprite.source.Width * sprite.source.Height];
        FitBaker.CopyFrame(atlas, sprite.texture.Width, sprite.source, pixels, sprite.source.Width, Point.Zero);
        return pixels;
    }

    private static void ItemMaskSourceSwitchTests()
    {
        // reuse one slot across two collections with deliberately different artwork
        var manager = JumpKing.Workshop.WorkshopManager.instance;
        var root = Path.Combine(output, "item-mask-collections"); Directory.CreateDirectory(root);
        var collections = new JumpKing.Workshop.Collection[2];
        try
        {
            for (int i = 0; i < collections.Length; i++)
            {
                var path = Path.Combine(root, "source-" + i); Directory.CreateDirectory(path);
                var bytes = File.ReadAllBytes(Path.Combine("Content", "king", "cape_owl.xnb"));
                // installed native atlases are uncompressed Color XNBs. A palette
                // variant changes source pixels while keeping the native anatomy
                if (bytes[0] != 'X' || bytes[1] != 'N' || bytes[2] != 'B' || bytes[5] != 0)
                    throw new InvalidDataException("Expected an uncompressed native atlas");
                if (i == 1) for (int p = bytes.Length - 832 * 384 * 4; p < bytes.Length; p += 4)
                { byte red = bytes[p]; bytes[p] = bytes[p + 1]; bytes[p + 1] = red; }
                File.WriteAllBytes(Path.Combine(path, "item.xnb"), bytes);
                File.WriteAllText(Path.Combine(path, "set_settings.xml"), "<SetSettings><Reskins><Reskin><skin>CapeOwl</skin><name>item</name></Reskin></Reskins></SetSettings>");
                collections[i] = new JumpKing.Workshop.Collection(path); manager.collections.Add(collections[i]);
            }
            var catalog = Catalog.Discover();
            var outfit = new Outfit { ParentId = Catalog.PackageId(collections[0]) };
            outfit.SetMaterial((int)Items.CapeOwl, MaterialKind.Diamond);
            Color[] firstOriginal = null;
            for (int selected = 0; selected < 2; selected++)
            {
                outfit.Set(new AppearanceChoice { Item = (int)Items.CapeOwl, Mode = ChoiceMode.Source,
                    SourceId = Catalog.SourceId(collections[selected], (int)Items.CapeOwl, true) });
                using (var changed = PreparedAppearance.Build(outfit, catalog, false))
                {
                    var plain = outfit.Copy(); plain.SetMaterial((int)Items.CapeOwl, MaterialKind.Original);
                    using (var original = PreparedAppearance.Build(plain, catalog, false))
                    {
                        var source = NativeAppearance.Frames(original.Items[Items.CapeOwl].m_groups[6])[14];
                        var result = NativeAppearance.Frames(changed.Items[Items.CapeOwl].m_groups[6])[14];
                        var mask = OwnershipMasks.For((int)Items.CapeOwl, 6, 14, 48, 64);
                        var a = FramePixels(source); var b = FramePixels(result);
                        if (selected == 0) firstOriginal = a;
                        else Check(!a.SequenceEqual(firstOriginal), "Switching collection item sources invalidates the previous texture, including Original material");
                        Check(changed.Resolved[(int)Items.CapeOwl].Id == outfit.Choice((int)Items.CapeOwl).SourceId
                            && a.Where((pixel, index) => !mask.Visible[index]).SequenceEqual(b.Where((pixel, index) => !mask.Visible[index])),
                            "Collection item override " + selected + " resolves its source before applying the slot mask");
                        Check(a.Where((pixel, index) => mask.Visible[index] && pixel.A > 0).Any(), "Source switch retains visible item artwork");
                        Check(FramePixels(NativeAppearance.Frames(original.Base.regular)[0]).SequenceEqual(FramePixels(NativeAppearance.Frames(changed.Base.regular)[0])),
                            "An item material override does not leak onto the collection body");
                    }
                }
            }
            outfit.Set(new AppearanceChoice { Item = (int)Items.CapeOwl, Mode = ChoiceMode.Inherit });
            outfit.SetMaterial((int)Items.CapeOwl, MaterialKind.Original);
            using (var restored = PreparedAppearance.Build(outfit, catalog, false))
                Check(FramePixels(NativeAppearance.Frames(restored.Items[Items.CapeOwl].m_groups[6])[14]).SequenceEqual(firstOriginal),
                    "Returning to the parent collection and Original restores its exact item pixels");
        }
        finally { foreach (var collection in collections) if (collection != null) manager.collections.Remove(collection); }
    }
}
