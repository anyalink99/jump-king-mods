using JumpKing.Player;
using Microsoft.Xna.Framework;
namespace SubframeCharge
{
    // retained discovery entry point; Runtime owns the actual draw pose
    internal static class PlayerPresentation
    {
        internal static void Reset() { JKRuntime.Presentation.SmoothRendering.Reset(); }
        internal static void AfterUpdate() { SubframePhysics.AfterNativeUpdate(); }
        internal static Vector2 Position(BodyComp body) { return JKRuntime.Presentation.SmoothRendering.Position(body); }
    }
}
