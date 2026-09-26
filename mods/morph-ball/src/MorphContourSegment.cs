using System;
using System.Collections.Generic;
using JumpKing.Level;
using JumpKing.Player;
using Microsoft.Xna.Framework;

namespace MorphBallMod
{
    internal struct MorphContourSegment
    {
        internal IBlock Block;
        internal Vector2 Start;
        internal Vector2 End;
        internal Vector2 Normal;
        // The configuration-space hull also contains faces swept out by a
        // body corner around a block vertex. Sticky follows the complete
        // hull, but a regular rolling ball may use only a real upper face.
        // Equivalent faces contributed by adjacent blocks are merged below.
        internal bool RollingSurface;

        internal Vector2 Tangent
        {
            get { return SurfaceMotion.GetTangent(Normal); }
        }
    }
}
