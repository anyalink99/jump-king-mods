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
        private static IDisposable links;

        internal static void Release()
        {
            if(links!=null){links.Dispose();links=null;}Current=null;
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
            JKRuntime.World.NativeSideLinks.SetPair(native,pair[0],pair[1]);
        }

        // OnLevelStart runs after native LoadScreens has finished unloading the
        // previous map and constructing the new one. no hooks survive map unload,
        // and no custom movement behaviour competes with the native teleporter
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
            var values=new List<JKRuntime.World.WorldSideLink>();
            foreach(var pair in layout.Links)for(int side=0;side<2;side++)if(pair.Value[side]>0)values.Add(new JKRuntime.World.WorldSideLink(pair.Key,side,pair.Value[side]));
            links=JKRuntime.World.NativeSideLinks.Apply("mega-mapping-expansion",values,layout.Screens,layout.AtlasSide);
            Current = layout;
            ModEntry.Log("Map layout active: content=" + layout.Screens + ", slots=" + screens.Length + ", linkedScreens=" + layout.Links.Count);
        }
    }
}
