using System;
using JumpKing.MiscEntities.WorldItems;
using JumpKing.MiscEntities.WorldItems.Inventory;
using System.Reflection;

namespace JKRuntime.UI
{
    // restore only changes this hold actually made; a later inventory edit wins
    internal sealed class TemporaryToggle
    {
        private bool armed, owned, before;
        internal void Update(bool down, bool available, Func<bool> read, Action<bool> write)
        {
            if (!available || !down) { Release(read, write); if (available && !down) armed = true; else armed = false; return; }
            if (!armed) return;
            if (owned) { if (!read()) { owned = false; armed = false; } return; }
            before = read(); if (!before) write(true); owned = true;
        }
        internal void Release(Func<bool> read, Action<bool> write)
        { bool restore = owned && !before; owned = false; if (restore && read()) write(false); }
        internal void Relinquish() { owned = armed = false; }
    }
    internal static class BindingEquipment
    {
        private static readonly Type Skin = typeof(JumpKing.Game1).Assembly.GetType("JumpKing.Player.Skins.SkinManager", true);
        private static readonly Func<Items, bool> Wearing = (Func<Items, bool>)Delegate.CreateDelegate(typeof(Func<Items, bool>), Skin.GetMethod("IsWearingSkin", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
        private static readonly Action<Items, bool> SetEnabled = (Action<Items, bool>)Delegate.CreateDelegate(typeof(Action<Items, bool>), Skin.GetMethod("SetSkinEnabled", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
        private static readonly TemporaryToggle boots = new TemporaryToggle(), ring = new TemporaryToggle();
        private static Items previousShoes = Items.NULL;
        private static bool writing;
        internal static MethodInfo WriteTarget { get { return Skin.GetMethod("SetSkinEnabled", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic); } }
        internal static void ObserveWrite(Items __0)
        {
            if (writing) return;
            // even an external write of the same value takes ownership away
            if (__0 == Items.GiantBoots || __0 == Items.Shoes || __0 == Items.YellowShoes) { boots.Relinquish(); previousShoes = Items.NULL; }
            if (__0 == Items.SnakeRing) ring.Relinquish();
        }
        private static void Write(Items item, bool enabled)
        { writing = true; try { SetEnabled(item, enabled); } finally { writing = false; } }
        internal static bool HoldBoots { get { return SettingsStore.GetBindingMode("jump-king.boots", UiBindingMode.Press) == UiBindingMode.Hold && BindingConversionContext.Reason("jump-king.boots") == null; } }
        internal static bool HoldRing { get { return SettingsStore.GetBindingMode("jump-king.snake", UiBindingMode.Press) == UiBindingMode.Hold && BindingConversionContext.Reason("jump-king.snake") == null; } }
        private static bool Boots() { return Wearing(Items.GiantBoots); }
        private static bool Ring() { return Wearing(Items.SnakeRing); }
        private static void SetBoots(bool enabled)
        {
            bool before = Boots();
            if (enabled) previousShoes = Wearing(Items.YellowShoes) ? Items.YellowShoes
                : Wearing(Items.Shoes) ? Items.Shoes : Items.NULL;
            Write(Items.GiantBoots, enabled);
            if (!enabled && previousShoes != Items.NULL) { Write(previousShoes, true); previousShoes = Items.NULL; }
            if (Boots() != before) UiSounds.Play(UiSound.Equipment);
        }
        private static void SetRing(bool enabled)
        {
            bool before = Ring(); Write(Items.SnakeRing, enabled);
            if (Ring() != before) UiSounds.Play(UiSound.Equipment);
        }
        internal static void Update(bool bootsDown, bool ringDown, bool available)
        {
            boots.Update(bootsDown, available && HoldBoots && InventoryManager.GetItemCount(Items.GiantBoots) > 0, Boots, SetBoots);
            ring.Update(ringDown, available && HoldRing && InventoryManager.GetItemCount(Items.SnakeRing) > 0, Ring, SetRing);
        }
        internal static void Release()
        { boots.Update(false, false, Boots, SetBoots); ring.Update(false, false, Ring, SetRing); }
    }
}
