namespace MorphBallMod
{
    public sealed class MorphBallOption : BallToggleOption
    {
        public MorphBallOption()
            : base("Enable Ball King", GetInitialValue())
        {
        }

        protected override void OnToggle()
        {
            Options.Enabled.Set(toggle);
        }
        protected override bool CanChange() { return JKRuntime.Gameplay.MapMechanics.CanConfigure(MapActivation.Id); }

        private static bool GetInitialValue()
        {
            SettingsStore.EnsureLoaded();
            return SettingsStore.Current.EnableMorphBall;
        }
    }
}
