using System;
using JumpKing;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace HammerKing
{
    internal sealed class HammerSprite : Sprite
    {
        private Sprite king;
        internal Sprite Source { get { return king; } }
        private readonly HammerPhysics physics;
        internal HammerSprite(Sprite sourceSprite, HammerPhysics state)
        {
            physics = state;
            SetSource(sourceSprite);
        }
        internal void SetSource(Sprite sourceSprite)
        {
            king = sourceSprite;
            texture = king.texture; source = king.source; center = king.center;
        }
        public override void Draw(Vector2 feet, SpriteEffects effects = SpriteEffects.None)
        {
            if (physics.Ready) DrawHammer(Game1.spriteBatch, Game1.instance.contentManager.Pixel.texture,
                feet + new Vector2(0, -13), Camera.TransformVector2(physics.Head), physics.Contact);
            king.Draw(feet, effects);
        }
        public override void Draw(float x, float y, SpriteEffects effects = SpriteEffects.None) { Draw(new Vector2(x, y), effects); }
        public override void Draw(Point point, SpriteEffects effects = SpriteEffects.None) { Draw(point.ToVector2(), effects); }
        public override void Draw(Rectangle rectangle, SpriteEffects effects = SpriteEffects.None) { Draw(rectangle.Center.ToVector2(), effects); }

        internal static void DrawHammer(SpriteBatch batch, Texture2D pixel, Vector2 pivot, Vector2 head, bool contact, Color? tint=null)
        {
            Vector2 direction = head - pivot;
            float length = direction.Length();
            if (length < 0.1f) return;
            direction /= length;
            Vector2 normal = new Vector2(-direction.Y, direction.X);
            Vector2 tail = HammerPhysics.HandleTail(pivot, head);
            Line(batch, pixel, tail, head, HammerPhysics.HandleRadius * 2, Tinted(new Color(42, 27, 23),tint));
            Line(batch, pixel, tail, head, 1, Tinted(new Color(157, 105, 54),tint));
            // the cross-head silhouette is distinct at the native resolution
            Line(batch, pixel, head - normal * 3.5f, head + normal * 3.5f, 4, Tinted(new Color(25, 28, 34),tint));
            Line(batch, pixel, head - normal * 2.5f, head + normal * 2.5f, 2, Tinted(new Color(117, 132, 143),tint));
            Line(batch, pixel, head - normal * 2f - direction, head + normal * 2f - direction, 1, Tinted(new Color(215, 226, 222),tint));
            if (contact) Line(batch, pixel, head + normal * 2f, head + normal * 3.5f, 1, Tinted(new Color(255, 218, 127),tint));
        }

        private static Color Tinted(Color color,Color? tint) {return tint.HasValue?new Color(color.ToVector4()*tint.Value.ToVector4()):color;}

        private static void Line(SpriteBatch batch, Texture2D pixel, Vector2 start, Vector2 end, float width, Color color)
        {
            Vector2 line = end - start;
            batch.Draw(pixel, start, new Rectangle(0, 0, 1, 1), color,
                (float)Math.Atan2(line.Y, line.X), new Vector2(0, 0.5f),
                new Vector2(line.Length(), width), SpriteEffects.None, 0f);
        }
    }
}
