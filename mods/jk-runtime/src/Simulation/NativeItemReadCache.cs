using System;
using System.Collections.Generic;
using System.Reflection;
using JumpKing.Player;

namespace JKRuntime.Simulation
{
    // SaveLube.Load consults this cache but does not populate it on a disk read.
    // Forecasts amplify Snow/Ice -> SnakeRing queries into hundreds of reads.
    // Use the native cache, not a second inventory snapshot that survives resets.
    internal static class NativeItemReadCache
    {
        private const BindingFlags Flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly Type Save = typeof(BodyComp).Assembly.GetType("JumpKing.SaveThread.SaveLube", true);
        private static readonly FieldInfo Cache = Save.GetField("loaded_objects", Flags);
        private static readonly FieldInfo Folder = Save.GetField("PERMANENT_FOLDER", Flags);
        private static readonly PropertyInfo Inventory = Save.GetProperty("inventory", Flags);
        private static readonly PropertyInfo Settings = Save.GetProperty("generalSettings", Flags);
        private static readonly Func<object> ReadInventory = delegate { return Inventory.GetValue(null, null); };
        private static readonly Func<object> ReadSettings = delegate { return Settings.GetValue(null, null); };

        internal static void Ensure()
        {
            RuntimeApi.Kernel.CheckThread();
            if (Cache == null || Cache.FieldType != typeof(Dictionary<string, object>) || Folder == null
                || Folder.FieldType != typeof(string) || Inventory == null || Settings == null)
                throw new NotSupportedException("Native inventory read-cache contract unavailable");
            var cache = (Dictionary<string, object>)Cache.GetValue(null);
            string folder = (string)Folder.GetValue(null);
            WarmMissing(cache, folder + "inventory.inv", ReadInventory);
            WarmMissing(cache, folder + "general_settings.set", ReadSettings);
        }

        internal static void WarmMissing(IDictionary<string, object> cache, string key, Func<object> read)
        {
            if (cache.ContainsKey(key)) return;
            object value = read();
            // Never replace an entry supplied by the native read or a callback.
            if (!cache.ContainsKey(key)) cache.Add(key, value);
        }
    }
}
