namespace MegaMappingExpansion
{
    internal static class GroundProjection
    {
        // Inverse projected height. A back light sends the upright silhouette
        // towards the camera (+screen Y), not vertically behind the character.
        internal static float Height(float y,float plane,float scale) { return (y-plane)/scale; }
        internal static float X(float left,float height,float shear) { return left+height*shear; }
    }
}
