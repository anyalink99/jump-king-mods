using System;
using System.Collections;
using System.Reflection;
using JumpKing;

namespace JumpKingJetpack
{
    internal static class SpriteLayerAccess
    {
        internal static IList GetLayers(Sprite sprite)
        {
            if (sprite == null)
            {
                throw new ArgumentNullException("sprite");
            }
            PropertyInfo property = sprite.GetType().GetProperty(
                "Sprites",
                BindingFlags.Instance | BindingFlags.Public);
            IList layers = property == null
                ? null
                : property.GetValue(sprite, null) as IList;
            if (layers == null)
            {
                throw new InvalidOperationException(
                    "Jump King sprite layers are unavailable");
            }
            return layers;
        }
    }
}
