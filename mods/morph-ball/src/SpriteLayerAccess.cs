using System;
using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using JumpKing;

namespace MorphBallMod
{
    internal static class SpriteLayerAccess
    {
        internal static IList GetLayersOrSelf(Sprite sprite)
        { return JKRuntime.Presentation.PlayerAppearance.Layers(sprite); }

        internal static int GetSignature(Sprite sprite)
        { return JKRuntime.Presentation.PlayerAppearance.Signature(sprite); }

        internal static bool IsJetpackLayer(Sprite sprite)
        {
            return (JKRuntime.Presentation.PlayerAppearance.RoleOf(sprite) & JKRuntime.Presentation.AppearanceLayerRole.ExcludeFromForm)!=0;
        }
    }
}
