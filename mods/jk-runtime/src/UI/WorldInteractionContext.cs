using EntityComponent;
using JumpKing.Player;
using Microsoft.Xna.Framework;

namespace JKRuntime.UI
{
    public static class WorldInteractionContext
    {
        private static PlayerEntity player;

        public static PlayerEntity Player
        {
            get
            {
                if (player == null || !player.IsAlive)
                    player = EntityManager.instance.Find<PlayerEntity>();
                return player;
            }
        }

        internal static void BeginFrame()
        {
            player = EntityManager.instance.Find<PlayerEntity>();
        }

        public static bool PlayerIntersectsScreenArea(int screen, Rectangle localArea)
        {
            PlayerEntity current = Player;
            if (current == null || screen < 1 || JumpKing.Camera.CurrentScreenIndex1 != screen) return false;
            Rectangle hitbox = current.m_body.GetHitbox();
            Vector2 topLeft = JumpKing.Camera.TransformVector2(new Vector2(hitbox.Left, hitbox.Top));
            Vector2 bottomRight = JumpKing.Camera.TransformVector2(new Vector2(hitbox.Right, hitbox.Bottom));
            Rectangle localHitbox = new Rectangle(
                (int)topLeft.X,
                (int)topLeft.Y,
                System.Math.Max(1, (int)(bottomRight.X - topLeft.X)),
                System.Math.Max(1, (int)(bottomRight.Y - topLeft.Y)));
            return localHitbox.Intersects(localArea);
        }
    }
}
