using JKRuntime.Modules;
using JKRuntime.Gameplay;
using JumpKing.PauseMenu;

namespace CasualJumping
{
    [RuntimeModule("casual-jumping", "Casual Jumping", Provides = new[] { "player.movement:1:0" })]
    public static class ModEntry
    {
        private static System.IDisposable movement;
        private sealed class Rules : JKRuntime.Gameplay.IMovementRules
        {
            public JKRuntime.Gameplay.MovementMode Mode { get { return (JKRuntime.Gameplay.MovementMode)SettingsStore.Current.Mode; } }
        }
        [BeforeLevelLoad]
        public static void BeforeLevelLoad()
        {
            SettingsStore.EnsureLoaded();
        }

        [OnLevelStart]
        public static void OnLevelStart(JKRuntime.ModuleContext context)
        {
            var rules = new Rules();
            movement = context.Track(JKRuntime.Gameplay.GameFeatures.RegisterMovement(rules));
            context.Track(JKRuntime.RuntimeApi.Mechanics.Register("casual-jumping",
                new MechanicDefinition("casual.controls", new System.Version(1, 0), MechanicEffects.Movement | MechanicEffects.Charge,
                    MechanicComposition.Exclusive, "player.controller"), delegate {
                    string owner = PlayerControl.Owner(JumpKing.GameManager.GameLoop.m_player == null ? null : JumpKing.GameManager.GameLoop.m_player.m_body);
                    bool enabled = rules.Mode != MovementMode.Vanilla, available = !GameFeatures.IsMorphed && !JumpSlot.ChargePolicySuspended && owner == null;
                    return new MechanicState(enabled, available, enabled && available, MechanicSource.Setting,
                        !available ? "Controller suspended: " + (owner ?? "native control reservation") : rules.Mode.ToString());
                }));
            context.Publish("player.movement", rules);
            CasualControllerInstaller.ApplyCurrentMode();
        }

        [OnLevelUnload]
        public static void OnLevelUnload()
        {
            CasualControllerInstaller.Uninstall();
            if (movement != null) { movement.Dispose(); movement = null; }
        }

        [OnLevelEnd]
        public static void OnLevelEnd()
        {
            OnLevelUnload();
        }

        [MainMenuItemSetting]
        public static ControlModeOption MainMenuOption(object factory, GuiFormat format)
        {
            return new ControlModeOption();
        }

        [PauseMenuItemSetting]
        public static ControlModeOption PauseMenuOption(object factory, GuiFormat format)
        {
            return new ControlModeOption();
        }

    }
}
