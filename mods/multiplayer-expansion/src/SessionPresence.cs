using Microsoft.Xna.Framework;

namespace MultiplayerExpansion
{
    internal static class SessionPresence
    {
        internal static bool Active(bool alive,bool currentBody,bool sameMap,bool paused,double bodyAge)
        { return alive && currentBody && sameMap && (paused || bodyAge<.15); }

        internal static void Freeze(InteractionFrame frame,bool paused)
        {
            if(!paused || !frame.Active) return;
            // pause keeps a solid body, without predicting motion or carrying stale impacts
            frame.Presence=PlayerPresence.Paused;frame.Velocity=Vector2.Zero;
            frame.Support=frame.SupportEpoch=0;frame.SupportOffset=Vector2.Zero;
            frame.Impacts=ContactImpact.None;
        }
    }
}
