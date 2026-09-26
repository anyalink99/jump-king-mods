using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;

namespace WardrobePlus
{
    public enum ChoiceMode { Inherit, LevelDefault, OriginalGame, Source }
    // Keep serialized/API IDs stable: Diamond is displayed as Glass, RedVelvet as Magenta.
    public enum MaterialKind { Original, Gold, Diamond, RedVelvet, Cosmic }

    public sealed class ItemMaterial
    {
        public int Item;
        public MaterialKind Kind;
        public ItemMaterial Copy() { return (ItemMaterial)MemberwiseClone(); }
    }

    public sealed class AppearanceChoice
    {
        public int Item;
        public ChoiceMode Mode;
        public string SourceId = "";
        public string Label = "";
        public bool Locked;
        public AppearanceChoice Copy() { return (AppearanceChoice)MemberwiseClone(); }
    }

    public sealed class FitAdjustment
    {
        public string BaseId = "";
        public string SourceId = "";
        public int Item;
        // -1/-1 applies to every pose; a specific pose replaces it.
        public int Group = -1;
        public int Frame = -1;
        public int X;
        public int Y;
        public FitAdjustment Copy() { return (FitAdjustment)MemberwiseClone(); }
    }

    public sealed class EquipmentSelection
    {
        public List<int> Items = new List<int>();
        public EquipmentSelection Copy() { return new EquipmentSelection { Items = new List<int>(Items) }; }
    }

    public sealed class Outfit
    {
        public string Id = Guid.NewGuid().ToString("N");
        public string Name = "New outfit";
        public string ParentId = "";
        public string ParentName = "";
        public bool Favorite;
        // Null is a legacy appearance-only preset; an empty selection means unequip all clothing.
        public EquipmentSelection Equipment;
        public MaterialKind Material;
        public List<ItemMaterial> Materials = new List<ItemMaterial>();
        public List<AppearanceChoice> Choices = new List<AppearanceChoice>();
        public List<FitAdjustment> Fits = new List<FitAdjustment>();
        public AppearanceChoice Choice(int item)
        {
            return Choices.Find(x => x.Item == item) ?? new AppearanceChoice { Item = item };
        }
        public void Set(AppearanceChoice value)
        {
            Choices.RemoveAll(x => x.Item == value.Item);
            Choices.Add(value.Copy());
        }
        public Outfit Copy()
        {
            return new Outfit { Id = Id, Name = Name, ParentId = ParentId, ParentName = ParentName, Favorite = Favorite,
                Equipment = Equipment == null ? null : Equipment.Copy(), Material = Material, Materials = Materials.Select(x => x.Copy()).ToList(),
                Choices = Choices.Select(x => x.Copy()).ToList(), Fits = Fits.Select(x => x.Copy()).ToList() };
        }
        public MaterialKind MaterialFor(int item)
        { var value = Materials.Find(x => x.Item == item); return value == null ? Material : value.Kind; }
        public void SetMaterial(int item, MaterialKind? kind)
        {
            Materials.RemoveAll(x => x.Item == item);
            if (kind.HasValue) Materials.Add(new ItemMaterial { Item = item, Kind = kind.Value });
        }
        public FitAdjustment Fit(string baseId, string sourceId, int item, int group, int frame)
        {
            return Fits.Find(x => x.BaseId == baseId && x.SourceId == sourceId && x.Item == item && x.Group == group && x.Frame == frame)
                ?? Fits.Find(x => x.BaseId == baseId && x.SourceId == sourceId && x.Item == item && x.Group == -1)
                ?? new FitAdjustment { BaseId = baseId, SourceId = sourceId, Item = item, Group = group, Frame = frame };
        }
        public void SetFit(FitAdjustment fit)
        {
            if (Math.Abs(fit.X) > 32 || Math.Abs(fit.Y) > 32) throw new ArgumentOutOfRangeException("fit", "Fit range is -32 to 32 pixels");
            Fits.RemoveAll(x => x.BaseId == fit.BaseId && x.SourceId == fit.SourceId && x.Item == fit.Item && x.Group == fit.Group && x.Frame == fit.Frame);
            Fits.Add(fit.Copy());
        }
    }

    public sealed class MapOutfit { public string MapId = ""; public string OutfitId = ""; }
    public sealed class BindingChord { public int[] Buttons = new int[0]; }
    public sealed class WardrobeData
    {
        public int Version = 3;
        public bool ImportedNative;
        public bool Enabled = true;
        public bool KeepCustomizations = true;
        // Retain legacy XML keys for round trips; entry placement is now fixed.
        public bool MainMenu;
        public bool PauseMenu;
        public bool Animate = true;
        public Outfit Current = new Outfit { Name = "Current outfit" };
        public Outfit Undo;
        public List<Outfit> Presets = new List<Outfit>();
        public List<string> Favorites = new List<string>();
        public List<MapOutfit> Maps = new List<MapOutfit>();
        public List<BindingChord> Binding = new List<BindingChord>();
        public WardrobeData Copy()
        {
            return new WardrobeData { Version = Version, ImportedNative = ImportedNative, Enabled = Enabled,
                KeepCustomizations = KeepCustomizations, MainMenu = MainMenu, PauseMenu = PauseMenu, Animate = Animate,
                Current = Current.Copy(), Undo = Undo == null ? null : Undo.Copy(), Presets = Presets.Select(x => x.Copy()).ToList(),
                Favorites = new List<string>(Favorites), Maps = Maps.Select(x => new MapOutfit { MapId = x.MapId, OutfitId = x.OutfitId }).ToList(),
                Binding = Binding.Select(x => new BindingChord { Buttons = (int[])x.Buttons.Clone() }).ToList() };
        }
    }

    public sealed class Recipe
    {
        public int Version = 3;
        public Outfit Outfit = new Outfit();
        public List<string> WorkshopLinks = new List<string>();
    }
}
