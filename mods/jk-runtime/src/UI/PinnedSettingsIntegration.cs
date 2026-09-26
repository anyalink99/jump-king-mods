using System;
using System.Collections.Generic;
using BehaviorTree;
using JumpKing.PauseMenu;
using JumpKing.PauseMenu.BT;

namespace JKRuntime.UI
{
    internal sealed class PinnedSettingMenuItem : IBTSimpleMenuItem
    {
        internal readonly string SettingId;
        private readonly IBTSimpleMenuItem inner;

        internal PinnedSettingMenuItem(string settingId, IBTSimpleMenuItem item)
        {
            SettingId = settingId;
            inner = item;
        }

        protected override BTresult MyRun(TickData data)
        {
            return inner.Run(data);
        }

        public override void ResetResult() { base.ResetResult(); inner.ResetResult(); }
        public override IBTnode[] GetRelatedNodes() { return new IBTnode[] { inner }; }

        public override void Draw(int x, int y, bool selected)
        {
            inner.Draw(x, y, selected);
        }

        public override Microsoft.Xna.Framework.Point GetSize()
        {
            return inner.GetSize();
        }
    }

    internal static class PinnedSettingsIntegration
    {
        private sealed class FactoryCache
        {
            internal WeakReference Factory;
            internal readonly Dictionary<string, PinnedSettingMenuItem> Items =
                new Dictionary<string, PinnedSettingMenuItem>(StringComparer.OrdinalIgnoreCase);
        }

        private static readonly List<FactoryCache> Caches = new List<FactoryCache>();

        internal static void Apply(object factory, GuiFormat format)
        {
            MenuSelector main;
            if (!VanillaMenuAdapter.TryGetPauseMainMenu(factory, out main)) return;
            FactoryCache cache = GetCache(factory);
            List<IBTnode> children = new List<IBTnode>(VanillaMenuAdapter.ChildrenForEdit(main));
            children.RemoveAll(delegate(IBTnode child) { return child is PinnedSettingMenuItem; });
            int insertion = FindResumeInsertion(children);
            foreach (string id in SettingsStore.Current.PinnedSettings)
            {
                ModSettingDescriptor descriptor = ModSettingsCatalog.Find(id);
                PinnedSettingMenuItem item;
                if (descriptor == null || !TryGetOrCreate(cache, factory, format, descriptor, out item))
                    continue;
                children.Insert(insertion++, item);
            }
            VanillaMenuAdapter.SetChildren(main, children.ToArray());
        }

        private static FactoryCache GetCache(object factory)
        {
            for (int i = Caches.Count - 1; i >= 0; i--)
            {
                object current = Caches[i].Factory.Target;
                if (current == null)
                {
                    Caches.RemoveAt(i);
                    continue;
                }
                if (ReferenceEquals(current, factory)) return Caches[i];
            }
            FactoryCache cache = new FactoryCache { Factory = new WeakReference(factory) };
            Caches.Add(cache);
            return cache;
        }

        private static bool TryGetOrCreate(
            FactoryCache cache,
            object factory,
            GuiFormat format,
            ModSettingDescriptor descriptor,
            out PinnedSettingMenuItem item)
        {
            if (cache.Items.TryGetValue(descriptor.Info.Id, out item)) return true;
            try
            {
                IBTSimpleMenuItem inner;
                if (!VanillaMenuAdapter.TryCreateSetting(
                    factory,
                    format,
                    descriptor.PauseMethod,
                    out inner)) return false;
                item = new PinnedSettingMenuItem(descriptor.Info.Id, inner);
                cache.Items[descriptor.Info.Id] = item;
                return true;
            }
            catch (Exception error)
            {
                Console.WriteLine("[JK Runtime UI] Could not create pinned setting: " + error.Message);
                item = null;
                return false;
            }
        }

        private static int FindResumeInsertion(List<IBTnode> children)
        {
            for (int i = 0; i < children.Count; i++)
            {
                TextButton text = children[i] as TextButton;
                if (text != null && text.Child != null
                    && text.Child.GetType().FullName == "JumpKing.PauseMenu.BT.Actions.Resume")
                    return i + 1;
            }
            return Math.Min(1, children.Count);
        }
    }
}
