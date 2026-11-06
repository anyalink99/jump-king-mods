using JKRuntime.Gameplay;

namespace NoWalkOff
{
    internal static class MapActivation
    {
        internal static void Register()
        {
            MapMechanics.Register("no-walk-off", "no-walk-off", delegate { if (Settings.Current.NoWalkOff) Settings.Edit(p => p.NoWalkOff = false); });
        }


        internal static MapMechanicDecision Edge(bool local)
        { return MapMechanics.Current("no-walk-off", Settings.Current.NoWalkOff, local, MapPixels.NoWalkOff.Screens.Contains(JumpKing.Camera.CurrentScreen)); }
        internal static string Id(string variant)
        { return "no-walk-off"; }
    }
}
