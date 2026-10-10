using JKRuntime.Modules;
using JKRuntime.Gameplay;
using JumpKing.PauseMenu;
using JKRuntime.UI;

namespace MoreItems
{
    [RuntimeModule("more-items", "More Items", Requires = new[] { "player.form:1:0:optional" }, Provides = new[] { "player.thrust:1:0" })]
    public static class ModEntry
    {
        private static System.IDisposable thrust;
        internal static JumpKingJetpack.JetpackSound PreparedJetpackSound;
        internal static HammerKing.HammerSound PreparedHammerSound;
        [OnWorldReady]
        public static void PrepareWorld(JKRuntime.RuntimeScope scope)
        {
            JKRuntime.Presentation.PlayerVisuals.Prepare(scope);
            JKRuntime.Particles.ParticleWorlds.PrepareNative(scope);
            using (JKRuntime.RuntimeApi.MeasureStartup("more-items.audio"))
            {
                var jetpack = scope.Own(new JumpKingJetpack.JetpackSound());
                PreparedJetpackSound = jetpack;
                scope.Defer(delegate { if (object.ReferenceEquals(PreparedJetpackSound, jetpack)) PreparedJetpackSound = null; });
                var hammer = scope.Own(new HammerKing.HammerSound());
                PreparedHammerSound = hammer;
                scope.Defer(delegate { if (object.ReferenceEquals(PreparedHammerSound, hammer)) PreparedHammerSound = null; });
            }
        }
        private sealed class Thrust : JKRuntime.Gameplay.IAirThrust
        {
            public bool Enabled { get { return JetpackDefinition.IsEnabledForPlayer(); } }
            public bool Active { get { return JumpKingJetpack.ThrustState.Active; } }
        }
        [BeforeLevelLoad]
        public static void BeforeLevelLoad()
        {
            SettingsStore.EnsureLoaded();
            MapActivation.Register();
            RewinderDefinition.RegisterModule();
            JetpackDefinition.RegisterModule();
            HammerDefinition.RegisterModule();
            RewinderDefinition.Register();
            JetpackDefinition.Register();
            HammerDefinition.Register();
            MerchantCurrencies.Register();
            UIApi.RegisterMerchant(BargainburgMerchantCatalog.CreateDefinition());
        }

        [BeforeAttempt]
        public static void PrepareAttempt(JKRuntime.RuntimeScope scope)
        {
            if (PreparedJetpackSound != null) scope.Defer(PreparedJetpackSound.Stop);
            if (PreparedHammerSound != null) scope.Defer(PreparedHammerSound.Stop);
            ItemWorldLoader.Prepare(scope, JumpKing.Game1.instance.contentManager.root);
            // read only. new-run pickup reset and durable inventory changes stay
            // at activation, so cancelling an intro can't rewrite a save
            try { ItemInventory.Prepare(scope); }
            catch (System.Exception error) { System.Console.WriteLine("[More Items] Inventory preparation deferred: " + error.Message); }
        }

        [OnLevelStart]
        public static void OnLevelStart(JKRuntime.ModuleContext context)
        {
            var provider = new Thrust();
            thrust = context.Track(JKRuntime.Gameplay.GameFeatures.RegisterThrust(provider));
            context.Track(JKRuntime.RuntimeApi.Mechanics.RegisterControlled("more-items",
                new MechanicDefinition("more-items.jetpack", new System.Version(1, 0), MechanicEffects.Movement,
                    MechanicComposition.Additive, "player.thrust"), delegate {
                    return new MechanicState(provider.Enabled, true, provider.Enabled && provider.Active, MechanicSource.Equipment,
                        provider.Active ? "Air thrust active" : provider.Enabled ? "Equipped; waiting for airborne release and repress" : "Not equipped or module disabled");
                }));
            context.Track(JKRuntime.RuntimeApi.Mechanics.RegisterControlled("more-items", "more-items.rewinder", new System.Version(1, 0),
                MechanicEffects.Movement | MechanicEffects.Time, RewinderDefinition.Describe));
            context.Publish("player.thrust", provider);
            HammerKing.HammerInstaller.Register(context);
            MoreItemsRuntime.Install();
            if (JKRuntime.Gameplay.MapMechanics.HasRules("more-items.jetpack") || JKRuntime.Gameplay.MapMechanics.HasRules("more-items.hammer"))
                context.Track(JKRuntime.Gameplay.MapMechanics.Watch("more-items", MapActivation.EquipmentState, MapActivation.Refresh));
        }

        [OnLevelUnload]
        public static void OnLevelUnload() { MoreItemsRuntime.Uninstall(); if (thrust != null) { thrust.Dispose(); thrust = null; } }

        [OnLevelEnd]
        public static void OnLevelEnd() { OnLevelUnload(); }

        [MainMenuItemSetting]
        public static ItemModuleToggleOption MainMenuRewinders(object factory, GuiFormat format)
        { return MoreItemsApi.CreateModuleToggle(RewinderDefinition.Id); }

        [PauseMenuItemSetting]
        public static ItemModuleToggleOption PauseMenuRewinders(object factory, GuiFormat format)
        { return MoreItemsApi.CreateModuleToggle(RewinderDefinition.Id); }

        [MainMenuItemSetting]
        public static ItemModuleToggleOption MainMenuJetpack(object factory, GuiFormat format)
        { return MoreItemsApi.CreateModuleToggle(JetpackDefinition.Id); }

        [PauseMenuItemSetting]
        public static ItemModuleToggleOption PauseMenuJetpack(object factory, GuiFormat format)
        { return MoreItemsApi.CreateModuleToggle(JetpackDefinition.Id); }

        [MainMenuItemSetting]
        public static ItemModuleToggleOption MainMenuHammer(object factory, GuiFormat format)
        { return MoreItemsApi.CreateModuleToggle(HammerDefinition.Id); }

        [PauseMenuItemSetting]
        public static ItemModuleToggleOption PauseMenuHammer(object factory, GuiFormat format)
        { return MoreItemsApi.CreateModuleToggle(HammerDefinition.Id); }

        [MainMenuItemSetting]
        public static JumpKing.PauseMenu.BT.TextButton MainMenuBindings(object factory, GuiFormat format)
        {
            // Resolve on open: other providers can register items after native menu construction.
            return new JumpKing.PauseMenu.BT.TextButton("Binds", UIApi.CreateMenuPage(factory,
                new UiPageStack(pages => new UiBindingsPage("Binds", null,
                    MoreItemsApi.GetHotkeyIds()) { ShowList = true })));
        }

        [PauseMenuItemSetting]
        public static JumpKing.PauseMenu.BT.TextButton PauseMenuBindings(object factory, GuiFormat format)
        { return MainMenuBindings(factory, format); }

        [MainMenuItemSetting]
        public static JumpKing.PauseMenu.BT.TextButton MainMenuItemsSettings(object factory, GuiFormat format)
        {
            return new JumpKing.PauseMenu.BT.TextButton(
                "Items Settings",
                ItemModuleRegistry.CreateSettingsGrid(factory, format));
        }

        [PauseMenuItemSetting]
        public static JumpKing.PauseMenu.BT.TextButton PauseMenuItemsSettings(object factory, GuiFormat format)
        {
            return new JumpKing.PauseMenu.BT.TextButton(
                "Items Settings",
                ItemModuleRegistry.CreateSettingsGrid(factory, format));
        }
    }
}
