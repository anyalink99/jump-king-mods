namespace MorphBallMod
{
    public sealed class MorphDoubleJumpOption : BallToggleOption
    {
        public MorphDoubleJumpOption(
            bool showScreenRestriction,
            object menuFactory)
            : base(
                "Double jump",
                GetInitialValue(),
                showScreenRestriction,
                menuFactory)
        {
        }

        protected override bool IsRestrictedOnCurrentScreen
        {
            get
            {
                return BallKingMapRules.OverridesDoubleJump(
                    JumpKing.Camera.CurrentScreen);
            }
        }

        protected override void OnToggle()
        {
            if (ScreenRestrictionActive)
            {
                return;
            }
            Options.DoubleJump.Set(toggle);
        }

        private static bool GetInitialValue()
        {
            SettingsStore.EnsureLoaded();
            return SettingsStore.Current.EnableDoubleJump;
        }
    }
}
