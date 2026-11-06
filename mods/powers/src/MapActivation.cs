using JKRuntime.Gameplay;

namespace Powers
{
    internal static class MapActivation
    {
        internal static bool AllowsMap()
        {
            var level = JumpKing.Game1.instance.contentManager.level;
            return level != null && level.Info.Tags != null && System.Array.IndexOf(level.Info.Tags, "AllowPowers") >= 0;
        }
        internal static void Register()
        {
            MapMechanics.Register("powers", "powers.warp", delegate { if (Settings.Current.WarpJump) Settings.Edit(p => p.WarpJump = false); });
            MapMechanics.Register("powers", "powers.air-dash", delegate { if (Settings.Current.AirDash) Settings.Edit(p => p.AirDash = false); });
        }
        internal static MapMechanicDecision Warp(bool local)
        { return MapMechanics.Current("powers.warp", Settings.Current.WarpJump, local, MapPixels.Warp.Screens.Contains(JumpKing.Camera.CurrentScreen)); }
        internal static MapMechanicDecision Dash(bool local)
        { return MapMechanics.Current("powers.air-dash", Settings.Current.AirDash, local, MapPixels.AirDash.Screens.Contains(JumpKing.Camera.CurrentScreen)); }

        internal static string Id(string variant)
        { return variant == "warp-jump" ? "powers.warp" : "powers.air-dash"; }
    }
}
