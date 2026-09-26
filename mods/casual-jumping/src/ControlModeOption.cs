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
            return true;
        }

        protected override string CurrentOptionName()
        {
            return "Controls: " + GetDisplayName((ControlMode)CurrentOption);
        }

        protected override void OnOptionChange(int option)
        {
            ControlMode mode = (ControlMode)option;
            Options.Controls.Set(mode);
        }

        private static int GetInitialOption()
        {
            SettingsStore.EnsureLoaded();
            return (int)SettingsStore.Current.Mode;
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
