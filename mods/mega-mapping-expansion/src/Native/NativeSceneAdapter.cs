using System;
using System.Reflection;
using JumpKing;
using JumpKing.Player;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MegaMappingExpansion
{
    internal static class NativeSceneAdapter
    {
        internal static bool FacingLeft(PlayerEntity player)
        { return ((SpriteEffects)PlayerFlip.GetValue(player) & SpriteEffects.FlipHorizontally) != 0; }
        private static readonly FieldInfo PlayerSprite = typeof(PlayerEntity).GetField("m_sprite", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo PlayerFlip = typeof(PlayerEntity).GetField("m_flip", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo SpriteTexture = typeof(Sprite).GetField("texture", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly FieldInfo SpriteSource = typeof(Sprite).GetField("source", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly Type LayeredSpriteType = typeof(Sprite).Assembly.GetType("JumpKing.XnaWrappers.LayeredSprite",true);
        private static readonly PropertyInfo Layers = LayeredSpriteType.GetProperty("Sprites",BindingFlags.Instance | BindingFlags.Public);
        private static readonly Type PauseType = typeof(Game1).Assembly.GetType("JumpKing.PauseMenu.PauseManager", true);
        private static readonly FieldInfo PauseInstance = PauseType.GetField("instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly MethodInfo ExitToMenu = PauseType.GetMethod("OnExitToMenu", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly MethodInfo Resume = PauseType.GetMethod("Resume", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly PropertyInfo SavedPosition = typeof(Game1).Assembly.GetType("JumpKing.SaveThread.SaveLube", true)
            .GetProperty("PlayerPosition", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly FieldInfo DebugPosition = typeof(JumpGame).GetField("m_debug_position", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        internal static void AssertContracts()
        {
            if(Layers==null || Layers.PropertyType!=typeof(System.Collections.Generic.List<Sprite>))
                throw new InvalidOperationException("Installed Jump King layered sprite contract is incompatible");
            if (PlayerSprite == null || PlayerSprite.FieldType != typeof(Sprite) || PlayerFlip == null || PlayerFlip.FieldType != typeof(SpriteEffects)
                || SpriteTexture == null || SpriteTexture.FieldType != typeof(Texture2D) || SpriteSource == null || SpriteSource.FieldType != typeof(Rectangle))
                throw new InvalidOperationException("Installed Jump King player sprite contract is incompatible");
            if (PauseInstance == null || ExitToMenu == null || ExitToMenu.GetParameters().Length != 0 || Resume == null
                || Resume.GetParameters().Length != 0 || SavedPosition == null || DebugPosition == null)
                throw new InvalidOperationException("Installed Jump King debug restart contract is incompatible");
        }
        internal static bool TrySprite(PlayerEntity player, out Sprite sprite, out Texture2D texture, out Rectangle source, out SpriteEffects flip)
        {
            sprite = null; texture = null; source = Rectangle.Empty; flip = SpriteEffects.None;
            if (player == null) return false;
            sprite = PlayerSprite.GetValue(player) as Sprite;
            if (sprite == null) return false;
            texture = SpriteTexture.GetValue(sprite) as Texture2D;
            source = (Rectangle)SpriteSource.GetValue(sprite); flip = (SpriteEffects)PlayerFlip.GetValue(player);
            return texture != null;
        }
        internal static System.Collections.Generic.List<Sprite> SpriteParts(Sprite sprite)
        {
            return LayeredSpriteType.IsInstanceOfType(sprite) ? (System.Collections.Generic.List<Sprite>)Layers.GetValue(sprite,null) : null;
        }
        internal static void PreviewScreen(int screen, float x = 64f, float y = 220f)
        {
            PlayerEntity player = JumpKing.GameManager.GameLoop.m_player;
            if (player == null) return;
            player.m_body.Position = new Vector2(x, y - (screen - 1) * 360);
            player.m_body.Velocity = Vector2.Zero;
            Camera.UpdateCamera(player.m_body.GetHitbox().Center);
        }
        internal static void RestartPreview()
        {
            // Same native restart-state transition used by the pause menu. This
            // inspector does not delete saves or synthesize module callbacks.
            DebugPosition.SetValue(Game1.instance.m_game, SavedPosition.GetValue(null, null));
            Game1.instance.m_game.m_restart_state = true;
            ExitToMenu.Invoke(PauseInstance.GetValue(null), null);
        }

        internal static void ResumePreview()
        { Resume.Invoke(PauseInstance.GetValue(null), null); }
    }
}
