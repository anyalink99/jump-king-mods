using JKRuntime.Gameplay;

namespace MorphBallMod
{
    internal static class MapActivation
    {
        internal const string Id = "ball-king.form";
        internal static void Register()
        {
            MapMechanics.Register("morph-ball", Id, delegate {
                SettingsStore.EnsureLoaded(); if (SettingsStore.Current.EnableMorphBall) SettingsStore.SetEnabled(false);
            }, new MapMechanicParameter("sticky", "true", "false"), new MapMechanicParameter("double-jump", "inherit", "on", "off"),
                new MapMechanicParameter("jump-profile", "standard", "restricted"), new MapMechanicParameter("jump-height", 0.05, 2));
        }
        internal static MapMechanicDecision Current
        { get { SettingsStore.EnsureLoaded(); return MapMechanics.Current(Id, SettingsStore.Current.EnableMorphBall, false, BallKingMapRules.RestrictionFor(JumpKing.Camera.CurrentScreen) != BallKingScreenRestriction.None); } }
        internal static bool DoubleJump
        { get { string mode = Current.Parameter("double-jump", "inherit"); return mode == "on" || (mode == "inherit" && BallKingMapRules.DoubleJumpEnabled(JumpKing.Camera.CurrentScreen, SettingsStore.Current.EnableDoubleJump)); } }
        internal static float JumpHeight(bool doubleJump)
        {
            var rule = Current;
            float scale = rule.Parameter("jump-profile") == "restricted" ? (doubleJump ? 0.35f : 0.75f)
                : rule.Parameter("jump-profile") == "standard" ? 1 : BallKingMapRules.JumpHeightScale(JumpKing.Camera.CurrentScreen, doubleJump);
            return rule.Number("jump-height", scale);
        }
    }
}
