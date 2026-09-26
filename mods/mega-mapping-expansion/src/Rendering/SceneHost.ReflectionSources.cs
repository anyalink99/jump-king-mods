using System;
using System.Collections.Generic;
using JumpKing;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MegaMappingExpansion
{
    internal sealed partial class SceneHost
    {
        private sealed class ReflectionSource
        {
            internal int Screen;
            internal string Selection;
            internal HashSet<string> Objects;
            internal RenderTarget2D Target;
        }
        private readonly Dictionary<string, ReflectionSource> reflectionSources = new Dictionary<string, ReflectionSource>(StringComparer.Ordinal);
        private void PrepareReflectionSources()
        {
            foreach (WaterData water in scene.Waters) PrepareReflectionSource(water.Screen, water.ReflectionObjects);
            foreach (PuddleData puddle in scene.Puddles) PrepareReflectionSource(puddle.Screen, puddle.ReflectionObjects);
        }
        private void PrepareReflectionSource(int screen, string selection)
        {
            if (selection == null) return;
            string key = screen + "/" + selection;
            if (reflectionSources.ContainsKey(key)) return;
            reflectionSources.Add(key, new ReflectionSource { Screen = screen, Selection = selection,
                Objects = new HashSet<string>(ReflectionSelection.Parse(selection), StringComparer.Ordinal),
                Target = new RenderTarget2D(Game1.instance.GraphicsDevice, 480, 360, false, SurfaceFormat.Color, DepthFormat.None) });
        }
        private RenderTarget2D ReflectionFrame(int screen, string selection)
        { return selection == null ? compositeTarget : reflectionSources[screen + "/" + selection].Target; }
        // Called with no open SpriteBatch, before the destination frame is rebuilt.
        // Rerender selected objects rather than trying to erase them from flattened pixels.
        private void DrawReflectionSources(ScreenRenderPlan plan)
        {
            if (plan == null) return;
            foreach (ReflectionSource source in reflectionSources.Values)
            {
                if (source.Screen != Camera.CurrentScreenIndex1 || !NeedsReflectionSource(plan, source.Selection)) continue;
                Game1.instance.GraphicsDevice.SetRenderTarget(source.Target);
                Game1.instance.GraphicsDevice.Clear(Color.Transparent);
                Game1.instance.StartBatch();
                try
                {
                    DrawReflectionObjects(plan.Background, source.Objects);
                    DrawReflectionObjects(plan.World, source.Objects);
                    foreach (string id in source.Objects) NativeNarrative.DrawReflection(id);
                    if (source.Objects.Contains("player"))
                    {
                        var player = GameLoopPlayer(); Sprite sprite; Texture2D texture; Rectangle region; SpriteEffects flip;
                        if (TryPlayerSprite(player, out sprite, out texture, out region, out flip))
                        {
                            Vector2 anchor = Camera.TransformVector2(player.m_body.Position + new Vector2(9, 26));
                            Game1.spriteBatch.Draw(texture, anchor, region, sprite.GetColor(), 0, region.Size.ToVector2() * sprite.center, 1, flip, 0);
                        }
                    }
                    DrawReflectionObjects(plan.Foreground, source.Objects);
                }
                finally { Game1.instance.EndBatch(); }
            }
        }
        private void DrawReflectionObjects(RenderCommand[] commands, HashSet<string> selected)
        { foreach (RenderCommand command in commands) if (selected.Contains(command.Id) && Visible(command)) command.Draw(); }
        private bool NeedsReflectionSource(ScreenRenderPlan plan, string selection)
        {
            foreach (WaterData water in plan.CompositeWaters)
                if (water.ReflectionObjects == selection && !hiddenIds.Contains(water.Id) && water.ReflectionOpacity > 0) return true;
            foreach (PuddleData puddle in plan.Puddles)
                if (puddle.ReflectionObjects == selection && !hiddenIds.Contains(puddle.Id) && puddle.Opacity > 0) return true;
            return false;
        }
    }
}
