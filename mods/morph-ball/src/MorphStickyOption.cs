namespace MorphBallMod
{
    public sealed class MorphStickyOption : BallToggleOption
    {
        public MorphStickyOption(
            bool showScreenRestriction,
            object menuFactory)
            : base(
                "Sticky",
                GetInitialValue(),
                showScreenRestriction,
                menuFactory)
        {
        }

        protected override bool IsRestrictedOnCurrentScreen
        {
            get
            {
                return BallKingMapRules.DisablesSticky(
                    JumpKing.Camera.CurrentScreen);
            }
        }

        protected override void OnToggle()
        {
            if (ScreenRestrictionActive)
            {
                return;
            }
            Options.Sticky.Set(toggle);
        }

        private static bool GetInitialValue()
        {
            SettingsStore.EnsureLoaded();
            return SettingsStore.Current.StickyMode;
        }
    }
}
