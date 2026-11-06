using System;
using System.Collections.Generic;
using JumpKing;
using JumpKing.PauseMenu;

namespace JKRuntime.UI
{
    internal static class ModMenuIntegration
    {
        private sealed class FactoryRegistration
        {
            internal WeakReference Factory;
            internal GuiFormat Format;
            internal bool IsPause;
            internal bool ApplyScheduled;
            internal bool FullRefresh;
        }

        private static readonly List<FactoryRegistration> Factories =
            new List<FactoryRegistration>();

        internal static void RegisterFactory(object factory, GuiFormat format, bool isPause)
        {
            if (factory == null) return;
            ModSettingsCatalog.Refresh(factory, format, isPause);
            FactoryRegistration registration = Find(factory);
            if (registration == null)
            {
                registration = new FactoryRegistration
                {
                    Factory = new WeakReference(factory),
                    Format = format,
                    IsPause = isPause
                };
                Factories.Add(registration);
            }
            else
            {
                registration.Format = format;
                registration.IsPause = registration.IsPause || isPause;
            }
            Schedule(registration);
        }

        internal static void Refresh()
        {
            RemoveDeadFactories();
            foreach (FactoryRegistration registration in Factories) Schedule(registration);
        }

        internal static void RefreshPins()
        {
            RemoveDeadFactories();
            foreach (FactoryRegistration registration in Factories)
                if (registration.IsPause) Schedule(registration, false);
        }

        internal static void ClosePause()
        {
            VanillaMenuAdapter.ClosePause();
        }

        private static FactoryRegistration Find(object factory)
        {
            RemoveDeadFactories();
            foreach (FactoryRegistration registration in Factories)
                if (ReferenceEquals(registration.Factory.Target, factory)) return registration;
            return null;
        }

        private static void RemoveDeadFactories()
        {
            for (int i = Factories.Count - 1; i >= 0; i--)
                if (!Factories[i].Factory.IsAlive) Factories.RemoveAt(i);
        }

        private static void Schedule(FactoryRegistration registration, bool full = true)
        {
            registration.FullRefresh |= full;
            if (registration.ApplyScheduled) return;
            registration.ApplyScheduled = true;
            Game1.callbackManager.CreateRoutine(
                1,
                delegate
                {
                    registration.ApplyScheduled = false;
                    bool applyAll = registration.FullRefresh; registration.FullRefresh = false;
                    object factory = registration.Factory.Target;
                    if (factory == null) return;
                    SettingsStore.EnsureLoaded();
                    if (!applyAll) { PinnedSettingsIntegration.Apply(factory, registration.Format); return; }
                    WorkshopGridIntegration.Apply(
                        factory,
                        SettingsStore.Current.UseCompactWorkshopGrids);
                    if (!registration.IsPause)
                        WorkshopMenuIntegration.Apply(factory);
                    if (!registration.IsPause)
                        RootMainMenuIntegration.Apply(factory);
                    if (registration.IsPause)
                        RootPauseMenuIntegration.Apply(factory);
                    if (registration.IsPause)
                        PinnedSettingsIntegration.Apply(factory, registration.Format);
                });
        }
    }
}
