using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using HarmonyLib;
using JKRuntime;
using LessAutoEquipping;

internal static class MigrationTests
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    private static int equipped, added, removed, saved, destroyed, paid;
    private static readonly Assembly Game = typeof(JumpKing.Game1).Assembly;
    private static Type Native(string name) { return Game.GetType(name, true); }
    private static MethodInfo Method(string type, string name) { return AccessTools.Method(Native(type), name); }
    private static object Empty(Type type) { return type.IsValueType ? Activator.CreateInstance(type) : FormatterServices.GetUninitializedObject(type); }
    private static void Set(object value, string field, object data) { value.GetType().GetField(field, Flags).SetValue(value, data); }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static bool Equip() { equipped++; return false; }
    private static bool Add() { added++; return false; }
    private static bool Remove() { removed++; return false; }
    private static bool Save() { saved++; return false; }
    private static bool Destroy() { destroyed++; return false; }
    private static bool Pay() { paid++; return false; }
    private static bool Skip() { return false; }
    private static bool IsSkin(ref bool __result) { __result = true; return false; }
    private static void Stub(Harmony fixture, string type, string method, string callback)
    { fixture.Patch(Method(type, method), prefix: new HarmonyMethod(typeof(MigrationTests).GetMethod(callback, Flags))); }

    private static void NativeFlows(bool prevent)
    {
        ModEntry.SetPreventAutoEquip(prevent);
        equipped = added = removed = saved = destroyed = paid = 0;
        var items = Native("JumpKing.MiscEntities.WorldItems.Items");
        object item = Enum.GetValues(items).GetValue(1);
        var giftType = Native("JumpKing.GameManager.MultiEnding.GiveWearableItemNode");
        object gift = Activator.CreateInstance(giftType, new[] { item });
        var reward = giftType.GetMethod("MyRun", Flags);
        object result = reward.Invoke(gift, reward.GetParameters().Select(p => p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null).ToArray());
        Check(result.ToString() == "Success" && added == 1 && equipped == (prevent ? 0 : 1), "Ending reward still grants its item; only equipping is conditional");

        var merchantType = Native("JumpKing.MiscEntities.Merchant.MerchantComp");
        object merchant = Empty(merchantType);
        object settings = Empty(merchantType.GetField("m_settings", Flags).FieldType);
        Set(settings, "sale_item", item); Set(settings, "currency_type", item);
        Set(settings, "sale_achievements", Array.CreateInstance(settings.GetType().GetField("sale_achievements").FieldType.GetElementType(), 0));
        Set(merchant, "m_settings", settings);
        object state = Empty(merchantType.GetField("m_state", Flags).FieldType);
        Set(state, "required_gold", 3); Set(merchant, "m_state", state);
        merchantType.GetMethod("OnSell", Flags).Invoke(merchant, null);
        Check(added == 2 && saved == 1 && paid == 1 && equipped == (prevent ? 0 : 2), "Merchant still saves sale, grants item and charges currency");

        var worldType = Native("JumpKing.MiscEntities.WorldItems.WorldItemComp");
        object pickup = Empty(worldType);
        object worldState = Empty(worldType.GetField("m_state", Flags).FieldType);
        Set(worldState, "item", item); Set(pickup, "m_state", worldState);
        typeof(EntityComponent.Component).GetField("m_owner", Flags).SetValue(pickup, new EntityComponent.Entity());
        worldType.GetMethod("OnPickup", Flags).Invoke(pickup, null);
        Check(added == 3 && removed == 1 && destroyed == 1 && equipped == (prevent ? 0 : 4), "Pickup still grants, removes and destroys its world item");
        worldType.GetMethod("OnPickup", Flags).Invoke(pickup, null);
        Check(added == 3 && removed == 1, "A collected world item is not granted twice");
    }

    public static int Main()
    {
        try
        {
            var directory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "preferences-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, PreferencesStore.FileName);
            Check(!new PreferencesStore(directory).Current.ShouldPreventAutoEquip && !File.Exists(path), "Default false does not create a file");
            foreach (var spelling in new[] { "True", "False", "true", "false" })
            {
                var original = "<Preferences><ShouldPreventAutoEquip>" + spelling + "</ShouldPreventAutoEquip></Preferences>";
                File.WriteAllText(path, original);
                Check(new PreferencesStore(directory).Current.ShouldPreventAutoEquip == bool.Parse(spelling), "Reads upstream and current booleans");
                Check(File.ReadAllText(path) == original, "Reading settings preserves original bytes");
            }
            var store = new PreferencesStore(directory);
            typeof(ModEntry).GetField("store", Flags).SetValue(null, store);
            store.Set(true);
            Check(new PreferencesStore(directory).Current.ShouldPreventAutoEquip && File.Exists(path + ".bak"), "Atomic save remains readable and preserves a backup");
            bool writeFailed = false;
            using (var locked = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None))
                try { store.Set(false); } catch (IOException) { writeFailed = true; }
            Check(writeFailed && store.Current.ShouldPreventAutoEquip && new PreferencesStore(directory).Current.ShouldPreventAutoEquip,
                "Failed writes preserve both the current value and the saved file");
            ModEntry.MainMenu(null, default(JumpKing.PauseMenu.GuiFormat)); ModEntry.PauseMenu(null, default(JumpKing.PauseMenu.GuiFormat));
            File.WriteAllText(path, "<broken");
            bool rejected = false;
            try { new PreferencesStore(directory); } catch (System.Xml.XmlException) { rejected = true; }
            Check(rejected && File.ReadAllText(path) == "<broken", "Malformed preferences are not overwritten");
            store.Set(false);

            var fixture = new Harmony("less-auto-equipping.migration-tests");
            Stub(fixture, "JumpKing.Player.Skins.SkinManager", "EnableSkin", "Equip");
            Stub(fixture, "JumpKing.Player.Skins.SkinManager", "SetSkinEnabled", "Equip");
            Stub(fixture, "JumpKing.Player.Skins.SkinManager", "IsSkin", "IsSkin");
            Stub(fixture, "JumpKing.MiscEntities.WorldItems.Inventory.InventoryManager", "AddItemOnce", "Add");
            Stub(fixture, "JumpKing.MiscEntities.WorldItems.Inventory.InventoryManager", "AddItem", "Add");
            Stub(fixture, "JumpKing.MiscEntities.WorldItems.Inventory.InventoryManager", "RemoveItems", "Pay");
            Stub(fixture, "JumpKing.MiscEntities.Merchant.MerchantComp", "Save", "Save");
            Stub(fixture, "JumpKing.MiscEntities.WorldItems.Entities.WorldItemEntityDisplay", "RemoveAllItemsOfType", "Skip");
            Stub(fixture, "JumpKing.SaveThread.SaveLube", "RemoveWorldItem", "Remove");
            Stub(fixture, "EntityComponent.Entity", "Destroy", "Destroy");
            var giftMethod = Method("JumpKing.GameManager.MultiEnding.GiveWearableItemNode", "MyRun");
            fixture.Patch(giftMethod, postfix: new HarmonyMethod(typeof(MigrationTests).GetMethod("ForeignPostfix", Flags)));
            for (int world = 0; world < 2; world++)
            {
                using (var scope = new RuntimeScope())
                {
                    ModEntry.PrepareWorld(scope);
                    bool duplicateRejected = false;
                    using (var duplicate = new RuntimeScope())
                        try { ModEntry.PrepareWorld(duplicate); } catch (InvalidOperationException) { duplicateRejected = true; }
                    Check(duplicateRejected, "Duplicate patch ownership is rejected without disturbing the active world");
                    // Repeated attempts reuse the same world patches.
                    NativeFlows(false); NativeFlows(true); NativeFlows(false);
                    Check(Harmony.GetPatchInfo(giftMethod).Transpilers.Count(p => p.owner == ModEntry.HarmonyOwner) == 1, "Attempts do not duplicate patches");
                }
                var info = Harmony.GetPatchInfo(giftMethod);
                Check(!info.Owners.Contains(ModEntry.HarmonyOwner) && info.Owners.Contains(fixture.Id), "World exit removes only owned patches");
                equipped = 0;
                ModEntry.SetPreventAutoEquip(true);
                var type = Native("JumpKing.GameManager.MultiEnding.GiveWearableItemNode");
                giftMethod.Invoke(Activator.CreateInstance(type, new[] { Enum.GetValues(Native("JumpKing.MiscEntities.WorldItems.Items")).GetValue(1) }), new object[] { null });
                Check(equipped == 1, "Native auto-equip is restored after world cleanup");
            }
            Console.WriteLine("[OK] Legacy XML, both menus, native reward/purchase/pickup semantics, repeated attempts, world cleanup and foreign patch preservation");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
    private static void ForeignPostfix() { }
}
