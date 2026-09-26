using System.Collections.Generic;
using JumpKing;
using Microsoft.Xna.Framework;

namespace MegaMappingExpansion
{
    internal sealed partial class SceneHost
    {
        private readonly Dictionary<PropData, Vector2> gazeOffsets = new Dictionary<PropData, Vector2>();
        private readonly Dictionary<int, ScreenLook> screenLooks = new Dictionary<int, ScreenLook>();
        private static readonly ScreenLook DefaultLook = new ScreenLook();

        private ScreenLook CurrentLook
        {
            get { ScreenLook look; return screenLooks.TryGetValue(Camera.CurrentScreenIndex1, out look) ? look : DefaultLook; }
        }

        private void UpdateGazes(int current)
        {
            var player = GameLoopPlayer();
            if (player == null) return;
            Vector2 target = Camera.TransformVector2(player.m_body.GetHitbox().Center.ToVector2());
            UpdateGazes(work.Props.At(current), current, target);
        }

        private void UpdateGazes(PropData[] props, int current, Vector2 target)
        {
            if (props == null) return;
            foreach (PropData prop in props)
            {
                if (!prop.LookAtKing || prop.Screen != current) continue;
                Vector2 desired = GazeMotion.Target(new Vector2(prop.X, prop.Y), target, prop.GazeX, prop.GazeY);
                Vector2 offset;
                if (!gazeOffsets.TryGetValue(prop, out offset)) offset = desired;
                gazeOffsets[prop] = GazeMotion.Advance(offset, desired, prop.GazeResponse, frameDelta);
            }
        }
    }
}
