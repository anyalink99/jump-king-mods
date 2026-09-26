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
            if (layers != null)
            {
                return layers;
            }
            return new Sprite[] { sprite };
        }

        internal static int GetSignature(Sprite sprite)
        {
            unchecked
            {
                int hash = 17;
                IList layers = GetLayersOrSelf(sprite);
                foreach (object value in layers)
                {
                    Sprite layer = value as Sprite;
                    if (layer == null)
                    {
                        continue;
                    }
                    hash = hash * 31 + RuntimeHelpers.GetHashCode(layer);
                    hash = hash * 31 + (layer.texture == null
                        ? 0
                        : RuntimeHelpers.GetHashCode(layer.texture));
                    hash = hash * 31 + layer.source.GetHashCode();
                    hash = hash * 31 + layer.center.GetHashCode();
                    hash = hash * 31 + layer.GetColor().GetHashCode();
                }
                return hash;
            }
        }

        internal static bool IsJetpackLayer(Sprite sprite)
        {
            if (sprite == null)
            {
                return false;
            }
            Type type = sprite.GetType();
            string assemblyName = type.Assembly.GetName().Name;
            return string.Equals(
                assemblyName,
                "Jetpack",
                StringComparison.OrdinalIgnoreCase)
                && type.FullName != null
                && type.FullName.StartsWith(
                    "JumpKingJetpack.",
                    StringComparison.Ordinal);
        }
    }
}
