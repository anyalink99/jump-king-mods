using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using JumpKing;
using JumpKing.JKMemory;
using JumpKing.MiscEntities.WorldItems;
using JumpKing.MiscEntities.WorldItems.Inventory;
using JumpKing.Player.Skins;
using JumpKing.SaveThread;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace WardrobePlus
{
    internal static class NativeAppearance
    {
        internal const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        internal static readonly Type Manager = typeof(Game1).Assembly.GetType("JumpKing.Player.Skins.SkinManager", true);
        internal static readonly FieldInfo ItemsField = Manager.GetField("m_king_sprites", Flags);
        internal static readonly FieldInfo AppliedField = Manager.GetField("m_applied_skins", Flags);
        internal static readonly FieldInfo SettingsField = Manager.GetField("m_settings", Flags);
        private static readonly FieldInfo GroupSprites = typeof(IKingSpriteGroup).GetField("m_key_sprites", Flags);
        private static readonly Type LayerType = typeof(Game1).Assembly.GetType("JumpKing.XnaWrappers.LayeredSprite", true);
        private static readonly PropertyInfo LayersProperty = LayerType.GetProperty("Sprites", Flags);
        private static readonly MethodInfo AddLayer = typeof(LayeredKingSprites).GetMethod("AddLayer", Flags);
        internal static readonly int BaseItem = (int)Items.NULL;
        internal static void Validate()
        {
            if (ItemsField == null || AppliedField == null || SettingsField == null || GroupSprites == null || LayersProperty == null || AddLayer == null)
                throw new MissingMemberException("Unsupported Jump King skin/layer contract");
        }
        internal static IDictionary<int, Sprite> Frames(IKingSpriteGroup group)
        { return (IDictionary<int, Sprite>)GroupSprites.GetValue(group); }
        internal static IList Layers(Sprite sprite)
        { return LayerType.IsInstanceOfType(sprite) ? (IList)LayersProperty.GetValue(sprite, null) : null; }
        internal static SkinSettings Settings()
        { return Store.Read<SkinSettings>(Path.Combine(Game1.instance.contentManager.root, "king", "skin_settings.xml")); }
        internal static string MapId()
        {
            var content = Game1.instance.contentManager;
            if (content.level != null) return Catalog.PackageId(content.level);
            return string.Equals(content.root, "Content", StringComparison.OrdinalIgnoreCase) ? "original" : "local-map:" + Path.GetFileName(content.root.TrimEnd('\\', '/'));
        }
        internal static Resolution Default(SkinSettings settings, int item, bool original)
        {
            string file = item == BaseItem ? "base" : settings.skins.First(x => (int)x.item == item).texture;
            if (original && item != BaseItem)
            {
                var native = Store.Read<SkinSettings>(Path.Combine("Content", "king", "skin_settings.xml"));
                file = native.skins.First(x => (int)x.item == item).texture;
            }
            string asset = original ? Path.GetFullPath(Path.Combine("Content", "king", file))
                : Path.GetFullPath(JKContentManager.SmartLoadPath("king/" + file, item == BaseItem));
            return new Resolution { Asset = asset, Id = (original ? "original" : "map:" + MapId()) + ":" + item,
                Label = original ? "Original game" : "Level default" };
        }
        internal static List<Skin> Equipped(SkinSettings settings)
        {
            var result = new List<Skin>();
            foreach (var skin in settings.skins)
            {
                if (!InventoryManager.HasItem(skin.item) || !JumpKing.SaveThread.SaveComponents.ItemEquipOptions.IsItemEnabled(skin.item)) continue;
                result.RemoveAll(x => x.layers.Intersect(skin.layers).Any());
                result.Add(skin);
            }
            return result.OrderBy(x => LayerOrder(x.layers[0])).ToList();
        }
        internal static int LayerOrder(SkinLayer layer)
        {
            switch (layer) { case SkinLayer.Cape: return 0; case SkinLayer.Boots: return 1; case SkinLayer.Shirt: return 2; case SkinLayer.SnakeRing: return 3; default: return 4; }
        }
        internal static List<int> Worn()
        { return ((List<Skin>)AppliedField.GetValue(null)).Select(x => (int)x.item).ToList(); }
        internal static LayeredKingSprites Layer(PreparedAppearance prepared, IEnumerable<int> equipped)
        {
            var layered = new LayeredKingSprites(prepared.BaseTexture);
            for (int group = 0; group < prepared.Base.m_groups.Count; group++)
                foreach (var frame in Frames(prepared.Base.m_groups[group]))
                    Layers(Frames(layered.m_groups[group])[frame.Key])[0] = frame.Value;
            foreach (int item in equipped)
                if (prepared.Items.ContainsKey((Items)item)) AddLayer.Invoke(layered, new object[] { prepared.Items[(Items)item] });
            return layered;
        }
        internal static void Publish(PreparedAppearance prepared)
        {
            var equipped = Equipped(prepared.Settings);
            var next = Layer(prepared, equipped.Select(x => (int)x.item));
            var content = Game1.instance.contentManager;
            var previous = content.playerSprites._CurrentSprites;
            var oldItems = (Dictionary<Items, KingSprites>)ItemsField.GetValue(null);
            var oldApplied = AppliedField.GetValue(null);
            var oldSettings = SettingsField.GetValue(null);
            var oldTexture = JKContentManager.PlayerTexture;
            var changes = new List<Tuple<IList, object[], object[]>>();
            var known = new HashSet<Sprite>();
            foreach (var item in oldItems.Values)
                foreach (var group in item.m_groups)
                    foreach (var sprite in Frames(group).Values) known.Add(sprite);
            bool keepForeign = Controller.Active != null && Controller.ActiveMap == MapId();
            // Validate every destination before mutating any wrapper. Retain foreign layers
            // (for example Jetpack fire) only within the same world.
            if (previous != null && previous.m_groups.Count == next.m_groups.Count)
            {
                for (int group = 0; group < next.m_groups.Count; group++)
                    foreach (var frame in Frames(next.m_groups[group]))
                    {
                        var destination = Layers(Frames(previous.m_groups[group])[frame.Key]);
                        var source = Layers(frame.Value);
                        if (destination == null || destination.IsReadOnly || destination.IsFixedSize || source == null)
                            throw new InvalidOperationException("Another renderer replaced native mutable sprite layers");
                        var replacements = source.Cast<object>().ToList();
                        if (keepForeign)
                            replacements.AddRange(destination.Cast<Sprite>().Skip(1).Where(x => !known.Contains(x)).Cast<object>());
                        changes.Add(Tuple.Create(destination, destination.Cast<object>().ToArray(), replacements.ToArray()));
                    }
                next = previous;
            }
            try
            {
                foreach (var change in changes) { change.Item1.Clear(); foreach (var part in change.Item3) change.Item1.Add(part); }
                ItemsField.SetValue(null, prepared.Items);
                AppliedField.SetValue(null, equipped);
                SettingsField.SetValue(null, prepared.Settings);
                JKContentManager.PlayerTexture = prepared.BaseTexture;
                content.playerSprites._CurrentSprites = next;
                RefreshFolly();
            }
            catch
            {
                foreach (var change in changes) { change.Item1.Clear(); foreach (var part in change.Item2) change.Item1.Add(part); }
                ItemsField.SetValue(null, oldItems); AppliedField.SetValue(null, oldApplied); SettingsField.SetValue(null, oldSettings);
                JKContentManager.PlayerTexture = oldTexture; content.playerSprites._CurrentSprites = previous;
                throw;
            }
        }
        internal static void RefreshFolly()
        {
            var type = typeof(Game1).Assembly.GetType("JumpKing.GameManager.TitleScreen.FollyPlayer");
            if (EntityComponent.EntityManager.instance == null) return;
            var method = typeof(EntityComponent.EntityManager).GetMethods().FirstOrDefault(x => x.Name == "Find" && x.IsGenericMethodDefinition && x.GetParameters().Length == 0);
            if (method == null) return;
            var folly = method.MakeGenericMethod(type).Invoke(EntityComponent.EntityManager.instance, null);
            if (folly != null) type.GetMethod("LoadFollySprite", Flags).Invoke(folly, null);
        }
    }

    internal sealed class PreparedAppearance : IDisposable
    {
        internal SkinSettings Settings;
        internal KingSprites Base;
        internal KingSprites PreviewBase;
        internal Dictionary<Items, KingSprites> PreviewItems;
        internal Outfit SourceOutfit;
        internal Texture2D BaseTexture;
        internal Dictionary<Items, KingSprites> Items = new Dictionary<Items, KingSprites>();
        internal Dictionary<int, Resolution> Resolved = new Dictionary<int, Resolution>();
        internal List<Texture2D> Owned = new List<Texture2D>();
        internal List<int> MaterialEquipment = new List<int>();
        internal bool HasRefraction;
        internal bool HasCosmic;
        private readonly List<TextureLease> leases = new List<TextureLease>();
        private PreparedAppearance materialOwner;
        private int references = 1;
        private bool released;
        internal bool CanRefit(Outfit outfit)
        {
            if (HasRefraction || PreviewBase == null || SourceOutfit.ParentId != outfit.ParentId || SourceOutfit.Material != outfit.Material) return false;
            return Resolved.Keys.All(item => SourceOutfit.MaterialFor(item) == outfit.MaterialFor(item)
                && SourceOutfit.Choice(item).Mode == outfit.Choice(item).Mode
                && SourceOutfit.Choice(item).SourceId == outfit.Choice(item).SourceId);
        }
        internal PreparedAppearance Refit(Outfit outfit)
        {
            if (!CanRefit(outfit)) throw new InvalidOperationException("Refitting requires the same source artwork and materials");
            var owner = materialOwner ?? this; owner.references++;
            var result = new PreparedAppearance { materialOwner = owner, Settings = Settings, BaseTexture = BaseTexture,
                SourceOutfit = outfit.Copy(), PreviewBase = PreviewBase, PreviewItems = PreviewItems,
                Base = CopySprites(PreviewBase), Items = PreviewItems.ToDictionary(x => x.Key, x => CopySprites(x.Value)),
                Resolved = Resolved, HasCosmic = HasCosmic, MaterialEquipment = new List<int>(MaterialEquipment) };
            try
            {
                foreach (int item in Resolved.Keys)
                    FitBaker.Apply(item == NativeAppearance.BaseItem ? result.Base : result.Items[(Items)item], outfit,
                        Resolved[NativeAppearance.BaseItem].Id, Resolved[item].Id, item, result.Owned);
                return result;
            }
            catch { result.Dispose(); throw; }
        }
        internal static PreparedAppearance Build(Outfit outfit, Catalog catalog, bool reload, bool bake = true, int? tryOn = null)
        {
            var result = new PreparedAppearance();
            result.SourceOutfit = outfit.Copy();
            try
            {
                if (reload) TextureCache.Invalidate();
                result.Settings = NativeAppearance.Settings();
                var targets = new[] { NativeAppearance.BaseItem }.Concat(result.Settings.skins.Select(x => (int)x.item));
                var bad = new HashSet<string>();
                foreach (int item in targets)
                {
                    Resolution resolution = null; Texture2D texture = null;
                    for (int attempt = 0; attempt < 3; attempt++)
                    {
                        resolution = Resolver.Resolve(outfit, item, catalog, (target, original) => NativeAppearance.Default(result.Settings, target, original),
                            asset => !bad.Contains(asset) && File.Exists(asset + ".xnb"));
                        try
                        {
                            var lease = TextureCache.Acquire(resolution.Asset); result.leases.Add(lease); texture = lease.Texture;
                            ValidateTexture(texture);
                            break;
                        }
                        catch (Exception error)
                        {
                            if (!catalog.Sources.Any(x => x.Asset == resolution.Asset)) throw;
                            bad.Add(resolution.Asset);
                            Controller.LastError = "Invalid texture: " + resolution.Label + ": " + error.GetBaseException().Message;
                        }
                    }
                    if (texture == null) throw new InvalidDataException("Could not resolve " + item);
                    result.Resolved[item] = resolution;
                    var sprites = new KingSprites(texture);
                    if (item == NativeAppearance.BaseItem) { result.BaseTexture = texture; result.Base = sprites; }
                    else result.Items.Add((Items)item, sprites);
                }
                result.MaterialEquipment = NativeAppearance.Equipped(result.Settings).Select(s => (int)s.item).ToList();
                if (tryOn.HasValue && tryOn.Value != NativeAppearance.BaseItem)
                {
                    var skin = result.Settings.skins.First(s => (int)s.item == tryOn.Value);
                    result.MaterialEquipment.RemoveAll(id => result.Settings.skins.Any(s => (int)s.item == id && s.layers.Intersect(skin.layers).Any()));
                    result.MaterialEquipment.Add(tryOn.Value);
                }
                result.MaterialEquipment = result.MaterialEquipment.OrderBy(id => NativeAppearance.LayerOrder(result.Settings.skins.First(s => (int)s.item == id).layers[0])).ToList();
                result.HasRefraction = CrystalRenderer.ExperimentalEnabled && targets.Any(id => outfit.MaterialFor(id) == MaterialKind.Diamond);
                result.HasCosmic = targets.Any(id => outfit.MaterialFor(id) == MaterialKind.Cosmic);
                MaterialBaker.Apply(result, outfit, result.MaterialEquipment, !bake);
                result.PreviewBase = CopySprites(result.Base);
                result.PreviewItems = result.Items.ToDictionary(x => x.Key, x => CopySprites(x.Value));
                if (bake)
                    foreach (int item in targets)
                        FitBaker.Apply(item == NativeAppearance.BaseItem ? result.Base : result.Items[(Items)item], outfit,
                            result.Resolved[NativeAppearance.BaseItem].Id, result.Resolved[item].Id, item, result.Owned);
                return result;
            }
            catch { result.Dispose(); throw; }
        }
        private static KingSprites CopySprites(KingSprites source)
        {
            var result = new KingSprites(null);
            for (int group = 0; group < source.m_groups.Count; group++)
                foreach (var frame in NativeAppearance.Frames(source.m_groups[group])) NativeAppearance.Frames(result.m_groups[group])[frame.Key] = frame.Value;
            return result;
        }
        internal static void ValidateTexture(Texture2D texture)
        {
            if (texture == null || texture.IsDisposed) throw new InvalidDataException("Texture is unavailable");
            var sprites = new KingSprites(texture);
            foreach (var group in sprites.m_groups)
                foreach (var sprite in NativeAppearance.Frames(group).Values)
                    if (sprite.source.X < 0 || sprite.source.Y < 0 || sprite.source.Right > texture.Width || sprite.source.Bottom > texture.Height)
                        throw new InvalidDataException("Texture does not contain the complete native atlas");
        }
        public void Dispose()
        {
            if (released) return; released = true; ReleaseReference();
        }
        private void ReleaseReference()
        {
            if (--references != 0) return;
            foreach (var texture in Owned) texture.Dispose(); Owned.Clear();
            foreach (var lease in leases) lease.Dispose(); leases.Clear();
            if (materialOwner != null) materialOwner.ReleaseReference(); materialOwner = null;
        }
    }
}
