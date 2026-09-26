using System;
using System.Reflection;
using System.Collections.Generic;
using JumpKing;
using JumpKing.Level;

namespace MegaMappingExpansion
{
    internal static class NativeMapLayout
    {
        private static readonly FieldInfo Screens = typeof(LevelManager).GetField("m_screens", BindingFlags.Static | BindingFlags.NonPublic);
        internal static MapLayout Current;
        private static readonly List<Action> restore = new List<Action>();

        internal static void Release()
        {
            foreach (var undo in restore) undo();
            restore.Clear(); Current = null;
        }

        internal static void AssertContracts()
        {
            if (Screens == null || Screens.FieldType != typeof(LevelScreen[])
                || typeof(LevelScreen).GetProperty("teleport").PropertyType != typeof(TeleportLink[]))
                throw new InvalidOperationException("Installed native screen topology is incompatible");
        }

        internal static void Apply(MapLayout layout, int screen, TeleportLink[] native)
        {
            int[] pair;
            if (!layout.Links.TryGetValue(screen, out pair)) return;
            if (native == null || native.Length != 2) throw new InvalidOperationException("Native side-link array is incompatible");
            for (int side = 0; side < 2; side++) if (pair[side] > 0) native[side] = new TeleportLink(pair[side]);
        }

        // OnLevelStart runs after native LoadScreens has finished unloading the
        // previous map and constructing the new one. No hooks survive map unload,
        // and no custom movement behaviour competes with the native teleporter.
        internal static void BeginRun(string root)
        { BeginRun(MapLayout.Read(root)); }
        internal static void BeginRun(MapLayout layout)
        {
            Release();
            if (layout == null) return;
            var texture = Game1.instance.contentManager.LevelTexture;
            layout.ValidateAtlas(texture.Width, texture.Height);
            var screens = (LevelScreen[])Screens.GetValue(null);
            if (screens == null || screens.Length != layout.AtlasSide * layout.AtlasSide)
                throw new InvalidOperationException("Native screen count does not match map.xml");
            foreach (int screen in layout.Links.Keys)
            {
                var native = screens[screen].teleport;
                var before = (TeleportLink[])native.Clone();
                Apply(layout, screen, native);
                for (int side = 0; side < 2; side++)
                {
                    int index = side;
                    var written = native[index]; var original = before[index];
                    if (ReferenceEquals(written, original)) continue;
                    restore.Add(delegate { if (ReferenceEquals(native[index], written)) native[index] = original; });
                }
            }
            Current = layout;
            ModEntry.Log("Map layout active: content=" + layout.Screens + ", slots=" + screens.Length + ", linkedScreens=" + layout.Links.Count);
        }
    }
}
