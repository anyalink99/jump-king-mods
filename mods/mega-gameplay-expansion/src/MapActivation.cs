using JKRuntime.Gameplay;

namespace MegaGameplayExpansion
{
    internal static class MapActivation
    {
        internal static void Register()
        {
            MapMechanics.Register("mega-gameplay-expansion", "mega.warp", delegate { if (Settings.Current.WarpJump) Settings.Edit(p => p.WarpJump = false); });
            MapMechanics.Register("mega-gameplay-expansion", "mega.no-walk-off", delegate { if (Settings.Current.NoWalkOff) Settings.Edit(p => p.NoWalkOff = false); });
            MapMechanics.Register("mega-gameplay-expansion", "mega.air-dash", delegate { if (Settings.Current.AirDash) Settings.Edit(p => p.AirDash = false); });
        }
        internal static MapMechanicDecision Warp(bool local)
        { return MapMechanics.Current("mega.warp", Settings.Current.WarpJump, local, MapPixels.Warp.Screens.Contains(JumpKing.Camera.CurrentScreen)); }
        internal static MapMechanicDecision Dash(bool local)
        { return MapMechanics.Current("mega.air-dash", Settings.Current.AirDash, local, MapPixels.AirDash.Screens.Contains(JumpKing.Camera.CurrentScreen)); }
        internal static MapMechanicDecision Edge(bool local)
        { return MapMechanics.Current("mega.no-walk-off", Settings.Current.NoWalkOff, local, MapPixels.NoWalkOff.Screens.Contains(JumpKing.Camera.CurrentScreen)); }
        internal static string Id(string variant)
        { return variant == "warp-jump" ? "mega.warp" : variant == "no-walk-off" ? "mega.no-walk-off" : "mega.air-dash"; }
    }
}
