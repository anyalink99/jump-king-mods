using JumpKing.PauseMenu.BT.Actions;

namespace CasualJumping
{
    public sealed class ControlModeOption : IOptions
    {
        public ControlModeOption()
            : base(3, GetInitialOption(), EdgeMode.Wrap)
        {
        }

        protected override bool CanChange()
        {
            return JKRuntime.Gameplay.MapMechanics.CanConfigure(MapActivation.Id);
        }

        protected override string CurrentOptionName()
        {
            return JKRuntime.Gameplay.MapMechanics.IsControlled(MapActivation.Id) ? "Controls: Map" : "Controls: " + GetDisplayName((ControlMode)CurrentOption);
        }

        protected override void OnOptionChange(int option)
        {
            ControlMode mode = (ControlMode)option;
            Options.Controls.Set(mode);
        }

        private static int GetInitialOption()
        {
            SettingsStore.EnsureLoaded();
            return SettingsStore.Current.Enabled ? (int)SettingsStore.Current.Mode : 0;
        }

        private static string GetDisplayName(ControlMode mode)
        {
            if (mode == ControlMode.CasualPlus)
            {
                return "Casual+";
            }
            return mode.ToString();
        }
    }
}
