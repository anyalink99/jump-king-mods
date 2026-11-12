using Microsoft.Xna.Framework;

namespace MoreItems
{
    internal static class RewinderDefinition
    {
        internal const string Id = "rewinder";
        private static RewinderController controller;
        internal static JKRuntime.Gameplay.MechanicState Describe()
        {
            bool enabled = MoreItemsApi.IsModuleEnabled(Id);
            return new JKRuntime.Gameplay.MechanicState(enabled, controller != null, enabled && controller != null && controller.IsRewinding,
                JKRuntime.Gameplay.MechanicSource.Equipment, "Restores movement only; inventory, world and run history are not rewound");
        }

        internal static void RegisterModule()
        {
            MoreItemsApi.RegisterModule(new ItemModuleDefinition(
                Id,
                "Rewinder",
                delegate
                {
                    SettingsStore.EnsureLoaded();
                    return SettingsStore.Current.EnableRewinders;
                },
                SettingsStore.SetRewindersEnabled,
                delegate(JumpKing.PauseMenu.BT.MenuSelector page)
                {
                    page.AddChild(new RewindRenderingOption());
                },
                InstallRuntime,
                UninstallRuntime));
        }

        internal static void Register()
        {
            MoreItemsApi.Register(new ConsumableDefinition(
                Id,
                "Rewinder",
                "Rewinds the current or most recently completed jump.",
                delegate { return controller != null && controller.CanRewind(); },
                delegate { return controller != null && controller.BeginRewind(); },
                new Color(66, 220, 255),
                "Rewinders",
                new ConsumableHotkey("Rewinder", 82),
                ConsumableArt.DrawRewinder,
                true,
                null,
                delegate { return MoreItemsApi.IsModuleEnabled(Id); }));
            MoreItemsApi.RegisterMerchantOffer(
                "rewinder-pack",
                Id,
                10,
                JumpKing.MiscEntities.WorldItems.Items.GhostFragment,
                1);
            JKRuntime.UI.UIApi.RegisterDebugAction(new JKRuntime.UI.UiDebugActionDefinition(
                "more-items.add-10-rewinders",
                "More Items",
                "Add 10 Rewinders",
                delegate
                {
                    MoreItemsApi.Add(Id, 10);
                },
                delegate { return MoreItemsApi.IsModuleEnabled(Id); }));
        }

        private static void InstallRuntime()
        {
            UninstallRuntime();
            EntityComponent.EntityManager manager =
                EntityComponent.EntityManager.instance;
            JumpKing.Player.PlayerEntity player =
                manager == null ? null : manager.Find<JumpKing.Player.PlayerEntity>();
            if (player == null) return;
            controller = new RewinderController(player);
        }

        private static void UninstallRuntime()
        {
            if (controller != null && controller.IsAlive) controller.Destroy();
            controller = null;
        }
    }
}
