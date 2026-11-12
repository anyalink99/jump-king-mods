using JumpKing.Player;
using Microsoft.Xna.Framework;
namespace SmoothCamera
{
    internal static class SubjectSource
    {
        internal static Vector2 Read(BodyComp body)
        { return JKRuntime.Presentation.SmoothRendering.Position(body); }
    }
}
